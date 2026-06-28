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
        {
            if (assemblies == null || assemblies.Length == 0)
                throw new ArgumentException("At least one assembly is required.", nameof(assemblies));

            return EnsureUniqueTables(assemblies
                .Where(a => a != null)
                .SelectMany(a => a.GetTypes())
                .Where(IsModelType)
                .Select(t => FromType(t, defaultSchema))
                .Where(t => t.Columns.Count > 0)
                .ToList());
        }

        public static IReadOnlyList<ModelTableDefinition> FromAssemblies(
            string defaultSchema,
            Type columnAttributeType,
            Type tableAttributeType,
            params Assembly[] assemblies)
        {
            if (columnAttributeType == null)
                throw new ArgumentNullException(nameof(columnAttributeType));
            if (!typeof(DbColumnTypeAttribute).IsAssignableFrom(columnAttributeType))
                throw new ArgumentException("Column attribute type must derive from DbColumnTypeAttribute.", nameof(columnAttributeType));
            if (tableAttributeType != null && !typeof(DbTableNameAttribute).IsAssignableFrom(tableAttributeType))
                throw new ArgumentException("Table attribute type must derive from DbTableNameAttribute.", nameof(tableAttributeType));
            if (assemblies == null || assemblies.Length == 0)
                throw new ArgumentException("At least one assembly is required.", nameof(assemblies));

            return EnsureUniqueTables(assemblies
                .Where(a => a != null)
                .SelectMany(a => a.GetTypes())
                .Where(t => IsModelType(t, columnAttributeType))
                .Select(t => FromType(t, defaultSchema, columnAttributeType, tableAttributeType))
                .Where(t => t.Columns.Count > 0)
                .ToList());
        }

        public static IReadOnlyList<ModelTableDefinition> FromTypes(string defaultSchema, params Type[] modelTypes)
        {
            if (modelTypes == null || modelTypes.Length == 0)
                throw new ArgumentException("At least one model type is required.", nameof(modelTypes));

            return EnsureUniqueTables(modelTypes
                .Where(t => t != null)
                .Where(IsModelType)
                .Select(t => FromType(t, defaultSchema))
                .Where(t => t.Columns.Count > 0)
                .ToList());
        }

        public static IReadOnlyList<ModelTableDefinition> FromTypes(
            string defaultSchema,
            Type columnAttributeType,
            Type tableAttributeType,
            params Type[] modelTypes)
        {
            if (columnAttributeType == null)
                throw new ArgumentNullException(nameof(columnAttributeType));
            if (!typeof(DbColumnTypeAttribute).IsAssignableFrom(columnAttributeType))
                throw new ArgumentException("Column attribute type must derive from DbColumnTypeAttribute.", nameof(columnAttributeType));
            if (tableAttributeType != null && !typeof(DbTableNameAttribute).IsAssignableFrom(tableAttributeType))
                throw new ArgumentException("Table attribute type must derive from DbTableNameAttribute.", nameof(tableAttributeType));
            if (modelTypes == null || modelTypes.Length == 0)
                throw new ArgumentException("At least one model type is required.", nameof(modelTypes));

            return EnsureUniqueTables(modelTypes
                .Where(t => t != null)
                .Where(t => IsModelType(t, columnAttributeType))
                .Select(t => FromType(t, defaultSchema, columnAttributeType, tableAttributeType))
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

        private static bool IsModelType(Type type, Type columnAttributeType)
        {
            if (!type.IsClass || type.IsAbstract)
                return false;

            return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Any(p => p.GetCustomAttributes(true).Any(columnAttributeType.IsInstanceOfType));
        }

        private static ModelTableDefinition FromType(Type type, string defaultSchema)
            => FromType(type, defaultSchema, typeof(DbColumnTypeAttribute), typeof(DbTableNameAttribute));

        private static ModelTableDefinition FromType(Type type, string defaultSchema, Type columnAttributeType, Type tableAttributeType)
        {
            var tableName = type.GetCustomAttributes(true)
                .OfType<DbTableNameAttribute>()
                .Where(a => tableAttributeType == null || tableAttributeType.IsInstanceOfType(a))
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
                var typeAttribute = attributes
                    .OfType<DbColumnTypeAttribute>()
                    .FirstOrDefault(a => columnAttributeType.IsInstanceOfType(a));
                if (typeAttribute == null)
                    continue;

                var index = attributes.OfType<DbColumnIndexAttribute>().FirstOrDefault();
                var foreignKey = attributes.OfType<DbColumnForeignKeyAttribute>().FirstOrDefault();
                table.Columns.Add(new ModelColumnDefinition
                {
                    Name = property.Name,
                    StoreType = typeAttribute.GetColumnType(),
                    IsPrimaryKey = attributes.OfType<DbColumnPrimaryKeyAttribute>().Any(),
                    IsRequired = attributes.OfType<DbColumnNotNullAttribute>().Any(),
                    IsUnique = attributes.OfType<DbColumnUniqueAttribute>().Any(),
                    IsIndexed = index != null,
                    IndexName = index == null ? string.Empty : index.IndexName,
                    IsUniqueIndex = index != null && index.IsUnique,
                    DefaultSql = attributes.OfType<DbColumnDefaultAttribute>().FirstOrDefault()?.DefaultValue ?? string.Empty,
                    CheckSql = attributes.OfType<DbColumnCheckAttribute>().FirstOrDefault()?.Expression ?? string.Empty,
                    ForeignKeyColumn = foreignKey?.ColumnName ?? string.Empty,
                    ForeignKeyTable = foreignKey?.ReferencedTable ?? string.Empty,
                    ForeignKeyReferenceColumn = foreignKey?.ReferencedColumn ?? string.Empty
                });
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
    }
}
