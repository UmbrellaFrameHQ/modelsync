using MySqlConnector;

namespace UmbrellaFrame.ModelSync.MySql
{
    internal static class MySqlConnectionFactory
    {
        public static MySqlConnection Create(string connectionString)
        {
            var builder = new MySqlConnectionStringBuilder(connectionString)
            {
                AllowUserVariables = true
            };
            return new MySqlConnection(builder.ConnectionString);
        }
    }
}
