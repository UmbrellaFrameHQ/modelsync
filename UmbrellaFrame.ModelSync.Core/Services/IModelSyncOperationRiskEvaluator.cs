namespace UmbrellaFrame.ModelSync.Core.Services
{
    /// <summary>
    /// Evaluates whether a model-diff operation can be applied automatically for a provider.
    /// </summary>
    public interface IModelSyncOperationRiskEvaluator
    {
        ModelSyncRiskEvaluation EvaluateMissingColumn(ModelTableDefinition table, ModelColumnDefinition column);
    }
}
