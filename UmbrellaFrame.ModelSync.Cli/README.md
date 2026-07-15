# ModelSync CLI

`modelsync` validates migration folders, previews pending scripts, applies migrations through the existing ModelSync runner, and writes Markdown or JSON deployment reports.

## Install

```bash
dotnet tool install --global UmbrellaFrame.ModelSync.Cli --version 1.3.0
modelsync version
```

## Safe Workflow

Keep the connection string in your environment:

```bash
export MODELSYNC_CONNECTION_STRING='Data Source=modelsync-preview.db'
```

Validate file discovery and duplicate IDs:

```bash
modelsync validate --scripts ./Database/Scripts
```

Preview without changing the database:

```bash
modelsync run \
  --provider sqlite \
  --connection-env MODELSYNC_CONNECTION_STRING \
  --scripts ./Database/Scripts \
  --dry-run
```

Apply deliberately and keep reports:

```bash
modelsync run \
  --provider sqlite \
  --connection-env MODELSYNC_CONNECTION_STRING \
  --scripts ./Database/Scripts \
  --apply \
  --report-md ./artifacts/modelsync-report.md \
  --report-json ./artifacts/modelsync-report.json
```

`--apply` is required before SQL is executed. Ctrl+C is forwarded as cancellation.

## Options

| Option | Meaning |
|---|---|
| `--provider` | `sqlserver`, `mysql`, `mariadb`, `postgresql`, or `sqlite` |
| `--scripts` | Root folder containing migration category folders |
| `--connection-env` | Environment variable containing the connection string |
| `--connection` | Inline compatibility option; may be visible in process listings |
| `--dry-run` | Read-only comparison |
| `--apply` | Explicit execution approval |
| `--history-schema` | History schema; defaults to `sec` |
| `--legacy-profile` | Enables `LegacyEmbeddedSql` compatibility |
| `--report-md` | Markdown result path |
| `--report-json` | JSON result path |

If neither connection option is supplied, the CLI reads `MODELSYNC_CONNECTION_STRING`.

`validate` checks folder discovery, script metadata, and duplicate IDs. It does not prove arbitrary SQL safe or execute provider syntax validation. Migration files remain reviewed application artifacts.
