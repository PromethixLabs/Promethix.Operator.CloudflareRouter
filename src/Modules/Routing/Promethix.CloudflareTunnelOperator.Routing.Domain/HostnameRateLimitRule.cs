namespace Promethix.CloudflareTunnelOperator.Routing.Domain;

public sealed record HostnameRateLimitRule(
    string Name,
    string Expression,
    int RequestsPerPeriod,
    int PeriodSeconds,
    string Action);
