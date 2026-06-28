using Microsoft.Extensions.Options;
using Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

namespace Promethix.CloudflareTunnelOperator.Hosting.Options;

internal sealed class KubernetesOperatorOptionsValidator : IValidateOptions<KubernetesOperatorOptions>
{
    public ValidateOptionsResult Validate(string? name, KubernetesOperatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ManagedClassName))
        {
            failures.Add("KubernetesOperator:ManagedClassName is required.");
        }

        if (string.IsNullOrWhiteSpace(options.ManagedTunnelName))
        {
            failures.Add("KubernetesOperator:ManagedTunnelName is required.");
        }

        if (string.IsNullOrWhiteSpace(options.ManagedIngressClassName))
        {
            failures.Add("KubernetesOperator:ManagedIngressClassName is required.");
        }

        if (options.IngressTargetUrl is null)
        {
            failures.Add("KubernetesOperator:IngressTargetUrl is required.");
        }
        else if (!string.Equals(options.IngressTargetUrl.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                 && !string.Equals(options.IngressTargetUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("KubernetesOperator:IngressTargetUrl must use http or https.");
        }

        if (string.IsNullOrWhiteSpace(options.ManagedFinalizerName))
        {
            failures.Add("KubernetesOperator:ManagedFinalizerName is required.");
        }

        if (string.IsNullOrWhiteSpace(options.OwnershipConfigMapNamespace))
        {
            failures.Add("KubernetesOperator:OwnershipConfigMapNamespace is required.");
        }

        if (string.IsNullOrWhiteSpace(options.OwnershipConfigMapName))
        {
            failures.Add("KubernetesOperator:OwnershipConfigMapName is required.");
        }

        if (options.EnforceNamespaceHostnamePolicy
            && string.IsNullOrWhiteSpace(options.AllowedHostnameSuffixesAnnotation))
        {
            failures.Add("KubernetesOperator:AllowedHostnameSuffixesAnnotation is required when KubernetesOperator:EnforceNamespaceHostnamePolicy is enabled.");
        }

        var configuredAllowedSuffixes = options.AllowedHostnameSuffixes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (configuredAllowedSuffixes.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add("KubernetesOperator:AllowedHostnameSuffixes must not contain empty values.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
