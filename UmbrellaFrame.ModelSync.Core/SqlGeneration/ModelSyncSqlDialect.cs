using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UmbrellaFrame.ModelSync.Core.SqlGeneration
{
    public sealed class ModelSyncSqlDialect
    {
        private readonly ModelSyncProviderDescriptor _descriptor;

        public ModelSyncSqlDialect(ModelSyncProviderDescriptor descriptor)
        {
            _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            if (string.IsNullOrWhiteSpace(_descriptor.ProviderId))
                throw new ArgumentException("Provider descriptor must define ProviderId.", nameof(descriptor));
        }

        public string Quote(string identifier)
        {
            ValidateIdentifier(identifier);
            return _descriptor.OpenQuote + identifier.Replace(_descriptor.CloseQuote, _descriptor.CloseQuote + _descriptor.CloseQuote) + _descriptor.CloseQuote;
        }

        public string Qualify(string schema, string table)
        {
            if (_descriptor.OmitSchemaInDdl || !_descriptor.SupportsSchemas || string.IsNullOrWhiteSpace(schema))
                return Quote(table);
            return Quote(schema) + "." + Quote(table);
        }

        public string BuildCreateTableSql(ModelTableDefinition table)
        {
            var lines = new List<string>();
            var primaryKeys = table.Columns.Where(c => c.IsPrimaryKey).Select(c => c.Name).ToList();
            foreach (var column in table.Columns)
                lines.Add("    " + BuildColumnDefinition(column, primaryKeys.Count <= 1));
            if (primaryKeys.Count > 1)
                lines.Add("    PRIMARY KEY (" + string.Join(", ", primaryKeys.Select(Quote)) + ")");

            return "CREATE TABLE " + Qualify(table.Schema, table.Name) + " (" + Environment.NewLine +
                   string.Join("," + Environment.NewLine, lines) + Environment.NewLine + ");";
        }

        public string BuildAddColumnSql(ModelTableDefinition table, ModelColumnDefinition column)
            => "ALTER TABLE " + Qualify(table.Schema, table.Name) + " " + _descriptor.AddColumnKeyword + " " + BuildColumnDefinition(column, true) + ";";

        public string BuildAddDefaultConstraintSql(ModelTableDefinition table, ModelColumnDefinition column)
        {
            if (!_descriptor.SupportsAddDefaultConstraint || _descriptor.DefaultConstraintStyle == DefaultConstraintStyle.Unsupported)
                return string.Empty;
            if (_descriptor.DefaultConstraintStyle == DefaultConstraintStyle.NamedConstraintForColumn)
                return "ALTER TABLE " + Qualify(table.Schema, table.Name) + " ADD CONSTRAINT " + Quote("DF_" + table.Name + "_" + column.Name) + " DEFAULT " + column.DefaultSql + " FOR " + Quote(column.Name) + ";";
            if (_descriptor.DefaultConstraintStyle == DefaultConstraintStyle.ModifyColumnDefault)
                return "ALTER TABLE " + Qualify(table.Schema, table.Name) + " MODIFY " + Quote(column.Name) + " DEFAULT " + column.DefaultSql + ";";
            return "ALTER TABLE " + Qualify(table.Schema, table.Name) + " ALTER " + Quote(column.Name) + " SET DEFAULT " + column.DefaultSql + ";";
        }

        public string BuildAlterColumnTypeSql(string schema, string table, string column, string storeType)
        {
            var prefix = "ALTER TABLE " + Qualify(schema, table) + " ";
            switch (_descriptor.AlterColumnTypeStyle)
            {
                case AlterColumnTypeStyle.ModifyColumn:
                    return prefix + "MODIFY COLUMN " + Quote(column) + " " + storeType + ";";
                case AlterColumnTypeStyle.Modify:
                    return prefix + "MODIFY " + Quote(column) + " " + storeType + ";";
                case AlterColumnTypeStyle.AlterColumnType:
                    return prefix + "ALTER COLUMN " + Quote(column) + " TYPE " + storeType + ";";
                default:
                    return prefix + "ALTER COLUMN " + Quote(column) + " " + storeType + ";";
            }
        }

        public IReadOnlyList<string> SystemDatabaseNames => _descriptor.SystemDatabaseNames;

        public ModelSyncSqlCommand BuildEnsureSchemaPlan(string schema)
        {
            if (!_descriptor.SupportsSchemas || string.IsNullOrWhiteSpace(schema))
                return new ModelSyncSqlCommand(string.Empty, ModelSyncSqlPurpose.Ddl);

            if (_descriptor.RequiresCreateSchemaGuard)
                return new ModelSyncSqlCommand(
                    "IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = @SchemaName) EXEC('CREATE SCHEMA " + Quote(schema) + " AUTHORIZATION dbo;');",
                    ModelSyncSqlPurpose.Ddl,
                    new[] { new ModelSyncSqlParameter("@SchemaName", schema) });

            return new ModelSyncSqlCommand("CREATE SCHEMA IF NOT EXISTS " + Quote(schema) + ";", ModelSyncSqlPurpose.Ddl);
        }

        public ModelSyncSqlCommand BuildEnsureHistoryInfrastructurePlan(string schema)
        {
            switch (_descriptor.HistoryStyle)
            {
                case HistorySqlStyle.DuplicateKeyUpdate:
                    return new ModelSyncSqlCommand(BuildHistoryTablesUnqualified("`", "`", "DATETIME", "CURRENT_TIMESTAMP"), ModelSyncSqlPurpose.History);
                case HistorySqlStyle.ConflictUpdate:
                    return new ModelSyncSqlCommand(
                        BuildEnsureSchemaPlan(schema).CommandText + BuildHistoryTablesQualified(schema, "VARCHAR(128)", "VARCHAR(256)", "TIMESTAMP", "CURRENT_TIMESTAMP"),
                        ModelSyncSqlPurpose.History);
                case HistorySqlStyle.FileStoreConflictUpdate:
                    return new ModelSyncSqlCommand(BuildHistoryTablesUnqualified(string.Empty, string.Empty, "TEXT", "CURRENT_TIMESTAMP"), ModelSyncSqlPurpose.History);
                case HistorySqlStyle.OracleMerge:
                    return new ModelSyncSqlCommand(BuildOracleHistoryInfrastructure(), ModelSyncSqlPurpose.History);
                default:
                    return new ModelSyncSqlCommand(BuildQualifiedHistoryInfrastructure(schema), ModelSyncSqlPurpose.History, new[] { new ModelSyncSqlParameter("@SchemaName", schema) });
            }
        }

        public ModelSyncSqlCommand BuildEnsureHistoryHashColumnsPlan(string schema)
        {
            var tables = new[]
            {
                "SchemaMigration_Tables",
                "SchemaMigration_StoredProcedures",
                "SchemaMigration_Triggers",
                "SchemaMigration_Seeds",
                "SchemaMigration_CustomSql"
            };

            if (_descriptor.HistoryStyle == HistorySqlStyle.QualifiedMerge)
            {
                var quotedSchema = Quote(schema);
                var sql = string.Concat(tables.Select(table =>
                    "IF EXISTS (SELECT 1 FROM sys.tables WHERE name = '" + table + "' AND schema_id = SCHEMA_ID(@SchemaName)) " +
                    "AND COL_LENGTH(N'" + schema.Replace("'", "''") + "." + table + "', 'SqlHash') IS NULL " +
                    "ALTER TABLE " + quotedSchema + "." + Quote(table) + " ADD " + Quote("SqlHash") + " NVARCHAR(128) NULL;"));
                return new ModelSyncSqlCommand(sql, ModelSyncSqlPurpose.History, new[] { new ModelSyncSqlParameter("@SchemaName", schema) });
            }

            if (_descriptor.HistoryStyle == HistorySqlStyle.DuplicateKeyUpdate)
            {
                var sql = string.Concat(tables.Select(table =>
                    "SET @ModelSyncSqlHashUpgrade = IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '" + table + "' AND COLUMN_NAME = 'SqlHash') = 0, " +
                    "'ALTER TABLE " + Quote(table) + " ADD COLUMN " + Quote("SqlHash") + " VARCHAR(128) NULL', 'SELECT 1');" +
                    "PREPARE ModelSyncSqlHashUpgradeStatement FROM @ModelSyncSqlHashUpgrade;" +
                    "EXECUTE ModelSyncSqlHashUpgradeStatement;" +
                    "DEALLOCATE PREPARE ModelSyncSqlHashUpgradeStatement;"));
                return new ModelSyncSqlCommand(sql, ModelSyncSqlPurpose.History);
            }

            if (_descriptor.HistoryStyle == HistorySqlStyle.ConflictUpdate)
            {
                var sql = string.Concat(tables.Select(table =>
                    "ALTER TABLE " + Qualify(schema, table) + " ADD COLUMN IF NOT EXISTS " + Quote("SqlHash") + " VARCHAR(128) NULL;"));
                return new ModelSyncSqlCommand(sql, ModelSyncSqlPurpose.History);
            }

            if (_descriptor.HistoryStyle == HistorySqlStyle.OracleMerge)
                return new ModelSyncSqlCommand(string.Empty, ModelSyncSqlPurpose.History);

            return new ModelSyncSqlCommand(string.Empty, ModelSyncSqlPurpose.History);
        }

        public ModelSyncSqlCommand BuildReadHistoryPlan(string schema, MigrationScriptCategory category)
        {
            var table = HistoryTableName(category);
            var target = _descriptor.HistoryStyle == HistorySqlStyle.DuplicateKeyUpdate || _descriptor.HistoryStyle == HistorySqlStyle.FileStoreConflictUpdate || _descriptor.HistoryStyle == HistorySqlStyle.OracleMerge
                ? Quote(table)
                : Qualify(schema, table);
            var id = Quote("Id");
            var hash = Quote("SqlHash");
            return new ModelSyncSqlCommand("SELECT " + id + ", " + hash + " FROM " + target + ";", ModelSyncSqlPurpose.History);
        }

        public ModelSyncSqlCommand BuildReadLegacyHistoryPlan(string schema, MigrationScriptCategory category)
        {
            var table = HistoryTableName(category);
            var target = _descriptor.HistoryStyle == HistorySqlStyle.DuplicateKeyUpdate || _descriptor.HistoryStyle == HistorySqlStyle.FileStoreConflictUpdate || _descriptor.HistoryStyle == HistorySqlStyle.OracleMerge
                ? Quote(table)
                : Qualify(schema, table);
            return new ModelSyncSqlCommand("SELECT " + Quote("Id") + " FROM " + target + ";", ModelSyncSqlPurpose.History);
        }

        public string BuildAddHistoryHashColumnSql(string schema, MigrationScriptCategory category)
        {
            var table = HistoryTableName(category);
            var target = _descriptor.HistoryStyle == HistorySqlStyle.DuplicateKeyUpdate || _descriptor.HistoryStyle == HistorySqlStyle.FileStoreConflictUpdate || _descriptor.HistoryStyle == HistorySqlStyle.OracleMerge
                ? Quote(table)
                : Qualify(schema, table);
            return "ALTER TABLE " + target + " " + _descriptor.AddColumnKeyword + " " + Quote("SqlHash") + " VARCHAR(128) NULL;";
        }

        public ModelSyncSqlCommand BuildRecordHistoryPlan(string schema, MigrationScriptCategory category, string id, string name, string hash)
        {
            var target = _descriptor.HistoryStyle == HistorySqlStyle.DuplicateKeyUpdate || _descriptor.HistoryStyle == HistorySqlStyle.FileStoreConflictUpdate || _descriptor.HistoryStyle == HistorySqlStyle.OracleMerge
                ? Quote(HistoryTableName(category))
                : Qualify(schema, HistoryTableName(category));
            string sql;
            if (_descriptor.HistoryStyle == HistorySqlStyle.DuplicateKeyUpdate)
            {
                sql = "INSERT INTO " + target + "(" + Quote("Id") + ", " + Quote("Name") + ", " + Quote("SqlHash") + ") VALUES (@Id, @Name, @SqlHash) ON DUPLICATE KEY UPDATE " + Quote("Name") + " = VALUES(" + Quote("Name") + "), " + Quote("SqlHash") + " = VALUES(" + Quote("SqlHash") + "), " + Quote("UpdateAt") + " = CURRENT_TIMESTAMP;";
            }
            else if (_descriptor.HistoryStyle == HistorySqlStyle.ConflictUpdate || _descriptor.HistoryStyle == HistorySqlStyle.FileStoreConflictUpdate)
            {
                var excluded = _descriptor.HistoryStyle == HistorySqlStyle.FileStoreConflictUpdate ? "excluded" : "EXCLUDED";
                sql = "INSERT INTO " + target + "(" + Quote("Id") + ", " + Quote("Name") + ", " + Quote("SqlHash") + ") VALUES (@Id, @Name, @SqlHash) ON CONFLICT(" + Quote("Id") + ") DO UPDATE SET " + Quote("Name") + " = " + excluded + "." + Quote("Name") + ", " + Quote("SqlHash") + " = " + excluded + "." + Quote("SqlHash") + ", " + Quote("UpdateAt") + " = CURRENT_TIMESTAMP;";
            }
            else if (_descriptor.HistoryStyle == HistorySqlStyle.OracleMerge)
            {
                sql = "MERGE INTO " + target + " target USING (SELECT :Id AS Id, :Name AS Name, :SqlHash AS SqlHash FROM dual) source ON (target." + Quote("Id") + " = source.Id) WHEN MATCHED THEN UPDATE SET target." + Quote("Name") + " = source.Name, target." + Quote("SqlHash") + " = source.SqlHash, target." + Quote("UpdateAt") + " = SYSTIMESTAMP WHEN NOT MATCHED THEN INSERT (" + Quote("Id") + ", " + Quote("Name") + ", " + Quote("SqlHash") + ") VALUES (source.Id, source.Name, source.SqlHash)";
            }
            else
            {
                sql = "MERGE " + target + " AS target USING (SELECT @Id AS Id, @Name AS Name, @SqlHash AS SqlHash) AS source ON target.Id = source.Id WHEN MATCHED THEN UPDATE SET " + Quote("Name") + " = source.Name, " + Quote("SqlHash") + " = source.SqlHash, " + Quote("UpdateAt") + " = SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT (" + Quote("Id") + ", " + Quote("Name") + ", " + Quote("SqlHash") + ") VALUES (source.Id, source.Name, source.SqlHash);";
            }

            return new ModelSyncSqlCommand(sql, ModelSyncSqlPurpose.History, new[]
            {
                new ModelSyncSqlParameter("@Id", id),
                new ModelSyncSqlParameter("@Name", name),
                new ModelSyncSqlParameter("@SqlHash", hash)
            });
        }

        public ModelSyncSqlCommand BuildParsedColumnExistsPlan(TableColumnDefinition column)
        {
            if (_descriptor.CatalogStyle == CatalogQueryStyle.NativeSystemCatalog)
                return new ModelSyncSqlCommand("SELECT COL_LENGTH(@ObjectName, @ColumnName)", ModelSyncSqlPurpose.Introspection, new[] { new ModelSyncSqlParameter("@ObjectName", column.Schema + "." + column.Table), new ModelSyncSqlParameter("@ColumnName", column.Column) });
            if (_descriptor.CatalogStyle == CatalogQueryStyle.FilePragma)
                return BuildReadFileCatalogTableInfoPlan(column.Table);
            if (_descriptor.CatalogStyle == CatalogQueryStyle.OracleDataDictionary)
                return new ModelSyncSqlCommand("SELECT COUNT(*) FROM USER_TAB_COLUMNS WHERE TABLE_NAME = :Table AND COLUMN_NAME = :Column", ModelSyncSqlPurpose.Introspection, new[] { new ModelSyncSqlParameter(":Table", column.Table), new ModelSyncSqlParameter(":Column", column.Column) });
            return new ModelSyncSqlCommand("SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = @Schema AND table_name = @Table AND column_name = @Column;", ModelSyncSqlPurpose.Introspection, new[] { new ModelSyncSqlParameter("@Schema", column.Schema), new ModelSyncSqlParameter("@Table", column.Table), new ModelSyncSqlParameter("@Column", column.Column) });
        }

        public string BuildAddParsedColumnSql(TableColumnDefinition column)
            => "ALTER TABLE " + Qualify(column.Schema, column.Table) + " " + _descriptor.AddColumnKeyword + " " + Quote(column.Column) + " " + column.Definition + ";";

        public string HistoryTableName(MigrationScriptCategory category)
        {
            switch (category)
            {
                case MigrationScriptCategory.StoredProcedures: return "SchemaMigration_StoredProcedures";
                case MigrationScriptCategory.Triggers: return "SchemaMigration_Triggers";
                case MigrationScriptCategory.Seeds: return "SchemaMigration_Seeds";
                case MigrationScriptCategory.CustomSql: return "SchemaMigration_CustomSql";
                default: return "SchemaMigration_Tables";
            }
        }

        public ModelSyncSqlCommand BuildReadColumnsPlan()
        {
            switch (_descriptor.CatalogStyle)
            {
                case CatalogQueryStyle.NativeSystemCatalog:
                    return new ModelSyncSqlCommand(@"SELECT s.name AS SchemaName, t.name AS TableName, c.name AS ColumnName, ty.name AS TypeName, c.max_length, c.precision, c.scale, c.is_nullable, CASE WHEN dc.object_id IS NULL THEN 0 ELSE 1 END AS HasDefault, CASE WHEN cc.object_id IS NULL THEN 0 ELSE 1 END AS HasCheck FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id JOIN sys.columns c ON c.object_id = t.object_id JOIN sys.types ty ON ty.user_type_id = c.user_type_id LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = t.object_id AND dc.parent_column_id = c.column_id LEFT JOIN sys.check_constraints cc ON cc.parent_object_id = t.object_id AND cc.parent_column_id = c.column_id WHERE s.name = @Schema;", ModelSyncSqlPurpose.Introspection);
                case CatalogQueryStyle.OracleDataDictionary:
                    return new ModelSyncSqlCommand(@"SELECT USER AS table_schema, TABLE_NAME, COLUMN_NAME, DATA_TYPE, DATA_LENGTH, DATA_PRECISION, DATA_SCALE, NULLABLE, CASE WHEN DATA_DEFAULT IS NULL THEN 0 ELSE 1 END AS has_default FROM USER_TAB_COLUMNS", ModelSyncSqlPurpose.Introspection);
                default:
                    return new ModelSyncSqlCommand(@"SELECT table_schema, table_name, column_name, data_type, character_maximum_length, numeric_precision, numeric_scale, is_nullable, CASE WHEN column_default IS NULL THEN 0 ELSE 1 END AS has_default FROM information_schema.columns WHERE table_schema = @Schema;", ModelSyncSqlPurpose.Introspection);
            }
        }

        public ModelSyncSqlCommand BuildReadIndexesPlan()
        {
            switch (_descriptor.CatalogStyle)
            {
                case CatalogQueryStyle.NativeSystemCatalog:
                    return new ModelSyncSqlCommand(@"SELECT s.name, t.name, i.name, i.is_unique, c.name FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id JOIN sys.schemas s ON s.schema_id = t.schema_id JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id WHERE i.is_primary_key = 0 AND i.name IS NOT NULL AND s.name = @Schema ORDER BY s.name, t.name, i.name, ic.key_ordinal;", ModelSyncSqlPurpose.Introspection);
                case CatalogQueryStyle.ObjectRelationalCatalog:
                    return new ModelSyncSqlCommand(@"SELECT n.nspname, t.relname, i.relname, ix.indisunique, a.attname FROM pg_index ix JOIN pg_class t ON t.oid = ix.indrelid JOIN pg_namespace n ON n.oid = t.relnamespace JOIN pg_class i ON i.oid = ix.indexrelid JOIN LATERAL unnest(ix.indkey) WITH ORDINALITY AS keys(attnum, ord) ON true JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = keys.attnum WHERE n.nspname = @Schema AND NOT ix.indisprimary ORDER BY n.nspname, t.relname, i.relname, keys.ord;", ModelSyncSqlPurpose.Introspection);
                case CatalogQueryStyle.OracleDataDictionary:
                    return new ModelSyncSqlCommand(@"SELECT USER, c.TABLE_NAME, c.INDEX_NAME, CASE WHEN i.UNIQUENESS = 'UNIQUE' THEN 1 ELSE 0 END, c.COLUMN_NAME FROM USER_IND_COLUMNS c JOIN USER_INDEXES i ON i.INDEX_NAME = c.INDEX_NAME WHERE c.INDEX_NAME NOT LIKE 'SYS_%' ORDER BY c.TABLE_NAME, c.INDEX_NAME, c.COLUMN_POSITION", ModelSyncSqlPurpose.Introspection);
                default:
                    return new ModelSyncSqlCommand("SELECT TABLE_SCHEMA, TABLE_NAME, INDEX_NAME, NON_UNIQUE, COLUMN_NAME FROM information_schema.statistics WHERE TABLE_SCHEMA = @Schema AND INDEX_NAME <> 'PRIMARY' ORDER BY TABLE_SCHEMA, TABLE_NAME, INDEX_NAME, SEQ_IN_INDEX;", ModelSyncSqlPurpose.Introspection);
            }
        }

        public ModelSyncSqlCommand BuildReadConstraintsPlan()
        {
            switch (_descriptor.CatalogStyle)
            {
                case CatalogQueryStyle.NativeSystemCatalog:
                    return new ModelSyncSqlCommand(@"SELECT s.name, t.name, kc.name, c.name FROM sys.key_constraints kc JOIN sys.tables t ON t.object_id = kc.parent_object_id JOIN sys.schemas s ON s.schema_id = t.schema_id JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id WHERE kc.type = 'UQ' AND s.name = @Schema;", ModelSyncSqlPurpose.Introspection);
                case CatalogQueryStyle.ObjectRelationalCatalog:
                    return new ModelSyncSqlCommand(@"SELECT n.nspname, t.relname, c.conname, c.contype FROM pg_constraint c JOIN pg_class t ON t.oid = c.conrelid JOIN pg_namespace n ON n.oid = t.relnamespace WHERE n.nspname = @Schema;", ModelSyncSqlPurpose.Introspection);
                case CatalogQueryStyle.OracleDataDictionary:
                    return new ModelSyncSqlCommand(@"SELECT USER, TABLE_NAME, CONSTRAINT_NAME, CASE CONSTRAINT_TYPE WHEN 'U' THEN 'UNIQUE' WHEN 'P' THEN 'PRIMARY KEY' WHEN 'R' THEN 'FOREIGN KEY' WHEN 'C' THEN 'CHECK' ELSE CONSTRAINT_TYPE END FROM USER_CONSTRAINTS WHERE CONSTRAINT_TYPE IN ('U','P','R','C')", ModelSyncSqlPurpose.Introspection);
                default:
                    return new ModelSyncSqlCommand("SELECT TABLE_SCHEMA, TABLE_NAME, CONSTRAINT_NAME, CONSTRAINT_TYPE FROM information_schema.table_constraints WHERE TABLE_SCHEMA = @Schema;", ModelSyncSqlPurpose.Introspection);
            }
        }

        public ModelSyncSqlCommand BuildReadForeignKeysPlan()
        {
            if (_descriptor.CatalogStyle != CatalogQueryStyle.NativeSystemCatalog)
                return new ModelSyncSqlCommand(string.Empty, ModelSyncSqlPurpose.Introspection);
            return new ModelSyncSqlCommand(@"SELECT s.name, t.name, fk.name, pc.name, rs.name, rt.name, rc.name FROM sys.foreign_keys fk JOIN sys.tables t ON t.object_id = fk.parent_object_id JOIN sys.schemas s ON s.schema_id = t.schema_id JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id JOIN sys.tables rt ON rt.object_id = fkc.referenced_object_id JOIN sys.schemas rs ON rs.schema_id = rt.schema_id JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id WHERE s.name = @Schema ORDER BY s.name, t.name, fk.name, fkc.constraint_column_id;", ModelSyncSqlPurpose.Introspection);
        }

        public ModelSyncSqlCommand BuildReadConstraintColumnsPlan()
        {
            if (_descriptor.CatalogStyle == CatalogQueryStyle.ObjectRelationalCatalog)
                return new ModelSyncSqlCommand(@"SELECT n.nspname, t.relname, c.conname, c.contype, a.attname, rn.nspname, rt.relname, ra.attname FROM pg_constraint c JOIN pg_class t ON t.oid = c.conrelid JOIN pg_namespace n ON n.oid = t.relnamespace JOIN LATERAL unnest(c.conkey) WITH ORDINALITY AS ck(attnum, ord) ON true JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = ck.attnum LEFT JOIN pg_class rt ON rt.oid = c.confrelid LEFT JOIN pg_namespace rn ON rn.oid = rt.relnamespace LEFT JOIN LATERAL unnest(c.confkey) WITH ORDINALITY AS rf(attnum, ord) ON rf.ord = ck.ord LEFT JOIN pg_attribute ra ON ra.attrelid = rt.oid AND ra.attnum = rf.attnum WHERE n.nspname = @Schema AND c.contype IN ('u', 'f') ORDER BY n.nspname, t.relname, c.conname, ck.ord;", ModelSyncSqlPurpose.Introspection);
            if (_descriptor.CatalogStyle == CatalogQueryStyle.OracleDataDictionary)
                return new ModelSyncSqlCommand(@"SELECT USER, c.TABLE_NAME, c.CONSTRAINT_NAME, CASE c.CONSTRAINT_TYPE WHEN 'U' THEN 'UNIQUE' WHEN 'R' THEN 'FOREIGN KEY' ELSE c.CONSTRAINT_TYPE END, cc.COLUMN_NAME, USER, rc.TABLE_NAME, rcc.COLUMN_NAME FROM USER_CONSTRAINTS c JOIN USER_CONS_COLUMNS cc ON cc.CONSTRAINT_NAME = c.CONSTRAINT_NAME LEFT JOIN USER_CONSTRAINTS rc ON rc.CONSTRAINT_NAME = c.R_CONSTRAINT_NAME LEFT JOIN USER_CONS_COLUMNS rcc ON rcc.CONSTRAINT_NAME = rc.CONSTRAINT_NAME AND rcc.POSITION = cc.POSITION WHERE c.CONSTRAINT_TYPE IN ('U','R') ORDER BY c.TABLE_NAME, c.CONSTRAINT_NAME, cc.POSITION", ModelSyncSqlPurpose.Introspection);
            return new ModelSyncSqlCommand(@"SELECT k.TABLE_SCHEMA, k.TABLE_NAME, k.CONSTRAINT_NAME, tc.CONSTRAINT_TYPE, k.COLUMN_NAME, k.REFERENCED_TABLE_SCHEMA, k.REFERENCED_TABLE_NAME, k.REFERENCED_COLUMN_NAME FROM information_schema.key_column_usage k JOIN information_schema.table_constraints tc ON tc.CONSTRAINT_SCHEMA = k.CONSTRAINT_SCHEMA AND tc.TABLE_NAME = k.TABLE_NAME AND tc.CONSTRAINT_NAME = k.CONSTRAINT_NAME WHERE k.TABLE_SCHEMA = @Schema AND tc.CONSTRAINT_TYPE IN ('UNIQUE', 'FOREIGN KEY') ORDER BY k.TABLE_SCHEMA, k.TABLE_NAME, k.CONSTRAINT_NAME, k.ORDINAL_POSITION;", ModelSyncSqlPurpose.Introspection);
        }

        public ModelSyncSqlCommand BuildReadChecksPlan()
            => _descriptor.CatalogStyle == CatalogQueryStyle.OracleDataDictionary
                ? new ModelSyncSqlCommand("SELECT USER, TABLE_NAME, CONSTRAINT_NAME FROM USER_CONSTRAINTS WHERE CONSTRAINT_TYPE = 'C'", ModelSyncSqlPurpose.Introspection)
                : new ModelSyncSqlCommand(@"SELECT tc.CONSTRAINT_SCHEMA, tc.TABLE_NAME, tc.CONSTRAINT_NAME
FROM information_schema.table_constraints tc
JOIN information_schema.check_constraints cc
  ON cc.CONSTRAINT_SCHEMA = tc.CONSTRAINT_SCHEMA
 AND cc.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
WHERE tc.CONSTRAINT_SCHEMA = @Schema
  AND tc.CONSTRAINT_TYPE = 'CHECK';", ModelSyncSqlPurpose.Introspection);

        public ModelSyncSqlCommand BuildReadFileCatalogTablesPlan()
            => new ModelSyncSqlCommand("SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';", ModelSyncSqlPurpose.Introspection);

        public ModelSyncSqlCommand BuildReadFileCatalogTableInfoPlan(string table)
        {
            ValidateIdentifier(table);
            return new ModelSyncSqlCommand("PRAGMA table_info(" + Quote(table) + ");", ModelSyncSqlPurpose.Introspection);
        }

        public ModelSyncSqlCommand BuildReadFileCatalogIndexListPlan(string table)
        {
            ValidateIdentifier(table);
            return new ModelSyncSqlCommand("PRAGMA index_list(" + Quote(table) + ");", ModelSyncSqlPurpose.Introspection);
        }

        public ModelSyncSqlCommand BuildReadFileCatalogIndexInfoPlan(string index)
        {
            ValidateIdentifier(index);
            return new ModelSyncSqlCommand("PRAGMA index_info(" + Quote(index) + ");", ModelSyncSqlPurpose.Introspection);
        }

        public ModelSyncSqlCommand BuildReadFileCatalogForeignKeysPlan(string table)
        {
            ValidateIdentifier(table);
            return new ModelSyncSqlCommand("PRAGMA foreign_key_list(" + Quote(table) + ");", ModelSyncSqlPurpose.Introspection);
        }

        public ModelSyncSqlCommand BuildAcquireMigrationLockPlan(string resourceName, TimeSpan timeout)
        {
            switch (_descriptor.MigrationLockStyle)
            {
                case MigrationLockStyle.ApplicationRoutine:
                    return new ModelSyncSqlCommand(
                        "DECLARE @Result INT; EXEC @Result = sp_getapplock @Resource = @ResourceName, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = @TimeoutMilliseconds; SELECT @Result;",
                        ModelSyncSqlPurpose.MigrationLock,
                        new[]
                        {
                            new ModelSyncSqlParameter("@ResourceName", resourceName),
                            new ModelSyncSqlParameter("@TimeoutMilliseconds", Convert.ToInt32(Math.Min(int.MaxValue, timeout.TotalMilliseconds)))
                        },
                        supportsTransaction: false);
                case MigrationLockStyle.NamedRoutine:
                    return new ModelSyncSqlCommand(
                        "SELECT GET_LOCK(@ResourceName, @TimeoutSeconds);",
                        ModelSyncSqlPurpose.MigrationLock,
                        new[]
                        {
                            new ModelSyncSqlParameter("@ResourceName", resourceName),
                            new ModelSyncSqlParameter("@TimeoutSeconds", Convert.ToInt32(Math.Ceiling(timeout.TotalSeconds)))
                        },
                        supportsTransaction: false);
                case MigrationLockStyle.AdvisoryRoutine:
                    return new ModelSyncSqlCommand(
                        "SELECT pg_try_advisory_lock(@LockKey);",
                        ModelSyncSqlPurpose.MigrationLock,
                        new[] { new ModelSyncSqlParameter("@LockKey", StableLockKey(resourceName)) },
                        supportsTransaction: false);
                case MigrationLockStyle.FileImmediateTransaction:
                    return new ModelSyncSqlCommand("BEGIN IMMEDIATE;", ModelSyncSqlPurpose.MigrationLock, supportsTransaction: false);
                default:
                    throw new NotSupportedException("ProviderNativeLockUnsupported");
            }
        }

        public ModelSyncSqlCommand BuildReleaseMigrationLockPlan(string resourceName)
        {
            switch (_descriptor.MigrationLockStyle)
            {
                case MigrationLockStyle.ApplicationRoutine:
                    return new ModelSyncSqlCommand(
                        "DECLARE @Result INT; EXEC @Result = sp_releaseapplock @Resource = @ResourceName, @LockOwner = 'Session'; SELECT @Result;",
                        ModelSyncSqlPurpose.MigrationLock,
                        new[] { new ModelSyncSqlParameter("@ResourceName", resourceName) },
                        supportsTransaction: false);
                case MigrationLockStyle.NamedRoutine:
                    return new ModelSyncSqlCommand(
                        "SELECT RELEASE_LOCK(@ResourceName);",
                        ModelSyncSqlPurpose.MigrationLock,
                        new[] { new ModelSyncSqlParameter("@ResourceName", resourceName) },
                        supportsTransaction: false);
                case MigrationLockStyle.AdvisoryRoutine:
                    return new ModelSyncSqlCommand(
                        "SELECT pg_advisory_unlock(@LockKey);",
                        ModelSyncSqlPurpose.MigrationLock,
                        new[] { new ModelSyncSqlParameter("@LockKey", StableLockKey(resourceName)) },
                        supportsTransaction: false);
                case MigrationLockStyle.FileImmediateTransaction:
                    return new ModelSyncSqlCommand("ROLLBACK;", ModelSyncSqlPurpose.MigrationLock, supportsTransaction: false);
                default:
                    throw new NotSupportedException("ProviderNativeLockUnsupported");
            }
        }

        public bool IsSuccessfulLockAcquireResult(object value)
        {
            if (_descriptor.MigrationLockStyle == MigrationLockStyle.FileImmediateTransaction)
                return true;
            if (value == null || value == DBNull.Value)
                return false;
            if (value is bool boolean)
                return boolean;
            var numeric = Convert.ToInt64(value);
            if (_descriptor.MigrationLockStyle == MigrationLockStyle.ApplicationRoutine)
                return numeric >= 0;
            return numeric == 1;
        }

        private static long StableLockKey(string value)
        {
            unchecked
            {
                const ulong offset = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;
                var hash = offset;
                foreach (var c in value ?? string.Empty)
                {
                    hash ^= c;
                    hash *= prime;
                }
                return (long)hash;
            }
        }

        public string BuildAddCheckConstraintSql(ModelTableDefinition table, ModelColumnDefinition column)
        {
            if (!_descriptor.SupportsAddCheckConstraint)
                return string.Empty;
            return "ALTER TABLE " + Qualify(table.Schema, table.Name) + " ADD CONSTRAINT " + Quote("CK_" + table.Name + "_" + column.Name) + " CHECK (" + column.CheckSql + ");";
        }

        public string BuildAddUniqueConstraintSql(ModelTableDefinition table, ModelColumnDefinition column)
            => _descriptor.OmitSchemaInDdl
                ? "CREATE UNIQUE INDEX " + Quote("UQ_" + table.Name + "_" + column.Name) + " ON " + Quote(table.Name) + " (" + Quote(column.Name) + ");"
                : "ALTER TABLE " + Qualify(table.Schema, table.Name) + " ADD CONSTRAINT " + Quote("UQ_" + table.Name + "_" + column.Name) + " UNIQUE (" + Quote(column.Name) + ");";

        public string BuildAddForeignKeySql(ModelTableDefinition table, ModelColumnDefinition column)
        {
            if (!_descriptor.SupportsAddForeignKey)
                return string.Empty;
            var localColumn = string.IsNullOrWhiteSpace(column.ForeignKeyColumn) ? column.Name : column.ForeignKeyColumn;
            return "ALTER TABLE " + Qualify(table.Schema, table.Name) + " ADD CONSTRAINT " + Quote("FK_" + table.Name + "_" + localColumn + "_" + column.ForeignKeyTable) +
                   " FOREIGN KEY (" + Quote(localColumn) + ") REFERENCES " + Qualify(table.Schema, column.ForeignKeyTable) + " (" + Quote(column.ForeignKeyReferenceColumn) + ");";
        }

        public string BuildCreateIndexSql(ModelTableDefinition table, ModelColumnDefinition column)
        {
            var indexName = string.IsNullOrWhiteSpace(column.IndexName) ? "idx_" + table.Name + "_" + column.Name : column.IndexName;
            return "CREATE " + (column.IsUniqueIndex ? "UNIQUE " : string.Empty) + "INDEX " + Quote(indexName) + " ON " + Qualify(table.Schema, table.Name) + " (" + Quote(column.Name) + ");";
        }

        public string BuildColumnDefinition(ModelColumnDefinition column, bool allowInlinePrimaryKey)
        {
            var sql = new StringBuilder();
            sql.Append(Quote(column.Name)).Append(' ').Append(column.StoreType);
            if (_descriptor.GeneratedValuePlacement == GeneratedValuePlacement.AfterStoreType)
                AppendGeneratedValue(sql, column);
            if (column.IsPrimaryKey && allowInlinePrimaryKey)
            {
                sql.Append(" PRIMARY KEY");
                if (_descriptor.GeneratedValuePlacement == GeneratedValuePlacement.AfterInlinePrimaryKey)
                    AppendGeneratedValue(sql, column);
            }
            if (column.IsRequired)
                sql.Append(" NOT NULL");
            if (column.IsUnique)
                sql.Append(" UNIQUE");
            if (!string.IsNullOrWhiteSpace(column.DefaultSql))
                sql.Append(" DEFAULT ").Append(column.DefaultSql);
            if (!string.IsNullOrWhiteSpace(column.CheckSql))
                sql.Append(" CHECK (").Append(column.CheckSql).Append(')');
            return sql.ToString();
        }

        private void AppendGeneratedValue(StringBuilder sql, ModelColumnDefinition column)
        {
            if (column.ValueGeneration == DbValueGenerationKind.Identity && !string.IsNullOrWhiteSpace(_descriptor.IdentityKeyword))
                sql.Append(' ').Append(_descriptor.IdentityKeyword.Replace("{seed}", (column.IdentitySeed ?? 1).ToString()).Replace("{increment}", (column.IdentityIncrement ?? 1).ToString()));
            else if (column.ValueGeneration == DbValueGenerationKind.AutoIncrement && !string.IsNullOrWhiteSpace(_descriptor.AutoIncrementKeyword))
                sql.Append(' ').Append(_descriptor.AutoIncrementKeyword);
            else if (column.ValueGeneration == DbValueGenerationKind.RowIdAlias && !string.IsNullOrWhiteSpace(_descriptor.RowIdKeyword))
                sql.Append(' ').Append(_descriptor.RowIdKeyword);
        }

        private string BuildQualifiedHistoryInfrastructure(string schema)
        {
            var ensure = BuildEnsureSchemaPlan(schema).CommandText;
            var quotedSchema = Quote(schema);
            return ensure +
                   "IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SchemaMigration_Tables' AND schema_id = SCHEMA_ID(@SchemaName)) CREATE TABLE " + quotedSchema + ".[SchemaMigration_Tables]([Id] NVARCHAR(128) NOT NULL PRIMARY KEY,[Name] NVARCHAR(256) NOT NULL,[SqlHash] NVARCHAR(128) NULL,[AppliedAt] DATETIME2 NOT NULL CONSTRAINT DF_ModelSync_Tables_AppliedAt DEFAULT SYSUTCDATETIME(),[UpdateAt] DATETIME2 NULL);" +
                   "IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SchemaMigration_StoredProcedures' AND schema_id = SCHEMA_ID(@SchemaName)) CREATE TABLE " + quotedSchema + ".[SchemaMigration_StoredProcedures]([Id] NVARCHAR(128) NOT NULL PRIMARY KEY,[Name] NVARCHAR(256) NOT NULL,[SqlHash] NVARCHAR(128) NULL,[AppliedAt] DATETIME2 NOT NULL CONSTRAINT DF_ModelSync_StoredProcedures_AppliedAt DEFAULT SYSUTCDATETIME(),[UpdateAt] DATETIME2 NULL);" +
                   "IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SchemaMigration_Triggers' AND schema_id = SCHEMA_ID(@SchemaName)) CREATE TABLE " + quotedSchema + ".[SchemaMigration_Triggers]([Id] NVARCHAR(128) NOT NULL PRIMARY KEY,[Name] NVARCHAR(256) NOT NULL,[SqlHash] NVARCHAR(128) NULL,[AppliedAt] DATETIME2 NOT NULL CONSTRAINT DF_ModelSync_Triggers_AppliedAt DEFAULT SYSUTCDATETIME(),[UpdateAt] DATETIME2 NULL);" +
                   "IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SchemaMigration_Seeds' AND schema_id = SCHEMA_ID(@SchemaName)) CREATE TABLE " + quotedSchema + ".[SchemaMigration_Seeds]([Id] NVARCHAR(128) NOT NULL PRIMARY KEY,[Name] NVARCHAR(256) NOT NULL,[SqlHash] NVARCHAR(128) NULL,[AppliedAt] DATETIME2 NOT NULL CONSTRAINT DF_ModelSync_Seeds_AppliedAt DEFAULT SYSUTCDATETIME(),[UpdateAt] DATETIME2 NULL);" +
                   "IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SchemaMigration_CustomSql' AND schema_id = SCHEMA_ID(@SchemaName)) CREATE TABLE " + quotedSchema + ".[SchemaMigration_CustomSql]([Id] NVARCHAR(128) NOT NULL PRIMARY KEY,[Name] NVARCHAR(256) NOT NULL,[SqlHash] NVARCHAR(128) NULL,[AppliedAt] DATETIME2 NOT NULL CONSTRAINT DF_ModelSync_CustomSql_AppliedAt DEFAULT SYSUTCDATETIME(),[UpdateAt] DATETIME2 NULL);";
        }

        private string BuildHistoryTablesQualified(string schema, string idType, string nameType, string timestampType, string now)
            => string.Concat(new[] { "SchemaMigration_Tables", "SchemaMigration_StoredProcedures", "SchemaMigration_Triggers", "SchemaMigration_Seeds", "SchemaMigration_CustomSql" }
                .Select(t => "CREATE TABLE IF NOT EXISTS " + Qualify(schema, t) + "(" + Quote("Id") + " " + idType + " PRIMARY KEY, " + Quote("Name") + " " + nameType + " NOT NULL, " + Quote("SqlHash") + " VARCHAR(128) NULL, " + Quote("AppliedAt") + " " + timestampType + " NOT NULL DEFAULT " + now + ", " + Quote("UpdateAt") + " " + timestampType + " NULL);"));

        private string BuildHistoryTablesUnqualified(string open, string close, string timestampType, string now)
            => string.Concat(new[] { "SchemaMigration_Tables", "SchemaMigration_StoredProcedures", "SchemaMigration_Triggers", "SchemaMigration_Seeds", "SchemaMigration_CustomSql" }
                .Select(t => "CREATE TABLE IF NOT EXISTS " + open + t + close + "(" + open + "Id" + close + " VARCHAR(128) NOT NULL PRIMARY KEY, " + open + "Name" + close + " VARCHAR(256) NOT NULL, " + open + "SqlHash" + close + " VARCHAR(128) NULL, " + open + "AppliedAt" + close + " " + timestampType + " NOT NULL DEFAULT " + now + ", " + open + "UpdateAt" + close + " " + timestampType + " NULL);"));

        private string BuildOracleHistoryInfrastructure()
            => string.Concat(new[] { "SchemaMigration_Tables", "SchemaMigration_StoredProcedures", "SchemaMigration_Triggers", "SchemaMigration_Seeds", "SchemaMigration_CustomSql" }
                .Select(t => "DECLARE n NUMBER; BEGIN SELECT COUNT(*) INTO n FROM USER_TABLES WHERE TABLE_NAME = '" + t.ToUpperInvariant() + "'; IF n = 0 THEN EXECUTE IMMEDIATE 'CREATE TABLE " + Quote(t) + "(" + Quote("Id") + " VARCHAR2(128) NOT NULL PRIMARY KEY, " + Quote("Name") + " VARCHAR2(256) NOT NULL, " + Quote("SqlHash") + " VARCHAR2(128) NULL, " + Quote("AppliedAt") + " TIMESTAMP DEFAULT SYSTIMESTAMP NOT NULL, " + Quote("UpdateAt") + " TIMESTAMP NULL)'; END IF; END;"));

        private static void ValidateIdentifier(string identifier)
            => SqlIdentifierValidator.Validate(identifier, nameof(identifier));
    }
}
