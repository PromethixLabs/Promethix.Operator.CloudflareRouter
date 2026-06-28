using Promethix.CloudflareTunnelOperator.Routing.Domain;

namespace Promethix.CloudflareTunnelOperator.Routing.Application;

public sealed record ReconciliationResult(
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    RouteIntentDocument Intent,
    RoutePlan Plan,
    bool ChangesApplied,
    bool ApplyBlocked = false,
    string? ApplyBlockReason = null,
    string? ApplyBlockMessage = null);
