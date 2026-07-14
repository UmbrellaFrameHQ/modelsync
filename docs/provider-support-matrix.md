# ModelSync Provider Support Matrix

This matrix is intentionally conservative. A feature is listed as supported only when it has package-level API coverage and regression tests in the repository. Preview means the provider exists in source and local package validation, but the public NuGet/package or full legacy migration surface is not yet treated as production-ready.

## Public Package Status

| Provider | Package | Public package status | Notes |
|---|---|---|---|
| Core | `UmbrellaFrame.ModelSync.Core` | Published | Shared contracts and provider-agnostic planning/compiler. |
| SQL Server | `UmbrellaFrame.ModelSync.SqlServer` | Published | Full migration runner, reset, native lock, table sync, stored procedures. |
| MySQL/MariaDB | `UmbrellaFrame.ModelSync.MySql` | Published | Table sync, migration runner, stored procedures, named locks. |
| PostgreSQL | `UmbrellaFrame.ModelSync.PostgreSQL` | Published | Table sync, migration runner, stored procedures, advisory locks. |
| SQLite | `UmbrellaFrame.ModelSync.SQLite` | Published | Table sync, migration runner, file/in-memory test support. Stored procedures unsupported. |
| Oracle | `UmbrellaFrame.ModelSync.Oracle` | Preview, public NuGet pending | Table DDL source and local package validation exist. Public NuGet publication requires package-owner API-key permission for the new package ID. |
| Analyzers | `UmbrellaFrame.ModelSync.Analyzers` | Published | Roslyn validation package. |

## Feature Coverage

| Feature | SQL Server | MySQL/MariaDB | PostgreSQL | SQLite | Oracle Preview |
|---|---|---|---|---|---|
| Attribute table DDL | Supported | Supported | Supported | Supported | Preview |
| Live model compare | Supported | Supported | Supported | Supported | Preview |
| Safe additive apply | Supported | Supported | Supported | Supported with SQLite limits | Preview |
| Explicit destructive opt-in | Supported | Supported | Supported | Supported | Preview |
| Ordered migration runner | Supported | Supported | Supported | Supported | Not production-ready |
| History/hash tracking | Supported | Supported | Supported | Supported | Not production-ready |
| Stored procedure sync | Supported | Supported | Supported | Unsupported by provider | Not supported |
| Trigger scripts | Supported as migration scripts | Supported as migration scripts | Supported as migration scripts | Supported as migration scripts | Not production-ready |
| Seed scripts | Supported | Supported | Supported | Supported | Not production-ready |
| Native migration lock | `sp_getapplock` | `GET_LOCK` | advisory lock | `BEGIN IMMEDIATE`/file lock semantics | Not production-ready |
| DB reset | Supported with explicit approval and optional backup | Supported with explicit approval | Supported with explicit approval | Limited; file-backed reset is destructive | Not production-ready |
| Backup before reset | SQL Server `BACKUP DATABASE` | Not built-in | Not built-in | Not built-in | Not built-in |
| Same-session batch execution | Supported | Supported | Supported | Supported | Not production-ready |

## Production Guidance

- Treat SQL Server, MySQL/MariaDB, PostgreSQL and SQLite as the current production provider set.
- Treat Oracle as preview until its public NuGet package is published and migration-runner coverage reaches the same level as the other providers.
- Prefer deployment-time migration jobs over every application instance running migrations at startup.
- If startup migrations are used, keep native migration locks enabled.
- Use `RunWithResultAsync()` and a persisted report for production deployments.
