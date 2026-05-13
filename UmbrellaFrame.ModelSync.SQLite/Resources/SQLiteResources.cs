using System.Resources;
using System.Reflection;

namespace UmbrellaFrame.ModelSync.SQLite.Resources
{
    internal static class SQLiteResources
    {
        private static readonly ResourceManager _resourceManager =
            new ResourceManager(
                "UmbrellaFrame.ModelSync.SQLite.Resources.Resources",
                typeof(SQLiteResources).GetTypeInfo().Assembly);

        internal static string Get(string name, params object[] args)
        {
            var value = _resourceManager.GetString(name) ?? name;
            return args.Length == 0 ? value : string.Format(value, args);
        }
    }
}
