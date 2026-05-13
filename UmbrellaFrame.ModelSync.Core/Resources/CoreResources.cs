using System.Resources;
using System.Reflection;

namespace UmbrellaFrame.ModelSync.Core.Resources
{
    internal static class CoreResources
    {
        private static readonly ResourceManager _resourceManager =
            new ResourceManager(
                "UmbrellaFrame.ModelSync.Core.Resources.Resources",
                typeof(CoreResources).GetTypeInfo().Assembly);

        /// <summary>
        /// Returns a culture-aware string for the current thread's UI culture (tr / en).
        /// </summary>
        internal static string Get(string name, params object[] args)
        {
            var value = _resourceManager.GetString(name) ?? name;
            return args.Length == 0 ? value : string.Format(value, args);
        }
    }
}
