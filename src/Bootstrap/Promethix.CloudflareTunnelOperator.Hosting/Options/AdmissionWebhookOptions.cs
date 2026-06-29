namespace Promethix.CloudflareTunnelOperator.Hosting.Options;

internal sealed class AdmissionWebhookOptions
{
    public const string SectionName = "AdmissionWebhook";

    public int ManagementPort { get; set; } = 8080;

    public bool Enabled { get; set; }

    public int Port { get; set; } = 8443;

    public string Path { get; set; } = "/admission/validate-tunnelpublichostname";

    public string CertificatePath { get; set; } = "/tls/tls.crt";

    public string PrivateKeyPath { get; set; } = "/tls/tls.key";
}
