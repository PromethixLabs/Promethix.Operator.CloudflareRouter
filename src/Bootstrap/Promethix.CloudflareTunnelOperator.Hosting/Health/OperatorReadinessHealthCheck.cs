using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Promethix.CloudflareTunnelOperator.Hosting.Admission;
using Promethix.CloudflareTunnelOperator.Routing.Application;

namespace Promethix.CloudflareTunnelOperator.Hosting.Health;

internal sealed class OperatorReadinessHealthCheck(
    OperatorState state,
    AdmissionWebhookRuntimeState webhookState,
    IOptions<RoutingOperatorOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return !state.HasCompletedInitialFullInventoryPass
            ? Task.FromResult(HealthCheckResult.Degraded("Initial full inventory reconciliation has not completed yet."))
            : webhookState.Enabled && !webhookState.ListenerReady
                ? Task.FromResult(
                    HealthCheckResult.Degraded(
                        webhookState.FailureReason ?? "Admission webhook is enabled but the TLS listener is not ready."))
            : options.Value.ApplyChanges && !state.IsStartupSafeForMutation
                ? Task.FromResult(
                    HealthCheckResult.Unhealthy(
                        state.StartupBlockMessage ?? "Cloudflare writes are blocked because startup safety checks have not passed.",
                        data: new Dictionary<string, object>
                        {
                            ["reason"] = state.StartupBlockReason ?? "Unknown",
                        }))
                : Task.FromResult(HealthCheckResult.Healthy("Operator startup safety checks have completed."));
    }
}
