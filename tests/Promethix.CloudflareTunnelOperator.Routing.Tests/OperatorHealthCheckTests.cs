using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Promethix.CloudflareTunnelOperator.Hosting;
using Promethix.CloudflareTunnelOperator.Hosting.Admission;
using Promethix.CloudflareTunnelOperator.Hosting.Health;
using Promethix.CloudflareTunnelOperator.Routing.Application;

namespace Promethix.CloudflareTunnelOperator.Routing.Tests;

public sealed class OperatorHealthCheckTests
{
    [Fact]
    public async Task ReadinessShouldBeUnhealthyWhenWebhookIsEnabledAndListenerIsNotReady()
    {
        var state = new OperatorState();
        state.MarkInitialFullInventoryPass(DateTimeOffset.UtcNow, startupSafeForMutation: true);

        var webhookState = new AdmissionWebhookRuntimeState
        {
            Enabled = true,
            ListenerReady = false,
            FailureReason = "Webhook TLS listener is not serving.",
        };

        var healthCheck = new OperatorReadinessHealthCheck(
            state,
            webhookState,
            Options.Create(new RoutingOperatorOptions()));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        _ = result.Status.Should().Be(HealthStatus.Unhealthy);
        _ = result.Description.Should().Be("Webhook TLS listener is not serving.");
    }

    [Fact]
    public async Task LivenessShouldStayHealthyWhileWaitingForWebhookTlsFiles()
    {
        var webhookState = new AdmissionWebhookRuntimeState
        {
            Enabled = true,
            ListenerReady = false,
            CertificateFilesPresent = false,
        };

        var healthCheck = new OperatorLivenessHealthCheck(webhookState);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        _ = result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task LivenessShouldBeUnhealthyWhenWebhookTlsFilesExistButListenerIsNotReady()
    {
        var webhookState = new AdmissionWebhookRuntimeState
        {
            Enabled = true,
            ListenerReady = false,
            CertificateFilesPresent = true,
            FailureReason = "Webhook TLS listener is not serving.",
        };

        var healthCheck = new OperatorLivenessHealthCheck(webhookState);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        _ = result.Status.Should().Be(HealthStatus.Unhealthy);
        _ = result.Description.Should().Be("Webhook TLS listener is not serving.");
    }
}
