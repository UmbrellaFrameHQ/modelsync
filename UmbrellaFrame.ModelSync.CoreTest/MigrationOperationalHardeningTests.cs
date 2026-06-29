using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
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
    public async Task RunWithResultAsync_WhenSecondBatchFails_ShouldNotRecordHistoryAndShouldReturnFailure()
    {
        var runner = new FakeRunner();
        runner.RegisterScript(MigrationScriptDefinition.Create("001", "Broken", MigrationScriptCategory.CustomSql, "OK;FAIL;"));

        var result = await runner.RunWithResultAsync();

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Items[0].Action, Is.EqualTo(MigrationExecutionAction.Failed));
        Assert.That(result.Items[0].BatchCount, Is.EqualTo(2));
        Assert.That(result.Items[0].CompletedBatchCount, Is.EqualTo(1));
        Assert.That(runner.RecordedHistoryCount, Is.EqualTo(0));
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

        public FakeRunner(MigrationRunnerOptions? options = null)
            : base(Configure(options ?? MigrationRunnerOptions.Default()))
        {
        }

        public bool ResetCalled { get; private set; }
        public int RecordedHistoryCount { get; private set; }

        protected override Task ResetDatabaseAsync(CancellationToken cancellationToken)
        {
            ResetCalled = true;
            return Task.CompletedTask;
        }

        protected override Task EnsureSchemasAsync(IEnumerable<string> schemas, CancellationToken cancellationToken) => Task.CompletedTask;
        protected override Task EnsureHistoryTablesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        protected override Task<IDictionary<string, string>> ReadHistoryAsync(CancellationToken cancellationToken) => Task.FromResult(_history);

        protected override Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken)
        {
            if (sql.Contains("FAIL", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Batch failed.");
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

        private static MigrationRunnerOptions Configure(MigrationRunnerOptions options)
        {
            options.LockOptions.Mode = MigrationLockMode.InMemory;
            return options;
        }
    }

}
