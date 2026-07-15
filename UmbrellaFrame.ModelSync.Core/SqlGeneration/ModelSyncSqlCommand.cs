using System.Collections.Generic;
using System.Linq;

namespace UmbrellaFrame.ModelSync.Core.SqlGeneration
{
    public sealed class ModelSyncSqlCommand
    {
        public ModelSyncSqlCommand(
            string commandText,
            ModelSyncSqlPurpose purpose,
            IEnumerable<ModelSyncSqlParameter>? parameters = null,
            bool supportsTransaction = true,
            bool isDestructive = false,
            bool requiresAdministrativeConnection = false)
        {
            CommandText = commandText ?? string.Empty;
            Purpose = purpose;
            Parameters = (parameters ?? Enumerable.Empty<ModelSyncSqlParameter>()).ToList();
            SupportsTransaction = supportsTransaction;
            IsDestructive = isDestructive;
            RequiresAdministrativeConnection = requiresAdministrativeConnection;
        }

        public string CommandText { get; }

        public IReadOnlyList<ModelSyncSqlParameter> Parameters { get; }

        public ModelSyncSqlPurpose Purpose { get; }

        public bool SupportsTransaction { get; }

        public bool IsDestructive { get; }

        public bool RequiresAdministrativeConnection { get; }
    }
}
