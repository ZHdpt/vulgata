# Implementation Plan: OCT-SCAN — First real Scan batch

**Branch**: `oct14/scan` | **Date**: 2026-10-06 | **Spec**: [spec.md](spec.md)

**Input**: `specs/20-1-Oct14-scan/spec.md`

**Historical planning status**: Definition/design only; independent review recorded separately. Neither review acceptance nor this plan alone authorizes tasks, implementation, native execution or provider spending. Later direct user authority and the current result are recorded below.

**Current bounded execution**: On October 7 the user authorized S2 T013–T024, reviewed owned commits and coordination with OCT-Integrator, with no skills loaded. S1 is complete; B0 and reader integration are imported. S2 began at integrator-approved `082994eb0a6ee0084efff0a21357c59e86b60fb5`. Configuration, real SDK/MAF Worker, shared scope, primary framing/F1, inventory and internal CL publication components are reviewed with controlled GREEN. Complete dynamic input provenance, exact-bound tool wrappers and F1/accounting refinements are reviewed at `f396e35b1fcaab83586245ad23ecbcc46616e639`. Executable Generate composition is now independently reviewed with 299/299 affected controlled checks. Broad regression and separately authorized qualification remain outstanding. Reviewed Business supporting-input behavior, canonical admission helpers and the corrected admission store are imported for controlled composition. Docker readiness currently blocks PostgreSQL qualification. Controlled and planned runner-owned PostgreSQL work proceed independently of missing T024 real-source/provider/retained-resource approvals. No Chat/dialogue, graph or B1/B2 prerequisite is introduced. Stop before S3; see `verification-s2.md`.

**Completed S1 result**: T005–T012 are complete. Final affected SelfContained checks pass (296/296); combined native source/JDT/CodeGraph qualification and all selected existing native regressions pass. Six focused process lifecycle checks pass after correcting their teardown. The user-authorized bounded correction in `PreparedJdtProcessLauncher.cs` separates stderr draining from process exit, terminates validated retained descendants and waits for actual Windows kernel signals before owned-state cleanup. No lease, shutdown limit or host profile was widened. Independent review accepted the correction; see `verification-s1.md`.

**Bounded preparation dispatch**: Subsequent coordinator authorization permits only T001–T004 import/conformance and commits, stopping before T005. Exact integration `3bcd815b09cfbac7418d6644a2a75816e0788ec0` (Business candidate `07c3fe4463e3228a6982fd106b58148ca87974bd`, handoff `7a189c34aec625202f74846d4907ded787ccbdca`) is now imported. Concrete symbol mapping and controlled-fixture limits are in `contracts/scan-boundaries.md`; verification/consumer acknowledgement belongs in `conformance-c0.md`. This supersedes the initial-commit-unavailable statement below for this import only, not the requirement for final integration-owned CP-K1 acceptance or separate Wave 2 authority. No product implementation is claimed.

**Cross-lane concurrence, 2026-10-06**: Adopt Business F1 serialized commit-submission eligibility and F2 Scan-owned execution scope, preserving F5 CL-only behavior, against canonical `knowledge-contract.md` SHA-256 `F449357B5273D009C3262277912A797886E385AA883E5A5A149A887946EA3D36` verified in the supplied Business checkout. See `contracts/scan-boundaries.md` for exact scope/probes. Earlier review holds remain historical; semantic concurrence does not prove compiled conformance or authorize implementation.

## Summary

Extend the existing October generator with Generate inside its own live authenticated session. Deterministic Scan Control inventories/adopts the configured slice, executes bounded real MAF file workstreams, supplies trusted terminal completion to Business's atomic acceptance port, settles writes, then uses the explicitly selected Business CL-publication or matching/BL-finalization path on the fixed accepted corpus. Begin with combined tool qualification, then one real accepted file, then the small two-Repository registration/login case. No original governed story is resumed.

Generate pins explicit `outputStage` in the existing scan-policy file: `cl-only` publishes the settled exact CL selection through Business B0 without matching/BL; `cl-and-bl` invokes later Business finalization. No second configuration mechanism or implicit downgrade is introduced.

This checkout was clean and detached before switching to the existing `oct14/scan`. Actual HEAD is `fad7f11c9d6bc9ed3da1b371b24beb9f5c09db5d`; `git merge-base --is-ancestor fad7f11 HEAD` returned 0. That planning baseline contains the October 6 inputs. The inputs' earlier `57fad124` runtime baseline is provenance, not a request to reset this checkout.

## Technical Context

**Language/Version**: C# / .NET 10, existing modular monolith and `Vulgata.OctoberGenerator` executable.

**Primary Dependencies**: Existing Microsoft.Agents.AI and Microsoft.Agents.AI.OpenAI 1.13.0, configured OpenAI-compatible provider transport, prepared source access, dedicated JDT-LS and native CodeGraph MCP. No package upgrade is planned.

**Storage**: Existing PostgreSQL 17 October store, extended exclusively by OCT-BUSINESS. Scan runtime scheduling/drafts are process-local for this first deliverable; accepted outcomes/operation keys/fixed selection are persisted through Business ports. No new Scan schema, context or migrations.

**Testing**: Existing xUnit project and `eng/test.ps1`: SelfContained, PostgreSql, LocalTools, External and E2E separated by their actual resource boundary. Behavioral changes require honest RED/GREEN and separate test/production ownership; documentation is checked directly.

**Target Platform**: The explicitly approved native host/JDK/tool profile and owned PostgreSQL target. Windows contained source support and actual profile compatibility must be qualified. No generic Docker/other-platform claim.

**Project Type**: Generator CLI with injected application/domain ports and infrastructure adapters; shared Web composition belongs to integration.

**Performance Goals**: Measure equal declared input scope at Worker cap 1 then 2/configured cap; record elapsed time, queue/tool/model/store waits and available usage. No promised speedup or machine throughput threshold. Serial tool gates may bound throughput.

**Constraints**: Finite whole-run and per-file/model/tool budgets; one valid writer per file; real MAF execution; source read-only; live use/disclosure authority; atomic idempotent acceptance; exact publication identity. Prepared origin stays non-Git. No global PATH/settings edits.

**Scale/Scope**: One host, configured slice across two prepared Repositories for qualification. Counts/names are not eligibility rules. Controlled tests cover one/three Repository inputs without expanding real qualification breadth.

## Constitution Check

| Principle | Pre-design assessment | Post-design result |
| --- | --- | --- |
| I: boundaries | Knowledge contract/store are not Scan-owned | Pass: no EF in Core/Agents, no competing schema; Business owns contract checkpoint |
| II: domain language | File workstreams differ from semantic Code Units and Document Versions | Pass: application completion and Knowledge-derived identity remain separate; honest prepared-origin exception |
| III: validation/contracts | Validate command/config/provider/tool inputs; preserve existing Prepare/Select | Pass: closed tools and explicit failures; no Web API change in Scan |
| IV: evidence | Runtime evidence cannot be inherited from standalone tool tests | Pass as plan: incremental RED/GREEN, native/store/provider/usefulness and final integration checks specified; no runtime pass claimed |
| V: security | Live source/advisory contributor checks, protected credentials and bounded resources | Pass as design: pre-use/post-result checks and stop/fence semantics; qualification still required |

Direct SpecKit is explicitly selected by the user for this new feature, overriding BMad story runner mechanics only. No other skills or optional agent-context hooks run; root AGENTS.md is unchanged. Compatible integrity, independent review, planned-change approval, testing and runtime permissions remain.

October scope deltas are preserved explicitly: prepared directories instead of Git capture; one bounded host batch instead of durable generic orchestration; fixed corpus instead of later-evidence reconciliation; fresh batch after abrupt interruption instead of restart recovery; native host instead of generic deployment work. These are first-deliverable selections from the delivery/Scan briefs and the proposal's authority-delta table, not claims that generic PRD obligations were fulfilled or erased.

## Project Structure

### Documentation (this feature)

`spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/scan-boundaries.md`, `quickstart.md`, `checklists/requirements.md`, `plan-review.md`. No tasks.md.

### Source Code (repository root) and ownership

All paths below are repository-relative; new filenames are proposed implementation locations, not existing APIs.

| Owner | Owned module/files | Boundary |
| --- | --- | --- |
| OCT-SCAN | `src/dotnet/Vulgata.OctoberGenerator/Program.cs`, `OctoberGeneratorModule.cs`, `PreparedSourceBinding.cs`; new `OctoberGenerateRunner.cs`, `OctoberScanPolicy.cs`, `OctoberToolRuntime.cs`, `OctoberWorkerClientFactory.cs` | CLI Generate, process-local orchestration, session/tool/client ownership and local composition |
| OCT-SCAN | New `src/dotnet/Vulgata.Core/DomainServices/OctoberScanExecutionContracts.cs` | Scan-only work/authority interfaces; consume Business's proposal/identity types, never redeclare them |
| OCT-SCAN | New `src/dotnet/Vulgata.Agents/WorkerExecution/OctoberFileWorkerExecutor.cs`, `Prompts/OctoberFileWorker.md` | Real MAF loop, bounded continuation, structured proposals; no direct EF/filesystem/process authority |
| OCT-SCAN, narrowly | `src/dotnet/Vulgata.Infrastructure/Analysis/CodeGraph/PreparedCodeGraphConnection.cs`, `NativeCodeGraphMcpClient.cs`, `NativeCodeGraphSession.cs`; existing owned-state coordinator; `Infrastructure/Source/PreparedSourceReadContext.cs`, source traversal/issuer and workspace issuer; `Analysis/Java/PreparedJavaNavigationContextFactory.cs` only if required for scoped lifetime policy | Same-process descriptor session reuse, contained excerpt validation, positive owned-state exclusion, bounded dedicated lease profile; preserve default behavior and historical contract tests |
| OCT-SCAN | Corresponding new tests under `tests/Vulgata.Tests/OctoberScan/` and scoped existing Source/Java/CodeGraph regressions | Separate controlled/native/provider suites; owned resources only |
| OCT-BUSINESS | Shared Core Knowledge contracts/types/fixture; `Infrastructure/Data/October/OctoberDbContext.cs`, configurations, migrations and snapshot; acceptance/read/publication/finalization/logging persistence | Only writer, including any required Scan terminal outcome/operation persistence. Scan requests additions and waits; it cannot modify schema |
| Integration owner | `src/dotnet/Vulgata.Web/Program.cs`, shared Web DI/routes/configuration and cross-module registration | Ordinary frozen-DefaultConnection wiki composition; no generator credential/session requirement for readers |
| Coordinate file owner before edits | `Directory.Packages.props`, project references/resource entries in Generator/Agents csproj, `tests/Vulgata.Tests/TestInfrastructure/suites.json`, shared provider admission/diagnostics contracts | Integration/test-infrastructure owner serializes shared changes; no blind shared-file overwrite |
| Other lanes | Explore reader/graph, Chat turn/transport/UI, Business relationships/BL | Consume/invoke their interfaces only; do not implement their internals |

Existing generator setup files are reused; do not route through `PreparedPass1WorkerDispatcher` or fabricate its pair/ScanRun/Git inputs. Existing MAF patterns in `PreparedPass1JobExecutor` are reusable mechanics, not semantic contracts. Scope infrastructure edits to the named methods/authority seams; stop for ownership reassignment before broader changes.

## Required shared interfaces and contract checkpoint

See [contracts/scan-boundaries.md](contracts/scan-boundaries.md) for the semantic requirements. OCT-BUSINESS selects actual interface/type names. Calls named here are responsibilities, not competing API definitions.

**C0 / Business CP-K1 sequence**: after separate authorization, Business publishes an initial compiling owner contract/labelled fixture commit, and integration imports the exact hash. Minimal consumer compile/conformance probes may validate that commit before dependent production is enabled; completed consumer implementations are not a prerequisite. Probes cover F1 submission/settlement/freeze, F2 shared scope, F5 early CL and exact authorized reads. Resolve objections and accept the exact checkpoint before separately authorized dependent production. Record actual symbols/commit when delivered. No compiled contract/fixture/probe checkpoint is currently supplied or verified here; the canonical Markdown hash is not that checkpoint. No competing types/schema or circular gate.

Checkpoint must prove: exact batch/root/context provenance; immutable proposal fingerprint and operation identity; non-model analysis completion distinct from live write eligibility; F1 atomic acceptance with guarded commit submission and exact settlement; identical replay/conflict/indeterminate lookup; zero-unit disposition; terminal coverage/freeze; F2 host execution scope with shared cap/request-rate/budget/cancellation/accounting; explicit publication binding; live subject/purpose/mode/all-contributor authorization and sanitized logging. Exercise the representative fixture through the same consumer ports. This is one shared checkpoint, not a new approval queue.

C0 denotes Business's CP-K1. F1/F2 semantics are adopted from the pinned owner revision, with controlled conformance still required. For S2 the shared checkpoint and B0 acceptance, fixed CL publication and exact-read implementation suffice; B1/B2 production services are needed only for S3. Dialogue-specific schema agreement does not gate B0. Initial contract/fixture and minimal probe work is distinct from dependent production and still requires separate authorization; none is performed by this correction.

## Execution design

### Host, inventory and admission

Generate authenticates and holds its own `IOctoberOperatorSession` inside `OctoberGeneratorModule.ExecuteAsync` until tools, acceptance and finalization settle. Rebind through existing `IOctoberBatchSelector.BindAsync`, trusted setup Snapshot and `PreparedSourceBinding.IssueAsync`; never reuse a disposed Prepare/Select result as authority. Existing commands retain their behavior.

Keep the current explicit provider document loading/precedence and selected profile identity. A generator-local factory resolves only that selected protected credential reference and supplies the real MAF client. `OctoberProviderBinder` alone is not a client. Unsupported API type, decryption/configuration failure or missing role profile fails before paid dispatch; no ambient secret search or provider switching.

Deterministically list authorized source and attach source facts/unknowns and existing snapshotted advisory context. Policy contains explicit selected/supporting/excluded rules with reasons and stable priority/tie order. No mandatory filename/framework classifier or new LLM overview stage. Source/context may inform proposals but Scan Control makes admission. A single queue schedules ready files round-robin by Repository, priority then relative path within each Repository. Only one active attempt token exists per file; waiting on an unknown target never holds Worker capacity. Required unprocessed regions remain pending until covered or a terminal gap is recorded.

Initial proposed policy defaults: Worker cap 1 (cap 2 is a separate measured qualification), run deadline 30 minutes, per-file deadline 10 minutes, maximum 4 continuation windows, maximum 8 model requests per file, transient retries at most 2 with bounded increasing delays (1 and 3 seconds, honor a shorter remaining deadline; Retry-After beyond budget stops). Require an explicit deployment token/cost ceiling before live calls; reserve budget before dispatch and treat unknown usage conservatively. Values are configurable finite limits, not coverage exclusions. Cap increases or unqualified host profile changes require new qualification, not a global default relaxation.

Malformed/schema results receive at most the configured remaining retry allowance; continued coverage is a separate window, not an accepted partial result. Transient isolated job failures consume bounded retries. Shared provider outage exhausts retries then closes dispatch and leaves an interrupted batch; configuration/security/source-integrity failures stop affected execution immediately. No durable pause/resume or automatic restart is implemented.

### Qualified tool composition (SC1 first)

1. One host-owned runtime per Repository fixes approved root, root-object identity, source approval, run lifetime and owned-state identity. Prepare native CodeGraph once before analysis via `PrepareAsync`; consume its returned descriptor/closed host arguments for discovery and `codegraph_explore`. Never reconstruct a descriptor from a path.
2. Retain the index/descriptor in the same process. Add serialized replacement native sessions only after confirmed disposal of the prior session. Keep the existing two-minute/ten-call session and 30-second operation limits; additionally cap replacements at 20 per Repository and at the run deadline. No per-Worker re-init/delete, restart adoption, foreign-state borrowing or automatic reclamation. If ownership/disposal cannot be proven, stop; do not reopen.
3. Ordinary source contexts keep the existing 30-second bound. Open around actual calls; hold one context for a bounded list/search page sequence and drain before model waits. Context-local single-use cursors never transfer across renewed grants. On expiry, explicitly restart at most once with stable file-identity deduplication; if still incomplete, record a gap/fail admission completeness rather than omit remaining files.
4. Call `OpenDedicatedAsync` for JDT with an application-owned workspace lease, separate disposable state and explicitly compatible JDK/host policy; preserve definition/reference and no import/build/restore. Prefer the existing 30-second lease. If it cannot deliver useful initialized navigation, the bounded adaptation proposed for approval is a Generate-only dedicated navigation grant of at most 120 seconds, still capped by remaining file/run time and checked before use/disclosure. This must be implemented as explicit policy, not widening ordinary read grants or all historical callers. SC1 must qualify the selected value; an unqualified longer grant cannot pass.
5. A single Repository tool gate serializes CodeGraph preparation/session activity and JDT windows; dispose/settle JDT before incompatible CodeGraph work. No simultaneous cache mutation and dedicated JDT use. Keep external JDT state separate. Source-tool calls operate only against a stable approved index state under the same lifecycle policy; do not treat unchanged-source observations as preventive isolation or inherit a broader read-only guarantee than the qualified profile provides. Any need for a new containment model stops for approval.
6. Integrate positive application-owned `.codegraph` identity into contained source traversal before enumeration/count/search budget use. Deny direct reads of that exact owned subtree; retain legitimate unowned same-name source. Bind continuation to root/state identity; replacement or disappearance fails closed. A post-return filename filter is insufficient. Persisted names alone do not prove ownership; later processes must not automatically adopt leftovers. Existing unowned `.codegraph` means blocked preparation, with explicit operator handling outside Generate.
7. Replace direct-path CodeGraph excerpt verification with the contained source read port and live result-disclosure authority. Normalize relative anchors and compare returned excerpts against contained reads of the bound source; fail closed on path/root/source mismatch, output overflow or access loss. Host paths/raw output must not reach the Worker.

### Whole-file completion and Business acceptance

MAF owns model/tool conversation, with injected closed tools, externalized prompt, serial tool invocation per Worker and explicit request/iteration/output budgets. Application validates provider finish (no truncation), no pending tool calls, complete required file coverage and proposal structure/source anchors. Continued windows preserve file identity and accumulate a draft; coverage validation is not a universal semantic proof. Real usefulness is assessed separately.

The host freezes the assembled proposal/fingerprint and supplies non-model-constructible completion proving analysis only. **F1 adopted:** Scan owns separate irreversible attempt eligibility, monotonic drain/run deadlines and hard stop. Business validates eligibility/live authority at transaction entry, then under the same per-attempt guard shared with fencing rechecks fence/deadline/hard stop/fresh authority immediately before marking and submitting the exact transaction as CommitInFlight. Hold the guard through submission, without unguarded work/await between final checks and submission. Expiry checks use monotonic time, not timer delivery. Seal→expiry/fence→entry rejects before transaction; expiry during staging rolls back without submission. Already-submitted commit may settle after cancellation; this guard is neither durable receipt nor proof of commit/rollback. Publication commits use the same rules with distinct operation identity/phase eligibility.

Hold the same operation key/digest. On ambiguous acknowledgement use bounded exact lookup independent of cancelled work I/O, with fresh disclosure authority. A momentarily absent row while the original write may still finish is indeterminate, not noncommit. No replacement write, inference regeneration, freeze or success until committed or confirmed rolled-back/never-submitted. Replay the same key/payload only after confirmed noncommit and renewed checks of still-current attempt eligibility; a fenced attempt cannot replay a new write. Changed payload needs a new application attempt identity only after definitive settlement. Preserve committed history even when subsequent disclosure is denied.

### Finish, graceful stop and finalization

Natural finish closes dispatch after admitted work settles. First graceful stop closes new file admission but is not a fence; existing attempts may finish inside the finite drain (default 30 seconds within run deadline). Drain expiry fences those file attempts against transaction/commit submission, without cancelling a separately allowed finalization phase or renewing its budget. Hard stop fences attempts, denies new calls/publication/finalization and cancels owned outstanding I/O; observed authority loss/unavailability also fences/cancels affected work and bars disclosure/submission under lost authority. For already-submitted commits, cancellation is not rollback proof: settle by exact lookup, retain committed history and recheck disclosure authority. Assign stopped-gap only after definitive noncommit; any potentially committing/indeterminate operation blocks freeze. Hard stop skips finalization even if a submitted acceptance later commits.

**F2 adopted:** every Business provider attempt/retry acquires the owner-published scope port (proposed `IOctoberExecutionScope.AcquireProviderAttemptAsync`) implemented by Scan's host. Bind run/phase/finalization operation, provider profile, one cap shared with file Workers, host request/rate admission, remaining request/usage budgets, monotonic deadline and hard-stop signal. Business can only narrow bounds. Recheck eligibility/rate permission on grant and immediately before send; queued/rate-delayed attempts retain the original deadline. No second pool/limiter, budget reset or cross-process Web permits. Finalization orchestration holds no provider permit while waiting for children, including at cap=1. Link provider I/O to deadline/cancellation. Settle each attempt exactly once with actual request count, available usage or explicit unknown and conservative reservation, waits/duration/outcome; block further constrained calls when remaining allowance is unknowable. Release only after transport settlement or confirmed disposal. Unconfirmed cancellation stops dispatch as owned outstanding/cleanup failure, never reclaimed capacity. Late responses cannot newly publish under F1. First graceful stop allows bounded drain and a separately eligible Business phase; hard stop/authority failure/run expiry deny queued/new/retry calls. CL-only uses no Business provider attempts.

Freeze through Business after all admitted work is terminal and all writes are known, obtaining fixed exact accepted membership/coverage. Branch on the already pinned `outputStage`:

- **S2, `cl-only`**: Invoke Business's B0 CL-only `PublishSelectionAsync` responsibility with the explicit accepted set/coverage, expected corpus identity and stable operation key. No B1 matching, B2 synthesis, BL provider calls or service-availability dependency. Return owner-issued exact batch/publication/Document Version/section handles; ordinary IT reads use that selection. Acknowledgement loss uses exact publication-operation lookup, not a new Worker run, fresh publication key or manual SQL. This is the smallest trusted real-file → accepted CL → published CL → ordinary-reader path.
- **S3, `cl-and-bl`**: Invoke Business's later fixed-corpus finalization under its idempotent operation key, current authority and remaining budgets. Business model/inquiry work must consume the F2 owner-agreed host scope, never a second pool. Missing B1/B2, failure or indeterminate result is not CL-and-BL success; no silent CL-only downgrade. Business owns matching, distinct rooted BL and publication. A later CL+BL publication is a new immutable selection and never mutates an earlier CL-only publication or retargets its handles.

Both routes report requested/completed stage, independent coverage/execution/publication status and exact confirmed handles. CL-only reports matching/BL `not-requested`; exit 0 means the requested CL stage and selected source scope completed, not S3 or release completion. Partial publication is exit 2, failure 1, interruption 3, indeterminate 4 and no-content 5. Zero accepted files or zero CL Documents yields no-content, not a useful wiki claim. Neither route grants restart/reuse authority or automatically updates Web's selected publication.

A confirmed partial publication after graceful stop is labelled partial and is distinct from full selected-scope completion. Zero accepted files yields explicit no-content outcome, not an empty successful wiki. Reader opening uses Business's exact publication binding and Explore's destination; generator operator authority is never reused as ordinary reader authorization.

## Small usable increments, prerequisites and verification

| Increment | Usable result | Prerequisites | Required verification before proceeding |
| --- | --- | --- | --- |
| C0 / CP-K1 | Initial owner contract/fixture, then accepted exact compiled checkpoint | Separate authorization; Business first publishes and integration imports initial commit; completed consumers not required | Minimal compile/conformance probes before dependent production: F1 submission/lookup/freeze, F2 cap=1/rate/queued hard-stop/settlement, F5 B0-only and exact authorized reads. No competing DTOs or circular gate |
| S1 / SC1 | Generate can hold a bounded multi-file tool runtime and report real inventory/gaps | Task/implementation approval, C0, approved native profile and owned fixture roots | Controlled expiry/revocation/path/state/cursor/disposal tests; real JDT definition/reference and returned-descriptor CodeGraph discovery/explore; >one session/call and run beyond startup; list/read/search pages, preexisting owned/unowned state, cancellation and cleanup. No provider needed |
| S2 / SC2 | Explicit `outputStage=cl-only`: one real file becomes exact readable CL | S1, accepted shared compiled checkpoint/F1 conformance, Business B0 acceptance/CL publication/exact reads only, provider/spend and owned PostgreSQL authority; ordinary reader composition | B1/B2 absent or zero-call sentinels; real MAF/tool path; atomic acceptance/publication and exact lookup, zero-unit/no-content, replay/conflict/stop; exact ordinary IT read and Business exclusion. Stage-labelled outcome; usefulness review. Fixture result alone cannot complete S2 |
| S3 / SC3 | Explicit `outputStage=cl-and-bl`: connected batch reaches fixed Business publication | S2, Business B1/B2 finalization, owner-agreed F2 execution scope, declared registration/login inputs in two prepared Repositories | Sequential baseline then cap 2/configured cap; one writer; drain/settlement/late result, hard interruption, budget exhaustion; real BL/link/unknown; exact counts/waits/usage; no downgrade or hardcoded pair; earlier CL-only handles unchanged |
| Integration handoff | Reproducible exact publication usable by ordinary IT/Business consumers | Business + Explore + Chat and integration owner's Web composition | October integration plan's combined real-source/store/tools/reader/Chat/graph and access checks. Scan alone cannot claim release completion |

Tests use the existing launcher and suite inventory. Owned test resources remain isolated by lane. The integration owner runs one combined build/SelfContained regression after final corrections plus affected native/PostgreSQL/provider/UI checks; repeat only after changes or unresolved failures. Preserve source-free sanitized `log.md` entries in the required `## [YYYY-MM-DD HH:MM] operation | target` format via the owner-supplied sink; no local competing Knowledge log store.

## Explicit deferred work

No full original 18.5 or old 18.2/18.3 skipped-task revival; no broad 18.7/18.8/18.12 matrices. No durable restart/recovery platform, cross-batch lineage/late target reconciliation, automatic source updates, Git acquisition/retained snapshot pipeline, universal classifier/framework matching, general tool ownership/reclamation/cross-process borrowing, arbitrary tools/SQL, global environment setup or generic Docker/backup rollout. No Scan Web dashboard, reader/graph/Chat implementation, relationship/BL acceptance or migration ownership. These remain unmet/deferred broader scope, not delivered claims.

## Exact implementation stop conditions and approval triggers

1. **This request**: stop after independent plan acceptance. Do not create tasks or implement.
2. Before dependent production: C0/CP-K1 compiled checkpoint absent/incompatible, F1/F2 conformance failing, or shared ownership conflict. Separately authorized initial owner contract/fixture and minimal compile/conformance probes may establish that checkpoint first; they do not need completed consumers. Return objections to Business/integration owner, no private replacement. Current semantic concurrence alone does not satisfy this hold.
3. Before native/provider/database use: missing scoped authorization, exact owned targets, compatible host/JDK settings, provider binding or finite budget. No unrelated processes/databases or global PATH/settings changes.
4. During tooling: uncontained/changed source, wrong root/batch/database, revoked/unavailable authority, uncertain tool-state ownership, unconfirmed disposal, failed pagination completeness or required native CodeGraph unavailable. Fence affected work, settle resources, expose the blocker. No weakening containment, blanket `.codegraph` exclusion or CodeGraph file fallback.
5. S1 fails useful multi-file lifetime/coexistence under the selected bounded policy: stop before paid analysis; changing isolation/host profile, extending unqualified limits or dropping required tools requires fresh design/approval.
6. Acceptance/finalization indeterminate, conflict, unproven completion, unresolved writes or exhausted budgets: stop success/publication claims; reconcile exact operation when permitted. No regenerated replacement, late write, empty-success wiki or old dataset.
7. Any new Worker tools, material Scan strategy or PRD deviation beyond the selected subset requires independent review and explicit planned-change approval before tasks. Future implementation authorization must also be explicit; historical delegates/waivers do not apply.

**Tool trigger applies**: real MAF tool exposure, positive state exclusion, contained CodeGraph verification, same-process native session replacement and the optional narrowly bounded dedicated JDT lease adaptation materially change tool behavior.

**Scan trigger applies**: config-driven admission/priority, pool scheduling, retry/windows, trusted completion and graceful-stop/freeze/finalization strategy.

**PRD relationship**: preserve FR-4.3/4.4/4.7/4.8/4.9, FR-13.1, FR-15.1, NFR-3.1 and live authorization. The selected October scope differs from generic FR-4.1/4.2, FR-12.1 and durable recovery/deployment obligations as recorded in the October proposal authority table; do not silently amend PRD. Approval to proceed should explicitly acknowledge these bounded October dispositions and any further proposed deviation. This planning request grants no post-plan approval.

## Complexity Tracking

No new framework, scheduler, context, schema or approval engine. Process-local runtime/drafts are a deliberate first-deliverable reduction of generic durable orchestration, justified by the October delivery brief's restart deferral; atomic accepted state/fencing remains mandatory. Reusing pair/ScanRun execution would fabricate identity, while building durable generic ownership/restart would exceed selected scope. Scoped tool adapter changes are necessary because existing limits/disposal/state exclusion do not qualify a multi-file batch.
