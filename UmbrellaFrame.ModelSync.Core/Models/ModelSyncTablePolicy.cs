using System;

namespace UmbrellaFrame.ModelSync.Core
{
    public sealed class ModelSyncTablePolicy
    {
        private ModelSyncTablePolicy() { }

        public Type? ModelType { get; private set; }
        public string Schema { get; private set; } = string.Empty;
        public string Table { get; private set; } = string.Empty;
        public bool IsSchemaPolicy { get; private set; }
        public ModelSyncTableMode Mode { get; private set; }

        public static ModelSyncTablePolicy ForType(Type modelType, ModelSyncTableMode mode)
            => new ModelSyncTablePolicy { ModelType = modelType ?? throw new ArgumentNullException(nameof(modelType)), Mode = mode };

        public static ModelSyncTablePolicy ForTable(string schema, string table, ModelSyncTableMode mode)
            => new ModelSyncTablePolicy
            {
                Schema = schema ?? string.Empty,
                Table = string.IsNullOrWhiteSpace(table) ? throw new ArgumentException("Table name cannot be empty.", nameof(table)) : table,
                Mode = mode
            };

        public static ModelSyncTablePolicy ForSchema(string schema, ModelSyncTableMode mode)
            => new ModelSyncTablePolicy
            {
                Schema = string.IsNullOrWhiteSpace(schema) ? throw new ArgumentException("Schema cannot be empty.", nameof(schema)) : schema,
                IsSchemaPolicy = true,
                Mode = mode
            };

        public bool HasSameSpecificityKey(ModelSyncTablePolicy other)
        {
            if (other == null)
                return false;
            if (ModelType != null || other.ModelType != null)
                return ModelType == other.ModelType;
            if (IsSchemaPolicy || other.IsSchemaPolicy)
                return IsSchemaPolicy == other.IsSchemaPolicy &&
                       string.Equals(Schema, other.Schema, StringComparison.OrdinalIgnoreCase);
            return string.Equals(Schema, other.Schema, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(Table, other.Table, StringComparison.OrdinalIgnoreCase);
        }
    }
}
