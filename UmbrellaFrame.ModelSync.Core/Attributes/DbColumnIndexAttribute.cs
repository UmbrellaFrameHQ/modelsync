using System;

namespace UmbrellaFrame.ModelSync.Core
{
    /// <summary>
    /// Marks a property for index creation via <c>GenerateIndexSql&lt;T&gt;()</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class DbColumnIndexAttribute : Attribute
    {
        /// <summary>Optional explicit index name. When empty a name is auto-generated as <c>idx_{table}_{column}</c>.</summary>
        public string IndexName { get; }

        /// <summary>When <c>true</c> a UNIQUE index is created.</summary>
        public bool IsUnique { get; }

        /// <param name="indexName">Optional index name override.</param>
        /// <param name="isUnique">Set to <c>true</c> to create a UNIQUE index.</param>
        public DbColumnIndexAttribute(string indexName = "", bool isUnique = false)
        {
            IndexName = indexName;
            IsUnique = isUnique;
        }
    }
}
