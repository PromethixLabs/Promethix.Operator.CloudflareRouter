using Promethix.CloudflareTunnelOperator.Routing.Domain;

namespace Promethix.CloudflareTunnelOperator.Routing.Application;

public interface IRouteMutationSafetyEvaluator
{
    Task<RouteMutationSafetyDecision> EvaluateFullReconcileAsync(
        RoutingOperatorOptions options,
        RouteIntentDocument intent,
        RoutePlan plan,
        CancellationToken cancellationToken);

    Task<RouteMutationSafetyDecision> EvaluateManagedRouteReconcileAsync(
        RoutingOperatorOptions options,
        ManagedRouteIntent intent,
        RoutePlan plan,
        CancellationToken cancellationToken);

    Task<RouteMutationSafetyDecision> EvaluateCleanupAsync(
        RoutingOperatorOptions options,
        string hostname,
        RoutePlan plan,
        CancellationToken cancellationToken);
}
