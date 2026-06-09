namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

public sealed class TunnelIngressTargetSpec
{
    public string ClassName { get; set; } = string.Empty;

    public TunnelIngressServiceTargetSpec? Service { get; set; }
}
