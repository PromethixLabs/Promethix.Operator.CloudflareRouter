using k8s;

namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

public static class KubernetesClientFactory
{
    public static IKubernetes Create()
    {
        var configuration = KubernetesClientConfiguration.IsInCluster()
            ? KubernetesClientConfiguration.InClusterConfig()
            : KubernetesClientConfiguration.BuildDefaultConfig();

        return new k8s.Kubernetes(configuration);
    }
}
