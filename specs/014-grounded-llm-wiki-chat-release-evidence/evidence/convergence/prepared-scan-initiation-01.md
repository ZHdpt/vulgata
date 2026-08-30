# T031 Convergence: Prepared Scan initiation and Pass 1 identity authority

- Story: `12.5-grounded-llm-wiki-chat-and-august-30-release-evidence`
- Feature: `specs/014-grounded-llm-wiki-chat-release-evidence`
- Outcome: `converged`
- Candidate implementation commit: `d8c5151d362fce6d0bf21dcba30f7939bf7023a6`
- Binding HEAD observed: `8886c81101b74af737a6ce5f79c290ce0536997f`
- Pinned implementation diff: `9d626259c0d64173fb5269be6690d6fbe455dd58..d8c5151d362fce6d0bf21dcba30f7939bf7023a6`
- Story 12.4 dependency: `4798b97d9f23aef587829175df7ab7ebc3daf995` (ancestral)
- Accepted implementation scope: `f5690de4e4ca6b188fbeeab54745c12266585601583a05f9ffd81516c088ebd1`
- Specification SHA-256: `469f72c4e7fc62874e082a7f425351d9f46cf422ae8641d4bee7b64915dd67c0`
- Plan SHA-256: `6ebb0a525721716bfcbb3dce0d330b4846a6f3cf50c604ceb2bf9b0ed41346a8`
- Tasks SHA-256: `6117b0b8eeea9e139868e60274a4b3ba1b25d38312555a06fce225f73cc9968e`
- Constitution SHA-256: `1c5e7319a6fa6ef6576e92a97f65db74057baa2226ca4bf28b45d884e7bd8971`

## Authoritative material inspected

The audit read the repository instructions and governing artifacts directly:

- `AGENTS.md`
- `.specify/memory/constitution.md`
- `docs/bmad/epics.md` (Story 12.5)
- `docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-30-story-12-5-prepared-scan-initiation.md`
- `specs/014-grounded-llm-wiki-chat-release-evidence/spec.md`
- `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, and all files in `contracts/`
- `tasks.md`, including the T025-T033 task definitions and traceability tables
- `workflow-state.json`, accepted plan/definition reviews and implementation decision
- T025 RED evidence and T029/T030 verification evidence
- pinned implementation sources and tests listed below

No specification, plan, task, workflow-state, review, decision, application source,
test, database, retained Track C run, or Git history was changed by this audit.

## Intent and implementation assessment

The accepted definition is satisfied by the pinned candidate. No actionable finding
was identified, so no Convergence section or task was appended to `tasks.md`.

| Requirement set | Assessment and traceable evidence |
| --- | --- |
| FR-063-FR-064 | `PreparedScanInitiationCoordinator` performs exact System/two-Repository/ref/revision validation, server-owned identity resolution, initial and fresh authorization checks; `PreparedScanInitiationAuthorizationRechecker` reloads Identity and materialized Core ownership seams. T029 evidence records denial/revocation zero-write coverage. |
| FR-065-FR-067 | `PreparedScanInitiationStore` creates/replays exactly two revision-bound `ScanRun`s, two new-path `ScanExecution`s and one coordination under a short PostgreSQL transaction with advisory serialization; external analysis begins only after commit. T029 PostgreSQL evidence records tuple visibility, rollback, save/commit and observer behavior. |
| FR-068 | The coordinator passes the immutable tuple into the existing `PreparedScanCoordinator`/`PreparedScanPairBuilder`; T029 public no-preseed evidence records two Prepared Scan Manifests and selected File Code Units through the existing CodeGraph 1.5/JDT-LS/safe XML path. |
| FR-069-FR-073 | Canonical identity, exact replay, reversed-order stability, mismatch conflict, competing/identical concurrency, partial/legacy/cross-paired rejection and post-initiation retry are covered by the PostgreSQL and public API evidence. Prepared-v2 result creation is retained by the accepted publication owner; legacy recovery identity remains unchanged. |
| FR-074-FR-075 | Default composition binds the Agents-owned `EmbeddedPreparedPass1ContractIdentitySource`, CodeGraph 1.5 ports and configured runtime resolver; guards show no CGC/legacy profiling registration or execution and no Worker/Pass 1/Pass 2/Document/Edge/Uncertainty/Chat/LLM action at initiation/preparation. |
| FR-076 | T029 records the no-preseed production vertical and its nine US6 scenarios; T025 records honest RED for the identity/layering seams before production edits. |
| FR-077-FR-078 | T030 and the plan preserve the retained blocked database, isolate future `vulgata_story125_*` release work to T034-T045, and leave general Scan/single/N-Repository/legacy-consolidation work out of scope. |
| SC-019-SC-025 | T029 reports complete tuple/manifest publication, deterministic replay, mismatch and authorization rejection, retry, composition/no-CGC and no-later-stage evidence. T030 reports Track A/B regression and forbidden-path guards. |

## T025-T030 traceability

- T025 honest RED is preserved in `evidence/tdd/prepared-pass1-identity-authority-red-01.md`.
- T026-T028 implement only the closed Core identity value/port, sole Agents embedded
  v2 identity source, Web rechecker/coordinator composition and DI integration.
- T029 verification reports `100 passed, 1 environment-dependent skip, 0 failed` for
  the combined identity/coordinator/runtime/API/PostgreSQL/atomic publication/store
  filter. It records two Runs, two Executions, one coordination, two prepared-v2
  results, two Manifests, 120 selected File Code Units, replay without duplicates,
  mismatch and authorization rejection, transient retry, and no later-stage action.
- T030 verification reports Track A `116 passed, 0 failed`; Track B `423 passed,
  0 product failures` after isolating two sandbox-only Data Protection failures; the
  existing Prepared Scan/Story 12.2 applicable set is `70 passed, 0 failed`; the Web
  build has zero errors; and `git diff --check` is clean.
- The implementation diff contains no EF migration, model snapshot, `VulgataDbContext`
  model change, Story 12.4 contract mutation, Worker dispatch identity mutation,
  project-file change, CGC/legacy profiling restoration, or Track A/B product change.

## Boundary and security proof

- The initiation store owns the only durable tuple transaction, PostgreSQL advisory
  lock and retry/ambiguous-acknowledgement mapping. PostgreSQL microsecond timestamp
  canonicalization preserves replay identity without changing the schema.
- The public coordinator receives only server-owned Prepared Pass 1 prompt/schema
  identity and checked runtime identities; callers cannot choose them.
- Authorization is checked before creation, freshly rechecked immediately before the
  store, and retained immediately before Manifest publication. Missing or revoked
  access is missing-equivalent and does not disclose protected state.
- `PreparedScanRepository` creates the current prepared-v2 `PreScanResult` when the
  accepted no-preseed path has no result, while retaining the compatibility upgrade
  path; `InspectAsync` requires prepared identity and Manifest bindings for replay.
- The embedded v2 prompt is strict UTF-8 and hashes to
  `pass1-code-logic-worker.v2/sha256/5b1e48e50b83c5d12b7916d72d1973f4984db8bf60d1fa62df5cb9f4bcd27e7b`,
  with schema `code-logic/file-v2`. The legacy `WorkerAgentDispatchIdentitySource`
  remains v1/`vulgata.knowledge-document/1.0` and is not used for initiation.
- No source, Document, Edge, Uncertainty, Chat or LLM mutation occurs before the
  accepted later stage owns it; no fake Backup B state or retained blocked database
  was used.

## Commands and observations

Commands run by this audit included:

```text
.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks
git status --short
git branch --show-current
git rev-parse HEAD
git diff --name-only 9d626259c0d64173fb5269be6690d6fbe455dd58..d8c5151d362fce6d0bf21dcba30f7939bf7023a6
git diff --check
git diff --stat 9d626259c0d64173fb5269be6690d6fbe455dd58..d8c5151d362fce6d0bf21dcba30f7939bf7023a6
git merge-base --is-ancestor 4798b97d9f23aef587829175df7ab7ebc3daf995 HEAD
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~PreparedPass1ContractIdentitySourceTests|FullyQualifiedName~PreparedScanInitiationIdentityTests|FullyQualifiedName~PreparedScanInitiationCoordinatorTests|FullyQualifiedName~PreparedScanBoundaryTests|FullyQualifiedName~PreparedScanHandoffProjectionTests" --logger "console;verbosity=minimal"
```

The independent no-build characterization returned `25 passed, 1 failed` out of 26.
The sole failure is the unchanged Story 12.2 `PreparedScanBoundaryTests` assertion
that searches `Program.cs` for the CodeGraph Windows package literal. Current
authority is intentionally in `PreparedScanRuntimeIdentityResolver`; the accepted
T030 report separately records this source-location assertion as pre-existing and
records the current resolver/production-literal tests as passing. The missing local
JDT-LS archive in exploratory qualification is likewise an external asset
prerequisite. Neither observation is an actionable gap in the accepted T025-T030
implementation boundary, and neither justifies a convergence task.

## Finding summary

- Requirement/acceptance items checked: FR-063-FR-078, SC-019-SC-025, all nine US6
  scenarios, plan decisions AD-PSI-1 through AD-PSI-5, and Constitution I-V.
- Findings: `0 missing`, `0 partial`, `0 contradicts`, `0 unrequested`.
- Severity: `0 CRITICAL`, `0 HIGH`, `0 MEDIUM`, `0 LOW`.
- Appended task IDs: none; `tasks.md` remains byte-for-byte unchanged at SHA-256
  `6117b0b8eeea9e139868e60274a4b3ba1b25d38312555a06fce225f73cc9968e`.

## Handoff

**Converged — the implementation satisfies the accepted spec, plan, and tasks for
T025-T030.** No further implementation pass is needed for this boundary. The root
orchestrator may prepare the verdict-neutral quality-review input and dispatch T032;
T034-T045/run-03 remain closed and are not implied by this convergence result.
