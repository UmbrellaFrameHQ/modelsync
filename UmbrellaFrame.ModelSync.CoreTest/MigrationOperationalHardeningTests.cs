using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Services;

namespace UmbrellaFrame.ModelSync.CoreTest;

public class MigrationOperationalHardeningTests
{
    [Test]
    public void RunWithResultAsync_WhenResetApprovalMissing_ShouldRejectBeforeReset()
    {
        var runner = new FakeRunner(new MigrationRunnerOptions { ResetDatabase = true });

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunWithResultAsync());

        Assert.That(ex!.Message, Does.Contain("destructive"));
        Assert.That(runner.ResetCalled, Is.False);
    }

    [Test]
    public void RunWithResultAsync_WhenExpectedDatabaseIsEmpty_ShouldRejectReset()
    {
        var runner = new FakeRunner(new MigrationRunnerOptions
        {
            ResetDatabase = true,
            ResetOptions = new DatabaseResetOptions
            {
                Enabled = true,
                Approval = DestructiveOperationOptions.Allow()
            }
        });

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunWithResultAsync());

        Assert.That(ex!.Message, Does.Contain("ExpectedDatabaseName"));
        Assert.That(runner.ResetCalled, Is.False);
    }

    [Test]
    public void RunWithResultAsync_WhenEnvironmentIsNotAllowed_ShouldRejectReset()
    {
        var runner = new FakeRunner(new MigrationRunnerOptions
        {
            ResetDatabase = true,
            ResetOptions = new DatabaseResetOptions
            {
                Enabled = true,
                Approval = DestructiveOperationOptions.Allow(),
                ExpectedDatabaseName = "appdb",
                EnvironmentName = "Production",
                AllowedEnvironments = new[] { "Development" }
            }
        });

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunWithResultAsync());

        Assert.That(ex!.Message, Does.Contain("environment"));
        Assert.That(runner.ResetCalled, Is.False);
    }

    [Test]
    public async Task RunWithResultAsync_WhenResetIsApproved_ShouldCallReset()
    {
        var runner = new FakeRunner(new MigrationRunnerOptions
        {
            ResetDatabase = true,
            ResetOptions = new DatabaseResetOptions
            {
                Enabled = true,
                Approval = DestructiveOperationOptions.Allow(),
                ExpectedDatabaseName = "appdb"
            }
        });

        await runner.RunWithResultAsync();

        Assert.That(runner.ResetCalled, Is.True);
    }

    [Test]
    public void RunWithResultAsync_WhenBackupBeforeResetHasNoPath_ShouldRejectBeforeReset()
    {
        var runner = new FakeRunner(new MigrationRunnerOptions
        {
            ResetDatabase = true,
            ResetOptions = new DatabaseResetOptions
            {
                Enabled = true,
                Approval = DestructiveOperationOptions.Allow(),
                ExpectedDatabaseName = "appdb",
                BackupBeforeReset = true
            }
        });

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunWithResultAsync());

        Assert.That(ex!.Message, Does.Contain("BackupDirectory"));
        Assert.That(runner.ResetCalled, Is.False);
    }

    [Test]
    public async Task RunWithResultAsync_WhenResetAndNativeLockAreEnabled_ShouldResetBeforeLockAndIgnoreReleaseFailure()
    {
        var events = new List<string>();
        var strategy = new ThrowingReleaseLockStrategy(events);
        var runner = new FakeRunner(new MigrationRunnerOptions
        {
            ResetDatabase = true,
            ResetOptions = new DatabaseResetOptions
            {
                Enabled = true,
                Approval = DestructiveOperationOptions.Allow(),
                ExpectedDatabaseName = "appdb"
            },
            LockOptions = new MigrationLockOptions
            {
                Mode = MigrationLockMode.ProviderNative,
                Name = "modelsync-reset-lock-test"
            }
        }, strategy, events);
        runner.RegisterScript(MigrationScriptDefinition.Create("001", "Create", MigrationScriptCategory.CustomSql, "OK;"));

        var result = await runner.RunWithResultAsync();

        Assert.That(result.Succeeded, Is.True);
        Assert.That(runner.ResetCalled, Is.True);
        Assert.That(events.Take(2).ToArray(), Is.EqualTo(new[] { "Reset", "Acquire" }));
        Assert.That(strategy.AcquireCount, Is.EqualTo(1));
        Assert.That(strategy.ReleaseAttemptCount, Is.EqualTo(1));
        Assert.That(runner.RecordedHistoryCount, Is.EqualTo(1));
    }

    [Test]
    public async Task RunWithResultAsync_WhenSecondBatchFails_ShouldNotRecordHistoryAndShouldReturnFailure()
    {
        var runner = new FakeRunner();
        runner.RegisterScript(MigrationScriptDefinition.Create("001", "Broken", MigrationScriptCategory.CustomSql, "OK;FAIL;"));

        var result = await runner.RunWithResultAsync();

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Items[0].Action, Is.EqualTo(MigrationExecutionAction.Failed));
        Assert.That(result.Items[0].BatchCount, Is.EqualTo(2));
        Assert.That(result.Items[0].CompletedBatchCount, Is.EqualTo(1));
        Assert.That(result.Items[0].FailedBatchIndex, Is.EqualTo(2));
        Assert.That(result.Items[0].FailedBatchPreview, Does.Contain("FAIL"));
        Assert.That(result.Items[0].ErrorMessage, Does.Contain("password=<redacted>"));
        Assert.That(result.Items[0].ErrorMessage, Does.Not.Contain("super-secret"));
        Assert.That(runner.RecordedHistoryCount, Is.EqualTo(0));
        Assert.That(result.HistoryWritten, Is.False);
    }

    [Test]
    public async Task RunWithResultAsync_WhenScriptSucceeds_ShouldRecordHistoryAfterExecution()
    {
        var runner = new FakeRunner();
        runner.RegisterScript(MigrationScriptDefinition.Create("001", "Create", MigrationScriptCategory.CustomSql, "OK;"));

        var result = await runner.RunWithResultAsync();

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Items[0].Action, Is.EqualTo(MigrationExecutionAction.Applied));
        Assert.That(result.Items[0].CompletedBatchCount, Is.EqualTo(1));
        Assert.That(runner.RecordedHistoryCount, Is.EqualTo(1));
    }

    [Test]
    public async Task CompareRegisteredAsync_RunOnceExistingChangedHash_ShouldSkipWithDriftDiagnostic()
    {
        var options = MigrationRunnerOptions.Default();
        options.CategoryPolicies.ForCategory(MigrationScriptCategory.Seeds, MigrationScriptExecutionMode.RunOnce);
        var runner = new FakeRunner(options);
        runner.SetHistory(MigrationScriptCategory.Seeds, "001", "old-hash");
        runner.RegisterScript(MigrationScriptDefinition.Create("001", "Seed", MigrationScriptCategory.Seeds, "INSERT INTO Seed VALUES (1);"));

        var plan = (await runner.CompareRegisteredAsync()).Single();

        Assert.That(plan.ChangeType, Is.EqualTo(MigrationChangeType.None));
        Assert.That(plan.ExecutionMode, Is.EqualTo(MigrationScriptExecutionMode.RunOnce));
        Assert.That(plan.DecisionReason, Does.Contain("RunOnceScriptChanged"));
    }

    [Test]
    public async Task CompareRegisteredAsync_HashTrackedChangedHash_ShouldReapply()
    {
        var runner = new FakeRunner();
        runner.SetHistory(MigrationScriptCategory.CustomSql, "001", "old-hash");
        runner.RegisterScript(MigrationScriptDefinition.Create("001", "Custom", MigrationScriptCategory.CustomSql, "SELECT 1;"));

        var plan = (await runner.CompareRegisteredAsync()).Single();

        Assert.That(plan.ChangeType, Is.EqualTo(MigrationChangeType.Reapply));
        Assert.That(plan.ExecutionMode, Is.EqualTo(MigrationScriptExecutionMode.HashTracked));
    }

    [Test]
    public async Task CompareRegisteredAsync_EveryRunSameHash_ShouldReapply()
    {
        var options = MigrationRunnerOptions.Default();
        options.CategoryPolicies.ForCategory(MigrationScriptCategory.StoredProcedures, MigrationScriptExecutionMode.EveryRun);
        var runner = new FakeRunner(options);
        var sql = "CREATE PROCEDURE Test AS SELECT 1;";
        runner.SetHistory(MigrationScriptCategory.StoredProcedures, "001", SqlDefinitionNormalizer.ComputeHash(sql));
        runner.RegisterScript(MigrationScriptDefinition.Create("001", "Proc", MigrationScriptCategory.StoredProcedures, sql));

        var plan = (await runner.CompareRegisteredAsync()).Single();

        Assert.That(plan.ChangeType, Is.EqualTo(MigrationChangeType.Reapply));
        Assert.That(plan.DecisionReason, Does.Contain("ExecutionModeEveryRun"));
    }

    [Test]
    public async Task RunWithResultAsync_LegacyHashMissingForRunOnceSeed_ShouldAdoptHashWithoutExecuting()
    {
        var options = MigrationRunnerOptions.Default();
        options.CategoryPolicies.ForCategory(MigrationScriptCategory.Seeds, MigrationScriptExecutionMode.RunOnce);
        var runner = new FakeRunner(options);
        runner.SetHistory(MigrationScriptCategory.Seeds, "001", string.Empty);
        runner.RegisterScript(MigrationScriptDefinition.Create("001", "Seed", MigrationScriptCategory.Seeds, "INSERT INTO Seed VALUES (1);"));

        var result = await runner.RunWithResultAsync();

        Assert.That(result.Succeeded, Is.True);
        Assert.That(runner.ExecutedSqlCount, Is.EqualTo(0));
        Assert.That(runner.RecordedHistoryCount, Is.EqualTo(1));
        Assert.That(result.Items.Single().LegacyHashAdopted, Is.True);
    }

    [Test]
    public void LegacyResetConfigurationAdapter_WhenFlagFalse_ShouldDisableReset()
    {
        var reset = LegacyResetConfigurationAdapter.Create(false, "Expertis", "Staging", false, "Staging");

        Assert.That(reset.Enabled, Is.False);
        Assert.That(reset.Approval, Is.Null);
        Assert.That(reset.ExpectedDatabaseName, Is.EqualTo("Expertis"));
    }

    [Test]
    public void ApplyCompatibilityProfile_LegacyEmbeddedSql_ShouldSetExpectedCategoryModes()
    {
        var options = MigrationRunnerOptions.Default()
            .ApplyCompatibilityProfile(MigrationCompatibilityProfiles.LegacyEmbeddedSql);

        Assert.That(options.CategoryPolicies.Resolve(MigrationScriptCategory.StoredProcedures), Is.EqualTo(MigrationScriptExecutionMode.EveryRun));
        Assert.That(options.CategoryPolicies.Resolve(MigrationScriptCategory.Triggers), Is.EqualTo(MigrationScriptExecutionMode.EveryRun));
        Assert.That(options.CategoryPolicies.Resolve(MigrationScriptCategory.Seeds), Is.EqualTo(MigrationScriptExecutionMode.RunOnce));
        Assert.That(options.CategoryPolicies.Resolve(MigrationScriptCategory.CustomSql), Is.EqualTo(MigrationScriptExecutionMode.HashTracked));
        Assert.That(options.AppliedCompatibilityProfiles, Does.Contain(MigrationCompatibilityProfiles.LegacyEmbeddedSql));
    }

    [Test]
    public void RunWithResultAsync_WhenTransactionRequiredButUnsupported_ShouldFailFast()
    {
        var runner = new FakeRunner(new MigrationRunnerOptions
        {
            TransactionPolicy = MigrationTransactionPolicy.Required
        });
        runner.RegisterScript(MigrationScriptDefinition.Create("001", "Create", MigrationScriptCategory.CustomSql, "OK;"));

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunWithResultAsync());

        Assert.That(ex!.Message, Does.Contain("TransactionRequiredButUnsupported"));
        Assert.That(runner.RecordedHistoryCount, Is.EqualTo(0));
    }

    [Test]
    public async Task InMemoryMigrationLockStrategy_ShouldBlockSameResourceUntilReleased()
    {
        var strategy = new InMemoryMigrationLockStrategy();
        var options = new MigrationLockOptions
        {
            Name = "modelsync-direct-lock-test",
            Timeout = TimeSpan.FromMilliseconds(50)
        };

        using (await strategy.AcquireAsync(null!, options, CancellationToken.None))
        {
            Assert.ThrowsAsync<TimeoutException>(() => strategy.AcquireAsync(null!, options, CancellationToken.None));
        }

        using var reacquired = await strategy.AcquireAsync(null!, options, CancellationToken.None);
        Assert.That(reacquired, Is.Not.Null);
    }

    [Test]
    public async Task ReadinessStrategy_ShouldRetryUntilProbeSucceeds()
    {
        var attempts = 0;
        var strategy = new DefaultDatabaseReadinessStrategy();
        var context = new DatabaseReadinessContext
        {
            Provider = "Fake",
            DatabaseName = "appdb",
            RetryCount = 3,
            RetryDelay = TimeSpan.FromMilliseconds(1),
            ProbeAsync = _ =>
            {
                attempts++;
                if (attempts < 3)
                    throw new InvalidOperationException("Transient");
                return Task.CompletedTask;
            }
        };

        await strategy.WaitUntilReadyAsync(null!, context, CancellationToken.None);

        Assert.That(attempts, Is.EqualTo(3));
    }

    [Test]
    public void ReadinessStrategy_WhenRetriesExhaust_ShouldPreserveLastException()
    {
        var strategy = new DefaultDatabaseReadinessStrategy();
        var context = new DatabaseReadinessContext
        {
            Provider = "Fake",
            DatabaseName = "appdb",
            RetryCount = 2,
            RetryDelay = TimeSpan.FromMilliseconds(1),
            ProbeAsync = _ => throw new InvalidOperationException("Last")
        };

        var ex = Assert.ThrowsAsync<DatabaseReadinessException>(() =>
            strategy.WaitUntilReadyAsync(null!, context, CancellationToken.None));

        Assert.That(ex!.Attempts, Is.EqualTo(2));
        Assert.That(ex.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    private class FakeRunner : SqlMigrationRunnerBase
    {
        private readonly IDictionary<string, string> _history = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly IList<string>? _events;

        public FakeRunner(MigrationRunnerOptions? options = null, IMigrationLockStrategy? lockStrategy = null, IList<string>? events = null)
            : base(Configure(options ?? MigrationRunnerOptions.Default(), lockStrategy), NullLogger.Instance, lockStrategy!)
        {
            _events = events;
        }

        public bool ResetCalled { get; private set; }
        public int RecordedHistoryCount { get; private set; }
        public int ExecutedSqlCount { get; private set; }

        public void SetHistory(MigrationScriptCategory category, string id, string hash)
            => _history[$"{category}:{id}"] = hash;

        protected override Task ResetDatabaseAsync(CancellationToken cancellationToken)
        {
            ResetCalled = true;
            _events?.Add("Reset");
            return Task.CompletedTask;
        }

        protected override Task EnsureSchemasAsync(IEnumerable<string> schemas, CancellationToken cancellationToken) => Task.CompletedTask;
        protected override Task EnsureHistoryTablesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        protected override Task<IDictionary<string, string>> ReadHistoryAsync(CancellationToken cancellationToken) => Task.FromResult(_history);

        protected override Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken)
        {
            if (sql.Contains("FAIL", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Batch failed. password=super-secret token=abc123");
            ExecutedSqlCount++;
            return Task.CompletedTask;
        }

        protected override IReadOnlyList<string> SplitBatches(string sql)
            => sql.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

        protected override Task RecordHistoryAsync(MigrationScriptDefinition definition, string hash, CancellationToken cancellationToken)
        {
            RecordedHistoryCount++;
            _history[$"{definition.Category}:{definition.Id}"] = hash;
            return Task.CompletedTask;
        }

        protected override Task<DbConnection?> CreateLockConnectionAsync(CancellationToken cancellationToken)
            => Task.FromResult<DbConnection?>(null);

        private static MigrationRunnerOptions Configure(MigrationRunnerOptions options, IMigrationLockStrategy? lockStrategy)
        {
            if (lockStrategy == null)
                options.LockOptions.Mode = MigrationLockMode.InMemory;
            return options;
        }
    }

    private sealed class ThrowingReleaseLockStrategy : IMigrationLockStrategy
    {
        private readonly IList<string> _events;

        public int AcquireCount { get; private set; }
        public int ReleaseAttemptCount { get; private set; }

        public ThrowingReleaseLockStrategy(IList<string> events)
        {
            _events = events;
        }

        public Task<IDisposable> AcquireAsync(DbConnection connection, MigrationLockOptions options, CancellationToken cancellationToken)
        {
            AcquireCount++;
            _events.Add("Acquire");
            return Task.FromResult<IDisposable>(new ThrowingHandle(this));
        }

        private sealed class ThrowingHandle : IDisposable
        {
            private readonly ThrowingReleaseLockStrategy _owner;

            public ThrowingHandle(ThrowingReleaseLockStrategy owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                _owner.ReleaseAttemptCount++;
                throw new InvalidOperationException("Simulated broken lock connection.");
            }
        }
    }

}
