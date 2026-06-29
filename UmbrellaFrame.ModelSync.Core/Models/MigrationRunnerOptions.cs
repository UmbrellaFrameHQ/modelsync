using System.Collections.Generic;

namespace UmbrellaFrame.ModelSync.Core
{
    /// <summary>Options for SQL migration runners.</summary>
    public sealed class MigrationRunnerOptions
    {
        public bool ResetDatabase { get; set; }
        public DestructiveOperationOptions? DestructiveOptions { get; set; }
        public DatabaseResetOptions? ResetOptions { get; set; }
        public bool EnsureHistoryTables { get; set; } = true;
        public bool AutoAddMissingColumnsFromTableScripts { get; set; } = true;
        public string HistorySchema { get; set; } = "sec";
        public IList<string> Schemas { get; } = new List<string>();
        public MigrationLockOptions LockOptions { get; set; } = new MigrationLockOptions();
        public MigrationTransactionPolicy TransactionPolicy { get; set; } = MigrationTransactionPolicy.Auto;
        public MigrationScriptExecutionMode DefaultExecutionMode { get; set; } = MigrationScriptExecutionMode.HashTracked;
        public MigrationCategoryExecutionPolicyCollection CategoryPolicies { get; } = new MigrationCategoryExecutionPolicyCollection();

        public static MigrationRunnerOptions Default()
            => new MigrationRunnerOptions();

        public MigrationRunnerOptions ApplyCompatibilityProfile(string profile)
        {
            if (profile == MigrationCompatibilityProfiles.LegacyEmbeddedSql)
            {
                CategoryPolicies
                    .ForCategory(MigrationScriptCategory.StoredProcedures, MigrationScriptExecutionMode.EveryRun)
                    .ForCategory(MigrationScriptCategory.Triggers, MigrationScriptExecutionMode.EveryRun)
                    .ForCategory(MigrationScriptCategory.Seeds, MigrationScriptExecutionMode.RunOnce)
                    .ForCategory(MigrationScriptCategory.CustomSql, MigrationScriptExecutionMode.HashTracked);
            }

            return this;
        }
    }
}
