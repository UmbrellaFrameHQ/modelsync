using System.Resources;
using System.Reflection;

namespace UmbrellaFrame.ModelSync.SqlServer.Resources
{
    internal static class SqlServerResources
    {
        private static readonly ResourceManager _resourceManager =
            new ResourceManager(
                "UmbrellaFrame.ModelSync.SqlServer.Resources.Resources",
                typeof(SqlServerResources).GetTypeInfo().Assembly);

        internal static string Get(string name, params object[] args)
        {
            var value = _resourceManager.GetString(name) ?? name;
            return args.Length == 0 ? value : string.Format(value, args);
        }
    }
}
