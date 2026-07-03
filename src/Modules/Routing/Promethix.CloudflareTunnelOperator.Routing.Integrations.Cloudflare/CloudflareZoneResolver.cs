using Microsoft.Extensions.Options;
using Promethix.CloudflareTunnelOperator.Routing.Application;

namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Cloudflare;

public sealed class CloudflareZoneResolver(IOptions<CloudflareTunnelOptions> options) : ICloudflareZoneResolver
{
    public string ResolveZoneId(string hostname)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);

        var normalizedHostname = NormalizeHostname(hostname);
        var mappings = options.Value.ZoneMappings
            .Where(mapping => !string.IsNullOrWhiteSpace(mapping.HostnameSuffix) && !string.IsNullOrWhiteSpace(mapping.ZoneId))
            .Select(mapping => new
            {
                Suffix = NormalizeSuffix(mapping.HostnameSuffix),
                ZoneId = mapping.ZoneId.Trim(),
            })
            .OrderByDescending(mapping => mapping.Suffix.Length)
            .ToArray();

        foreach (var mapping in mappings)
        {
            if (HostnameMatchesSuffix(normalizedHostname, mapping.Suffix))
            {
                return mapping.ZoneId;
            }
        }

        return mappings.Length == 0 && !string.IsNullOrWhiteSpace(options.Value.ZoneId)
            ? options.Value.ZoneId.Trim()
            : throw new InvalidOperationException(
                mappings.Length == 0
                    ? $"No Cloudflare zone is configured for hostname '{hostname}'. Configure CloudflareTunnel:ZoneMappings or the legacy CloudflareTunnel:ZoneId."
                    : $"Hostname '{hostname}' does not match any configured Cloudflare zone mapping.");
    }

    private static string NormalizeHostname(string hostname)
    {
        return hostname.Trim().TrimEnd('.').ToUpperInvariant();
    }

    private static string NormalizeSuffix(string suffix)
    {
        return suffix.Trim().Trim().TrimStart('.').TrimEnd('.').ToUpperInvariant();
    }

    private static bool HostnameMatchesSuffix(string hostname, string suffix)
    {
        return string.Equals(hostname, suffix, StringComparison.OrdinalIgnoreCase)
               || hostname.EndsWith($".{suffix}", StringComparison.OrdinalIgnoreCase);
    }
}
