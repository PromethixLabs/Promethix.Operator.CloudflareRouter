using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Promethix.CloudflareTunnelOperator.Hosting.Options;
using Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

namespace Promethix.CloudflareTunnelOperator.Hosting.Admission;

internal sealed class TunnelPublicHostnameAdmissionService(
    KubernetesTunnelPublicHostnameClient client,
    IOptions<KubernetesOperatorOptions> kubernetesOptions)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AdmissionReview> ValidateAsync(AdmissionReview review, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(review);

        var request = review.Request;
        if (request is null)
        {
            return CreateDeniedResponse(string.Empty, "AdmissionReview.request is required.", HttpStatusCode.BadRequest);
        }

        if (string.Equals(request.Operation, "DELETE", StringComparison.OrdinalIgnoreCase))
        {
            return CreateAllowedResponse(request.Uid);
        }

        if (request.Object.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return CreateDeniedResponse(request.Uid, "AdmissionReview.request.object is required.", HttpStatusCode.BadRequest);
        }

        TunnelPublicHostnameCustomResource? resource;
        try
        {
            resource = request.Object.Deserialize<TunnelPublicHostnameCustomResource>(JsonOptions);
        }
        catch (JsonException ex)
        {
            return CreateDeniedResponse(request.Uid, $"TunnelPublicHostname payload could not be parsed: {ex.Message}", HttpStatusCode.BadRequest);
        }

        if (resource is null)
        {
            return CreateDeniedResponse(request.Uid, "TunnelPublicHostname payload could not be parsed.", HttpStatusCode.BadRequest);
        }

        if (!client.IsManaged(resource))
        {
            return CreateAllowedResponse(request.Uid);
        }

        var (managedIntent, invalidIntent) = await client.TryBuildIntentAsync(resource, cancellationToken).ConfigureAwait(false);
        return invalidIntent is not null
            ? CreateDeniedResponse(request.Uid, invalidIntent.Reason, HttpStatusCode.UnprocessableEntity)
            : kubernetesOptions.Value.EnforceNamespaceHostnamePolicy && managedIntent is null
            ? CreateDeniedResponse(request.Uid, "Managed TunnelPublicHostname did not produce a valid route intent.", HttpStatusCode.UnprocessableEntity)
            : CreateAllowedResponse(request.Uid);
    }

    private static AdmissionReview CreateAllowedResponse(string uid)
    {
        return new AdmissionReview
        {
            Response = new AdmissionResponse
            {
                Uid = uid,
                Allowed = true,
            },
        };
    }

    private static AdmissionReview CreateDeniedResponse(string uid, string message, HttpStatusCode statusCode)
    {
        return new AdmissionReview
        {
            Response = new AdmissionResponse
            {
                Uid = uid,
                Allowed = false,
                Status = new AdmissionStatus
                {
                    Message = message,
                    Code = (int)statusCode,
                },
            },
        };
    }
}
