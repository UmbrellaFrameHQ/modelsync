using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace UmbrellaFrame.ModelSync.Core.Services
{
    public sealed class NoOpMigrationLockStrategy : IMigrationLockStrategy
    {
        private sealed class NoOpHandle : IDisposable
        {
            public void Dispose() { }
        }

        public Task<IDisposable> AcquireAsync(DbConnection connection, MigrationLockOptions options, CancellationToken cancellationToken)
            => Task.FromResult<IDisposable>(new NoOpHandle());
    }
}
