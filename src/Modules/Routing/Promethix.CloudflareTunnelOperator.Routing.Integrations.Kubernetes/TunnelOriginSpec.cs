namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

public sealed class TunnelOriginSpec
{
    public Uri Url { get; set; } = new("http://localhost");

    public string Protocol { get; set; } = "http";
}
