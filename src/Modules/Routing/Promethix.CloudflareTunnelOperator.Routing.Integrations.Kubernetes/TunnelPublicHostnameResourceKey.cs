namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

public readonly record struct TunnelPublicHostnameResourceKey(string Namespace, string Name)
{
    public override string ToString()
    {
        return $"{Namespace}/{Name}";
    }
}
