# Changelog

All notable ModelSync changes are tracked here.

## [1.2.1] - 2026-06-30

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