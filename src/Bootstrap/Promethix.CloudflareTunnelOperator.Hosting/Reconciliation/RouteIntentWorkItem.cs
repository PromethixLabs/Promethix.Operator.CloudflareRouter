using Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

namespace Promethix.CloudflareTunnelOperator.Hosting.Reconciliation;

internal enum RouteIntentWorkItemKind
{
    FullResync,
    Resource,
}

internal readonly record struct RouteIntentWorkItem(
    RouteIntentWorkItemKind Kind,
    string Reason,
    TunnelPublicHostnameResourceKey? ResourceKey = null);
