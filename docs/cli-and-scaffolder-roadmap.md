# CLI and DB-First Scaffolder Roadmap

ModelSync already owns table DDL, live schema comparison, ordered SQL migrations, history/hash tracking, native migration locks and reset safety. The next product step is to expose those capabilities through focused tooling instead of adding a DbUp dependency.

## CLI Direction

Initial package:

```text
UmbrellaFrame.ModelSync.Cli
```

Initial commands:

```bash
modelsync run
modelsync run --dry-run
modelsync validate
modelsync version
```

The first CLI consumes the existing Core migration APIs instead of introducing a second execution engine. `modelsync validate` checks script folder shape before a database is touched. `modelsync run --dry-run` uses `CompareRegisteredAsync()` to preview work without applying SQL. `modelsync run` executes ordered SQL migration files and can write Markdown/JSON reports by using `RunWithResultAsync()`, `MigrationExecutionMarkdownReport`, and `MigrationExecutionJsonReport`.

Current run shape:

```bash
modelsync validate \
  --scripts ./Database/Scripts

modelsync run \
  --provider sqlserver \
  --connection "<connection-string>" \
  --scripts ./Database/Scripts \
  --dry-run

modelsync run \
  --provider sqlserver \
  --connection "<connection-string>" \
  --scripts ./Database/Scripts \
  --report-md ./artifacts/modelsync-report.md \
  --report-json ./artifacts/modelsync-report.json
```

Candidate later commands:

```bash
modelsync diff
modelsync report
modelsync reset --backup
```

Later commands:

```bash
modelsync script
modelsync scaffold
```

CLI rules:

- no hidden destructive action,
- exact provider selection,
- dry-run first where possible,
- structured JSON output for CI,
- Markdown report output for humans,
- no plain-text secret logging.

## DB-First Scaffolder Direction

Candidate package/project:

```text
UmbrellaFrame.ModelSync.Scaffolder
```

Responsibilities:

- inspect a live database,
- generate C# model classes,
- apply provider-specific ModelSync attributes,
- preserve nullable, primary-key, default, index and foreign-key metadata where possible,
- support one table, one schema, or full database generation.

The Visual Studio extension should be a separate shell over this scaffolder engine. The scaffolder engine should not depend on Visual Studio APIs.

## Why Not Add DbUp Directly?

DbUp is excellent for ordered script execution and journaling, but ModelSync already has its own migration runner, history tables, hash tracking, provider batch handling and live model synchronization. Adding DbUp to Core would create two migration authorities and weaken ModelSync's product identity.

The preferred direction is:

```text
ModelSync Core migration engine
        ->
CLI / scaffolder / IDE extension
```

DbUp compatibility can be considered later as a bridge package, not as a Core dependency.
