using Microsoft.Extensions.Options;
using Promethix.CloudflareTunnelOperator.Routing.Integrations.Cloudflare;

namespace Promethix.CloudflareTunnelOperator.Hosting.Options;

internal sealed class CloudflareTunnelOptionsValidator : IValidateOptions<CloudflareTunnelOptions>
{
    public ValidateOptionsResult Validate(string? name, CloudflareTunnelOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.AccountId))
        {
            failures.Add("CloudflareTunnel:AccountId is required.");
        }

        if (string.IsNullOrWhiteSpace(options.TunnelId))
        {
            failures.Add("CloudflareTunnel:TunnelId is required.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiToken))
        {
            failures.Add("CloudflareTunnel:ApiToken is required.");
        }

        var normalizedSuffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in options.ZoneMappings)
        {
            if (string.IsNullOrWhiteSpace(mapping.HostnameSuffix))
            {
                failures.Add("CloudflareTunnel:ZoneMappings[].HostnameSuffix is required.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(mapping.ZoneId))
            {
                failures.Add($"CloudflareTunnel:ZoneMappings['{mapping.HostnameSuffix}'].ZoneId is required.");
                continue;
            }

            var normalizedSuffix = mapping.HostnameSuffix.Trim().TrimStart('.').TrimEnd('.');
            if (!normalizedSuffixes.Add(normalizedSuffix))
            {
                failures.Add($"CloudflareTunnel:ZoneMappings contains a duplicate hostname suffix '{normalizedSuffix}'.");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
