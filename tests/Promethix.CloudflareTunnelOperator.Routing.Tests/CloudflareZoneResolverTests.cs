using FluentAssertions;
using Microsoft.Extensions.Options;
using Promethix.CloudflareTunnelOperator.Routing.Integrations.Cloudflare;

namespace Promethix.CloudflareTunnelOperator.Routing.Tests;

public sealed class CloudflareZoneResolverTests
{
    [Fact]
    public void ResolveZoneIdShouldUseLegacyZoneIdWhenNoMappingsExist()
    {
        var resolver = CreateResolver(options => options.ZoneId = "legacy-zone");

        var zoneId = resolver.ResolveZoneId("api.example.com");

        _ = zoneId.Should().Be("legacy-zone");
    }

    [Fact]
    public void ResolveZoneIdShouldUseLongestMatchingSuffix()
    {
        var resolver = CreateResolver(options =>
        {
            options.ZoneMappings.Add(new CloudflareZoneMapping
            {
                HostnameSuffix = "example.com",
                ZoneId = "zone-root",
            });
            options.ZoneMappings.Add(new CloudflareZoneMapping
            {
                HostnameSuffix = "apps.example.com",
                ZoneId = "zone-apps",
            });
        });

        var zoneId = resolver.ResolveZoneId("whoami.apps.example.com");

        _ = zoneId.Should().Be("zone-apps");
    }

    [Fact]
    public void ResolveZoneIdShouldRejectUnmappedHostnameWhenMappingsExist()
    {
        var resolver = CreateResolver(options =>
        {
            options.ZoneId = "legacy-zone";
            options.ZoneMappings.Add(new CloudflareZoneMapping
            {
                HostnameSuffix = "example.com",
                ZoneId = "zone-root",
            });
        });

        var act = () => resolver.ResolveZoneId("whoami.other.net");

        _ = act.Should().Throw<InvalidOperationException>()
            .WithMessage("Hostname 'whoami.other.net' does not match any configured Cloudflare zone mapping.");
    }

    private static CloudflareZoneResolver CreateResolver(Action<CloudflareTunnelOptions>? configure = null)
    {
        var options = new CloudflareTunnelOptions();
        configure?.Invoke(options);
        return new CloudflareZoneResolver(Options.Create(options));
    }
}
