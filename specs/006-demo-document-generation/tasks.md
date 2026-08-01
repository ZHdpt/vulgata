# Tasks: Demo Document Generation

**Input**: Accepted design set in `specs/006-demo-document-generation/`
**Boundary**: base commit `c59171b2e5fb726f0ca40e15dcdeea2749744d01`; reviewed diff SHA-256 `e69b706a82c0498b68587d2ace4a99d430998befc7fefd4fe4842a786f0d9443`
**Execution level**: `expedited-mvp`

**TDD rule**: In every G0-G7 group, complete and observe the named RED task before changing production code. Keep every committed state green; transient failing tests are evidence only and must never be committed.

**Schema stop rule**: No EF entity, configuration, migration, or model-snapshot change is authorized. If any task requires one, stop Story 11.3 and return to plan review/planned-change authorization; do not create a migration task.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Safe to execute concurrently because it has no incomplete dependency or shared-file overlap.
- **[US1]**: Source-grounded Pass 1 Code Logic Document generation.
- **[US2]**: Fail-closed invalid/unsafe result isolation.
- **[US3]**: One prepared Pass 2 Business Logic Document lifecycle.

No task is marked `[P]`: G0-G7 intentionally share contracts, fixtures, runner state, and validation evidence, so the safe execution order is sequential.

---

## Phase 1: Setup

**Purpose**: Establish deterministic, credential-free fixtures used by later observable RED tests.

- [x] T001 Create the reusable loopback OpenAI-compatible HTTP capture, committed-snapshot lease, provider-assignment, Repository access-scope, and deterministic knowledge/result builders in `tests/Vulgata.Tests/WorkerAgentTestFixtures.cs`; validation: fixtures use no live provider, real credential, raw diagnostic persistence, or absolute host path in assertions.

---

## Phase 2: Foundational — G0 F003 Production-Reachability Gate

**Purpose**: Correct the inherited Story 11.2 relational reservation defect before production DI can expose the runner.

> **RED first**: T002 must fail against the pinned implementation because the reservation is not obtained before read/revalidation.

- [x] T002 Add and run a real shared file-backed SQLite two-`VulgataDbContext` regression in `tests/Vulgata.Tests/ScanExecutionRepositoryConcurrencyTests.cs` that coordinates both writers at the pre-reservation boundary; validation: RED shows both can act on stale state, while the specified result is one valid transition and one loser that observes/rejects post-reservation state, with no SQLite `FOR UPDATE`.
- [x] T003 After T002 is observably RED, correct `src/dotnet/Vulgata.Infrastructure/Data/ScanExecutionRepository.cs` so SQLite begins the supported serializable transaction, obtains a parameterized no-op write reservation over deterministically ordered owner row(s) before reading/revalidating/mutating, and PostgreSQL retains its existing row-lock path; validation: one writer wins, the loser deterministically observes/rejects, and no schema shape changes.
- [x] T004 Run the focused G0 test plus existing Story 11.2 runner persistence/restart tests from `tests/Vulgata.Tests/ScanExecutionRepositoryConcurrencyTests.cs` and `tests/Vulgata.Tests/SequentialScanRunnerRestartTests.cs`; validation: G0 is GREEN and production DI remains unchanged until G6.

**Checkpoint**: F003 is GREEN; user-story production work may begin.

---

## Phase 3: User Story 1 — Generate Source-Grounded Code Logic Documents (Priority: P1) MVP

**Goal**: Hydrate one exact authorized committed-revision slice, dispatch one bounded Pass 1 Worker Agent job, and commit one complete Code Logic Document aggregate before valid progress.

**Independent Test**: Run one authorized prepared Pass 1 job against the committed-snapshot and loopback fixtures; verify only the exact slice crosses the provider boundary and exactly one immutable Code Logic Document Version, Source Anchors, permitted same-Repository `REFERENCES` Edges, communication evidence, and retrieval projection are committed before progress increments.

### G1 — Closed Pass 1 Job and Result Contracts

- [x] T005 [US1] Add and run RED contract tests in `tests/Vulgata.Tests/WorkerAgentContractTests.cs` for a typed Pass 1 payload/result, exact Job/System/Repository/ScanRun/Code Unit/Profiled Revision/path/range/source-hash/File-Identity-Hash equality, canonical unique collections, finite named bounds, and valid-envelope kind/payload matching; validation: tests fail because the Story 11.2 reserved/open payload cannot express the accepted aggregate.
- [x] T006 [US1] After T005 is RED, implement the provider-independent Pass 1 discriminated-union contracts and shared bound constants in `src/dotnet/Vulgata.Core/DomainServices/WorkerAgentJobKind.cs`, `WorkerAgentJobPacket.cs`, `WorkerAgentResultPayload.cs`, and `WorkerAgentResultEnvelope.cs`; validation: construction accepts exactly one matching Pass 1 variant and rejects zero/multiple/mismatched variants without adding dictionaries, `object`, arbitrary JSON, or raw source.
- [x] T007 [US1] Run and refactor the G1 contract suite in `tests/Vulgata.Tests/WorkerAgentContractTests.cs`; validation: G1 is GREEN, existing Story 11.2 contract tests remain GREEN, and Core has no EF/provider dependency.

### G2 — Exact Revision-Bound Hydration

- [x] T008 [US1] Add and run RED hydration tests in `tests/Vulgata.Tests/WorkerAgentHydrationTests.cs` for authorized scope, exact committed Profiled Revision before/after read, repository-relative inclusive slice, File Identity Hash and slice SHA-256, dirty/moved working-copy isolation, allowlisted structural/communication context, 2,000-line/65,536-byte slice bounds, and 131,072-byte request bound; validation: the valid fixture proves the intended transient context and the tests fail because no hydrator exists.
- [x] T009 [US1] After T008 is RED, implement `RevisionBoundWorkerContextHydrator` and transient bounded context types in `src/dotnet/Vulgata.Agents/WorkerExecution/RevisionBoundWorkerContextHydrator.cs` and `WorkerAgentExecutionContext.cs` using the existing `ICommittedRepositorySnapshotService`; validation: only the requested archive file/range is read, hashes are constant-time compared, revision is rechecked, and hydrated source is neither logged nor persisted.
- [x] T010 [US1] Run and refactor the G2 suite in `tests/Vulgata.Tests/WorkerAgentHydrationTests.cs`; validation: exact committed source reaches the transient context once, the mutable checkout never does, and G2 is GREEN.

### G3 — Pinned Pass 1 Provider Adapter

- [x] T011 [US1] Add and run RED loopback tests in `tests/Vulgata.Tests/WorkerAgentDispatcherTests.cs` for System Worker Agent override then global Worker Agent assignment, exactly the configured endpoint, one Pass 1 call, fixed embedded prompt, variant schema request, minimal hydrated context, and no runtime fallback; validation: captured HTTP contains the required identities/slice and no credential, unrelated source, or absolute host path.
- [x] T012 [US1] Add and run RED resource tests in `tests/Vulgata.Tests/WorkerAgentDispatcherTests.cs` for fixed-name discovery and finite non-empty content of `pass1-code-logic-worker.v1.md`, plus failure before HTTP on missing/ambiguous/empty/oversized resources and absence of hardcoded runtime prompt replacement.
- [x] T013 [US1] After T011-T012 are RED, add the centrally pinned package references and embedded Pass 1 resource declaration to `src/dotnet/Vulgata.Agents/Vulgata.Agents.csproj`, and author the versioned prompt at `src/dotnet/Vulgata.Agents/Prompts/pass1-code-logic-worker.v1.md`; validation: the prompt is resolved only by its fixed allowlist name.
- [x] T014 [US1] Implement scoped assignment/credential resolution and the Microsoft Agent Framework OpenAI-compatible dispatcher in `src/dotnet/Vulgata.Agents/WorkerExecution/WorkerAgentProviderResolver.cs` and `WorkerAgentDispatcher.cs`; validation: the API key is decrypted only immediately before client construction, exactly one configured endpoint is used, and no retry, failover, external fallback, or telemetry exporter is introduced.
- [x] T015 [US1] Implement bounded JSON streaming preflight, closed source-generated metadata, and Pass 1 semantic validation in `src/dotnet/Vulgata.Agents/WorkerExecution/WorkerAgentJsonPreflight.cs`, `WorkerAgentJsonContext.cs`, and `WorkerAgentResultValidator.cs`; validation: the valid loopback response is accepted only after packet-identity, ordering, uniqueness, bounds, Source Anchor, and allowlisted same-Repository target checks.
- [x] T016 [US1] Run and refactor the G3 tests in `tests/Vulgata.Tests/WorkerAgentDispatcherTests.cs`; validation: G3 is GREEN with exactly one provider call and no live-provider dependency.

### G4 — Atomic Pass 1 Commit Before Progress

- [x] T017 [US1] Add and run RED persistence tests in `tests/Vulgata.Tests/WorkerAgentResultCommitterTests.cs` for one authorization-scoped Pass 1 `KnowledgeCommit` containing immutable Code Logic content, exact Source Anchors, allowlisted same-Repository `REFERENCES`, communication evidence, retrieval projection, stable replay identity, idempotency, and preserved Version Chain; validation: the complete aggregate is atomically visible or no row is visible.
- [x] T018 [US1] After T017 is RED, add the narrow provider-independent port and closed outcomes in `src/dotnet/Vulgata.Core/DomainServices/IWorkerAgentResultCommitter.cs`; validation: `CommitAsync` accepts only packet, valid result, caller `RepositoryAccessScope`, and cancellation token, and only `Committed` can permit progress.
- [x] T019 [US1] Implement Pass 1 mapping through the existing sole knowledge-store boundary in `src/dotnet/Vulgata.Infrastructure/Data/WorkerAgentResultCommitter.cs`; validation: it rechecks variant/identity/scope, creates exactly one existing `KnowledgeCommit`, derives stable operation/aggregate IDs, and delegates only to `IKnowledgeStoreRepository`.
- [x] T020 [US1] Run the G4 SQLite persistence/replay/Version Chain tests and the existing PostgreSQL knowledge-store concurrency regression from `tests/Vulgata.Tests/WorkerAgentResultCommitterTests.cs` and `tests/Vulgata.Tests/KnowledgeStorePostgreSqlConcurrencyTests.cs`; validation: G4 is GREEN without a second Document repository or distributed transaction.
- [x] T021 [US1] Add and run RED ordering tests in `tests/Vulgata.Tests/SequentialScanRunnerDocumentGenerationTests.cs` proving the runner awaits `Committed` before recording valid/clearing in-flight/incrementing progress, and repeats the identical knowledge commit before a progress retry; validation: an interceptor observes knowledge before progress and no duplicate logical truth after replay.
- [x] T022 [US1] After T021 is RED, inject and await `IWorkerAgentResultCommitter` with caller-supplied `RepositoryAccessScope` in `src/dotnet/Vulgata.Core/DomainServices/SequentialScanRunner.cs`; validation: Pass 1 reaches `Pass1Completed` only after every prepared commit and never reaches terminal `Completed` in this group.
- [x] T023 [US1] Run the complete US1 focused suite in `tests/Vulgata.Tests/WorkerAgentContractTests.cs`, `WorkerAgentHydrationTests.cs`, `WorkerAgentDispatcherTests.cs`, `WorkerAgentResultCommitterTests.cs`, and `SequentialScanRunnerDocumentGenerationTests.cs`; validation: the independent test passes and every recorded-valid job has exactly one prior complete aggregate.

**Checkpoint**: US1 independently satisfies AC-1/AC-3, FR-001-FR-003/FR-005-FR-006/FR-008-FR-009, and SC-001.

---

## Phase 4: User Story 2 — Fail Closed on Unsafe or Invalid Results (Priority: P1)

**Goal**: Reject unsafe input/output before provider execution or knowledge persistence, emit only bounded Chinese failures, and record zero valid progress.

**Independent Test**: Submit malformed, oversized, duplicate, unordered, identity-mismatched, stale-hash, unauthorized, sensitive, absolute-path, raw-source-carrier, unsafe-diagnostic, conflict, rejection, and cancellation fixtures; verify zero affected knowledge rows, zero provider calls where pre-dispatch validation applies, and zero valid progress.

### G2/G3 — Source and Provider Isolation Matrix

- [x] T024 [US2] Add and run RED pre-dispatch rejection tests in `tests/Vulgata.Tests/WorkerAgentHydrationTests.cs` for wrong System/Repository/ScanRun/Code Unit/Profiled Revision/path/range/hash/File Identity Hash, revision drift, parent traversal/absolute path, secret, oversized source/request, unauthorized scope, and cancellation; validation: every fixture expects zero provider calls and no hydrated content in logs/exceptions.
- [x] T025 [US2] After T024 is RED, harden `src/dotnet/Vulgata.Agents/WorkerExecution/RevisionBoundWorkerContextHydrator.cs` to fail closed for the complete matrix before provider invocation; validation: outcomes use closed failure codes and bounded Chinese messages without echoing source or diagnostics.
- [x] T026 [US2] Add and run RED provider-output tests in `tests/Vulgata.Tests/WorkerAgentDispatcherTests.cs` for oversized bytes before allocation, invalid UTF-8, depth over 32, trailing JSON, duplicate/unknown properties, unknown enum, duplicate/out-of-order values, identity mismatch, out-of-range Source Anchor, unallowlisted target, secret, absolute path, raw-source carrier, raw provider diagnostic, HTTP error, and cancellation.
- [x] T027 [US2] After T026 is RED, complete strict preflight, closed deserialization, safety scanning, semantic validation, and diagnostic mapping in `src/dotnet/Vulgata.Agents/WorkerExecution/WorkerAgentJsonPreflight.cs`, `WorkerAgentJsonContext.cs`, `WorkerAgentResultValidator.cs`, and `WorkerAgentDispatcher.cs`; validation: response size is capped at 65,536 UTF-8 bytes, every invalid response is non-valid, and no raw response/exception fragment enters an envelope.

### G4/G5 — Zero-Write and Zero-Progress Failure Semantics

- [x] T028 [US2] Add and run RED committer rejection tests in `tests/Vulgata.Tests/WorkerAgentResultCommitterTests.cs` for non-valid/mismatched envelopes, cross-Repository/System scope, stale identities, invalid anchors/references, conflict, rejection, persistence failure, and cancellation; validation: zero Document Version, Source Anchor, Edge, flow-evidence, section, and retrieval-projection rows become visible.
- [x] T029 [US2] After T028 is RED, complete defensive revalidation and closed outcome mapping in `src/dotnet/Vulgata.Infrastructure/Data/WorkerAgentResultCommitter.cs`; validation: only `Committed` is progress-capable and no failed mapping partially writes.
- [x] T030 [US2] Add and run RED lifecycle tests in `tests/Vulgata.Tests/SequentialScanRunnerDocumentGenerationTests.cs` for hydration/dispatch/schema/authorization/commit conflict/rejection/failure/cancellation outcomes; validation: each produces the existing bounded Chinese failure behavior, no valid progress, and no fabricated phase advance.
- [x] T031 [US2] After T030 is RED, complete fail-closed result handling in `src/dotnet/Vulgata.Core/DomainServices/SequentialScanRunner.cs`; validation: commit is never attempted for non-valid output, progress is never recorded for non-`Committed`, and cancellation does not fabricate a later transition.
- [x] T032 [US2] Run the complete invalid/security matrix in `tests/Vulgata.Tests/WorkerAgentHydrationTests.cs`, `WorkerAgentDispatcherTests.cs`, `WorkerAgentResultCommitterTests.cs`, and `SequentialScanRunnerDocumentGenerationTests.cs`; validation: G2-G5 are GREEN, affected visible writes and valid-job records are both zero, and failures expose no raw diagnostics.

**Checkpoint**: US2 independently satisfies AC-2, FR-003-FR-004/FR-007/FR-016-FR-018, SC-002, and the Pass 1 portion of SC-006.

---

## Phase 5: User Story 3 — Generate One Prepared Business Logic Document (Priority: P2)

**Goal**: Resolve one fixed prepared flow at `Pass1Completed`, synthesize from exact current Code Logic Document Versions, atomically commit exact provenance, then transition to `Completed`.

**Independent Test**: Begin with all prepared Pass 1 aggregates durably committed; verify exactly one authorized Pass 2 dispatch, one Business Logic Document Version with purpose/ordered steps/outcome and exact evidence, set-equal `GENERATED_FROM` Edges, zero narrative-created base `REFERENCES`, and `Completed` only after commit.

### G5 — Closed Prepared Flow and Exact Pass 2 Context

- [x] T033 [US3] Add and run RED manifest/model tests in `tests/Vulgata.Tests/PreparedPass2FlowTests.cs` for one fixed-name embedded closed-schema manifest, exact schema/System/Repository identities, non-empty ordered unique members, contiguous bounded trace/evidence, and rejection of zero/multiple/missing/extra/stale/duplicate/reordered/ambiguous/unauthorized members, raw source, credentials, absolute paths, queries, and traversal rules.
- [x] T034 [US3] After T033 is RED, add the provider-independent closed manifest model in `src/dotnet/Vulgata.Core/DomainServices/PreparedPass2Flow.cs`; validation: the model carries only prepared identities/evidence and no discovery, clustering, graph query, or arbitrary source capability.
- [x] T035 [US3] Add the single version-controlled manifest and embedded resource declaration in `src/dotnet/Vulgata.Agents/PreparedFlows/demo-business-flow.v1.json` and `src/dotnet/Vulgata.Agents/Vulgata.Agents.csproj`; validation: exactly one allowlisted finite manifest is discoverable.
- [x] T036 [US3] Implement fixed catalog loading and authorization-scoped exact-current-version resolution in `src/dotnet/Vulgata.Agents/WorkerExecution/PreparedPass2FlowCatalog.cs` and `PreparedPass2FlowResolver.cs`; validation: every member matches Repository/ScanRun/Profiled Revision/currentness and resolves in manifest order before provider execution.
- [x] T037 [US3] Run and refactor `tests/Vulgata.Tests/PreparedPass2FlowTests.cs`; validation: G5 manifest/resolution predicates are GREEN and invalid resolution yields zero Pass 2 calls.

### G1/G3 — Closed Pass 2 Union and Provider Execution

- [x] T038 [US3] Add and run RED Pass 2 contract tests in `tests/Vulgata.Tests/WorkerAgentContractTests.cs` for the second and only other job/result variant, required non-technical purpose/ordered main steps/outcome, exact flow evidence and Code Logic Document Version set, and the absence/rejection of any base `REFERENCES` request field.
- [x] T039 [US3] After T038 is RED, extend `src/dotnet/Vulgata.Core/DomainServices/WorkerAgentJobKind.cs`, `WorkerAgentJobPacket.cs`, `WorkerAgentResultPayload.cs`, and `WorkerAgentResultEnvelope.cs` to the exhaustive two-variant union; validation: unknown kinds/fields and kind/payload mismatches remain unrepresentable or rejected.
- [x] T040 [US3] Add and run RED Pass 2 loopback tests in `tests/Vulgata.Tests/WorkerAgentDispatcherTests.cs` for the fixed embedded prompt, exact ordered trace/current-version projections only, strict Business Logic schema, one configured endpoint, and no source hydration/discovery/fallback.
- [x] T041 [US3] After T040 is RED, author and embed `src/dotnet/Vulgata.Agents/Prompts/pass2-business-flow-worker.v1.md` via `src/dotnet/Vulgata.Agents/Vulgata.Agents.csproj`, then extend `src/dotnet/Vulgata.Agents/WorkerExecution/WorkerAgentDispatcher.cs`, `WorkerAgentJsonContext.cs`, and `WorkerAgentResultValidator.cs` for the Pass 2 variant; validation: the valid result is bounded, understandable without source, evidence-linked, and cannot request a narrative-derived base Edge.

### G4/G5 — Exact Pass 2 Commit and Lifecycle

- [x] T042 [US3] Add and run RED Pass 2 persistence tests in `tests/Vulgata.Tests/WorkerAgentResultCommitterTests.cs` for one atomic Business Logic aggregate with sections, Source Anchors, prepared flow evidence, retrieval projection, and `GENERATED_FROM` target set exactly equal to synthesis inputs; validation: extra/missing/stale targets reject atomically and narrative dependency text creates zero base Code Logic `REFERENCES` Edges.
- [x] T043 [US3] After T042 is RED, add Pass 2 `KnowledgeCommit` mapping to `src/dotnet/Vulgata.Infrastructure/Data/WorkerAgentResultCommitter.cs`; validation: existing Story 11.1 validation remains the sole atomic/authorization/Version Chain boundary.
- [x] T044 [US3] Add and run RED lifecycle tests in `tests/Vulgata.Tests/SequentialScanRunnerDocumentGenerationTests.cs` for `Pass1Completed` as observable nonterminal state, exactly one Pass 2 job, exact resolved input set, commit-before-`Completed`, and dispatch/validation/authorization/conflict/rejection/cancellation/failure remaining non-completed.
- [x] T045 [US3] After T044 is RED, extend `src/dotnet/Vulgata.Core/DomainServices/SequentialScanRunner.cs` with the one prepared Pass 2 lifecycle; validation: it requires all Pass 1 commits, resolves one manifest, dispatches/commits once, reaches `Completed` only on `Committed`, and contains no general flow planner, traversal, clustering, retry, or concurrent scheduling.
- [x] T046 [US3] Run the complete US3 suite in `tests/Vulgata.Tests/PreparedPass2FlowTests.cs`, `WorkerAgentContractTests.cs`, `WorkerAgentDispatcherTests.cs`, `WorkerAgentResultCommitterTests.cs`, and `SequentialScanRunnerDocumentGenerationTests.cs`; validation: AC-4/AC-5 and SC-003-SC-005 pass independently.

**Checkpoint**: US3 satisfies FR-009-FR-015 and the Pass 2 portions of FR-018-FR-019.

---

## Phase 6: G6 Production Composition and Security

**Purpose**: Make the accepted path reachable only after G0-G5 are green and prove the provider/security boundary without a live provider.

- [x] T047 Add and run RED DI/security tests in `tests/Vulgata.Tests/WorkerAgentProductionRegistrationTests.cs` that resolve `SequentialScanRunner`, production `IWorkerAgentDispatcher`, `IWorkerAgentResultCommitter`, committed-snapshot/provider/store seams, System override/global assignment rules, and scoped encrypted-key execution; validation: tests fail before registration and never contact a live endpoint.
- [x] T048 After T047 is RED and T004 is GREEN, register the accepted Agents/Infrastructure services and runner in `src/dotnet/Vulgata.Web/Program.cs`; validation: the scoped graph resolves, credentials remain outside packets/prompts/results/logs/exceptions/durable evidence, and no public API, UI, Chat Agent tool, retry/failover policy, scheduler, or alternate provider is registered.
- [x] T049 Run and refactor `tests/Vulgata.Tests/WorkerAgentProductionRegistrationTests.cs`; validation: G6 is GREEN, the configured self-hosted loopback is the only contacted boundary, and plaintext-credential/raw-diagnostic/absolute-path scans have zero hits.

---

## Phase 7: G7 Current-Boundary Integration and Final Verification

**Purpose**: Prove the accepted Story 11.3 boundary end to end without claiming Story 11.5 responsibilities.

- [x] T050 Add and run the RED current-boundary scenario in `tests/Vulgata.Tests/SequentialScanRunnerDocumentGenerationTests.cs` using one temporary Git Repository with a committed revision plus dirty/moved HEAD, prepared Code Units, caller Repository scope, encrypted test assignment, loopback provider, embedded manifest, and relational storage; validation: RED identifies the first unintegrated G0-G6 seam rather than weakening an assertion.
- [x] T051 After T050 is RED, make only bounded integration corrections in `src/dotnet/Vulgata.Core/DomainServices/SequentialScanRunner.cs`, `src/dotnet/Vulgata.Agents/WorkerExecution/`, `src/dotnet/Vulgata.Infrastructure/Data/WorkerAgentResultCommitter.cs`, and `src/dotnet/Vulgata.Web/Program.cs`; validation: exact committed slices, commit-before-progress, one Pass 2, exact `GENERATED_FROM`, no narrative base `REFERENCES`, bounded failures, and security capture are all observable GREEN.
- [x] T052 Verify the no-migration and scope guard with `src/dotnet/Vulgata.Infrastructure/Data/Migrations/`, `src/dotnet/Vulgata.Infrastructure/Data/Migrations/VulgataDbContextModelSnapshot.cs`, and `src/dotnet/Vulgata.Web/Api/`; validation: Story 11.3 changes none of these and adds no UI/API/Chat/Docker artifact. Stop as a blocker if a schema need is found.
- [x] T053 Run the focused filter from `specs/006-demo-document-generation/quickstart.md` against `tests/Vulgata.Tests/Vulgata.Tests.csproj`; validation: all Worker Agent, prepared Pass 2, sequential lifecycle, F003, persistence, authorization, and security tests pass with zero failures.
- [x] T054 Build `src/dotnet/Vulgata.Web/Vulgata.Web.csproj`; validation: .NET analyzers and enforced code style report zero errors.
- [x] T055 Run the full non-final-image suite in `tests/Vulgata.Tests/Vulgata.Tests.csproj`; validation: zero failures and no live provider call.
- [x] T056 Audit G0-G7 evidence against the coverage matrix in `specs/006-demo-document-generation/tasks.md`; validation: every FR-001-FR-020, SC-001-SC-006, and AC-1-AC-6 has at least one named observable test, all production behavior followed recorded RED-before-GREEN order, and deferred scope is not claimed complete.

---

## Dependencies and Execution Order

### Group Dependencies

```text
T001 fixtures
  -> G0 T002-T004 (F003 gate)
  -> G1 T005-T007 (closed Pass 1 contracts)
  -> G2 T008-T010 (exact hydration)
  -> G3 T011-T016 (Pass 1 adapter)
  -> G4 T017-T023 (Pass 1 commit before progress)
  -> US2 T024-T032 (fail-closed matrix)
  -> G5 T033-T046 (one prepared Pass 2)
  -> G6 T047-T049 (production DI/security; requires G0 GREEN)
  -> G7 T050-T056 (current-boundary integration/verification)
```

- Within every group: RED test task -> observed named failure -> production implementation -> focused GREEN/refactor.
- US2 depends on the US1 seams because it hardens their negative boundaries.
- US3 depends on committed Pass 1 evidence and the fail-closed boundaries.
- G6 is forbidden before G0 is GREEN.
- G7 begins only after G0-G6 are GREEN.

### Parallel Opportunities

None are declared. Different test files appear independent, but the accepted groups mutate shared union, dispatcher, committer, and runner contracts; sequential execution preserves observable TDD and avoids false RED/GREEN evidence.

---

## Requirement and Acceptance Coverage

| Authority | Observable task coverage |
| --- | --- |
| FR-001, FR-002; AC-1 | T005-T007, T011-T016, T023 |
| FR-003; AC-1/AC-2 | T008-T010, T024-T025, T050-T051 |
| FR-004; AC-2 | T005-T007, T026-T027, T030-T032 |
| FR-005; AC-3 | T017-T020, T023 |
| FR-006; AC-3 | T021-T023, T050-T051 |
| FR-007; AC-2 | T024-T032, T044-T046 |
| FR-008; AC-3 | T017-T023 |
| FR-009 | T021-T023, T044-T046 |
| FR-010, FR-011, FR-012; AC-4 | T033-T041, T044-T046 |
| FR-013, FR-014; AC-5 | T042-T046, T050-T051 |
| FR-015; AC-4 | T044-T046, T050-T051 |
| FR-016, FR-017; AC-6 | T011-T016, T026-T027, T047-T051 |
| FR-018 | T008-T010, T017-T020, T024-T032, T033-T046 |
| FR-019 | T012-T016, T035-T041 |
| FR-020; AC-6 | T002-T004, T047-T056 |
| SC-001 | T017-T023, T050-T051 |
| SC-002 | T024-T032, T050-T051 |
| SC-003 | T033-T046, T050-T051 |
| SC-004 | T038-T046, T050-T051 |
| SC-005 | T038-T046 |
| SC-006 | T024-T032, T047-T051 |

---

## Implementation Strategy

### MVP First

1. Complete setup and G0; do not expose production DI before F003 is GREEN.
2. Complete US1 G1-G4 and validate the source-grounded Pass 1 increment.
3. Complete US2 and validate zero-write/zero-progress isolation.
4. Complete US3 G5, then G6 composition and G7 integration.

### Bounded Deferrals

Do not add or claim: concurrency, retry/failover, partial-failure continuation, general tracing or clustering, general Cross-Repository Resolution, public UI/API/Chat, a multi-Repository scheduler, EF migration/model-snapshot work, the Story 11.5 clean-database two-Repository E2E, or final Docker verification.

## Completion Criteria

- All 56 tasks retain strict checklist format and execute in dependency order.
- Every production behavior has observable transient RED evidence before GREEN implementation.
- US1, US2, and US3 each meet their independent validation criteria.
- G0-G7, focused tests, Web build, full non-final-image suite, security checks, and no-migration guard are GREEN.
- FR-001-FR-020, SC-001-SC-006, and AC-1-AC-6 are fully covered without broadening the accepted boundary.
