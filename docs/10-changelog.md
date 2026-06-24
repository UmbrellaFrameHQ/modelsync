# 10 - Changelog

All notable changes are documented here.
Format: Keep a Changelog.
Versioning: Semantic Versioning.

---

## [Unreleased]

### Added
- Added SQL Server stored procedure synchronization primitives.
- Added MySQL/MariaDB stored procedure synchronization.
- Added PostgreSQL stored procedure synchronization.
- Added explicit SQLite stored procedure unsupported behavior.
- Added project-file based stored procedure registration via `RegisterProcedureFile`.
- Added dry-run plans for `Create`, `Alter`, and `None` stored procedure changes.
- Added SQL Server stored procedure synchronization docs and examples.
- Added Docker Compose based local test database environment for SQL Server, MySQL, and PostgreSQL.
- Added opt-in stored procedure integration smoke tests for SQL Server, MySQL/MariaDB, and PostgreSQL.

---

## [1.0.5] - SQL Correctness and Repository Hardening

### Added
- Added analyzer unit tests for `MSYNC001`, `MSYNC002`, and `MSYNC003`.
- Added SQL generation coverage for composite primary keys.
- Added SQL Server `IF OBJECT_ID` guard coverage.
- Added comprehensive `docs/` directory.
- Added article drafts for introducing ModelSync.
- Added examples for MySQL, SQL Server, SQLite, and destructive-operation behavior.

### Changed
- Composite primary keys now generate a table-level `PRIMARY KEY (col1, col2)` constraint instead of multiple inline primary-key fragments.
- Strengthened README, NuGet README, and attribute documentation warnings for raw `DbColumnDefault` and `DbColumnCheck` expressions.
- Refocused the repository on ModelSync runtime packages, provider packages, analyzers, documentation, and examples.
- Updated README and documentation to keep database-first scaffolding and Visual Studio tooling as separate-product directions.
- Package versions were bumped to `1.0.5`.

### Fixed
- Fixed SQL Server `IF OBJECT_ID` guards to use validated object names instead of bracket-quoted identifiers inside the string literal.
- Removed a tracked backup file and ignored future `*.Backup.tmp` files.
- Cleaned corrupted separator comments in `ITableGenerator.cs`.

### Removed
- Removed the experimental Visual Studio companion tooling projects, workflow, docs, icon, and tests from the main runtime repository.

---

## [1.0.4] - NuGet Links and Documentation Refresh

### Changed
- NuGet package README content now uses `docs/nuget/README.md`, a pure Markdown file that renders cleanly on NuGet.org.
- GitHub repository, source ZIP, releases, examples, and tutorial links were moved to `UmbrellaFrameHQ/modelsync`.
- Package metadata fields were refreshed: `PackageProjectUrl`, `RepositoryUrl`, release notes, and copyright.
- Package versions were bumped to `1.0.4`.
- GitHub Actions NuGet publish flow now works for `v*` tags.

### Added
- Added `scripts/publish-nuget.ps1`.
- Added download and NuGet links to the documentation index.

---

## [1.0.3] - NuGet README Compatibility

### Changed
- Removed HTML heading content from the package README.
- Updated logo URL to a NuGet-compatible GitHub raw URL.
- Package versions were bumped to `1.0.3`.

---

## [1.0.2] - Security Hardening

### Added
- Added `DestructiveOperationOptions`.
- Added explicit destructive-operation approval for `DropTables`, `DropColumn`, and `AlterColumnType`.
- Added strict identifier validation for table, column, index, and database names.
- Added tests for cached drop SQL using attribute table names.
- Added tests for suspicious identifier rejection.

### Changed
- Moved provider quoting implementations onto `QuoteValidatedIdentifier`; validation is now centralized in the base class.
- Updated README to describe v1.0.2 destructive-operation behavior.
- Package versions were bumped to `1.0.2`.

### Fixed
- Fixed cached drop SQL using model class names instead of attribute table names.
- Aligned drop behavior with cached table-name metadata across MySQL, PostgreSQL, SQL Server, and SQLite providers.

---

## [1.0.0] - Initial Public Release

### Added
- `ITableGenerator` interface for DI support.
- `SqlTableGenerator` abstract base class.
- Provider-specific identifier quoting.
- `IF NOT EXISTS` support for MySQL, PostgreSQL, and SQLite.
- Async APIs: `GenerateSqlTableAsync`, `CreateTablesAsync`, `DropTablesAsync`.
- `GenerateDropTableSql<T>()`.
- `GenerateTruncateTableSql<T>()`.
- `GenerateIndexSql<T>()`.
- `DbColumnDefaultAttribute`.
- `DbColumnCheckAttribute`.
- `DbColumnIndexAttribute`.
- Per-instance thread-safe cache.
- Deterministic property ordering with `MetadataToken`.
- `ILogger` integration with `NullLogger` fallback.
- Roslyn Analyzer rules `MSYNC001`, `MSYNC002`, and `MSYNC003`.
- GitHub Actions CI/CD pipeline.
- NuGet metadata for all library packages.

### Changed
- Target framework changed from `netcoreapp3.1` to `netstandard2.0`.
- Generator methods were renamed to provider aliases such as `GenerateMySqlTable` and `GenerateSqlServerTable`.
- DDL execution moved from reader/scalar calls to non-query execution.

### Fixed
- Fixed SQL cache pollution across providers.
- Guaranteed property order with `MetadataToken`.

---

## [0.1.0] - Internal Prototype

### Added
- Basic MySQL attribute-based CREATE TABLE generation.
- `MySqlTableGenerator`.
- `MySqlColumnTypeAttribute`, `MySqlTableNameAttribute`.
- `DynamicPropertyManager<T>` reflection helper.
