namespace UmbrellaFrame.ModelSync.Core.Services
{
    /// <summary>
    /// Default conservative risk evaluator used by provider model synchronizers.
    /// </summary>
    public class DefaultModelSyncOperationRiskEvaluator : IModelSyncOperationRiskEvaluator
    {
        public virtual ModelSyncRiskEvaluation EvaluateMissingColumn(ModelTableDefinition table, ModelColumnDefinition column)
        {
            if (column.IsPrimaryKey)
            {
                return ModelSyncRiskEvaluation.Blocked(
                    ModelSyncOperationRisk.Risky,
                    "Adding a primary-key column to an existing table is not automatically safe. Use a reviewed migration script.");
            }

            if (column.ValueGeneration != DbValueGenerationKind.None)
            {
                return ModelSyncRiskEvaluation.Blocked(
                    ModelSyncOperationRisk.Risky,
                    "Adding an identity, auto-increment, serial, or rowid column to an existing table is not automatically safe. Use a reviewed migration script.");
            }

            if (column.IsUnique)
            {
                return ModelSyncRiskEvaluation.Blocked(
                    ModelSyncOperationRisk.Risky,
                    "Adding a unique column to an existing table can fail on existing duplicate data. Add the plain column first or use a reviewed migration script.");
            }

            if (column.IsRequired && string.IsNullOrWhiteSpace(column.DefaultSql))
            {
                return ModelSyncRiskEvaluation.Blocked(
                    ModelSyncOperationRisk.Risky,
                    "Adding a NOT NULL column without a default can fail on existing rows. Add a default or handle it with a reviewed migration script.");
            }

            return ModelSyncRiskEvaluation.Safe("Column is missing in the live database.");
        }
    }
}
