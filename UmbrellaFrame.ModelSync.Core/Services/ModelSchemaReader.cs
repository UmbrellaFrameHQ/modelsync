using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.Core.Services
{
    public static class ModelSchemaReader
    {
        public static IReadOnlyList<ModelTableDefinition> FromAssemblies(string defaultSchema, params Assembly[] assemblies)
            => FromAssemblies(defaultSchema, ProviderAttributeSet.Generic, assemblies);

        public static IReadOnlyList<ModelTableDefinition> FromAssemblies(
            string defaultSchema,
            Type columnAttributeType,
            Type tableAttributeType,
            params Assembly[] assemblies)
            => FromAssemblies(
                defaultSchema,
                new ProviderAttributeSet(
                    tableAttributeType,
                    columnAttributeType,
                    typeof(DbColumnPrimaryKeyAttribute),
                    typeof(DbColumnNotNullAttribute),
                    typeof(DbColumnUniqueAttribute),
                    typeof(DbColumnForeignKeyAttribute)),
                assemblies);

        public static IReadOnlyList<ModelTableDefinition> FromAssemblies(
            string defaultSchema,
            ProviderAttributeSet providerAttributes,
            params Assembly[] assemblies)
        {
            if (providerAttributes == null)
                throw new ArgumentNullException(nameof(providerAttributes));
            if (assemblies == null || assemblies.Length == 0)
                throw new ArgumentException("At least one assembly is required.", nameof(assemblies));

            return EnsureUniqueTables(assemblies
                .Where(a => a != null)
                .SelectMany(a => a.GetTypes())
                .Where(t => IsModelType(t, providerAttributes))
                .Select(t => FromType(t, defaultSchema, providerAttributes))
                .Where(t => t.Columns.Count > 0)
                .ToList());
        }

        public static IReadOnlyList<ModelTableDefinition> FromTypes(string defaultSchema, params Type[] modelTypes)
            => FromTypes(defaultSchema, ProviderAttributeSet.Generic, modelTypes);

        public static IReadOnlyList<ModelTableDefinition> FromTypes(
            string defaultSchema,
            Type columnAttributeType,
            Type tableAttributeType,
            params Type[] modelTypes)
            => FromTypes(
                defaultSchema,
                new ProviderAttributeSet(
                    tableAttributeType,
                    columnAttributeType,
                    typeof(DbColumnPrimaryKeyAttribute),
                    typeof(DbColumnNotNullAttribute),
                    typeof(DbColumnUniqueAttribute),
                    typeof(DbColumnForeignKeyAttribute)),
                modelTypes);

        public static IReadOnlyList<ModelTableDefinition> FromTypes(
            string defaultSchema,
            ProviderAttributeSet providerAttributes,
            params Type[] modelTypes)
        {
            if (providerAttributes == null)
                throw new ArgumentNullException(nameof(providerAttributes));
            if (modelTypes == null || modelTypes.Length == 0)
                throw new ArgumentException("At least one model type is required.", nameof(modelTypes));

            return EnsureUniqueTables(modelTypes
                .Where(t => t != null)
                .Where(t => IsModelType(t, providerAttributes))
                .Select(t => FromType(t, defaultSchema, providerAttributes))
                .Where(t => t.Columns.Count > 0)
                .ToList());
        }

        private static bool IsModelType(Type type)
        {
            if (!type.IsClass || type.IsAbstract)
                return false;

            return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Any(p => p.GetCustomAttributes(true).OfType<DbColumnTypeAttribute>().Any());
        }

        private static bool IsModelType(Type type, ProviderAttributeSet providerAttributes)
        {
            if (!type.IsClass || type.IsAbstract)
                return false;

            return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Any(p => !p.GetCustomAttributes(true).OfType<DbIgnoreAttribute>().Any() &&
                          p.GetCustomAttributes(true).Any(providerAttributes.ColumnTypeAttribute.IsInstanceOfType));
        }

        private static ModelTableDefinition FromType(Type type, string defaultSchema)
            => FromType(type, defaultSchema, ProviderAttributeSet.Generic);

        private static ModelTableDefinition FromType(Type type, string defaultSchema, ProviderAttributeSet providerAttributes)
        {
            var tableName = type.GetCustomAttributes(true)
                .OfType<DbTableNameAttribute>()
                .Where(a => providerAttributes.TableNameAttribute.IsInstanceOfType(a))
                .FirstOrDefault()?.TableName ?? type.Name;

            var table = new ModelTableDefinition
            {
                ModelType = type,
                Schema = string.IsNullOrWhiteSpace(defaultSchema) ? string.Empty : defaultSchema,
                Name = tableName
            };

            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var attributes = property.GetCustomAttributes(true);
                if (attributes.OfType<DbIgnoreAttribute>().Any())
                    continue;

                var typeAttribute = attributes
                    .OfType<DbColumnTypeAttribute>()
                    .FirstOrDefault(a => providerAttributes.ColumnTypeAttribute.IsInstanceOfType(a));
                if (typeAttribute == null)
                    continue;

                var index = attributes.OfType<DbColumnIndexAttribute>().FirstOrDefault();
                var foreignKey = attributes.OfType<DbColumnForeignKeyAttribute>().FirstOrDefault(a => providerAttributes.ForeignKeyAttribute.IsInstanceOfType(a));
                var primaryKey = attributes.OfType<DbColumnPrimaryKeyAttribute>().FirstOrDefault(a => providerAttributes.PrimaryKeyAttribute.IsInstanceOfType(a));
                var columnName = attributes.OfType<DbColumnNameAttribute>().FirstOrDefault()?.ColumnName ?? property.Name;
                var column = new ModelColumnDefinition
                {
                    Name = columnName,
                    StoreType = typeAttribute.GetColumnType(),
                    IsPrimaryKey = primaryKey != null,
                    IsRequired = attributes.OfType<DbColumnNotNullAttribute>().Any(a => providerAttributes.NotNullAttribute.IsInstanceOfType(a)),
                    IsUnique = attributes.OfType<DbColumnUniqueAttribute>().Any(a => providerAttributes.UniqueAttribute.IsInstanceOfType(a)),
                    IsIndexed = index != null,
                    IndexName = index == null ? string.Empty : index.IndexName,
                    IsUniqueIndex = index != null && index.IsUnique,
                    DefaultSql = attributes.OfType<DbColumnDefaultAttribute>().FirstOrDefault()?.DefaultValue ?? string.Empty,
                    CheckSql = attributes.OfType<DbColumnCheckAttribute>().FirstOrDefault()?.Expression ?? string.Empty,
                    ForeignKeyColumn = foreignKey?.ColumnName ?? string.Empty,
                    ForeignKeyTable = foreignKey?.ReferencedTable ?? string.Empty,
                    ForeignKeyReferenceColumn = foreignKey?.ReferencedColumn ?? string.Empty
                };

                if (primaryKey != null && providerAttributes.ValueGenerationResolver != null)
                    column.ValueGeneration = providerAttributes.ValueGenerationResolver(primaryKey, column);
                SetPrimaryKeySnippet(column, primaryKey);

                table.Columns.Add(column);
            }

            return table;
        }

        private static IReadOnlyList<ModelTableDefinition> EnsureUniqueTables(IReadOnlyList<ModelTableDefinition> tables)
        {
            var duplicate = tables
                .GroupBy(t => $"{t.Schema}.{t.Name}", StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicate != null)
            {
                var modelNames = string.Join(", ", duplicate.Select(t => t.ModelType.FullName));
                throw new InvalidOperationException(
                    $"Multiple model types map to the same table '{duplicate.Key}': {modelNames}.");
            }

            return tables;
        }

        private static void SetPrimaryKeySnippet(ModelColumnDefinition column, DbColumnPrimaryKeyAttribute? primaryKey)
        {
#pragma warning disable CS0618
            column.PrimaryKeySqlSnippet = primaryKey?.GetSqlSnippet() ?? string.Empty;
#pragma warning restore CS0618
        }
    }
}
