namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

public sealed class TunnelPublicHostnameSpec
{
    public string ClassName { get; set; } = string.Empty;

    public string Hostname { get; set; } = string.Empty;

    public TunnelReferenceSpec TunnelRef { get; set; } = new();

    public TunnelOriginSpec Origin { get; set; } = new();

    public CloudflareRouteSpec Cloudflare { get; set; } = new();

    public bool Enabled { get; set; } = true;
}
