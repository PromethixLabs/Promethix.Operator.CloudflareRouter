namespace Promethix.CloudflareTunnelOperator.Routing.Domain;

public sealed record HostnameSecurityPolicy(
    string Hostname,
    string OwnershipTag,
    IReadOnlyCollection<HostnameRateLimitRule> RateLimitRules)
{
    public bool HasRules => RateLimitRules.Count > 0;
}
