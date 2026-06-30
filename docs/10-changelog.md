# 10 - Changelog

All notable changes are documented here.
Format: Keep a Changelog.
Versioning: Semantic Versioning.

---

## [Unreleased]

## [1.2.1] - 2026-07-01 - Provider API Clarity and Operational Hardening

### Added
- Added provider-specific default, raw default SQL, check and index attributes.
- Added structured provider error metadata, failed-batch reporting and same-session script execution.
- Added SQL Server legacy routine normalization for legacy embedded SQL profile.

### Compatibility
- Verified NuGet.org 1.2.0 baseline consumer compatibility and local 1.2.1 package compatibility.
- Existing 1.2.0 source remains compatible with zero new warning delta.

---


## [1.2.0] - 2026-06-29 - Legacy Migration Compatibility

### Added
- Added category execution modes for migration scripts: `RunOnce`, `HashTracked`, and `EveryRun`.
- Added `MigrationRunnerOptions.CategoryPolicies` and the `LegacyEmbeddedSql` compatibility profile.
- Added additive legacy `SqlHash` history upgrade support for existing history tables.
- Added legacy hash adoption so matching legacy history rows can be upgraded without rerunning `RunOnce` scripts.
- Added `LegacyResetConfigurationAdapter` for mapping legacy reset flags into explicit safe reset options.
- Added repository gates for migration execution policy, legacy history compatibility, and all-provider legacy fixture coverage.
- Added all-provider legacy compatibility fixture coverage for SQL Server, MySQL, MariaDB, PostgreSQL, and SQLite.
- Added Expertis-oriented migration guidance for replacing legacy embedded SQL runners with ModelSync.

### Changed
- Stored procedure and trigger categories can be configured as `EveryRun`; seed categories can be configured as `RunOnce`; custom SQL remains hash tracked by default.
- Compare paths remain read-only and report required adoption/upgrade work without writing history state.
- Provider services continue to delegate framework SQL generation to the Core descriptor-driven compiler.

### Release validation
- SQL Server, MySQL, MariaDB, PostgreSQL, and SQLite legacy fixtures passed with no skipped mandatory cases.
- Package versions were bumped to `1.2.0` after the all-provider release gate passed.

---

## [1.1.0] - 2026-06-29 - Operational Hardening and Live Provider Release Gate

### Added
- Added `DbColumnNameAttribute` for explicit column-name mapping in schema models.
- Added `DbIgnoreAttribute` so public helper properties can be excluded from ModelSync schema discovery.
- Added provider-aware model discovery through `ProviderAttributeSet`; each provider synchronizer now reads only its own table, column, key, nullability, uniqueness, and foreign-key attributes.
- Added structured value-generation metadata through `DbValueGenerationKind` so SQL Server identity, MySQL auto-increment, PostgreSQL serial/bigserial, and SQLite rowid primary keys are preserved by model synchronizers.
- Added `IMigrationRunner.EnsureInfrastructureAsync()` for explicit history/schema bootstrap.
- Added `ModelSyncResult.SkippedOperations` and `SkippedByOption` risk classification for safe operations disabled by configuration.
- Added global model-sync plan phases so tables, columns, constraints, indexes, foreign keys, and scripts are emitted in deterministic order.
- Added semantic index and foreign-key metadata containers for live schema introspection.
- Added package smoke validation for packed `.nupkg` files through cross-platform .NET repository tooling.
- Added `DatabaseResetOptions` for explicit reset approval, expected database checks, environment allow-list checks, retry settings, and command timeout metadata.
- Added `DatabaseReadinessContext`, `IDatabaseReadinessStrategy`, `DefaultDatabaseReadinessStrategy`, and `DatabaseReadinessException`.
- Added `ModelSyncConnectionFactory` delegate for future provider factory overloads.
- Added migration lock contracts: `MigrationLockOptions`, `IMigrationLockStrategy`, and a no-op default strategy.
- Added `MigrationTransactionPolicy` and structured migration execution result models.
- Added `RunWithResultAsync()` to migration runners while preserving the existing `RunAsync()` API.
- Added table execution policies for model synchronization: `ModelSyncTableMode`, `DefaultTableMode`, and `TablePolicies.ForType`, `ForTable`, and `ForSchema`.
- Added `AutomaticOperations`, `ManualOperations`, `SkippedOperations`, and `BlockedOperations` result categorization for mixed manual/automatic model sync runs.
- Replaced concrete Core provider dialect classes with a provider-agnostic `ModelSyncSqlDialect` compiler driven by structured provider descriptors.
- Added cross-platform .NET repository checks for provider SQL ownership, scanner self-tests, package smoke validation, version consistency, release documentation consistency, direct provider connection ownership, and shell-script policy enforcement.

### Changed
- `CompareRegisteredAsync()` is now read-only when migration history infrastructure is missing; infrastructure creation happens during `RunAsync()` or explicit `EnsureInfrastructureAsync()`.
- SQL Server and PostgreSQL model synchronizer comparison no longer creates schemas during `CompareAsync()`.
- Model synchronization now blocks missing primary-key, generated-value, unique, foreign-key, and unsafe `NOT NULL` columns on existing tables instead of treating them as automatic safe additions.
- Model synchronization now uses a provider pluggable `IModelSyncOperationRiskEvaluator` for missing-column risk decisions.
- SQLite, SQL Server, MySQL/MariaDB, and PostgreSQL introspection now populate semantic index/foreign-key metadata where provider catalogs expose it.
- SQL Server and MySQL primary-key generation now emits identity/auto-increment before `PRIMARY KEY`, matching provider SQL syntax expectations.
- Duplicate migration script IDs are validated before database access.
- SQL Server, MySQL/MariaDB, and PostgreSQL reset paths now reject known provider system databases and validate expected database names when `DatabaseResetOptions` is configured.
- Existing `RunAsync()` remains backward-compatible and still returns dry-run plans from before execution; `RunWithResultAsync()` returns per-script execution results.
- `ApplyAsync()` on model-sync results now applies only automatic safe operations; manual table-policy operations are reported but never executed automatically.
- Provider-unsupported safe-looking operations without executable SQL are now blocked in automatic scope instead of being silently ignored.
- Provider model synchronizers now delegate DDL, catalog introspection, history planning, parsed-column repair, and stored procedure framework planning to Core compiler services instead of maintaining their own framework SQL builders.
- Provider services now use canonical provider connection factories; direct concrete provider connection creation is rejected outside factory files.
- Provider-native migration locks are wired for SQL Server application locks, MySQL/MariaDB named locks, PostgreSQL advisory locks, and SQLite write-lock behavior through `BEGIN IMMEDIATE`.
- Stored procedure comparison handles MySQL missing procedure error `1305` and normalizes PostgreSQL dollar-quote routine bodies before comparison.
- Live integration coverage was validated for SQL Server, MySQL, MariaDB, PostgreSQL, and SQLite.

### Notes
- Transaction and history hardening is capability-aware. ModelSync does not claim that every provider can make every DDL operation fully transactional.
- Migration locks can be disabled explicitly, but disabled lock mode does not provide distributed safety.
- SQLite does not support stored procedures.
- Compare APIs remain read-only; infrastructure creation, history writes, reset, and DDL execution happen only through explicit mutation APIs.

---

## [1.0.8] - Model Synchronizer and Custom SQL Scripts

### Added
- Added provider model synchronizers for SQL Server, MySQL/MariaDB, PostgreSQL, and SQLite.
- Added live schema introspection and safe model-to-database diff plans.
- Added automatic safe apply for missing tables, additive columns, indexes, and supported constraints.
- Added blocked reporting for destructive/risky operations such as drop, rename, type changes, and nullable-to-not-null changes.
- Added `CustomSql` migration script category and `SchemaMigration_CustomSql` history tracking.
- Added explicit `FromTypes` synchronizer APIs for precise model selection.
- Added complete English and Turkish NuGet usage guides for ModelSync 1.0.8.
- Added a language selector page for the full usage guide.
- Clarified migration runner scope in README, NuGet README, and migration runner documentation.
- Documented why migration history tables are used alongside provider catalog checks.
- Added provider migration runners for SQL Server, MySQL/MariaDB, PostgreSQL, and SQLite.
- Added migration history tables for tables, stored procedures, triggers, and seeds.
- Added ordered table/stored procedure/trigger/seed script execution.
- Added embedded `.sql` resource discovery for migration runners.
- Added migration dry-run plans and script hash tracking.
- Added provider-specific batch execution, including SQL Server `GO` splitting.
- Added optional destructive database reset guarded by `DestructiveOperationOptions.Allow()`.
- Added schema creation for SQL Server and PostgreSQL migration runners.
- Added missing-column repair from changed table scripts.
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
- Added NuGet release helper automation, later replaced by cross-platform .NET repository tooling.
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
