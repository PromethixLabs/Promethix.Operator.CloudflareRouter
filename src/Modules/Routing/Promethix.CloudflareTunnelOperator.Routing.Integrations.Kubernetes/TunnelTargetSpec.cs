namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

public sealed class TunnelTargetSpec
{
    public string Mode { get; set; } = "ingress";

    public TunnelIngressTargetSpec? Ingress { get; set; }

    public TunnelDirectTargetSpec? Direct { get; set; }
}
