---

description: "Dependency-ordered Story 18.3 tasks for native CodeGraph MCP against prepared source"
---

# Tasks: Use Native CodeGraph MCP Against Prepared Source

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [native boundary](contracts/native-codegraph-boundary.md), and [quickstart.md](quickstart.md) under `specs/18-3-use-native-codegraph-mcp-against-prepared-source/`.

**Prerequisites**: Plan-review-03 ACCEPT and planned-change authorization permit task generation. Production work still requires accepted complete-definition review and implementation approval. Story 18.1 is integrated; Story 18.2's shared trusted physical-workspace checkpoint is not yet verified. The standalone CodeGraph probe proves native transport on an owned fixture only.

**Verification routing**: Changed production behavior uses behavioral RED, minimal GREEN and final GREEN through the accepted public seam. Dependency pinning and host qualification use risk-appropriate direct verification. Existing-correct behavior needing coverage is test-only with passing tests and a coverage reason; do not manufacture RED. A test task below precedes its paired behavior task; final-shape assessment and surrounding tests follow each coherent group under governed-tdd.

**Task syntax**: `[P]` means different paths with no unfinished task dependency. `[USn]` maps to a specification journey. `ID-*` cites accepted plan implementation design; X/C IDs are rationale, not implementation obligations. `BLOCKED-18.2` is an execution predicate, not a completed task or a new approval gate.

## Phase 1: Setup

**Purpose**: Establish the declared native client dependency without claiming integration.

- [x] T001 Verify the compatible official MCP C# SDK/API against declared Agent Framework packages, pin its version in `Directory.Packages.props` and reference it from `src/dotnet/Vulgata.Infrastructure/Vulgata.Infrastructure.csproj`; use direct restore/build verification, not an artifact-hash test. [ID-I03] [ID-L01]

---

## Phase 2: Foundational boundary

**Purpose**: Story 18.1's approved source authority and three-function read seam are already integrated. No new shared foundation or Story 18.2 producer is created here; the Story 18.3 public seam belongs to User Story 1 below.

---

## Phase 3: User Story 1 — Explore the correct prepared Repository (P1)

**Goal**: Supply genuine native transport and a fixed Worker grant, then bind it to approved prepared source when the shared workspace exists.

**Independent test**: On an owned prepared fixture, observe native `codegraph_explore` discovery and useful source; after the Story 18.2 checkpoint, prove Vulgata uses the approved Repository and rejects a foreign project/path.

### Closed Worker seam — independent work

- [x] T002 [US1] Write behavioral RED tests for the accepted `ICodeGraphWorkerToolSession : IAsyncDisposable` and `CodeGraphFunctions` public seam in `tests/Vulgata.Tests/Agents/CodeGraphFunctionsTests.cs`: `DiscoverAsync(CancellationToken)` returns typed readiness, `ExploreAsync(string query, int maxFiles, CancellationToken)` returns typed sanitized result, `CreateAsync(CancellationToken)` exposes only one fixed `codegraph_explore` function on successful discovery, borrows the caller-owned session, and forwards cancellation. [ID-R01] [ID-R03] [ID-I04] [ID-L02] [ID-T01]
- [x] T003 [US1] Implement the minimal GREEN public contract and closed Agent wrapper in `src/dotnet/Vulgata.Core/DomainServices/CodeGraphWorkerToolContracts.cs` and `src/dotnet/Vulgata.Agents/WorkerExecution/CodeGraphFunctions.cs`; allow only nonempty UTF-8 `query` <=512 bytes and optional integral `maxFiles` 1–12 (default 4), reject `projectPath`, nested/unknown fields and malformed types before session dispatch, and expose no Story 18.1 fourth function. Recheck focused and surrounding tests after final-shape assessment. [ID-R01] [ID-R03] [ID-I02] [ID-I04] [ID-L02] [ID-T01]

### Native client on owned fixture — independent work

- [x] T004 [US1] Write behavioral RED tests of the Vulgata native client through the accepted `ICodeGraphWorkerToolSession` seam and an owned writable fixture in `tests/Vulgata.Tests/AnalysisRuntime/NativeCodeGraphMcpClientTests.cs`: discover only the reviewed `codegraph_explore` name/schema, perform one useful native call, report changed/missing schema and no-match distinctly, and never present a CLI-query wrapper or standalone probe as app readiness. This test-only fixture grants no production physical access. [ID-R02] [ID-R03] [ID-I03] [ID-S02] [ID-F01] [ID-L01] [ID-T02]
- [x] T005 [US1] Implement minimal GREEN local stdio native MCP initialize/list/call in `src/dotnet/Vulgata.Infrastructure/Analysis/CodeGraph/NativeCodeGraphMcpClient.cs`, using the declared executable and only reviewed `codegraph_explore`; parse/sanitize outcomes and keep CLI/JSON historical behavior separate. Do not add a source-root/session producer or run `codegraph init` against operator-prepared live source. Verify final shape and surrounding CodeGraph tests. [ID-R02] [ID-R03] [ID-I03] [ID-S02] [ID-A02] [ID-F01] [ID-L01] [ID-T02]

### Approved prepared-source integration — BLOCKED-18.2

- [ ] T006 [US1] BLOCKED-18.2: Once an identifiable verified Story 18.2 shared-workspace checkpoint exists, reconcile its callable construction, exact source identity, containment, lifetime and safe writable `.codegraph` placement into `specs/18-3-use-native-codegraph-mcp-against-prepared-source/plan.md` and this `tasks.md`; obtain affected independent review before the following physical tasks execute. If the contract cannot supply safe placement, return to plan instead of creating a parallel lease, writing the live root or copying source without authority. This task records/reconciles the dependency; it does not implement Story 18.2. [ID-I01] [ID-I05] [ID-S03] [ID-F04] [ID-T03]
- [ ] T007 [US1] BLOCKED-18.2 after T006/review: Write behavioral RED integration tests using the approved Story 18.2 workspace and exact Story 18.1 authority in `tests/Vulgata.Tests/AnalysisRuntime/NativeCodeGraphPreparedSourceTests.cs`; prove correct Repository-relative result, foreign-root/`projectPath` denial, unchanged operator-prepared live source, bounded source <=256 KiB, and no unapproved source copy. Use one binding-scoped session, not a mutable current project. [ID-R01] [ID-I01] [ID-I02] [ID-I05] [ID-S01] [ID-S03] [ID-F03] [ID-F04] [ID-T03]
- [ ] T008 [US1] BLOCKED-18.2 after T007 RED: Implement minimal GREEN trusted CodeGraph preparation/session producer and Web composition in `src/dotnet/Vulgata.Infrastructure/Analysis/CodeGraph/NativeCodeGraphSession.cs` and `src/dotnet/Vulgata.Web/Program.cs`. Consume the verified Story 18.2 workspace without JDT-LS coupling, bind exact `PreparedSourceApproval` and mandatory current verifier, fix native `projectPath` privately, place writable `.codegraph` state only as qualified, and release only contained Repository-relative results. No new grant, root registry or generic MCP framework. Assess final shape and rerun focused/surrounding tests. [ID-R01] [ID-R02] [ID-I01] [ID-I02] [ID-I03] [ID-I05] [ID-S01] [ID-S03] [ID-F03] [ID-F04] [ID-L02] [ID-T03]

**Checkpoint**: T002–T005 are independent after ordinary gates. T006–T008 require the verified Story 18.2 checkpoint plus affected plan/task reconciliation and independent review; a task description alone cannot clear that hold.

---

## Phase 4: User Story 2 — Know whether CodeGraph is usable (P2)

**Goal**: Keep required-tool readiness, authority loss, limits and process settlement truthful.

**Independent test**: Controlled sessions distinguish preparation, connection, discovery, invocation, no-match, incomplete/limited, authority denial/unavailability, timeout and cancellation; an independently authorized Story 18.1 file-tool call remains usable.

- [ ] T009 [US2] Write behavioral RED controlled-session tests in `tests/Vulgata.Tests/AnalysisRuntime/NativeCodeGraphSessionOutcomeTests.cs` using a controlled `ICodeGraphWorkerToolSession` and owned fixture transport for separate preparation/connection/discovery/invocation status, no-match versus incomplete, malformed/limited result, cancellation and expiry. Require one session <=10 calls/2 minutes, each call <=30 seconds, with counters independent of Story 18.1 read-context budgets; a CLI/index success is not ready. Do not simulate a production authority verifier or physical workspace. [ID-S02] [ID-A01] [ID-A02] [ID-F01] [ID-F02] [ID-F03] [ID-T01]
- [ ] T010 [US2] Implement minimal GREEN controlled timeout/status/result policy in `src/dotnet/Vulgata.Infrastructure/Analysis/CodeGraph/NativeCodeGraphMcpClient.cs` and `src/dotnet/Vulgata.Agents/WorkerExecution/CodeGraphFunctions.cs`: cap work/result, discard outputs on cancellation/expiry, reject untrusted server instructions, and never silently switch to file-only analysis. Keep Scan Control retry/pause outside this adapter; exact live authority integration remains for T011–T012. Assess final shape and rerun focused/surrounding tests. [ID-R02] [ID-I02] [ID-S02] [ID-A01] [ID-A02] [ID-F01] [ID-F02] [ID-F03] [ID-L02] [ID-T01]
- [ ] T011 [US2] BLOCKED-18.2 after T006/T008: Write behavioral RED integration tests at the approved physical seam in `tests/Vulgata.Tests/AnalysisRuntime/NativeCodeGraphPreparedSourceTests.cs` for exact current approval rechecks at session creation, operation start and before result disclosure; changed/revoked/unavailable authority with buffered output withheld; changed root, concurrent Repositories, owned-process cleanup, lost physical access, unsafe writable state and independently usable 18.1 file tools. Reconcile findings through plan if the owner contract cannot satisfy the accepted safety boundary; do not weaken tests or broaden grants. [ID-R01] [ID-I01] [ID-I05] [ID-S01] [ID-S03] [ID-A01] [ID-F02] [ID-F04] [ID-T03]
- [ ] T012 [US2] BLOCKED-18.2 after T011 RED: Implement minimal GREEN exact current-authority checks, changed-root/concurrent-root isolation and owned-process settlement in `src/dotnet/Vulgata.Infrastructure/Analysis/CodeGraph/NativeCodeGraphSession.cs`; withhold buffered source/locations on revoked/changed/unavailable approval, preserve the one-binding session and separate 18.1 file-tool availability, then assess final shape and rerun focused/surrounding tests. [ID-R01] [ID-I01] [ID-I05] [ID-S01] [ID-S03] [ID-A01] [ID-F02] [ID-F04] [ID-T03]

**Checkpoint**: Controlled outcome behavior is testable before Story 18.2's checkpoint. Real physical-source failure/cancellation behavior remains held until T006–T008.

---

## Phase 5: User Story 3 — Qualify the selected host capability (P3)

**Goal**: Separate native fixture transport, controlled Vulgata tests and real selected-host integration evidence.

**Independent test**: Record actual CodeGraph version and one production Agent discovery/invocation on an approved owned prepared fixture, including denied foreign-root and truthful failure/cancellation outcomes.

- [ ] T013 [US3] BLOCKED-18.2 after T006–T012: Execute the owned selected-host native smoke described in `specs/18-3-use-native-codegraph-mcp-against-prepared-source/quickstart.md` through Vulgata's production Agent-tool integration; record installed CodeGraph and MCP SDK versions, verified Story 18.2 workspace/index placement, real `codegraph_explore` result, relative source, correct/foreign-root behavior, authority loss, cancellation and owned cleanup. Direct runtime verification is required; existing standalone probe and controlled tests are not substitutes. Use the qualified launcher/permissions boundary and do not invoke a model/provider. [ID-R02] [ID-I03] [ID-S02] [ID-F01] [ID-T02] [ID-T04]

---

## Final Phase: Focused and surrounding verification

- [ ] T014 After each implemented behavior group, run focused and relevant surrounding tests in `tests/Vulgata.Tests/Vulgata.Tests.csproj` plus `dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj`; preserve RED/minimal-GREEN/final-GREEN evidence and final-shape assessment. For physical/host checks use `docs/agents/testing.md` and the qualified launcher; report restricted sandbox failure separately from product behavior. This is verification of accepted changes, not a new test-only requirement or claim that the standalone probe qualified Vulgata. [ID-T01] [ID-T02] [ID-T03] [ID-T04]

---

## Dependencies and Execution Order

- **Ordinary gates**: T001–T005/T009–T010 become implementable only after accepted complete-definition review and implementation approval. Their presence here is not execution authorization. T001 precedes T004/T005. T002 RED precedes T003 GREEN; T004 RED precedes T005 GREEN; T009 RED precedes T010 GREEN.
- **Shared-workspace hold**: T006 cannot close until Story 18.2 provides an identifiable verified shared trusted physical-workspace checkpoint with source identity, containment, lifetime and safe writable `.codegraph` placement. Root then reconciles affected plan/tasks and independent review. T007–T008/T011–T013 cannot execute before that holds-clear evidence; if the checkpoint is incompatible, return to plan rather than improvising an adapter.
- **Story completion**: US1 requires T002–T008; US2 requires T009–T012; US3 requires T013. Controlled independent slices may finish first, but neither US1 nor the whole story is complete before the blocked slices and final verification.
- **Parallel example**: Story 18.2's shared-workspace owner can progress in its own worktree while Story 18.3 executes T001–T005/T009–T010 after ordinary gates. Within Story 18.3, T002 → T003 establishes the accepted seam before T004 → T005 and T009 → T010; shared source paths make these groups sequential unless root assigns disjoint ownership. T006 reconciles Story 18.2's verified output before physical work joins it.

## Implementation Strategy

1. Establish the exact public seam and native client against controlled/owned fixtures; preserve truthful RED/GREEN and direct dependency/build checks.
2. Keep T006–T008/T011–T013 visibly pending while the Story 18.2 physical-workspace owner completes and verifies its checkpoint. Do not relabel the standalone probe as Vulgata qualification.
3. Reconcile the checkpoint through plan/tasks and independent review, then execute the dependent physical integration and host smoke under the ordinary approved boundary. Stop and return to plan if safe tool-state placement cannot be proved.
