namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

public sealed class CloudflareSecuritySpec
{
    public CloudflareRateLimitSpec? RateLimit { get; set; }
}
