namespace Promethix.CloudflareTunnelOperator.Routing.Application;

public interface ICloudflareZoneResolver
{
    string ResolveZoneId(string hostname);
}
