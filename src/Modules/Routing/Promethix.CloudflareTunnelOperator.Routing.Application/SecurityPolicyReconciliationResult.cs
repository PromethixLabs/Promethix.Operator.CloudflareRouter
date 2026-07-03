using Promethix.CloudflareTunnelOperator.Routing.Domain;

namespace Promethix.CloudflareTunnelOperator.Routing.Application;

public sealed record SecurityPolicyReconciliationResult(
    HostnameSecurityPolicy Policy,
    SecurityPolicyPlan Plan,
    bool ChangesApplied);
