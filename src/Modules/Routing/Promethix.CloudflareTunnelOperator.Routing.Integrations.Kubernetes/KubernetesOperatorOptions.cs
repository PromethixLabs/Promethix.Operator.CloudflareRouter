namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

public sealed class KubernetesOperatorOptions
{
    public const string SectionName = "KubernetesOperator";

    public string ManagedClassName { get; set; } = "public";

    public string ManagedTunnelName { get; set; } = "delta-public";

    public string ManagedFinalizerName { get; set; } = "edge.promethix.net/tunnelpublichostname-protection";

    public string OwnershipConfigMapNamespace { get; set; } = "edge-system";

    public string OwnershipConfigMapName { get; set; } = "promethix-cloudflare-tunnel-operator-ownership";
}
