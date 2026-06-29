namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

public enum IngressServiceOverrideMode
{
    Disabled = 0,
    ConfiguredTargetOnly = 1,
    Any = 2,
}
