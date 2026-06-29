using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace UmbrellaFrame.ModelSync.Core
{
    /// <summary>Creates database connections for ModelSync. The caller owns and disposes the returned connection.</summary>
    public delegate ValueTask<DbConnection> ModelSyncConnectionFactory(CancellationToken cancellationToken);
}
