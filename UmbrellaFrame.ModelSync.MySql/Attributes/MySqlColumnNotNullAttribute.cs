
using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.MySql
{
    public class MySqlColumnNotNullAttribute : DbColumnNotNullAttribute
    {
        public MySqlColumnNotNullAttribute() : base() { }

        public override string GetSqlSnippet()
        {
            return "NOT NULL";
        }
    }
}
