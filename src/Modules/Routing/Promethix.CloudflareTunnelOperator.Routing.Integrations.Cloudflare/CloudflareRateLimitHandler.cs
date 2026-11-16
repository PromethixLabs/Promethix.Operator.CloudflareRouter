namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Cloudflare;

public sealed class CloudflareRateLimitHandler(CloudflareRequestGate gate) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return gate.SendAsync(token => base.SendAsync(request, token), cancellationToken);
    }
}
