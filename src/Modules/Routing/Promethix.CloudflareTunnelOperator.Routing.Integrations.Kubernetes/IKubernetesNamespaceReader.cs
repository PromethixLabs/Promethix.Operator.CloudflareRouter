using k8s.Models;

namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

public interface IKubernetesNamespaceReader
{
    Task<V1Namespace> ReadAsync(string namespaceName, CancellationToken cancellationToken);
}
