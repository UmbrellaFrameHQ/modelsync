using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.PostgreSQL
{
    public sealed class PostgresColumnCheckAttribute : DbColumnCheckAttribute
    {
        public PostgresColumnCheckAttribute(string expression)
            : base(expression)
        {
        }
    }
}
