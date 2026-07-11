using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.Oracle
{
    public class OracleColumnNotNullAttribute : DbColumnNotNullAttribute
    {
        public override string GetSqlSnippet() => "NOT NULL";
    }
}
