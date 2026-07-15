namespace UmbrellaFrame.ModelSync.Core
{
    /// <summary>Dry-run result for one migration script.</summary>
    public sealed class MigrationSyncPlan
    {
        public MigrationScriptDefinition Definition { get; set; } = new MigrationScriptDefinition();
        public MigrationChangeType ChangeType { get; set; }
        public string CurrentHash { get; set; } = string.Empty;
        public string TargetHash { get; set; } = string.Empty;
        public string SqlToApply { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public MigrationScriptExecutionMode ExecutionMode { get; set; } = MigrationScriptExecutionMode.HashTracked;
        public string DecisionReason { get; set; } = string.Empty;
        public bool LegacyHashAdoptionRequired { get; set; }
        public bool LegacyHashAdopted { get; set; }
        public bool HistoryRowExists { get; set; }

        public bool HasChanges => ChangeType != MigrationChangeType.None;
    }
}
