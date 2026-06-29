using System;
using System.Text.RegularExpressions;

namespace UmbrellaFrame.ModelSync.Core.SqlGeneration
{
    public static class SqlIdentifierValidator
    {
        private static readonly Regex SafeIdentifierPattern = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        public static void Validate(string identifier, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(identifier) || !SafeIdentifierPattern.IsMatch(identifier))
                throw new ArgumentException("Invalid SQL identifier '" + identifier + "'.", parameterName);
        }
    }
}
