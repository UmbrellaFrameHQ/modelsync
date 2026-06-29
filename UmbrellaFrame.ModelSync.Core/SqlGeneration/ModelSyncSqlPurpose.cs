namespace UmbrellaFrame.ModelSync.Core.SqlGeneration
{
    public enum ModelSyncSqlPurpose
    {
        Ddl,
        Introspection,
        History,
        Reset,
        Readiness,
        MigrationLock,
        StoredProcedure,
        Transaction
    }
}
