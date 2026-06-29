namespace UmbrellaFrame.ModelSync.Core
{
    public enum ModelSyncOperationRisk
    {
        Safe = 0,
        SkippedByOption = 1,
        Risky = 2,
        Destructive = 3,
        Unsupported = 4
    }
}
