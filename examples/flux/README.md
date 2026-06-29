# Flux Example

These manifests show one way to deploy the operator with Flux using the public Promethix Labs Helm repository.

They are intentionally generic. Replace:

- Cloudflare account, tunnel, and token values
- namespace names
- ingress target service URL
- image tag, if you want to override the chart `appVersion`
- chart version range

Files:

- `namespace.yaml`
- `cloudflare-secret.example.yaml`
- `helmrepository.yaml`
- `helmrelease.yaml`
- `helmrelease.shared-cluster.yaml`
- `kustomization.yaml`

Do not commit real Cloudflare credentials in plain text. Use SOPS, External Secrets, Vault Secrets Operator, or another secret management approach for production.

`helmrelease.yaml` is the simpler baseline example.

`helmrelease.shared-cluster.yaml` shows a stricter shared-cluster posture:

- namespace hostname policy enabled
- operator-wide hostname suffix allowlist enabled
- cross-namespace direct targets disabled
- destructive Cloudflare mutations blocked by default
- webhook enabled with cert-manager-backed TLS
- ingress service override mode set to `ConfiguredTargetOnly`, so only the approved shared ingress target is accepted
