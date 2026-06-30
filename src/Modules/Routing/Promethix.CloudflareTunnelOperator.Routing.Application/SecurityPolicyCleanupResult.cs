using Promethix.CloudflareTunnelOperator.Routing.Domain;

namespace Promethix.CloudflareTunnelOperator.Routing.Application;

public sealed record SecurityPolicyCleanupResult(
    string Hostname,
    SecurityPolicyPlan Plan,
    bool ChangesApplied);
