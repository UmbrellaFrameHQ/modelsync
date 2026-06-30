using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.PostgreSQL
{
    public sealed class PostgresColumnIndexAttribute : DbColumnIndexAttribute
    {
        public PostgresColumnIndexAttribute(string indexName = "", bool isUnique = false)
            : base(indexName, isUnique)
        {
        }
    }
}
