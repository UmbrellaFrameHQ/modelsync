using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UmbrellaFrame.ModelSync.Core.Interfaces;

namespace UmbrellaFrame.ModelSync.Core.Services
{
    /// <summary>Base implementation for ordered SQL migration runners.</summary>
    public abstract class SqlMigrationRunnerBase : IMigrationRunner
    {
        private readonly List<MigrationScriptDefinition> _definitions = new List<MigrationScriptDefinition>();
        private readonly ILogger _logger;
        private readonly IMigrationLockStrategy _lockStrategy;

        protected SqlMigrationRunnerBase(MigrationRunnerOptions options = null, ILogger logger = null, IMigrationLockStrategy lockStrategy = null)
        {
            Options = options ?? MigrationRunnerOptions.Default();
            _logger = logger ?? NullLogger.Instance;
            _lockStrategy = lockStrategy;
        }

        protected MigrationRunnerOptions Options { get; }

        public void RegisterScript(MigrationScriptDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            _definitions.Add(definition);
        }

        public MigrationScriptDefinition RegisterScriptFile(string path, MigrationScriptCategory? category = null, string id = null, string name = null)
        {
            var definition = MigrationScriptDefinition.FromFile(path, category, id, name);
            RegisterScript(definition);
            return definition;
        }

        public IReadOnlyList<MigrationScriptDefinition> RegisterEmbeddedScripts(Assembly assembly, params string[] prefixes)
        {
            var definitions = MigrationScriptDiscovery.FromEmbeddedResources(assembly, prefixes);
            foreach (var definition in definitions)
                RegisterScript(definition);
            return definitions;
        }

        public async Task<IReadOnlyList<MigrationSyncPlan>> CompareRegisteredAsync(CancellationToken cancellationToken = default)
        {
            ValidateUniqueDefinitions();

            IDictionary<string, string> history;
            try
            {
                history = await ReadHistoryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsMissingInfrastructureException(ex))
            {
                history = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var plans = new List<MigrationSyncPlan>();
            foreach (var definition in MigrationScriptDiscovery.Order(_definitions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var key = CreateHistoryKey(definition.Category, definition.Id);
                var historyRowExists = history.TryGetValue(key, out var currentHash);
                var targetHash = SqlDefinitionNormalizer.ComputeHash(definition.Sql);
                var legacyHashMissing = historyRowExists && string.IsNullOrWhiteSpace(currentHash);
                var mode = Options.CategoryPolicies.Resolve(definition.Category, Options.DefaultExecutionMode);
                var changed = historyRowExists && !legacyHashMissing && !string.Equals(currentHash, targetHash, StringComparison.Ordinal);
                var changeType = ResolveChangeType(mode, historyRowExists, legacyHashMissing, changed);
                var reason = ResolveDecisionReason(mode, historyRowExists, legacyHashMissing, changed);

                plans.Add(new MigrationSyncPlan
                {
                    Definition = definition,
                    ChangeType = changeType,
                    CurrentHash = currentHash,
                    TargetHash = targetHash,
                    SqlToApply = changeType != MigrationChangeType.None ? definition.Sql : string.Empty,
                    Reason = reason,
                    DecisionReason = reason,
                    ExecutionMode = mode,
                    HistoryRowExists = historyRowExists,
                    LegacyHashAdoptionRequired = legacyHashMissing
                });
            }

            return plans;
        }

        public async Task EnsureInfrastructureAsync(CancellationToken cancellationToken = default)
        {
            await EnsureSchemasAsync(Options.Schemas, cancellationToken).ConfigureAwait(false);

            if (Options.EnsureHistoryTables)
                await EnsureHistoryTablesAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<MigrationSyncPlan>> RunAsync(CancellationToken cancellationToken = default)
        {
            if (Options.ResetDatabase)
                ValidateResetSafety();

            IDisposable? lockHandle = null;
            DbConnection? lockConnection = null;
            try
            {
                TransactionPolicyStartsTransaction();
                ResolveAtomicityLevel();

                if (Options.LockOptions != null && Options.LockOptions.Enabled && Options.LockOptions.Mode != MigrationLockMode.Disabled)
                {
                    var strategy = ResolveLockStrategy();
                    lockConnection = await CreateLockConnectionAsync(cancellationToken).ConfigureAwait(false);
                    lockHandle = await strategy.AcquireAsync(lockConnection, Options.LockOptions, cancellationToken).ConfigureAwait(false);
                    lockConnection = null;
                }

                if (Options.ResetDatabase)
                    await ResetDatabaseAsync(cancellationToken).ConfigureAwait(false);

                await EnsureInfrastructureAsync(cancellationToken).ConfigureAwait(false);

                var plans = await CompareRegisteredAsync(cancellationToken).ConfigureAwait(false);
                foreach (var plan in plans.Where(p => !p.HasChanges && p.LegacyHashAdoptionRequired))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await AdoptLegacyHashAsync(plan, cancellationToken).ConfigureAwait(false);
                }

                foreach (var plan in plans.Where(p => p.HasChanges))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ApplyPlanAsync(plan, cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("Migration script applied: {Category} {Id} {Name}", plan.Definition.Category, plan.Definition.Id, plan.Definition.Name);
                }

                return plans;
            }
            finally
            {
                lockHandle?.Dispose();
                lockConnection?.Dispose();
            }
        }

        public async Task<MigrationExecutionResult> RunWithResultAsync(CancellationToken cancellationToken = default)
        {
            var startedAt = DateTimeOffset.UtcNow;
            var items = new List<MigrationExecutionItemResult>();
            IDisposable? lockHandle = null;
            DbConnection? lockConnection = null;
            var lockAcquired = false;
            if (Options.ResetDatabase)
                ValidateResetSafety();

            try
            {
                TransactionPolicyStartsTransaction();
                ResolveAtomicityLevel();

                if (Options.LockOptions != null && Options.LockOptions.Enabled && Options.LockOptions.Mode != MigrationLockMode.Disabled)
                {
                    var strategy = ResolveLockStrategy();
                    lockConnection = await CreateLockConnectionAsync(cancellationToken).ConfigureAwait(false);
                    lockHandle = await strategy.AcquireAsync(lockConnection, Options.LockOptions, cancellationToken).ConfigureAwait(false);
                    lockConnection = null;
                    lockAcquired = true;
                }

                if (Options.ResetDatabase)
                {
                    await ResetDatabaseAsync(cancellationToken).ConfigureAwait(false);
                }

                await EnsureInfrastructureAsync(cancellationToken).ConfigureAwait(false);

                var plans = await CompareRegisteredAsync(cancellationToken).ConfigureAwait(false);
                foreach (var plan in plans.Where(p => p.HasChanges))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var item = await ApplyPlanWithResultAsync(plan, cancellationToken).ConfigureAwait(false);
                    items.Add(item);
                    if (item.Action == MigrationExecutionAction.Failed)
                        break;
                    _logger.LogInformation("Migration script applied: {Category} {Id} {Name}", plan.Definition.Category, plan.Definition.Id, plan.Definition.Name);
                }

                foreach (var plan in plans.Where(p => !p.HasChanges))
                {
                    if (plan.LegacyHashAdoptionRequired)
                        await AdoptLegacyHashAsync(plan, cancellationToken).ConfigureAwait(false);

                    var now = DateTimeOffset.UtcNow;
                    items.Add(new MigrationExecutionItemResult
                    {
                        Category = plan.Definition.Category,
                        ScriptId = plan.Definition.Id,
                        Name = plan.Definition.Name,
                        Source = plan.Definition.Source,
                        Action = MigrationExecutionAction.Skipped,
                        ExistingHash = plan.CurrentHash,
                        TargetHash = plan.TargetHash,
                        ExecutionMode = plan.ExecutionMode,
                        DecisionReason = plan.DecisionReason,
                        LegacyHashAdopted = plan.LegacyHashAdoptionRequired,
                        StartedAt = now,
                        CompletedAt = now
                    });
                }

                var failed = items.Any(i => i.Action == MigrationExecutionAction.Failed || i.Action == MigrationExecutionAction.Blocked);
                var historyWritten = items.Any(i => i.Action == MigrationExecutionAction.Applied || i.Action == MigrationExecutionAction.Reapplied);
                return new MigrationExecutionResult(
                    items,
                    startedAt,
                    DateTimeOffset.UtcNow,
                    failed ? MigrationExecutionState.Failed : ResolveSuccessfulState(),
                    ResolveAtomicityLevel(),
                    lockAcquired,
                    TransactionPolicyStartsTransaction(),
                    historyWritten);
            }
            catch (OperationCanceledException)
            {
                return new MigrationExecutionResult(items, startedAt, DateTimeOffset.UtcNow, MigrationExecutionState.Cancelled, ResolveAtomicityLevel(), lockAcquired, TransactionPolicyStartsTransaction(), false);
            }
            catch (TimeoutException)
            {
                return new MigrationExecutionResult(items, startedAt, DateTimeOffset.UtcNow, MigrationExecutionState.LockTimeout, MigrationAtomicityLevel.None, lockAcquired, false, false);
            }
            catch
            {
                return new MigrationExecutionResult(items, startedAt, DateTimeOffset.UtcNow, MigrationExecutionState.Failed, ResolveAtomicityLevel(), lockAcquired, TransactionPolicyStartsTransaction(), false);
            }
            finally
            {
                lockHandle?.Dispose();
                lockConnection?.Dispose();
            }
        }

        protected virtual async Task ApplyPlanAsync(MigrationSyncPlan plan, CancellationToken cancellationToken)
            => await ApplyPlanWithResultAsync(plan, cancellationToken).ConfigureAwait(false);

        protected virtual async Task<MigrationExecutionItemResult> ApplyPlanWithResultAsync(MigrationSyncPlan plan, CancellationToken cancellationToken)
        {
            var startedAt = DateTimeOffset.UtcNow;
            var result = new MigrationExecutionItemResult
            {
                Category = plan.Definition.Category,
                ScriptId = plan.Definition.Id,
                Name = plan.Definition.Name,
                Source = plan.Definition.Source,
                Action = plan.ChangeType == MigrationChangeType.Reapply ? MigrationExecutionAction.Reapplied : MigrationExecutionAction.Applied,
                ExistingHash = plan.CurrentHash,
                TargetHash = plan.TargetHash,
                ExecutionMode = plan.ExecutionMode,
                DecisionReason = plan.DecisionReason,
                StartedAt = startedAt
            };

            var scripts = new List<string>();
            if (plan.Definition.Category == MigrationScriptCategory.Tables &&
                plan.ChangeType == MigrationChangeType.Reapply &&
                Options.AutoAddMissingColumnsFromTableScripts)
            {
                scripts.AddRange(await BuildMissingColumnScriptsAsync(plan.Definition, cancellationToken).ConfigureAwait(false));
            }
            else
            {
                scripts.Add(plan.SqlToApply);
            }

            try
            {
                foreach (var sql in scripts)
                {
                    var batches = SplitBatches(sql).Where(batch => !string.IsNullOrWhiteSpace(batch)).ToList();
                    result.BatchCount += batches.Count;
                    foreach (var batch in batches)
                    {
                        await ExecuteSqlAsync(batch, cancellationToken).ConfigureAwait(false);
                        result.CompletedBatchCount++;
                    }
                }

                if (scripts.Count > 0)
                    await RecordHistoryAsync(plan.Definition, plan.TargetHash, cancellationToken).ConfigureAwait(false);
                else if (plan.LegacyHashAdoptionRequired)
                    await AdoptLegacyHashAsync(plan, cancellationToken).ConfigureAwait(false);

                result.LegacyHashAdopted = plan.LegacyHashAdoptionRequired;
                result.CompletedAt = DateTimeOffset.UtcNow;
                return result;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                result.Action = MigrationExecutionAction.Failed;
                result.FailureStage = result.CompletedBatchCount < result.BatchCount ? "ExecuteBatch" : "RecordHistory";
                result.ErrorCode = ex.GetType().Name;
                result.CompletedAt = DateTimeOffset.UtcNow;
                return result;
            }
        }

        private static MigrationChangeType ResolveChangeType(
            MigrationScriptExecutionMode mode,
            bool historyRowExists,
            bool legacyHashMissing,
            bool changed)
        {
            if (!historyRowExists)
                return MigrationChangeType.Apply;
            if (legacyHashMissing)
                return mode == MigrationScriptExecutionMode.EveryRun ? MigrationChangeType.Reapply : MigrationChangeType.None;
            if (mode == MigrationScriptExecutionMode.EveryRun)
                return MigrationChangeType.Reapply;
            if (mode == MigrationScriptExecutionMode.HashTracked && changed)
                return MigrationChangeType.Reapply;
            return MigrationChangeType.None;
        }

        private static string ResolveDecisionReason(
            MigrationScriptExecutionMode mode,
            bool historyRowExists,
            bool legacyHashMissing,
            bool changed)
        {
            if (!historyRowExists)
                return "Script has not been applied.";
            if (legacyHashMissing && mode == MigrationScriptExecutionMode.EveryRun)
                return "LegacyHashAdopted; ExecutionModeEveryRun.";
            if (legacyHashMissing)
                return "LegacyHashAdopted.";
            if (mode == MigrationScriptExecutionMode.EveryRun)
                return "ExecutionModeEveryRun.";
            if (mode == MigrationScriptExecutionMode.RunOnce && changed)
                return "RunOnceScriptChanged.";
            if (changed)
                return "Script hash changed.";
            return "Script already applied.";
        }

        protected virtual Task AdoptLegacyHashAsync(MigrationSyncPlan plan, CancellationToken cancellationToken)
            => RecordHistoryAsync(plan.Definition, plan.TargetHash, cancellationToken);

        protected virtual void ValidateResetSafety()
        {
            var reset = Options.ResetOptions;
            var approval = reset?.Approval ?? Options.DestructiveOptions;
            if (approval == null || !approval.AllowDestructiveChanges)
                throw new InvalidOperationException("Database reset is destructive. Provide explicit destructive approval before executing it.");
            if (reset != null && reset.Enabled)
            {
                if (string.IsNullOrWhiteSpace(reset.ExpectedDatabaseName))
                    throw new InvalidOperationException("ExpectedDatabaseName is required for database reset.");
                if (reset.AllowedEnvironments != null && reset.AllowedEnvironments.Count > 0 &&
                    !reset.AllowedEnvironments.Any(e => string.Equals(e, reset.EnvironmentName, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Database reset is not allowed for the configured environment.");
                ValidateResetDatabaseName(reset.ExpectedDatabaseName);
            }
        }

        protected virtual bool SupportsTransactions => false;

        protected virtual bool SupportsTransactionalDdl => false;

        protected virtual MigrationExecutionState ResolveSuccessfulState()
        {
            if (Options.TransactionPolicy == MigrationTransactionPolicy.Forbidden)
                return MigrationExecutionState.CompletedWithoutTransaction;
            return SupportsTransactions ? MigrationExecutionState.Committed : MigrationExecutionState.CompletedWithoutTransaction;
        }

        protected virtual MigrationAtomicityLevel ResolveAtomicityLevel()
        {
            if (Options.TransactionPolicy == MigrationTransactionPolicy.Required && !SupportsTransactions)
                throw new InvalidOperationException("TransactionRequiredButUnsupported");
            if (Options.TransactionPolicy == MigrationTransactionPolicy.Forbidden)
                return MigrationAtomicityLevel.None;
            if (SupportsTransactions && SupportsTransactionalDdl)
                return MigrationAtomicityLevel.Full;
            if (SupportsTransactions)
                return MigrationAtomicityLevel.HistoryOnly;
            return MigrationAtomicityLevel.Unsupported;
        }

        protected virtual bool TransactionPolicyStartsTransaction()
        {
            if (Options.TransactionPolicy == MigrationTransactionPolicy.Forbidden)
                return false;
            if (Options.TransactionPolicy == MigrationTransactionPolicy.Required && !SupportsTransactions)
                throw new InvalidOperationException("TransactionRequiredButUnsupported");
            return SupportsTransactions;
        }

        protected virtual Task<DbConnection?> CreateLockConnectionAsync(CancellationToken cancellationToken)
            => Task.FromResult<DbConnection?>(null);

        private IMigrationLockStrategy ResolveLockStrategy()
        {
            if (Options.LockOptions == null || !Options.LockOptions.Enabled || Options.LockOptions.Mode == MigrationLockMode.Disabled)
                return new NoOpMigrationLockStrategy();
            if (Options.LockOptions.Mode == MigrationLockMode.InMemory)
                return _lockStrategy ?? new InMemoryMigrationLockStrategy();
            if (_lockStrategy == null)
                throw new InvalidOperationException("ProviderNativeLockUnsupported");
            return _lockStrategy;
        }

        protected virtual void ValidateResetDatabaseName(string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName))
                throw new InvalidOperationException("ExpectedDatabaseName is required for database reset.");
        }

        protected static string CreateHistoryKey(MigrationScriptCategory category, string id)
            => $"{category}:{id}";

        protected virtual IReadOnlyList<string> SplitBatches(string sql)
            => SqlBatchSplitter.SingleBatch(sql);

        protected virtual Task<IReadOnlyList<string>> BuildMissingColumnScriptsAsync(MigrationScriptDefinition definition, CancellationToken cancellationToken)
            => Task.FromResult((IReadOnlyList<string>)new List<string>());

        protected virtual bool IsMissingInfrastructureException(Exception exception)
            => false;

        private void ValidateUniqueDefinitions()
        {
            var duplicate = _definitions
                .GroupBy(d => CreateHistoryKey(d.Category, d.Id), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicate == null)
                return;

            var sources = string.Join(", ", duplicate.Select(d => string.IsNullOrWhiteSpace(d.Source) ? d.Name : d.Source));
            var first = duplicate.First();
            throw new InvalidOperationException(
                $"Duplicate migration script id '{first.Id}' in category '{first.Category}'. Sources: {sources}.");
        }

        protected abstract Task ResetDatabaseAsync(CancellationToken cancellationToken);
        protected abstract Task EnsureSchemasAsync(IEnumerable<string> schemas, CancellationToken cancellationToken);
        protected abstract Task EnsureHistoryTablesAsync(CancellationToken cancellationToken);
        protected abstract Task<IDictionary<string, string>> ReadHistoryAsync(CancellationToken cancellationToken);
        protected abstract Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken);
        protected abstract Task RecordHistoryAsync(MigrationScriptDefinition definition, string hash, CancellationToken cancellationToken);
    }
}
