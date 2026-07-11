using System;
using System.Collections.Generic;

namespace UmbrellaFrame.ModelSync.Core
{
    /// <summary>Structured safety options for destructive database reset operations.</summary>
    public sealed class DatabaseResetOptions
    {
        public bool Enabled { get; set; }
        public DestructiveOperationOptions? Approval { get; set; }
        public string ExpectedDatabaseName { get; set; } = string.Empty;
        public string? EnvironmentName { get; set; }
        public IReadOnlyCollection<string> AllowedEnvironments { get; set; } = Array.Empty<string>();
        public int ReadinessRetryCount { get; set; } = 20;
        public TimeSpan ReadinessRetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);
        public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public bool BackupBeforeReset { get; set; }
        public string? BackupFilePath { get; set; }
        public string? BackupDirectory { get; set; }
        public string? BackupFileName { get; set; }
    }
}
