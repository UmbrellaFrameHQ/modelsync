using System;
using System.Collections.Generic;
using System.Linq;

namespace UmbrellaFrame.ModelSync.Core
{
    public sealed class ModelSyncTablePolicyCollection
    {
        private readonly List<ModelSyncTablePolicy> _policies = new List<ModelSyncTablePolicy>();

        public ModelSyncTablePolicyCollection ForType<TModel>(ModelSyncTableMode mode)
            => ForType(typeof(TModel), mode);

        public ModelSyncTablePolicyCollection ForType(Type modelType, ModelSyncTableMode mode)
        {
            if (modelType == null)
                throw new ArgumentNullException(nameof(modelType));
            Add(ModelSyncTablePolicy.ForType(modelType, mode));
            return this;
        }

        public ModelSyncTablePolicyCollection ForTable(string schema, string table, ModelSyncTableMode mode)
        {
            Add(ModelSyncTablePolicy.ForTable(schema, table, mode));
            return this;
        }

        public ModelSyncTablePolicyCollection ForSchema(string schema, ModelSyncTableMode mode)
        {
            Add(ModelSyncTablePolicy.ForSchema(schema, mode));
            return this;
        }

        public IReadOnlyList<ModelSyncTablePolicy> Policies => _policies;

        private void Add(ModelSyncTablePolicy policy)
        {
            var conflict = _policies.FirstOrDefault(p => p.HasSameSpecificityKey(policy) && p.Mode != policy.Mode);
            if (conflict != null)
                throw new InvalidOperationException("Conflicting ModelSync table policies were configured for the same scope.");
            _policies.Add(policy);
        }
    }
}
