
using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.MySql
{
    public class MySqlColumnUniqueAttribute : DbColumnUniqueAttribute
    {
        public MySqlColumnUniqueAttribute() : base() { }
        public override string GetSqlSnippet()
        {
            return "UNIQUE";
        }
    }
}
