# ModelSync General Recommendation Roadmap

This document separates current ModelSync capability from the maturity work required before recommending it as a default database migration choice for broad production use.

## Current Position

ModelSync is a strong fit for Dapper, ADO.NET, hand-written SQL, and teams that want schema changes to remain visible in code review. The current 1.4.0-rc.1 line includes:

- provider-specific table DDL generation from attributed C# models,
- live model/database comparison with safe additive apply,
- ordered SQL migration runner with history and hash tracking,
- stored procedure synchronization for supported providers,
- CLI validation, dry-run, apply, and Markdown/JSON reports,
- explicit destructive-operation approval,
- provider-native locking and structured migration execution results,
- one-million-row scale tests and live provider integration tests.

ModelSync is not an ORM and is not a direct clone of FluentMigrator or DbUp. Its strongest category is model-aware schema lifecycle tooling for projects that still want readable SQL and explicit review points.

## Recommended Today

Use ModelSync when:

- you do not want EF Core migrations,
- your data access layer is Dapper, ADO.NET, or hand-written SQL,
- model-to-schema visibility is valuable,
- destructive changes must be explicit and reviewable,
- deployment evidence through dry-run and Markdown/JSON reports matters.

Prefer FluentMigrator or another mature migration framework when:

- your main requirement is a long-established `Up`/`Down` migration model,
- first-class rollback workflow is mandatory,
- organizational policy requires multiple independent maintainers, signed packages, or long production history,
- you need a migration framework already proven by many external teams.

## P0 Maturity Work

| Priority | Work | Current Status |
|---|---|---|
| P0 | Transaction and partial-failure model | 1.4.0-rc.1 has structured states and provider-aware transaction behavior; provider limits remain documented. |
| P0 | Rollback and forward-only workflow | Not implemented as a first-class workflow. Rollback scripts are currently user-managed. |
| P0 | Fault-injection and upgrade testing | Some compatibility and live provider gates exist; process-kill and network-loss gates should be expanded. |
| P0 | Independent production evidence | Not a code feature; requires external adopters, issues, PRs, and case studies. |

## P1 Product Work

| Priority | Work | Notes |
|---|---|---|
| P1 | Deterministic schema diff expansion | Rename hints, type-change plans, nullability transitions, constraint updates, and richer risk reasons. |
| P1 | DB-first scaffold and baseline | Generate schema models, initial migration, and baseline history from an existing database. |
| P1 | Advanced index and relationship modeling | Composite indexes, included columns, filtered/partial indexes, sort direction, expression indexes, cascade options, deferrable constraints, and dependency ordering. |
| P1 | Documentation consistency | Keep version scope, support matrix, release notes, and migration guides aligned for every release. |

## P2 Trust Work

| Priority | Work | Notes |
|---|---|---|
| P2 | Package signing and provenance | Signed packages, reproducible build checks, SBOM, and build provenance. |
| P2 | Governance | Clear support windows, deprecation policy, security response process, and at least two maintainers. |
| P2 | External validation | Independent production users, anonymized migration examples, public issues, and contributor PRs. |

## P3 Provider Work

| Priority | Work | Notes |
|---|---|---|
| P3 | Oracle stable provider | Oracle is published as preview and intentionally has a smaller supported migration surface. |
| P3 | Wider provider version matrix | Test more database major versions beyond the current integration baselines. |

## Honest Recommendation Language

Use this phrasing in comparisons:

> ModelSync is a model-aware schema lifecycle toolkit for Dapper, ADO.NET, and hand-written SQL projects. It is production-oriented where safe schema generation, live comparison, explicit destructive approval, migration reports, and provider-aware execution matter. It is not yet the default recommendation over FluentMigrator for teams that primarily need a long-established `Up`/`Down` migration framework with broad independent production proof.
