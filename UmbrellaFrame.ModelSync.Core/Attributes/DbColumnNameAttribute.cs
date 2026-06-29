using System;
using System.Text.RegularExpressions;

namespace UmbrellaFrame.ModelSync.Core
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class DbColumnNameAttribute : Attribute
    {
        private static readonly Regex SafeIdentifierPattern =
            new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        public DbColumnNameAttribute(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName) || !SafeIdentifierPattern.IsMatch(columnName))
                throw new ArgumentException($"Invalid SQL identifier '{columnName}'.", nameof(columnName));

            ColumnName = columnName;
        }

        public string ColumnName { get; }
    }
}
