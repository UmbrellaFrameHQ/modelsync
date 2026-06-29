namespace UmbrellaFrame.ModelSync.Core
{
    public class ModelSyncOptions
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string HistorySchema { get; set; } = "sec";
        public string DefaultSchema { get; set; } = "dbo";
        public bool AllowDestructiveChanges { get; set; }
        public bool ApplyStoredProceduresOnEveryRun { get; set; }
        public bool ApplyTriggersOnEveryRun { get; set; }
        public bool ApplySeedsWithHashTracking { get; set; } = true;
        public bool ApplyCustomSqlWithHashTracking { get; set; } = true;
        public bool CreateMissingTables { get; set; } = true;
        public bool AddMissingColumns { get; set; } = true;
        public bool AddMissingIndexes { get; set; } = true;
        public bool AddMissingConstraints { get; set; } = true;
        public bool ReportUnmappedTables { get; set; }
        public ModelSyncTableMode DefaultTableMode { get; set; } = ModelSyncTableMode.Inherit;
        public ModelSyncTablePolicyCollection TablePolicies { get; } = new ModelSyncTablePolicyCollection();
    }
}
