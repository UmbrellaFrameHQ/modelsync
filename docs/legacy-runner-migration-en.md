# Legacy Runner Migration - English

This guide describes how to move a project that already has embedded SQL migrations into ModelSync without losing legacy history.

Status: released in the 1.2.0 line after the all-provider legacy compatibility gate passed.

## Compatibility Profile

Use `MigrationCompatibilityProfiles.LegacyEmbeddedSql` when an existing runner has separate table, stored procedure, trigger, and seed folders.

The profile maps categories as follows:

| Category | Mode | Reason |
|---|---|---|
| Tables | HashTracked | Table scripts should reapply only when the hash changes. |
| StoredProcedures | EveryRun | Legacy systems often recreate routines every run. |
| Triggers | EveryRun | Trigger definitions should stay in sync with the script body. |
| Seeds | RunOnce | Seed scripts must not duplicate existing data. |
| CustomSql | HashTracked | Custom SQL should run when first added or when changed. |

## Migration Steps

1. Take a database backup.
2. Configure ModelSync with the legacy compatibility profile.
3. Run compare first and verify that it reports required `SqlHash` upgrade/adoption work without mutating the database.
4. Run the first mutation pass in a controlled environment.
5. Verify that legacy history tables received the additive `SqlHash` column.
6. Verify seed duplicate protection: matching legacy seed rows should be adopted, not rerun.
7. Run a second mutation pass and verify idempotency.
8. Verify stored procedure and trigger `EveryRun` behavior.
9. Remove the old runner only after ModelSync history and idempotency are confirmed.
10. Keep only the ModelSync hosted service/startup path.
11. Remove old migration core code and legacy runner registrations.

## Expertis Migration Notes

For Expertis-style SQL Server projects, preserve the existing `sec.SchemaMigration_Tables`, `sec.SchemaMigration_StoredProcedures`, `sec.SchemaMigration_Triggers`, and `sec.SchemaMigration_Seeds` rows. ModelSync should add `SqlHash` without deleting orphan legacy rows. `sec.SchemaMigration_CustomSql` can be bootstrapped by ModelSync when custom SQL scripts are introduced.

## Safety Rules

Compare is read-only. Infrastructure creation, `SqlHash` upgrade, hash adoption, reset, locks, and script execution happen only through mutation APIs.

Safe ALTER rules still apply to model tables. Adding `SqlHash` to framework history tables is an infrastructure upgrade and does not make destructive model changes automatic.
