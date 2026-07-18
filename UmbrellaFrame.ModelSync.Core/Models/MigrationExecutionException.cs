using System;
using System.Linq;

namespace UmbrellaFrame.ModelSync.Core
{
    /// <summary>Represents a failed migration item returned by the execution engine.</summary>
    public sealed class MigrationExecutionException : Exception
    {
        public MigrationExecutionException(MigrationExecutionResult result)
            : base(CreateMessage(result))
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            Item = result.Items.FirstOrDefault(item =>
                item.Action == MigrationExecutionAction.Failed || item.Action == MigrationExecutionAction.Blocked);
        }

        public MigrationExecutionResult Result { get; }
        public MigrationExecutionItemResult? Item { get; }

        private static string CreateMessage(MigrationExecutionResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            var item = result.Items.FirstOrDefault(candidate =>
                candidate.Action == MigrationExecutionAction.Failed || candidate.Action == MigrationExecutionAction.Blocked);
            if (item == null)
                return $"Migration execution failed with state '{result.State}': {result.ErrorMessage}";

            var detail = string.IsNullOrWhiteSpace(item.ErrorMessage) ? item.ErrorCode : item.ErrorMessage;
            return $"Migration script '{item.ScriptId}' failed during '{item.FailureStage}': {detail}";
        }
    }
}
