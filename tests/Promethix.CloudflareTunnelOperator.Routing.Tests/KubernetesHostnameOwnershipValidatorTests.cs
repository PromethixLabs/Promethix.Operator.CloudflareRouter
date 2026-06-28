using FluentAssertions;
using k8s.Models;
using Microsoft.Extensions.Options;
using Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

namespace Promethix.CloudflareTunnelOperator.Routing.Tests;

public sealed class KubernetesHostnameOwnershipValidatorTests
{
    [Fact]
    public async Task ValidateAsyncAllowsHostnameWithinNamespaceSuffix()
    {
        var validator = CreateValidator(
            "demo",
            "edge.promethix.net/allowed-hostname-suffixes",
            "delta.promethix.net, apps.promethix.net");

        var resource = CreateResource("demo", "whoami.delta.promethix.net");

        var act = () => validator.ValidateAsync(resource, CancellationToken.None);

        _ = await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateAsyncRejectsHostnameOutsideNamespaceSuffixes()
    {
        var validator = CreateValidator(
            "demo",
            "edge.promethix.net/allowed-hostname-suffixes",
            "apps.promethix.net");

        var resource = CreateResource("demo", "whoami.delta.promethix.net");

        var act = () => validator.ValidateAsync(resource, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        _ = exception.Which.Message.Should().Be(
            "Hostname 'whoami.delta.promethix.net' is not permitted for namespace 'demo'. Allowed suffixes: apps.promethix.net.");
    }

    [Fact]
    public async Task ValidateAsyncRejectsMissingNamespaceAnnotation()
    {
        var validator = CreateValidator(
            "demo",
            "edge.promethix.net/allowed-hostname-suffixes",
            null);

        var resource = CreateResource("demo", "whoami.delta.promethix.net");

        var act = () => validator.ValidateAsync(resource, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        _ = exception.Which.Message.Should().Be(
            "Namespace 'demo' is not allowed to claim hostnames because annotation 'edge.promethix.net/allowed-hostname-suffixes' is missing or empty.");
    }

    [Fact]
    public async Task ValidateAsyncSkipsCheckWhenPolicyDisabled()
    {
        var validator = new KubernetesHostnameOwnershipValidator(
            new FakeNamespaceReader(new V1Namespace()),
            Options.Create(new KubernetesOperatorOptions
            {
                EnforceNamespaceHostnamePolicy = false,
            }));

        var resource = CreateResource("demo", "whoami.delta.promethix.net");

        var act = () => validator.ValidateAsync(resource, CancellationToken.None);

        _ = await act.Should().NotThrowAsync();
    }

    private static KubernetesHostnameOwnershipValidator CreateValidator(
        string namespaceName,
        string annotationName,
        string? annotationValue)
    {
        var annotations = annotationValue is null
            ? null
            : new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [annotationName] = annotationValue,
            };

        return new KubernetesHostnameOwnershipValidator(
            new FakeNamespaceReader(new V1Namespace
            {
                Metadata = new V1ObjectMeta
                {
                    Name = namespaceName,
                    Annotations = annotations,
                },
            }),
            Options.Create(new KubernetesOperatorOptions
            {
                EnforceNamespaceHostnamePolicy = true,
                AllowedHostnameSuffixesAnnotation = annotationName,
            }));
    }

    private static TunnelPublicHostnameCustomResource CreateResource(string namespaceName, string hostname)
    {
        return new TunnelPublicHostnameCustomResource
        {
            Metadata = new V1ObjectMeta
            {
                Name = "test",
                NamespaceProperty = namespaceName,
            },
            Spec = new TunnelPublicHostnameSpec
            {
                ClassName = "public",
                Hostname = hostname,
                TunnelRef = new TunnelReferenceSpec
                {
                    Name = "delta-public",
                },
            },
        };
    }

    private sealed class FakeNamespaceReader(V1Namespace namespaceResource) : IKubernetesNamespaceReader
    {
        public Task<V1Namespace> ReadAsync(string namespaceName, CancellationToken cancellationToken)
        {
            return Task.FromResult(namespaceResource);
        }
    }
}
