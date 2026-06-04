using Microsoft.Extensions.Options;
using Promethix.CloudflareTunnelOperator.Routing.Application;
using Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

namespace Promethix.CloudflareTunnelOperator.Hosting.Reconciliation;

internal sealed class OperatorWorker(
    RouteReconciler reconciler,
    IRouteIntentStatusUpdater statusUpdater,
    KubernetesTunnelPublicHostnameClient resourceClient,
    RouteIntentWorkQueue workQueue,
    IOptions<RoutingOperatorOptions> options,
    OperatorState state,
    ILogger<OperatorWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, string, int, int, int, int, bool, Exception?> LogReconciliationCompleted =
        LoggerMessage.Define<string, int, int, int, int, bool>(
            LogLevel.Information,
            new EventId(2000, "ReconciliationCompleted"),
            "Reconciliation completed from {Source}. Create {CreateCount}, update {UpdateCount}, delete {DeleteCount}, conflicts {ConflictCount}, applied {ChangesApplied}.");

    private static readonly Action<ILogger, Exception> LogReconciliationFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2001, "ReconciliationFailed"),
            "Reconciliation iteration failed.");

    private static readonly Action<ILogger, string, Exception?> LogReconciliationTriggered =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(2002, "ReconciliationTriggered"),
            "Starting reconciliation triggered by {TriggerReason}.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        workQueue.EnqueueFullResync("startup");

        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await workQueue
                .WaitAsync(TimeSpan.FromSeconds(options.Value.ReconciliationIntervalSeconds), stoppingToken)
                .ConfigureAwait(false);

            await RunIterationAsync(workItem, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunIterationAsync(RouteIntentWorkItem workItem, CancellationToken cancellationToken)
    {
        LogReconciliationTriggered(logger, workItem.Reason, null);

        try
        {
            await PrepareLifecycleAsync(workItem, cancellationToken).ConfigureAwait(false);

            var result = await reconciler.ReconcileAsync(options.Value, cancellationToken).ConfigureAwait(false);
            state.MarkReconciliationCompleted(result.CompletedAt);
            await EnsureManagedFinalizersAsync(result, cancellationToken).ConfigureAwait(false);
            await statusUpdater.UpdateAsync(result, failure: null, cancellationToken).ConfigureAwait(false);
            await FinalizeLifecycleAsync(workItem, cancellationToken).ConfigureAwait(false);

            LogReconciliationCompleted(
                logger,
                result.Intent.Source,
                result.Plan.ToCreate.Count,
                result.Plan.ToUpdate.Count,
                result.Plan.ToDelete.Count,
                result.Plan.Conflicts.Count,
                result.ChangesApplied,
                null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ReconciliationFailedException ex) when (ex.Intent is not null && ex.InnerException is not null)
        {
            await statusUpdater.UpdateFailureAsync(ex.Intent, ex.InnerException, cancellationToken).ConfigureAwait(false);
            LogReconciliationFailed(logger, ex.InnerException);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogReconciliationFailed(logger, ex);
        }
    }

    private async Task PrepareLifecycleAsync(RouteIntentWorkItem workItem, CancellationToken cancellationToken)
    {
        if (workItem.Kind != RouteIntentWorkItemKind.Resource || workItem.ResourceKey is not { } key)
        {
            return;
        }

        var resource = await resourceClient.GetAsync(key, cancellationToken).ConfigureAwait(false);

        if (resource is null || KubernetesTunnelPublicHostnameClient.IsDeleting(resource) || !resourceClient.IsManaged(resource))
        {
            return;
        }

        await resourceClient.EnsureFinalizerAsync(key, cancellationToken).ConfigureAwait(false);
    }

    private async Task FinalizeLifecycleAsync(RouteIntentWorkItem workItem, CancellationToken cancellationToken)
    {
        if (workItem.Kind != RouteIntentWorkItemKind.Resource || workItem.ResourceKey is not { } key)
        {
            return;
        }

        var resource = await resourceClient.GetAsync(key, cancellationToken).ConfigureAwait(false);

        if (resource is null)
        {
            return;
        }

        if (KubernetesTunnelPublicHostnameClient.IsDeleting(resource) || !resourceClient.IsManaged(resource))
        {
            await resourceClient.RemoveFinalizerAsync(key, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task EnsureManagedFinalizersAsync(ReconciliationResult result, CancellationToken cancellationToken)
    {
        foreach (var intent in result.Intent.ManagedRoutes)
        {
            await resourceClient.EnsureFinalizerAsync(
                new TunnelPublicHostnameResourceKey(intent.Namespace, intent.Name),
                cancellationToken).ConfigureAwait(false);
        }
    }
}
