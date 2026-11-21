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

    public int Depth { get { lock (syncRoot) { return fullResyncPending ? 1 : pendingResources.Count; } } }

    public void EnqueueFullResync(string reason)
    {
        lock (syncRoot)
        {
            if (fullResyncPending)
            {
                return;
            }

            fullResyncPending = true;
            _ = workItems.Writer.TryWrite(new RouteIntentWorkItem(RouteIntentWorkItemKind.FullResync, NormalizeReason(reason)));
        }
    }

    public void EnqueueResource(TunnelPublicHostnameResourceKey key, string reason)
    {
        lock (syncRoot)
        {
            if (fullResyncPending || !pendingResources.Add(key))
            {
                return;
            }
            if (pendingResources.Count > 256)
            {
                while (workItems.Reader.TryRead(out _)) { }
                pendingResources.Clear();
                fullResyncPending = true;
                _ = workItems.Writer.TryWrite(new RouteIntentWorkItem(RouteIntentWorkItemKind.FullResync, "queue-capacity"));
                return;
            }
            _ = workItems.Writer.TryWrite(new RouteIntentWorkItem(RouteIntentWorkItemKind.Resource, NormalizeReason(reason), key));
        }
    }

    public async ValueTask<RouteIntentWorkItem> WaitAsync(TimeSpan fallbackInterval, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(fallbackInterval, TimeSpan.Zero);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(fallbackInterval);
        try
        {
            var item = await workItems.Reader.ReadAsync(timeout.Token).ConfigureAwait(false);

            lock (syncRoot)
            {
                if (fullResyncPending || item.Kind == RouteIntentWorkItemKind.FullResync)
                {
                    item = new RouteIntentWorkItem(RouteIntentWorkItemKind.FullResync, item.Reason);
                    while (workItems.Reader.TryRead(out _))
                    {
                    }
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

        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }

        return new RouteIntentWorkItem(RouteIntentWorkItemKind.FullResync, "interval");
    }

    private static string NormalizeReason(string reason)
    {
        return string.IsNullOrWhiteSpace(reason) ? "unspecified" : reason;
    }
}
