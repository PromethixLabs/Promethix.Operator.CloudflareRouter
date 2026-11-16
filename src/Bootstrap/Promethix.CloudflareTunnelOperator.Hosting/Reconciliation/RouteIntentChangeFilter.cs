using k8s;
using Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;
using System.Text.Json;

namespace Promethix.CloudflareTunnelOperator.Hosting.Reconciliation;

internal sealed class RouteIntentChangeFilter
{
    private readonly Dictionary<TunnelPublicHostnameResourceKey, string> snapshots = [];

    public bool ShouldReconcile(WatchEventType eventType, TunnelPublicHostnameCustomResource resource)
    {
        if (eventType is WatchEventType.Bookmark or WatchEventType.Error)
        {
            return false;
        }

        var key = new TunnelPublicHostnameResourceKey(resource.Metadata.NamespaceProperty ?? string.Empty, resource.Metadata.Name ?? string.Empty);
        if (eventType == WatchEventType.Deleted)
        {
            _ = snapshots.Remove(key);
            return true;
        }

        var fingerprint = JsonSerializer.Serialize(new
        {
            resource.Metadata.Uid,
            resource.Metadata.Generation,
            resource.Metadata.DeletionTimestamp,
            Labels = resource.Metadata.Labels?.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            Annotations = resource.Metadata.Annotations?.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            Finalizers = resource.Metadata.Finalizers?.Order(StringComparer.Ordinal),
            resource.Metadata.OwnerReferences,
            resource.Spec,
        });
        var changed = !snapshots.TryGetValue(key, out var previous) || previous != fingerprint;
        snapshots[key] = fingerprint;
        return changed;
    }
}
