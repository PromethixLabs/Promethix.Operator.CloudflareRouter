using System.Text.Json.Serialization;

namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Cloudflare;

internal sealed class CloudflareRuleset
{
    public string? Id { get; set; }

    public string Name { get; set; } = "Promethix Cloudflare Tunnel Operator rate limits";

    public string Kind { get; set; } = "zone";

    public string Phase { get; set; } = "http_ratelimit";

    public List<CloudflareRulesetRule> Rules { get; set; } = [];
}

internal sealed class CloudflareRulesetRule
{
    public string? Id { get; set; }

    public string Action { get; set; } = string.Empty;

    public string Expression { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CloudflareRateLimit? Ratelimit { get; set; }
}

internal sealed class CloudflareRateLimit
{
    public IReadOnlyCollection<string> Characteristics { get; set; } = ["cf.colo.id", "ip.src"];

    public int Period { get; set; }

    public int RequestsPerPeriod { get; set; }

    public int MitigationTimeout { get; set; }
}

