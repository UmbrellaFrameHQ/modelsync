using System.Resources;
using System.Reflection;

namespace UmbrellaFrame.ModelSync.PostgreSQL.Resources
{
    internal static class PostgresResources
    {
        private static readonly ResourceManager _resourceManager =
            new ResourceManager(
                "UmbrellaFrame.ModelSync.PostgreSQL.Resources.Resources",
                typeof(PostgresResources).GetTypeInfo().Assembly);

        internal static string Get(string name, params object[] args)
        {
            var value = _resourceManager.GetString(name) ?? name;
            return args.Length == 0 ? value : string.Format(value, args);
        }
    }
}
