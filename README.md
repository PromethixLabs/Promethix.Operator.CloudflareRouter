# Promethix Cloudflare Tunnel Operator

[![Build Status](https://drone.promethix.net/api/badges/gentoorax/Promethix.Operator.CloudflareRouter/status.svg)](https://drone.promethix.net/gentoorax/Promethix.Operator.CloudflareRouter)

Promethix Cloudflare Tunnel Operator is a Kubernetes operator for publishing cluster services through an existing Cloudflare Tunnel.

The current focus is HTTP and HTTPS public hostname management for remotely managed tunnels. The operator is intended to work with explicit cluster-declared intent, predictable ownership, and safe reconciliation.

## What it does

- watches `TunnelPublicHostname` resources
- reconciles public hostnames into Cloudflare Tunnel configuration
- supports ingress-backed publication through a dedicated Traefik path
- keeps a direct origin mode for workloads that should not traverse Traefik
- updates resource status and respects ownership when reconciling shared tunnel config

## Current model

The preferred path is ingress-backed:

- workloads use normal Kubernetes `Ingress`
- Traefik handles routing, middleware, and TLS
- the operator publishes the hostname through Cloudflare Tunnel
- for HTTPS ingress targets, the operator sets Cloudflare `originRequest.originServerName` to the public hostname so cloudflared can verify the Traefik certificate correctly

Direct origin publication is also supported for cases where going through ingress is not appropriate.

## Project layout

```text
src
|-- Modules
|   `-- Routing
|       |-- Promethix.CloudflareTunnelOperator.Routing.Application
|       |-- Promethix.CloudflareTunnelOperator.Routing.Domain
|       |-- Promethix.CloudflareTunnelOperator.Routing.Integrations.Cloudflare
|       `-- Promethix.CloudflareTunnelOperator.Routing.Integrations.Kubernetes
`-- Bootstrap
    `-- Promethix.CloudflareTunnelOperator.Hosting

tests
`-- Promethix.CloudflareTunnelOperator.Routing.Tests
```

## Examples

Example manifests are in [examples](examples):

- [ingress-backed-app.yaml](examples/ingress-backed-app.yaml)
- [direct-origin-app.yaml](examples/direct-origin-app.yaml)

## Development

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/Bootstrap/Promethix.CloudflareTunnelOperator.Hosting
```

Health endpoints:

- `GET /health/live`
- `GET /health/ready`

## Status

This project is still under active development. The core reconciliation and Kubernetes integration are in place, but the operator is not yet feature-complete.

Design notes are in [docs/architecture.md](docs/architecture.md).

## License

Licensed under the GNU General Public License v2. See `LICENSE`.
