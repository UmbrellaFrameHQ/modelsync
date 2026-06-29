using Npgsql;

namespace UmbrellaFrame.ModelSync.PostgreSQL
{
    internal static class PostgresConnectionFactory
    {
        public static NpgsqlConnection Create(string connectionString)
            => new NpgsqlConnection(connectionString);
    }
}
