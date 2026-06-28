using Promethix.CloudflareTunnelOperator.Routing.Domain;

namespace Promethix.CloudflareTunnelOperator.Routing.Application;

public sealed record RouteCleanupResult(
    string Hostname,
    RoutePlan Plan,
    bool ChangesApplied,
    bool ApplyBlocked = false,
    string? ApplyBlockReason = null,
    string? ApplyBlockMessage = null);
