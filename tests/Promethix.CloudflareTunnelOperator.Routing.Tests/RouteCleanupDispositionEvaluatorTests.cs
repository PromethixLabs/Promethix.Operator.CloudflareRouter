using FluentAssertions;
using Promethix.CloudflareTunnelOperator.Hosting.Reconciliation;
using Promethix.CloudflareTunnelOperator.Routing.Application;
using Promethix.CloudflareTunnelOperator.Routing.Domain;

namespace Promethix.CloudflareTunnelOperator.Routing.Tests;

public sealed class RouteCleanupDispositionEvaluatorTests
{
    [Fact]
    public void EvaluateShouldBlockDeletionWhenApplyChangesIsDisabled()
    {
        var disposition = RouteCleanupDispositionEvaluator.Evaluate(
            new RoutingOperatorOptions
            {
                ApplyChanges = false,
            },
            new RouteCleanupResult(
                "app.example.com",
                CreateDeletePlan("app.example.com"),
                ChangesApplied: false));

        _ = disposition.ShouldRemoveFinalizer.Should().BeFalse();
        _ = disposition.IsBlocked.Should().BeTrue();
        _ = disposition.Reason.Should().Be("ApplyDisabled");
    }

    [Fact]
    public void EvaluateShouldBlockDeletionWhenMutationPolicyBlocksApply()
    {
        var disposition = RouteCleanupDispositionEvaluator.Evaluate(
            new RoutingOperatorOptions
            {
                ApplyChanges = true,
            },
            new RouteCleanupResult(
                "app.example.com",
                CreateDeletePlan("app.example.com"),
                ChangesApplied: false,
                ApplyBlocked: true,
                ApplyBlockReason: "MutationModeRestricted",
                ApplyBlockMessage: "Mutation mode create-update-only blocks deletes."));

        _ = disposition.ShouldRemoveFinalizer.Should().BeFalse();
        _ = disposition.IsBlocked.Should().BeTrue();
        _ = disposition.Reason.Should().Be("MutationModeRestricted");
        _ = disposition.Message.Should().Contain("blocks deletes");
    }

    [Fact]
    public void EvaluateShouldAllowFinalizerRemovalWhenDeleteSucceeded()
    {
        var disposition = RouteCleanupDispositionEvaluator.Evaluate(
            new RoutingOperatorOptions
            {
                ApplyChanges = true,
            },
            new RouteCleanupResult(
                "app.example.com",
                CreateDeletePlan("app.example.com"),
                ChangesApplied: true));

        _ = disposition.ShouldRemoveFinalizer.Should().BeTrue();
        _ = disposition.IsBlocked.Should().BeFalse();
        _ = disposition.Reason.Should().Be("CleanedUp");
    }

    [Fact]
    public void EvaluateShouldAllowFinalizerRemovalWhenNoRemoteCleanupIsRequired()
    {
        var disposition = RouteCleanupDispositionEvaluator.Evaluate(
            new RoutingOperatorOptions
            {
                ApplyChanges = false,
            },
            new RouteCleanupResult(
                "app.example.com",
                new RoutePlan([], [], [], []),
                ChangesApplied: false));

        _ = disposition.ShouldRemoveFinalizer.Should().BeTrue();
        _ = disposition.IsBlocked.Should().BeFalse();
        _ = disposition.Reason.Should().Be("CleanedUp");
    }

    private static RoutePlan CreateDeletePlan(string hostname)
    {
        var route = PublicHostnameRoute.Create(
            hostname,
            new Uri("https://app.example.svc.cluster.local"),
            RouteProtocol.Https,
            "promethix-cloudflare-tunnel-operator");

        return new RoutePlan([], [], [route], []);
    }
}
