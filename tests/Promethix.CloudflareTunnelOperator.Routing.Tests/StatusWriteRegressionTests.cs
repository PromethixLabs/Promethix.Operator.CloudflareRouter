using k8s;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Promethix.CloudflareTunnelOperator.Routing.Application;
using Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Promethix.CloudflareTunnelOperator.Routing.Tests;

public sealed class StatusWriteRegressionTests
{
    [Fact]
    public async Task IdenticalFailuresDoNotPatchAndReasonChangesPreserveTransitionTime()
    {
        using var handler = new StatusHandler();
        using var kubernetes = new Kubernetes(new KubernetesClientConfiguration { Host = "http://localhost" }, handler);
        var updater = new KubernetesRouteIntentStatusUpdater(kubernetes,
            Options.Create(new KubernetesOperatorOptions()), Options.Create(new RoutingOperatorOptions()),
            NullLogger<KubernetesRouteIntentStatusUpdater>.Instance);
        await updater.UpdateCleanupBlockedAsync("tenant", "app", 1, "app.example.com", "Blocked", "same failure", CancellationToken.None);
        var firstTransition = handler.Resource["status"]!["conditions"]![0]!["lastTransitionTime"]!.ToJsonString();
        await updater.UpdateCleanupBlockedAsync("tenant", "app", 1, "app.example.com", "Blocked", "same failure", CancellationToken.None);
        Assert.Equal(1, handler.Patches);
        await updater.UpdateCleanupBlockedAsync("tenant", "app", 1, "app.example.com", "OtherReason", "different failure", CancellationToken.None);
        Assert.Equal(2, handler.Patches);
        Assert.Equal(firstTransition, handler.Resource["status"]!["conditions"]![0]!["lastTransitionTime"]!.ToJsonString());
    }

    private sealed class StatusHandler : DelegatingHandler
    {
        public JsonNode Resource { get; } = JsonNode.Parse("""
            {"apiVersion":"edge.promethix.net/v1alpha1","kind":"TunnelPublicHostname","metadata":{"name":"app","namespace":"tenant","resourceVersion":"1"},"spec":{}}
            """)!;

        public int Patches { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Patch)
            {
                var patch = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false))!;
                Assert.Equal("1", patch["metadata"]!["resourceVersion"]!.GetValue<string>());
                Resource["status"] = patch["status"]!.DeepClone();
                Patches++;
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent(Resource.ToJsonString(), Encoding.UTF8, "application/json"),
            };
        }
    }
}
