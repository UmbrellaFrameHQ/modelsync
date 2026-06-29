using MySqlConnector;

namespace UmbrellaFrame.ModelSync.MySql
{
    internal static class MySqlConnectionFactory
    {
        public static MySqlConnection Create(string connectionString)
            => new MySqlConnection(connectionString);
    }
}
