using Promethix.CloudflareTunnelOperator.Routing.Domain;

namespace Promethix.CloudflareTunnelOperator.Routing.Application;

public interface ICloudflareHostnameSecurityPolicyClient
{
    Task<SecurityPolicyPlan> ReconcileAsync(
        HostnameSecurityPolicy policy,
        bool applyChanges,
        CancellationToken cancellationToken);

    Task<SecurityPolicyPlan> CleanupAsync(
        string hostname,
        string ownershipTag,
        bool applyChanges,
        CancellationToken cancellationToken);
}
