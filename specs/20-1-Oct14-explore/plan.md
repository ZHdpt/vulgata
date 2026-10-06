# Implementation Plan: OCT-EXPLORE

**Branch**: `oct14/explore` | **Date**: 2026-10-06 | **Spec**: [spec.md](spec.md)

**Input**: `specs/20-1-Oct14-explore/spec.md`

**Status**: Design ready for independent review; no tasks or implementation authorized.

## Summary

Deliver an ordinary-user exact reader and bounded browse first, then a small manually refreshed graph of accepted Knowledge. Reuse Blazor/Fluent and a native SVG presentation; consume OCT-BUSINESS shared contracts without direct database/source access. Integration owns selection and shared Web composition. Chat and graph use one exact destination.

The actual branch was switched and verified as `oct14/explore`; HEAD and required planning baseline are `fad7f11c9d6bc9ed3da1b371b24beb9f5c09db5d`, with ancestry exit 0. The earlier `57fad124` source baseline is included through these planning inputs. A clean worktree was observed before drafting. This is a new feature, not resumed governed stories.

Every SpecKit helper uses process-local `SPECIFY_FEATURE_DIRECTORY=C:\Users\13728\.codex\worktrees\bd91\vulgata\specs\20-1-Oct14-explore`. The helper's JSON `BRANCH=20-1-Oct14-explore` is its feature-selection label; `git branch --show-current` is the actual Git evidence. Only this worktree's `.specify/feature.json` is selected. Optional after-specify/after-plan agent-context hooks and the plan's root-agent update step are skipped by explicit user instruction. No other skill is invoked.

## Technical Context

**Language/Version**: Existing C#/.NET 10 and Blazor Interactive Server.

**Primary Dependencies**: Existing Fluent UI, CommunityToolkit.Mvvm, Identity and FluentValidation. No new graph, Markdown, JavaScript framework or package dependency. Use Razor-encoded structured text initially; no raw untrusted HTML.

**Storage**: None owned by Explore. OCT-BUSINESS owns October Knowledge persistence/migrations and shared read/authorization ports. Ordinary Web reads through those ports using integration's frozen DefaultConnection composition.

**Testing**: Existing single xUnit/WebApplicationFactory project, suite inventory and `eng/test.ps1`; controlled component/projection tests plus explicitly scoped PostgreSQL and browser integration qualification later.

**Target Platform / Project Type**: Existing modular monolith Web host and desktop browser; no new host/service/project.

**Performance Goals**: Bounded work rather than an invented latency SLA: at most 40 displayed nodes, 80 Edges, 20 browse items/page; graph acquisition at most 8 shared read calls and a 10-second cancellation deadline. Shared service caps may tighten these. Ordinary related retrieval is one-hop over the displayed seed page; no recursive closure.

**Constraints**: Exact publication/version/section, live authorization on every disclosure, no implicit latest fallback, Business BL-only before projection/serialization, coherent replacement, safe error/diagnostic output.

**Exact-content bound (F3 correction)**: Business limits each text/JSON projection response to 64 KiB UTF-8. Reader fetches one owner-defined chunk per explicit open/next action and retains only the currently displayed chunk, its permitted qualification context and opaque next cursor. No automatic whole-Document drain or accumulated content cache; graph call/output budgets remain graph-only.

**Scale/Scope**: One small source-backed registration/login case crossing prepared Repositories, later the explicitly selected slice. Repository names/counts never determine eligibility. No native tools or provider calls belong to this lane.

## Constitution Check

| Principle / gate | Pre-design | Post-design evidence |
| --- | --- | --- |
| I: Layered ownership | Pass | Knowledge-owned Core/shared contracts; Explore projection in Web feature service, presentation-only Shared/ViewModels; no EF/source tool access. |
| II: Domain purity/exact immutable Documents | Pass | Read-only exact versions/sections and accepted Edges; no schema or mutable wiki projection. |
| III: Validation/API/UI | Pass | FluentValidation for feature input; Chinese Fluent/Library Oak and LoadState. Initial Blazor in-process facade adds no REST endpoint; any future endpoint must preserve ProblemDetails/401/403 conventions. |
| IV: Honest verification | Pass for planning | No production tests claimed. Future behavior uses meaningful RED/GREEN, separate test/production ownership and explicit suite classification; fixtures do not prove real generation or PostgreSQL. |
| V: Security/runtime authority | Pass | Live shared policy before any content/metadata, deny on outage, no arbitrary links/scripts, no runtime resources authorized now. |
| Workflow choice | User override recorded | Direct specify/plan and independent review only; no governed runner, tasks, implementation or agent-context changes. Compatible integrity and approval gates retained. |
| Shared contracts | Implementation dependency open | B0 compiled checkpoint absent at planning baseline. Design fixes required semantics, not invented shared signatures or a claim of readiness. |

No unresolved product clarification prevents this design. Shared contract publication and runtime composition are explicit implementation prerequisites rather than silently filled-in APIs. Broader PRD requirements are preserved as uncompleted scope below.

## Project Structure

### Documentation (this feature)

`spec.md`, `checklists/requirements.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/consumer-boundary.md`, `quickstart.md`, and the independent `plan-review.md`. No `tasks.md` is generated.

### Source Code (repository root)

Proposed new files, not existing implementation:

| Owner | Paths / responsibilities |
| --- | --- |
| OCT-EXPLORE | `src/dotnet/Vulgata.Shared/Explore/ExplorePresentationModels.cs`, `IExplorePresentationService.cs`, `ExploreInputValidators.cs`: bounded presentation inputs/results and UI facade. They wrap owner-approved handle representations; never redefine Knowledge identity or authorization. |
| OCT-EXPLORE | `src/dotnet/Vulgata.Web.ViewModels/Explore/DocumentReaderViewModel.cs`, `DocumentBrowseViewModel.cs`, `KnowledgeGraphViewModel.cs`: LoadState, cancellation, request-generation checks and presentation state; keep existing Shared-only reference graph. |
| OCT-EXPLORE | `src/dotnet/Vulgata.Web/Explore/ExplorePresentationService.cs`, `KnowledgeGraphProjector.cs`, `ExactDocumentLinkBuilder.cs`, `ExploreModule.cs`: trusted subject/mode/selection binding, owner-service calls, final disclosure validation, bounded projection, link encoding and feature-local registration method. |
| OCT-EXPLORE | `src/dotnet/Vulgata.Web/Components/Pages/Explore/{DocumentsPage,DocumentReaderPage,GraphPage}.razor` and scoped code/CSS; `Components/Explore/{DocumentSections,KnowledgeGraph,ExploreStatus}.razor`; colocated `KnowledgeGraph.razor.js` only for local pointer/viewport work if needed. No JS Knowledge fetch/cache. |
| OCT-EXPLORE | `tests/Vulgata.Tests/Explore/`: focused reader/link/projection/race/component/authorization tests and feature integration cases; existing harness, no new test project. |
| OCT-BUSINESS | New shared Knowledge Core contracts/DTOs, representative fixture and live Identity/System/Repository/all-contributor policy. Actual names/paths are published at B0; Explore consumes those files unchanged. |
| OCT-BUSINESS | `src/dotnet/Vulgata.Infrastructure/Data/October/OctoberDbContext.cs`, `Data/October/Migrations/**` and snapshot, persistence/read implementations. Also any change to existing `Core/DomainServices/KnowledgeStoreContracts.cs` or `IKnowledgeStoreRepository.cs`. No Explore edits. |
| Integration owner | `src/dotnet/Vulgata.Web/Program.cs`, shared module wiring, `Components/Routes.razor`, `Components/Layout/{MainLayout,ManagementLayout}.razor`, appsettings/launch/deployment/package files and shared theme assets. |
| Integration owner | Existing `Components/Pages/Management/{DocumentsPage,GraphPage}.razor`: remove/replace placeholders with links to new ordinary routes, preserving management shell authorization. Never relax ManagementLayout as a workaround. |
| Integration owner coordinates | `tests/Vulgata.Tests/TestInfrastructure/suites.json`: Explore supplies exact inventory additions with its test increment; merge serially, without replacing another lane's entries. Existing project/harness files remain shared. |
| OCT-CHAT | `Components/Pages/ChatPage*`, turn/footer/reference presentation and dialogue behavior. Consume the Explore link builder/destination; do not create a second reader or edit Chat from Explore. |
| OCT-SCAN | Generator, native CodeGraph/JDT/source preparation and Scan strategy. No changes by Explore. |

**Structure Decision**: Keep existing project references. Web feature services can consume Core while ViewModels depend on presentation-only Shared types. Shared DTOs are derived, transient presentation results, never independently minted semantic IDs, persistence records or authorization decisions.

## Required contracts and checkpoint

See [consumer-boundary.md](contracts/consumer-boundary.md). Before dependent production implementation, OCT-BUSINESS must publish one compiling checkpoint on `oct14/main` containing agreed identity, exact reads with owner-defined opaque content continuation, bounded discovery/related reads, live ordinary-reader authorization and a labelled shared fixture. Minimal compile/conformance probes may validate that initial shared contract before dependent production wiring; they use owner types and are not a consumer DTO fork or permission to implement during this planning correction. Record its commit/type paths when supplied; none is available or invented now. All lanes consume the same checkpoint. Integration brings it into this assigned branch with authorization; planning does not merge branches.

Checkpoint acceptance requires tests demonstrating exact version/section membership, wrong publication rejection, BL-only metadata, composite-contributor revocation/outage, accepted Edge endpoint/direction semantics and publication-coherent pages. The fixture includes permitted CL/BL, multiple versions/sections, accepted REFERENCES/GENERATED_FROM, an Uncertainty and a denied contributor. A reusable application service port is required; existing GetCurrentAsync, optional live-verifier scopes and operator-only IOctoberBatchSelector do not satisfy it.

F3 adds owner-fixture cases for a >64-KiB Document and an individual >64-KiB section, multibyte text and qualified claims across chunk boundaries, explicit terminal continuation, and tampered/expired/wrong-context cursors. Initial conformance must demonstrate bounded lossless ordered reading and live authorization on later chunks. The named Business planning contract is reference evidence, not a claim that B0 compiled types have arrived.

If B0 cannot expose an authorized bounded relationship read or final disclosure recheck, request an owner amendment. Never fetch all rows then locally hide them, synthesize missing DTOs/schemas, wrap current-version reads, or use fixtures as runtime fallback. Contract change needs the owner and all consumers to adopt the same small checkpoint; semantic/security changes reopen independent review.

## Small usable increments

These are implementation sequencing boundaries, not generated tasks or permission to execute.

| Increment | Prerequisites | Usable result / verification | Exact stop |
| --- | --- | --- | --- |
| E0: exact reader and browse with controlled data | Subsequent implementation authorization; shared compiled B0 contracts/fixture; integration agrees destination and feature registration seam | Ordinary IT browses/opens exact CL/sections, Business gets honest empty/BL states. Meaningful controlled tests for exactness, missing targets, authorization, safe markup, races and keyboard basics. | Stop backend wiring if checkpoint semantics absent; no consumer contract fork. Fixture route/test host stays labelled and cannot be production fallback. |
| E1: real CL then BL reader | B0 real accepted-file/publication store and ordinary-reader authority; integration opt-in read composition/owned target; Scan publishes a real file | Same reader opens persisted real CL without manager role, then BL when B2 arrives. Wrong batch/publication, revoked contributor and no-wiki startup behave correctly. | No claim of real reader delivery until actual persisted publication is read. Missing BL never delays useful IT CL, never causes Business CL substitution. |
| E2: bounded accepted graph | E0 reader; shared accepted-related contract/fixture; E1 for real-data proof | One bounded seed page and one-hop relationships; typed/directed SVG, list alternative, pan/zoom/fit/select/open/manual refresh; counts/limits/qualifications. Test cap boundaries, denial noninterference, cycles, refresh races and exact links. | No substitute edges, source CodeGraph or inferred BL dependencies; insufficient relationship contract returns unavailable and goes to Business. |
| E3: combined case qualification | Business B1/B2 real relationships/BL, Scan's real publication, Chat exact references, integration-owned combined Web/configuration/resources | Same selected publication for Chat/graph/reader; ordinary user CL/BL, cross-Repository accepted Edge and genuine Uncertainty; browser keyboard/theme checks and scoped store/end-to-end evidence. | Missing real generation/Chat/permissions/runtime evidence leaves integration incomplete. Owner distributes defects; Explore changes only its files. |

E2 fixture work can overlap E1 producer delivery once E0 is usable. Reader does not wait for Chat or broad batch coverage; graph delivery does not gate Chat. Keep small reviewable increments and separate controlled tests from real-data evidence.

E0 includes one-chunk reader loading/next/restart behavior and the oversized Document/section controlled cases; E1 repeats the owner continuation seam against persisted data. These are corrections to the reader increment, not a new pagination framework or graph increment.

## Detailed behavior

- New ordinary routes: `/knowledge/documents`, `/knowledge/documents/read`, `/knowledge/graph`. Reader query fields carry the owner-approved explicit batch/publication/version and optional section; optional presentation mode cannot grant permissions. Parameter names/encoding are fixed with Chat/integration at checkpoint. Local URL builder is shared with Chat; supplied URLs are never blindly followed.
- New pages use `[Authorize]` and existing MainLayout with feature-local theme boundaries, not ManagementAccess. Server-bound authenticated subject and effective mode/purpose flow to every Knowledge call. Explicit link publication is resolved exactly if permitted by the host's selection policy; a different configured publication causes safe unavailability, never retargeting.
- Reader obtains one exact authorized structured chunk (at most 64 KiB UTF-8 text/JSON projection) per open/next action through the owner's ReadExact semantics and opaque continuation type. Stable section IDs map to escaped DOM anchors only after membership validation; missing section fails the navigation instead of jumping elsewhere. An oversized section opens directly at its first chunk and focuses its loaded heading/portion, labelled `本节尚未加载完`; `下一部分` stays within that exact section until owner-reported exhaustion. Whole-Document reading follows the owner's section/chunk order; no newer version, local offsets, synthesized cursors or whole-Document fallback is used. Render Razor-encoded headings/paragraphs/lists, keeping applicable claim qualifications beside every displayed fragment, including a continued claim; a partial chunk is not complete content. Avoid MarkupString/raw HTML and retain constructed internal exact links only; no source viewer.
- The UI exposes `已加载部分内容` and a keyboard-accessible next action while the owner supplies continuation; only an explicit terminal owner result permits `已到文档末尾` or `已到本节末尾`. No auto-prefetch/drain. Before next, capture generation, selection/subject/purpose/mode/scope, exact version/optional section and the opaque cursor, disable duplicate next actions and clear the old displayed chunk. After live owner checks, replace it only if the entire capture still matches. Preserve narrative ordering and qualifications through the owner's chunk metadata; never parse/reconstruct incomplete raw JSON or silently skip malformed/unsupported fragments. Retain only current-chunk state; no prior-chunk stack is needed for this slice.
- Context switch (including version/section), cancel, denial, invalid continuation, expiry, timeout or service failure invalidates the generation and clears displayed content, qualifier state and cursors. Cancellation returns no partial page; late or duplicate responses cannot append or restore anything. Retry after those failures is explicit restart at the first chunk of the same exact target with a fresh owner selection and live authority; it does not reuse a cursor across selections or retry against latest data. Successful next calls keep the same selection/publication/version/section and use owner entry/final disclosure checks. If owner chunks cannot preserve qualified content/order within the cap, stop reader wiring and request the missing contract behavior.
- Browse uses owner-returned bounded safe entries/continuation; never derives titles/counts from denied records. A cursor is bound to publication/subject/mode/scope by the owner. Mode/scope/selection change discards it.
- Graph takes a bounded page of permitted seeds and accepted-related results within that same Knowledge Read Context. Keep only Edges whose exact endpoints are in the displayed permitted node set. No placeholders for hidden nodes, hidden degree, restricted counts or inferred Edges. Display only shown-node/Edge counts; an incomplete marker reflects permitted retrieval/display bounds, not the number of denied records. Shared policy must make denied-record additions observationally equivalent.
- Enforce 40-node/80-Edge output, 8-call budget, per-call owner caps and 10-second total deadline. Stop immediately on cancellation, invalid identity, authorization outage or inconsistent publication. A timeout/error clears protected result and reports unavailable; successful bounded output may be shown as incomplete. Fixed one-hop acquisition does not recursively expand candidates or load the whole corpus. Cap qualification always states this limited scope, even if the permitted seed page is exhausted.
- Revalidate the accumulated display through the Knowledge-owned live disclosure operation immediately before returning it. If any contributor changes, withhold affected material through owner policy and produce a coherent safe result or fail closed. Per-request success is not a reusable authorization ticket.
- SVG positions use deterministic grid placement with distinct arrow markers/type legends and a keyboard-accessible node list/inspection panel. Movement changes only transient viewport coordinates. No drag-to-create/edit domain relationships. A self-loop or parallel Edge has a distinct readable label/list entry even if visual paths overlap.
- Every request captures a monotonically increasing view generation, mode, scope and explicit selection. New navigation/mode/refresh cancels prior requests; results apply only if the full capture still matches. Clear old protected content on refresh/context changes and failures; do not redisplay cached results after denial. A complete successful graph replaces nodes/Edges/qualifications atomically. In-flight cancellation is not proof the old response cannot arrive.
- No live refresh/polling engine: next disclosure/retry/refresh rechecks access; already delivered pixels cannot be recalled. When access loss is learned, clear owned display state. Reattachment does not redisclose cached content without a fresh authorized read.

## Verification and dependencies

[quickstart.md](quickstart.md) defines future commands and expected observations. Test-first behavior covers FR-001–013 and SC-001–007 with separate test/production ownership in later implementation. Inventory additions accompany tests. Real PostgreSQL and browser runs use only explicitly owned resources; Web startup still migrates existing Identity/domain contexts, so do not point it at ambient/shared databases. October migrations are applied only by the designated owner, never Web.

Static planning checks: correct branch/ancestor, resolved templates, actual setup-plan JSON and prerequisite helper, complete documentation, links/placeholder checks and scoped Git diff. No build, test suite, database, native tool, provider or browser execution is claimed by this plan.

Remaining dependencies: B0 compiled contract and representative fixture; B0 real store/live authority; B1/B2 supported relationships and BL; Scan real source results; integration route/navigation/DI and explicit database/publication binding; Chat shared destination use; authorized runtime resource allocation. Dates in the source plans are working targets, not evidence or guaranteed estimates.

## Deferred work and implementation stop conditions

Defer full history browsing/source viewer, advanced filters/search, expansion/large-graph layout, live refresh, graph editing, graph database, package upgrades, generic currentness machinery and all Scan/tool/agent changes. Preserve exact historical links where owner-authorized membership exists. Do not mark broader PRD FR-9.4, FR-11.2/11.7, FR-12.6 or full Stories 18.6/20.1/20.2 complete.

1. This session stops after accepted independent plan review. Tasks, code, tests and runtime execution require a subsequent explicit instruction.
2. No accepted compiled shared checkpoint: stop dependent production wiring; minimal owner-type compile/conformance probes may establish that initial checkpoint when separately authorized, but never invent shared contracts/schemas or reuse GetCurrentAsync as an exact read. This correction authorizes no probe execution.
3. No ordinary live all-contributor/mode/purpose policy, final disclosure validation or safe bounded related semantics: stop consumer disclosure; report the missing B0 seam.
4. Required change enters Business persistence/contracts, integration composition/navigation/packages or Chat/Scan ownership: hand off the concrete change; no unilateral shared-file edit.
5. Wrong/unavailable batch/publication, malformed handles, mixed selections, unknown content schema, access revocation/outage: fail closed with safe feedback; no older data, current checkout or demo substitution.
6. No owned DB/ports/processes/source/provider permission for a runtime check: stop that check with explicit prerequisite, not an invented PASS. Never modify global PATH/settings or unrelated resources.
7. A new/materially changed Agent tool, Scan strategy or PRD requirement is proposed: stop before tasks for explicit planned-change approval, citing affected plan/PRD. The current design adds none; first-deliverable deferral preserves broader obligations. Changing that disposition would trigger approval and review.
8. Actual results contradict limits, exactness, authorization or meaningful relationships: fix within ownership and recheck affected behavior; do not weaken assertions or expand scope to claim success.

## Complexity Tracking

No architecture/constitution exception is required. User-selected direct SpecKit and omitted agent-context writes are explicit task authority, not an inferred integrity waiver. Native SVG avoids dependency/package coordination for this bounded view; a large-graph framework is unnecessary for the accepted first-deliverable scope.
