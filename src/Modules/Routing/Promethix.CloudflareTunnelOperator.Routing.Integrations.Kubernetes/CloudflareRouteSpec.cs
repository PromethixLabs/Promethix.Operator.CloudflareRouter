namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

public sealed class CloudflareRouteSpec
{
    public bool Proxied { get; set; } = true;

    public CloudflareSecuritySpec? Security { get; set; }
}
