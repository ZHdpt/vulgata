# Project Lessons

## Define callable public seams before behavioral TDD

Applies when a story introduces a production capability that tests, adapters or Agent functions must invoke through a new public boundary. Story 18.1 reached implementation with the intended issuer and Agent tool behaviors accepted, but their callable constructor/factory signatures were not explicit; the test owner correctly stopped twice instead of inventing APIs.

Before implementation approval, verify that every new public seam names its construction inputs, callable method, return/result shape, ownership or disposal rule, cancellation behavior and permitted argument surface. Treat a missing callable shape as a plan-owned definition defect, correct it through the retained author and independent review, and preserve existing approval only when the correction demonstrably leaves semantic scope unchanged. Last verified by Story 18.1 on 2026-09-28.

## Pin PostgreSQL migration-history schema in isolated fixtures

Applies when a test constructs `VulgataDbContext` directly against PostgreSQL and migrates a disposable database across multiple contexts or target migrations. A fixture that relies on provider-default migration-history placement can create the domain schema successfully, then fail on its next context by replaying `InitialVulgataDomain` and colliding with an existing table.

Configure Npgsql test contexts with `MigrationsHistoryTable("__EFMigrationsHistory", "public")`, matching production and current fixture composition. Prove the correction against a newly created owned database and the complete affected migration-test class; do not attribute `relation already exists` to retained database contamination until the unique-database lifecycle reproduces it. Last verified during Story 18.1 final verification on 2026-09-29.
