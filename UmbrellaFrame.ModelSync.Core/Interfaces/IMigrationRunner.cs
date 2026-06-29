using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace UmbrellaFrame.ModelSync.Core.Interfaces
{
    /// <summary>Runs ordered SQL migration scripts and records migration history.</summary>
    public interface IMigrationRunner
    {
        void RegisterScript(MigrationScriptDefinition definition);
        MigrationScriptDefinition RegisterScriptFile(string path, MigrationScriptCategory? category = null, string id = null, string name = null);
        IReadOnlyList<MigrationScriptDefinition> RegisterEmbeddedScripts(Assembly assembly, params string[] prefixes);
        Task EnsureInfrastructureAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MigrationSyncPlan>> CompareRegisteredAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MigrationSyncPlan>> RunAsync(CancellationToken cancellationToken = default);
        Task<MigrationExecutionResult> RunWithResultAsync(CancellationToken cancellationToken = default);
    }
}
