using Microsoft.Extensions.Options;
using Promethix.CloudflareTunnelOperator.Routing.Application;

namespace Promethix.CloudflareTunnelOperator.Hosting.Options;

internal sealed class RoutingOperatorOptionsValidator : IValidateOptions<RoutingOperatorOptions>
{
    public ValidateOptionsResult Validate(string? name, RoutingOperatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.OwnershipTag))
        {
            failures.Add("RoutingOperator:OwnershipTag is required.");
        }

        if (options.ReconciliationIntervalSeconds <= 0)
        {
            failures.Add("RoutingOperator:ReconciliationIntervalSeconds must be greater than zero.");
        }

        if (!Enum.TryParse<RouteMutationMode>(options.MutationMode, ignoreCase: true, out _))
        {
            failures.Add("RoutingOperator:MutationMode must be one of Full, CreateUpdateOnly, or CreateOnly.");
        }

        if (options.MaxDeleteCount < -1)
        {
            failures.Add("RoutingOperator:MaxDeleteCount must be -1 or greater.");
        }

        if (options.MaxDeletePercentage is < -1 or > 100)
        {
            failures.Add("RoutingOperator:MaxDeletePercentage must be between 0 and 100, or -1 to disable.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
