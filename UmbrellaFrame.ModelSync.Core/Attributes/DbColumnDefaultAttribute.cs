using System;
using UmbrellaFrame.ModelSync.Core.Resources;

namespace UmbrellaFrame.ModelSync.Core
{
    /// <summary>
    /// Specifies a DEFAULT value for the column.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class DbColumnDefaultAttribute : Attribute
    {
        /// <summary>The raw SQL default value expression (e.g. <c>0</c>, <c>'active'</c>, <c>CURRENT_TIMESTAMP</c>).</summary>
        public string DefaultValue { get; }

        /// <param name="defaultValue">Raw SQL expression to use as the column default.</param>
        public DbColumnDefaultAttribute(string defaultValue)
        {
            if (string.IsNullOrWhiteSpace(defaultValue))
                throw new ArgumentException(CoreResources.Get("DbColumnDefault_NullOrEmpty"), nameof(defaultValue));

            DefaultValue = defaultValue;
        }
    }
}
