using FluentAssertions;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Promethix.CloudflareTunnelOperator.Hosting.Options;
using Promethix.CloudflareTunnelOperator.Routing.Application;
using Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

namespace Promethix.CloudflareTunnelOperator.Routing.Tests;

public sealed class TunnelPublicHostnameMappingTests
{
    [Fact]
    public void SpecShouldDefaultToIngressFriendlyShape()
    {
        var resource = new TunnelPublicHostnameCustomResource
        {
            Spec = new TunnelPublicHostnameSpec
            {
                ClassName = "public",
                Hostname = "app.promethix.net",
                TunnelRef = new TunnelReferenceSpec { Name = "delta-public" },
                Target = new TunnelTargetSpec
                {
                    Mode = "ingress",
                    Ingress = new TunnelIngressTargetSpec
                    {
                        ClassName = "traefik-cloudflare-tunnel",
                    },
                },
            },
        };

        resource.Spec.Enabled.Should().BeTrue();
        resource.Spec.Cloudflare.Proxied.Should().BeTrue();
        resource.Spec.TunnelRef.Name.Should().Be("delta-public");
        resource.Spec.Target.Mode.Should().Be("ingress");
        resource.Spec.Target.Ingress!.ClassName.Should().Be("traefik-cloudflare-tunnel");
    }

    [Fact]
    public void KubernetesOptionsShouldRequireIngressTargetSettings()
    {
        var validator = new KubernetesOperatorOptionsValidator();
        var options = new KubernetesOperatorOptions
        {
            ManagedClassName = "public",
            ManagedTunnelName = "delta-public",
            ManagedIngressClassName = "traefik-cloudflare-tunnel",
            IngressTargetUrl = new Uri("https://traefik-cloudflare-tunnel.edge-system.svc.cluster.local"),
            ManagedFinalizerName = "edge.promethix.net/tunnelpublichostname-protection",
            OwnershipConfigMapNamespace = "edge-system",
            OwnershipConfigMapName = "promethix-cloudflare-tunnel-operator-ownership",
        };

        var result = validator.Validate(Options.DefaultName, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task IngressTargetCanOverrideDefaultService()
    {
        var coreV1 = new Mock<ICoreV1Operations>(MockBehavior.Strict);
        coreV1
            .Setup(client => client.ReadNamespacedServiceAsync(
                "traefik-cloudflare-tunnel",
                "edge-system",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new V1Service
            {
                Spec = new V1ServiceSpec
                {
                    Ports =
                    [
                        new V1ServicePort
                        {
                            Port = 443,
                        }
                    ],
                },
            });

        var networkingV1 = new Mock<INetworkingV1Operations>(MockBehavior.Strict);
        networkingV1
            .Setup(client => client.ListIngressForAllNamespacesAsync(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                false,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new V1IngressList
            {
                Items =
                [
                    new V1Ingress
                    {
                        Spec = new V1IngressSpec
                        {
                            IngressClassName = "traefik-cloudflare-tunnel",
                            Rules =
                            [
                                new V1IngressRule
                                {
                                    Host = "whoami.delta.promethix.net",
                                }
                            ],
                        },
                    },
                ],
            });

        var kubernetes = new Mock<IKubernetes>(MockBehavior.Strict);
        kubernetes.SetupGet(client => client.CoreV1).Returns(coreV1.Object);
        kubernetes.SetupGet(client => client.NetworkingV1).Returns(networkingV1.Object);

        var client = new KubernetesTunnelPublicHostnameClient(
            kubernetes.Object,
            Options.Create(new KubernetesOperatorOptions
            {
                ManagedClassName = "public",
                ManagedTunnelName = "delta-public",
                ManagedIngressClassName = "traefik-cloudflare-tunnel",
                IngressTargetUrl = new Uri("https://default.edge-system.svc.cluster.local"),
                ManagedFinalizerName = "edge.promethix.net/tunnelpublichostname-protection",
                OwnershipConfigMapNamespace = "edge-system",
                OwnershipConfigMapName = "promethix-cloudflare-tunnel-operator-ownership",
            }),
            Options.Create(new RoutingOperatorOptions
            {
                OwnershipTag = "promethix-cloudflare-tunnel-operator",
            }),
            NullLogger<KubernetesTunnelPublicHostnameClient>.Instance);

        var resource = new TunnelPublicHostnameCustomResource
        {
            Metadata = new k8s.Models.V1ObjectMeta
            {
                Name = "whoami-public",
                NamespaceProperty = "demo",
            },
            Spec = new TunnelPublicHostnameSpec
            {
                ClassName = "public",
                Hostname = "whoami.delta.promethix.net",
                TunnelRef = new TunnelReferenceSpec { Name = "delta-public" },
                Target = new TunnelTargetSpec
                {
                    Mode = "ingress",
                    Ingress = new TunnelIngressTargetSpec
                    {
                        ClassName = "traefik-cloudflare-tunnel",
                        Service = new TunnelIngressServiceTargetSpec
                        {
                            Name = "traefik-cloudflare-tunnel",
                            Namespace = "edge-system",
                            Port = 443,
                            Scheme = "https",
                        },
                    },
                },
            },
        };

        var (managedIntent, invalidIntent) = await client.TryBuildIntentAsync(resource, CancellationToken.None);

        invalidIntent.Should().BeNull();
        managedIntent.Should().NotBeNull();
        managedIntent!.Route.OriginService.Should().Be(new Uri("https://traefik-cloudflare-tunnel.edge-system.svc.cluster.local:443"));
    }
}
