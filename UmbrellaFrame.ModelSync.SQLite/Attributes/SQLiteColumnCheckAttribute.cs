using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.SQLite
{
    public sealed class SQLiteColumnCheckAttribute : DbColumnCheckAttribute
    {
        public SQLiteColumnCheckAttribute(string expression)
            : base(expression)
        {
        }
    }
}
