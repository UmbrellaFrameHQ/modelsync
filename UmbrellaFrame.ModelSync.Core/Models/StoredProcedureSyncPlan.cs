namespace UmbrellaFrame.ModelSync.Core
{
    /// <summary>
    /// A dry-run plan for a stored procedure synchronization operation.
    /// </summary>
    public sealed class StoredProcedureSyncPlan
    {
        /// <summary>The tracked project definition.</summary>
        public StoredProcedureDefinition Definition { get; set; }

        /// <summary>The detected change type.</summary>
        public StoredProcedureChangeType ChangeType { get; set; }

        /// <summary>Hash of the current database definition, if the procedure exists.</summary>
        public string CurrentHash { get; set; }

        /// <summary>Hash of the project definition.</summary>
        public string TargetHash { get; set; }

        /// <summary>SQL that will be executed when this plan is applied.</summary>
        public string SqlToApply { get; set; }

        /// <summary>Returns <c>true</c> when the plan contains a create or alter operation.</summary>
        public bool HasChanges => ChangeType != StoredProcedureChangeType.None;
    }
}
