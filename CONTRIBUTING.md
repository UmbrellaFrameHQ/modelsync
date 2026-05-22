# Contributing to ModelSync

Thank you for considering contributing to ModelSync! This document explains how to get involved.

---

## Getting Started

### Prerequisites

| Tool | Minimum Version |
|---|---|
| .NET SDK | 8.0 |
| Git | 2.x |
| Visual Studio 2022 / VS Code / Rider | Any recent version |

### Clone & Build

```bash
git clone https://github.com/UmbrellaFrameHQ/modelsync.git
cd modelsync
dotnet restore
dotnet build
dotnet test --filter "Category!=Integration"
```

---

## Project Structure

```
ModelSync/
├── UmbrellaFrame.ModelSync.Core/           # Abstract base (netstandard2.0)
├── UmbrellaFrame.ModelSync.MySql/          # MySQL provider (netstandard2.0)
├── UmbrellaFrame.ModelSync.SqlServer/      # SQL Server provider (netstandard2.0)
├── UmbrellaFrame.ModelSync.PostgreSQL/     # PostgreSQL provider (netstandard2.0)
├── UmbrellaFrame.ModelSync.SQLite/         # SQLite provider (netstandard2.0)
├── UmbrellaFrame.ModelSync.Analyzers/      # Roslyn Analyzer (netstandard2.0)
├── UmbrellaFrame.ModelSync.*Test/          # Unit tests (net8.0)
└── docs/                                   # Documentation
```

---

## How to Contribute

### Reporting Bugs

1. Search existing [Issues](https://github.com/UmbrellaFrameHQ/modelsync/issues) first.
2. If not found, open a new issue using the **Bug Report** template.
3. Include the provider, .NET version, a minimal repro, and the expected vs actual SQL output.

### Suggesting Features

1. Open a [Discussion](https://github.com/UmbrellaFrameHQ/modelsync/discussions) to propose the idea.
2. Once agreed upon, open an Issue referencing the discussion.

### Submitting a Pull Request

1. Fork the repository and create a feature branch:
   ```bash
   git checkout -b feature/my-feature
   ```
2. Write tests **first** (TDD encouraged).
3. Implement the change.
4. Ensure `dotnet build` has **0 errors, 0 new warnings**.
5. Run `dotnet test --filter "Category!=Integration"` — all must pass.
6. Update `docs/` if the public API changed.
7. Update `docs/10-changelog.md`.
8. Open a PR against `main`.

---

## PR Checklist

- [ ] All unit tests pass (`dotnet test --filter "Category!=Integration"`)
- [ ] New code has unit tests
- [ ] `netstandard2.0` compatible (no `await using`, no `IAsyncEnumerable`, etc.)
- [ ] XML documentation on all new public members
- [ ] `docs/` updated if public API changed
- [ ] `docs/10-changelog.md` updated
- [ ] No hardcoded connection strings

---

## Coding Standards

- **Naming:** PascalCase for types/methods/properties, `_camelCase` for private fields
- **Attributes:** `{Provider}{Purpose}Attribute` pattern (e.g. `MySqlColumnTypeAttribute`)
- **Namespaces:** `UmbrellaFrame.ModelSync.{Provider}[.{Subfolder}]`
- **XML docs:** Required on all public members
- **No `await using`** — not supported in `netstandard2.0`

---

## Adding a New Database Provider

1. Create a new project: `UmbrellaFrame.ModelSync.{Provider}` targeting `netstandard2.0`
2. Add project reference to `UmbrellaFrame.ModelSync.Core`
3. Implement provider-specific attributes (inherit from Core base attributes)
4. Implement `{Provider}TableGenerator : SqlTableGenerator, ITableGenerator`
   - Override `QuoteIdentifier(string identifier)`
   - Override `IfNotExistsClause` if needed
   - Implement `CreateTables`, `CreateTablesAsync`, `DropTables`, `DropTablesAsync`
5. Create a test project `UmbrellaFrame.ModelSync.{Provider}Test` targeting `net8.0`
6. Update `docs/04-providers.md` and `docs/index.md`

---

## Running Integration Tests

Integration tests require a running database. Use Docker:

```bash
# MySQL
docker run -d -p 3306:3306 -e MYSQL_ROOT_PASSWORD=123 -e MYSQL_DATABASE=ModelSyncTest mysql:8

# SQL Server
docker run -d -p 1433:1433 -e SA_PASSWORD=Secret!123 -e ACCEPT_EULA=Y mcr.microsoft.com/mssql/server:2022-latest

# PostgreSQL
docker run -d -p 5432:5432 -e POSTGRES_PASSWORD=123 -e POSTGRES_DB=ModelSyncTest1 postgres:16

# Then run integration tests
dotnet test --filter "Category=Integration"
```
