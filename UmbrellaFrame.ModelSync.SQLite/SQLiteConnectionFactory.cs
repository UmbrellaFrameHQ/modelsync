using Microsoft.Data.Sqlite;

namespace UmbrellaFrame.ModelSync.SQLite
{
    internal static class SQLiteConnectionFactory
    {
        public static SqliteConnection Create(string connectionString)
            => new SqliteConnection(connectionString);
    }
}
