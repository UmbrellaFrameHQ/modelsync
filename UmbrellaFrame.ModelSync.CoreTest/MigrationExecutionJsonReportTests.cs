using System;
using System.Text.Json;
using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.CoreTest;

public class MigrationExecutionJsonReportTests
{
    [Test]
    public void Create_ShouldRenderValidJson()
    {
        var result = new MigrationExecutionResult(
            new[]
            {
                new MigrationExecutionItemResult
                {
                    Category = MigrationScriptCategory.StoredProcedures,
                    Name = "dbo.usp_Search.sql",
                    Action = MigrationExecutionAction.Applied,
                    ExecutionMode = MigrationScriptExecutionMode.EveryRun,
                    StartedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                    CompletedAt = DateTimeOffset.Parse("2026-01-01T00:00:01Z"),
                    BatchCount = 1,
                    CompletedBatchCount = 1
                }
            },
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-01-01T00:00:01Z"),
            lockAcquired: true,
            historyWritten: true);

        using var document = JsonDocument.Parse(MigrationExecutionJsonReport.Create(result));
        var root = document.RootElement;

        Assert.That(root.GetProperty("succeeded").GetBoolean(), Is.True);
        Assert.That(root.GetProperty("lockAcquired").GetBoolean(), Is.True);
        Assert.That(root.GetProperty("items").GetArrayLength(), Is.EqualTo(1));
        Assert.That(root.GetProperty("items")[0].GetProperty("name").GetString(), Is.EqualTo("dbo.usp_Search.sql"));
    }

    [Test]
    public void Create_ShouldEscapeStringsAndBoundFailedBatchPreview()
    {
        var longSql = "SELECT '<redacted>'\n" + new string('B', 1200);
        var result = new MigrationExecutionResult(
            new[]
            {
                new MigrationExecutionItemResult
                {
                    Category = MigrationScriptCategory.CustomSql,
                    Name = "999_Fail.sql",
                    Action = MigrationExecutionAction.Failed,
                    ErrorMessage = "Bad \"SQL\"",
                    FailedBatchIndex = 1,
                    FailedBatchPreview = longSql,
                    StartedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                    CompletedAt = DateTimeOffset.Parse("2026-01-01T00:00:01Z"),
                    BatchCount = 1
                }
            },
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-01-01T00:00:01Z"),
            MigrationExecutionState.Failed);

        using var document = JsonDocument.Parse(MigrationExecutionJsonReport.Create(result));
        var item = document.RootElement.GetProperty("items")[0];
        var preview = item.GetProperty("failedBatchPreview").GetString();

        Assert.That(item.GetProperty("errorMessage").GetString(), Is.EqualTo("Bad \"SQL\""));
        Assert.That(preview, Has.Length.LessThanOrEqualTo(1024));
        Assert.That(preview, Does.StartWith("SELECT '<redacted>'"));
        Assert.That(preview, Does.Not.Contain("secret-value"));
    }

    [Test]
    public void Create_ShouldRejectNullResult()
    {
        Assert.Throws<ArgumentNullException>(() => MigrationExecutionJsonReport.Create(null!));
    }

    [Test]
    public void Create_ShouldIncludeRootFailureMetadata()
    {
        var result = new MigrationExecutionResult(
            Array.Empty<MigrationExecutionItemResult>(),
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-01-01T00:00:01Z"),
            MigrationExecutionState.Failed,
            errorCode: "InvalidOperationException",
            errorMessage: "Infrastructure failed password=<redacted>");

        using var document = JsonDocument.Parse(MigrationExecutionJsonReport.Create(result));
        var root = document.RootElement;

        Assert.That(root.GetProperty("errorCode").GetString(), Is.EqualTo("InvalidOperationException"));
        Assert.That(root.GetProperty("errorMessage").GetString(), Does.Contain("password=<redacted>"));
    }
}
