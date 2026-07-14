using System;
using System.Collections.Generic;
using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.CoreTest;

public class MigrationExecutionMarkdownReportTests
{
    [Test]
    public void Create_ShouldRenderSummaryAndItemCounts()
    {
        var result = new MigrationExecutionResult(
            new[]
            {
                new MigrationExecutionItemResult
                {
                    Category = MigrationScriptCategory.Tables,
                    Name = "001_CreateProducts.sql",
                    Action = MigrationExecutionAction.Applied,
                    ExecutionMode = MigrationScriptExecutionMode.HashTracked,
                    StartedAt = DateTimeOffset.UtcNow,
                    CompletedAt = DateTimeOffset.UtcNow.AddMilliseconds(42),
                    BatchCount = 2,
                    CompletedBatchCount = 2
                },
                new MigrationExecutionItemResult
                {
                    Category = MigrationScriptCategory.Seeds,
                    Name = "010_SeedProducts.sql",
                    Action = MigrationExecutionAction.Skipped,
                    ExecutionMode = MigrationScriptExecutionMode.RunOnce,
                    StartedAt = DateTimeOffset.UtcNow,
                    CompletedAt = DateTimeOffset.UtcNow.AddMilliseconds(3),
                    BatchCount = 1,
                    CompletedBatchCount = 0
                }
            },
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMilliseconds(50),
            lockAcquired: true,
            historyWritten: true);

        var report = MigrationExecutionMarkdownReport.Create(result);

        Assert.That(report, Does.Contain("# ModelSync Migration Report"));
        Assert.That(report, Does.Contain("| Applied | 1 |"));
        Assert.That(report, Does.Contain("| Skipped | 1 |"));
        Assert.That(report, Does.Contain("001_CreateProducts.sql"));
        Assert.That(report, Does.Contain("2/2"));
    }

    [Test]
    public void Create_ShouldIncludeBoundedFailurePreview()
    {
        var longSql = new string('A', 1200);
        var result = new MigrationExecutionResult(
            new[]
            {
                new MigrationExecutionItemResult
                {
                    Category = MigrationScriptCategory.CustomSql,
                    Name = "999_Fail.sql",
                    Action = MigrationExecutionAction.Failed,
                    ErrorMessage = "Syntax error",
                    ProviderErrorCode = "42000",
                    FailedBatchIndex = 2,
                    FailedBatchPreview = longSql,
                    BatchCount = 3,
                    CompletedBatchCount = 1,
                    StartedAt = DateTimeOffset.UtcNow,
                    CompletedAt = DateTimeOffset.UtcNow.AddMilliseconds(9)
                }
            },
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMilliseconds(10),
            MigrationExecutionState.Failed);

        var report = MigrationExecutionMarkdownReport.Create(result);

        Assert.That(report, Does.Contain("## Failures"));
        Assert.That(report, Does.Contain("- Failed batch: 2"));
        Assert.That(report, Does.Contain("Syntax error"));
        Assert.That(report, Does.Not.Contain(new string('A', 1100)));
    }

    [Test]
    public void Create_ShouldRejectNullResult()
    {
        Assert.Throws<ArgumentNullException>(() => MigrationExecutionMarkdownReport.Create(null!));
    }
}
