using Microsoft.Extensions.Options;

namespace Promethix.CloudflareTunnelOperator.Hosting.Options;

internal sealed class AdmissionWebhookOptionsValidator : IValidateOptions<AdmissionWebhookOptions>
{
    public ValidateOptionsResult Validate(string? name, AdmissionWebhookOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (options.Port <= 0)
        {
            failures.Add("AdmissionWebhook:Port must be greater than zero when the admission webhook is enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.Path) || options.Path[0] != '/')
        {
            failures.Add("AdmissionWebhook:Path must be an absolute path when the admission webhook is enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.CertificatePath))
        {
            failures.Add("AdmissionWebhook:CertificatePath is required when the admission webhook is enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.PrivateKeyPath))
        {
            failures.Add("AdmissionWebhook:PrivateKeyPath is required when the admission webhook is enabled.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
