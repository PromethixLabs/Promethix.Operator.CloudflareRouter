using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Promethix.CloudflareTunnelOperator.Routing.Domain;
using Promethix.CloudflareTunnelOperator.Routing.Integrations.Cloudflare;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Promethix.CloudflareTunnelOperator.Routing.Tests;

public sealed class CloudflareHostnameSecurityPolicyClientTests
{
    [Fact]
    public async Task ReconcileShouldPlanCreateWithoutApplyingInDryRun()
    {
        using var handler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.NotFound));
        using var httpClient = CreateHttpClient(handler);
        var client = CreateClient(httpClient);
        var policy = CreatePolicy();

        var plan = await client.ReconcileAsync(policy, applyChanges: false, CancellationToken.None);

        _ = plan.ToCreate.Should().ContainSingle();
        _ = plan.ToUpdate.Should().BeEmpty();
        _ = plan.ToDelete.Should().BeEmpty();
        _ = handler.Requests.Should().ContainSingle(request => request.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task ReconcileShouldCreateEntrypointRulesetWithManagedRule()
    {
        using var handler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.NotFound),
            JsonResponse("""{"success":true,"result":{"id":"ruleset-id"}}"""));
        using var httpClient = CreateHttpClient(handler);
        var client = CreateClient(httpClient);

        var plan = await client.ReconcileAsync(CreatePolicy(), applyChanges: true, CancellationToken.None);

        _ = plan.ToCreate.Should().ContainSingle();
        var write = handler.Requests.Should().ContainSingle(request => request.Method == HttpMethod.Post).Subject;
        _ = write.RequestUri!.ToString().Should().Be("https://api.cloudflare.com/client/v4/zones/zone-id/rulesets");
        var body = await write.Content!.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var rule = document.RootElement.GetProperty("rules")[0];

        _ = document.RootElement.GetProperty("phase").GetString().Should().Be("http_ratelimit");
        _ = rule.GetProperty("action").GetString().Should().Be("managed_challenge");
        _ = rule.GetProperty("expression").GetString().Should().Be("(http.host eq \"api.example.com\" and starts_with(http.request.uri.path, \"/v1/\"))");
        _ = rule.GetProperty("description").GetString().Should().Be("promethix-cloudflare-tunnel-operator:owner:api.example.com:api-v1");
        _ = rule.GetProperty("ratelimit").GetProperty("period").GetInt32().Should().Be(60);
        _ = rule.GetProperty("ratelimit").GetProperty("requests_per_period").GetInt32().Should().Be(60);
        _ = rule.GetProperty("ratelimit").GetProperty("mitigation_timeout").GetInt32().Should().Be(60);
    }

    [Fact]
    public async Task ReconcileShouldUpdateExistingManagedRuleWhenLimitChanges()
    {
        var existingRuleset = """
            {
              "success": true,
              "result": {
                "id": "ruleset-id",
                "name": "existing",
                "kind": "zone",
                "phase": "http_ratelimit",
                "rules": [
                  {
                    "id": "rule-id",
                    "action": "managed_challenge",
                    "expression": "(http.host eq \"api.example.com\" and starts_with(http.request.uri.path, \"/v1/\"))",
                    "description": "promethix-cloudflare-tunnel-operator:owner:api.example.com:api-v1",
                    "enabled": true,
                    "ratelimit": {
                      "period": 60,
                      "requests_per_period": 30,
                      "mitigation_timeout": 60,
                      "characteristics": ["cf.colo.id", "ip.src"]
                    }
                  }
                ]
              }
            }
            """;
        using var handler = new RecordingHttpMessageHandler(
            JsonResponse(existingRuleset),
            JsonResponse("""{"success":true,"result":{"id":"ruleset-id"}}"""));
        using var httpClient = CreateHttpClient(handler);
        var client = CreateClient(httpClient);

        var plan = await client.ReconcileAsync(CreatePolicy(), applyChanges: true, CancellationToken.None);

        _ = plan.ToUpdate.Should().ContainSingle();
        var write = handler.Requests.Should().ContainSingle(request => request.Method == HttpMethod.Put).Subject;
        _ = write.RequestUri!.ToString().Should().Be("https://api.cloudflare.com/client/v4/zones/zone-id/rulesets/ruleset-id");
        var body = await write.Content!.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        _ = document.RootElement.GetProperty("rules")[0].GetProperty("ratelimit").GetProperty("requests_per_period").GetInt32().Should().Be(60);
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://api.cloudflare.com/client/v4/"),
        };
    }

    private static CloudflareHostnameSecurityPolicyClient CreateClient(HttpClient httpClient)
    {
        return new CloudflareHostnameSecurityPolicyClient(
            httpClient,
            new CloudflareZoneResolver(
                Options.Create(new CloudflareTunnelOptions
                {
                    AccountId = "account-id",
                    TunnelId = "tunnel-id",
                    ZoneId = "zone-id",
                    ApiToken = "token",
                    OwnershipTag = "owner",
                })),
            NullLogger<CloudflareHostnameSecurityPolicyClient>.Instance);
    }

    private static HostnameSecurityPolicy CreatePolicy()
    {
        return new HostnameSecurityPolicy(
            "api.example.com",
            "zone-id",
            "owner",
            [
                new HostnameRateLimitRule(
                    "api-v1",
                    "(http.host eq \"api.example.com\" and starts_with(http.request.uri.path, \"/v1/\"))",
                    60,
                    60,
                    "managed_challenge"),
            ]);
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class RecordingHttpMessageHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses = new(responses);

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(CloneRequest(request));
            return Task.FromResult(this.responses.Count > 0
                ? this.responses.Dequeue()
                : new HttpResponseMessage(HttpStatusCode.OK));
        }

        private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri);

            if (request.Content is not null)
            {
                var content = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                clone.Content = new StringContent(content, Encoding.UTF8, request.Content.Headers.ContentType?.MediaType);
            }

            return clone;
        }
    }
}
