using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.Oracle
{
    public class OracleColumnUniqueAttribute : DbColumnUniqueAttribute
    {
        public override string GetSqlSnippet() => "UNIQUE";
    }
}
