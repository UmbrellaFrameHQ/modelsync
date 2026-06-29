using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace UmbrellaFrame.ModelSync.Core.Services
{
    public interface IDatabaseReadinessStrategy
    {
        Task WaitUntilReadyAsync(DbConnection connection, DatabaseReadinessContext context, CancellationToken cancellationToken);
    }
}
