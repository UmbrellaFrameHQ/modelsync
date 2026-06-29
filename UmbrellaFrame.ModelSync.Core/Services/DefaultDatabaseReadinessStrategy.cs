using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace UmbrellaFrame.ModelSync.Core.Services
{
    public sealed class DefaultDatabaseReadinessStrategy : IDatabaseReadinessStrategy
    {
        public async Task WaitUntilReadyAsync(DbConnection connection, DatabaseReadinessContext context, CancellationToken cancellationToken)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (connection == null && context.ProbeAsync == null)
                throw new ArgumentNullException(nameof(connection));

            var attempts = Math.Max(1, context.RetryCount);
            Exception? last = null;

            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (context.ProbeAsync != null)
                        await context.ProbeAsync(cancellationToken).ConfigureAwait(false);
                    else if (connection!.State != ConnectionState.Open)
                        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    last = ex;
                    if (attempt == attempts)
                        break;
                    await Task.Delay(context.RetryDelay, cancellationToken).ConfigureAwait(false);
                }
            }

            throw new DatabaseReadinessException(context.Provider, context.DatabaseName, attempts, "OpenConnection", last ?? new InvalidOperationException("Unknown readiness failure."));
        }
    }
}
