using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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
        private readonly IMigrationLockStrategy? _lockStrategy;

        protected SqlMigrationRunnerBase(MigrationRunnerOptions? options = null, ILogger? logger = null, IMigrationLockStrategy? lockStrategy = null)
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

        public MigrationScriptDefinition RegisterScriptFile(string path, MigrationScriptCategory? category = null, string? id = null, string? name = null)
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
                var plannedSql = changeType != MigrationChangeType.None
                    ? new List<string> { definition.Sql }
                    : new List<string>();
                var repairSql = new List<string>();
                var unappliedDrift = new List<string>();
                var requiresManualReview = false;
                var historyDecision = changeType == MigrationChangeType.None
                    ? (legacyHashMissing ? MigrationHistoryDecision.AdoptLegacyHash : MigrationHistoryDecision.NoHistoryChange)
                    : MigrationHistoryDecision.RecordFullTargetHash;

                if (definition.Category == MigrationScriptCategory.Tables && changed)
                {
                    if (Options.AutoAddMissingColumnsFromTableScripts)
                        repairSql.AddRange(await BuildMissingColumnScriptsAsync(definition, cancellationToken).ConfigureAwait(false));

                    plannedSql.Clear();
                    requiresManualReview = true;
                    historyDecision = MigrationHistoryDecision.ManualReviewRequired;
                    reason = "Changed CREATE TABLE scripts require manual review because ModelSync cannot prove that the complete source drift is additive.";
                    if (Options.AutoAddMissingColumnsFromTableScripts)
                        unappliedDrift.Add("Best-effort missing-column repair SQL was generated for review only.");
                    else
                        unappliedDrift.Add("Best-effort missing-column repair is disabled. Enable AutoAddMissingColumnsFromTableScripts only for an explicit repair review workflow.");
                    unappliedDrift.Add("The full target hash will not be recorded. Review type, nullability, default, check, key, index, and constraint changes explicitly.");
                }

                plans.Add(new MigrationSyncPlan
                {
                    Definition = definition,
                    ChangeType = changeType,
                    CurrentHash = currentHash,
                    TargetHash = targetHash,
                    SqlToApply = plannedSql.Count == 1 ? plannedSql[0] : string.Empty,
                    SourceSql = definition.Sql,
                    PlannedExecutionSql = plannedSql,
                    RepairSql = repairSql,
                    UnappliedDrift = unappliedDrift,
                    RequiresManualReview = requiresManualReview,
                    HistoryDecision = historyDecision,
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
            var result = await RunWithResultAsync(cancellationToken).ConfigureAwait(false);
            if (result.State == MigrationExecutionState.Cancelled)
                throw new OperationCanceledException(result.ErrorMessage, cancellationToken);
            if (result.State == MigrationExecutionState.LockTimeout)
                throw new TimeoutException(result.ErrorMessage);
            if (!result.Succeeded)
                throw new MigrationExecutionException(result);
            return result.Plans;
        }

        public async Task<MigrationExecutionResult> RunWithResultAsync(CancellationToken cancellationToken = default)
        {
            var startedAt = DateTimeOffset.UtcNow;
            var items = new List<MigrationExecutionItemResult>();
            IDisposable? lockHandle = null;
            DbConnection? lockConnection = null;
            var lockAcquired = false;
            IReadOnlyList<MigrationSyncPlan> plans = Array.Empty<MigrationSyncPlan>();
            if (Options.ResetDatabase)
                ValidateResetSafety();

            try
            {
                TransactionPolicyStartsTransaction();
                ResolveAtomicityLevel();

                if (Options.ResetDatabase)
                {
                    await ResetDatabaseAsync(cancellationToken).ConfigureAwait(false);
                    await WaitUntilDatabaseReadyAfterResetAsync(cancellationToken).ConfigureAwait(false);
                }

                if (Options.LockOptions != null && Options.LockOptions.Enabled && Options.LockOptions.Mode != MigrationLockMode.Disabled)
                {
                    var strategy = ResolveLockStrategy();
                    lockConnection = await CreateLockConnectionAsync(cancellationToken).ConfigureAwait(false);
                    lockHandle = await strategy.AcquireAsync(lockConnection!, Options.LockOptions, cancellationToken).ConfigureAwait(false);
                    lockConnection = null;
                    lockAcquired = true;
                }

                await EnsureInfrastructureAsync(cancellationToken).ConfigureAwait(false);

                plans = await CompareRegisteredAsync(cancellationToken).ConfigureAwait(false);
                foreach (var plan in plans.Where(p => p.HasChanges))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var item = await ApplyPlanWithResultAsync(plan, cancellationToken).ConfigureAwait(false);
                    items.Add(item);
                    if (item.Action == MigrationExecutionAction.Failed || item.Action == MigrationExecutionAction.Blocked)
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
                var transactionStarted = items.Any(i => i.TransactionStarted);
                return new MigrationExecutionResult(
                    items,
                    startedAt,
                    DateTimeOffset.UtcNow,
                    ResolveExecutionState(items, failed, transactionStarted),
                    transactionStarted ? ResolveAtomicityLevel() : MigrationAtomicityLevel.None,
                    lockAcquired,
                    transactionStarted,
                    historyWritten,
                    plans: plans);
            }
            catch (OperationCanceledException ex)
            {
                return new MigrationExecutionResult(items, startedAt, DateTimeOffset.UtcNow, MigrationExecutionState.Cancelled, ResolveAtomicityLevel(), lockAcquired, TransactionPolicyStartsTransaction(), false, ex.GetType().Name, Redact(ex.Message), Redact(ex.InnerException?.Message ?? string.Empty), plans);
            }
            catch (TimeoutException ex)
            {
                return new MigrationExecutionResult(items, startedAt, DateTimeOffset.UtcNow, MigrationExecutionState.LockTimeout, MigrationAtomicityLevel.None, lockAcquired, false, false, ex.GetType().Name, Redact(ex.Message), Redact(ex.InnerException?.Message ?? string.Empty), plans);
            }
            catch (Exception ex)
            {
                return new MigrationExecutionResult(items, startedAt, DateTimeOffset.UtcNow, MigrationExecutionState.Failed, ResolveAtomicityLevel(), lockAcquired, TransactionPolicyStartsTransaction(), false, ex.GetType().Name, Redact(ex.Message), Redact(ex.InnerException?.Message ?? string.Empty), plans);
            }
            finally
            {
                SafeDisposeLock(lockHandle);
                lockConnection?.Dispose();
            }
        }

        protected virtual async Task ApplyPlanAsync(MigrationSyncPlan plan, CancellationToken cancellationToken)
        {
            var result = await ApplyPlanWithResultAsync(plan, cancellationToken).ConfigureAwait(false);
            if (result.Action == MigrationExecutionAction.Failed || result.Action == MigrationExecutionAction.Blocked)
                throw new MigrationExecutionException(new MigrationExecutionResult(
                    new[] { result },
                    result.StartedAt,
                    result.CompletedAt,
                    MigrationExecutionState.Failed));
        }

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

            if (plan.RequiresManualReview)
            {
                result.Action = MigrationExecutionAction.Blocked;
                result.FailureStage = "ManualReview";
                result.ErrorCode = "TableScriptDriftRequiresManualReview";
                result.ErrorMessage = plan.DecisionReason;
                result.CompletedAt = DateTimeOffset.UtcNow;
                return result;
            }

            var scripts = plan.PlannedExecutionSql.Count > 0
                ? plan.PlannedExecutionSql.ToList()
                : string.IsNullOrWhiteSpace(plan.SqlToApply) ? new List<string>() : new List<string> { plan.SqlToApply };

            try
            {
                var transactionPolicy = ResolveScriptTransactionPolicy(plan.Definition);
                using (var scope = await OpenExecutionScopeAsync(transactionPolicy, cancellationToken).ConfigureAwait(false))
                {
                    result.TransactionStarted = scope.TransactionStarted;
                    foreach (var sql in scripts)
                    {
                        var batches = SplitBatches(PrepareScriptSql(plan.Definition, sql)).Where(batch => !string.IsNullOrWhiteSpace(batch)).ToList();
                        result.BatchCount += batches.Count;
                        foreach (var batch in batches)
                        {
                            try
                            {
                                await scope.ExecuteSqlAsync(batch, cancellationToken).ConfigureAwait(false);
                                result.CompletedBatchCount++;
                            }
                            catch (Exception ex) when (!(ex is OperationCanceledException))
                            {
                                result.RollbackSucceeded = await scope.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                                result.Action = MigrationExecutionAction.Failed;
                                result.FailureStage = "ExecuteBatch";
                                result.ErrorCode = ex.GetType().Name;
                                result.FailedBatchIndex = result.CompletedBatchCount + 1;
                                result.FailedBatchPreview = CreateBatchPreview(batch);
                                PopulateProviderError(result, ex);
                                result.CompletedAt = DateTimeOffset.UtcNow;
                                return result;
                            }
                        }
                    }

                    try
                    {
                        if (scripts.Count > 0)
                            await RecordHistoryAsync(scope, plan.Definition, plan.TargetHash, cancellationToken).ConfigureAwait(false);
                        else if (plan.LegacyHashAdoptionRequired)
                            await AdoptLegacyHashAsync(plan, cancellationToken).ConfigureAwait(false);
                        await scope.CompleteAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        result.RollbackSucceeded = await scope.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                        result.Action = MigrationExecutionAction.Failed;
                        result.FailureStage = "RecordHistoryOrCommit";
                        result.ErrorCode = ex.GetType().Name;
                        PopulateProviderError(result, ex);
                        result.CompletedAt = DateTimeOffset.UtcNow;
                        return result;
                    }
                }

                result.LegacyHashAdopted = plan.LegacyHashAdoptionRequired;
                result.CompletedAt = DateTimeOffset.UtcNow;
                return result;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                result.Action = MigrationExecutionAction.Failed;
                result.FailureStage = result.CompletedBatchCount < result.BatchCount ? "ExecuteBatch" : "RecordHistory";
                result.ErrorCode = ex.GetType().Name;
                PopulateProviderError(result, ex);
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
            if (reset == null || !reset.Enabled)
                throw new InvalidOperationException("Database reset requires ResetOptions.Enabled=true, explicit approval, and ExpectedDatabaseName.");

            var approval = reset.Approval;
            if (approval == null || !approval.AllowDestructiveChanges)
                throw new InvalidOperationException("Database reset is destructive. Provide explicit destructive approval before executing it.");
            if (string.IsNullOrWhiteSpace(reset.ExpectedDatabaseName))
                throw new InvalidOperationException("ExpectedDatabaseName is required for database reset.");
            if (reset.BackupBeforeReset &&
                string.IsNullOrWhiteSpace(reset.BackupFilePath) &&
                string.IsNullOrWhiteSpace(reset.BackupDirectory))
                throw new InvalidOperationException("BackupDirectory or BackupFilePath is required when BackupBeforeReset is enabled.");
            if (reset.BackupBeforeReset && !SupportsDatabaseBackupBeforeReset)
                throw new NotSupportedException($"{ProviderName} does not support BackupBeforeReset. Create and verify a provider-native backup before requesting reset.");
            if (reset.AllowedEnvironments != null && reset.AllowedEnvironments.Count > 0 &&
                !reset.AllowedEnvironments.Any(e => string.Equals(e, reset.EnvironmentName, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Database reset is not allowed for the configured environment.");
            ValidateResetDatabaseName(reset.ExpectedDatabaseName);
        }

        protected virtual bool SupportsTransactions => false;

        protected virtual bool SupportsTransactionalDdl => false;

        protected virtual bool SupportsDatabaseBackupBeforeReset => false;

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

        private MigrationExecutionState ResolveExecutionState(
            IReadOnlyList<MigrationExecutionItemResult> items,
            bool failed,
            bool transactionStarted)
        {
            if (!failed)
                return transactionStarted ? MigrationExecutionState.Committed : MigrationExecutionState.CompletedWithoutTransaction;

            var failedIndex = items.ToList().FindIndex(item =>
                item.Action == MigrationExecutionAction.Failed || item.Action == MigrationExecutionAction.Blocked);
            var priorApplied = failedIndex > 0 && items.Take(failedIndex).Any(item =>
                item.Action == MigrationExecutionAction.Applied || item.Action == MigrationExecutionAction.Reapplied);
            var failedItem = failedIndex >= 0 ? items[failedIndex] : null;
            if (priorApplied || (failedItem != null && failedItem.CompletedBatchCount > 0 && !failedItem.RollbackSucceeded))
                return MigrationExecutionState.PartiallyApplied;
            if (failedItem != null && failedItem.RollbackSucceeded)
                return MigrationExecutionState.RolledBack;
            return MigrationExecutionState.Failed;
        }

        protected bool TransactionPolicyStartsTransaction(MigrationTransactionPolicy policy)
        {
            if (policy == MigrationTransactionPolicy.Forbidden)
                return false;
            if (policy == MigrationTransactionPolicy.Required && !SupportsTransactions)
                throw new InvalidOperationException("TransactionRequiredButUnsupported");
            return SupportsTransactions;
        }

        protected MigrationTransactionPolicy ResolveScriptTransactionPolicy(MigrationScriptDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            var policy = definition.TransactionPolicyOverride ?? Options.TransactionPolicy;
            if (policy == MigrationTransactionPolicy.Auto && !IsTransactionCompatible(definition))
                return MigrationTransactionPolicy.Forbidden;
            return policy;
        }

        protected virtual bool IsTransactionCompatible(MigrationScriptDefinition definition)
            => true;

        protected virtual Task<DbConnection?> CreateLockConnectionAsync(CancellationToken cancellationToken)
            => Task.FromResult<DbConnection?>(null);

        protected virtual Task<DbConnection?> CreateReadinessConnectionAsync(CancellationToken cancellationToken)
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

        protected virtual string ProviderName => GetType().Name;

        protected int ResetCommandTimeoutSeconds
        {
            get
            {
                var timeout = Options.ResetOptions?.CommandTimeout ?? TimeSpan.FromSeconds(30);
                return Math.Max(1, (int)Math.Ceiling(timeout.TotalSeconds));
            }
        }

        protected static string CreateHistoryKey(MigrationScriptCategory category, string id)
            => $"{category}:{id}";

        protected virtual IReadOnlyList<string> SplitBatches(string sql)
            => SqlBatchSplitter.SingleBatch(sql);

        protected virtual string PrepareScriptSql(MigrationScriptDefinition definition, string sql)
            => sql;

        protected virtual Task<IMigrationExecutionScope> OpenExecutionScopeAsync(MigrationTransactionPolicy transactionPolicy, CancellationToken cancellationToken)
            => Task.FromResult<IMigrationExecutionScope>(new DelegatingMigrationExecutionScope(ExecuteSqlAsync));

        protected virtual Task RecordHistoryAsync(IMigrationExecutionScope scope, MigrationScriptDefinition definition, string hash, CancellationToken cancellationToken)
            => RecordHistoryAsync(definition, hash, cancellationToken);

        protected virtual void PopulateProviderError(MigrationExecutionItemResult result, Exception exception)
        {
            result.ErrorMessage = Redact(exception.Message);
            result.InnerErrorMessage = Redact(exception.InnerException?.Message ?? string.Empty);
        }

        protected static string CreateBatchPreview(string sql)
        {
            var normalized = Regex.Replace(sql ?? string.Empty, @"\s+", " ").Trim();
            normalized = SqlTextSanitizer.RedactLiteralsAndComments(normalized);
            normalized = Redact(normalized);
            return normalized.Length <= 1024 ? normalized : normalized.Substring(0, 1024);
        }

        protected static string Redact(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var redacted = Regex.Replace(value, @"(?i)(password|pwd|token|api[_-]?key|secret|access[_-]?key)\s*=\s*[^;\s]+", "$1=<redacted>");
            redacted = Regex.Replace(redacted, @"(?i)(password|pwd|token|api[_-]?key|secret|access[_-]?key)\s*[:=]\s*['""]?[^,'""\s;]+", "$1=<redacted>");
            redacted = Regex.Replace(redacted, @"(?i)\bBearer\s+[A-Za-z0-9._~+/=-]+", "Bearer <redacted>");
            redacted = Regex.Replace(redacted, @"\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\b", "<redacted-jwt>");
            return redacted;
        }

        protected virtual Task<IReadOnlyList<string>> BuildMissingColumnScriptsAsync(MigrationScriptDefinition definition, CancellationToken cancellationToken)
            => Task.FromResult((IReadOnlyList<string>)new List<string>());

        protected virtual bool IsMissingInfrastructureException(Exception exception)
            => false;

        private void SafeDisposeLock(IDisposable? lockHandle)
        {
            if (lockHandle == null)
                return;

            try
            {
                lockHandle.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Migration lock release failed after migration execution. The original migration outcome is preserved.");
            }
        }

        protected virtual async Task WaitUntilDatabaseReadyAfterResetAsync(CancellationToken cancellationToken)
        {
            var reset = Options.ResetOptions;
            if (reset == null)
                return;

            using (var connection = await CreateReadinessConnectionAsync(cancellationToken).ConfigureAwait(false))
            {
                if (connection == null)
                    return;

                var strategy = new DefaultDatabaseReadinessStrategy();
                await strategy.WaitUntilReadyAsync(
                    connection,
                    new DatabaseReadinessContext
                    {
                        Provider = ProviderName,
                        DatabaseName = reset.ExpectedDatabaseName,
                        RetryCount = reset.ReadinessRetryCount,
                        RetryDelay = reset.ReadinessRetryDelay
                    },
                    cancellationToken).ConfigureAwait(false);
            }
        }

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

        protected interface IMigrationExecutionScope : IDisposable
        {
            bool TransactionStarted { get; }
            Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken);
            Task CompleteAsync(CancellationToken cancellationToken);
            Task<bool> RollbackAsync(CancellationToken cancellationToken);
        }

        private sealed class DelegatingMigrationExecutionScope : IMigrationExecutionScope
        {
            private readonly Func<string, CancellationToken, Task> _execute;

            public DelegatingMigrationExecutionScope(Func<string, CancellationToken, Task> execute)
            {
                _execute = execute;
            }

            public Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken)
                => _execute(sql, cancellationToken);

            public bool TransactionStarted => false;

            public Task CompleteAsync(CancellationToken cancellationToken)
                => Task.CompletedTask;

            public Task<bool> RollbackAsync(CancellationToken cancellationToken)
                => Task.FromResult(false);

            public void Dispose()
            {
            }
        }
    }
}
