namespace UmbrellaFrame.ModelSync.Core
{
    public enum MigrationExecutionAction
    {
        Applied = 1,
        Reapplied = 2,
        Skipped = 3,
        Blocked = 4,
        Failed = 5
    }
}
