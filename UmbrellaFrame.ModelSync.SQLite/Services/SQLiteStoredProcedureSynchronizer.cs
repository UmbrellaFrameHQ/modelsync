using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Interfaces;

namespace UmbrellaFrame.ModelSync.SQLite
{
    /// <summary>
    /// SQLite does not support stored procedures.
    /// </summary>
    public sealed class SQLiteStoredProcedureSynchronizer : IStoredProcedureSynchronizer
    {
        /// <inheritdoc/>
        public void RegisterProcedure(StoredProcedureDefinition definition)
            => throw CreateNotSupported();

        /// <inheritdoc/>
        public StoredProcedureDefinition RegisterProcedureFile(string path, string name = null, string schema = "dbo")
            => throw CreateNotSupported();

        /// <inheritdoc/>
        public Task<StoredProcedureSyncPlan> CompareAsync(StoredProcedureDefinition definition, CancellationToken cancellationToken = default)
            => throw CreateNotSupported();

        /// <inheritdoc/>
        public Task<IReadOnlyList<StoredProcedureSyncPlan>> CompareRegisteredAsync(CancellationToken cancellationToken = default)
            => throw CreateNotSupported();

        /// <inheritdoc/>
        public Task ApplyAsync(StoredProcedureSyncPlan plan, CancellationToken cancellationToken = default)
            => throw CreateNotSupported();

        /// <inheritdoc/>
        public Task<IReadOnlyList<StoredProcedureSyncPlan>> SyncRegisteredAsync(CancellationToken cancellationToken = default)
            => throw CreateNotSupported();

        private static NotSupportedException CreateNotSupported()
            => new NotSupportedException("SQLite does not support stored procedures.");
    }
}
