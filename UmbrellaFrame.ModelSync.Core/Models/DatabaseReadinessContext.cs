using System;

namespace UmbrellaFrame.ModelSync.Core
{
    public sealed class DatabaseReadinessContext
    {
        public string Provider { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public int RetryCount { get; set; } = 20;
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);
        public Func<System.Threading.CancellationToken, System.Threading.Tasks.Task>? ProbeAsync { get; set; }
    }
}
