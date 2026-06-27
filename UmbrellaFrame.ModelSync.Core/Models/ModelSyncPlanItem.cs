namespace UmbrellaFrame.ModelSync.Core
{
    public sealed class ModelSyncPlanItem
    {
        public System.Func<ModelSyncPlanItem, System.Threading.CancellationToken, System.Threading.Tasks.Task>? ApplyOperationAsync { get; set; }
        public ModelSyncChangeType ChangeType { get; set; }
        public ModelSyncOperationRisk Risk { get; set; }
        public string Schema { get; set; } = string.Empty;
        public string Table { get; set; } = string.Empty;
        public string Column { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Sql { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public bool CanApplyAutomatically { get; set; }

        public bool HasSql => !string.IsNullOrWhiteSpace(Sql);
        public bool HasApplyOperation => ApplyOperationAsync != null;
    }
}
