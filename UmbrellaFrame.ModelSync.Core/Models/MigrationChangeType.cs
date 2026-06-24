namespace UmbrellaFrame.ModelSync.Core
{
    /// <summary>Describes how a migration script will be handled.</summary>
    public enum MigrationChangeType
    {
        None = 0,
        Apply = 1,
        Reapply = 2
    }
}
