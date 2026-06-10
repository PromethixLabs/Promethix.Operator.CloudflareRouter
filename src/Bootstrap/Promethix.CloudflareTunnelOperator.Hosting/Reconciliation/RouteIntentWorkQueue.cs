using Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;
using System.Threading.Channels;

namespace Promethix.CloudflareTunnelOperator.Hosting.Reconciliation;

internal sealed class RouteIntentWorkQueue
{
    private readonly Channel<RouteIntentWorkItem> workItems = Channel.CreateUnbounded<RouteIntentWorkItem>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    private readonly Lock syncRoot = new();
    private readonly HashSet<TunnelPublicHostnameResourceKey> pendingResources = [];
    private bool fullResyncPending;

    public void EnqueueFullResync(string reason)
    {
        lock (syncRoot)
        {
            if (fullResyncPending)
            {
                return;
            }

            fullResyncPending = true;
        }

        _ = workItems.Writer.TryWrite(new RouteIntentWorkItem(RouteIntentWorkItemKind.FullResync, NormalizeReason(reason)));
    }

    public void EnqueueResource(TunnelPublicHostnameResourceKey key, string reason)
    {
        lock (syncRoot)
        {
            if (fullResyncPending || !pendingResources.Add(key))
            {
                return;
            }
        }

        _ = workItems.Writer.TryWrite(new RouteIntentWorkItem(RouteIntentWorkItemKind.Resource, NormalizeReason(reason), key));
    }

    public async ValueTask<RouteIntentWorkItem> WaitAsync(TimeSpan fallbackInterval, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(fallbackInterval, TimeSpan.Zero);

        var readTask = workItems.Reader.ReadAsync(cancellationToken).AsTask();
        var delayTask = Task.Delay(fallbackInterval, cancellationToken);
        var completedTask = await Task.WhenAny(readTask, delayTask).ConfigureAwait(false);

        if (completedTask == readTask)
        {
            var item = await readTask.ConfigureAwait(false);

            lock (syncRoot)
            {
                if (item.Kind == RouteIntentWorkItemKind.FullResync)
                {
                    fullResyncPending = false;
                    pendingResources.Clear();
                }
                else if (item.ResourceKey is { } key)
                {
                    _ = pendingResources.Remove(key);
                }
            }

            return item;
        }

        await delayTask.ConfigureAwait(false);
        return new RouteIntentWorkItem(RouteIntentWorkItemKind.FullResync, "interval");
    }

    private static string NormalizeReason(string reason)
    {
        return string.IsNullOrWhiteSpace(reason) ? "unspecified" : reason;
    }
}
