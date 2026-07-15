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
        private readonly IModelSyncOperationRiskEvaluator _riskEvaluator;
        private readonly IModelSyncTablePolicyResolver? _policyResolver;

        public ModelSyncPlanBuilder(
            Func<string, string> quote,
            Func<string, string, string> qualify,
            Func<ModelTableDefinition, string> createTableSql,
            Func<ModelTableDefinition, ModelColumnDefinition, string> addColumnSql,
            Func<ModelTableDefinition, ModelColumnDefinition, string> addDefaultSql,
            Func<ModelTableDefinition, ModelColumnDefinition, string> addCheckSql,
            Func<ModelTableDefinition, ModelColumnDefinition, string> addUniqueSql,
            Func<ModelTableDefinition, ModelColumnDefinition, string> addForeignKeySql,
            Func<ModelTableDefinition, ModelColumnDefinition, string> addIndexSql,
            IModelSyncOperationRiskEvaluator? riskEvaluator = null,
            IModelSyncTablePolicyResolver? policyResolver = null)
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
            _riskEvaluator = riskEvaluator ?? new DefaultModelSyncOperationRiskEvaluator();
            _policyResolver = policyResolver;
        }

        public IReadOnlyList<ModelSyncPlanItem> Build(
            IEnumerable<ModelTableDefinition> modelTables,
            IDictionary<string, DatabaseTableDefinition> databaseTables,
            ModelSyncOptions options)
        {
            var createTablePlans = new List<ModelSyncPlanItem>();
            var addColumnPlans = new List<ModelSyncPlanItem>();
            var defaultPlans = new List<ModelSyncPlanItem>();
            var checkPlans = new List<ModelSyncPlanItem>();
            var uniquePlans = new List<ModelSyncPlanItem>();
            var indexPlans = new List<ModelSyncPlanItem>();
            var foreignKeyPlans = new List<ModelSyncPlanItem>();
            var blockedPlans = new List<ModelSyncPlanItem>();
            var resolver = _policyResolver ?? new ModelSyncTablePolicyResolver(options);
            var allModelTables = modelTables.ToList();
            var modelTableList = allModelTables.Where(t => resolver.Resolve(t) != ModelSyncTableMode.Ignore).ToList();

            foreach (var modelTable in modelTableList)
            {
                var tableMode = resolver.Resolve(modelTable);
                var tableKey = Key(modelTable.Schema, modelTable.Name);
                if (!databaseTables.TryGetValue(tableKey, out var dbTable))
                {
                    createTablePlans.Add(ApplyPolicy(Safe(ModelSyncPlanPhase.CreateTables, ModelSyncChangeType.CreateTable, modelTable, null, _createTableSql(modelTable), "Table is missing in the live database.", options.CreateMissingTables), tableMode));
                    foreach (var column in modelTable.Columns.Where(c => c.IsIndexed))
                    {
                        indexPlans.Add(ApplyPolicy(Safe(ModelSyncPlanPhase.AddIndexes, ModelSyncChangeType.AddIndex, modelTable, column, _addIndexSql(modelTable, column),
                            "Index will be created after the missing table is created.", options.CreateMissingTables && options.AddMissingIndexes), tableMode));
                    }

                    foreach (var column in modelTable.Columns.Where(c => !string.IsNullOrWhiteSpace(c.ForeignKeyTable)))
                    {
                        var fkPlan = Safe(ModelSyncPlanPhase.AddForeignKeys, ModelSyncChangeType.AddForeignKey, modelTable, column, _addForeignKeySql(modelTable, column),
                            "Foreign key will be created after missing tables are created.", options.CreateMissingTables && options.AddMissingConstraints);
                        foreignKeyPlans.Add(ApplyDependencyPolicy(ApplyPolicy(fkPlan, tableMode), column, modelTable, modelTableList, databaseTables, resolver));
                    }
                    continue;
                }

                foreach (var modelColumn in modelTable.Columns)
                {
                    if (!dbTable.Columns.TryGetValue(modelColumn.Name, out var dbColumn))
                    {
                        var evaluation = _riskEvaluator.EvaluateMissingColumn(modelTable, modelColumn);
                        if (evaluation.CanApplyAutomatically)
                            addColumnPlans.Add(ApplyPolicy(Safe(ModelSyncPlanPhase.AddColumns, ModelSyncChangeType.AddColumn, modelTable, modelColumn, _addColumnSql(modelTable, modelColumn), evaluation.Reason, options.AddMissingColumns), tableMode));
                        else
                            blockedPlans.Add(ApplyPolicy(Blocked(ModelSyncChangeType.AddColumn, evaluation.Risk, modelTable, modelColumn, evaluation.Reason), tableMode));
                        continue;
                    }

                    if (!SameStoreType(modelColumn.StoreType, dbColumn.StoreType))
                    {
                        blockedPlans.Add(ApplyPolicy(Blocked(ModelSyncChangeType.AlterColumnType, ModelSyncOperationRisk.Risky, modelTable, modelColumn,
                            $"Column type differs. Model: {modelColumn.StoreType}; database: {dbColumn.StoreType}."), tableMode));
                    }

                    if (modelColumn.IsRequired && dbColumn.IsNullable)
                    {
                        blockedPlans.Add(ApplyPolicy(Blocked(ModelSyncChangeType.AlterNullability, ModelSyncOperationRisk.Risky, modelTable, modelColumn,
                            "Changing nullable column to NOT NULL can fail or cause data cleanup requirements."), tableMode));
                    }

                    if (!string.IsNullOrWhiteSpace(modelColumn.DefaultSql) && !dbColumn.HasDefault)
                    {
                        defaultPlans.Add(ApplyPolicy(Safe(ModelSyncPlanPhase.AddDefaultConstraints, ModelSyncChangeType.AddDefaultConstraint, modelTable, modelColumn, _addDefaultSql(modelTable, modelColumn), "Default constraint is missing.", options.AddMissingConstraints), tableMode));
                    }

                    if (!string.IsNullOrWhiteSpace(modelColumn.CheckSql) && !dbColumn.HasCheck)
                    {
                        checkPlans.Add(ApplyPolicy(Safe(ModelSyncPlanPhase.AddCheckConstraints, ModelSyncChangeType.AddCheckConstraint, modelTable, modelColumn, _addCheckSql(modelTable, modelColumn), "Check constraint is missing.", options.AddMissingConstraints), tableMode));
                    }
                }

                foreach (var dbColumn in dbTable.Columns.Values)
                {
                    if (!modelTable.Columns.Any(c => string.Equals(c.Name, dbColumn.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        blockedPlans.Add(ApplyPolicy(Blocked(ModelSyncChangeType.DropColumn, ModelSyncOperationRisk.Destructive, modelTable, new ModelColumnDefinition { Name = dbColumn.Name },
                            "Database column does not exist on the model. Dropping columns is destructive and is never automatic."), tableMode));
                    }
                }

                foreach (var column in modelTable.Columns.Where(c => c.IsIndexed))
                {
                    var indexName = string.IsNullOrWhiteSpace(column.IndexName)
                        ? $"idx_{modelTable.Name}_{column.Name}"
                        : column.IndexName;

                    if (!HasIndex(dbTable, column.IsUniqueIndex, column.Name, indexName))
                    {
                        indexPlans.Add(ApplyPolicy(Safe(ModelSyncPlanPhase.AddIndexes, ModelSyncChangeType.AddIndex, modelTable, column, _addIndexSql(modelTable, column), "Index is missing.", options.AddMissingIndexes), tableMode));
                    }
                }

                foreach (var column in modelTable.Columns.Where(c => c.IsUnique))
                {
                    var name = $"UQ_{modelTable.Name}_{column.Name}";
                    if (!HasUniqueConstraint(dbTable, column.Name, name))
                    {
                        uniquePlans.Add(ApplyPolicy(Safe(ModelSyncPlanPhase.AddUniqueConstraints, ModelSyncChangeType.AddUniqueConstraint, modelTable, column, _addUniqueSql(modelTable, column), "Unique constraint is missing.", options.AddMissingConstraints), tableMode));
                    }
                }

                foreach (var column in modelTable.Columns.Where(c => !string.IsNullOrWhiteSpace(c.ForeignKeyTable)))
                {
                    var name = $"FK_{modelTable.Name}_{ForeignKeyColumn(column)}_{column.ForeignKeyTable}";
                    if (!HasForeignKey(dbTable, modelTable.Schema, column, name))
                    {
                        var fkPlan = Safe(ModelSyncPlanPhase.AddForeignKeys, ModelSyncChangeType.AddForeignKey, modelTable, column, _addForeignKeySql(modelTable, column), "Foreign key is missing.", options.AddMissingConstraints);
                        foreignKeyPlans.Add(ApplyDependencyPolicy(ApplyPolicy(fkPlan, tableMode), column, modelTable, modelTableList, databaseTables, resolver));
                    }
                }
            }

            if (options.ReportUnmappedTables)
            {
                foreach (var dbTable in databaseTables.Values)
                {
                    if (IsModelSyncHistoryTable(dbTable.Name))
                        continue;
                    if (resolver.Resolve(dbTable.Schema, dbTable.Name) == ModelSyncTableMode.Ignore)
                        continue;

                    var existsInModel = modelTableList.Any(t =>
                        string.Equals(t.Schema, dbTable.Schema, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(t.Name, dbTable.Name, StringComparison.OrdinalIgnoreCase));

                    if (!existsInModel)
                    {
                        blockedPlans.Add(new ModelSyncPlanItem
                        {
                            Phase = ModelSyncPlanPhase.BlockedReview,
                            TableMode = resolver.Resolve(dbTable.Schema, dbTable.Name),
                            Disposition = ModelSyncOperationDisposition.Blocked,
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
            }

            return createTablePlans
                .Concat(addColumnPlans)
                .Concat(defaultPlans)
                .Concat(checkPlans)
                .Concat(uniquePlans)
                .Concat(indexPlans)
                .Concat(foreignKeyPlans)
                .Concat(blockedPlans)
                .ToList();
        }

        public static string Key(string schema, string table)
            => $"{schema ?? string.Empty}.{table ?? string.Empty}";

        private static bool SameStoreType(string modelType, string dbType)
        {
            var normalizedModel = Normalize(modelType);
            var normalizedDatabase = Normalize(dbType);
            if (normalizedModel == normalizedDatabase)
                return true;

            normalizedModel = NormalizeDefaultTimestamp(normalizedModel);
            normalizedDatabase = NormalizeDefaultTimestamp(normalizedDatabase);
            return normalizedModel == normalizedDatabase;
        }

        private static string Normalize(string value)
            => new string((value ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

        private static string NormalizeDefaultTimestamp(string value)
        {
            if (value == "TIMESTAMPWITHOUTTIMEZONE" || value == "TIMESTAMP(6)")
                return "TIMESTAMP";
            return value;
        }

        private static bool IsModelSyncHistoryTable(string tableName)
            => !string.IsNullOrWhiteSpace(tableName) &&
               tableName.StartsWith("SchemaMigration_", StringComparison.OrdinalIgnoreCase);

        private static string ForeignKeyColumn(ModelColumnDefinition column)
            => string.IsNullOrWhiteSpace(column.ForeignKeyColumn) ? column.Name : column.ForeignKeyColumn;

        private static bool HasIndex(DatabaseTableDefinition table, bool unique, string columnName, string indexName)
            => table.Indexes.Contains(indexName) ||
               table.SemanticIndexes.Any(i => i.Matches(unique, new[] { columnName }));

        private static bool HasUniqueConstraint(DatabaseTableDefinition table, string columnName, string constraintName)
            => table.UniqueConstraints.Contains(constraintName) ||
               table.SemanticIndexes.Any(i => i.Matches(true, new[] { columnName }));

        private static bool HasForeignKey(DatabaseTableDefinition table, string schema, ModelColumnDefinition column, string constraintName)
            => table.ForeignKeys.Contains(constraintName) ||
               table.SemanticForeignKeys.Any(f => f.Matches(
                   new[] { ForeignKeyColumn(column) },
                   schema,
                   column.ForeignKeyTable,
                   new[] { column.ForeignKeyReferenceColumn }));

        private static ModelSyncPlanItem ApplyPolicy(ModelSyncPlanItem item, ModelSyncTableMode tableMode)
        {
            item.TableMode = tableMode;

            if (tableMode == ModelSyncTableMode.Ignore)
            {
                item.Disposition = ModelSyncOperationDisposition.Skipped;
                item.CanApplyAutomatically = false;
                if (string.IsNullOrWhiteSpace(item.Reason))
                    item.Reason = "Table is ignored by table policy.";
                return item;
            }

            if (item.Risk == ModelSyncOperationRisk.SkippedByOption)
            {
                item.Disposition = ModelSyncOperationDisposition.Skipped;
                item.CanApplyAutomatically = false;
                return item;
            }

            if (tableMode == ModelSyncTableMode.ManualOnly)
            {
                item.Disposition = ModelSyncOperationDisposition.Manual;
                item.CanApplyAutomatically = false;
                return item;
            }

            if (item.Risk == ModelSyncOperationRisk.Safe && item.CanApplyAutomatically)
            {
                if (item.HasSql || item.HasApplyOperation)
                {
                    item.Disposition = ModelSyncOperationDisposition.Automatic;
                    return item;
                }

                item.Risk = ModelSyncOperationRisk.Unsupported;
                item.Disposition = ModelSyncOperationDisposition.Blocked;
                item.CanApplyAutomatically = false;
                item.Reason = "Provider cannot generate executable SQL for this operation. Use a reviewed manual migration.";
                return item;
            }

            item.Disposition = ModelSyncOperationDisposition.Blocked;
            item.CanApplyAutomatically = false;
            return item;
        }

        private static ModelSyncPlanItem ApplyDependencyPolicy(
            ModelSyncPlanItem item,
            ModelColumnDefinition column,
            ModelTableDefinition currentTable,
            IReadOnlyList<ModelTableDefinition> modelTables,
            IDictionary<string, DatabaseTableDefinition> databaseTables,
            IModelSyncTablePolicyResolver resolver)
        {
            if (item.Disposition != ModelSyncOperationDisposition.Automatic &&
                !(item.ChangeType == ModelSyncChangeType.AddForeignKey && item.Risk == ModelSyncOperationRisk.Unsupported))
                return item;

            var referencedSchema = currentTable.Schema;
            var referencedTable = column.ForeignKeyTable;
            if (string.IsNullOrWhiteSpace(referencedTable))
                return item;

            if (databaseTables.ContainsKey(Key(referencedSchema, referencedTable)))
                return item;

            var referencedModel = modelTables.FirstOrDefault(t =>
                string.Equals(t.Schema, referencedSchema, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(t.Name, referencedTable, StringComparison.OrdinalIgnoreCase));

            if (referencedModel == null)
            {
                BlockMissingDependency(item, $"Required dependency '{FormatTableName(referencedSchema, referencedTable)}' does not exist.");
                return item;
            }

            var referencedMode = resolver.Resolve(referencedModel);
            if (referencedMode == ModelSyncTableMode.ManualOnly || referencedMode == ModelSyncTableMode.Ignore)
            {
                BlockMissingDependency(item, $"Required manual dependency '{FormatTableName(referencedSchema, referencedTable)}' does not exist.");
                return item;
            }

            return item;
        }

        private static void BlockMissingDependency(ModelSyncPlanItem item, string reason)
        {
            item.Phase = ModelSyncPlanPhase.BlockedReview;
            item.Risk = ModelSyncOperationRisk.Risky;
            item.Disposition = ModelSyncOperationDisposition.Blocked;
            item.CanApplyAutomatically = false;
            item.Reason = reason;
        }

        private static string FormatTableName(string schema, string table)
            => string.IsNullOrWhiteSpace(schema) ? table : schema + "." + table;

        private static ModelSyncPlanItem Safe(ModelSyncPlanPhase phase, ModelSyncChangeType changeType, ModelTableDefinition table, ModelColumnDefinition? column, string sql, string reason, bool enabled)
            => new ModelSyncPlanItem
            {
                Phase = enabled ? phase : ModelSyncPlanPhase.BlockedReview,
                ChangeType = changeType,
                Risk = enabled ? ModelSyncOperationRisk.Safe : ModelSyncOperationRisk.SkippedByOption,
                Schema = table.Schema,
                Table = table.Name,
                Column = column?.Name ?? string.Empty,
                Name = column?.Name ?? table.Name,
                Sql = enabled ? sql : string.Empty,
                Reason = enabled ? reason : "This safe operation is disabled by options.",
                CanApplyAutomatically = enabled
            };

        private static ModelSyncPlanItem Blocked(ModelSyncChangeType changeType, ModelSyncOperationRisk risk, ModelTableDefinition table, ModelColumnDefinition? column, string reason)
            => new ModelSyncPlanItem
            {
                Phase = ModelSyncPlanPhase.BlockedReview,
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
