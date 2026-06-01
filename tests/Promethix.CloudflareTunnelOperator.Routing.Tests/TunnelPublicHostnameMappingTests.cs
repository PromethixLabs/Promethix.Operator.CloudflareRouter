using FluentAssertions;
using Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

namespace Promethix.CloudflareTunnelOperator.Routing.Tests;

public sealed class TunnelPublicHostnameMappingTests
{
    [Fact]
    public void SpecShouldDefaultToHttpHttpsFriendlyShape()
    {
        var resource = new TunnelPublicHostnameCustomResource
        {
            Spec = new TunnelPublicHostnameSpec
            {
                ClassName = "public",
                Hostname = "app.promethix.net",
                TunnelRef = new TunnelReferenceSpec { Name = "delta-public" },
                Origin = new TunnelOriginSpec
                {
                    Url = new Uri("https://app.demo.svc.cluster.local:8443"),
                    Protocol = "https",
                },
            },
        };

        resource.Spec.Enabled.Should().BeTrue();
        resource.Spec.Cloudflare.Proxied.Should().BeTrue();
        resource.Spec.TunnelRef.Name.Should().Be("delta-public");
    }
}
