using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.MySql
{
    public sealed class MySqlColumnIndexAttribute : DbColumnIndexAttribute
    {
        public MySqlColumnIndexAttribute(string indexName = "", bool isUnique = false)
            : base(indexName, isUnique)
        {
        }
    }
}
