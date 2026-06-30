using System.Net;
using System.Net.Http;
using Promethix.CloudflareTunnelOperator.Routing.Application;
using Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

namespace Promethix.CloudflareTunnelOperator.Hosting.Reconciliation;

internal sealed record SecurityPolicyCleanupDisposition(
    bool ShouldBlockFinalizer,
    bool ShouldUpdateSecurityCondition,
    string SecurityConditionStatus,
    string SecurityConditionReason,
    string SecurityConditionMessage,
    string CleanupReason,
    string CleanupMessage);

internal static class SecurityPolicyCleanupDispositionEvaluator
{
    public static SecurityPolicyCleanupDisposition Evaluate(
        TunnelPublicHostnameCustomResource resource,
        SecurityPolicyCleanupResult? cleanupResult,
        Exception? failure)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var provenOwnedSecurityPolicy = HasProvenOwnedSecurityPolicy(resource);

        return failure is null
            ? Completed(cleanupResult)
            : failure is HttpRequestException { StatusCode: HttpStatusCode.Forbidden } forbidden
                ? provenOwnedSecurityPolicy
                    ? Blocked(
                        "SecurityPolicyCleanupForbidden",
                        forbidden.Message,
                        "Cloudflare route cleanup succeeded, but cleanup of a previously reconciled owned security policy was forbidden. The finalizer is being retained.")
                    : CompletedWithWarning(
                        "SecurityPolicyCleanupForbiddenUnproven",
                        forbidden.Message,
                        "Cloudflare route cleanup succeeded. Security policy cleanup was forbidden, but no owned security policy had been proven, so the finalizer will be removed.")
                : failure is HttpRequestException { StatusCode: HttpStatusCode.NotFound } notFound
                    ? CompletedWithWarning(
                        "SecurityPolicyCleanupNotFound",
                        notFound.Message,
                        "Cloudflare route cleanup succeeded. No matching owned security policy was found during cleanup.")
                    : provenOwnedSecurityPolicy
                        ? Blocked(
                            "SecurityPolicyCleanupFailed",
                            failure.Message,
                            "Cloudflare route cleanup succeeded, but cleanup of a previously reconciled owned security policy failed. The finalizer is being retained.")
                        : CompletedWithWarning(
                            "SecurityPolicyCleanupFailedUnproven",
                            failure.Message,
                            "Cloudflare route cleanup succeeded. Security policy cleanup failed, but no owned security policy had been proven, so the finalizer will be removed.");
    }

    private static SecurityPolicyCleanupDisposition Completed(SecurityPolicyCleanupResult? cleanupResult)
    {
        return cleanupResult is not null && cleanupResult.Plan.HasChanges && cleanupResult.ChangesApplied
            ? new SecurityPolicyCleanupDisposition(
                ShouldBlockFinalizer: false,
                ShouldUpdateSecurityCondition: true,
                SecurityConditionStatus: "True",
                SecurityConditionReason: "CleanedUp",
                SecurityConditionMessage: "Owned security policy cleanup completed.",
                CleanupReason: "CleanedUp",
                CleanupMessage: "Managed route and owned security policy cleanup completed.")
            : new SecurityPolicyCleanupDisposition(
                ShouldBlockFinalizer: false,
                ShouldUpdateSecurityCondition: false,
                SecurityConditionStatus: "True",
                SecurityConditionReason: "NotRequested",
                SecurityConditionMessage: "No security policy cleanup was required.",
                CleanupReason: "CleanedUp",
                CleanupMessage: "Managed route cleanup completed.");
    }

    private static SecurityPolicyCleanupDisposition CompletedWithWarning(
        string reason,
        string securityMessage,
        string cleanupMessage)
    {
        return new SecurityPolicyCleanupDisposition(
            ShouldBlockFinalizer: false,
            ShouldUpdateSecurityCondition: true,
            SecurityConditionStatus: "False",
            SecurityConditionReason: reason,
            SecurityConditionMessage: securityMessage,
            CleanupReason: "CleanedUp",
            CleanupMessage: cleanupMessage);
    }

    private static SecurityPolicyCleanupDisposition Blocked(
        string reason,
        string securityMessage,
        string cleanupMessage)
    {
        return new SecurityPolicyCleanupDisposition(
            ShouldBlockFinalizer: true,
            ShouldUpdateSecurityCondition: true,
            SecurityConditionStatus: "False",
            SecurityConditionReason: reason,
            SecurityConditionMessage: securityMessage,
            CleanupReason: reason,
            CleanupMessage: cleanupMessage);
    }

    private static bool HasProvenOwnedSecurityPolicy(TunnelPublicHostnameCustomResource resource)
    {
        var condition = resource.Status?.Conditions
            .FirstOrDefault(current => string.Equals(current.Type, "SecurityPolicyReady", StringComparison.Ordinal));

        return string.Equals(condition?.Status, "True", StringComparison.OrdinalIgnoreCase)
            && string.Equals(condition?.Reason, "Reconciled", StringComparison.Ordinal);
    }
}
