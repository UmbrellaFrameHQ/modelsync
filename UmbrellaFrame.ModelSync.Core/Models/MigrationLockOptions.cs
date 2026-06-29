using System;

namespace UmbrellaFrame.ModelSync.Core
{
    public sealed class MigrationLockOptions
    {
        public bool Enabled { get; set; } = true;
        public string Name { get; set; } = "UmbrellaFrame.ModelSync";
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
        public MigrationLockMode Mode { get; set; } = MigrationLockMode.ProviderNative;
    }
}
