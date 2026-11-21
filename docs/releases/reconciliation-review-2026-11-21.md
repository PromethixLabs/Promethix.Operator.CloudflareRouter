# Reconciliation incident review — 2026-11-21

## Confirmed cause and current source

Affected rc2: RouteIntentWatchService accepted status-only Modified events; status updater patched failure status; OperatorWorker immediately processed another event; CloudflareTunnelRouteClient.GetConfigurationAsync issued GET and received 429. Both workers ran without leader election. This is a control-plane failure; existing tunnel traffic does not require successful reconciliation.

Commit c593972 (immutable prerelease 1.2.1-alpha-c593972.190) added RouteIntentChangeFilter.ShouldReconcile (status/resourceVersion/managedFields ignored; bookmarks ignored), full-pass coalescing in RouteIntentWorkQueue, no-op status comparison and generation-aware conditions in KubernetesRouteIntentStatusUpdater, shared CloudflareRequestGate, and Lease election in OperatorWorker.ExecuteAsync. Inspect those methods rather than assuming rc2 behavior still exists on main. Full passes share route inventory; mutation paths reread fresh configuration to preserve external routes. Security policy inventory remains per hostname. Dependency changes converge through periodic inventory.

## Remaining failure paths and local patch

OperatorWorker.RunIterationAsync catches ReconciliationFailedException then calls UpdateFailureAsync inside that catch. An exception from this status write is not caught by sibling catch clauses, escaping RunLeaderAsync and ExecuteAsync and stopping the host. The local patch adds a cancellation-preserving iteration boundary with a five-second delay after reporting failures. It does not set BackgroundServiceExceptionBehavior.Ignore, which could leave a dead reconciler unnoticed. Election-task faults remain an additional path requiring supervision before production restoration.

Readiness previously returned Degraded for missing initial inventory before evaluating webhook TLS/listener failure. Since Degraded maps to HTTP 200, a passive replica could mask webhook failure. The patch evaluates webhook health first. Healthy passive webhook replicas can remain available while Cloudflare inventory is unavailable.

## Architecture and remaining work

Use one Lease holder per tunnel/installation; all replicas serve local admission. Lease coordination only protects replicas sharing a cluster and lock identity, not separate clusters. Retain cancellation and fresh inventory before mutations. A shared process gate already serializes both typed Cloudflare clients and honors Retry-After delta/date with jittered bounded backoff on 429. It performs no inline request replay. Watch events cannot bypass its deadline. Cooldown resets on leader process replacement.

Before declaring this production-grade, add deterministic clock/random injection; bounded attempts per failed work episode; cooldown for 5xx/network failures; Cloudflare rate-limit-header parsing; election failure supervision; and metrics for queue depth, attempts, failure class, 429, retry deadline and last successful cycle. Current repeated periodic reconciliation has no finite retry exhaustion and the gate only backs off 429. Avoid replaying uncertain mutation requests: read and compare fresh state first. Account-wide coordination with external-dns requires a shared budget or conservative client limits; process-local cooldown cannot reserve external-dns capacity.

Token sharing is UNVERIFIED: no live cluster or secret access was performed. Compare secret reference names and account IDs from approved GitOps manifests; if needed compare token fingerprints in a controlled environment without printing token values. Different tokens can still consume shared account allowances. Do not conclude separate budgets from separate secret names.

## Verification and restoration plan

Keep GitOps replicas at zero during review. Retain existing routes, admission failurePolicy Ignore, namespace enforcement, security policies, CreateUpdateOnly, startup protection and zero deletion limits. No cluster or Cloudflare changes were performed during this investigation.

In staging, test status/bookmark filtering, spec and deletion convergence, identical failure no-op patches, 429 delta/date and recovery, cooldown under event bursts, jitter bounds, retry exhaustion, Kubernetes timeout/status-report failure containment, two-replica election and failover, passive webhook health, and incomplete-inventory mutation blocking. Existing regression coverage is useful but does not cover every one of these acceptance criteria.

After all gaps are resolved, publish a new immutable candidate; do not overwrite the prior prerelease. Apply candidate chart including scoped Lease RBAC through GitOps with one replica first. Observe at least five minutes after cooldown: stable resourceVersions for identical failures, bounded request counts, conditions recovering for all 22 hostnames, no restarts, expected HTTP responses, and external-dns recovery. Validate staged spec changes and deletion protection. Only then optionally increase to two replicas and verify exactly one Lease holder and one active reconciler, both webhook endpoints ready. Restore failurePolicy Fail only after admission health is verified according to operational policy.

Rollback by returning GitOps replicas to zero and keeping failurePolicy Ignore; retain routes and ownership state. Rolling back to rc2 with active replicas would restore the incident defect. Do not restart cloudflared, Traefik or application workloads.
