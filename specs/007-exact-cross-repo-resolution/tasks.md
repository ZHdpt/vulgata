# Tasks: Exact Cross-Repository Resolution

**Input**: Accepted design set in `specs/007-exact-cross-repo-resolution/`

**Boundary**: Story `11-4-exact-rpc-http-cross-repository-demo-resolution`, source commit `67b5eaf45ff3c3348e240e323bed6f80c9e885e0`, accepted plan SHA-256 `286E3058F60FAAFC297D075DE9815C357899716C0AA8619C6D385490BF480224`, accepted review `specs/007-exact-cross-repo-resolution/reviews/plan-review/plan-review-iteration-3.json`

**Execution level**: `expedited-mvp`. Every production-behavior task is gated by an observable focused RED task in the same group. RED is transient; no failing test may be committed or handed to review.

**Organization**: Tasks are grouped into bounded RED -> GREEN groups and user-story phases. A fresh implementer may take one group only after its listed dependencies are complete; the sole root owns checkpoints, shared-file integration, review dispatch, staging, and commits.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: May run in parallel only with the explicitly named sibling group because files and prerequisites are disjoint.
- **[US1]**, **[US2]**, **[US3]**: User story traced from `spec.md`.
- Every task names exact repository paths and an observable validation criterion.

---

## Phase 1: Setup and Pinned Boundary

**Purpose**: Preserve the accepted planning boundary and establish the sole-root execution controls before any RED test or production edit.

- [ ] T001 Verify `git rev-parse HEAD`, `Get-FileHash specs/007-exact-cross-repo-resolution/plan.md -Algorithm SHA256`, and `git status --short` against the boundary recorded in `specs/007-exact-cross-repo-resolution/reviews/plan-review/plan-review-iteration-3.json`; validation: HEAD is `67b5eaf45ff3c3348e240e323bed6f80c9e885e0`, plan hash is `286E3058F60FAAFC297D075DE9815C357899716C0AA8619C6D385490BF480224`, and every pre-existing authorized planning/state path remains present and unmodified by implementation setup.
- [ ] T002 Assign one integration owner before edits to `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Configurations/SourceAnchorConfiguration.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Migrations/`, `src/dotnet/Vulgata.Infrastructure/Data/Migrations/VulgataDbContextModelSnapshot.cs`, and `src/dotnet/Vulgata.Web/Program.cs`; validation: the sole root records one owner for all five shared seams and no parallel group edits any of them without that owner's coordination.
- [ ] T003 Run the predecessor baseline `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~KnowledgeStore|FullyQualifiedName~SequentialScanRunner|FullyQualifiedName~ScanExecutionRepositoryConcurrencyTests|FullyQualifiedName~RepositoryProfilingTests"` and `dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj`; validation: both commands exit 0 before Story 11.4 tests are introduced, or the sole root stops and records the pre-existing failure without changing source.

---

## Phase 2: Foundational Test Fixture

**Purpose**: Create test-only, disposable infrastructure shared by all vertical groups. This phase adds no production behavior.

- [ ] T004 Add bounded disposable two-Repository Git/SQLite fixtures, deterministic IDs/timestamps, authorization access probes, safe log capture, exact Pass 1 Document Version/Source Anchor builders, and cleanup helpers in `tests/Vulgata.Tests/ExactCrossRepositoryTestFixtures.cs`; validation: the helper compiles in `tests/Vulgata.Tests/Vulgata.Tests.csproj`, creates only unique temporary roots/databases, never touches the actual prepared Repositories, and exposes counters that can prove zero Git/source/query/write calls.

**Checkpoint 0**: Sole root confirms T001-T004, preserves the dirty planning boundary, and releases only G1. No production file has changed.

---

## Phase 3: User Story 1 - Resolve One Exact Custom-RPC Path (Priority: P1)

**Goal**: Bind the reviewed profile to two authorized prepared Repository executions at their exact Profiled Revisions and record exactly one cross-Repository `REFERENCES` Edge with complete typed evidence.

**Independent Test**: With two explicit authorized bindings, exact tagged Git fixtures, durable `Pass1Completed` executions, current Code Logic Document Versions/Source Anchors, and the reviewed `cust/updateSession` source/XML shapes, run the resolver and assert exactly one consumer-to-provider `REFERENCES` Edge, one complete evidence row, no inferred Edge, and exact replay without duplication.

### G1 - Reviewed Profile and Runtime Contracts

- [ ] T005 [US1] Write focused failing profile/runtime contract tests in `tests/Vulgata.Tests/ExactResolutionProfileTests.cs` for fixed-resource loading, exact reviewed JSON bytes and fingerprint, canonical ordering, the two full tag/commit bindings, bounded closed values, complete runtime alias bindings, distinct Repositories in one System, and absence of database IDs/Git URLs/credentials/environment or absolute paths; validation: `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter FullyQualifiedName~ExactResolutionProfileTests` fails for the missing Story 11.4 contracts/catalog rather than fixture or compilation defects unrelated to the intended behavior.
- [ ] T006 [US1] Implement provider-independent profile, selector, runtime binding, candidate, decision-batch, closed outcome/read-model, exact-Git lease/port, validator, and fingerprint contracts in `src/dotnet/Vulgata.Core/DomainServices/ExactResolutionContracts.cs`, `src/dotnet/Vulgata.Core/DomainServices/IExactCrossRepositoryResolver.cs`, and `src/dotnet/Vulgata.Core/DomainServices/IExactCrossRepositoryResolutionStore.cs`; validation: Core has no EF/provider dependency, exposes no `IQueryable`/`DbSet`, bounds every collection/string, and T005 advances past missing-contract failures.
- [ ] T007 [US1] Implement strict fixed-resource loading in `src/dotnet/Vulgata.Infrastructure/CrossRepository/ExactResolutionProfileCatalog.cs`, add the reviewed bytes at `src/dotnet/Vulgata.Infrastructure/CrossRepository/Profiles/demo-resolution-profile.v1.json`, and embed that exact resource in `src/dotnet/Vulgata.Infrastructure/Vulgata.Infrastructure.csproj`; validation: T005 is GREEN, the embedded bytes match `specs/007-exact-cross-repo-resolution/contracts/demo-resolution-profile.v1.json`, unknown/duplicate/trailing/over-depth/oversized/noncanonical inputs are rejected, and callers cannot supply a replacement path/resource/selector.

### G2 - Isolated Exact Git Revision Acquisition

- [ ] T008 [P] [US1] Write focused failing exact-Git tests in `tests/Vulgata.Tests/ExactGitRevisionAcquisitionTests.cs` for annotated/lightweight full-ref peeling, exact 40-character commit equality, operation-private lease identity, exact-then-normal acquisition, and concurrent ordinary/exact acquisition preserving the ordinary checkout path/attached branch/HEAD/origin/index/clean status; validation: the focused filter fails because `IExactGitRevisionSnapshotService` behavior is absent while ordinary `IGitWorkingCopyService.AcquireAsync` fixture assertions remain GREEN. Parallel with G3 after G1.
- [ ] T009 [P] [US1] Implement operation-private exact-object acquisition and idempotent lease cleanup in `src/dotnet/Vulgata.Infrastructure/Git/ExactGitRevisionSnapshotService.cs` without changing `src/dotnet/Vulgata.Infrastructure/Git/GitWorkingCopyService.cs`; validation: T008 is GREEN, the returned Profiled Revision is the full configured commit, all writes stay beneath a unique Vulgata-owned `.exact-revisions` operation root, and ordinary acquisition works before/during/after the exact lease. Parallel with G3 after G1.

### G3 - Positive Deterministic Candidate Extraction

- [ ] T010 [P] [US1] Write focused failing positive extraction tests in `tests/Vulgata.Tests/ExactResolutionExtractionTests.cs` for the reviewed line-45 `netTools.execute` direct literal at argument index 1, line-2 root `trans@name`, bounded Repository-relative locators, exact provider implementation type mapping, one current consumer/provider Document Version and Source Anchor, DTD-disabled XML, and finite file/candidate limits; validation: the focused filter fails only because the closed extractor behavior is absent. Parallel with G2 after G1.
- [ ] T011 [P] [US1] Implement the bounded lexical/XML extractors and exact current-owner mapping in `src/dotnet/Vulgata.Infrastructure/CrossRepository/ExactCandidateExtractors.cs`; validation: T010 is GREEN, only profile-listed files are read, XML resolution is disabled, source text is never returned, XML declaration evidence stays distinct from the provider implementation Source Anchor, and no directory walk or LLM/System-context input exists. Parallel with G2 after G1.

### G4 - Exact Match, Normalization, and Direction

- [ ] T012 [US1] Write focused failing resolver tests in `tests/Vulgata.Tests/ExactCrossRepositoryResolverTests.cs` for Form-C plus ASCII-edge normalization, removal of exactly one provider-side `in/` prefix, ordinal equality, configured consumer-to-provider direction/target binding, one eligible provider cardinality, confidence `1.0000`, and complete decision ordering; validation: the focused filter fails because exact matching/decision construction is absent after G2 and G3 are GREEN.
- [ ] T013 [US1] Implement direction-qualified exact matching and profile-ordered decision construction in `src/dotnet/Vulgata.Infrastructure/CrossRepository/ExactCrossRepositoryResolver.cs`; validation: T012 is GREEN, `cust/updateSession` and `in/cust/updateSession` yield one Exact Match only in the configured direction, no Repository-global provider/consumer state is introduced, and narrative/LLM/System context is not consulted.

### G5 - Exact Edge Evidence, Atomic Store, and Replay

- [ ] T014 [US1] Write focused failing exact-persistence tests in `tests/Vulgata.Tests/ExactCrossRepositoryPersistenceTests.cs` for one atomic `DocumentEdge`/`ExactCrossRepositoryEdgeEvidence` write, owner-safe consumer/provider Source Anchor tuples, required internal relational identities, complete safe evidence text, deterministic IDs/fingerprints, identical full-row replay as `AlreadyApplied`, drift as `Conflict`, ordered owner locks, current-pointer revalidation, and whole-batch rollback; validation: the focused filter fails because the exact entity/configuration/store does not exist, not because G1-G4 regressed.
- [ ] T015 [US1] Add the exact evidence entity and EF model in `src/dotnet/Vulgata.Core/Entities/ExactCrossRepositoryEdgeEvidence.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Configurations/ExactCrossRepositoryEdgeEvidenceConfiguration.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Configurations/SourceAnchorConfiguration.cs`, and `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs` under the integration owner; validation: SQLite model assertions in T014 prove the one-to-one restricted Edge FK, both composite Source Anchor FKs, differing Repository check, closed protocol/resolution/confidence/schema/hash constraints, unique evidence fingerprint, and additive Source Anchor alternate key.
- [ ] T016 [US1] Implement complete-batch validation, UUID-ordered owner locking, currentness/ownership/Profiled Revision revalidation, atomic exact Edge/evidence insert, byte-for-byte replay verification, and conflict rollback in `src/dotnet/Vulgata.Infrastructure/Data/ExactCrossRepositoryResolutionStore.cs`; validation: T014 is GREEN on SQLite, an exact write cannot partially survive, timestamps do not change on replay, and the existing `IKnowledgeStoreRepository.CommitAsync` contract remains unchanged.
- [ ] T017 [US1] Run `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~ExactResolutionProfileTests|FullyQualifiedName~ExactGitRevisionAcquisitionTests|FullyQualifiedName~ExactResolutionExtractionTests|FullyQualifiedName~ExactCrossRepositoryResolverTests|FullyQualifiedName~ExactCrossRepositoryPersistenceTests"`; validation: all US1 tests exit 0 and the sole root records the GREEN command/result before releasing US2 groups.

**Checkpoint 1 - US1**: The reviewed positive occurrence is independently testable and replay-safe. G2 and G3 may have run in parallel, but the sole root integrates them before G4. Do not claim a safe release yet: US2 fail-closed behavior and US3 authorization/migration/integration remain mandatory.

---

## Phase 4: User Story 2 - Fail Closed on Inexact or Ambiguous Evidence (Priority: P1)

**Goal**: Refuse invalid binding/revision/source evidence before graph truth, and persist only bounded canonical nonterminal Uncertainty for eligible anchored diagnostic outcomes.

**Independent Test**: Run the accepted invalid-binding, moved/missing ref, variable/disallowed call, malformed/wrong-root XML, mismatch, zero/multiple provider, stale/multiple owner, replay-drift, and unsupported file/storage matrix; assert zero false Edges, zero writes for pre-evidence refusal, and exactly one safe replay-stable Uncertainty only where an authorized current Source Anchor exists.

### G6 - Pre-Evidence Binding and Exact-Git Refusal

- [ ] T018 [P] [US2] Extend `tests/Vulgata.Tests/ExactResolutionProfileTests.cs` and `tests/Vulgata.Tests/ExactGitRevisionAcquisitionTests.cs` with focused failing cases for unknown/duplicate/trailing profile members, missing/duplicate/extra/reused/cross-System bindings, provenance mismatch, unsafe refs/paths, moved/missing/similarly named tags, abbreviated/wrong commits, remote/archive/identity failure, cancellation, concurrent lease isolation, and cleanup failure; validation: focused filters observe RED in the missing refusal/cleanup branches and probes prove zero source/store access. Parallel with G7 after US1.
- [ ] T019 [P] [US2] Implement bounded indistinguishable pre-evidence refusal, exact end-to-end revision equality, cancellation/process-tree drain, operation-local cleanup, and sanitized failure categories in `src/dotnet/Vulgata.Infrastructure/CrossRepository/ExactResolutionProfileCatalog.cs` and `src/dotnet/Vulgata.Infrastructure/Git/ExactGitRevisionSnapshotService.cs`; validation: T018 is GREEN, failures return no usable lease/source/write, one lease cannot delete another, and diagnostics/logs contain no ref contents, Git URL, credentials, host/private paths, raw command, or stderr. Parallel with G7 after US1.

### G7 - Inexact Extraction and Ambiguous Matching

- [ ] T020 [P] [US2] Extend `tests/Vulgata.Tests/ExactResolutionExtractionTests.cs` and `tests/Vulgata.Tests/ExactCrossRepositoryResolverTests.cs` with focused failing cases for comments, symbols/variables/computed/concatenated/template targets, wrong call/file/argument/line/ordinal, malformed escapes/XML, DTD/external entities, missing/duplicate `name`, traversal/symlink/reparse/out-of-root/over-bound files, missing/repeated/empty/control/oversized provider prefix results, unmatched/normalization mismatch, zero/multiple providers, and zero/multiple/stale Document/Source Anchor mappings; validation: every fixture is RED only where the corresponding fail-closed branch is missing and no negative fixture yields an exact decision. Parallel with G6 after US1.
- [ ] T021 [P] [US2] Complete fail-closed lexical/XML extraction, mapping, normalizer, and cardinality branches in `src/dotnet/Vulgata.Infrastructure/CrossRepository/ExactCandidateExtractors.cs` and `src/dotnet/Vulgata.Infrastructure/CrossRepository/ExactCrossRepositoryResolver.cs`; validation: T020 is GREEN, all accepted negative fixtures produce zero exact Edges, same identifiers in both Repositories cannot self/reverse-match, and Pass 1 narrative/LLM/System context cannot create or strengthen match truth. Parallel with G6 after US1.

### G8 - Canonical Uncertainty, Unsupported Observation, and Atomic Failure

- [ ] T022 [US2] Extend `tests/Vulgata.Tests/ExactCrossRepositoryPersistenceTests.cs` with focused failing tests for canonical `unresolved/{cross_repo|unknown_target|ambiguous}` outcomes, all closed reason codes, the reviewed owner-anchored `file_storage`/`unsupported_kind` observation, zero unsupported Edge/evidence rows, future-resolution eligibility, exact `AlreadyRecorded` replay, drift conflict, stale/currentness race, unique/FK/check failure, and complete-batch rollback; validation: the focused filter observes RED because Uncertainty persistence is absent and proves binding/authorization/revision/owner-mapping failures never call the store.
- [ ] T023 [US2] Add canonical Uncertainty behavior in `src/dotnet/Vulgata.Core/Entities/Uncertainty.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Configurations/UncertaintyConfiguration.cs`, `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`, and `src/dotnet/Vulgata.Infrastructure/Data/ExactCrossRepositoryResolutionStore.cs` under the integration owner; validation: T022 is GREEN, automatic writes are nonterminal `unresolved`, `sub_status` is restricted to `cross_repo|unknown_target|ambiguous`, `wontfix` is never automatic, the reviewed unsupported observation remains later-resolvable, and no failure leaves a partial or duplicate row.
- [ ] T024 [US2] Run `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~ExactResolutionProfileTests|FullyQualifiedName~ExactGitRevisionAcquisitionTests|FullyQualifiedName~ExactResolutionExtractionTests|FullyQualifiedName~ExactCrossRepositoryResolverTests|FullyQualifiedName~ExactCrossRepositoryPersistenceTests"`; validation: all US1 and US2 tests exit 0 and the sole root records the GREEN command/result before releasing US3 groups.

**Checkpoint 2 - US2**: Every accepted inexact, ambiguous, or unsupported case creates zero relationship truth; only authorized current anchored diagnostics may create one bounded replay-safe Uncertainty.

---

## Phase 5: User Story 3 - Preserve Direction, Authorization, and Source Isolation (Priority: P2)

**Goal**: Support occurrence-local direction in either configured Repository direction, enforce both-endpoint authorization before all access/write/return, keep internal PK/FKs private, and expose only safe authorized evidence or the metadata-free restricted marker.

**Independent Test**: Run reviewed and synthetic reverse-direction occurrences with both, one, or neither endpoint authorized; assert correct direction without permanent role classification, zero unauthorized Git/source/query/write, opaque authorized relationship identity plus safe metadata only, and identity-free/metadata-free `RestrictedRelationship` otherwise.

### G9 - Both-Endpoint Authorization, Retrieval, and Leakage Boundary

- [ ] T025 [P] [US3] Write focused failing authorization/security tests in `tests/Vulgata.Tests/ExactCrossRepositoryAuthorizationTests.cs` for both-System/Repository visibility before Git/source/query/write, stale scope recheck inside the store/retrieval transaction, opaque authorized `relationshipIdentity`, safe exact evidence projection, marker-only `RestrictedRelationship`, indistinguishable unauthorized/missing Uncertainty retrieval, required internal relational PK/FKs, and scans of profile/text columns/logs/exceptions/diagnostics/returned models for forbidden IDs/source/credentials/URLs/absolute paths/business metadata; validation: the focused filter observes RED at the absent authorization/read-model boundaries and access probes remain capable of detecting any early call. Parallel with G10 after US2.
- [ ] T026 [P] [US3] Implement authorized exact evidence/read-model contracts and two-endpoint projection in `src/dotnet/Vulgata.Core/DomainServices/KnowledgeStoreContracts.cs`, `src/dotnet/Vulgata.Infrastructure/Data/KnowledgeStoreRepository.cs`, and `src/dotnet/Vulgata.Infrastructure/Data/ExactCrossRepositoryResolutionStore.cs`; validation: T025 is GREEN, authorized serialization exposes only opaque relationship identity plus safe endpoint/profile/Evidence Locator metadata, internal Repository/ScanRun/Document Version/Source Anchor PK/FKs remain relational only, and unauthorized results contain only the existing identity-free metadata-free marker. Parallel with G10 after US2.

### G10 - Additive Migration Lifecycle

- [ ] T027 [P] [US3] Write focused failing migration/model tests in `tests/Vulgata.Tests/ExactCrossRepositoryMigrationTests.cs` for exactly two additive tables, the Source Anchor alternate key, owner-safe composite FKs, restricted deletes, closed checks, unique fingerprints/indexes, allowed migration operation types, rollback exactly to `20260801033655_AddSequentialScanRunner`, predecessor row/schema preservation, reapply equivalence, and Story 11.4 DbSets; validation: the focused filter is RED because the Story 11.4 migration is absent. Parallel with G9 after US2.
- [ ] T028 [P] [US3] Under the integration owner, generate the `AddExactCrossRepositoryResolution` migration in `src/dotnet/Vulgata.Infrastructure/Data/Migrations/` and update `src/dotnet/Vulgata.Infrastructure/Data/Migrations/VulgataDbContextModelSnapshot.cs` from the accepted configurations without hand-edit drift; validation: T027 is GREEN on SQLite, `Down` removes only Story 11.4 schema, and all predecessor entities/data remain intact. Parallel with G9 after US2.
- [ ] T029 [P] [US3] Run the uniquely named disposable PostgreSQL apply -> verify -> rollback to `20260801033655_AddSequentialScanRunner` -> verify predecessor preservation -> reapply lane through `tests/Vulgata.Tests/ExactCrossRepositoryMigrationTests.cs`, then run `dotnet ef migrations has-pending-model-changes --project src/dotnet/Vulgata.Infrastructure --startup-project src/dotnet/Vulgata.Web`; validation: both commands exit 0, the disposable database is removed after terminating its connections, and no shared/production database is targeted. Parallel with G9 after T028.

### G11 - Authorized Post-Pass-1 Current-Boundary Integration

- [ ] T030 [US3] Write the focused failing current-boundary integration in `tests/Vulgata.Tests/ExactCrossRepositoryIntegrationTests.cs` covering `WaitingForPass1`, two explicit durable `Pass1Completed` bindings, configured full revision equality through every artifact, reviewed exact occurrence, reviewed unsupported observation, authorized trace, marker-only unauthorized trace, exact/unsupported replay, synthetic reverse direction, same-name direction disambiguation, complete negative matrix, exact concurrent with ordinary acquisition, unchanged queued System-context behavior, and production DI registration; validation: the filter observes RED because coordinator/composition is absent after G9/G10 are GREEN.
- [ ] T031 [US3] Implement explicit two-binding `ResolveAfterPass1Async` orchestration, ManagementAccess/SystemOwner-filter binding, readiness/current Pass 1 checks, authorization before acquisition, exact snapshot/extract/resolve/store sequencing, bounded outcomes, and cancellation in `src/dotnet/Vulgata.Web/Data/ExactCrossRepositoryResolutionCoordinator.cs`; validation: coordinator-focused cases in T030 are GREEN, the first incomplete execution returns `WaitingForPass1` with zero access/write, and the coordinator does not enumerate a System, schedule scans, run Pass 2, call an Agent/LLM/MCP service, or change `SequentialScanRunner` phases.
- [ ] T032 [US3] Register the reviewed profile catalog, isolated exact Git service/root, resolver, store, and coordinator only through `src/dotnet/Vulgata.Web/Program.cs` under the integration owner; validation: production-composition assertions in T030 resolve every specific service, ordinary `IGitWorkingCopyService` behavior/registration is unchanged, and no public API/UI/Agent tool is added.
- [ ] T033 [US3] Run `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~ExactCrossRepositoryAuthorizationTests|FullyQualifiedName~ExactCrossRepositoryMigrationTests|FullyQualifiedName~ExactCrossRepositoryIntegrationTests"`; validation: all US3 tests exit 0, the current-boundary fixture records exactly one exact Edge/evidence plus one nonterminal unsupported Uncertainty and zero unsupported Edges, and no result is claimed as Story 11.5's final prepared-Repositories/Docker proof.

**Checkpoint 3 - US3 / Reviewed Candidate**: Sole root integrates G9 and G10, then G11, pins the complete changed-path boundary, and records transient RED plus GREEN evidence for every group. No staging or commit occurs until the repository workflow owner performs its later checkpoint action.

---

## Phase 6: Cross-Cutting Security, Regression, Review, and Handoff

**Purpose**: Verify the full accepted Story 11.4 boundary without broadening into Story 11.5.

- [ ] T034 [P] Run the security/static boundary inspection over `src/dotnet/Vulgata.Core/`, `src/dotnet/Vulgata.Infrastructure/CrossRepository/`, `src/dotnet/Vulgata.Infrastructure/Git/ExactGitRevisionSnapshotService.cs`, `src/dotnet/Vulgata.Infrastructure/Data/ExactCrossRepositoryResolutionStore.cs`, `src/dotnet/Vulgata.Web/Data/ExactCrossRepositoryResolutionCoordinator.cs`, `src/dotnet/Vulgata.Infrastructure/CrossRepository/Profiles/demo-resolution-profile.v1.json`, and captured outputs from `tests/Vulgata.Tests/ExactCrossRepositoryAuthorizationTests.cs`; validation: zero Core EF types, Repository-name/commit production conditionals, serialized internal owner IDs in forbidden text surfaces, plaintext source, credentials, Git URLs, absolute host/WSL/container paths, provider diagnostics, or unauthorized relationship metadata. Parallel with T035 after T033.
- [ ] T035 [P] Run all focused Story 11.4 tests via `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~ExactResolution|FullyQualifiedName~ExactGitRevisionAcquisitionTests|FullyQualifiedName~ExactCrossRepository"`, then `dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj` and `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj`; validation: every command exits 0 with no analyzer warning/error, predecessor regression, nondeterministic duplicate, or deliberately failing deferred test. Parallel with T034 after T033.
- [ ] T036 Validate `specs/007-exact-cross-repo-resolution/quickstart.md` sections 1-8 against the implemented current boundary, run `git diff --check`, scan changed files with `rg -n "T[O]DO|T[B]D|F[I]XME|NEEDS CLARIFICATIO[N]|\[FEATURE NAM[E]\]|TXX[X]"`, and list raw changed paths with `git status --short`; validation: all applicable Story 11.4 commands/predicates pass, no unresolved placeholder remains, whitespace check exits 0, only authorized Story 11.4 implementation/tests plus preserved planning/state edits are present, and Story 11.5-only gates are explicitly unclaimed.
- [ ] T037 At the sole-root reviewed-candidate checkpoint, hand the pinned changed-path boundary plus RED/GREEN, security, migration, current-boundary integration, build, and regression evidence to the fresh combined quality review required by `docs/agent-workflow/august-15-expedited-mvp-exception.md`; validation: review covers AC-1..AC-7, FR-001..FR-024, SC-001..SC-008, security/migration/regression/known limitations and returns raw ACCEPT before later workflow completion, while this task performs no lifecycle-state update, staging, commit, final Docker build, or actual prepared-Repositories clean-database rehearsal.

---

## Dependencies and Execution Order

### Group Dependency Graph

```text
G0 Setup/Foundation (T001-T004)
  -> G1 Profile/contracts (T005-T007)
       -> G2 Exact Git positive (T008-T009) ---------+
       -> G3 Extraction positive (T010-T011) -------+-> G4 Match/direction (T012-T013)
                                                        -> G5 Exact persistence (T014-T017)
                                                             -> US1 checkpoint
                                                                  -> G6 Binding/Git refusal (T018-T019) --+
                                                                  -> G7 Extraction/match refusal (T020-T021)+-> G8 Uncertainty/rollback (T022-T024)
                                                                                                               -> US2 checkpoint
                                                                                                                    -> G9 Authorization/retrieval (T025-T026) --+
                                                                                                                    -> G10 Migration (T027-T029) --------------+-> G11 Coordinator/integration (T030-T033)
                                                                                                                                                                   -> G12 Verification/review (T034-T037)
```

### Phase Dependencies

- **Setup/Foundation**: no dependency; blocks all behavior work.
- **US1**: G1 first; G2 and G3 may run in parallel; G4 requires both; G5 requires G4.
- **US2**: requires the US1 checkpoint; G6 and G7 may run in parallel; G8 requires both.
- **US3**: requires the US2 checkpoint; G9 and G10 may run in parallel under the integration-owner rule; G11 requires both.
- **Cross-cutting verification**: requires G11 GREEN; T034 and T035 may run in parallel, then T036 and T037 run in order.

### RED -> GREEN Invariants

- G1: T005 RED precedes T006-T007 GREEN.
- G2: T008 RED precedes T009 GREEN.
- G3: T010 RED precedes T011 GREEN.
- G4: T012 RED precedes T013 GREEN.
- G5: T014 RED precedes T015-T016 GREEN; T017 closes the group.
- G6: T018 RED precedes T019 GREEN.
- G7: T020 RED precedes T021 GREEN.
- G8: T022 RED precedes T023 GREEN; T024 closes the group.
- G9: T025 RED precedes T026 GREEN.
- G10: T027 RED precedes T028-T029 GREEN.
- G11: T030 RED precedes T031-T032 GREEN; T033 closes the group.
- No production behavior may move earlier than its paired RED task, and no group may hand off a failing test.

### Parallel Examples

- **US1**: after G1, one implementer may execute T008-T009 in `tests/Vulgata.Tests/ExactGitRevisionAcquisitionTests.cs` and `src/dotnet/Vulgata.Infrastructure/Git/ExactGitRevisionSnapshotService.cs` while another executes T010-T011 in `tests/Vulgata.Tests/ExactResolutionExtractionTests.cs` and `src/dotnet/Vulgata.Infrastructure/CrossRepository/ExactCandidateExtractors.cs`; the sole root integrates both before T012.
- **US2**: after T017, G6 owns profile/Git files while G7 owns extractor/resolver files; both finish RED->GREEN before T022 touches persistence.
- **US3**: after T024, G9 owns authorization/retrieval files while the integration owner alone runs G10 over EF/migration/snapshot files; both finish before T030.
- **Final verification**: T034 is read-only security inspection and T035 is test/build execution; they may run concurrently after T033 because neither writes repository files.

---

## Implementation Strategy

### MVP Ordering

1. Complete Setup/Foundation and G1.
2. Complete G2 and G3, then G4 and G5 to produce the independently testable US1 positive path.
3. Stop and validate US1; this is the smallest demonstrable exact-resolution increment, not a releasable security boundary.
4. Complete US2 before trusting the demonstration: false-Edge refusal is co-equal P1 behavior.
5. Complete US3 before review: authorization, migration, and source isolation are mandatory even though US3 is P2.
6. Complete cross-cutting verification and combined review. A release-ready Story 11.4 candidate requires all three user stories; there is no authorized shortcut that ships US1 alone.

### Fresh-Implementer / Sole-Root Rules

- A fresh implementer receives exactly one G1-G11 group, its accepted paths, and its prerequisite GREEN evidence.
- The implementer observes and reports the focused RED before editing production, then reports the focused GREEN and raw changed paths.
- The implementer never stages, commits, updates BMad/workflow state, edits accepted planning/contracts/profile, or touches another group's files.
- The sole root resolves overlaps, owns T002 shared files, records checkpoints, pins the candidate, and dispatches combined review.

---

## Requirements and Acceptance Coverage

### Functional Requirements

| Requirement | Behavior tasks | Verification |
| --- | --- | --- |
| FR-001 | T030-T032 | T033, T035 |
| FR-002 | T005-T007 | T017, T034 |
| FR-003 | T005, T007 | T025, T034 |
| FR-004 | T005-T007, T018-T019 | T024, T025, T033 |
| FR-005 | T005, T007-T009, T018-T019 | T017, T024, T033 |
| FR-006 | T018-T019, T025-T026, T030-T031 | T024, T033, T034 |
| FR-007 | T010-T011, T020-T021 | T024, T033 |
| FR-008 | T010-T011, T020-T021 | T024, T033 |
| FR-009 | T012-T013, T020-T021 | T017, T024 |
| FR-010 | T012-T013, T020-T021, T030 | T033 |
| FR-011 | T012-T016 | T017, T033 |
| FR-012 | T014-T016, T025-T026 | T017, T033, T034 |
| FR-013 | T018-T023 | T024, T033 |
| FR-014 | T020-T023 | T024, T034 |
| FR-015 | T014-T016, T022-T023, T030 | T017, T024, T033 |
| FR-016 | T012-T013, T030-T031 | T033, T034 |
| FR-017 | T012-T013, T030-T031 | T033 |
| FR-018 | T012-T013, T020-T021, T030 | T024, T033, T034 |
| FR-019 | T030-T031 | T033, T035 |
| FR-020 | T025-T026, T030-T031 | T033, T034 |
| FR-021 | T014-T016, T019, T023, T025-T026 | T033, T034 |
| FR-022 | T022-T023, T030-T031 | T024, T033 |
| FR-023 | T005-T007, T010-T013 | T017, T034 |
| FR-024 | T030-T033 | T036-T037 |

### Success Criteria

| Criterion | Behavior tasks | Verification |
| --- | --- | --- |
| SC-001 | T012-T016, T030-T032 | T033, T035 |
| SC-002 | T012-T013, T030-T031 | T033 |
| SC-003 | T018-T023, T030 | T024, T033 |
| SC-004 | T014-T016, T025-T026 | T017, T033 |
| SC-005 | T014-T016, T022-T023, T030 | T017, T024, T033 |
| SC-006 | T005-T009, T014-T016, T019, T023, T025-T026 | T029, T033-T035 |
| SC-007 | T022-T023, T030-T031 | T024, T033 |
| SC-008 | T012-T016, T025-T026, T030-T032 | T033, T034 |

### Story Acceptance Criteria

| Criterion | Behavior tasks | Verification |
| --- | --- | --- |
| AC-1 | T005-T009, T018-T019, T025, T030-T031 | T017, T024, T033 |
| AC-2 | T010-T016, T025-T026, T030-T032 | T017, T033 |
| AC-3 | T018-T023 | T024, T033-T034 |
| AC-4 | T012-T013, T030-T031 | T033-T034 |
| AC-5 | T020-T021, T030-T031 | T024, T033, T035 |
| AC-6 | T019, T025-T026, T030-T032 | T033-T034 |
| AC-7 | T022-T023, T030-T031 | T024, T033 |

---

## Known Limitations and Explicit Non-Goals

- This story resolves only the reviewed configured custom-RPC occurrence and one reviewed file/storage unsupported observation. It does not implement general HTTP/RPC discovery, MQ, page navigation, indirect object flow, general file/storage resolution, or Service Topology MCP.
- Provider endpoint and consumer dispatch candidates are transient. Persistent general `communication_endpoints`, `dispatch_sites`, candidate catalogs, advanced Uncertainty resolution/reclassification, and a general multi-Repository scheduler remain backlog.
- The coordinator accepts exactly two explicit prepared bindings after durable Pass 1. It does not enumerate Systems/Repositories, change `SequentialScanRunner` phases, schedule/retry/deadlock multi-Repository work, or run Pass 2.
- No public API, UI, Worker Agent/Chat Agent tool, prompt, LLM call, or System-context match rule is added. Existing queued System-context behavior remains unchanged.
- Story 11.4 uses disposable Git fixtures and current-boundary relational integration only. Story 11.5 still owns the actual prepared-Repositories clean PostgreSQL E2E, complete profiling/Pass 1/Pass 2/retrieval/chat/citation path, final Docker image build, and release verification; Story 11.4 evidence cannot substitute for those gates.
- No task may mutate, check out, stage, or commit either external prepared Repository, and no task may stage or commit the Vulgata worktree on behalf of the task author.

---

## Task Summary

- **Total**: 37 tasks (`T001`-`T037`)
- **Setup/Foundation**: 4 tasks
- **US1**: 13 tasks (`T005`-`T017`)
- **US2**: 7 tasks (`T018`-`T024`)
- **US3**: 9 tasks (`T025`-`T033`)
- **Cross-cutting verification/review**: 4 tasks (`T034`-`T037`)
- **Groups**: G0=4, G1=3, G2=2, G3=2, G4=2, G5=4, G6=2, G7=2, G8=3, G9=2, G10=3, G11=4, G12=4
- **Parallel opportunities**: G2 with G3, G6 with G7, G9 with integration-owner G10, and read-only T034 with T035
