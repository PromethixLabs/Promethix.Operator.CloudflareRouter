using k8s;
using k8s.LeaderElection;
using k8s.LeaderElection.ResourceLock;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace Promethix.CloudflareTunnelOperator.Routing.Tests;

public sealed class LeaderElectionRegressionTests
{
    [Fact]
    public async Task TwoReplicasSharingLeaseStartOnlyOneWriter()
    {
        using var handler = new LeaseHandler();
        using var kubernetes = new Kubernetes(new KubernetesClientConfiguration { Host = "http://localhost" }, handler);
        using var first = CreateElector(kubernetes, "first");
        using var second = CreateElector(kubernetes, "second");
        var writers = 0;
        first.OnStartedLeading += () => Interlocked.Increment(ref writers);
        second.OnStartedLeading += () => Interlocked.Increment(ref writers);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var tasks = new[] { first.RunUntilLeadershipLostAsync(cancellation.Token), second.RunUntilLeadershipLostAsync(cancellation.Token) };
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.WhenAll(tasks));
        Assert.Equal(1, writers);
    }

    private static LeaderElector CreateElector(IKubernetes kubernetes, string identity)
    {
        return new LeaderElector(new LeaderElectionConfig(new LeaseLock(kubernetes, "system", "shared-tunnel", identity))
        {
            LeaseDuration = TimeSpan.FromSeconds(60),
            RenewDeadline = TimeSpan.FromSeconds(20),
            RetryPeriod = TimeSpan.FromMilliseconds(50),
        });
    }

    private sealed class LeaseHandler : DelegatingHandler
    {
        private readonly Lock syncRoot = new();
        private JsonNode? lease;
        private int version;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            lock (syncRoot)
            {
                if (request.Method == HttpMethod.Post && lease is not null)
                {
                    return Response(request, HttpStatusCode.Conflict, "{}");
                }
                if (request.Method == HttpMethod.Put && body?["metadata"]?["resourceVersion"]?.ToJsonString() != lease?["metadata"]?["resourceVersion"]?.ToJsonString())
                {
                    return Response(request, HttpStatusCode.Conflict, "{}");
                }
                if (body is not null)
                {
                    lease = body;
                    lease["metadata"]!["resourceVersion"] = (++version).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                return lease is null ? Response(request, HttpStatusCode.NotFound, "{}") : Response(request, HttpStatusCode.OK, lease.ToJsonString());
            }
        }

        private static HttpResponseMessage Response(HttpRequestMessage request, HttpStatusCode status, string body)
        {
            return new HttpResponseMessage(status) { RequestMessage = request, Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
