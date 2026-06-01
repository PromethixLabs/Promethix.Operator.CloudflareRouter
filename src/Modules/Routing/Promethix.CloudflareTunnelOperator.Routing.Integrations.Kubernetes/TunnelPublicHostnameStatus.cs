using k8s.Models;

namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

public sealed class TunnelPublicHostnameStatus
{
    public long? ObservedGeneration { get; set; }

    public string? OwnershipTag { get; set; }

    public string? AppliedTunnelName { get; set; }

    public string? AppliedHostname { get; set; }

    public IList<V1Condition> Conditions { get; } = [];
}
