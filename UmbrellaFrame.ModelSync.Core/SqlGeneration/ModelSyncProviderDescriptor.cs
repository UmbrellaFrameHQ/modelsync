using System;
using System.Collections.Generic;

namespace UmbrellaFrame.ModelSync.Core.SqlGeneration
{
    public sealed class ModelSyncProviderDescriptor
    {
        public string ProviderId { get; set; } = string.Empty;

        public string OpenQuote { get; set; } = "\"";

        public string CloseQuote { get; set; } = "\"";

        public bool SupportsSchemas { get; set; } = true;

        public bool OmitSchemaInDdl { get; set; }

        public GeneratedValuePlacement GeneratedValuePlacement { get; set; } = GeneratedValuePlacement.AfterStoreType;

        public string IdentityKeyword { get; set; } = string.Empty;

        public string AutoIncrementKeyword { get; set; } = string.Empty;

        public string RowIdKeyword { get; set; } = string.Empty;

        public string AddColumnKeyword { get; set; } = "ADD COLUMN";

        public AlterColumnTypeStyle AlterColumnTypeStyle { get; set; } = AlterColumnTypeStyle.AlterColumn;

        public bool SupportsAddForeignKey { get; set; } = true;

        public bool SupportsAddCheckConstraint { get; set; } = true;

        public bool SupportsAddDefaultConstraint { get; set; } = true;

        public DefaultConstraintStyle DefaultConstraintStyle { get; set; } = DefaultConstraintStyle.AlterColumnSetDefault;

        public HistorySqlStyle HistoryStyle { get; set; } = HistorySqlStyle.QualifiedMerge;

        public CatalogQueryStyle CatalogStyle { get; set; } = CatalogQueryStyle.StandardInformationSchema;

        public IReadOnlyList<string> SystemDatabaseNames { get; set; } = Array.Empty<string>();

        public string CurrentTimestampExpression { get; set; } = "CURRENT_TIMESTAMP";

        public bool RequiresCreateSchemaGuard { get; set; }

        public bool SupportsStoredProcedures { get; set; } = true;

        public RoutineCreationMode RoutineCreationMode { get; set; } = RoutineCreationMode.CreateOrReplace;

        public RoutineCatalogStyle RoutineCatalogStyle { get; set; } = RoutineCatalogStyle.FunctionCatalog;

        public MigrationLockStyle MigrationLockStyle { get; set; } = MigrationLockStyle.Unsupported;
    }

    public enum GeneratedValuePlacement
    {
        AfterStoreType,
        AfterInlinePrimaryKey
    }

    public enum AlterColumnTypeStyle
    {
        AlterColumn,
        ModifyColumn,
        AlterColumnType
    }

    public enum DefaultConstraintStyle
    {
        AlterColumnSetDefault,
        NamedConstraintForColumn,
        Unsupported
    }

    public enum HistorySqlStyle
    {
        QualifiedMerge,
        DuplicateKeyUpdate,
        ConflictUpdate,
        FileStoreConflictUpdate
    }

    public enum CatalogQueryStyle
    {
        NativeSystemCatalog,
        StandardInformationSchema,
        ObjectRelationalCatalog,
        FilePragma
    }

    public enum RoutineCreationMode
    {
        CreateOrAlter,
        DropCreate,
        CreateOrReplace,
        Unsupported
    }

    public enum RoutineCatalogStyle
    {
        SystemModuleCatalog,
        ShowCreateRoutine,
        FunctionCatalog,
        Unsupported
    }

    public enum MigrationLockStyle
    {
        Unsupported,
        ApplicationRoutine,
        NamedRoutine,
        AdvisoryRoutine,
        FileImmediateTransaction
    }
}
