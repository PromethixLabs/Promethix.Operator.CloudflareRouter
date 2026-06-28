namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

public interface IHostnameOwnershipValidator
{
    Task ValidateAsync(
        TunnelPublicHostnameCustomResource resource,
        CancellationToken cancellationToken);
}
