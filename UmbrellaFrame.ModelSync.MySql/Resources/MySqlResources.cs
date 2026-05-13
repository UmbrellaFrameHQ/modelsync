using System.Resources;
using System.Reflection;

namespace UmbrellaFrame.ModelSync.MySql.Resources
{
    internal static class MySqlResources
    {
        private static readonly ResourceManager _resourceManager =
            new ResourceManager(
                "UmbrellaFrame.ModelSync.MySql.Resources.Resources",
                typeof(MySqlResources).GetTypeInfo().Assembly);

        internal static string Get(string name, params object[] args)
        {
            var value = _resourceManager.GetString(name) ?? name;
            return args.Length == 0 ? value : string.Format(value, args);
        }
    }
}
