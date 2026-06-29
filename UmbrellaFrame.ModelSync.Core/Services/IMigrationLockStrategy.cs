using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace UmbrellaFrame.ModelSync.Core.Services
{
    public interface IMigrationLockStrategy
    {
        Task<IDisposable> AcquireAsync(DbConnection connection, MigrationLockOptions options, CancellationToken cancellationToken);
    }
}
