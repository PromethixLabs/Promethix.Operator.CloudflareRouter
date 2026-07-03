using System.Collections.ObjectModel;

namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Cloudflare;

public sealed class CloudflareTunnelOptions
{
    public const string SectionName = "CloudflareTunnel";

    public string AccountId { get; set; } = string.Empty;

    public string TunnelId { get; set; } = string.Empty;

    public string ZoneId { get; set; } = string.Empty;

    public Collection<CloudflareZoneMapping> ZoneMappings { get; } = [];

    public string ApiToken { get; set; } = string.Empty;

    public string OwnershipTag { get; set; } = "promethix-cloudflare-tunnel-operator";
}

public sealed class CloudflareZoneMapping
{
    public string HostnameSuffix { get; set; } = string.Empty;

    public string ZoneId { get; set; } = string.Empty;
}
