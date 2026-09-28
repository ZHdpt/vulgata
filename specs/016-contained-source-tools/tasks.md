---
description: "Dependency-ordered implementation tasks for Story 18.1"
---

# Tasks: Contained Prepared Source Tools

**Input**: `specs/016-contained-source-tools/spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/prepared-source-tools.md`, and `quickstart.md`.

**Prerequisites**: Accepted architecture-classified plan and its planned Worker-tool authorization. Production execution still requires the separate complete-definition review and implementation approval.

**Tests**: Changed production behavior follows governed behavioral RED, minimal GREEN and final-shape verification at the accepted Core issuer/read and Agent tool seams. Fixture preparation and contract declarations use direct build/fixture checks; existing-correct Git-bound behavior uses regression tests without fabricated RED.

**Organization**: Tasks are ordered by the three specification user stories. Test code under `tests/Vulgata.Tests/PreparedSource/` belongs to the test author; production code under `src/dotnet/` belongs to the implementer. Neither owner changes the other's files. Task IDs are dependency order, not permission to bypass the workflow gates.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Different files and no dependency on an incomplete task.
- **[Story]**: US1, US2 or US3 from `spec.md`.
- Exact repository-relative file paths appear on each checklist line.

## Path Conventions

- Domain contracts: `src/dotnet/Vulgata.Core/DomainServices/`.
- Source authority and filesystem adapter: `src/dotnet/Vulgata.Infrastructure/Source/`.
- Thin Worker functions: `src/dotnet/Vulgata.Agents/WorkerExecution/`.
- Owned-directory behavioral tests: `tests/Vulgata.Tests/PreparedSource/`; existing Git-bound regression tests stay in `tests/Vulgata.Tests/RetainedSource/`.

## Phase 1: Setup (Shared Test Infrastructure)

**Purpose**: Make direct tests repeatable with small owned prepared trees, a sibling outside the grant and a controllable in-memory current-authority verifier. This is fixture setup, not a production approval source.

- [x] T001 Create `tests/Vulgata.Tests/PreparedSource/PreparedSourceFixture.cs` with owned temporary trees, unknown-extension UTF-8, long/empty/invalid-text files, sibling sentinel, controllable exact-tuple approval/revocation/unavailability and deterministic cleanup; directly verify fixture ownership/cleanup without DB, Git acquisition, model, CodeGraph or LSP. (ID-T01)

---

## Phase 2: Foundational (Contract and Caps)

**Purpose**: Establish the accepted public seam so subsequent RED tests can target it; no production source operation is implemented here.

- [x] T002 Define the Core grant, `List`, `ReadTextRange`, `SearchText`, current-authority verifier, request/result/location/outcome and disposable-context contracts in `src/dotnet/Vulgata.Core/DomainServices/PreparedSourceContracts.cs`; encode `data-model.md` constraints: “Batch ID | Current operator-approved batch scope”, “Repository ID | One approved Repository”, “Subject and purpose | Approved caller identity and Worker-source-read purpose”, “Approval identity/epoch | Exact currently approved grant generation”, “Opaque root-binding identity | Approval's exact prepared-root binding without exposing its host path”, “Operator label | Descriptive prepared-input label”, “Physical root and held root identity | Directory object allowed for this grant”, “Limits/deadline | Cumulative operations, metadata entries, examined/returned bytes and time”, and “Lifetime | Open, authority-closed, expired/cancelled or disposed”; expose only the contract's closed relative arguments and sanitized outcomes; directly verify the Core project builds. (ID-I01, ID-I02, ID-S01)

**Checkpoint**: Core contracts compile; no prepared-source behavior has been claimed.

---

## Phase 3: User Story 1 - Inspect an approved Repository (Priority: P1) 🎯 MVP

**Goal**: One trusted approved grant can list/read a held root while foreign bindings, host paths, links and writes cannot reach outside it.

**Independent Test**: Use the production issuer/read seam against one owned tree and its sibling with a controllable approved tuple. Permitted relative list/read succeed; foreign approval and escape attempts disclose no outside metadata or bytes.

### Behavioral Tests for User Story 1

- [x] T003 [US1] Write public-seam RED tests in `tests/Vulgata.Tests/PreparedSource/PreparedSourceGrantTests.cs` that construct `PreparedSourceContextIssuer(IPreparedSourceCurrentAuthorityVerifier)` with the T001 in-memory verifier and call `IssueAsync(PreparedSourceGrantRequest, CancellationToken)`; assert exact batch/Repository/subject/Worker-source-read purpose/approval/root-binding checks, operator label as label, missing-verifier/null-request programmer errors, and typed `PreparedSourceIssueResult` invariant (`Issued` iff one nonnull `IPreparedSourceReadContext`; every failure has null context). Cover sanitized `Denied`, `AuthorityUnavailable`, `RootUnavailable`, `UnsupportedPlatform`, `Cancelled` and `DeadlineExceeded` where the owned platform/fixture can exercise them, plus no raw path/approval detail and closure of any root handle opened before failed issuance; record RED before T004. (ID-R01, ID-I01, ID-T01)
- [x] T004 [US1] Implement minimal GREEN `PreparedSourceContextIssuer(IPreparedSourceCurrentAuthorityVerifier)` and `IssueAsync(PreparedSourceGrantRequest, CancellationToken)` in `src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceContextIssuer.cs`, with public typed `PreparedSourceIssueStatus`/`PreparedSourceIssueResult` from `contracts/prepared-source-tools.md`; reject missing verifier/null request as programmer errors, bind exact approval identity/epoch, opaque root-binding identity, label, held root, caps and deadline, return one nonnull Core context only for `Issued`, map revoked/malformed to sanitized `Denied`, verifier failure to `AuthorityUnavailable`, missing/unreadable root to `RootUnavailable`, unqualified platform to `UnsupportedPlatform`, cancellation/deadline to their statuses, and close any acquired root handle on failure. Provide no always-allow production verifier or model root selector; rerun T003. (ID-R01, ID-I01, ID-S01)
- [x] T005 [US1] Write RED owned-Windows-fixture tests for canonical relative paths, sibling/absolute/drive/UNC/ADS/device/alias rejection, root reparse components, in-root and escaping links, and replacement/retarget races in `tests/Vulgata.Tests/PreparedSource/PreparedSourceContainmentTests.cs`; assert no outside bytes/metadata or sentinel changes and mark unsupported platform setup unqualified; record RED before T006. (ID-F01, ID-T02)
- [x] T006 [US1] Implement minimal GREEN held-root, handle-relative no-follow child traversal and rejection of every symlink/reparse component in `src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceTraversal.cs`; never use lexical-prefix check-then-open as the safety boundary, fail closed where an equivalent adapter is unavailable, and rerun T005. (ID-R02, ID-F01)
- [x] T007 [US1] Write RED public-context tests for immediate root listing and one bounded file read, Repository-relative locations, unfamiliar extension discoverability, non-traversable link metadata and no admission decision in `tests/Vulgata.Tests/PreparedSource/PreparedSourceBasicReadTests.cs`; record RED before T008. (ID-R03, ID-I01, ID-T01)
- [x] T008 [US1] Implement minimal GREEN list and single-range read on the trusted held root in `src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceReadContext.cs`; keep root/handles private, return the bound Repository ID and canonical relative locations, never emit a Profiled Revision/hash/physical path or admission label, and leave existing `src/dotnet/Vulgata.Core/DomainServices/RetainedSourceContracts.cs` behavior intact; rerun T007. (ID-R02, ID-R03, ID-L01)
- [x] T009 [US1] Have each owner assess its affected final test/production units in `tests/Vulgata.Tests/PreparedSource/PreparedSourceGrantTests.cs`, `tests/Vulgata.Tests/PreparedSource/PreparedSourceContainmentTests.cs`, `tests/Vulgata.Tests/PreparedSource/PreparedSourceBasicReadTests.cs`, `src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceContextIssuer.cs`, `src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceTraversal.cs` and `src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceReadContext.cs`; correct only concrete local defects while GREEN and rerun focused US1 tests after final shape. (ID-T01, ID-T02)

**Checkpoint**: An independently testable approved-root MVP exists; no paging/search or later failure breadth is claimed.

---

## Phase 4: User Story 2 - Continue bounded discovery (Priority: P2)

**Goal**: Listing, ranged reading and literal search expose precise locations, finite limits and truthful continuations across a prepared tree.

**Independent Test**: Follow list/search pages and read ranges over the fixture until completion; compare every location and result with fixture ground truth, including unknown extensions, UTF-8 multibyte text and complete-empty outcomes.

### Behavioral Tests for User Story 2

- [ ] T010 [US2] Write RED tests for immediate-child ordinal listing, page size 1–100, complete/complete-empty/limited states, one-use same-scope continuation, changed-entry invalidation and cumulative metadata/operation limits in `tests/Vulgata.Tests/PreparedSource/PreparedSourceListTests.cs`; record RED before T011. (ID-I02, ID-I04, ID-S02, ID-T03)
- [ ] T011 [US2] Implement minimal GREEN bounded list ordering and context-local cursor state in `src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceReadContext.cs`; enforce exact approval/operation/path/page-size binding, observed identity/metadata, one-use tokens and explicit exhausted-limit reports without durable cursor/tree copy/hash; rerun T010. (ID-R02, ID-I04, ID-S02)
- [ ] T012 [US2] Write RED tests for zero-based UTF-8 byte offsets, positive `byteCount` 1–65,536, strict whole-slice decoding, initial BOM, code-point split/invalid-range, empty-file or EOF complete-empty, beyond-EOF invalid-range, returned-byte caps and `nextByteOffset` in `tests/Vulgata.Tests/PreparedSource/PreparedSourceRangeReadTests.cs`; record RED before T013. (ID-I03, ID-I02, ID-T03)
- [ ] T013 [US2] Implement minimal GREEN bounded streaming text ranges in `src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceReadContext.cs` and `src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceText.cs`; enforce strict UTF-8, embedded-NUL/binary unsupported-text, accurate `[byteStart, byteEndExclusive)`, finite examined/returned bytes and remaining-range/limit information; rerun T012. (ID-I03, ID-A02, ID-R02)
- [ ] T014 [US2] Write RED tests for case-sensitive ordinal literal search of a nonempty single-line query at most 256 UTF-8 bytes, unknown-extension files, path-then-byte ordering, zero-based byte spans, 1-based Unicode-scalar line/column, CRLF, 1–64 match pages, one-use continuation, complete no-match and explicit limits in `tests/Vulgata.Tests/PreparedSource/PreparedSourceSearchTests.cs`; record RED before T015. (ID-I03, ID-I04, ID-T03)
- [ ] T015 [US2] Implement minimal GREEN bounded incremental literal search in `src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceReadContext.cs` and `src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceText.cs`; traverse regular files regardless of extension, stream rather than preload whole files/trees, preserve exact match locations and same-scope cursor, and account for examined/returned byte caps; rerun T014. (ID-R02, ID-I03, ID-I04, ID-A02)
- [ ] T016 [US2] Write RED public-seam tests in `tests/Vulgata.Tests/PreparedSource/PreparedSourceToolFunctionTests.cs` that construct `PreparedSourceFunctions(IPreparedSourceReadContext)` with an issued or controlled context, call `Create()` and invoke exactly three `Microsoft.Extensions.AI.AIFunction`s in order `list_prepared_source`, `read_prepared_source`, `search_prepared_source`; assert null context rejection, exact closed argument schemas, sanitized denied/no Core call for unknown or malformed JSON, alternate batch/Repository/root selectors and write/command fields, unchanged `PreparedSourceResult` and `CancellationToken` forwarding for valid calls, caller-owned disposal and sanitized `Cancelled` after disposal; record RED before T017. (ID-I01, ID-L02, ID-T01)
- [ ] T017 [US2] Implement minimal GREEN `PreparedSourceFunctions(IPreparedSourceReadContext).Create()` in `src/dotnet/Vulgata.Agents/WorkerExecution/PreparedSourceFunctions.cs`, returning exactly the ordered three `Microsoft.Extensions.AI.AIFunction`s over the borrowed context; reject null context, expose only accepted closed arguments, map schema-invalid calls to sanitized denied without Core dispatch, forward valid requests/cancellation unchanged to Core and return its `PreparedSourceResult` unchanged, leave context disposal with the trusted caller and never reopen after disposal; add no fourth tool, `CreateSession`, registry, filesystem policy, retry or Worker scheduling; rerun T016. (ID-I01, ID-L02)
- [ ] T018 [US2] Have both owners assess final affected units in `tests/Vulgata.Tests/PreparedSource/PreparedSourceListTests.cs`, `tests/Vulgata.Tests/PreparedSource/PreparedSourceRangeReadTests.cs`, `tests/Vulgata.Tests/PreparedSource/PreparedSourceSearchTests.cs`, `tests/Vulgata.Tests/PreparedSource/PreparedSourceToolFunctionTests.cs`, `src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceReadContext.cs`, `src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceText.cs` and `src/dotnet/Vulgata.Agents/WorkerExecution/PreparedSourceFunctions.cs`; preserve behavior during any bounded GREEN refactor and rerun focused US1+US2 tests. (ID-T03)

**Checkpoint**: All three bounded operations and closed Agent function shapes are independently observable with honest coverage states.

---

## Phase 5: User Story 3 - Understand interruption and source limits (Priority: P3)

**Goal**: Revocation, verifier failure, source gaps, cancellation and budgets end in distinct sanitized results without post-stop disclosure or leaked resources.

**Independent Test**: With the same production context and owned fixture, revoke/change/fail approval before and during operations, interrupt work at different points, vary source availability and race budgets; assert exact outcomes, no buffered unauthorized payload and settled activity.

### Behavioral Tests for User Story 3

- [ ] T019 [US3] Write RED tests that revoke or change any exact grant tuple field, or make the verifier unavailable, before a call, between entries/chunks/pages and immediately before release in `tests/Vulgata.Tests/PreparedSource/PreparedSourceAuthorityLifecycleTests.cs`; assert denied versus authority-unavailable, discarded buffered entries/counts/text/matches/tokens, all concurrent calls closed, invalidated continuations and fresh-grant requirement after restoration; record RED before T020. (ID-R01, ID-S01, ID-A01, ID-F02, ID-F03, ID-T03)
- [ ] T020 [US3] Implement minimal GREEN awaited current-authority checks at issuance, operation start, between bounded units and immediately before every release, with shared closed latch and cursor invalidation in `src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceContextIssuer.cs` and `src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceReadContext.cs`; revoke/change => denied, verifier unavailable/error => authority-unavailable, both empty/no continuation and settled handles; rerun T019. (ID-S01, ID-A01, ID-F02, ID-F03)
- [ ] T021 [US3] Write RED tests for missing, permission/I/O unreadable, invalid UTF-8/binary, invalid range, observed source change, partial unreadable list/search and sanitized error payloads in `tests/Vulgata.Tests/PreparedSource/PreparedSourceFailureTests.cs`; ensure complete-empty only after full authorized inspection and no physical path/subject/approval/source fragment leaks; record RED before T022. (ID-I02, ID-F02, ID-S02, ID-T03)
- [ ] T022 [US3] Implement minimal GREEN separate complete, complete-empty, limited, incomplete, denied, authority-unavailable, missing, unreadable, unsupported-text, invalid-range, changed-input, cancelled and deadline-exceeded result mapping in `src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceReadContext.cs`; partial source I/O returns relative limitation, detected drift invalidates affected cursor, malformed path/token denial does not needlessly close a healthy grant, and no automatic retry/fallback hides failure; rerun T021. (ID-I02, ID-F02, ID-F03, ID-S02)
- [ ] T023 [US3] Write RED tests for cancellation during enumeration/read/search, deadline/disposal, concurrent calls near operation/metadata/examined/returned caps and post-stop handle settlement in `tests/Vulgata.Tests/PreparedSource/PreparedSourceCancellationBudgetTests.cs`; assert no over-budget or post-cancellation disclosure and 100 operations, 1,000 metadata entries, 8 MiB examined, 1 MiB returned, 30-second grant maxima with per-call 100 entries, 64 KiB returned text, 64 matches and 1 MiB examined; record RED before T024. (ID-S01, ID-A01, ID-A02, ID-T03)
- [ ] T024 [US3] Implement minimal GREEN linked cancellation/deadline/lifetime, atomic cumulative reservations, finite per-call caps and disposal of owned iterators/streams/handles in `src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceReadContext.cs`; let trusted issuance lower but never raise accepted maxima, report explicit stopping dimension, and rerun T023. (ID-S01, ID-A01, ID-A02, ID-F02)
- [ ] T025 [US3] Have both owners assess final affected test/production units in `tests/Vulgata.Tests/PreparedSource/PreparedSourceAuthorityLifecycleTests.cs`, `tests/Vulgata.Tests/PreparedSource/PreparedSourceFailureTests.cs`, `tests/Vulgata.Tests/PreparedSource/PreparedSourceCancellationBudgetTests.cs`, `src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceContextIssuer.cs` and `src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceReadContext.cs`; correct concrete local defects while GREEN and rerun all focused prepared-source tests after final shape. (ID-T03)

**Checkpoint**: The complete direct prepared-source contract has current-authority, containment, truthful failure and resource-settlement evidence.

---

## Phase 6: Polish & Cross-Cutting Verification

**Purpose**: Verify the accepted boundary without adding product behavior or unrelated infrastructure.

- [ ] T026 Run the `specs/016-contained-source-tools/quickstart.md` build and focused prepared-source tests plus selected existing `tests/Vulgata.Tests/RetainedSource/RetainedSourceReadContextTests.cs`, `tests/Vulgata.Tests/RetainedSource/RetainedSourceRangeReadTests.cs` and `tests/Vulgata.Tests/RetainedSource/RetainedSourceCaptureTests.cs` Git-bound regressions; record exact commands, OS/fixture privileges, final assembly boundary, current-authority timing, Windows link/race qualification and any blocked prerequisite, then inspect final source-only diff for accidental Git/copy/hash/retention/DB/provider/CodeGraph/LSP/UI/CLI work. (ID-L01, ID-T02, ID-T03)

---

## Dependencies & Execution Order

### Phase Dependencies

- Setup T001 precedes behavioral fixture tests. Foundational contract T002 precedes all Core-seam behavioral tests.
- Within each user story, each numbered RED test precedes its immediately following minimal GREEN task. Preserve raw RED, minimal GREEN and final GREEN evidence; final-shape assessment follows GREEN.
- US2 depends on US1's grant, containment and basic read context. US3 depends on the complete list/read/search surface so it can exercise all operation lifetimes. T026 follows all three story checkpoints.
- T019–T024 may expose a defect in an earlier slice; route correction to the responsible test or production owner without changing the accepted contract in `specs/016-contained-source-tools/plan.md`.

### User Story Dependencies

- **US1 (P1)**: T001–T002, then T003–T009. Independent approved-root MVP.
- **US2 (P2)**: US1, then T010–T018. Adds continuation, strict ranges, search and Agent function projection.
- **US3 (P3)**: US2, then T019–T025. Qualifies live authority, error truthfulness, cancellation and caps.

### Parallel Opportunities

- No checklist task is marked `[P]`: the accepted root/context and single test project create ordered shared seams. During a coherent GREEN slice, the test author can inspect test shape while the implementer inspects production shape in disjoint files; coordinate final reruns to avoid shared build-output locks.
- Existing Git-bound regression review can be prepared independently, but the T026 command runs after the final prepared-source shape so its result applies to the candidate boundary.

## Parallel Example: User Story 1

No independent checklist tasks are dispatched concurrently in US1. After T004 reaches GREEN, the test author can assess `tests/Vulgata.Tests/PreparedSource/PreparedSourceGrantTests.cs` while the implementer assesses `src/dotnet/Vulgata.Infrastructure/Source/PreparedSourceContextIssuer.cs`; both report through root before the final focused run. T005 then starts the next RED slice.

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete T001–T002 with direct fixture/build checks.
2. Complete US1's RED/GREEN slices and final-shape check T003–T009.
3. Demonstrate one approved-root list/read and denied foreign/escape access through the production seam; do not infer paging/search or full workflow qualification from this MVP.

### Incremental Delivery

1. Complete US2's list, range, search and Agent-wrapper RED/GREEN slices, then verify their final shape.
2. Complete US3's current-authority and interruption slices, then verify their final shape.
3. Run T026 on the final candidate and report controlled test evidence separately from later real Worker/model integration.

## Notes

- Production contract filenames above are task ownership locations; private helper decomposition may remain local while preserving the accepted interfaces and invariants.
- No task authorizes a new Scan admission strategy, database or production approval source. The current-authority seam uses controlled in-memory approval for direct Story 18.1 tests; later batch integration supplies its production approval source.
- A failed or unqualified Windows containment fixture must be reported as such. Existing Git-bound tests may have prerequisites; skipped or blocked checks are not passes.
- Root alone handles lifecycle, evidence, approvals, checkpoints and Git. Workers do not stage or commit.
