namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

public sealed class CloudflareRateLimitRuleSpec
{
    public string Name { get; set; } = string.Empty;

    public string? PathPrefix { get; set; }

    public string? Expression { get; set; }

    public int RequestsPerPeriod { get; set; }

    public int PeriodSeconds { get; set; }

    public string Action { get; set; } = "block";
}
