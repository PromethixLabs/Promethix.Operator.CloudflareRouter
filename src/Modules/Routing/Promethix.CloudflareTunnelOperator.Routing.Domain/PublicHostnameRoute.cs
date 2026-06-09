namespace Promethix.CloudflareTunnelOperator.Routing.Domain;

public sealed record PublicHostnameRoute(
    string Hostname,
    Uri OriginService,
    RouteProtocol Protocol,
    string OwnershipTag,
    string? OriginServerName)
{
    public static PublicHostnameRoute Create(
        string hostname,
        Uri originService,
        RouteProtocol protocol,
        string ownershipTag,
        string? originServerName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);
        ArgumentNullException.ThrowIfNull(originService);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownershipTag);

        if (!string.Equals(originService.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(originService.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only HTTP and HTTPS origin services are currently supported.", nameof(originService));
        }

        var normalizedOriginServerName = string.IsNullOrWhiteSpace(originServerName)
            ? null
            : originServerName.Trim();

        return new PublicHostnameRoute(
            hostname.Trim(),
            originService,
            protocol,
            ownershipTag.Trim(),
            normalizedOriginServerName);
    }
}
