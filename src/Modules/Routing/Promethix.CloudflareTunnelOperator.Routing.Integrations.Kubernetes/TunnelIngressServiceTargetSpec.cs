namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

public sealed class TunnelIngressServiceTargetSpec
{
    public string Name { get; set; } = string.Empty;

    public string Namespace { get; set; } = string.Empty;

    public int Port { get; set; }

    public string Scheme { get; set; } = "https";
}
