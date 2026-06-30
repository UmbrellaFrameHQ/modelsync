using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.SQLite
{
    public sealed class SQLiteColumnIndexAttribute : DbColumnIndexAttribute
    {
        public SQLiteColumnIndexAttribute(string indexName = "", bool isUnique = false)
            : base(indexName, isUnique)
        {
        }
    }
}
