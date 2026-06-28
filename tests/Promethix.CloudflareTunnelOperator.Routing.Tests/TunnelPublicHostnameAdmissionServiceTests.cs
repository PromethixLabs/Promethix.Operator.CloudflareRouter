using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Promethix.CloudflareTunnelOperator.Hosting.Admission;
using Promethix.CloudflareTunnelOperator.Hosting.Options;
using Promethix.CloudflareTunnelOperator.Routing.Application;
using Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

namespace Promethix.CloudflareTunnelOperator.Routing.Tests;

public sealed class TunnelPublicHostnameAdmissionServiceTests
{
    [Fact]
    public async Task ValidateAsyncShouldAllowDeleteOperations()
    {
        var service = CreateService();
        var review = new AdmissionReview
        {
            Request = new AdmissionRequest
            {
                Uid = "123",
                Operation = "DELETE",
            },
        };

        var response = await service.ValidateAsync(review, CancellationToken.None);

        _ = response.Response.Should().NotBeNull();
        _ = response.Response!.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsyncShouldAllowUnmanagedResources()
    {
        var service = CreateService();
        var resource = CreateResource("other", "other-tunnel", "whoami.example.com");
        var review = CreateReview("CREATE", resource);

        var response = await service.ValidateAsync(review, CancellationToken.None);

        _ = response.Response.Should().NotBeNull();
        _ = response.Response!.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsyncShouldRejectInvalidManagedResources()
    {
        var service = CreateService();
        var resource = CreateResource("public", "delta-public", "whoami.example.com");
        resource.Spec.Target = new TunnelTargetSpec
        {
            Mode = "ingress",
            Ingress = new TunnelIngressTargetSpec
            {
                ClassName = "wrong-ingress",
            },
        };

        var review = CreateReview("CREATE", resource);

        var response = await service.ValidateAsync(review, CancellationToken.None);

        _ = response.Response.Should().NotBeNull();
        _ = response.Response!.Allowed.Should().BeFalse();
        _ = response.Response.Status!.Message.Should().Contain("does not match managed ingress class");
    }

    private static TunnelPublicHostnameAdmissionService CreateService()
    {
        var client = new KubernetesTunnelPublicHostnameClient(
            kubernetes: null!,
            Options.Create(new KubernetesOperatorOptions
            {
                ManagedClassName = "public",
                ManagedTunnelName = "delta-public",
                ManagedIngressClassName = "traefik-cloudflare-tunnel",
                IngressTargetUrl = new Uri("https://traefik-cloudflare-tunnel.edge-system.svc.cluster.local"),
                ManagedFinalizerName = "edge.promethix.net/tunnelpublichostname-protection",
                OwnershipConfigMapNamespace = "edge-system",
                OwnershipConfigMapName = "promethix-cloudflare-tunnel-operator-ownership",
            }),
            Options.Create(new RoutingOperatorOptions
            {
                OwnershipTag = "promethix-cloudflare-tunnel-operator",
            }),
            new AcceptingHostnameOwnershipValidator(),
            new AcceptingIngressTargetValidator(),
            NullLogger<KubernetesTunnelPublicHostnameClient>.Instance);

        return new TunnelPublicHostnameAdmissionService(
            client,
            Options.Create(new KubernetesOperatorOptions
            {
                ManagedClassName = "public",
                ManagedTunnelName = "delta-public",
                ManagedIngressClassName = "traefik-cloudflare-tunnel",
                IngressTargetUrl = new Uri("https://traefik-cloudflare-tunnel.edge-system.svc.cluster.local"),
                ManagedFinalizerName = "edge.promethix.net/tunnelpublichostname-protection",
                OwnershipConfigMapNamespace = "edge-system",
                OwnershipConfigMapName = "promethix-cloudflare-tunnel-operator-ownership",
            }));
    }

    private static AdmissionReview CreateReview(string operation, TunnelPublicHostnameCustomResource resource)
    {
        return new AdmissionReview
        {
            Request = new AdmissionRequest
            {
                Uid = "123",
                Operation = operation,
                Object = JsonSerializer.SerializeToElement(resource),
            },
        };
    }

    private static TunnelPublicHostnameCustomResource CreateResource(string className, string tunnelName, string hostname)
    {
        return new TunnelPublicHostnameCustomResource
        {
            Metadata = new k8s.Models.V1ObjectMeta
            {
                Name = "whoami-public",
                NamespaceProperty = "demo",
            },
            Spec = new TunnelPublicHostnameSpec
            {
                ClassName = className,
                Hostname = hostname,
                TunnelRef = new TunnelReferenceSpec
                {
                    Name = tunnelName,
                },
            },
        };
    }

    private sealed class AcceptingHostnameOwnershipValidator : IHostnameOwnershipValidator
    {
        public Task ValidateAsync(TunnelPublicHostnameCustomResource resource, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class AcceptingIngressTargetValidator : IIngressTargetValidator
    {
        public Task ValidateAsync(
            TunnelPublicHostnameCustomResource resource,
            TunnelIngressTargetSpec ingress,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
