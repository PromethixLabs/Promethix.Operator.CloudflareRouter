using Promethix.CloudflareTunnelOperator.Routing.Application;

namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

public sealed class KubernetesRouteIntentSource(KubernetesTunnelPublicHostnameClient client) : IClusterRouteIntentSource
{
    public async Task<RouteIntentDocument> GetDesiredRoutesAsync(CancellationToken cancellationToken)
    {
        return await client.GetDesiredRoutesAsync(cancellationToken).ConfigureAwait(false);
    }
}
