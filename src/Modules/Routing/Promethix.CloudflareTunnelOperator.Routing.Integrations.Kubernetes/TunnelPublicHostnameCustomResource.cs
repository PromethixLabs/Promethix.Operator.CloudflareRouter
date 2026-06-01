using k8s.Models;
using System.Text.Json.Serialization;

namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

public sealed class TunnelPublicHostnameCustomResource
{
    public const string Group = "edge.promethix.net";
    public const string Version = "v1alpha1";
    public const string Kind = "TunnelPublicHostname";
    public const string PluralName = "tunnelpublichostnames";

    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; } = $"{Group}/{Version}";

    [JsonPropertyName("kind")]
    public string ResourceKind { get; set; } = Kind;

    [JsonPropertyName("metadata")]
    public V1ObjectMeta Metadata { get; set; } = new();

    [JsonPropertyName("spec")]
    public TunnelPublicHostnameSpec Spec { get; set; } = new();

    [JsonPropertyName("status")]
    public TunnelPublicHostnameStatus? Status { get; set; }
}
