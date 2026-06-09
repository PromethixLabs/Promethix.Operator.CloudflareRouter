namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

public sealed class TunnelDirectTargetSpec
{
    public Uri Url { get; set; } = new("http://localhost");

    public string Protocol { get; set; } = "http";
}
