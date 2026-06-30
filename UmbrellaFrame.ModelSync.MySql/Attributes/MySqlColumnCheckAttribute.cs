using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.MySql
{
    public sealed class MySqlColumnCheckAttribute : DbColumnCheckAttribute
    {
        public MySqlColumnCheckAttribute(string expression)
            : base(expression)
        {
        }
    }
}
