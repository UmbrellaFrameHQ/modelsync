# Changelog

All notable ModelSync changes are tracked here.

## [1.3.0] - 2026-07-14

CLI, Dry-Run and Migration Reporting.

### Added

- `MigrationExecutionMarkdownReport` renders `RunWithResultAsync()` output as a Markdown deployment report.
- `MigrationExecutionJsonReport` renders `RunWithResultAsync()` output as machine-readable JSON without adding a new runtime dependency.
- `UmbrellaFrame.ModelSync.Cli` adds the `modelsync` .NET tool with `version`, `validate`, and `run --dry-run` commands.
- Provider support matrix, migration reporting guidance, and CLI/DB-first scaffolder roadmap documentation.
- Migration report, CLI quickstart, and GitHub Actions examples for storing Markdown and JSON deployment artifacts.
- Performance smoke coverage for rendering large migration execution reports.
- CLI support for environment-variable connection strings, explicit `--apply`, Ctrl+C cancellation, and secret redaction.
- Direct CLI security and argument tests.
- Analyzer rules `MSYNC004`-`MSYNC008` for mapping conflicts, invalid column names, generated values, and provider primary-key conflicts.
- Opt-in live scale coverage for one million rows on SQL Server, MySQL, MariaDB, PostgreSQL, SQLite, and Oracle.

### Changed

- `UmbrellaFrame.ModelSync.Oracle` is published on NuGet as a preview provider; publication status does not expand its intentionally limited migration surface.
- 1.3.0 positions ModelSync as a schema lifecycle toolkit: CLI validation, dry-run previews, migration execution, reporting, provider support clarity, and future scaffolder foundation.
- Full EN/TR guides, provider/API references, NuGet README, and CLI examples now match the 1.3.0 behavior.
- NuGet publication and partial-recovery gates validate the complete eight-package set, including CLI and Oracle.
- Repository builds treat warnings as errors; production projects no longer suppress nullable warnings.

### Fixed

- MySQL 8 check-constraint introspection resolves table ownership through `table_constraints`; it no longer reads a non-existent `TABLE_NAME` field from `check_constraints`.
- Schema comparison treats PostgreSQL `TIMESTAMP WITHOUT TIME ZONE` and Oracle's default `TIMESTAMP(6)` as equivalent to an unspecified `TIMESTAMP`, while explicit precision differences remain blocked.

### Tests

- Added a six-provider, one-million-row synchronization matrix covering live compare, safe column/index changes, idempotency, destructive-change blocking, and migration history where supported.
- Added a weekly and manually triggered scale workflow without adding the expensive matrix to routine pull-request CI.

## [1.2.3] - 2026-07-12

SQL Server DBReset and native migration lock fix.

### Fixed

- SQL Server migration runner now performs destructive database reset before acquiring the provider-native migration lock.
- Native lock release failures are logged without hiding the migration result.
- SQL Server reset can optionally create a backup before dropping the target database with `DatabaseResetOptions.BackupBeforeReset`.

### Tests

- Added unit coverage for reset-before-lock ordering, safe lock release failure handling, and backup path validation.
- Added SQL Server integration coverage for reset + schema/history creation + SQL migration + seed execution followed by an idempotent second run.

## [1.2.2] - 2026-07-01

Integration Workflow Reliability and Release Gate Correction.

### Fixed

- MySQL CI service now creates the configured target database.
- MariaDB CI service now creates the configured target database.
- Stored procedure integration test is enabled in the required release workflow.
- Integration release gates now fail on unexpected skipped tests.
- Database images are pinned for reproducible CI.
- Target database readiness is validated before tests begin.

### Compatibility

- No ModelSync runtime breaking change.
- Existing 1.2.0 and 1.2.1-compatible source remains compatible.
- All six packages remain synchronized at version 1.2.2.

### Unpublished 1.2.1 tag

Version 1.2.1 was tagged but was not published to NuGet because the required
MySQL/MariaDB integration gate did not pass. Version 1.2.2 supersedes the
unpublished 1.2.1 tag.

## [1.2.1] - 2026-07-01

Provider API Clarity and Operational Hardening

### Added

- Provider-specific default attributes for SQL Server, MySQL/MariaDB, PostgreSQL and SQLite.
- Provider-specific raw default SQL attributes.
- Provider-specific check attributes.
- Provider-specific index attributes.
- Typed provider default expression enums.
- Structured provider error metadata on migration execution results.
- Per-script execution scope so all batches of one script share the same provider session.
- Failed batch reporting with 1-based batch index and bounded SQL preview.
- Consumer compatibility verification against NuGet.org 1.2.0 and local candidate packages.
- Release documentation contract checks.

### Changed

- Canonical API examples now use provider-specific attributes.
- Core/provider responsibility boundary is repository-enforced.
- SQL execution batches preserve the same provider session.
- SQL Server legacy routine normalization is provider-owned.
- Release gates now verify external NuGet consumer compatibility.

### Fixed

- Provider-specific defaults no longer require ambiguous Core-first examples.
- Actual database exception information was previously lost.
- Failed migration batch was not visible.
- SQL Server GO batches could execute in different sessions.
- Terminal GO and legacy deployment batches could prevent routine execution.
- Release changes and migration instructions were not consistently documented.

### Deprecated

- `DbColumnDefault`, `DbColumnCheck` and `DbColumnIndex` continue to work as 1.x compatibility APIs.
- New canonical code should use provider-specific attributes.
- No public API removal will happen before a major version.
- This release does not introduce compiler or analyzer deprecation warnings for these APIs.

### Removed

None.

### Security

- Public error output redacts secrets.
- Failed SQL batch preview is bounded.
- Connection strings and credentials are suppressed from public execution results.

### Compatibility

- Existing 1.2.0 consumer source compiles.
- New warning delta is zero.
- `TreatWarningsAsErrors` passes.
- Compatibility proof uses NuGet packages only and no `ProjectReference`.

## [1.2.0]

See [docs/10-changelog.md](docs/10-changelog.md) for the full 1.2.0 release notes.
