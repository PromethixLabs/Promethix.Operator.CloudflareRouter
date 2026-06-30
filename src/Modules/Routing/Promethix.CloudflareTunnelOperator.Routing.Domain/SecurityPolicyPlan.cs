namespace Promethix.CloudflareTunnelOperator.Routing.Domain;

public sealed record SecurityPolicyPlan(
    string Hostname,
    IReadOnlyCollection<HostnameRateLimitRule> ToCreate,
    IReadOnlyCollection<HostnameRateLimitRule> ToUpdate,
    IReadOnlyCollection<HostnameRateLimitRule> ToDelete,
    IReadOnlyCollection<RouteConflict> Conflicts)
{
    public bool HasChanges => ToCreate.Count > 0 || ToUpdate.Count > 0 || ToDelete.Count > 0;
}
