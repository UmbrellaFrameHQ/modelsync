using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.CoreTest
{
    [TestFixture]
    public sealed class MigrationExecutionReportPerformanceTests
    {
        [Test]
        public void ReportRendering_PerformanceSmoke_ShouldRenderLargeExecutionResultQuickly()
        {
            var startedAt = DateTimeOffset.UtcNow;
            var items = new List<MigrationExecutionItemResult>();

            for (var index = 0; index < 1000; index++)
            {
                items.Add(new MigrationExecutionItemResult
                {
                    Category = MigrationScriptCategory.CustomSql,
                    ScriptId = index.ToString("0000"),
                    Name = "Script " + index,
                    Source = "Database/Scripts/CustomSql/" + index.ToString("0000") + "_Script.sql",
                    Action = MigrationExecutionAction.Applied,
                    ExistingHash = string.Empty,
                    TargetHash = "hash-" + index,
                    StartedAt = startedAt.AddMilliseconds(index),
                    CompletedAt = startedAt.AddMilliseconds(index + 1),
                    BatchCount = 3,
                    CompletedBatchCount = 3,
                    ExecutionMode = MigrationScriptExecutionMode.HashTracked,
                    DecisionReason = "Performance smoke test item"
                });
            }

            var result = new MigrationExecutionResult(
                items,
                startedAt,
                startedAt.AddSeconds(2),
                MigrationExecutionState.Committed,
                MigrationAtomicityLevel.Full,
                lockAcquired: true,
                transactionStarted: true,
                historyWritten: true);

            var stopwatch = Stopwatch.StartNew();
            var markdown = MigrationExecutionMarkdownReport.Create(result);
            var json = MigrationExecutionJsonReport.Create(result);
            stopwatch.Stop();

            Assert.That(markdown, Does.Contain("Script 999"));
            Assert.That(json, Does.Contain("\"scriptId\": \"0999\""));
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(2000));
        }
    }
}
