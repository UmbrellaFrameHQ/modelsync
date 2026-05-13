using System;
using UmbrellaFrame.ModelSync.Core.Resources;

namespace UmbrellaFrame.ModelSync.Core
{
    /// <summary>
    /// Specifies a CHECK constraint expression for the column.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class DbColumnCheckAttribute : Attribute
    {
        /// <summary>The raw SQL CHECK expression (e.g. <c>Price &gt; 0</c>).</summary>
        public string Expression { get; }

        /// <param name="expression">The SQL expression to enforce as a CHECK constraint.</param>
        public DbColumnCheckAttribute(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                throw new ArgumentException(CoreResources.Get("DbColumnCheck_NullOrEmpty"), nameof(expression));

            Expression = expression;
        }
    }
}
