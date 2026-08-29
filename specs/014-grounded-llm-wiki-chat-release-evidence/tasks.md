---
description: "Dependency-ordered Story 12.5 Track C-MVP tasks over the accepted Track A/B boundary"
---

# Tasks: Story 12.5 Track C-MVP Release Evidence

**Input**: `spec.md`, accepted corrected `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, every contract under `specs/014-grounded-llm-wiki-chat-release-evidence/contracts/`, `.specify/memory/constitution.md`, `AGENTS.md`, Story 12.5 and the August 30 PRD/architecture authorities, the accepted Sprint Change Proposal, raw plan-review attempts 1-2, raw definition-review attempt 1, accepted planned-change decision, accepted Track B final verification, and the current source/tests named below.

**Pinned definition**: Source commit `1517d8c85eed643a092fbb1c997f232c90e6b52c`; specification SHA-256 `15bc1cc7afe3d7250d8d247546da4ab72086f030bcb81e7b33b875701ffba309`; accepted plan SHA-256 `614bf140543847a396ca04eedb1f007a7e937e260619818838506f053c1cc0d7`; accepted Track A/Track B checkpoint `f292caec427f9844be3c0f65b4cb00a9292080a5`; immutable Story 12.4 dependency `4798b97d9f23aef587829175df7ab7ebc3daf995`.

**Regeneration disposition**: The former proof-subsystem Track C tasks (historical T042-T053: attestation, sealed admission, counter recomputation, ten-check/schema-2.2 validation, supersession/closure, duplicate Tier 2, outage injection, automated screenshot binding, and release-summary work) are invalidated downstream evidence. They are not carried forward. Accepted Track A/Track B source, tests, review, E2E, and verification remain immutable evidence and are not regenerated or reopened absent a newly observed regression.

**Definition-review correction disposition**: Raw definition-review attempt 1 returned `REVISE` and invalidated task hash `3dccac35a3c5dda500646496c6082bfbbe1b78c54e1c758a1bec9ea150036937` for `DR-TC-MVP-001` and `DR-TC-MVP-002`. The corrected plan hash above preserves the accepted Track C-MVP architecture and scope while making the manual-evaluation oracle and production-source failure routing unambiguous. This regenerated task set applies only those corrections; a fresh combined definition review is required.

**Lifecycle authorization boundary**: Independent definition review must first accept this exact `tasks.md`. The orchestrator must then pause for explicit Track C-MVP implementation approval recorded at `specs/014-grounded-llm-wiki-chat-release-evidence/decisions/implementation-approval-track-c-mvp-01.md`. No checklist task below may start before both gates accept the same definition hash. The existing planned-change decision authorizes task generation only.

**Classification rule**: `EXISTING-EVIDENCE` re-observes accepted behavior without manufacturing a RED. `COMPATIBILITY` adds a test over an existing-correct artifact and may be green on its first run. `RED` must fail at the named public test seam before its paired `GREEN`. `GREEN` is the smallest test/evidence-harness change that turns that observed RED green. `GATE`, `RUN`, `QUERY`, `RESTART`, `MANUAL`, `REPORT`, and `VERIFY` do not authorize product changes or imply acceptance by themselves.

**Production-write rule**: No task plans a production write. `src/dotnet/`, `VulgataDbContext`, configurations, migrations, model snapshot, Story 12.4, Scan behavior, accepted Track A/B tests, endpoint registration, DI, project files, and package files are read-only. If any real run, convergence, review, or E2E step exposes a production-source defect, stop this lane, preserve the observation, return to planning, rerun plan review, rebind the approved definition/candidate, and obtain the required approval before any new task set may authorize TDD. Only evidence-file, operator-command, or read-only-query corrections may remain inside this approval. Any requested Scan, persistence, Story 12.4, accepted Chat-security, Agent-tool, or product-feature change contradicts the accepted plan and returns to plan review.

## Immutable accepted-evidence ledger

| Boundary | Immutable authority | Treatment in this task set |
| --- | --- | --- |
| Track A | `f292caec427f9844be3c0f65b4cb00a9292080a5`; `evidence/verification/track-a-01.md`; `evidence/checkpoints/track-a-preservation-01.md` | Regress only in Tier 1; do not edit or reimplement. |
| Track B | `f292caec427f9844be3c0f65b4cb00a9292080a5`; `evidence/verification/track-b-final-01.md`; `evidence/e2e/track-b-run-01.md`; `reviews/quality-review/track-b-quality-review-05.json` | Regress only in Tier 1; quote accepted content-dark streaming evidence without recomputation. |
| Story 12.4 | `4798b97d9f23aef587829175df7ab7ebc3daf995` | Consume exact decisions, current Edges, GENERATED_FROM, and published Flow Groups read-only. |
| Historical Track C | Existing unchecked proof-subsystem tasks and `contracts/story125-release-evidence.schema.json` | Retain as explicitly deferred history; never use as current Track C-MVP acceptance authority. |

---

## Phase 1: Definition acceptance and implementation-entry preflight

**Purpose**: Re-observe the accepted correction and prevent task execution before definition review and implementation approval.

- [x] T001 Classification: GATE — Verify branch `demo0831/story-12-5-trackC`, definition source commit ancestry, exact spec/plan hashes, accepted plan-review attempt 2, accepted planned-change decision, clean/owned Git state, accepted Track A/B checkpoint ancestry, and the accepted definition-review plus implementation-approval artifacts before any test, harness, evidence, database, or product action; record the read-only result in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/preflight/track-c-mvp-entry-01.md` (FR-048-FR-052, SC-013-SC-014).
- [x] T002 Classification: EXISTING-EVIDENCE — In `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/preflight/track-c-mvp-entry-01.md`, bind the immutable Track A/B and Story 12.4 evidence ledger above, record that former proof-subsystem Track C tasks are invalidated, and verify no real regression currently requires reopening Track A/B; do not rerun implementation work or transfer an earlier Track C implementation approval (FR-001-FR-040, FR-048-FR-061, SC-001-SC-008, SC-013-SC-017).

**Checkpoint**: Failure of either entry check stops before any executable or external action. Passing this phase does not itself approve implementation.

---

## Phase 2: User Story 5 - Lightweight current-boundary support (Priority: P1)

**Goal**: Add only the missing test/evidence support for a human-readable Markdown report and a preconnection database-ownership gate.

**Independent Test**: The accepted Markdown contract remains representable without the historical JSON envelope, and malformed/protected database names invoke a supplied connection factory zero times while one valid owned name invokes it exactly once.

### Existing-correct compatibility coverage

- [x] T003 [P] [US5] Classification: COMPATIBILITY — Add `tests/Vulgata.Tests/ReleaseGate/Story125/Story125TrackCMvpReportContractTests.cs` to characterize the already-correct `specs/014-grounded-llm-wiki-chat-release-evidence/contracts/release-evidence-contract.md`: current Markdown authority, four dispositions, preflight/command/query/twelve-question/restart/five-screenshot/failures sections, blocked evidence, deferred historical schema, and absence of any required attestation, ten-check, counter-recomputation, supersession/closure, duplicate-orchestration, outage-injection, screenshot-automation, or release-summary mechanism; a first-run GREEN is honest existing-correct evidence, not a fabricated RED (FR-037-FR-040, FR-046, FR-061-FR-062, SC-008, SC-013, SC-018).

### Behavioral RED then minimal GREEN

- [x] T004 [P] [US5] Classification: RED — Add `tests/Vulgata.Tests/ReleaseGate/Story125/Story125TrackCMvpPreflightTests.cs` with `PreconnectionGuard_InvalidTarget_DoesNotInvokeConnectionFactory` plus a valid-owned-name companion against a not-yet-existing `Story125ReleaseHarness.CreateOwnedDatabaseConnection<T>(string?, Func<T>)`; prove `null`, empty, wrong-case, malformed, `postgres`, `template0`, `template1`, `vulgata`, every `vulgata_story124_*`, and non-owned names fail before the spy factory is called, while a generated lowercase `vulgata_story125_*` name calls it exactly once (FR-041, FR-047, FR-051, SC-014).
- [x] T005 [US5] Classification: GREEN — Only after T004 is observed RED, implement the smallest synchronous preconnection seam `CreateOwnedDatabaseConnection<T>(string?, Func<T>)` in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarness.cs`: delegate to the existing `IsOwnedDatabaseName`, throw a closed non-secret exception for rejection, invoke the factory exactly once only after acceptance, and add no provisioner, product host, connection-string parser, database query, cleanup, or release-envelope semantics (FR-041, FR-047, FR-051, SC-014).
- [x] T006 [US5] Classification: VERIFY — Run `Story125TrackCMvpReportContractTests`, `Story125TrackCMvpPreflightTests`, `Story125Tier2PostgreSqlTests`, `Story125ReleaseHarnessTests`, and `Story125EvaluationContractTests`; preserve every accepted historical test unchanged, record the T004 RED and T005 GREEN commands/results plus the exact three-file owned diff in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/verification/track-c-mvp-support-01.md`, and stop rather than weakening a historical compatibility assertion (FR-037-FR-041, FR-047-FR-051, FR-061-FR-062, SC-008, SC-013-SC-014, SC-018).

**Checkpoint**: The only executable delta is two new test files and, if the honest RED requires it, the minimal test/evidence-harness method in `Story125ReleaseHarness.cs`. Any production-path need stops this lane and returns through planning, plan review, definition/candidate rebinding, and renewed approval before a new task set may authorize TDD.

---

## Phase 3: User Story 5 - Tier 1 regression and forbidden-path guard (Priority: P1)

**Goal**: Regress the accepted product boundary without a network LLM before any real external run.

**Independent Test**: Accepted Track A/Track B focused suites and the new Track C-MVP support slice pass; Web builds; no production/persistence/Story 12.4 delta exists.

- [x] T007 [US5] Classification: VERIFY — Run the quickstart Tier 1 filters for Story 12.5 corpus/guard support, Prepared Chat retrieval/currentness/packet/citation/conversation, strict primary/repair streaming, atomic accepted publication, accepted-only Unicode reveal, Business/IT page behavior, production Scan status/resume, and deterministic behavior, then run `dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj --no-restore`, `git diff --check`, and forbidden diffs for `src/dotnet/`, EF/migrations/snapshot, Story 12.4, Scan strategy, DI/endpoints/projects/packages; record exact commands, counts, warnings, exclusions, and the immutable Track B evidence quoted (not recomputed) in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/verification/track-c-mvp-tier1-01.md` (FR-001-FR-040, FR-048-FR-061, SC-001-SC-008, SC-013-SC-017).

**Checkpoint**: A newly observed accepted-boundary regression stops Track C and is preserved as a production-source finding. Correction returns through planning, plan review, definition/candidate rebinding, and renewed approval before a new task set may authorize the owning TDD lane; no regression is inferred from the already documented unrelated full-suite/environment failures.

---

## Phase 4: User Story 5 - External preflight and owned database gate (Priority: P1)

**Goal**: Pin one runnable candidate and prove every required external identity without exposing secrets or touching product data prematurely.

**Independent Test**: Before any product connection, migration, host start, or query, the report binds one candidate, two exact Repository Profiled Revisions, required tool/provider/browser identities, and one unique owned database name.

- [ ] T008 [US5] Classification: GATE — Create `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/track-c-mvp/run-01/release-report.md` with `running` or sanitized `blocked` disposition; pin the post-support candidate commit, exact two real Java/XML Repository aliases and Profiled Revisions, clean owned state, PostgreSQL 17/Docker, `@colbymchenry/codegraph@1.5.0` version/hash, Java 21, `jdt-language-server-1.61.0-202608141332` version/hash, non-secret configured streaming-provider identity/credential presence/network readiness, authenticated authorized user readiness, and local browser/manual-screenshot capability without printing secrets, connection strings, credentials, tokens, or absolute host paths (FR-039, FR-041-FR-042, FR-046-FR-047, FR-052, FR-062, SC-010, SC-013-SC-014, SC-018).
- [ ] T009 [US5] Classification: GATE — Generate one fresh lowercase `vulgata_story125_*` identity through `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarness.cs`, exercise the preconnection guard, prove zero product connection/host/query calls before acceptance, then use the approved administrator mechanism to create only that clean database and record sanitized purpose/owner/version/command/exit evidence in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/track-c-mvp/run-01/release-report.md`; never use `vulgata`, a system database, any `vulgata_story124_*` database, or the main development database (FR-039, FR-041, FR-047, FR-051-FR-052, FR-062, SC-013-SC-014, SC-018).

**External blocker protocol**: If T008 or T009 finds a missing/unsafe prerequisite, set the report to `blocked`, name only the sanitized prerequisite and completed truthful checks, and write `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-c-mvp-blocked-01.md`. Leave dependent tasks unchecked and do not substitute another database, fake provider, fixtures, fabricated observations, or an unchanged expensive rerun. A blocked checkpoint is safe evidence, not Story completion.

---

## Phase 5: User Story 5 - One combined host integration and real release run (Priority: P1) 🎯 MVP

**Goal**: Operate the existing candidate across real Scan, persisted LLM-Wiki, grounded streaming Chat, and same-database restart without creating a second orchestration path.

**Independent Test**: One host-run Vulgata candidate and one owned PostgreSQL database produce the required two-Repository outputs, twelve fixed Chat observations, restart continuity, and five manually redacted screenshots.

### Existing product run and read-only observations

- [ ] T010 [US5] Classification: RUN — Start host-run Vulgata against only the T009 database, authenticate, select exactly the two pinned real Java/XML Repositories, prepare both at their exact Profiled Revisions, observe real CodeGraph/JDT LS/safe catalog-driven XML evidence, execute the existing Pass 1, exact Cross-Repository Resolution, and Pass 2 surfaces to terminal state, and record all sanitized commands/actions/exits/durations in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/track-c-mvp/run-01/release-report.md`; require 50-150 automatically selected File Code Units and do not change parsing, selection, scheduling, retry, dispatch, relationship, or persistence behavior inside the run (FR-039, FR-042-FR-043, FR-049-FR-052, FR-062, SC-010-SC-011, SC-018).
- [ ] T011 [US5] Classification: QUERY — Against only the owned database, run straightforward read-only observations using the actual mappings in `src/dotnet/Vulgata.Infrastructure/Data/Configurations/PreparedScanManifestConfiguration.cs`, `PreparedScanManifestEntryConfiguration.cs`, `Pass1OutcomeConfiguration.cs`, `Pass1PairExecutionConfiguration.cs`, `Pass2ExecutionConfiguration.cs`, `Pass2JobExecutionConfiguration.cs`, `DocumentVersionConfiguration.cs`, `SourceAnchorConfiguration.cs`, `ExactCrossRepositoryResolutionDecisionConfiguration.cs`, `DocumentEdgeConfiguration.cs`, `UncertaintyConfiguration.cs`, `FlowGroupConfiguration.cs`, `FlowTraceConfiguration.cs`, and `FlowTraceStepConfiguration.cs`; record bounded rows for both manifests/revisions/tool bindings, 50-150 File Code Units, terminal jobs, high-value-seed selected/succeeded/failed counts with failed zero and selected equal succeeded, ordinary failure budget, current source-anchored Pass 1/Pass 2 Documents, one exact decision-bound Cross-Repository REFERENCES Edge, one honest Uncertainty, three Business Logic Documents, one published Cross-Repository Flow Group, and pre-restart Document/Edge/Flow Group identities/counts in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/track-c-mvp/run-01/release-report.md`; never insert, repair, or semantically recompute missing rows (FR-039, FR-043, FR-049-FR-051, SC-010-SC-011, SC-013-SC-014).

### Real mandatory-streaming Chat and manual scoring

- [ ] T012 [US5] Classification: MANUAL — Before the first provider call, pin exactly `q01`-`q12` from `tests/Vulgata.Tests/ReleaseGate/Story125/Fixtures/story125-question-corpus.json`; execute every case in order through the existing authenticated Business/IT page and configured real `OpenAiCompatibleChatProviderStreamingTransport` as an independent row with a fresh `ChatConversationState`. Treat q01 Business -> q02 IT -> q03 Business only as topical lineage (`followUpTo`), with each mode change/reset carrying no Conversation State. For q12, keep the same authenticated principal and the same exactly two authorized Repositories and record only the `prompt-boundary-only` oracle: a prompt about a Repository outside the selected scope does not cause the answer to confirm or disclose that Repository's existence, Business Rules, Documents, relationships, citations, or Source Anchors; do not claim a denied principal, access denial/revocation, a known protected Repository, or true missing-equivalent behavior. Cite `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/verification/track-b-final-01.md` and `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/e2e/track-b-run-01.md` as the accepted Tier 1 authority for same-mode multi-turn behavior and actual authorization/currentness. Manually record all twelve independent rows, execution isolation/topical lineage/authorization oracle, complete-render duration, accepted/cited material-claim counts, usefulness, unauthorized disclosure, streaming observation, packet fingerprint when exposed, and bounded limitations in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/track-c-mvp/run-01/release-report.md`; do not replace failed cases or count deterministic rescue/fake/non-streaming output as a real answer (FR-037-FR-039, FR-044, FR-053-FR-060, FR-062, SC-008-SC-009, SC-013, SC-015-SC-018).

### Same-database application restart

- [ ] T013 [US5] Classification: RESTART — Record pre-restart process/database identity, stop only the Vulgata application process, keep PostgreSQL and the owned database unchanged, restart Vulgata with the same configuration, re-authenticate, open persisted wiki content, and submit one new grounded Chat request; record stop/start/authenticated wiki/Chat outcomes and explicitly exclude transient Conversation State survival from the claim in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/track-c-mvp/run-01/release-report.md` (FR-039, FR-045, FR-053, SC-012-SC-013, SC-015).
- [ ] T014 [US5] Classification: QUERY — Rerun the read-only DocumentVersion, DocumentEdge, and FlowGroup identity/count observations after T013, compare them with T011, and record persisted access, data-loss disposition, and zero restart-caused duplicate publication in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/track-c-mvp/run-01/release-report.md`; a missing row or duplicate is a product/result failure and must not be corrected in the database (FR-039, FR-043, FR-045, FR-049-FR-051, SC-011-SC-014).

### Five manual screenshots

- [ ] T015 [US5] Classification: MANUAL — Manually capture one real, repository-relative screenshot for each required category at `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/track-c-mvp/run-01/screenshots/scan.png`, `business-chat.png`, `it-chat-source-anchors.png`, `cross-repository-result.png`, and `post-restart-wiki-chat.png`; manually redact/reject secrets, credentials, connection strings, absolute host paths, fake Backup B state, or unrelated candidate output, and record category/capture time/real-state/redaction disposition in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/track-c-mvp/run-01/release-report.md` without adding screenshot automation or hashes as acceptance machinery (FR-039, FR-046, FR-061-FR-062, SC-013, SC-018).

### Report completion

- [ ] T016 [US5] Classification: REPORT — Complete `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/track-c-mvp/run-01/release-report.md` against `specs/014-grounded-llm-wiki-chat-release-evidence/contracts/release-evidence-contract.md`, including exactly one truthful `passed`, `failed`, `blocked`, or `not-run` disposition; candidate/revisions/tool/provider/database identities; ordered sanitized commands/exits; every required query observation; the twelve independent fresh-state manual rows when provider execution occurred; q01-q03 topical-lineage-only notation; q12's exact `prompt-boundary-only` oracle and excluded authorization claims; the accepted Tier 1 evidence references for same-mode multi-turn and actual authorization/currentness; restart; five screenshot links for a passed report; and failures/limitations. A passed disposition requires useful rate at least 80%, 100% citations for accepted material technical claims, zero unauthorized disclosure, zero accepted non-streaming answers, every accepted complete render below 30,000 ms, every Scan/output/restart threshold, and no forbidden change (FR-039-FR-047, FR-053, FR-060-FR-062, SC-009-SC-018).

**Checkpoint**: This is one combined candidate/database run. A prerequisite, provider, or product failure preserves earlier truthful evidence and stops only dependent claims. Do not run a duplicate Tier 2, inject an outage, fabricate missing results, or patch product behavior inside the evidence run.

---

## Phase 6: Convergence, independent review, required E2E, pinned verification, and completion

**Purpose**: Apply every remaining schema-v3 gate to the same candidate/report without treating task completion as acceptance.

- [ ] T017 Classification: VERIFY — Run independent convergence against the accepted spec/plan/tasks, the complete three-file maximum support delta, Tier 1 evidence, and the candidate-bound report/screenshots; write the traceable verdict to `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/convergence/track-c-mvp-01.md`, append current-lane work only for a traceable evidence-file, operator-command, or read-only-query gap, route any production-source defect back through planning, plan review, definition/candidate rebinding, and renewed approval before TDD, and classify missing external observations as blocked rather than unbuilt code (FR-039-FR-062, SC-009-SC-018).
- [ ] T018 Classification: VERIFY — Only after T017 converges, prepare verdict-neutral review input with exact candidate/base/diff, authority hashes, owned-path manifest, support RED/GREEN evidence, Tier 1 results, complete report disposition, external exclusions, secrets/redaction review, and no-production/no-EF/no-Story-12.4/no-Scan proof in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/review/track-c-mvp-quality-input-01.md`; do not issue or predict the reviewer verdict (FR-039-FR-062, SC-009-SC-018).
- [ ] T019 Classification: GATE — Dispatch a fresh independent quality review against T018 and the same candidate; only the reviewer may write `specs/014-grounded-llm-wiki-chat-release-evidence/reviews/quality-review/track-c-mvp-quality-review-01.json` and assess correctness, security, evidence honesty, database ordering, external failure behavior, report completeness, test adequacy, architecture fit, regression risk, and retained/deferred scope. `REVISE` may return inside this task set only for an evidence-file, operator-command, or read-only-query correction; a production-source finding returns to planning, plan review, definition/candidate rebinding, and renewed approval before a new task set may authorize TDD. `BLOCKED` preserves a safe checkpoint; only `ACCEPT` advances (FR-039-FR-062, SC-009-SC-018).
- [ ] T020 Classification: VERIFY — After quality acceptance, run the required independent E2E gate against the combined host-run evidence and, only where needed, economical same-candidate browser/API checks for authenticated real Scan, Business/IT grounded accepted-only render, exact Cross-Repository result, and post-restart wiki/Chat access; do not create a second orchestration run or substitute mocks, and write the decision/execution evidence to `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/e2e/track-c-mvp-run-01.md` (FR-039-FR-046, FR-053-FR-062, SC-009-SC-018).
- [ ] T021 Classification: VERIFY — After T019 and T020 accept, run final focused/full-applicable tests, Web build, `git diff --check`, exact hash inventory, candidate/Story 12.4 ancestry, database/revision/restart bindings, report-contract checklist, five relative screenshot paths/redaction results, no-secret scan, and forbidden-path guards; write the pinned verdict to `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/verification/track-c-mvp-final-01.md`, preserving any blocked/failed report instead of claiming release success (FR-039-FR-062, SC-009-SC-018).
- [ ] T022 Classification: GATE — Only after T021 verifies a passed report and all workflow gates accept the same candidate, bind the completion checkpoint in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-c-mvp-completion-01.md`, then let the root orchestrator update Story 12.5 completion in `docs/bmad/implementation-artifacts/sprint-status.yaml` and the workflow-owned state; never mark Story 12.5 done for a blocked, failed, not-run, unreviewed, or uncheckpointed result (FR-039, FR-048-FR-052, FR-062, SC-013-SC-014, SC-018).

---

## Dependencies and execution order

### Phase dependencies

1. Independent definition review and explicit implementation approval precede T001.
2. `T001 -> T002`; neither task authorizes a source/test/database/product action.
3. After T002, T003 and T004 are parallel-safe because they create different test files; `T004 -> T005` is the sole RED/GREEN pair.
4. `T003 + T005 -> T006 -> T007`; Tier 1 blocks every external action.
5. `T007 -> T008 -> T009`; database creation occurs only after every preconnection guard and prerequisite check.
6. `T009 -> T010 -> T011 -> T012 -> T013 -> T014 -> T015 -> T016` is one serial candidate/database/report lane. The twelve case IDs are pinned before T012's first provider call; each case uses fresh Conversation State, q01-q03 retain topical lineage only across mode resets, and restart follows the twelve initial observations as required by AD-CMVP-3 and AD-CMVP-9.
7. `T016 -> T017 -> T018 -> T019 -> T020 -> T021 -> T022`; no gate acceptance is inferred from a completed predecessor task.
8. Any external blocker follows the blocked protocol, writes the safe checkpoint, and prevents all dependent tasks and T022 completion.

### Dependency graph

```text
definition review -> implementation approval -> T001 -> T002
                                                    |-> T003 --|
                                                    |-> T004 -> T005 --|-> T006 -> T007
                                                                             |
                                                                             v
T022 <- T021 <- T020 <- T019 <- T018 <- T017 <- T016 <- T015 <- T014 <- T013 <- T012 <- T011 <- T010 <- T009 <- T008
```

### Parallel-safe group

```text
After T002:
  T003 owns tests/Vulgata.Tests/ReleaseGate/Story125/Story125TrackCMvpReportContractTests.cs
  T004 owns tests/Vulgata.Tests/ReleaseGate/Story125/Story125TrackCMvpPreflightTests.cs
```

No other tasks are parallel-safe: T005 owns the shared harness; T008-T016 serialize one report/database/candidate; T017-T022 are ordered acceptance gates.

---

## Exact ownership and path map

| Owner/boundary | Writable paths | Read-only authorities |
| --- | --- | --- |
| Report contract test owner (T003) | `tests/Vulgata.Tests/ReleaseGate/Story125/Story125TrackCMvpReportContractTests.cs` | All contracts and accepted Track A/B tests |
| Preconnection test owner (T004) | `tests/Vulgata.Tests/ReleaseGate/Story125/Story125TrackCMvpPreflightTests.cs` | Existing database-name tests |
| Minimal harness owner (T005) | `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarness.cs` | `src/dotnet/`, database/product host, historical schema semantics |
| Verification owner (T006-T007, T021) | `evidence/verification/track-c-mvp-support-01.md`, `track-c-mvp-tier1-01.md`, `track-c-mvp-final-01.md` | Source/tests/contracts |
| Real-run evidence owner (T008-T016) | `evidence/track-c-mvp/run-01/release-report.md` and the five named files under `evidence/track-c-mvp/run-01/screenshots/` | Product, database rows, tool/provider output |
| Convergence owner (T017) | `evidence/convergence/track-c-mvp-01.md` | Complete accepted boundary |
| Review-input owner (T018) | `evidence/review/track-c-mvp-quality-input-01.md` | Complete accepted boundary |
| Independent reviewer (T019) | `reviews/quality-review/track-c-mvp-quality-review-01.json` | T018 input and candidate |
| Independent E2E owner (T020) | `evidence/e2e/track-c-mvp-run-01.md` | Combined run/report and candidate |
| Root completion owner (T022) | `evidence/checkpoints/track-c-mvp-completion-01.md`, then workflow-owned state and `docs/bmad/implementation-artifacts/sprint-status.yaml` | Accepted final verification |

`src/dotnet/`, EF/configuration/migration/model-snapshot paths, `specs/013-exact-cross-repository-flow-pass2/`, accepted Track A/B tests, contracts, PRD/architecture, and Git history are never writable under this task definition.

---

## Requirement and success-criterion coverage

| Boundary | Functional requirements | Success criteria | Owner |
| --- | --- | --- | --- |
| Immutable Track A/B evidence | FR-001-FR-038, FR-048, FR-053-FR-060 | SC-001-SC-008, SC-015-SC-017 | Ledger, T002, T007 |
| Markdown/support boundary | FR-037-FR-041, FR-047-FR-051, FR-061-FR-062 | SC-008, SC-013-SC-014, SC-018 | T003-T006 |
| Tier 1 and no-change guard | FR-001-FR-040, FR-048-FR-061 | SC-001-SC-008, SC-013-SC-017 | T007 |
| External preflight/database | FR-039, FR-041-FR-042, FR-046-FR-047, FR-051-FR-052, FR-062 | SC-010, SC-013-SC-014, SC-018 | T008-T009 |
| Real Scan and persisted outputs | FR-039, FR-042-FR-043, FR-049-FR-052, FR-062 | SC-010-SC-011, SC-013-SC-014, SC-018 | T010-T011 |
| Twelve real Chat cases | FR-037-FR-039, FR-044, FR-053-FR-060, FR-062 | SC-008-SC-009, SC-013, SC-015-SC-018 | T012 |
| Restart continuity | FR-039, FR-043, FR-045, FR-049-FR-053 | SC-011-SC-015 | T013-T014 |
| Manual screenshots/report | FR-039-FR-047, FR-053, FR-060-FR-062 | SC-009-SC-018 | T015-T016 |
| Downstream acceptance | FR-039-FR-062 | SC-009-SC-018 | T017-T022 |

All FR-001-FR-062 and SC-001-SC-018 are covered either by immutable accepted evidence plus regression or by one current Track C-MVP owner. No accepted Track A/B requirement is reimplemented merely to produce proof.

T012 owns twelve independent real-run observations and q12's prompt-boundary-only result. The immutable Track B ledger and T007 remain the authority for same-mode multi-turn behavior and actual authorization/currentness; q12 does not exercise or claim denial, revocation, a protected Repository, or missing-equivalent behavior.

---

## Implementation strategy

### MVP sequence

1. Accept the definition and obtain explicit implementation approval.
2. Add the compatibility test and the one honest preconnection RED/GREEN seam.
3. Regress Tier 1 and enforce the zero-production-write boundary.
4. Run external preflight; checkpoint honestly if blocked.
5. Execute one candidate/database Scan + Chat + restart lane and finish the Markdown report/screenshots.
6. Converge, independently review, run required E2E, verify, and checkpoint before Story completion.

### Production-source failure boundary

A real product failure does not authorize an ad hoc fix or an immediate RED/GREEN cycle in T008-T021. Preserve the failed observation, stop dependent claims, and return the minimal reproducible behavior to planning. Before any production TDD, the corrected plan must pass independent plan review, the definition and candidate must be rebound, and the applicable approval gates must accept a new task set. Only evidence-file, operator-command, and read-only-query corrections stay within the current approval. Scan, persistence, Story 12.4, accepted Chat-security, Agent-tool, and other production needs follow the same return boundary.

---

## Inline task-author self-review

| Lens | Result | Evidence |
| --- | --- | --- |
| Complete source coverage | PASS | Corrected spec/plan, all design companions/contracts, raw plan reviews, raw definition-review attempt 1, accepted decisions, PRD/architecture/Story 12.5, Constitution/AGENTS, accepted Track B verification, invalidated tasks, and exact harness/tests were inspected. |
| Exact paths and ownership | PASS | Every task names its writable path; the only test/harness writes are two new test files plus one existing test-only harness file. The report, five screenshot paths, and every downstream gate artifact are exact. |
| Dependency ordering | PASS | Definition/approval -> compatibility/RED-GREEN -> Tier 1 -> preconnection gate -> one run -> queries -> twelve questions -> restart -> screenshots/report -> convergence/review/E2E/verification/completion. Shared report/harness paths are serialized. |
| TDD honesty | PASS | T004 is a genuine missing-seam compile/behavior RED paired with T005. T003 is explicitly COMPATIBILITY because the accepted Markdown contract is already correct; it is not mislabeled RED. No production GREEN is planned, and a newly discovered production defect returns to plan/review/rebinding before any new TDD authority exists. |
| Accepted evidence preservation | PASS | Track A/B and Story 12.4 are an immutable ledger and Tier 1 input. No task edits or recreates accepted production/tests, and no regression lane opens without a newly observed failure. |
| Evaluation and authorization honesty | PASS | T012 makes q01-q12 independent fresh-state rows, treats q01-q03 as topical lineage only across mode resets, limits q12 to the prompt-boundary-only oracle, and cites accepted Tier 1 for same-mode multi-turn and actual authorization/currentness. |
| Retained/deferred scope | PASS | Real tools, two Repositories, 50-150 File Code Units, LLM, queries, q01-q12, restart, five screenshots, security and streaming remain. Attestation, sealed admission, counter recomputation, ten-check/schema-2.2 machinery, closure graphs, duplicate Tier 2, outage injection, screenshot automation, and release-summary work are absent. |
| External failure behavior | PASS | T008/T009 fail before unsafe access, preserve a sanitized blocked report/checkpoint, and prohibit fake substitution, fabricated evidence, completion claims, or unchanged expensive reruns. |
| Forbidden architecture | PASS | No EF/DbContext/snapshot, Story 12.4, Scan, Agent-tool, endpoint, DI, project/package, or production write is authorized. A production-source finding returns to planning, review, definition/candidate rebinding, and renewed approval before TDD. |
| FR/SC traceability | PASS | Coverage table spans FR-001-FR-062 and SC-001-SC-018, distinguishing immutable accepted behavior from new Track C-MVP execution. |
| Placeholders | PASS | No design placeholder remains. Repository revisions, database identity, provider identity, credentials, and tool/browser availability are runtime preflight values with explicit fail-closed handling. |
| Checklist format | PASS | All 22 tasks use `- [ ] T### [P?] [US5?]` with sequential IDs, classifications, exact paths, and dependency-aware order. |

**Self-review conclusion**: `TASKS_READY_FOR_INDEPENDENT_DEFINITION_REVIEW`. This definition authorizes no work by itself; the next action is independent definition review, followed by the mandatory explicit implementation-approval pause.
