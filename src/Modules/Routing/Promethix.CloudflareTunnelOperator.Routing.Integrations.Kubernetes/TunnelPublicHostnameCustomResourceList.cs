using k8s.Models;
using System.Text.Json.Serialization;

namespace Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes;

public sealed class TunnelPublicHostnameCustomResourceList
{
    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; } = $"{TunnelPublicHostnameCustomResource.Group}/{TunnelPublicHostnameCustomResource.Version}";

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "TunnelPublicHostnameList";

    [JsonPropertyName("metadata")]
    public V1ListMeta Metadata { get; set; } = new();

    [JsonPropertyName("items")]
    public IList<TunnelPublicHostnameCustomResource> Items { get; } = [];
}
