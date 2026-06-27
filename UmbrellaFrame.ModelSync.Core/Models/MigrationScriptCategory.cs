namespace UmbrellaFrame.ModelSync.Core
{
    /// <summary>Supported migration script categories.</summary>
    public enum MigrationScriptCategory
    {
        Tables = 0,
        StoredProcedures = 1,
        Triggers = 2,
        Seeds = 3,
        CustomSql = 4
    }
}
