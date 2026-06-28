namespace Promethix.CloudflareTunnelOperator.Hosting;

internal sealed class OperatorState
{
    public bool HasCompletedInitialReconciliation { get; private set; }

    public bool HasCompletedInitialFullInventoryPass { get; private set; }

    public bool HasObservedWatchActivity { get; private set; }

    public bool IsStartupSafeForMutation { get; private set; }

    public string? StartupBlockReason { get; private set; }

    public string? StartupBlockMessage { get; private set; }

    public DateTimeOffset? LastCompletedAtUtc { get; private set; }

    public void MarkReconciliationCompleted(DateTimeOffset completedAtUtc)
    {
        HasCompletedInitialReconciliation = true;
        LastCompletedAtUtc = completedAtUtc;
    }

    public void MarkInitialFullInventoryPass(DateTimeOffset completedAtUtc, bool startupSafeForMutation, string? reason = null, string? message = null)
    {
        HasCompletedInitialFullInventoryPass = true;
        IsStartupSafeForMutation = startupSafeForMutation;
        StartupBlockReason = startupSafeForMutation ? null : reason;
        StartupBlockMessage = startupSafeForMutation ? null : message;
        MarkReconciliationCompleted(completedAtUtc);
    }

    public bool MarkWatchActivityObserved()
    {
        if (HasObservedWatchActivity)
        {
            return false;
        }

        HasObservedWatchActivity = true;
        return true;
    }
}
