using Promethix.CloudflareTunnelOperator.Routing.Application;

namespace Promethix.CloudflareTunnelOperator.Hosting.Reconciliation;

internal sealed record RouteCleanupDisposition(
    bool ShouldRemoveFinalizer,
    bool IsBlocked,
    string Reason,
    string Message);

internal static class RouteCleanupDispositionEvaluator
{
    public static RouteCleanupDisposition Evaluate(
        RoutingOperatorOptions options,
        RouteCleanupResult cleanupResult)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(cleanupResult);

        return !cleanupResult.Plan.HasChanges || cleanupResult.ChangesApplied
            ? Completed()
            : !options.ApplyChanges
            ? Blocked("ApplyDisabled", "Managed route deletion is blocked because applyChanges=false.")
            : cleanupResult.ApplyBlocked
            ? Blocked(
                cleanupResult.ApplyBlockReason ?? "DeleteBlocked",
                cleanupResult.ApplyBlockMessage ?? "Managed route deletion is blocked by operator safety policy.")
            : Blocked(
                "DeleteNotApplied",
                "Managed route deletion could not be completed, so the finalizer is being retained.");
    }

    private static RouteCleanupDisposition Completed()
    {
        return new RouteCleanupDisposition(
            ShouldRemoveFinalizer: true,
            IsBlocked: false,
            Reason: "CleanedUp",
            Message: "Managed route cleanup completed.");
    }

    private static RouteCleanupDisposition Blocked(string reason, string message)
    {
        return new RouteCleanupDisposition(
            ShouldRemoveFinalizer: false,
            IsBlocked: true,
            Reason: reason,
            Message: message);
    }
}
