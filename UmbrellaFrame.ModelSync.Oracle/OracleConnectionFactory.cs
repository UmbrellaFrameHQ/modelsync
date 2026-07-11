using Oracle.ManagedDataAccess.Client;

namespace UmbrellaFrame.ModelSync.Oracle
{
    internal static class OracleConnectionFactory
    {
        public static OracleConnection Create(string connectionString)
            => new OracleConnection(connectionString);
    }
}
