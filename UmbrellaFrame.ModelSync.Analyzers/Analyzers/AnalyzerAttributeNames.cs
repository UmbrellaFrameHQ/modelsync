namespace UmbrellaFrame.ModelSync.Core.Analyzers
{
    internal static class AnalyzerAttributeNames
    {
        public static bool IsTableNameAttribute(string attributeName)
        {
            return attributeName.EndsWith("TableNameAttribute") ||
                   attributeName.EndsWith("TableName");
        }
    }
}
