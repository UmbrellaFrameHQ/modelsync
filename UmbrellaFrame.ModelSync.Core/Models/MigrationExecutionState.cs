namespace UmbrellaFrame.ModelSync.Core
{
    public enum MigrationExecutionState
    {
        Committed = 1,
        CompletedWithoutTransaction = 2,
        RolledBack = 3,
        PartiallyApplied = 4,
        LockTimeout = 5,
        Cancelled = 6,
        Failed = 7
    }
}
