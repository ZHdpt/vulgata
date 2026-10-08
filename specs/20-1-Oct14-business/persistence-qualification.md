# October persistence qualification — 2026-10-08

## Verified boundary

Assigned `oct14/business` was clean at `b3edb9e0dcb2dd2638582977744e06a5a0ba0b79`. Integrator approved `f6be1b410bd5de08394d70e5e83017c6512fec51`; the assigned worktree merged it without conflicts at `41cc6d16dd4d3aeb893a4c8f3fe9ae772ad1faff`. Existing controlled admission, dialogue, resolution, synthesis and BL publication implementations are present. Unchecked runtime tasks are not evidence that those implementations are absent.

This inventory describes the authored PostgreSQL qualification; executed results are recorded below. Existing SelfContained evidence remains in `quickstart.md`. No source/provider/native/E2E qualification follows from database tests.

## Source-free owner selection

All classes below are in `tests/Vulgata.Tests/October/Knowledge`, named `October{Group}PostgreSqlTests` and classified PostgreSql in the suite inventory.

| Group | Authored cases | Qualification |
| --- | ---: | --- |
| GenerateAdmission | 6 | Concurrent admission/replay, immutable setup, F1 fence, pending commit and lost acknowledgement |
| FileAcceptance | 9 | Atomic whole-file acceptance, concurrent collisions, rollback, immutable history, exact cross-batch keys and pending commit |
| Publication | 11 | Exact membership/coverage, JSONB reads, keys, concurrency, freeze versus late acceptance and rollback |
| Dialogue | 16 | All four active states, concurrent admission/save, JSONB capsules/references, immutable history, exact references and ambiguous commits |
| Resolution | 13 | Original witness/full dependencies, qualified edges, concurrency, exact keys, immutable evidence, rollback and complete dispositions |
| FlowPublication | 7 | Populated historical upgrade preservation, exclusive CL/BL ownership, typed dependencies, immutable BL and atomic rollback |
| Total | 62 | 38 facts plus 24 rows across 10 theories; expected zero skips |

```powershell
pwsh -NoProfile -File eng/test.ps1 -Suite PostgreSql -Filter 'FullyQualifiedName~OctoberGenerateAdmissionPostgreSqlTests|FullyQualifiedName~OctoberFileAcceptancePostgreSqlTests|FullyQualifiedName~OctoberPublicationPostgreSqlTests|FullyQualifiedName~OctoberDialoguePostgreSqlTests|FullyQualifiedName~OctoberResolutionPostgreSqlTests|FullyQualifiedName~OctoberFlowPublicationPostgreSqlTests'
```

The selection uses direct owner stores and controlled semantic inputs. It does not launch application children, scan repositories, call providers, import an external seed archive or invoke native source tools. Historical upgrade tests create additional owned donor databases containing controlled test data; this is database-test setup, never evidence of real generated Knowledge.

Separate optional cases require explicit inclusion: Wiki PostgreSQL (1) exercises controlled publication/Explore; Scan acceptance (3) and CL publication (2) exercise controlled adapters with PostgreSQL. Setup isolation (3) launches owned generator `dotnet` children and an in-memory WebApplicationFactory, including Identity/management migrations in owned databases. These are not silently included in the 62-case command.

## Migration and resource order

Only the October context applies this chain, using `october.__OctoberMigrationsHistory`:

1. `20260930000000_InitialOctoberSetup`
2. `20260930010000_AddOctoberPreparedSourceApprovals`
3. `20261006000000_AddOctoberKnowledgeFoundation`
4. `20261006010000_AddOctoberKnowledgePublications`
5. `20261007000000_AddOctoberDialogue`
6. `20261007010000_AddOctoberResolutionEvidence`
7. `20261007140153_AddOctoberFlowPublications`

The final migration's Designer/Guards and snapshot are not additional migration steps. October history remains separate from Identity and ordinary application histories. Upgrade fixtures migrate explicitly selected historical targets forward; no shared database or destructive downgrade is authorized.

The standard runner owns one fresh `postgres:17-alpine` container and labelled named volume, a random loopback port and generated process-only credentials. Each fixture creates uniquely named, registered databases with nonpooled connections. Donor/target pairs are both owned. Fixture cleanup is limited to those databases; runner cleanup validates labels/mounts, checks container removal, removes its volume and explicitly verifies volume absence. Cleanup failure is qualification failure. Run one coordinated launcher at a time; do not migrate a shared target concurrently. Distinct per-test databases are isolated.

Read-only prerequisite observation on October 8: Docker server `29.8.2` reachable; cached image ID `sha256:dc17045ccfd343b49600570ea734b9c4991cf1c3f3302e67df51e3b402dd55c4`. No resource was started by that check. Integrator is coordinating source-free scope A separately from real-run source/provider scope B and retained presentation scope C. The user subsequently approved A through Integrator and Business was released as its sole runner; A does not depend on provider/source disclosure approval.

## Real-run support boundary

Reuse Integrator's single generated dataset and exact batch/publication/version handles. Compare generated CL and BL with the authorized actual source: positive relationship evidence, conditions/branches/effects, distinct useful roots, explicit uncertainties and processing gaps, and advisory qualification retained per claim. Confirm complete dependencies and live ordinary-reader authority through canonical ports, not citations reconstructed by consumers or operator authority substituted for reader identity. Preserve early CL-only usability independently of BL/Chat completion.

Do not manually insert accepted Knowledge or manufacture witnesses to satisfy the real run. Database fixtures qualify persistence only. Report receipt durability, execution status, file/root publication coverage and semantic uncertainty separately; preserve confirmed publication alongside a reported processing failure where the existing contract permits it.

## Execution record

The user approved exactly operation A with "Approve A now" in Integrator on October 8. Integrator released Business as sole runner for the six-class selection and its focused corrective replay, using fresh resources each time. B remains explicitly paused pending LLM configuration; C remains pending until B passes.

| Run | Selection/outcome | Interpretation |
| --- | --- | --- |
| `0974d7184f22465e91955763b4a2b295` | 62 selected, 60 passed, 2 failed, zero skips | Publication and resolution history tests expected trigger SQLSTATE23514 for non-CASCADE TRUNCATE; current incoming FKs rejected earlier with native0A000. All other cases passed. |
| `480ef62c6b234f7aba64214135ed8f99` | 2 selected, 2 passed, zero skips | Corrected tests verify exact0A000 plus the FK diagnostic, then CASCADE must hit immutable23514. Before/after row equality is preserved. |
| `7ba55259efe249ee8bbfbf881af42fcf` | 62 selected, 62 passed, zero skips | Final complete approved selection: admission6, acceptance9, publication11, dialogue16, resolution13, flowpublication7. Build zero errors/19 warnings. |

All three runs used the standard launcher, fresh independently labelled PostgreSQL17 resources and no seed archive. Container removal and volume absence were verified for each; final container absence was additionally checked explicitly. All databases lived only inside these deleted owned resources. No database or dataset is retained for a real run or presentation.

The isolated correction changes only two test assertions/helpers. Native FK refusal and CASCADE immutable-history rejection are both required; no production/schema guard was changed or weakened. Independent test review passed. The seven-step migration chain, controlled populated upgrades, atomic outcomes, exact relationships/dependencies, dialogue active-state ownership and immutable history now have actual PostgreSQL evidence on this checkpoint. This does not assert arbitrary shared-database upgrade readiness.

Optional Wiki/Scan/setup-host/Explorer PostgreSQL categories were not selected. SelfContained, LocalTools, External, E2E and HistoricalBoundary were excluded from these runs. Previously recorded controlled results remain separate. Real source generation, source-content review, provider calls, command activation and retained presentation remain unexecuted. The separate dialogue cleanup-lifetime concern is a source-inspected integration issue under owner/consumer agreement; these database tests do not establish scope-disposal completion.
