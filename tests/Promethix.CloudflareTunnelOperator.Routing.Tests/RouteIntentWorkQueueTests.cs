using FluentAssertions;
using Promethix.CloudflareTunnelOperator.Hosting.Reconciliation;
using Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

namespace Promethix.CloudflareTunnelOperator.Routing.Tests;

public sealed class RouteIntentWorkQueueTests
{
    [Fact]
    public async Task EnqueueResourceShouldDeduplicateByKey()
    {
        var queue = new RouteIntentWorkQueue();
        var key = new TunnelPublicHostnameResourceKey("demo", "app-promethix-net");

        queue.EnqueueResource(key, "watch:Added");
        queue.EnqueueResource(key, "watch:Modified");

        var first = await queue.WaitAsync(TimeSpan.FromMilliseconds(10), CancellationToken.None);
        var second = await queue.WaitAsync(TimeSpan.FromMilliseconds(10), CancellationToken.None);

        _ = first.Kind.Should().Be(RouteIntentWorkItemKind.Resource);
        _ = first.ResourceKey.Should().Be(key);
        _ = second.Kind.Should().Be(RouteIntentWorkItemKind.FullResync);
        _ = second.Reason.Should().Be("interval");
    }
}
