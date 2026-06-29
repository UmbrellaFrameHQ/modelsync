using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using UmbrellaFrame.ModelSync.Core.SqlGeneration;

namespace UmbrellaFrame.ModelSync.Core.Services
{
    public sealed class ProviderNativeMigrationLockStrategy : IMigrationLockStrategy
    {
        private readonly ModelSyncSqlDialect _dialect;

        private sealed class Handle : IDisposable
        {
            private readonly DbConnection _connection;
            private readonly ModelSyncSqlDialect _dialect;
            private readonly string _resourceName;
            private bool _disposed;

            public Handle(DbConnection connection, ModelSyncSqlDialect dialect, string resourceName)
            {
                _connection = connection;
                _dialect = dialect;
                _resourceName = resourceName;
            }

            public void Dispose()
            {
                if (_disposed)
                    return;
                _disposed = true;

                try
                {
                    var plan = _dialect.BuildReleaseMigrationLockPlan(_resourceName);
                    using (var command = _connection.CreateCommand())
                    {
                        command.CommandText = plan.CommandText;
                        AddParameters(command, plan);
                        command.ExecuteScalar();
                    }
                }
                finally
                {
                    _connection.Dispose();
                }
            }
        }

        public ProviderNativeMigrationLockStrategy(ModelSyncSqlDialect dialect)
        {
            _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
        }

        public async Task<IDisposable> AcquireAsync(DbConnection connection, MigrationLockOptions options, CancellationToken cancellationToken)
        {
            options = options ?? new MigrationLockOptions();
            if (connection == null)
                throw new InvalidOperationException("ProviderNativeLockUnsupported");
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var resourceName = string.IsNullOrWhiteSpace(options.Name) ? "UmbrellaFrame.ModelSync" : options.Name;
            var plan = _dialect.BuildAcquireMigrationLockPlan(resourceName, options.Timeout);
            using (var command = connection.CreateCommand())
            {
                command.CommandText = plan.CommandText;
                AddParameters(command, plan);
                var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (!_dialect.IsSuccessfulLockAcquireResult(value!))
                    throw new TimeoutException("MigrationLockTimeout");
            }

            return new Handle(connection, _dialect, resourceName);
        }

        private static void AddParameters(DbCommand command, ModelSyncSqlCommand plan)
        {
            foreach (var parameter in plan.Parameters)
            {
                var dbParameter = command.CreateParameter();
                dbParameter.ParameterName = parameter.Name;
                dbParameter.Value = parameter.Value ?? DBNull.Value;
                command.Parameters.Add(dbParameter);
            }
        }
    }
}
