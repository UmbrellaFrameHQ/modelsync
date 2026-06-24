using System;
using System.Collections.Generic;
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

        protected SqlMigrationRunnerBase(MigrationRunnerOptions options = null, ILogger logger = null)
        {
            Options = options ?? MigrationRunnerOptions.Default();
            _logger = logger ?? NullLogger.Instance;
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
            if (Options.EnsureHistoryTables)
                await EnsureHistoryTablesAsync(cancellationToken).ConfigureAwait(false);

            var history = await ReadHistoryAsync(cancellationToken).ConfigureAwait(false);
            var plans = new List<MigrationSyncPlan>();
            foreach (var definition in MigrationScriptDiscovery.Order(_definitions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var key = CreateHistoryKey(definition.Category, definition.Id);
                history.TryGetValue(key, out var currentHash);
                var targetHash = SqlDefinitionNormalizer.ComputeHash(definition.Sql);
                var isApplied = !string.IsNullOrWhiteSpace(currentHash);
                var changed = isApplied && !string.Equals(currentHash, targetHash, StringComparison.Ordinal);

                plans.Add(new MigrationSyncPlan
                {
                    Definition = definition,
                    ChangeType = !isApplied ? MigrationChangeType.Apply : changed ? MigrationChangeType.Reapply : MigrationChangeType.None,
                    CurrentHash = currentHash,
                    TargetHash = targetHash,
                    SqlToApply = !isApplied || changed ? definition.Sql : string.Empty,
                    Reason = !isApplied ? "Script has not been applied." : changed ? "Script hash changed." : "Script already applied."
                });
            }

            return plans;
        }

        public async Task<IReadOnlyList<MigrationSyncPlan>> RunAsync(CancellationToken cancellationToken = default)
        {
            if (Options.ResetDatabase)
            {
                if (Options.DestructiveOptions == null || !Options.DestructiveOptions.AllowDestructiveChanges)
                    throw new InvalidOperationException("ResetDatabase is destructive. Set DestructiveOptions = DestructiveOperationOptions.Allow() to execute it.");
                await ResetDatabaseAsync(cancellationToken).ConfigureAwait(false);
            }

            await EnsureSchemasAsync(Options.Schemas, cancellationToken).ConfigureAwait(false);

            if (Options.EnsureHistoryTables)
                await EnsureHistoryTablesAsync(cancellationToken).ConfigureAwait(false);

            var plans = await CompareRegisteredAsync(cancellationToken).ConfigureAwait(false);
            foreach (var plan in plans.Where(p => p.HasChanges))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ApplyPlanAsync(plan, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Migration script applied: {Category} {Id} {Name}", plan.Definition.Category, plan.Definition.Id, plan.Definition.Name);
            }

            return plans;
        }

        protected virtual async Task ApplyPlanAsync(MigrationSyncPlan plan, CancellationToken cancellationToken)
        {
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

            foreach (var sql in scripts)
            {
                foreach (var batch in SplitBatches(sql))
                {
                    if (!string.IsNullOrWhiteSpace(batch))
                        await ExecuteSqlAsync(batch, cancellationToken).ConfigureAwait(false);
                }
            }

            if (scripts.Count > 0)
                await RecordHistoryAsync(plan.Definition, plan.TargetHash, cancellationToken).ConfigureAwait(false);
        }

        protected static string CreateHistoryKey(MigrationScriptCategory category, string id)
            => $"{category}:{id}";

        protected virtual IReadOnlyList<string> SplitBatches(string sql)
            => SqlBatchSplitter.SingleBatch(sql);

        protected virtual Task<IReadOnlyList<string>> BuildMissingColumnScriptsAsync(MigrationScriptDefinition definition, CancellationToken cancellationToken)
            => Task.FromResult((IReadOnlyList<string>)new List<string>());

        protected abstract Task ResetDatabaseAsync(CancellationToken cancellationToken);
        protected abstract Task EnsureSchemasAsync(IEnumerable<string> schemas, CancellationToken cancellationToken);
        protected abstract Task EnsureHistoryTablesAsync(CancellationToken cancellationToken);
        protected abstract Task<IDictionary<string, string>> ReadHistoryAsync(CancellationToken cancellationToken);
        protected abstract Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken);
        protected abstract Task RecordHistoryAsync(MigrationScriptDefinition definition, string hash, CancellationToken cancellationToken);
    }
}
