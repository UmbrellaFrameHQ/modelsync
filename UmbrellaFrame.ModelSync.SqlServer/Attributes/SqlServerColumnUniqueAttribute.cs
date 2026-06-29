
using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.SqlServer
{
    public class SqlServerColumnUniqueAttribute : DbColumnUniqueAttribute
    {
        public SqlServerColumnUniqueAttribute() : base() {  }
        public override string GetSqlSnippet()
        {
            return "UNIQUE";
        }
    }
}
