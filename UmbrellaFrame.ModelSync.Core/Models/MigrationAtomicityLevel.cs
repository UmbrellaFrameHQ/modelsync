namespace UmbrellaFrame.ModelSync.Core
{
    public enum MigrationAtomicityLevel
    {
        Full = 1,
        HistoryOnly = 2,
        Partial = 3,
        None = 4,
        Unsupported = 5
    }
}
