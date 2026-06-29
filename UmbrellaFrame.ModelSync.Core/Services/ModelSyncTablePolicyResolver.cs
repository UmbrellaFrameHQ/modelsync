using System;
using System.Linq;

namespace UmbrellaFrame.ModelSync.Core.Services
{
    public sealed class ModelSyncTablePolicyResolver : IModelSyncTablePolicyResolver
    {
        private readonly ModelSyncOptions _options;

        public ModelSyncTablePolicyResolver(ModelSyncOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public ModelSyncTableMode Resolve(ModelTableDefinition table)
        {
            if (table == null)
                throw new ArgumentNullException(nameof(table));

            var typePolicy = _options.TablePolicies.Policies
                .LastOrDefault(p => p.ModelType == table.ModelType);
            if (typePolicy != null)
                return typePolicy.Mode;

            return Resolve(table.Schema, table.Name);
        }

        public ModelSyncTableMode Resolve(string schema, string table)
        {
            var tablePolicy = _options.TablePolicies.Policies.LastOrDefault(p =>
                p.ModelType == null &&
                !p.IsSchemaPolicy &&
                string.Equals(p.Schema ?? string.Empty, schema ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.Table, table, StringComparison.OrdinalIgnoreCase));
            if (tablePolicy != null)
                return tablePolicy.Mode;

            var schemaPolicy = _options.TablePolicies.Policies.LastOrDefault(p =>
                p.ModelType == null &&
                p.IsSchemaPolicy &&
                string.Equals(p.Schema, schema ?? string.Empty, StringComparison.OrdinalIgnoreCase));
            if (schemaPolicy != null)
                return schemaPolicy.Mode;

            return _options.DefaultTableMode;
        }
    }
}
