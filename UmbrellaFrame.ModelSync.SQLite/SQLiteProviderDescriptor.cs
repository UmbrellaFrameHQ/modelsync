using UmbrellaFrame.ModelSync.Core.SqlGeneration;

namespace UmbrellaFrame.ModelSync.SQLite
{
    public static class SQLiteProviderDescriptor
    {
        public static ModelSyncProviderDescriptor Create()
            => new ModelSyncProviderDescriptor
            {
                ProviderId = "sqlite",
                OpenQuote = "\"",
                CloseQuote = "\"",
                SupportsSchemas = false,
                OmitSchemaInDdl = true,
                GeneratedValuePlacement = GeneratedValuePlacement.AfterInlinePrimaryKey,
                RowIdKeyword = "AUTOINCREMENT",
                AddColumnKeyword = "ADD COLUMN",
                SupportsAddForeignKey = false,
                SupportsAddCheckConstraint = false,
                SupportsAddDefaultConstraint = false,
                DefaultConstraintStyle = DefaultConstraintStyle.Unsupported,
                HistoryStyle = HistorySqlStyle.FileStoreConflictUpdate,
                CatalogStyle = CatalogQueryStyle.FilePragma,
                SupportsStoredProcedures = false,
                RoutineCreationMode = RoutineCreationMode.Unsupported,
                RoutineCatalogStyle = RoutineCatalogStyle.Unsupported,
                MigrationLockStyle = MigrationLockStyle.FileImmediateTransaction
            };
    }
}
