using FluentAssertions;
using k8s.Models;
using Promethix.CloudflareTunnelOperator.Hosting.Reconciliation;
using Promethix.CloudflareTunnelOperator.Routing.Application;
using Promethix.CloudflareTunnelOperator.Routing.Domain;
using Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;
using System.Net;

namespace Promethix.CloudflareTunnelOperator.Routing.Tests;

public sealed class SecurityPolicyCleanupDispositionEvaluatorTests
{
    [Fact]
    public void ForbiddenShouldNotBlockFinalizerWhenOwnershipWasNotPreviouslyProven()
    {
        var disposition = SecurityPolicyCleanupDispositionEvaluator.Evaluate(
            CreateResource(),
            cleanupResult: null,
            failure: new HttpRequestException("forbidden", null, HttpStatusCode.Forbidden));

        _ = disposition.ShouldBlockFinalizer.Should().BeFalse();
        _ = disposition.ShouldUpdateSecurityCondition.Should().BeTrue();
        _ = disposition.SecurityConditionReason.Should().Be("SecurityPolicyCleanupForbiddenUnproven");
    }

    [Fact]
    public void ForbiddenShouldBlockFinalizerWhenOwnershipWasPreviouslyProven()
    {
        var disposition = SecurityPolicyCleanupDispositionEvaluator.Evaluate(
            CreateResource("True", "Reconciled"),
            cleanupResult: null,
            failure: new HttpRequestException("forbidden", null, HttpStatusCode.Forbidden));

        _ = disposition.ShouldBlockFinalizer.Should().BeTrue();
        _ = disposition.ShouldUpdateSecurityCondition.Should().BeTrue();
        _ = disposition.SecurityConditionReason.Should().Be("SecurityPolicyCleanupForbidden");
    }

    [Fact]
    public void SuccessfulCleanupWithChangesShouldReportOwnedSecurityPolicyCleanupCompleted()
    {
        var disposition = SecurityPolicyCleanupDispositionEvaluator.Evaluate(
            CreateResource("True", "Reconciled"),
            new SecurityPolicyCleanupResult(
                "app.example.com",
                new SecurityPolicyPlan(
                    "app.example.com",
                    [],
                    [],
                    [new HostnameRateLimitRule("api", "expr", 60, 60, "block")],
                    []),
                ChangesApplied: true),
            failure: null);

        _ = disposition.ShouldBlockFinalizer.Should().BeFalse();
        _ = disposition.ShouldUpdateSecurityCondition.Should().BeTrue();
        _ = disposition.SecurityConditionReason.Should().Be("CleanedUp");
        _ = disposition.CleanupMessage.Should().Contain("owned security policy cleanup completed");
    }

    private static TunnelPublicHostnameCustomResource CreateResource(
        string securityPolicyStatus = "False",
        string securityPolicyReason = "NotRequested")
    {
        return new TunnelPublicHostnameCustomResource
        {
            Metadata = new V1ObjectMeta
            {
                Name = "app-public",
                NamespaceProperty = "demo",
            },
            Status = new TunnelPublicHostnameStatus
            {
                Conditions =
                [
                    new V1Condition
                    {
                        Type = "SecurityPolicyReady",
                        Status = securityPolicyStatus,
                        Reason = securityPolicyReason,
                        Message = "test",
                    },
                ],
            },
        };
    }
}
