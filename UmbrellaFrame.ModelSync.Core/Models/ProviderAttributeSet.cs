using System;

namespace UmbrellaFrame.ModelSync.Core
{
    public sealed class ProviderAttributeSet
    {
        public ProviderAttributeSet(
            Type tableNameAttribute,
            Type columnTypeAttribute,
            Type primaryKeyAttribute,
            Type notNullAttribute,
            Type uniqueAttribute,
            Type foreignKeyAttribute,
            Func<DbColumnPrimaryKeyAttribute, ModelColumnDefinition, DbValueGenerationKind>? valueGenerationResolver = null)
        {
            TableNameAttribute = RequireAssignable(tableNameAttribute, typeof(DbTableNameAttribute), nameof(tableNameAttribute));
            ColumnTypeAttribute = RequireAssignable(columnTypeAttribute, typeof(DbColumnTypeAttribute), nameof(columnTypeAttribute));
            PrimaryKeyAttribute = RequireAssignable(primaryKeyAttribute, typeof(DbColumnPrimaryKeyAttribute), nameof(primaryKeyAttribute));
            NotNullAttribute = RequireAssignable(notNullAttribute, typeof(DbColumnNotNullAttribute), nameof(notNullAttribute));
            UniqueAttribute = RequireAssignable(uniqueAttribute, typeof(DbColumnUniqueAttribute), nameof(uniqueAttribute));
            ForeignKeyAttribute = RequireAssignable(foreignKeyAttribute, typeof(DbColumnForeignKeyAttribute), nameof(foreignKeyAttribute));
            ValueGenerationResolver = valueGenerationResolver;
        }

        public Type TableNameAttribute { get; }
        public Type ColumnTypeAttribute { get; }
        public Type PrimaryKeyAttribute { get; }
        public Type NotNullAttribute { get; }
        public Type UniqueAttribute { get; }
        public Type ForeignKeyAttribute { get; }
        public Func<DbColumnPrimaryKeyAttribute, ModelColumnDefinition, DbValueGenerationKind>? ValueGenerationResolver { get; }

        public static ProviderAttributeSet Generic { get; } =
            new ProviderAttributeSet(
                typeof(DbTableNameAttribute),
                typeof(DbColumnTypeAttribute),
                typeof(DbColumnPrimaryKeyAttribute),
                typeof(DbColumnNotNullAttribute),
                typeof(DbColumnUniqueAttribute),
                typeof(DbColumnForeignKeyAttribute));

        private static Type RequireAssignable(Type candidate, Type baseType, string parameterName)
        {
            if (candidate == null)
                throw new ArgumentNullException(parameterName);
            if (!baseType.IsAssignableFrom(candidate))
                throw new ArgumentException($"{parameterName} must derive from {baseType.Name}.", parameterName);
            return candidate;
        }
    }
}
