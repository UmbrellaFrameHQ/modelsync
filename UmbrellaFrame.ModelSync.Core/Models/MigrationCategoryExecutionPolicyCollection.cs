using System.Collections.Generic;

namespace UmbrellaFrame.ModelSync.Core
{
    /// <summary>Category-specific execution policies for migration scripts.</summary>
    public sealed class MigrationCategoryExecutionPolicyCollection
    {
        private readonly Dictionary<MigrationScriptCategory, MigrationScriptExecutionMode> _modes =
            new Dictionary<MigrationScriptCategory, MigrationScriptExecutionMode>();

        public MigrationCategoryExecutionPolicyCollection ForCategory(
            MigrationScriptCategory category,
            MigrationScriptExecutionMode mode)
        {
            _modes[category] = mode;
            return this;
        }

        public MigrationScriptExecutionMode Resolve(
            MigrationScriptCategory category,
            MigrationScriptExecutionMode fallback = MigrationScriptExecutionMode.HashTracked)
            => _modes.TryGetValue(category, out var mode) ? mode : fallback;

        public IReadOnlyDictionary<MigrationScriptCategory, MigrationScriptExecutionMode> Items => _modes;
    }
}
