using System.Text.Json;
using System.Text.Json.Serialization;

namespace Promethix.CloudflareTunnelOperator.Hosting.Admission;

internal sealed class AdmissionReview
{
    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; } = "admission.k8s.io/v1";

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "AdmissionReview";

    [JsonPropertyName("request")]
    public AdmissionRequest? Request { get; set; }

    [JsonPropertyName("response")]
    public AdmissionResponse? Response { get; set; }
}

internal sealed class AdmissionRequest
{
    [JsonPropertyName("uid")]
    public string Uid { get; set; } = string.Empty;

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public JsonElement Object { get; set; }
}

internal sealed class AdmissionResponse
{
    [JsonPropertyName("uid")]
    public string Uid { get; set; } = string.Empty;

    [JsonPropertyName("allowed")]
    public bool Allowed { get; set; }

    [JsonPropertyName("status")]
    public AdmissionStatus? Status { get; set; }
}

internal sealed class AdmissionStatus
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public int Code { get; set; }
}
