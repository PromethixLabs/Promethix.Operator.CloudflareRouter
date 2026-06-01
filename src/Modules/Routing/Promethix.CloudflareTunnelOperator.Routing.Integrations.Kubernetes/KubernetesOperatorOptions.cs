namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

public sealed class KubernetesOperatorOptions
{
    public const string SectionName = "KubernetesOperator";

    public string ManagedClassName { get; set; } = "public";

    public string ManagedTunnelName { get; set; } = "delta-public";
}
