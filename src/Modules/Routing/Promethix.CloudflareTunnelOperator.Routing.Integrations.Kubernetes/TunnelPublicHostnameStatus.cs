using k8s.Models;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

public sealed class TunnelPublicHostnameStatus
{
    [JsonPropertyName("observedGeneration")]
    public long? ObservedGeneration { get; set; }

    [JsonPropertyName("ownershipTag")]
    public string? OwnershipTag { get; set; }

    [JsonPropertyName("appliedTunnelName")]
    public string? AppliedTunnelName { get; set; }

    [JsonPropertyName("appliedHostname")]
    public string? AppliedHostname { get; set; }

    [JsonPropertyName("conditions")]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Setter required for Kubernetes status deserialization.")]
    public IList<V1Condition> Conditions { get; set; } = [];
}
