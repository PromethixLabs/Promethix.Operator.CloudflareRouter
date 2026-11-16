using k8s;
using Promethix.CloudflareTunnelOperator.Hosting.Reconciliation;
using Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;
using Promethix.CloudflareTunnelOperator.Routing.Integrations.Cloudflare;
using System.Net;
using System.Net.Http.Headers;

namespace Promethix.CloudflareTunnelOperator.Routing.Tests;

public sealed class WatchFeedbackRegressionTests
{
    [Fact]
    public void StatusAndResourceVersionChangesDoNotRequeueButSpecMetadataAndDeletionDo()
    {
        var filter = new RouteIntentChangeFilter();
        var resource = new TunnelPublicHostnameCustomResource();
        resource.Metadata.Name = "app";
        resource.Metadata.NamespaceProperty = "tenant";
        Assert.True(filter.ShouldReconcile(WatchEventType.Added, resource));
        for (var count = 0; count < 100; count++)
        {
            resource.Metadata.ResourceVersion = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            resource.Status = new TunnelPublicHostnameStatus { ObservedGeneration = count };
            Assert.False(filter.ShouldReconcile(WatchEventType.Modified, resource));
        }

        resource.Metadata.Generation = 2;
        Assert.True(filter.ShouldReconcile(WatchEventType.Modified, resource));
        resource.Metadata.Annotations = new Dictionary<string, string> { ["example.com/policy"] = "changed" };
        Assert.True(filter.ShouldReconcile(WatchEventType.Modified, resource));
        resource.Metadata.DeletionTimestamp = DateTime.UtcNow;
        Assert.True(filter.ShouldReconcile(WatchEventType.Modified, resource));
        Assert.True(filter.ShouldReconcile(WatchEventType.Deleted, resource));
    }

    [Fact]
    public async Task IntervalTimeoutDoesNotStealNextWatchEvent()
    {
        var queue = new RouteIntentWorkQueue();
        _ = await queue.WaitAsync(TimeSpan.FromMilliseconds(10), CancellationToken.None);
        queue.EnqueueFullResync("spec-change");
        var item = await queue.WaitAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.Equal("spec-change", item.Reason);
    }

    [Fact]
    public async Task RateLimitBlocksSubsequentRequestsEvenWhenWorkKeepsArriving()
    {
        using var gate = new CloudflareRequestGate();
        using var limited = await gate.SendAsync(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(1));
            return Task.FromResult(response);
        }, CancellationToken.None);
        var sent = 0;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var requests = Enumerable.Range(0, 10).Select(async index =>
        {
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                using var response = await gate.SendAsync(token =>
                {
                    _ = Interlocked.Increment(ref sent);
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                }, cancellation.Token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        });
        await Task.WhenAll(requests);
        Assert.Equal(0, sent);
    }
}
