namespace UmbrellaFrame.ModelSync.Core.Services
{
    /// <summary>
    /// Describes the risk classification for one planned model synchronization operation.
    /// </summary>
    public sealed class ModelSyncRiskEvaluation
    {
        public static ModelSyncRiskEvaluation Safe(string reason)
            => new ModelSyncRiskEvaluation(ModelSyncOperationRisk.Safe, true, reason);

        public static ModelSyncRiskEvaluation Blocked(ModelSyncOperationRisk risk, string reason)
            => new ModelSyncRiskEvaluation(risk, false, reason);

        private ModelSyncRiskEvaluation(ModelSyncOperationRisk risk, bool canApplyAutomatically, string reason)
        {
            Risk = risk;
            CanApplyAutomatically = canApplyAutomatically;
            Reason = reason ?? string.Empty;
        }

        public ModelSyncOperationRisk Risk { get; }
        public bool CanApplyAutomatically { get; }
        public string Reason { get; }
    }
}
