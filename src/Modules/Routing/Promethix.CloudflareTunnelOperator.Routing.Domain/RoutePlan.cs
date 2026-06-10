namespace Promethix.CloudflareTunnelOperator.Routing.Domain;

public sealed record RoutePlan(
    IReadOnlyCollection<PublicHostnameRoute> ToCreate,
    IReadOnlyCollection<PublicHostnameRoute> ToUpdate,
    IReadOnlyCollection<PublicHostnameRoute> ToDelete,
    IReadOnlyCollection<RouteConflict> Conflicts)
{
    public bool HasChanges => ToCreate.Count > 0 || ToUpdate.Count > 0 || ToDelete.Count > 0;
}
