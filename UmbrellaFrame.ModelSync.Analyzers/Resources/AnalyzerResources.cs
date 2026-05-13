using System.Resources;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace UmbrellaFrame.ModelSync.Core.Analyzers.Resources
{
    internal static class AnalyzerResources
    {
        private static readonly ResourceManager _resourceManager =
            new ResourceManager(
                "UmbrellaFrame.ModelSync.Core.Analyzers.Resources.Resources",
                typeof(AnalyzerResources).GetTypeInfo().Assembly);

        /// <summary>
        /// Roslyn DiagnosticDescriptor için LocalizableString döndürür.
        /// IDE, CurrentUICulture'a göre otomatik dil seçer (tr / en).
        /// </summary>
        internal static LocalizableString Get(string name)
            => new LocalizableResourceString(name, _resourceManager, typeof(AnalyzerResources));

        /// <summary>
        /// Runtime exception mesajları için kültüre göre düz string döndürür.
        /// </summary>
        internal static string GetString(string name, params object[] args)
        {
            var value = _resourceManager.GetString(name) ?? name;
            return args.Length == 0 ? value : string.Format(value, args);
        }
    }
}
