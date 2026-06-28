using k8s;
using k8s.Autorest;
using k8s.Models;
using System.Net;

namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

public sealed class KubernetesNamespaceReader(IKubernetes kubernetes) : IKubernetesNamespaceReader
{
    public async Task<V1Namespace> ReadAsync(string namespaceName, CancellationToken cancellationToken)
    {
        try
        {
            return await kubernetes.CoreV1.ReadNamespaceAsync(
                namespaceName,
                pretty: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                $"Namespace '{namespaceName}' was not found while validating hostname ownership.",
                ex);
        }
    }
}
