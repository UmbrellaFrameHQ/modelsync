using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.SqlServer
{
    public sealed class SqlServerColumnIndexAttribute : DbColumnIndexAttribute
    {
        public SqlServerColumnIndexAttribute(string indexName = "", bool isUnique = false)
            : base(indexName, isUnique)
        {
        }
    }
}
