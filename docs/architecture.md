# Architecture Notes

## Intent

The operator is designed as a narrow, explicit control-loop service rather than a broad automation platform. The current focus is safe publication of HTTP/S public hostnames for an existing Cloudflare Tunnel.

## Main Decisions

### Capability-first but pragmatic

The solution uses a single `Routing` capability with explicit `Domain`, `Application`, and `Integrations.Cloudflare` projects plus a `Hosting` bootstrap project. That keeps boundaries clear without inventing modules that do not exist yet.

### Explicit ownership

Managed routes carry an ownership marker. Reconciliation only proposes deletion for routes already owned by this operator instance. That creates room for shared-tunnel scenarios and safer rollout.

### Explicit Kubernetes intent contract

Route publication intent lives in a dedicated `TunnelPublicHostname` custom resource rather than annotations on unrelated ingress resources. That keeps tunnel-specific concerns explicit:

- class-based selection
- tunnel selection
- target mode selection
- future direct-target support
- reconciliation status

The CRD now distinguishes between:

- `target.mode: ingress`
  - preferred common-case flow
  - Cloudflare Tunnel targets a dedicated internal Traefik service
  - apps still use normal Kubernetes `Ingress`
- `target.mode: direct`
  - retained as an escape hatch for workloads that should not traverse Traefik
  - currently limited to HTTP/S origins

### Simple reconciliation flow

The control loop shape is:

```text
TunnelPublicHostname CRDs -> Kubernetes integration -> route resolution -> application planner -> Cloudflare adapter -> health/logging
```

Ingress-backed mode intentionally narrows responsibility:

- Traefik owns HTTP routing, middleware, and certificate termination
- `external-dns` and `cert-manager` continue using normal ingress annotations
- the operator owns only Cloudflare Tunnel hostname publication

The Kubernetes side reads class-filtered CRD intent for a single managed tunnel and resolves ingress-backed routes to an operator-configured dedicated Traefik target URL.

Ingress-backed mode supports two resolution paths:

- default shared target via `KubernetesOperator:IngressTargetUrl`
- explicit CRD override via `spec.target.ingress.service`

The CRD-level service override is still constrained by `spec.target.ingress.className`, so the operator keeps a policy boundary around which ingress path may be used for managed tunnel publication.

If that target URL uses HTTPS, the dedicated Traefik origin is expected to present a certificate valid for the configured target name. The operator currently treats that origin URL as authoritative rather than modeling per-hostname SNI overrides.

### Production-minded defaults

The host includes:

- options validation on startup
- structured logging
- liveness and readiness endpoints
- container-oriented `8080` HTTP binding
- a background reconciliation worker

## Growth Path

Likely next steps:

1. Validate ingress-backed intents against real Kubernetes `Ingress` resources and ingress class.
2. Add explicit support for direct TCP or non-HTTP tunnel targets.
3. Add metrics and richer operational diagnostics around Cloudflare auth and route sync failures.
