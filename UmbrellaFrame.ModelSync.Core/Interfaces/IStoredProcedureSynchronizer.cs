using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UmbrellaFrame.ModelSync.Core.Interfaces
{
    /// <summary>
    /// Synchronizes project-side stored procedure definitions with a live database.
    /// </summary>
    public interface IStoredProcedureSynchronizer
    {
        /// <summary>Registers a stored procedure definition for later comparison or synchronization.</summary>
        void RegisterProcedure(StoredProcedureDefinition definition);

        /// <summary>Registers a stored procedure SQL file for later comparison or synchronization.</summary>
        StoredProcedureDefinition RegisterProcedureFile(string path, string name = null, string schema = "dbo");

        /// <summary>Compares one project-side procedure definition with the live database.</summary>
        Task<StoredProcedureSyncPlan> CompareAsync(
            StoredProcedureDefinition definition,
            CancellationToken cancellationToken = default);

        /// <summary>Compares all registered procedure definitions with the live database.</summary>
        Task<IReadOnlyList<StoredProcedureSyncPlan>> CompareRegisteredAsync(
            CancellationToken cancellationToken = default);

        /// <summary>Applies one synchronization plan to the live database.</summary>
        Task ApplyAsync(
            StoredProcedureSyncPlan plan,
            CancellationToken cancellationToken = default);

        /// <summary>Compares and applies all registered procedures.</summary>
        Task<IReadOnlyList<StoredProcedureSyncPlan>> SyncRegisteredAsync(
            CancellationToken cancellationToken = default);
    }
}
