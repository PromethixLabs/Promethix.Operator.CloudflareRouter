# Promethix Cloudflare Tunnel Operator

`Promethix.CloudflareTunnelOperator` is a .NET 10 Kubernetes-oriented control-plane service for reconciling cluster-declared public hostname intent into Cloudflare Tunnel route configuration.

The first scaffold focuses on one narrow responsibility:

- reconcile explicit hostname-to-origin intent
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
- class-filtered Kubernetes intent loading for a remotely managed tunnel
- reconciliation planning that respects operator ownership
- a hosted control loop with health checks and structured logging
- configuration objects and startup validation
- a stub Cloudflare adapter and a Kubernetes CRD integration scaffold
- Docker and Kubernetes deployment assets
- a starter Helm chart and CRD manifest

The scaffold does not yet implement:

- live Cloudflare API calls
- watch-driven event queueing
- status publishing back into the cluster
- finalizers and deletion workflows against the Kubernetes API

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
