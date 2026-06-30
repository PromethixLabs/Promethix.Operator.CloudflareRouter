using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Promethix.CloudflareTunnelOperator.Routing.Application;
using Promethix.CloudflareTunnelOperator.Routing.Domain;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Cloudflare;

public sealed class CloudflareHostnameSecurityPolicyClient(
    HttpClient httpClient,
    IOptions<CloudflareTunnelOptions> options,
    ILogger<CloudflareHostnameSecurityPolicyClient> logger) : ICloudflareHostnameSecurityPolicyClient
{
    private const string RateLimitPhase = "http_ratelimit";
    private const string ManagedPrefix = "promethix-cloudflare-tunnel-operator";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Action<ILogger, string, int, int, int, bool, Exception?> LogRateLimitPlan =
        LoggerMessage.Define<string, int, int, int, bool>(
            LogLevel.Information,
            new EventId(1100, "CloudflareRateLimitPlan"),
            "Cloudflare rate-limit policy plan for {Hostname}: create {CreateCount}, update {UpdateCount}, delete {DeleteCount}, apply {ApplyChanges}.");

    private static readonly Action<ILogger, int, string, Exception?> LogCloudflareWriteRejected =
        LoggerMessage.Define<int, string>(
            LogLevel.Error,
            new EventId(1101, "CloudflareRulesetWriteRejected"),
            "Cloudflare rejected ruleset update with status {StatusCode}. Response body: {ResponseBody}");

    public async Task<SecurityPolicyPlan> ReconcileAsync(
        HostnameSecurityPolicy policy,
        bool applyChanges,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(policy);
        EnsureZoneConfigured();

        var ruleset = await GetRateLimitRulesetAsync(cancellationToken).ConfigureAwait(false);
        var plan = BuildPlan(policy, ruleset.Rules);

        LogRateLimitPlan(logger, policy.Hostname, plan.ToCreate.Count, plan.ToUpdate.Count, plan.ToDelete.Count, applyChanges, null);

        if (applyChanges && plan.HasChanges && plan.Conflicts.Count == 0)
        {
            ApplyPlanToRuleset(policy, ruleset, plan);
            await SaveRulesetAsync(ruleset, cancellationToken).ConfigureAwait(false);
        }

        return plan;
    }

    public async Task<SecurityPolicyPlan> CleanupAsync(
        string hostname,
        string ownershipTag,
        bool applyChanges,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownershipTag);
        EnsureZoneConfigured();

        var ruleset = await GetRateLimitRulesetAsync(cancellationToken).ConfigureAwait(false);
        var ownedRules = ruleset.Rules
            .Where(rule => IsManagedRuleForHostname(rule, hostname, ownershipTag))
            .Select(ToDomainRule)
            .ToArray();
        var plan = new SecurityPolicyPlan(hostname, [], [], ownedRules, []);

        LogRateLimitPlan(logger, hostname, 0, 0, plan.ToDelete.Count, applyChanges, null);

        if (applyChanges && plan.HasChanges)
        {
            _ = ruleset.Rules.RemoveAll(rule => IsManagedRuleForHostname(rule, hostname, ownershipTag));
            await SaveRulesetAsync(ruleset, cancellationToken).ConfigureAwait(false);
        }

        return plan;
    }

    private static SecurityPolicyPlan BuildPlan(HostnameSecurityPolicy policy, IReadOnlyCollection<CloudflareRulesetRule> actualRules)
    {
        var desiredByName = policy.RateLimitRules.ToDictionary(rule => rule.Name, StringComparer.OrdinalIgnoreCase);
        var actualManagedByName = actualRules
            .Where(rule => IsManagedRuleForHostname(rule, policy.Hostname, policy.OwnershipTag))
            .ToDictionary(GetManagedRuleName, StringComparer.OrdinalIgnoreCase);

        var toCreate = new List<HostnameRateLimitRule>();
        var toUpdate = new List<HostnameRateLimitRule>();
        var toDelete = new List<HostnameRateLimitRule>();
        var conflicts = new List<RouteConflict>();

        foreach (var desired in policy.RateLimitRules)
        {
            if (!actualManagedByName.TryGetValue(desired.Name, out var actual))
            {
                toCreate.Add(desired);
                continue;
            }

            if (!RuleMatches(desired, actual))
            {
                toUpdate.Add(desired);
            }
        }

        foreach (var actual in actualManagedByName)
        {
            if (!desiredByName.ContainsKey(actual.Key))
            {
                toDelete.Add(ToDomainRule(actual.Value));
            }
        }

        return new SecurityPolicyPlan(policy.Hostname, toCreate, toUpdate, toDelete, conflicts);
    }

    private static void ApplyPlanToRuleset(HostnameSecurityPolicy policy, CloudflareRuleset ruleset, SecurityPolicyPlan plan)
    {
        _ = ruleset.Rules.RemoveAll(
            rule => IsManagedRuleForHostname(rule, policy.Hostname, policy.OwnershipTag)
                && (plan.ToDelete.Any(delete => string.Equals(delete.Name, GetManagedRuleName(rule), StringComparison.OrdinalIgnoreCase))
                    || plan.ToUpdate.Any(update => string.Equals(update.Name, GetManagedRuleName(rule), StringComparison.OrdinalIgnoreCase))));

        foreach (var rule in plan.ToCreate.Concat(plan.ToUpdate))
        {
            ruleset.Rules.Add(ToCloudflareRule(policy, rule));
        }
    }

    private async Task<CloudflareRuleset> GetRateLimitRulesetAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(
            new Uri($"zones/{options.Value.ZoneId}/rulesets/phases/{RateLimitPhase}/entrypoint", UriKind.Relative),
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new CloudflareRuleset();
        }

        _ = response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<CloudflareRulesetResponse>(
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        return envelope?.Result ?? new CloudflareRuleset();
    }

    private async Task SaveRulesetAsync(CloudflareRuleset ruleset, CancellationToken cancellationToken)
    {
        var response = string.IsNullOrWhiteSpace(ruleset.Id)
            ? await httpClient.PostAsJsonAsync(
                new Uri($"zones/{options.Value.ZoneId}/rulesets", UriKind.Relative),
                ruleset,
                JsonOptions,
                cancellationToken).ConfigureAwait(false)
            : await httpClient.PutAsJsonAsync(
                new Uri($"zones/{options.Value.ZoneId}/rulesets/{ruleset.Id}", UriKind.Relative),
                ruleset,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        LogCloudflareWriteRejected(logger, (int)response.StatusCode, responseBody, null);
        _ = response.EnsureSuccessStatusCode();
    }

    private void EnsureZoneConfigured()
    {
        if (string.IsNullOrWhiteSpace(options.Value.ZoneId))
        {
            throw new InvalidOperationException("CloudflareTunnel:ZoneId is required when Cloudflare security policy reconciliation is enabled.");
        }
    }

    private static CloudflareRulesetRule ToCloudflareRule(HostnameSecurityPolicy policy, HostnameRateLimitRule rule)
    {
        return new CloudflareRulesetRule
        {
            Action = rule.Action,
            Expression = rule.Expression,
            Description = BuildDescription(policy.Hostname, policy.OwnershipTag, rule.Name),
            Enabled = true,
            Ratelimit = new CloudflareRateLimit
            {
                Period = rule.PeriodSeconds,
                RequestsPerPeriod = rule.RequestsPerPeriod,
                MitigationTimeout = rule.PeriodSeconds,
            },
        };
    }

    private static HostnameRateLimitRule ToDomainRule(CloudflareRulesetRule rule)
    {
        return new HostnameRateLimitRule(
            GetManagedRuleName(rule),
            rule.Expression,
            rule.Ratelimit?.RequestsPerPeriod ?? 0,
            rule.Ratelimit?.Period ?? 0,
            rule.Action);
    }

    private static bool RuleMatches(HostnameRateLimitRule desired, CloudflareRulesetRule actual)
    {
        return string.Equals(desired.Expression, actual.Expression, StringComparison.Ordinal)
            && string.Equals(desired.Action, actual.Action, StringComparison.Ordinal)
            && desired.RequestsPerPeriod == actual.Ratelimit?.RequestsPerPeriod
            && desired.PeriodSeconds == actual.Ratelimit?.Period;
    }

    private static string BuildDescription(string hostname, string ownershipTag, string ruleName)
    {
        return $"{ManagedPrefix}:{ownershipTag}:{hostname}:{ruleName}";
    }

    private static bool IsManagedRuleForHostname(CloudflareRulesetRule rule, string hostname, string ownershipTag)
    {
        var prefix = $"{ManagedPrefix}:{ownershipTag}:{hostname}:";
        return rule.Description.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetManagedRuleName(CloudflareRulesetRule rule)
    {
        var parts = rule.Description.Split(':');
        return parts.Length >= 4 ? parts[^1] : rule.Description;
    }
}
