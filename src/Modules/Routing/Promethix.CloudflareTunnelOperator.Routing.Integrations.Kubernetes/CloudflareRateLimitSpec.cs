namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

public sealed class CloudflareRateLimitSpec
{
    public bool Enabled { get; set; }

    public IReadOnlyCollection<CloudflareRateLimitRuleSpec> Rules { get; set; } = [];
}
