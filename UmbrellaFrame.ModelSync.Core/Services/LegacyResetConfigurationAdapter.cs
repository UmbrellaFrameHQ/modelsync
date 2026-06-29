using System;
namespace UmbrellaFrame.ModelSync.Core
{
    /// <summary>Maps legacy reset flags into safe ModelSync reset options without bypassing safeguards.</summary>
    public static class LegacyResetConfigurationAdapter
    {
        public static DatabaseResetOptions Create(
            bool resetRequested,
            string expectedDatabaseName,
            string environmentName,
            bool destructiveApproval,
            params string[] allowedEnvironments)
        {
            return new DatabaseResetOptions
            {
                Enabled = resetRequested,
                ExpectedDatabaseName = expectedDatabaseName ?? string.Empty,
                EnvironmentName = environmentName,
                AllowedEnvironments = allowedEnvironments ?? Array.Empty<string>(),
                Approval = destructiveApproval ? DestructiveOperationOptions.Allow() : null
            };
        }
    }
}
