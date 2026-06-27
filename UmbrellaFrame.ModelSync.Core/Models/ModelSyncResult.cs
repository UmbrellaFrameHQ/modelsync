using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace UmbrellaFrame.ModelSync.Core
{
    public sealed class ModelSyncResult
    {
        private readonly Func<string, CancellationToken, Task> _executor;

        public ModelSyncResult(IEnumerable<ModelSyncPlanItem> operations, Func<string, CancellationToken, Task> executor)
        {
            Operations = (operations ?? throw new ArgumentNullException(nameof(operations))).ToList();
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        public IReadOnlyList<ModelSyncPlanItem> Operations { get; }

        public IReadOnlyList<ModelSyncPlanItem> SafeOperations
            => Operations.Where(o => o.Risk == ModelSyncOperationRisk.Safe && o.CanApplyAutomatically && (o.HasSql || o.HasApplyOperation)).ToList();

        public IReadOnlyList<ModelSyncPlanItem> BlockedOperations
            => Operations.Where(o => o.Risk != ModelSyncOperationRisk.Safe || !o.CanApplyAutomatically).ToList();

        public bool HasChanges => Operations.Any(o => o.ChangeType != ModelSyncChangeType.None);
        public bool HasBlockedOperations => BlockedOperations.Count > 0;

        public Task ThrowIfUnsupportedOrDestructiveAsync()
        {
            if (!HasBlockedOperations)
                return Task.CompletedTask;

            var details = string.Join(Environment.NewLine, BlockedOperations.Select(o =>
                $"- {o.ChangeType} {o.Schema}.{o.Table}{(string.IsNullOrWhiteSpace(o.Column) ? string.Empty : "." + o.Column)}: {o.Reason}"));

            throw new InvalidOperationException("ModelSync found destructive, risky, or unsupported operations. Review the plan before applying SQL." + Environment.NewLine + details);
        }

        public async Task ApplyAsync(CancellationToken cancellationToken = default)
        {
            await ThrowIfUnsupportedOrDestructiveAsync().ConfigureAwait(false);

            foreach (var operation in SafeOperations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (operation.ApplyOperationAsync != null)
                    await operation.ApplyOperationAsync(operation, cancellationToken).ConfigureAwait(false);
                else
                    await _executor(operation.Sql, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
