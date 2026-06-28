using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Promethix.CloudflareTunnelOperator.Hosting;
using Promethix.CloudflareTunnelOperator.Hosting.Reconciliation;
using Promethix.CloudflareTunnelOperator.Routing.Application;
using Promethix.CloudflareTunnelOperator.Routing.Domain;

namespace Promethix.CloudflareTunnelOperator.Routing.Tests;

public sealed class RouteMutationSafetyEvaluatorTests
{
    private const string OwnershipTag = "promethix-cloudflare-tunnel-operator";

    [Fact]
    public async Task FullReconcileShouldBlockBeforeInitialWatchActivity()
    {
        var evaluator = new RouteMutationSafetyEvaluator(
            new StubOwnershipStore(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            new OperatorState(),
            NullLogger<RouteMutationSafetyEvaluator>.Instance);

        var intent = CreateIntent(["one.example.com"]);
        var plan = new RoutePlan([], [], [], []);

        var decision = await evaluator.EvaluateFullReconcileAsync(
            CreateOptions(),
            intent,
            plan,
            CancellationToken.None);

        _ = decision.AllowApply.Should().BeFalse();
        _ = decision.Reason.Should().Be("StartupInventoryIncomplete");
    }

    [Fact]
    public async Task FullReconcileShouldBlockWhenDeleteBudgetExceeded()
    {
        var state = new OperatorState();
        _ = state.MarkWatchActivityObserved();

        var evaluator = new RouteMutationSafetyEvaluator(
            new StubOwnershipStore(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["one.example.com"] = OwnershipTag,
                ["two.example.com"] = OwnershipTag,
                ["three.example.com"] = OwnershipTag,
                ["four.example.com"] = OwnershipTag,
            }),
            state,
            NullLogger<RouteMutationSafetyEvaluator>.Instance);

        var intent = CreateIntent(["one.example.com"]);
        var plan = new RoutePlan(
            [],
            [],
            [
                CreateRoute("two.example.com"),
                CreateRoute("three.example.com"),
                CreateRoute("four.example.com"),
            ],
            []);

        var decision = await evaluator.EvaluateFullReconcileAsync(
            CreateOptions(maxDeleteCount: 2, maxDeletePercentage: 50),
            intent,
            plan,
            CancellationToken.None);

        _ = decision.AllowApply.Should().BeFalse();
        _ = decision.Reason.Should().Be("ProtectiveDeleteGuardTriggered");
    }

    [Fact]
    public async Task TargetedReconcileShouldBlockUntilStartupMarkedSafe()
    {
        var evaluator = new RouteMutationSafetyEvaluator(
            new StubOwnershipStore(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            new OperatorState(),
            NullLogger<RouteMutationSafetyEvaluator>.Instance);

        var intent = new ManagedRouteIntent("app", "demo", 1, CreateRoute("one.example.com"));
        var plan = new RoutePlan([], [CreateRoute("one.example.com")], [], []);

        var decision = await evaluator.EvaluateManagedRouteReconcileAsync(
            CreateOptions(),
            intent,
            plan,
            CancellationToken.None);

        _ = decision.AllowApply.Should().BeFalse();
        _ = decision.Reason.Should().Be("StartupInventoryIncomplete");
    }

    [Fact]
    public async Task CleanupShouldRespectMutationMode()
    {
        var state = new OperatorState();
        _ = state.MarkWatchActivityObserved();
        state.MarkInitialFullInventoryPass(DateTimeOffset.UtcNow, startupSafeForMutation: true);

        var evaluator = new RouteMutationSafetyEvaluator(
            new StubOwnershipStore(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            state,
            NullLogger<RouteMutationSafetyEvaluator>.Instance);

        var plan = new RoutePlan([], [], [CreateRoute("one.example.com")], []);

        var decision = await evaluator.EvaluateCleanupAsync(
            CreateOptions(mutationMode: "CreateUpdateOnly"),
            "one.example.com",
            plan,
            CancellationToken.None);

        _ = decision.AllowApply.Should().BeFalse();
        _ = decision.Reason.Should().Be("MutationModeRestricted");
    }

    private static RoutingOperatorOptions CreateOptions(
        string mutationMode = "Full",
        int maxDeleteCount = 5,
        int maxDeletePercentage = 50)
    {
        return new RoutingOperatorOptions
        {
            OwnershipTag = OwnershipTag,
            MutationMode = mutationMode,
            StartupProtectionEnabled = true,
            MaxDeleteCount = maxDeleteCount,
            MaxDeletePercentage = maxDeletePercentage,
        };
    }

    private static RouteIntentDocument CreateIntent(string[] hostnames)
    {
        return new RouteIntentDocument(
            "test",
            [.. hostnames.Select((hostname, index) => new ManagedRouteIntent($"app-{index}", "demo", 1, CreateRoute(hostname)))],
            []);
    }

    private static PublicHostnameRoute CreateRoute(string hostname)
    {
        return PublicHostnameRoute.Create(
            hostname,
            new Uri($"https://{hostname.Replace('.', '-')}.demo.svc.cluster.local:8443"),
            RouteProtocol.Https,
            OwnershipTag);
    }

    private sealed class StubOwnershipStore(IReadOnlyDictionary<string, string> ownership) : IManagedRouteOwnershipStore
    {
        public Task<IReadOnlyDictionary<string, string>> GetOwnershipAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(ownership);
        }

        public Task SaveOwnershipAsync(IReadOnlyDictionary<string, string> ownershipByHostname, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
