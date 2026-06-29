
using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.SqlServer
{
    public class SqlServerColumnNotNullAttribute : DbColumnNotNullAttribute
    {
        public SqlServerColumnNotNullAttribute() : base() { }

        public override string GetSqlSnippet()
        {
            return "NOT NULL";
        }
    }
}
