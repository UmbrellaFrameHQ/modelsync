using System;

namespace UmbrellaFrame.ModelSync.Core
{
    public sealed class DatabaseReadinessException : Exception
    {
        public DatabaseReadinessException(string provider, string databaseName, int attempts, string failureStage, Exception innerException)
            : base($"Database readiness check failed for provider '{provider}', database '{databaseName}', after {attempts} attempt(s) at stage '{failureStage}'.", innerException)
        {
            Provider = provider ?? string.Empty;
            DatabaseName = databaseName ?? string.Empty;
            Attempts = attempts;
            FailureStage = failureStage ?? string.Empty;
        }

        public string Provider { get; }
        public string DatabaseName { get; }
        public int Attempts { get; }
        public string FailureStage { get; }
    }
}
