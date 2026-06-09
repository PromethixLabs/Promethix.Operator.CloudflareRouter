namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

public sealed class TunnelPublicHostnameSpec
{
    public string ClassName { get; set; } = string.Empty;

    public string Hostname { get; set; } = string.Empty;

    public TunnelReferenceSpec TunnelRef { get; set; } = new();

    public TunnelTargetSpec? Target { get; set; }

    // Compatibility shim for the original v1alpha1 direct-origin shape.
    public TunnelOriginSpec? Origin { get; set; }

    public CloudflareRouteSpec Cloudflare { get; set; } = new();

    public bool Enabled { get; set; } = true;
}
