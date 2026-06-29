using UmbrellaFrame.ModelSync.Core.SqlGeneration;

namespace UmbrellaFrame.ModelSync.MySql
{
    public static class MySqlProviderDescriptor
    {
        public static ModelSyncProviderDescriptor Create()
            => new ModelSyncProviderDescriptor
            {
                ProviderId = "mysql",
                OpenQuote = "`",
                CloseQuote = "`",
                SupportsSchemas = true,
                AutoIncrementKeyword = "AUTO_INCREMENT",
                AddColumnKeyword = "ADD COLUMN",
                AlterColumnTypeStyle = AlterColumnTypeStyle.ModifyColumn,
                DefaultConstraintStyle = DefaultConstraintStyle.AlterColumnSetDefault,
                HistoryStyle = HistorySqlStyle.DuplicateKeyUpdate,
                CatalogStyle = CatalogQueryStyle.StandardInformationSchema,
                RoutineCreationMode = RoutineCreationMode.DropCreate,
                RoutineCatalogStyle = RoutineCatalogStyle.ShowCreateRoutine,
                MigrationLockStyle = MigrationLockStyle.NamedRoutine,
                SystemDatabaseNames = new[] { "mysql", "information_schema", "performance_schema", "sys" }
            };
    }
}
