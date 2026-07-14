# Migration Report Example

Use this when you want a human-readable deployment artifact after running ModelSync migrations.

```csharp
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.SqlServer;

var runner = new SqlServerMigrationRunner(new MigrationRunnerOptions
{
    ConnectionString = connectionString,
    HistorySchema = "sec",
    DefaultSchema = "app"
});

runner.RegisterScriptDirectory("Database/Scripts");

var result = await runner.RunWithResultAsync(cancellationToken);
var report = MigrationExecutionMarkdownReport.Create(result, "ModelSync Deployment Report");
var json = MigrationExecutionJsonReport.Create(result);

Directory.CreateDirectory("artifacts");
File.WriteAllText("artifacts/modelsync-migration-report.md", report);
File.WriteAllText("artifacts/modelsync-migration-report.json", json);

if (!result.Succeeded)
{
    throw new InvalidOperationException("ModelSync migration failed. See artifacts/modelsync-migration-report.md.");
}
```

Recommended use:

- store the report as a CI/deployment artifact,
- use JSON when a CI system or dashboard needs structured status,
- include it in release evidence,
- review failed batch index and category before rerunning,
- do not paste reports publicly when SQL scripts contain business-sensitive data.
