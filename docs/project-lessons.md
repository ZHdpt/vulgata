# Project Lessons

## Define callable public seams before behavioral TDD

Applies when a story introduces a production capability that tests, adapters or Agent functions must invoke through a new public boundary. Story 18.1 reached implementation with the intended issuer and Agent tool behaviors accepted, but their callable constructor/factory signatures were not explicit; the test owner correctly stopped twice instead of inventing APIs.

Before implementation approval, verify that every new public seam names its construction inputs, callable method, return/result shape, ownership or disposal rule, cancellation behavior and permitted argument surface. Treat a missing callable shape as a plan-owned definition defect, correct it through the retained author and independent review, and preserve existing approval only when the correction demonstrably leaves semantic scope unchanged. Last verified by Story 18.1 on 2026-09-28.

## Pin PostgreSQL migration-history schema in isolated fixtures

Applies when a test constructs `VulgataDbContext` directly against PostgreSQL and migrates a disposable database across multiple contexts or target migrations. A fixture that relies on provider-default migration-history placement can create the domain schema successfully, then fail on its next context by replaying `InitialVulgataDomain` and colliding with an existing table.

Configure Npgsql test contexts with `MigrationsHistoryTable("__EFMigrationsHistory", "public")`, matching production and current fixture composition. Prove the correction against a newly created owned database and the complete affected migration-test class; do not attribute `relation already exists` to retained database contamination until the unique-database lifecycle reproduces it. Last verified during Story 18.1 final verification on 2026-09-29.

## Canonicalize hash-bound resources and serialize shared database pressure

Applies when tests compare an embedded resource's exact fingerprint across platforms or when many isolated PostgreSQL fixtures share one local server during a large parallel suite. Identical logical JSON can compile to different resource bytes when checkout EOL policy is unspecified, and GUID database ownership prevents collisions but does not prevent connection, migration or cleanup pressure.

Pin every hash-bound source and embedded copy to one EOL policy in `.gitattributes`, derive the reviewed fingerprint from those canonical bytes, and verify both copies remain identical. For database tests that are individually green but time out only in the parallel full suite, preserve their unique database names and cleanup assertions while placing the expensive classes in an explicit disabled-parallelization collection. Diagnose with serial fresh-database runs before changing timeouts or product code. Last verified during Story 18.1 final verification on 2026-09-29.

## Match native filesystem test permissions across launcher boundaries

Applies when Windows tests create an owned temporary tree successfully but a native handle-relative traversal later reports `RootUnavailable`, especially when the repository launcher starts a child process under a filesystem sandbox. Leaf creation permission does not prove that `NtCreateFile` may open every ancestor directory required by the containment adapter.

Compare focused and complete runs through the same launcher and effective permission mode. Capture the native status before changing product containment or test assertions; `0xC0000022` at an ancestor open identifies an execution-permission boundary, not a source-root defect. Run the repository command with explicitly approved execution outside the Codex filesystem sandbox when that is the qualified environment, and continue to report excluded suites separately. Last verified by Story 18.1 on 2026-09-29 with restricted reproduction of the same 88 failures and an approved `SelfContained` pass of 1,975/1,975.
