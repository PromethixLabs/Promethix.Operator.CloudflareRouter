namespace Promethix.CloudflareTunnelOperator.Hosting.Admission;

internal sealed class AdmissionWebhookRuntimeState
{
    public bool Enabled { get; init; }

    public bool ListenerReady { get; set; }

    public bool CertificateFilesPresent { get; set; }

    public string? FailureReason { get; set; }
}
