# ModelSync CLI Quickstart

This example shows the first safe workflow for the `modelsync` command-line tool:

1. Validate migration script files.
2. Preview the migration plan with `--dry-run`.
3. Apply migrations.
4. Save Markdown and JSON reports as deployment artifacts.

## Install

```bash
dotnet tool install --global UmbrellaFrame.ModelSync.Cli --version 1.3.0
```

## Validate Scripts

```bash
modelsync validate --scripts ./Database/Scripts
```

Expected output:

```text
Validation: PASS
Scripts: 1
Tables: 1
```

## Dry Run

```bash
modelsync run \
  --provider sqlite \
  --connection "Data Source=cli-quickstart.db" \
  --scripts ./Database/Scripts \
  --dry-run
```

`--dry-run` compares registered scripts through the migration runner and does not apply SQL.

## Apply and Write Reports

```bash
modelsync run \
  --provider sqlite \
  --connection "Data Source=cli-quickstart.db" \
  --scripts ./Database/Scripts \
  --report-md ./artifacts/modelsync-report.md \
  --report-json ./artifacts/modelsync-report.json
```

Keep the generated reports with your CI or deployment artifacts. The JSON report is useful for automation; the Markdown report is easier to review in pull requests and release notes.

