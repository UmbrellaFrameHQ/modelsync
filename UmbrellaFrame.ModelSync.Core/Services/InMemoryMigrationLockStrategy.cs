using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace UmbrellaFrame.ModelSync.Core.Services
{
    public sealed class InMemoryMigrationLockStrategy : IMigrationLockStrategy
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.Ordinal);

        private sealed class Handle : IDisposable
        {
            private readonly string _name;
            private readonly SemaphoreSlim _semaphore;
            private bool _disposed;

            public Handle(string name, SemaphoreSlim semaphore)
            {
                _name = name;
                _semaphore = semaphore;
            }

            public void Dispose()
            {
                if (_disposed)
                    return;
                _disposed = true;
                _semaphore.Release();
                if (_semaphore.CurrentCount > 0)
                    Locks.TryRemove(_name, out _);
            }
        }

        public async Task<IDisposable> AcquireAsync(DbConnection connection, MigrationLockOptions options, CancellationToken cancellationToken)
        {
            if (options == null || !options.Enabled)
                return new NoOpHandle();

            var name = string.IsNullOrWhiteSpace(options.Name) ? "UmbrellaFrame.ModelSync" : options.Name;
            var semaphore = Locks.GetOrAdd(name, _ => new SemaphoreSlim(1, 1));
            var acquired = await semaphore.WaitAsync(options.Timeout, cancellationToken).ConfigureAwait(false);
            if (!acquired)
                throw new TimeoutException("Migration lock could not be acquired within the configured timeout.");
            return new Handle(name, semaphore);
        }

        private sealed class NoOpHandle : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
