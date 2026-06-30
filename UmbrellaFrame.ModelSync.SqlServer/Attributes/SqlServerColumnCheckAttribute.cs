using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.SqlServer
{
    public sealed class SqlServerColumnCheckAttribute : DbColumnCheckAttribute
    {
        public SqlServerColumnCheckAttribute(string expression)
            : base(expression)
        {
        }
    }
}
