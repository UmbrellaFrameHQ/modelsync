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
        /// <summary>
        /// When enabled, changed table scripts may produce best-effort missing-column repair SQL.
        /// Changed table scripts still require manual review and never advance history automatically.
        /// </summary>
        public bool AutoAddMissingColumnsFromTableScripts { get; set; }
        public string HistorySchema { get; set; } = "sec";
        public IList<string> Schemas { get; } = new List<string>();
        public MigrationLockOptions LockOptions { get; set; } = new MigrationLockOptions();
        public MigrationTransactionPolicy TransactionPolicy { get; set; } = MigrationTransactionPolicy.Auto;
        public MigrationScriptExecutionMode DefaultExecutionMode { get; set; } = MigrationScriptExecutionMode.HashTracked;
        public MigrationCategoryExecutionPolicyCollection CategoryPolicies { get; } = new MigrationCategoryExecutionPolicyCollection();
        public IList<string> AppliedCompatibilityProfiles { get; } = new List<string>();

        public static MigrationRunnerOptions Default()
            => new MigrationRunnerOptions();

        public MigrationRunnerOptions ApplyCompatibilityProfile(string profile)
        {
            if (profile == MigrationCompatibilityProfiles.LegacyEmbeddedSql)
            {
                if (!AppliedCompatibilityProfiles.Contains(profile))
                    AppliedCompatibilityProfiles.Add(profile);

                CategoryPolicies
                    .ForCategory(MigrationScriptCategory.StoredProcedures, MigrationScriptExecutionMode.EveryRun)
                    .ForCategory(MigrationScriptCategory.Triggers, MigrationScriptExecutionMode.EveryRun)
                    .ForCategory(MigrationScriptCategory.Seeds, MigrationScriptExecutionMode.RunOnce)
                    .ForCategory(MigrationScriptCategory.CustomSql, MigrationScriptExecutionMode.HashTracked);
            }

            if (profile == MigrationCompatibilityProfiles.LegacyApplicationSchemas)
            {
                if (!AppliedCompatibilityProfiles.Contains(profile))
                    AppliedCompatibilityProfiles.Add(profile);

                foreach (var schema in new[] { "app", "ref", "sec", "auth", "log", "crm", "exp", "veh", "fin" })
                {
                    if (!Schemas.Contains(schema))
                        Schemas.Add(schema);
                }
            }

            return this;
        }
    }
}
