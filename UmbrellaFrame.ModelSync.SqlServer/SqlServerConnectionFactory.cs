using Microsoft.Data.SqlClient;

namespace UmbrellaFrame.ModelSync.SqlServer
{
    internal static class SqlServerConnectionFactory
    {
        public static SqlConnection Create(string connectionString)
            => new SqlConnection(connectionString);
    }
}
