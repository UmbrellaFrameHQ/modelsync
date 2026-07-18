using System;
using System.Collections.Generic;
using System.Linq;

namespace UmbrellaFrame.ModelSync.Core
{
    public sealed class MigrationExecutionResult
    {
        public MigrationExecutionResult(
            IEnumerable<MigrationExecutionItemResult> items,
            DateTimeOffset startedAt,
            DateTimeOffset completedAt,
            MigrationExecutionState state = MigrationExecutionState.CompletedWithoutTransaction,
            MigrationAtomicityLevel atomicity = MigrationAtomicityLevel.None,
            bool lockAcquired = false,
            bool transactionStarted = false,
            bool historyWritten = false,
            string errorCode = "",
            string errorMessage = "",
            string innerErrorMessage = "",
            IEnumerable<MigrationSyncPlan>? plans = null)
        {
            Items = (items ?? Enumerable.Empty<MigrationExecutionItemResult>()).ToList();
            StartedAt = startedAt;
            CompletedAt = completedAt;
            State = state;
            Atomicity = atomicity;
            LockAcquired = lockAcquired;
            TransactionStarted = transactionStarted;
            HistoryWritten = historyWritten;
            ErrorCode = errorCode ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
            InnerErrorMessage = innerErrorMessage ?? string.Empty;
            Plans = (plans ?? Enumerable.Empty<MigrationSyncPlan>()).ToList();
        }

        public IReadOnlyList<MigrationExecutionItemResult> Items { get; }
        public DateTimeOffset StartedAt { get; }
        public DateTimeOffset CompletedAt { get; }
        public MigrationExecutionState State { get; }
        public MigrationAtomicityLevel Atomicity { get; }
        public bool LockAcquired { get; }
        public bool TransactionStarted { get; }
        public bool HistoryWritten { get; }
        public string ErrorCode { get; }
        public string ErrorMessage { get; }
        public string InnerErrorMessage { get; }
        public IReadOnlyList<MigrationSyncPlan> Plans { get; }
        public TimeSpan Duration => CompletedAt >= StartedAt ? CompletedAt - StartedAt : TimeSpan.Zero;
        public bool Succeeded => State != MigrationExecutionState.Failed &&
                                 State != MigrationExecutionState.RolledBack &&
                                 State != MigrationExecutionState.PartiallyApplied &&
                                 State != MigrationExecutionState.LockTimeout &&
                                 State != MigrationExecutionState.Cancelled &&
                                 Items.All(i => i.Action != MigrationExecutionAction.Failed && i.Action != MigrationExecutionAction.Blocked);
    }
}
