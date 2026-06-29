using System;

namespace UmbrellaFrame.ModelSync.Core
{
    public sealed class MigrationExecutionItemResult
    {
        public MigrationScriptCategory Category { get; set; }
        public string ScriptId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public MigrationExecutionAction Action { get; set; }
        public string ExistingHash { get; set; } = string.Empty;
        public string TargetHash { get; set; } = string.Empty;
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset CompletedAt { get; set; }
        public TimeSpan Duration => CompletedAt >= StartedAt ? CompletedAt - StartedAt : TimeSpan.Zero;
        public int BatchCount { get; set; }
        public int CompletedBatchCount { get; set; }
        public string FailureStage { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public MigrationScriptExecutionMode ExecutionMode { get; set; } = MigrationScriptExecutionMode.HashTracked;
        public string DecisionReason { get; set; } = string.Empty;
        public bool LegacyHashAdopted { get; set; }
    }
}
