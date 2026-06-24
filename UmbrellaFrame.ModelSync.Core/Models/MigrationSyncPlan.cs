namespace UmbrellaFrame.ModelSync.Core
{
    /// <summary>Dry-run result for one migration script.</summary>
    public sealed class MigrationSyncPlan
    {
        public MigrationScriptDefinition Definition { get; set; }
        public MigrationChangeType ChangeType { get; set; }
        public string CurrentHash { get; set; }
        public string TargetHash { get; set; }
        public string SqlToApply { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;

        public bool HasChanges => ChangeType != MigrationChangeType.None;
    }
}
