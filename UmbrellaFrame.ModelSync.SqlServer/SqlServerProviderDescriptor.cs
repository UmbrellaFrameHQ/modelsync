using UmbrellaFrame.ModelSync.Core.SqlGeneration;

namespace UmbrellaFrame.ModelSync.SqlServer
{
    public static class SqlServerProviderDescriptor
    {
        public static ModelSyncProviderDescriptor Create()
            => new ModelSyncProviderDescriptor
            {
                ProviderId = "sqlserver",
                OpenQuote = "[",
                CloseQuote = "]",
                SupportsSchemas = true,
                RequiresCreateSchemaGuard = true,
                IdentityKeyword = "IDENTITY({seed},{increment})",
                AddColumnKeyword = "ADD",
                AlterColumnTypeStyle = AlterColumnTypeStyle.AlterColumn,
                DefaultConstraintStyle = DefaultConstraintStyle.NamedConstraintForColumn,
                HistoryStyle = HistorySqlStyle.QualifiedMerge,
                CatalogStyle = CatalogQueryStyle.NativeSystemCatalog,
                RoutineCreationMode = RoutineCreationMode.CreateOrAlter,
                RoutineCatalogStyle = RoutineCatalogStyle.SystemModuleCatalog,
                MigrationLockStyle = MigrationLockStyle.ApplicationRoutine,
                SystemDatabaseNames = new[] { "master", "model", "msdb", "tempdb" }
            };
    }
}
