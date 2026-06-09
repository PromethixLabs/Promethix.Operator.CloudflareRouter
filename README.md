# Promethix Cloudflare Tunnel Operator

`Promethix.CloudflareTunnelOperator` is a .NET 10 Kubernetes-oriented control-plane service for reconciling cluster-declared public hostname publication intent into Cloudflare Tunnel route configuration.

The current shape focuses on one narrow responsibility:

- reconcile explicit hostname publication intent
- target an existing remotely managed Cloudflare Tunnel
- manage only routes owned by this operator instance
- stay container-friendly and ready for Kubernetes deployment

## Solution Shape

```text
src
|-- Modules
|   `-- Routing
|       |-- Promethix.CloudflareTunnelOperator.Routing.Application
|       |-- Promethix.CloudflareTunnelOperator.Routing.Domain
|       `-- Promethix.CloudflareTunnelOperator.Routing.Integrations.Cloudflare
`-- Bootstrap
    `-- Promethix.CloudflareTunnelOperator.Hosting

tests
`-- Promethix.CloudflareTunnelOperator.Routing.Tests
```

## Current Scope

The scaffold includes:

- explicit domain types for managed public hostname routes
- a dedicated `TunnelPublicHostname` CRD contract
- target modes for ingress-backed publication and direct origin publication
- class-filtered Kubernetes intent loading for a remotely managed tunnel
- reconciliation planning that respects operator ownership
- a watch-driven hosted control loop with health checks and structured logging
- configuration objects and startup validation
- a Cloudflare adapter for remotely managed tunnel configuration
- Docker and Kubernetes deployment assets
- a starter Helm chart and CRD manifest

The operator currently prefers an ingress-backed model:

- apps continue using normal Kubernetes `Ingress`
- Traefik remains responsible for middleware, TLS, and backend routing
- the operator publishes the hostname through Cloudflare Tunnel to a dedicated internal Traefik target
- when `operator.ingressTargetUrl` uses `https`, the dedicated Traefik origin must present a certificate valid for that origin name
- ingress-backed CRDs can optionally override the default target with an explicit ingress service reference

The operator still keeps a direct origin mode for workloads that should not traverse Traefik.

The scaffold does not yet implement:

- ingress existence validation against Kubernetes `Ingress` resources
- direct TCP or non-HTTP tunnel targets
- metrics and richer operational diagnostics

## Examples

Starter manifests are in [examples](/c:/Source/Git/Promethix.Operator.CloudflareRouter/examples):

- [ingress-backed-app.yaml](/c:/Source/Git/Promethix.Operator.CloudflareRouter/examples/ingress-backed-app.yaml)
- [direct-origin-app.yaml](/c:/Source/Git/Promethix.Operator.CloudflareRouter/examples/direct-origin-app.yaml)

## Running Locally

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/Bootstrap/Promethix.CloudflareTunnelOperator.Hosting
```

Health endpoints:

- `GET /health/live`
- `GET /health/ready`

## Notes

Further architecture notes are in [docs/architecture.md](/c:/Source/Git/Promethix.Operator.CloudfareRouter/docs/architecture.md).
