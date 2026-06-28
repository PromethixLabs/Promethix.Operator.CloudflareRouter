namespace Promethix.CloudflareTunnelOperator.Routing.Application;

public sealed record RouteMutationSafetyDecision(
    bool AllowApply,
    string? Reason = null,
    string? Message = null);
