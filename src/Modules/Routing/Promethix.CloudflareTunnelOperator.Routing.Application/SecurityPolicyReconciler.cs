namespace Promethix.CloudflareTunnelOperator.Routing.Application;

public sealed class SecurityPolicyReconciler(ICloudflareHostnameSecurityPolicyClient securityPolicyClient)
{
    public async Task<SecurityPolicyReconciliationResult?> ReconcileAsync(
        RoutingOperatorOptions options,
        ManagedRouteIntent intent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(intent);

        if (intent.SecurityPolicy is null)
        {
            return null;
        }

        var plan = await securityPolicyClient
            .ReconcileAsync(intent.SecurityPolicy, options.ApplyChanges, cancellationToken)
            .ConfigureAwait(false);

        return new SecurityPolicyReconciliationResult(
            intent.SecurityPolicy,
            plan,
            options.ApplyChanges && plan.HasChanges && plan.Conflicts.Count == 0);
    }

    public async Task<SecurityPolicyCleanupResult> CleanupAsync(
        RoutingOperatorOptions options,
        string hostname,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);

        var plan = await securityPolicyClient
            .CleanupAsync(hostname, options.OwnershipTag, options.ApplyChanges, cancellationToken)
            .ConfigureAwait(false);

        return new SecurityPolicyCleanupResult(
            hostname,
            plan,
            options.ApplyChanges && plan.HasChanges && plan.Conflicts.Count == 0);
    }
}
