# Architecture Notes

## Intent

The operator is designed as a narrow, explicit control-loop service rather than a broad automation platform. The initial focus is safe reconciliation of HTTP/S public hostname routes for an existing Cloudflare Tunnel.

## Main Decisions

### Capability-first but pragmatic

The solution uses a single `Routing` capability with explicit `Domain`, `Application`, and `Integrations.Cloudflare` projects plus a `Hosting` bootstrap project. That keeps boundaries clear without inventing modules that do not exist yet.

### Explicit ownership

Managed routes carry an ownership marker. Reconciliation only proposes deletion for routes already owned by this operator instance. That creates room for shared-tunnel scenarios and safer rollout.

### Explicit Kubernetes intent contract

Route intent now lives in a dedicated `TunnelPublicHostname` custom resource rather than annotations on unrelated ingress resources. That keeps tunnel-specific concerns explicit:

- class-based selection
- tunnel selection
- origin URL and protocol
- future Cloudflare-specific options
- reconciliation status

### Simple reconciliation flow

The control loop shape is:

```text
TunnelPublicHostname CRDs -> Kubernetes integration -> application planner -> Cloudflare adapter -> health/logging
```

The current scaffold keeps Kubernetes and Cloudflare interactions behind explicit integrations. The Kubernetes side now reads class-filtered CRD intent for a single managed tunnel, leaving room for later watch registration, finalizers, and status updates.

### Production-minded defaults

The host includes:

- options validation on startup
- structured logging
- liveness and readiness endpoints
- container-oriented `8080` HTTP binding
- a background reconciliation worker

## Growth Path

Likely next steps:

1. Replace list-based CRD loading with watch-driven event queueing and status updates.
2. Implement the Cloudflare API client and remote diff/application logic.
3. Add conflict handling, status conditions, metrics, and richer tests.
