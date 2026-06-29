namespace UmbrellaFrame.ModelSync.Core
{
    /// <summary>Controls how a migration script category is evaluated against history.</summary>
    public enum MigrationScriptExecutionMode
    {
        RunOnce = 0,
        HashTracked = 1,
        EveryRun = 2
    }
}
