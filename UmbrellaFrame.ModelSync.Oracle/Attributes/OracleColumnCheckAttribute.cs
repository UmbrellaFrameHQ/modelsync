using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.Oracle
{
    public sealed class OracleColumnCheckAttribute : DbColumnCheckAttribute
    {
        public OracleColumnCheckAttribute(string expression)
            : base(expression)
        {
        }
    }
}
