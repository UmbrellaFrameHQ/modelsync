# Migration Reporting

ModelSync migration runners expose `RunWithResultAsync()` for structured execution results. The result can be inspected directly or rendered as Markdown/JSON deployment evidence.

```csharp
var result = await runner.RunWithResultAsync(cancellationToken);
var markdown = MigrationExecutionMarkdownReport.Create(result, "Production Migration Report");
var json = MigrationExecutionJsonReport.Create(result);

File.WriteAllText("modelsync-migration-report.md", markdown);
File.WriteAllText("modelsync-migration-report.json", json);
```

Reports include:

- overall state and duration,
- lock, transaction and history flags,
- applied/skipped/failed counts,
- script category, execution mode and batch counts,
- failed batch index,
- bounded failed-batch preview.

Security rules:

- The report does not include connection strings.
- The failed SQL preview is capped at 1024 characters.
- Reports are meant for deployment logs and internal review, not for public bug reports when scripts contain business-sensitive SQL.

Use Markdown for human review and JSON for CI systems or dashboards.

CLI usage:

```bash
modelsync run \
  --provider sqlite \
  --connection-env MODELSYNC_CONNECTION_STRING \
  --scripts ./Database/Scripts \
  --apply \
  --report-md ./artifacts/modelsync-report.md \
  --report-json ./artifacts/modelsync-report.json
```

Set `MODELSYNC_CONNECTION_STRING` through your shell or CI secret store. The inline `--connection` option is retained for compatibility but can expose credentials in process listings. `--apply` is required for mutation; use `--dry-run` for a read-only preview.

Recommended production flow:

1. Run migrations from a deployment job.
2. Save Markdown and JSON reports as build/deployment artifacts.
3. Fail the deployment when `result.Succeeded` is false.
4. Keep reports next to application logs for support and audit review.
