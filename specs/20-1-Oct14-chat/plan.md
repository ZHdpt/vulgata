# Implementation Plan: OCT-CHAT — First useful streaming conversations

**Branch**: `oct14/chat` | **Date**: 2026-10-06 | **Spec**: [spec.md](spec.md)
**Input**: `C:\Users\13728\.codex\worktrees\c1c7\vulgata\specs\20-1-Oct14-chat\spec.md`
**Status**: Proposed for independent review; planning only. No tasks or implementation authorized.

## Summary

Replace the October Chat execution path's fixed-pair/candidate-buffering assumptions with bounded MAF wiki-tool conversations, immediate authorized text, exact current-turn references and durable dialogue. Preserve the existing Chat UI shell and historical contracts. Consume OCT-BUSINESS B0's shared fixed-publication reads, live authority and persistence; OCT-EXPLORE supplies the reader and the integration owner composes Web/provider/admission. First prove a useful fixture-backed vertical slice, then real generated Knowledge and provider behavior.

The user's new-feature/direct-SpecKit choice is consistent with the October delivery input's explicit execution boundary. This is not a resumption of Stories 18.x/19.x. Broader original tasks are provenance, not an implicit work queue. Planning acceptance neither approves agent-tool implementation nor proves any runtime result.

## Technical Context

**Language/Version**: C# / .NET 10; existing Blazor Interactive Server modular monolith.
**Primary Dependencies**: Installed Microsoft.Agents.AI and .OpenAI 1.13.0; Fluent UI 4.14.3; FluentValidation; current OpenAI-compatible ChatCompletions streaming. No package change planned.
**Storage**: PostgreSQL 17, existing OctoberDbContext extended exclusively by OCT-BUSINESS. Chat owns dialogue domain semantics; Business owns storage/migrations and shared Knowledge policy.
**Testing**: Single xUnit project, controlled transports/clocks/Identity harness; existing `eng/test.ps1` suite separation. PostgreSQL, External and E2E checks use explicitly owned resources. This planning run does not execute application tests or hosts.
**Target Platform**: Existing Web host and intended two-service Vulgata/PostgreSQL deployment; no new service or host-tool installation.
**Project Type**: Feature inside the six existing projects.
**Performance Goals**: Observe first visible answer before provider completion; measure first-useful-text and total duration under declared provider/source/environment. No universal latency claim. Small UI coalescing target <=50 ms.
**Constraints**: One active/unresolved answer per conversation; fixed selected publication/mode/scope; live all-contributor checks; strict bounded transport/tool/model/history work; no automatic generation retry/failover. See [budgets](contracts/consumer-boundaries.md#execution-limits-and-provider-completion).
**Scale/Scope**: One selected publication, arbitrary permitted Repository membership (no hardcoded pair), selected cross-Repository business case for qualification. One active Web execution owner for the first slice; no distributed runtime/recovery platform.

**Provider admission clarification**: Agreed policy with separately owned process-local Web/generator permits. Chat uses Web's pool; generator Business finalization uses Scan's pool. References below to shared admission mean this arrangement, not a new cross-process coordinator or globally enforced capacity cap.

## Constitution Check

Pre-research and post-design evaluation against constitution 2.0.0:

| Principle / authority | Result and retained boundary |
| --- | --- |
| I — Layered boundaries | PASS in design: Core contracts/domain, Agents bounded MAF, Infrastructure persistence owned by Business, Web presentation/composition split. No EF or host authority in Agents. |
| II — Domain purity | PASS: consume exact immutable Document Versions and publication; no direct writes, mutable wiki copy or legacy latest-version substitution. |
| III — Validation/API/UI | PASS: FluentValidation, Chinese Fluent/Library Oak, mode themes, safe rendering. Existing API 401/403/ProblemDetails rules retained if an endpoint is needed; direct Blazor feature service is sufficient initially. |
| IV — Testing | PASS as plan: separate future behavioral test/production ownership; honest red/green, focused regression and relevant real provider/PostgreSQL/UI proof. No fabricated planning tests. |
| V — Stewardship | PASS: live purpose/mode/all contributors, protected credentials, no source/DB tools, evidence-as-data and scoped resources. |
| Workflow choice | Explicit user override selects direct SpecKit for this new feature. No governed skill, state machine, story resume or optional agent-context update. Root instructions remain untouched. |
| PRD/AD breadth | October-authorized fixed publication/scope and reconnect/history/source omissions are scoped delivery dispositions; see deferred table. No claim that wider PRD/AD-25/28 is fulfilled. |
| Planned-change approval | ACTIVE STOP before tasks/implementation: new/materially changed agent wiki/report tools require explicit user approval after accepted review. No Scan-strategy change proposed. |

Post-design: no new integrity exception introduced. K0, shared runtime composition and real qualification remain dependencies, not constitutional waivers or completed gates.

## Project Structure

### Documentation (this feature)

```text
specs/20-1-Oct14-chat/
  spec.md
  plan.md
  research.md
  data-model.md
  contracts/consumer-boundaries.md
  quickstart.md
  checklists/requirements.md
  reviews/plan-review.md
```

No tasks.md, production code, tests, migrations or workflow-state changes are generated in this phase.

### Source Code (repository root) and ownership

Proposed new paths below are feature ownership allocations, not claims that files already exist. Use existing projects; finalize exact shared type names at K0.

| Owner | Modules/files | Change responsibility |
| --- | --- | --- |
| OCT-CHAT | `src/dotnet/Vulgata.Core/DomainServices/OctoberChat/` | New conversation/turn execution/events and persistence requirements/ports, using Business-issued Knowledge types. Own dialogue lifecycle, not Knowledge schema. |
| OCT-CHAT | `src/dotnet/Vulgata.Agents/ChatExecution/October/` | MAF streaming adapter, native tool assembly, wiki tool facade, turn read ledger, contributor lineage and budgets. |
| OCT-CHAT | `src/dotnet/Vulgata.Agents/Prompts/OctoberChat*.txt` | Externalized business/IT instructions for narrative synthesis, evidence treatment, hypotheses and exact document reporting. Match existing embedded-resource convention. |
| OCT-CHAT | `src/dotnet/Vulgata.Shared/OctoberChat/` | Chat-only event/presentation DTOs and validators. No duplicated Knowledge identities or access enums. |
| OCT-CHAT | `src/dotnet/Vulgata.Web/Services/OctoberChat/` and `src/dotnet/Vulgata.Web.ViewModels/OctoberChat/` | Host-owned coordinator/subscription, domain lifecycle, history projection and LoadState. No direct database queries. |
| OCT-CHAT | `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor`, `.razor.cs`, `.razor.css`, `.razor.js`; new `Components/Chat/OctoberChatTurn.razor` | Feature-local binding to new October module, real text/activity/footer in existing shell, fixed-scope selection and confirmed fresh conversation. Remove typewriter behavior only on October path. |
| OCT-CHAT | new `tests/Vulgata.Tests/October/Chat/` files | Focused tools/stream/lifecycle/presentation and qualification cases; future test/production authors have disjoint write boundaries. Preserve historical candidate tests. |
| OCT-BUSINESS | Shared Knowledge Core/Shared contracts and fixture, `src/dotnet/Vulgata.Infrastructure/Data/October/OctoberDbContext.cs`, October mappings/repositories, `.../October/Migrations/` and snapshot | Sole shared contracts, read/authorization and persistence writer. Implements Chat dialogue contribution serially, no second context or competing migration. |
| OCT-EXPLORE | Exact reader route/components and destination projection | One exact version/section reader shared with Chat footer and graph. Chat does not define a competing route. |
| Integration owner | `src/dotnet/Vulgata.Web/Program.cs`, shared navigation/routes, `src/dotnet/Vulgata.Agents/DependencyInjection.cs`, project/package files, runtime config and shared provider admission | Opt-in October module, explicit publication/provider binding, host execution identity and coordinated route registration. Chat supplies a feature-local registration entry point/call requirements. |
| Test infrastructure owner / integration coordination | `tests/Vulgata.Tests/TestInfrastructure/suites.json`, shared fixtures and runner | Classify new test/helper files without competing writes. No launcher/policy redesign. |

**Structure Decision**: Add feature-local paths where legacy types encode incompatible semantics. Do not rewrite `ChatConversationContracts.cs`, `ChatAgentDispatcher.cs`, the legacy candidate assembler, `PreparedChatScopeResolver.cs`, historical specs or their tests as an incidental cleanup. Reuse compatible transport/security code by a scoped adapter/extraction only if it preserves old behavior and is reviewed; shared extraction requires integration coordination. No other worktree is edited.

## Required contracts and checkpoint

[K0 requirements](contracts/consumer-boundaries.md#k0--required-compiled-contract-checkpoint) specify consumer needs. At this baseline B0 does not yet export the new compiled contracts. K0 must pin the Business-owned integration commit, actual type signatures, common fixture and owner acknowledgement before dependent implementation. Chat must not publish lookalike Knowledge records while waiting.

Required ports are stable published selection, bounded discovery/content/related reads, exact version/section handles, live contributor authorization and Chat dialogue begin/fence/history/save/lookup/orphan operations. Conceptual labels are not invented public APIs. Chat owns its event types and tool adapter behavior. Business implements data-model.md through its October persistence. Explore agrees the exact reader projection; integration agrees Web profile source and shared admission. Dialogue/reader extension checkpoints may follow K0, but each dependent increment stops until its specific owner-issued port is present.

The common fixture must include CL/BL, accepted direction/meaning, an Uncertainty and denied contributor. Development can prove mechanics against that same labelled fixture before real data arrives. Final acceptance cannot substitute it for actual generated Knowledge.

## Execution design

1. Validate authenticated request and fixed conversation selection. Ask the Business-owned dialogue port to admit or look up the turn with request digest and a durable active fence. Reject overlap and conflicting replay. Do not dispatch on indeterminate begin.
2. Open Knowledge's turn-bound read context for the conversation's exact immutable publication. Authorize selected scope/purpose/mode and bounded completed history. Bind all model-visible wiki functions server-side; no model authority selectors.
3. Execute one MAF conversation using the selected supported Chat profile. Inject bounded tool wrappers and provider streaming adapter. Preserve strict UTF-8/SSE/body/deadline protections, disable SDK generation retries and acquire shared admission for each provider request. MAF owns continuation; no independent second tool loop.
4. Tool wrappers authorize through B0, return bounded results with qualifications and record content reads plus all contributor lineage. Completed native calls alone execute; counts/deadline cover every round. Tool text cannot alter allowed tools or permissions.
5. Relay text immediately under current contributor authorization through typed turn events; show actual sanitized tool activity even between text segments. Host-owned execution outlives a disconnected circuit within its original budget; a detached subscriber cannot stall generation. No reattachment machinery is built.
6. Validate reported current-turn handles, derive safe titles/Explore destinations and update footer independently. Search/related metadata and earlier-turn references do not count as current reads. Use a conservative contributor union for generated text, not just footer contributors. Render safe Markdown with no raw HTML or arbitrary model-generated links.
7. On final valid completion or interruption, settle owned tools/provider permits and atomically save one content/reference/dependency/status outcome. Show Completed only after confirmation. On uncertain acknowledgement use durable lookup/identical save under the same fence; do not regenerate or release the conversation for overlapping retry while indeterminate.
8. Load saved history only after live contributor checks. A host restart fences confirmed orphaned running turns as interrupted and never resumes model calls. Unsaved token recovery is not promised. New mode/scope/publication creates a confirmed empty conversation after active work settles.

Detailed limits, terminal conditions and event shapes live in [contracts](contracts/consumer-boundaries.md), and field/state requirements in [data model](data-model.md). No exact SDK signature is claimed without the C1 compile/control proof.

### F3/F4 — shared read and qualification conformance

ReadWiki accepts Business-issued opaque exact-content continuation in addition to the exact handle/optional section. Delegate cursor validation and bounded reading to Business; keep publication/version/section and live authority fixed. Read Documents and single sections beyond 64 KiB through successive complete-UTF-8 chunks within existing call/time/aggregate budgets. Record only successfully returned coverage; a prefix is not a whole Document/section read. References to partial reads retain their coverage limitation; budget exhaustion permits honest incompleteness. Explore owns UI reader continuation, and Chat introduces no alternate cursor/Knowledge type.

Prompts, immediate answer prose, saved text and reused completed history preserve Business's SourceSupported/Advisory/Hypothesis/Uncertain/Mixed material claim-group qualifications, with explicit clause attribution for Mixed. Qualifiers stay beside the affected text from its first streamed presentation and across content chunks; a provenance footer alone is insufficient. Reuse Business's lock-acquisition versus supplied business-motive fixture so a source action cannot become a proven motive/duration. Bounded history preserves claim and qualifier together or omits the affected group/turn; Business-safe labels do not expose CL/source metadata. This is a consumer-preservation obligation, not per-sentence citations, a whole-answer semantic verifier, buffering or a new publication gate.

C1 controlled conformance includes >64-KiB multi-section Document and >64-KiB single-section continuation, UTF-8/qualification boundaries, cursor mismatch/expiry/revocation and budget-limited partial coverage. C2 verifies attribution/partial-coverage preservation in saved-history follow-ups. These corrections do not expand the separately deferred dialogue contract; its existing implementation/schema and focused PostgreSQL checks remain mandatory before live C1 dispatch.

## Small usable increments, prerequisites and verification

These are implementation planning slices, not generated tasks or authorization to begin.

| Increment | Usable result and owned boundary | Prerequisites | Required verification / stop |
| --- | --- | --- | --- |
| C0 — Safe selected Chat entry | Existing page can bind a permitted fixed October selection and show truthful ready/empty/unavailable state; mode/scope changes start empty context. | User authorizes subsequent work; K0 selected/read/access contracts plus fixture; integration-issued composition stub/module seam. | Ordinary-user access without operator auth; Business BL-only including metadata; one/several/standalone membership; wrong/unpublished selection cannot use legacy fallback. No model call necessary. |
| C1 — Live tool conversation | Existing assistant bubble shows real native wiki-tool activity and immediate text against agreed fixture, with explicit reference reporting and durably admitted/saved turns. | C0; approved agent tools; selected supported provider adapter; installed MAF controlled streaming proof; shared admission contract; before any real provider or user-facing dispatch, Business's implemented dialogue begin/fence/terminal-save/lookup/orphan operations and owner-applied schema. | Controlled fragmented tools, interleaved text before blocked provider finish, invalid finish/truncation/budgets/cancellation, adversarial content, invented/foreign-turn handles. Prove PostgreSQL active fencing and ambiguous-save recovery before explicitly authorized real provider execution against fixture; label Knowledge as controlled. Stop if MAF/transport cannot enforce bounds or event semantics. |
| C2 — Saved follow-up and exact footer | Build on C1's durable turn foundation to deliver ordered useful completed-history follow-ups, interruption presentation and exact reader navigation. | C1 including Business-owned dialogue implementation/extension checkpoint and migration; Explore exact reader projection/route. | Extend PostgreSQL contention/replay/conflict/uncertain-ack/orphan coverage as behavior changes; completed-history follow-up; deny revoked non-footer contributors; saved partial output never normal inference history; UI footer opens original exact section. Reader fixture alone cannot qualify actual navigation. |
| C3 — Real publication integration | Ordinary user asks useful multi-document question/follow-up on the same real published data as reader/graph; visible-before-finish and unknown/interruption captured. | Business B0 and real BL/publication (B2); Scan's real generated source case; integration-owner merged Web/profile/admission/schema window; Explore reader. | Source-checked questions, actual tool round, browser timing, exact references, Business denial/revocation, owned PostgreSQL/provider/UI observations. Preserve distinctions between retrieval gaps and document/answer quality. Full combined build/SelfContained run belongs to integration after final corrections. |

C0/C1 fixtures unblock mechanics, not full delivery. Before Business's durable dialogue implementation arrives, C1 work is test-only controlled transport/tool/UI mechanics with an agreed test double: production user-facing dispatch and live provider calls remain disabled. Every real dispatch requires successful durable begin/fence, and terminal save/lookup must already be available; there is no temporary in-memory production path or Chat-owned schema fallback. C2 adds history/reader value on that foundation. Chat does not wait for the graph to implement C0–C2. October 6–8 are feature targets and October 9–10 is the separate integration phase; dates are not completion guarantees.

## Verification strategy

[quickstart.md](quickstart.md) defines planned reproducible scenarios and suite command shapes. New behavior requires observed RED/GREEN by separate test and production owners after authorization. Reuse existing controlled transport tests only where semantics stay unchanged; do not reinterpret legacy candidate tests as October streaming proof.

Cover request validation, fixed mode/scope, live all-contributor denial, malicious retrieval, strict stream/tool framing, bounded loops, early text, history isolation, save fencing, uncertain acknowledgement and actual reader navigation. Controlled events prove ordering; actual browser observation proves user-visible delivery; PostgreSQL proves real persistence; provider calls and source-checked answers establish their own limits. None substitutes for another.

Planning verification is confined to Git branch/ancestry, template/helper outputs, complete artifact/link checks, whitespace and independent review. No tests, Scan, model calls, migrations or application startup are claimed in this phase.

## Explicit deferred work

| Deferred from this deliverable | Retained safeguard / future requirement |
| --- | --- |
| Same-turn live reconnect/reattachment (AD-28, PRD FR-11.7/NFR-3.3) | Host-owned bounded work, durable history, honest saved/interrupted status; no duplicate generation or transparent recovery claim. |
| Automatic newer-publication selection, broad all-accessible/default scope discovery (AD-25, FR-11.4/11.8) | Explicit fixed publication and permitted scope; new selection creates an empty conversation, running reads never mix. Wider behavior remains unmet. |
| Advanced history/summary management and in-place context transitions (FR-11.3) | Durable order, bounded useful completed history, confirmed empty conversation on change, live revocation. |
| IT source inspection, source viewer, attachments, external MCP/operational tools | Wiki-only scope; no arbitrary filesystem, native tool or database authority. |
| Sentence-level verification/candidate gates, new retrieval/RAG UI, visual redesign | Useful natural synthesis, exact references and separately assessed faithfulness; no full-answer delay. |
| Other provider API types, failover, general distributed recovery/token journal | Explicit ChatCompletions profile, classified unsupported state, bounded host execution and idempotent terminal persistence. |
| Knowledge acceptance/schema, graph/reader ownership, Scan strategy, generalized tool ownership | Consume owner contracts only; retain dependencies without taking over another lane. |
| Full original Stories 19.1–19.3 / generic-product closure | First-deliverable outcome never marks broader story/PRD completion. |

## Exact stop conditions and approval triggers

**Current stop:** After accepted independent plan review, stop before tasks.md, production/test implementation, migrations or runtime. No user/machine PATH, global setting, root agent instruction, unrelated process or database changes.

**Tool approval required:** This plan adds/materially changes agent-visible SearchWiki/BrowseWiki, ReadWiki, GetRelatedDocuments and ReportAnswerDocuments with native multi-round execution and current-turn exact references. Under root AGENTS.md Planned-change authorization, explicit user approval after accepted review is required before tasks. A future request should cite this section, the contract and PRD FR-11.6/11.7/13.3, explain wiki-only read authority and state intent to implement C0–C3. This dispatch grants planning only and does not supply that approval.

**Scan trigger:** None in this plan. If implementation would change Scan parsing/filtering/scheduling/grouping/retry/dispatch or add source/native tools, stop and return the proposal to its owner for explicit approval and review.

**PRD trigger:** No new requirement weakening proposed beyond the explicitly authorized October subset. The deferred table records differences from full FR-11.3/11.4/11.7/11.8 and AD-25/28; do not describe them as generic compliance. Any additional omission or change to Business isolation, live authorization, stable reads, genuine streaming, valid completion, save semantics or useful real evidence triggers explicit PRD approval before tasks/implementation.

**Dependency stops:** No dependent Knowledge implementation before owner-issued K0; no duplicate types/schema to bridge it. No real provider or production user-facing turn dispatch before Business's durable dialogue begin/fence/terminal-save/lookup/orphan contract, implementation and schema are available and focused PostgreSQL admission/save verification passes. Earlier C1 activity is controlled test mechanics only. No exact-link completion before Explore route; no live execution before supported selected provider/profile, shared admission, source/data consent and explicitly owned runtime resources. Missing/mismatched input returns unavailable; it never grants another lane's authority.

**Failure stops:** Unsupported MAF streaming/budget/cancellation behavior, failed strict transport qualification, uncertain turn ownership/save, unknown authorization, unsafe selected dataset or missing schema compatibility prevent dependent dispatch/completion. Capture the concrete gap; do not quietly retry paid generation, upgrade packages, edit shared files or loosen security/tests. A material design correction returns for independent review.

## Complexity Tracking

No new project, service, package or persistence schema owner. The new October path is necessary because existing contracts enforce fixed pairs, nonempty citations and complete-candidate gating; simply relabeling or weakening them would preserve incompatible behavior. Explicit user-selected direct SpecKit and October scope cuts are documented authority decisions, not hidden integrity exceptions.

## Execution provenance

- Actual Git branch: `oct14/chat`; HEAD and required included baseline: `fad7f11c9d6bc9ed3da1b371b24beb9f5c09db5d`; `git merge-base --is-ancestor fad7f11 HEAD` returned 0.
- Every SpecKit helper received process-local `SPECIFY_FEATURE_DIRECTORY=C:\Users\13728\.codex\worktrees\c1c7\vulgata\specs\20-1-Oct14-chat` and explicit project root. Feature selection changed only in this worktree. SpecKit normalized its local feature.json path to a repository-relative form; the helper invocation remained absolute.
- Core spec/plan templates resolved through common.ps1. setup-plan.ps1 -Json returned the exact feature paths. Its BRANCH output was `20-1-Oct14-chat` because common.ps1 treats the explicit feature directory as its current-feature name; Git verification, not that helper field, establishes `oct14/chat`.
- Before hooks absent; optional agent-context after-specify/after-plan hooks skipped by explicit instruction. No other skills invoked; no root context update.
- Initial sandbox process creation failed before execution (`apply deny-read ACLs`). Approved scoped shell execution outside that launcher enabled local reads, branch switch and planning writes. No global setting changed.
