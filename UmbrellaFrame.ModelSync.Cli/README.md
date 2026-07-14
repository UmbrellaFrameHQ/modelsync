# ModelSync CLI

`UmbrellaFrame.ModelSync.Cli` provides the `modelsync` command-line tool.

## Install

```bash
dotnet tool install --global UmbrellaFrame.ModelSync.Cli --version 1.3.0
```

## Commands

```bash
modelsync version
```

Run ordered migration scripts and write deployment reports:

```bash
modelsync run \
  --provider sqlserver \
  --connection "<connection-string>" \
  --scripts ./Database/Scripts \
  --report-md ./artifacts/modelsync-report.md \
  --report-json ./artifacts/modelsync-report.json
```

Supported providers:

- `sqlserver`
- `mysql`
- `mariadb`
- `postgresql`
- `sqlite`

The CLI uses the existing ModelSync migration runner engine. It does not introduce a second migration engine.
