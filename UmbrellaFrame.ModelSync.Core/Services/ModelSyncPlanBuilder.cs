using System;
using System.Collections.Generic;
using System.Linq;

namespace UmbrellaFrame.ModelSync.Core.Services
{
    public sealed class ModelSyncPlanBuilder
    {
        private readonly Func<string, string> _quote;
        private readonly Func<string, string, string> _qualify;
        private readonly Func<ModelTableDefinition, string> _createTableSql;
        private readonly Func<ModelTableDefinition, ModelColumnDefinition, string> _addColumnSql;
        private readonly Func<ModelTableDefinition, ModelColumnDefinition, string> _addDefaultSql;
        private readonly Func<ModelTableDefinition, ModelColumnDefinition, string> _addCheckSql;
        private readonly Func<ModelTableDefinition, ModelColumnDefinition, string> _addUniqueSql;
        private readonly Func<ModelTableDefinition, ModelColumnDefinition, string> _addForeignKeySql;
        private readonly Func<ModelTableDefinition, ModelColumnDefinition, string> _addIndexSql;

        public ModelSyncPlanBuilder(
            Func<string, string> quote,
            Func<string, string, string> qualify,
            Func<ModelTableDefinition, string> createTableSql,
            Func<ModelTableDefinition, ModelColumnDefinition, string> addColumnSql,
            Func<ModelTableDefinition, ModelColumnDefinition, string> addDefaultSql,
            Func<ModelTableDefinition, ModelColumnDefinition, string> addCheckSql,
            Func<ModelTableDefinition, ModelColumnDefinition, string> addUniqueSql,
            Func<ModelTableDefinition, ModelColumnDefinition, string> addForeignKeySql,
            Func<ModelTableDefinition, ModelColumnDefinition, string> addIndexSql)
        {
            _quote = quote;
            _qualify = qualify;
            _createTableSql = createTableSql;
            _addColumnSql = addColumnSql;
            _addDefaultSql = addDefaultSql;
            _addCheckSql = addCheckSql;
            _addUniqueSql = addUniqueSql;
            _addForeignKeySql = addForeignKeySql;
            _addIndexSql = addIndexSql;
        }

        public IReadOnlyList<ModelSyncPlanItem> Build(
            IEnumerable<ModelTableDefinition> modelTables,
            IDictionary<string, DatabaseTableDefinition> databaseTables,
            ModelSyncOptions options)
        {
            var plans = new List<ModelSyncPlanItem>();
            var postCreatePlans = new List<ModelSyncPlanItem>();
            var modelTableList = modelTables.ToList();

            foreach (var modelTable in modelTableList)
            {
                var tableKey = Key(modelTable.Schema, modelTable.Name);
                if (!databaseTables.TryGetValue(tableKey, out var dbTable))
                {
                    plans.Add(Safe(ModelSyncChangeType.CreateTable, modelTable, null, _createTableSql(modelTable), "Table is missing in the live database.", options.CreateMissingTables));
                    foreach (var column in modelTable.Columns.Where(c => c.IsIndexed))
                    {
                        postCreatePlans.Add(Safe(ModelSyncChangeType.AddIndex, modelTable, column, _addIndexSql(modelTable, column),
                            "Index will be created after the missing table is created.", options.CreateMissingTables && options.AddMissingIndexes));
                    }

                    foreach (var column in modelTable.Columns.Where(c => !string.IsNullOrWhiteSpace(c.ForeignKeyTable)))
                    {
                        postCreatePlans.Add(Safe(ModelSyncChangeType.AddForeignKey, modelTable, column, _addForeignKeySql(modelTable, column),
                            "Foreign key will be created after missing tables are created.", options.CreateMissingTables && options.AddMissingConstraints));
                    }
                    continue;
                }

                foreach (var modelColumn in modelTable.Columns)
                {
                    if (!dbTable.Columns.TryGetValue(modelColumn.Name, out var dbColumn))
                    {
                        if (modelColumn.IsRequired && string.IsNullOrWhiteSpace(modelColumn.DefaultSql))
                        {
                            plans.Add(Blocked(ModelSyncChangeType.AddColumn, ModelSyncOperationRisk.Risky, modelTable, modelColumn,
                                "Adding a NOT NULL column without a default can fail on existing rows. Add a default or handle it with a reviewed migration script."));
                        }
                        else
                        {
                            plans.Add(Safe(ModelSyncChangeType.AddColumn, modelTable, modelColumn, _addColumnSql(modelTable, modelColumn), "Column is missing in the live database.", options.AddMissingColumns));
                        }
                        continue;
                    }

                    if (!SameStoreType(modelColumn.StoreType, dbColumn.StoreType))
                    {
                        plans.Add(Blocked(ModelSyncChangeType.AlterColumnType, ModelSyncOperationRisk.Risky, modelTable, modelColumn,
                            $"Column type differs. Model: {modelColumn.StoreType}; database: {dbColumn.StoreType}."));
                    }

                    if (modelColumn.IsRequired && dbColumn.IsNullable)
                    {
                        plans.Add(Blocked(ModelSyncChangeType.AlterNullability, ModelSyncOperationRisk.Risky, modelTable, modelColumn,
                            "Changing nullable column to NOT NULL can fail or cause data cleanup requirements."));
                    }

                    if (!string.IsNullOrWhiteSpace(modelColumn.DefaultSql) && !dbColumn.HasDefault)
                    {
                        plans.Add(Safe(ModelSyncChangeType.AddDefaultConstraint, modelTable, modelColumn, _addDefaultSql(modelTable, modelColumn), "Default constraint is missing.", options.AddMissingConstraints));
                    }

                    if (!string.IsNullOrWhiteSpace(modelColumn.CheckSql) && !dbColumn.HasCheck)
                    {
                        plans.Add(Safe(ModelSyncChangeType.AddCheckConstraint, modelTable, modelColumn, _addCheckSql(modelTable, modelColumn), "Check constraint is missing.", options.AddMissingConstraints));
                    }
                }

                foreach (var dbColumn in dbTable.Columns.Values)
                {
                    if (!modelTable.Columns.Any(c => string.Equals(c.Name, dbColumn.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        plans.Add(Blocked(ModelSyncChangeType.DropColumn, ModelSyncOperationRisk.Destructive, modelTable, new ModelColumnDefinition { Name = dbColumn.Name },
                            "Database column does not exist on the model. Dropping columns is destructive and is never automatic."));
                    }
                }

                foreach (var column in modelTable.Columns.Where(c => c.IsIndexed))
                {
                    var indexName = string.IsNullOrWhiteSpace(column.IndexName)
                        ? $"idx_{modelTable.Name}_{column.Name}"
                        : column.IndexName;

                    if (!dbTable.Indexes.Contains(indexName))
                    {
                        plans.Add(Safe(ModelSyncChangeType.AddIndex, modelTable, column, _addIndexSql(modelTable, column), "Index is missing.", options.AddMissingIndexes));
                    }
                }

                foreach (var column in modelTable.Columns.Where(c => c.IsUnique))
                {
                    var name = $"UQ_{modelTable.Name}_{column.Name}";
                    if (!dbTable.UniqueConstraints.Contains(name))
                    {
                        plans.Add(Safe(ModelSyncChangeType.AddUniqueConstraint, modelTable, column, _addUniqueSql(modelTable, column), "Unique constraint is missing.", options.AddMissingConstraints));
                    }
                }

                foreach (var column in modelTable.Columns.Where(c => !string.IsNullOrWhiteSpace(c.ForeignKeyTable)))
                {
                    var name = $"FK_{modelTable.Name}_{ForeignKeyColumn(column)}_{column.ForeignKeyTable}";
                    if (!dbTable.ForeignKeys.Contains(name))
                    {
                        plans.Add(Safe(ModelSyncChangeType.AddForeignKey, modelTable, column, _addForeignKeySql(modelTable, column), "Foreign key is missing.", options.AddMissingConstraints));
                    }
                }
            }

            plans.AddRange(postCreatePlans);

            if (!options.ReportUnmappedTables)
                return plans;

            foreach (var dbTable in databaseTables.Values)
            {
                if (IsModelSyncHistoryTable(dbTable.Name))
                    continue;

                var existsInModel = modelTableList.Any(t =>
                    string.Equals(t.Schema, dbTable.Schema, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(t.Name, dbTable.Name, StringComparison.OrdinalIgnoreCase));

                if (!existsInModel)
                {
                    plans.Add(new ModelSyncPlanItem
                    {
                        ChangeType = ModelSyncChangeType.DropTable,
                        Risk = ModelSyncOperationRisk.Destructive,
                        Schema = dbTable.Schema,
                        Table = dbTable.Name,
                        Name = dbTable.Name,
                        Reason = "Database table does not exist in the model set. Dropping tables is destructive and is never automatic.",
                        CanApplyAutomatically = false
                    });
                }
            }

            return plans;
        }

        public static string Key(string schema, string table)
            => $"{schema ?? string.Empty}.{table ?? string.Empty}";

        private static bool SameStoreType(string modelType, string dbType)
            => Normalize(modelType) == Normalize(dbType);

        private static string Normalize(string value)
            => new string((value ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

        private static bool IsModelSyncHistoryTable(string tableName)
            => !string.IsNullOrWhiteSpace(tableName) &&
               tableName.StartsWith("SchemaMigration_", StringComparison.OrdinalIgnoreCase);

        private static string ForeignKeyColumn(ModelColumnDefinition column)
            => string.IsNullOrWhiteSpace(column.ForeignKeyColumn) ? column.Name : column.ForeignKeyColumn;

        private static ModelSyncPlanItem Safe(ModelSyncChangeType changeType, ModelTableDefinition table, ModelColumnDefinition column, string sql, string reason, bool enabled)
            => new ModelSyncPlanItem
            {
                ChangeType = changeType,
                Risk = enabled ? ModelSyncOperationRisk.Safe : ModelSyncOperationRisk.Unsupported,
                Schema = table.Schema,
                Table = table.Name,
                Column = column?.Name ?? string.Empty,
                Name = column?.Name ?? table.Name,
                Sql = enabled ? sql : string.Empty,
                Reason = enabled ? reason : "This safe operation is disabled by options.",
                CanApplyAutomatically = enabled
            };

        private static ModelSyncPlanItem Blocked(ModelSyncChangeType changeType, ModelSyncOperationRisk risk, ModelTableDefinition table, ModelColumnDefinition column, string reason)
            => new ModelSyncPlanItem
            {
                ChangeType = changeType,
                Risk = risk,
                Schema = table.Schema,
                Table = table.Name,
                Column = column?.Name ?? string.Empty,
                Name = column?.Name ?? table.Name,
                Reason = reason,
                CanApplyAutomatically = false
            };
    }
}
