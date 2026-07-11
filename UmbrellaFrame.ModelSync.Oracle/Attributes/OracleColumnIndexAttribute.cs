using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.Oracle
{
    public sealed class OracleColumnIndexAttribute : DbColumnIndexAttribute
    {
        public OracleColumnIndexAttribute(string indexName = "", bool isUnique = false)
            : base(indexName, isUnique)
        {
        }
    }
}
