using NUnit.Framework;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Cli;

namespace UmbrellaFrame.ModelSync.CliTest;

[TestFixture]
[NonParallelizable]
public sealed class CliSecurityTests
{
    [Test]
    public void ResolveConnectionString_ReadsNamedEnvironmentVariable()
    {
        const string name = "MODELSYNC_CLI_TEST_CONNECTION";
        const string expected = "Data Source=:memory:";
        Environment.SetEnvironmentVariable(name, expected);

        try
        {
            var options = CliOptions.Parse(new[] { "--connection-env", name });
            Assert.That(Program.ResolveConnectionString(options), Is.EqualTo(expected));
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Test]
    public void ResolveConnectionString_RejectsInlineAndEnvironmentTogether()
    {
        var options = CliOptions.Parse(new[]
        {
            "--connection", "Data Source=:memory:",
            "--connection-env", "MODELSYNC_CONNECTION_STRING"
        });

        Assert.That(
            () => Program.ResolveConnectionString(options),
            Throws.TypeOf<CliUsageException>().With.Message.Contains("either"));
    }

    [Test]
    public void ResolveConnectionString_RejectsInvalidEnvironmentVariableName()
    {
        var options = CliOptions.Parse(new[] { "--connection-env", "BAD-NAME" });
        Assert.That(() => Program.ResolveConnectionString(options), Throws.TypeOf<CliUsageException>());
    }

    [Test]
    public void SafeMessage_RedactsCommonSecrets()
    {
        var message = Program.SafeMessage(new InvalidOperationException(
            "Server=db;Password=super-secret;ApiKey: abc123 Token=token-value"));

        Assert.That(message, Does.Not.Contain("super-secret"));
        Assert.That(message, Does.Not.Contain("abc123"));
        Assert.That(message, Does.Not.Contain("token-value"));
        Assert.That(message, Does.Contain("<redacted>"));
    }

    [Test]
    public async Task RunWithoutApplyOrDryRun_ReturnsUsageErrorBeforeDatabaseAccess()
    {
        var scripts = Path.Combine(Path.GetTempPath(), "modelsync-cli-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(scripts, "Tables"));
        await File.WriteAllTextAsync(Path.Combine(scripts, "Tables", "001_Test.sql"), "CREATE TABLE Test(Id INTEGER);");

        try
        {
            var exitCode = await Program.Main(new[]
            {
                "run",
                "--provider", "sqlite",
                "--connection", "Data Source=:memory:",
                "--scripts", scripts
            });

            Assert.That(exitCode, Is.EqualTo(2));
        }
        finally
        {
            Directory.Delete(scripts, recursive: true);
        }
    }

    [TestCase(MigrationExecutionState.Cancelled, false, 130)]
    [TestCase(MigrationExecutionState.Failed, false, 1)]
    [TestCase(MigrationExecutionState.LockTimeout, false, 1)]
    [TestCase(MigrationExecutionState.Committed, true, 0)]
    [TestCase(MigrationExecutionState.CompletedWithoutTransaction, true, 0)]
    public void MapExecutionResultToExitCode_UsesDocumentedProcessCodes(
        MigrationExecutionState state,
        bool successfulItem,
        int expected)
    {
        var items = successfulItem
            ? new[] { new MigrationExecutionItemResult { Action = MigrationExecutionAction.Applied } }
            : Array.Empty<MigrationExecutionItemResult>();
        var result = new MigrationExecutionResult(items, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, state);

        Assert.That(Program.MapExecutionResultToExitCode(result), Is.EqualTo(expected));
    }
}
