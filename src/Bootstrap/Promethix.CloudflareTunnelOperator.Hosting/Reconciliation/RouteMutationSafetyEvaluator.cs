using Microsoft.Extensions.Logging;
using Promethix.CloudflareTunnelOperator.Routing.Application;
using Promethix.CloudflareTunnelOperator.Routing.Domain;

namespace Promethix.CloudflareTunnelOperator.Hosting.Reconciliation;

internal sealed class RouteMutationSafetyEvaluator(
    IManagedRouteOwnershipStore ownershipStore,
    OperatorState state,
    ILogger<RouteMutationSafetyEvaluator> logger) : IRouteMutationSafetyEvaluator
{
    private static readonly Action<ILogger, int, int, int, int, Exception?> LogFullInventorySummary =
        LoggerMessage.Define<int, int, int, int>(
            LogLevel.Information,
            new EventId(2010, "FullInventorySummary"),
            "Full inventory loaded. Managed desired routes {DesiredCount}, invalid routes {InvalidCount}, previously owned routes {PriorOwnedCount}, planned deletes {DeleteCount}.");

    private static readonly Action<ILogger, string, string, Exception?> LogMutationBlocked =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(2011, "MutationBlocked"),
            "Blocking Cloudflare mutation. Reason {Reason}. {Message}");

    public async Task<RouteMutationSafetyDecision> EvaluateFullReconcileAsync(
        RoutingOperatorOptions options,
        RouteIntentDocument intent,
        RoutePlan plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(plan);

        var mutationModeDecision = EvaluateMutationMode(options, plan);
        if (!mutationModeDecision.AllowApply)
        {
            LogBlocked(mutationModeDecision);
            return mutationModeDecision;
        }

        var ownership = await ownershipStore.GetOwnershipAsync(cancellationToken).ConfigureAwait(false);
        var priorOwnedCount = ownership.Count(pair => string.Equals(pair.Value, options.OwnershipTag, StringComparison.Ordinal));
        var desiredCount = intent.ManagedRoutes.Count;
        var invalidCount = intent.InvalidRoutes.Count;
        var deleteCount = plan.ToDelete.Count;

        LogFullInventorySummary(logger, desiredCount, invalidCount, priorOwnedCount, deleteCount, null);

        if (options.StartupProtectionEnabled && !state.HasObservedWatchActivity)
        {
            var decision = new RouteMutationSafetyDecision(
                false,
                "StartupInventoryIncomplete",
                "Cloudflare writes are blocked until the Kubernetes watch has reported initial activity.");
            LogBlocked(decision);
            return decision;
        }

        var shrinkDecision = EvaluateDeleteGuards(options, desiredCount, priorOwnedCount, deleteCount);
        if (!shrinkDecision.AllowApply)
        {
            LogBlocked(shrinkDecision);
            return shrinkDecision;
        }

        return new RouteMutationSafetyDecision(true);
    }

    public Task<RouteMutationSafetyDecision> EvaluateManagedRouteReconcileAsync(
        RoutingOperatorOptions options,
        ManagedRouteIntent intent,
        RoutePlan plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(plan);

        var mutationModeDecision = EvaluateMutationMode(options, plan);
        if (!mutationModeDecision.AllowApply)
        {
            LogBlocked(mutationModeDecision);
            return Task.FromResult(mutationModeDecision);
        }

        if (options.StartupProtectionEnabled && !state.IsStartupSafeForMutation)
        {
            var decision = new RouteMutationSafetyDecision(
                false,
                "StartupInventoryIncomplete",
                "Targeted reconciliation is blocked until an initial full inventory reconciliation completes safely.");
            LogBlocked(decision);
            return Task.FromResult(decision);
        }

        return Task.FromResult(new RouteMutationSafetyDecision(true));
    }

    public Task<RouteMutationSafetyDecision> EvaluateCleanupAsync(
        RoutingOperatorOptions options,
        string hostname,
        RoutePlan plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);
        ArgumentNullException.ThrowIfNull(plan);

        var mutationModeDecision = EvaluateMutationMode(options, plan);
        if (!mutationModeDecision.AllowApply)
        {
            LogBlocked(mutationModeDecision);
            return Task.FromResult(mutationModeDecision);
        }

        if (options.StartupProtectionEnabled && !state.IsStartupSafeForMutation)
        {
            var decision = new RouteMutationSafetyDecision(
                false,
                "StartupInventoryIncomplete",
                "Cleanup deletes are blocked until an initial full inventory reconciliation completes safely.");
            LogBlocked(decision);
            return Task.FromResult(decision);
        }

        return Task.FromResult(new RouteMutationSafetyDecision(true));
    }

    private static RouteMutationSafetyDecision EvaluateMutationMode(RoutingOperatorOptions options, RoutePlan plan)
    {
        var mode = ParseMutationMode(options.MutationMode);

        return mode switch
        {
            RouteMutationMode.CreateOnly when plan.ToUpdate.Count > 0 || plan.ToDelete.Count > 0 => new RouteMutationSafetyDecision(
                false,
                "MutationModeRestricted",
                "Mutation mode create-only blocks updates and deletes."),
            RouteMutationMode.CreateUpdateOnly when plan.ToDelete.Count > 0 => new RouteMutationSafetyDecision(
                false,
                "MutationModeRestricted",
                "Mutation mode create-update-only blocks deletes."),
            RouteMutationMode.Full => new RouteMutationSafetyDecision(true),
            _ => new RouteMutationSafetyDecision(true),
        };
    }

    private static RouteMutationSafetyDecision EvaluateDeleteGuards(
        RoutingOperatorOptions options,
        int desiredCount,
        int priorOwnedCount,
        int deleteCount)
    {
        if (deleteCount == 0)
        {
            return new RouteMutationSafetyDecision(true);
        }

        if (options.MaxDeleteCount >= 0 && deleteCount > options.MaxDeleteCount)
        {
            return new RouteMutationSafetyDecision(
                false,
                "ProtectiveDeleteGuardTriggered",
                $"Planned delete count {deleteCount} exceeds the configured maximum of {options.MaxDeleteCount}.");
        }

        if (priorOwnedCount > 0 && options.MaxDeletePercentage >= 0)
        {
            var shrinkPercentage = (priorOwnedCount - desiredCount) * 100 / priorOwnedCount;
            if (shrinkPercentage > options.MaxDeletePercentage)
            {
                return new RouteMutationSafetyDecision(
                    false,
                    "UnsafeDesiredState",
                    $"Desired managed route count shrank from {priorOwnedCount} to {desiredCount}, exceeding the configured maximum shrink of {options.MaxDeletePercentage}%.");
            }
        }

        return new RouteMutationSafetyDecision(true);
    }

    private static RouteMutationMode ParseMutationMode(string mutationMode)
    {
        return Enum.TryParse<RouteMutationMode>(mutationMode, ignoreCase: true, out var mode)
            ? mode
            : throw new InvalidOperationException($"Unsupported mutation mode '{mutationMode}'.");
    }

    private void LogBlocked(RouteMutationSafetyDecision decision)
    {
        LogMutationBlocked(
            logger,
            decision.Reason ?? "SafetyBlocked",
            decision.Message ?? "Cloudflare mutation blocked by operator safety policy.",
            null);
    }
}
