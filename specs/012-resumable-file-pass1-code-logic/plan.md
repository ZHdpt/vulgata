# Implementation Plan: Resumable File Pass 1 & Code Logic Documents

**Branch**: `demo0831/story-12-3` | **Date**: 2026-08-21 | **Spec**: [spec.md](spec.md)

**Input**: Accepted self-reviewed Story 12.3 specification at the pinned planning boundary.

## Summary

Consume one exact authorized Story 12.2 prepared pair, atomically create the complete Story 12.1 deterministic job set, and execute it through a pair-wide, durably reconfigurable 1-4 lease scheduler. A new prepared Worker dispatcher exposes only one durable-budgeted `navigate_code` function over accepted CodeGraph/JDT-LS/source/knowledge ports, including metadata-only bound-exhaustion proof. It accepts only strict `code-logic/file-v2` JSON with exact Story 12.2 Communication Kind Classification bindings, derives every display/search representation server-side, atomically commits one immutable Code Logic Document with its job, pauses/retries/fails by durable scope, applies the high-value-seed/10% partial-completion rule, and idempotently reconciles exact current same-Repository REFERENCES Edges without rewriting Documents. Story 12.4/12.5 remain excluded.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`); strict UTF-8 JSON; PowerShell qualification on Windows

**Primary Dependencies**: EF Core/Npgsql 10.0.9; Microsoft.Agents.AI.OpenAI 1.13.0; Microsoft.Extensions.AI.Abstractions 10.7.0; OpenAI-compatible Chat Completions; CodeGraph `@colbymchenry/codegraph@1.5.0`; Java 21; JDT LS `jdt-language-server-1.61.0-202608141332`; StreamJsonRpc 2.25.29; existing Story 12.1 typed analysis/source ports and Story 12.2 canonical prepared contracts

**Storage**: PostgreSQL 17 via `VulgataDbContext`; SQLite only for economical non-locking harnesses; immutable Document graph plus additive Pass 1 operational tables

**Testing**: xUnit in `tests/Vulgata.Tests`; pure literal vectors, public-seam integration, `WebApplicationFactory<Program>`, disposable PostgreSQL 17 atomic/migration tests, controllable provider fixture, and real Windows-host CodeGraph/Java/JDT-LS qualification

**Target Platform**: Windows host for Vulgata and analysis tools; PostgreSQL 17 is the only required Docker service; no application/tool container is required

**Project Type**: multi-project Blazor/ASP.NET Core Web application with Core domain ports, Infrastructure persistence/analysis, Agents provider execution, Shared DTO/validation, and Web API/host composition

**Performance Goals**: enforce at most four concurrent pair-wide Workers; navigation max 8 calls, 20 results/call, 65,536 cumulative evidence bytes and depth 3; provider recovery exactly five increasing windows; deterministic outputs across concurrency 1/2/4 and ten completion schedules

**Constraints**: exactly two approved manifests; complete job set or none; one result <=65,536 UTF-8 bytes/depth 32; no raw protocols/storage, CodeGraphContext, shell, network/filesystem/database tools; no Story 12.4/12.5; source/provider secrets and host paths never persist or enter Documents

**Scale/Scope**: one automatically selected 50-150 File Code Unit pair; one job per unit; up to 512 anchors, 1,024 Edges, 256 terms and 256 sections under existing knowledge limits, with the v2 projector producing at most nine sections

No clarification remains. Research decisions are closed in [research.md](research.md).

## Constitution Check

### Pre-design gate

| Principle | Plan evidence | Result |
| --- | --- | --- |
| I. Layered Project Boundaries | Core owns entities/pure contracts; Infrastructure alone owns EF/Git/analysis composition; Agents owns provider/function loop; Shared owns HTTP DTO validation; Web owns APIs/DI/hosted execution. | PASS |
| II. Domain Purity & Ubiquitous Language | Specific ports only; no generic repository/IQueryable/DbSet; canonical terms; immutable Document/Version Chain and separate reconciliation Edges. | PASS |
| III. Validation & API Contract | FluentValidation, plural `/api/...` routes, ProblemDetails, Chinese sanitized errors, 401/403/ownership-filtered 404. | PASS |
| IV. Test-First Integration Harness | Every observable slice begins at a named public seam; PostgreSQL separately proves locks/transactions/migration. | PASS |
| V. Security & Data Stewardship | one closed function; self-hosted provider boundary retained; no raw source/status/provider/host/tool exposure; prompt embedded. | PASS |

No Constitution exception is requested. No ADR/context artifact exists in this checkout; therefore there is no ADR/context conflict to waive.

### Post-design gate

The complete data model and contracts preserve the five principles. The only accepted-seam changes are the explicit additive compatibility corrections in AD-12 and [data-model.md](data-model.md) §§7-9; none weakens identity, leases, replay, recovery, authorization, immutability, or prepared-pair meaning. Result: **PASS**.

## Project Structure

### Documentation (this feature)

```text
specs/012-resumable-file-pass1-code-logic/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── spec.md
└── contracts/
    ├── canonical-envelope.md
    ├── code-logic-file-v2.schema.json
    ├── code-logic-projection-contract.md
    ├── navigate-code-contract.md
    ├── pass1-api.md
    ├── pass1-runtime-contracts.md
    └── reconciliation-contract.md
```

`tasks.md` is intentionally absent at this plan stage.

### Planned production ownership

```text
src/dotnet/
├── Vulgata.Core/
│   ├── Entities/
│   │   ├── Pass1PairExecution.cs
│   │   ├── Pass1ConcurrencyChange.cs
│   │   ├── Pass1NavigationBudget.cs
│   │   ├── Pass1NavigationReceipt.cs
│   │   ├── Pass1Outcome.cs
│   │   ├── ScanExecution.cs                       # additive transitions only
│   │   ├── ScanExecutionPhase.cs                  # additive FailedPass1 only
│   │   ├── TwoRepositoryScanCoordination.cs       # additive FailedPass1 only
│   │   └── SourceAnchor.cs                        # nullable byte bounds
│   └── DomainServices/
│       ├── PreparedPass1Contracts.cs
│       ├── PreparedPass1CanonicalCodec.cs
│       ├── CodeLogicFileV2Contracts.cs
│       ├── CodeLogicFileV2Projector.cs
│       ├── IPreparedPass1Store.cs
│       ├── IPass1NavigationService.cs
│       ├── IPreparedPass1WorkerDispatcher.cs
│       └── IPass1CodeLogicDocumentCommitter.cs
├── Vulgata.Infrastructure/
│   ├── Analysis/PreparedNavigation/PreparedPass1NavigationService.cs
│   └── Data/
│       ├── PreparedPass1Store.cs
│       ├── Pass1CodeLogicDocumentCommitter.cs
│       ├── Configurations/Pass1*.cs
│       ├── Configurations/{DocumentVersion,SourceAnchor,ScanExecution,TwoRepositoryScanCoordination}Configuration.cs
│       ├── VulgataDbContext.cs
│       └── Migrations/{migration,designer,VulgataDbContextModelSnapshot}.cs
├── Vulgata.Agents/
│   ├── WorkerExecution/
│   │   ├── PreparedPass1WorkerDispatcher.cs
│   │   ├── PreparedNavigateCodeFunction.cs
│   │   ├── PreparedPass1ResultParser.cs
│   │   └── WorkerAgentProviderHealthProbe.cs
│   ├── Prompts/pass1-code-logic-worker.v2.md
│   └── Vulgata.Agents.csproj
├── Vulgata.Shared/Scans/
│   ├── Pass1ExecutionContracts.cs
│   └── Pass1ExecutionValidators.cs
└── Vulgata.Web/
    ├── Api/Scans/Pass1ExecutionEndpoints.cs
    ├── Data/PreparedPass1Coordinator.cs
    ├── Data/PreparedPass1DispatchWorker.cs
    ├── Api/ApiEndpointRegistrationExtensions.cs
    └── Program.cs
```

### Planned test ownership

```text
tests/Vulgata.Tests/PreparedPass1/
├── Pass1CanonicalEnvelopeTests.cs
├── Pass1PairStartTests.cs
├── Pass1PairPostgreSqlAtomicTests.cs
├── Pass1ConcurrencyAndOrderingTests.cs
├── Pass1ConcurrencyUpdateTests.cs
├── NavigateCodeContractTests.cs
├── NavigateCodeSecurityTests.cs
├── NavigateCodeRealToolQualificationTests.cs
├── CodeLogicFileV2SchemaTests.cs
├── CodeLogicFileV2ProjectionTests.cs
├── Pass1DocumentCommitTests.cs
├── Pass1DocumentPostgreSqlAtomicTests.cs
├── Pass1FailurePolicyTests.cs
├── Pass1ProviderCircuitTests.cs
├── Pass1RecoveryAndResumeApiTests.cs
├── Pass1ReconciliationTests.cs
├── Pass1MigrationTests.cs
├── Pass1HostRuntimeQualificationTests.cs
└── Pass1ProductionBoundaryQualificationTests.cs
```

**Structure Decision**: This follows the enforced project boundaries and creates a new prepared path. Existing legacy Worker/result/materializer files receive no semantic change. Shared internal knowledge transaction code may be factored only under characterization coverage; public legacy ports remain unchanged.

## Architecture Decisions

### AD-1: One prepared pair execution, one atomic complete job set

`Pass1PairExecution` binds the exact Story 12.2 pair and both accepted Story 12.1 Scan Executions. Start requires their already-persisted nonempty prompt identity, exact `code-logic/file-v2` schema identity, and accepted runtime identities; it never invents or rewrites them. It reconstructs both packages and inserts the pair, every job, and every navigation budget in one PostgreSQL transaction before any claim. Concurrency is stored but excluded from all content/identity ordering. Alternative lazy creation or start-time rebinding is rejected because it exposes partial queue state or breaks replay identity.

### AD-2: Pair-row locking owns shared capacity, active reconfiguration, and canonical claim order

One versioned pair row serializes capacity accounting. Claim count is the current unexpired Running leases across both children; the next job is the smallest eligible Pair Selection Ordinal. A separate authorized expected-version command changes effective limit 1-4, appends a canonical audit record, preserves existing leases, and affects only later claims. Initial concurrency remains for exact Start replay; effective concurrency remains outside scan/content/display identity. Existing Manifest Ordinal remains Repository-local display order. Alternative independent semaphores/`SKIP LOCKED`, lease revocation, or changed-Start mutation are rejected because they violate the shared cap, accepted leases, or replay meaning.

### AD-3: New prepared dispatcher, not legacy sequential mutation

A Story 12.3-specific dispatcher builds one v2 packet and explicit loop. Legacy `WorkerAgentDispatcher`, retry decorator, v1 result and Pass 2 prepared-demo seams remain unchanged. Alternative in-place mutation is rejected because it would silently alter accepted Story 11/12.1 behavior.

### AD-4: Exactly one closed `navigate_code` AIFunction

The Agent sees one function and eight operation enum values. The server binds all scope identity and composes only current-Document reads, accepted JDT LS/CodeGraph typed operations, Story 12.2 approved inventory, and exact revision-bound source reads. Absence/out-of-scope are indistinguishable. No other tool is registered. Alternative raw commands or many tools is rejected as unnecessary exposure.

### AD-5: Durable attempt/call/evidence/depth budget and receipt proof

Ten provider turns and eight total function attempts bound the full loop. Accepted calls use a persisted 8/20/65,536/depth-3 budget shared across retries. Source-bearing evidence is released only after its receipt commits. Results/bytes/depth refusal creates a zero-evidence metadata receipt; reaching accepted-call or tool-attempt count eight creates one server marker without a ninth attempt. Those receipts support only truthful deferred bound outcomes. Alternative in-memory/reset-per-retry counting or receiptless BoundExceeded is rejected because restart could evade limits or make fallback unrepresentable.

### AD-6: Strict v2 grammar with server-owned canonical JSON

The JSON Schema plus semantic contract rejects unsafe/unknown/duplicate/unbound values. Communication kind is closed to `Rpc|Http`; every classification binding must exactly equal the referenced Story 12.2 ID, fingerprint, kind, strength, rule/provenance, Catalog Rule Identity, and Evidence Locator, and mixed/mismatched kinds reject. The Worker never authors server IDs, ContentMarkdown, Document Sections, search text, Index Entry, Edge IDs, or fingerprints. A closed writer emits canonical ContentJson. Alternative arbitrary JSON/JCS, free-form communication labels, ID-only classification trust, or LLM-authored projections is rejected because authority/replay would diverge.

### AD-7: Pure deterministic projector

One Core projector maps the validated JSON into fixed Chinese Markdown headings, at most nine group Sections, bounded Index Entry terms/search text, anchors, and direct same-Repository Edges. It escapes/arranges labels but never summarizes or paraphrases. Alternative separate model calls is rejected by FR-029/030.

### AD-8: Server-owned identities preserve Version Chains and completion invariance

Reuse Story 12.1 job ID. Logical Document is Repository/path/segment; Document Version is job/File Code Unit/schema; observations exclude evidence form; Edge is source/target/type. Exact bytes are in [canonical-envelope.md](contracts/canonical-envelope.md). Alternative LLM stable keys or timing/concurrency inputs is rejected.

### AD-9: Specific atomic v2 commit port over the accepted transaction invariant

`IPass1CodeLogicDocumentCommitter` validates and commits the immutable v2 graph plus job completion together. Already-Completed exact replay precedes lease validation and reconstructs all persisted bytes/projections. Alternative two transactions or unchanged legacy materializer is rejected.

### AD-10: Pair-wide provider circuit with deterministic bounded probes

Provider transient stops claims across the pair. Persist exactly five eligibility windows (10/30/60/120/300 seconds), deterministic <10% nonnegative jitter, bounded Retry-After delay, and an active source-free probe. Fifth failure pauses pair/children; success closes circuit. Alternative per-job provider retry amplifies outage.

### AD-11: Scope-based job/failure policy and structured outcome

One isolated provider-authored job defect may retry once; ordinary terminal failures count once. Trusted binding contradiction is Integrity; forbidden disclosure/access is Security; provider configuration pauses; unknown systemic pauses inspection. High-value seeds are reconstructed from manifest scores/order. One immutable outcome/warning inventory plus local reconciliation decides success. Alternative exception-only classification or prose-only warnings is rejected.

### AD-12: Explicit additive compatibility correction

Add warning-free new-path Complete, `FailedPass1`/`Pass1Policy` atoms, v2 Document schema allowance, and nullable v2 Source Anchor byte bounds. These gaps are proven in the pinned current source and are required for truthful/valid Story 12.3 state. Historical values/rows and accepted behavior are unchanged. Alternatives that mislabel ordinary failure or store v2 under v1 identity are rejected.

### AD-13: Existing coordination `Resolving` is handoff, not absorbed scope

After successful terminal accounting and local same-Repository reconciliation, Story 12.3 advances the accepted coordination `Pass1 -> Resolving` transition and stops. It creates no Cross-Repository Resolution/Pass 2 work. Alternative adding another coordination phase is unnecessary; running Story 12.4 inline violates scope.

## Data, Persistence, and Migration

The authoritative entity/relationship/transition model is [data-model.md](data-model.md). One cohesive additive migration creates pair execution, concurrency-change audit, navigation budget/receipt, outcome/warning tables, adds nullable anchor byte bounds, and changes exact closed checks for v2 schema and honest Pass 1 failure atoms. No historical backfill or reinterpretation is allowed.

Migration verification must prove clean PostgreSQL 17 apply, rollback, reapply, model snapshot parity, v1/v2 coexistence, legacy null-byte anchors, new v2 required-byte enforcement, every check/FK/index, and zero pending model changes. SQLite tests do not substitute for this evidence.

## Failure, Recovery, and Security Invariants

- no start mutation until both exact manifests and current authorization reconstruct successfully;
- no claim can exceed pair capacity or bypass pair ordinal;
- active limit updates are authorization/version conditional, preserve valid leases, and affect new claims only;
- navigation returns all bounded evidence or none; it cannot schedule/await another file;
- bound refusals persist metadata proof but disclose/source-authorize nothing;
- provider probing stops new dispatch and cannot exceed five probes;
- retry is the same job, at most once, with remaining durable navigation budget;
- no invalid result can partially write Document/projection/Edge/job state;
- committed Documents survive pause, restart, ordinary failure, and pair failure;
- exact Completed replay is zero-write; changed replay is zero-write conflict;
- restart/manual resume revalidates both children plus pair/manifest/provider state;
- failed seed or over-budget ordinary failure cannot authorize the coordination handoff;
- reconciliation writes only exact same-Repository current REFERENCES Edges and operational outcome;
- raw source/provider/tool diagnostics, credentials, lease tokens and host paths never persist or enter API/Document output;
- CodeGraphContext and all unrestricted tools remain absent from new production composition.

Exact behavior is normative in [pass1-runtime-contracts.md](contracts/pass1-runtime-contracts.md), [navigate-code-contract.md](contracts/navigate-code-contract.md), and [reconciliation-contract.md](contracts/reconciliation-contract.md).

## Public-Seam Test-First Slices

These are architecture seams for later task generation, not tasks or implementation authorization.

1. **Canonical identity RED -> GREEN**: `Pass1CanonicalEnvelopeTests` asserts literal pair/concurrency-change/job-associated/Document/anchor/source-and-bound-receipt/observation/projection/commit vectors with an independent reconstructor before `PreparedPass1CanonicalCodec` exists.
2. **Atomic start RED -> GREEN**: `Pass1PairStartTests` drives `IPreparedPass1Store.StartAsync`; then PostgreSQL fault injection proves complete-or-none and exact replay/conflict.
3. **Shared claim/reconfiguration RED -> GREEN**: `Pass1ConcurrencyAndOrderingTests` and `Pass1ConcurrencyUpdateTests` compete claimers at limits 1/2/4, update 4->1 and 1->4 with active leases, replay/stale/authorization races, expired/future retries and reversed inputs; production owners are the specific store claim/update methods and PATCH endpoint.
4. **Navigation contract/security RED -> GREEN**: `NavigateCodeContractTests` and `NavigateCodeSecurityTests` call `IPass1NavigationService`, first with deterministic fakes and then every forbidden/over-bound form; zero/prior-receipt, first depth-four refusal, results/bytes refusal, and call-eight marker vectors prove deferred fallback while current-document/source-fallback identity equivalence is retained.
5. **Bounded Agent loop RED -> GREEN**: dispatcher tests use a scripted provider that emits valid/invalid/repeated tool calls and final JSON, proving ten turns/eight attempts and durable remaining budget.
6. **Strict result/projector RED -> GREEN**: schema single-field mutations and pure golden outputs precede parser/projector implementation; kind, strength, classification ID/fingerprint, catalog identity, rule/provenance, Evidence Locator, mixed-kind, and bound-receipt mutations reject, and no LLM-authored projection property exists.
7. **Atomic Document/job RED -> GREEN**: public commit port tests exercise every FR-033 mutation and lost-acknowledgement replay; PostgreSQL injects failure before/after every write boundary.
8. **Failure/circuit RED -> GREEN**: injected clock/provider classifications prove exact windows, Retry-After bound, pair-wide claim stop, one job retry, pauses/failures and no duplicate counts.
9. **Resume API RED -> GREEN**: authentication/policy/ownership nondisclosure, pair revalidation, Chinese action, expired lease/due retry, provider probe and exact no-op behavior precede endpoint/coordinator code.
10. **Terminal/reconciliation RED -> GREEN**: budget/seed boundaries and ten completion permutations precede outcome/reconciler implementation; Edge set and warning inventory must be byte-identical and Documents unchanged.
11. **Migration/host/security qualification**: PostgreSQL lifecycle, real typed tools, host topology, production literal/composition inspection, Web build and surrounding/full suites form the final barrier.

Test-only behavior that already exists must be labeled characterization/qualification, not fabricated RED. Pure documentation or fixture work is nonbehavioral. Any production behavior change requires an observed public-seam RED before its production owner.

## Verification Strategy

### Focused deterministic suites

- all 56 FRs and 28 acceptance scenarios have direct named tests or trace to one of the public seams above;
- literal canonical vectors and independent reconstruction;
- concurrency 1/2/4 across at least ten deterministic completion permutations;
- zero/limit/over bounds for navigation, provider probes, retry and failure budget;
- every strict schema/identity/authorization/hash/range/reference/classification/receipt mutation;
- direct-current versus source-fallback observation convergence;
- projection byte identity and no claim absent from JSON.

### PostgreSQL 17 integration

Use a disposable dedicated database/service. Fault injection covers pair start, capacity races, budget/receipt conditional updates, Document/job transaction, Version Chain, exact replay, warning outcome, reconciliation transaction, stale/concurrent writers, cancellation, rollback and zero partial visibility. The migration lifecycle remains a separate suite.

### Real tool/provider qualification

On Windows, verify exact CodeGraph 1.5.0, Java 21, pinned JDT LS archive/release over stdio and StreamJsonRpc 2.25.29. Exercise all eight navigation operations possible for neutral Java/XML fixtures; unsupported operation/language combinations must fail closed. A controllable OpenAI-compatible fixture proves tool-call protocol and outage classification without external source transfer. A final approved-pair host run proves PostgreSQL is the only required Docker service and no CodeGraphContext production dependency exists.

### Final barrier

Run focused PreparedPass1 tests, adjacent DurableScanning/PreparedScanning/KnowledgeStore/AnalysisRuntime suites, Web build, full test suite, migration checks, production boundary/literal inspection, `git diff --check`, and verify production/test paths are unchanged from the accepted quality-review boundary. Story 12.4/12.5 evidence is not required or claimed.

## Requirement and Authority Traceability

| Plan/contract coverage | Specification | Authority/source |
| --- | --- | --- |
| AD-1/2; pair start, identity, active concurrency update/order | FR-001-FR-008; SC-001/002 | BMad 12.3 first group; Story 12.1 jobs/leases; Story 12.2 pair/ordinals; PRD overlay FR-4.2 |
| AD-3/4/5; exact classification packet, navigation/security/refusal proof | FR-009-FR-020; SC-003/004 | August design §6; Story 12.1 analysis/source contracts; Story 12.2 classification vocabulary/identity; PRD FR-4.3/4.4/4.9/4.12, NFR-2.3 |
| AD-6/7/8/9; v2 JSON/projections/atomic replay | FR-021-FR-034; SC-005-SC-007 | August design §7; immutable Document Constitution; Story 12.1 atomic commit; PRD FR-4.3/4.6/4.7/4.10/11.7 |
| AD-9/10/11/12; inherited state, circuit/retry/resume | FR-035-FR-044; SC-007-SC-009 | Story 12.1 durable/recovery contracts; Story 12.2 immutable pair; August design §9; PRD NFR-3.1-NFR-3.3 |
| AD-11/13; seed/budget/outcome/reconciliation | FR-045-FR-052; SC-010/011 | Story 12.2 seed order; August design §§6/9; PRD overlay FR-4.4/4.12 |
| Host boundary and exclusions | FR-053-FR-056; SC-012 | August host-runtime proposal; Story 12.1 runtime; Story 12.4/12.5 boundaries; NFR-4.1 dated replacement |

Coverage count: **56/56 FRs, 12/12 SCs, and 28/28 acceptance scenarios**. No requirement is deferred inside Story 12.3.

## Planned-Change Authorization Disclosure

Independent plan-only review is required before the root requests authorization. The later request must disclose this exact matrix.

| Trigger | Effect and implementation intent | Affected plan material | PRD/authority clauses |
| --- | --- | --- | --- |
| Adds/materially changes Worker Agent tool | one closed `navigate_code` function; eight operations; durable 8/20/64KiB/depth-3 budget; metadata-only bound receipts; ten-turn loop; no other/raw tool | AD-3-5; navigate contract; research R-4-6; test seams 4-5 | FR-4.3/4.4/4.9/4.12; FR-11.6 dated replacement; NFR-2.3 |
| Material Scan dispatch/scheduling | exact pair atomic start, one job/File Code Unit, versioned active pair-wide concurrency 1-4 preserving leases, canonical claims/order | AD-1/2; runtime Start/Claim/Active update; API Update concurrency; data model §§1-2 | FR-4.2; NFR-1.1 dated 50-150 scope; NFR-3.3 |
| Material retry/pause/resume/failure | pair provider circuit, five windows, bounded Retry-After, one job retry, classified pauses/fails, manual pair resume | AD-10-12; runtime Recovery; data model transitions | NFR-3.1-3.3; FR-4.2 |
| Material result/Document behavior | authoritative v2 JSON, exact Story 12.2 communication bindings, server-derived Markdown/sections/index, exact anchors, atomic job/Document replay | AD-6-9/12; schema/projection/canonical contracts | FR-4.3/4.6/4.7/4.10; FR-11.7 |
| Material reconciliation/handoff | exact local post-Pass1 Edges, immutable Documents, `Resolving` handoff only | AD-11/13; reconciliation contract | FR-4.4 and FR-4.12 dated replacement; FR-6.1/6.2 remain Story 12.4 |
| Explicit compatibility correction | warning-free Complete, honest FailedPass1 atoms, v2 schema allowlist, nullable exact byte anchors; historical rows untouched | AD-12; data model §§8-10; migration | accepted Story 12.1 state representation and immutable Document constraints; no requirement weakening |
| Host analysis-tool use | typed CodeGraph/JDT-LS/source facade only; pinned host runtime; no CodeGraphContext | AD-4; real-tool qualification | FR-4.9; NFR-2.3; NFR-4.1 August replacement |

Authorization intent is to proceed from an independently accepted plan to task generation only. It does not authorize implementation by itself and does not waive task self-review, full definition review, implementation approval, TDD RED/GREEN, convergence, quality review, required economical E2E, security/migration/real-tool qualification, final verification, or root-owned checkpoint/state/commit.

## Plan-Review Correction Closure

| Finding | Normative closure | Test seam |
| --- | --- | --- |
| PR-001 | AD-2; data model §§1-2; canonical envelope §2.1; runtime Active concurrency update; API Update concurrency. Initial/effective limits are distinct, the command is authorized/versioned/audited/replayable, valid leases survive, and only later claims use the new limit. | `Pass1ConcurrencyUpdateTests` plus PostgreSQL claim/update races: 4->1, 1->4, equal/no-op, lost-ack replay, stale/changed actor/target, hidden ownership, paused/reconciling/terminal. |
| PR-002 | AD-6; strict schema `communicationClassificationEvidence`; projection Semantic validation. Kind is closed `Rpc|Http`; every echoed ID/fingerprint/kind/level/confidence/rule/provenance/Catalog Rule Identity/Evidence Locator must exactly equal the supplied Story 12.2 row and enclosing kind. | `CodeLogicFileV2SchemaTests` single-field and mixed-kind mutations plus `Pass1DocumentCommitTests` zero-row checks. |
| PR-003 | AD-5; data model §§4-5; canonical envelope §5; navigate Bounds/receipt; deferred `BoundExceeded` schema/projection semantics. Results/bytes/depth refusals and accepted-call/tool-attempt exhaustion have metadata-only proof that cannot authorize evidence or a relationship. | `NavigateCodeContractTests` first depth-four/no-prior-receipt, results, bytes, call/attempt-eight marker, retry/replay/stale marker, and positive-claim misuse vectors. |

These corrections preserve the accepted specification intent and require a fresh independent plan-only review. They do not authorize tasks or implementation.

## Explicit Exclusions

- no Cross-Repository Resolution or Cross-Repository REFERENCES/Uncertainty decision;
- no endpoint-centered flow grouping or `retrieve_code_logic`;
- no Pass 2 or Business Logic Document;
- no Chat, final release UI/dashboard, evaluation questions, or release run;
- no CodeGraphContext, raw LSP/CodeGraph storage, shell/network/filesystem/database Worker tool;
- no automatic background resume or broad provider failover;
- no change to Story 12.2 selection, parsing, hashes, classification, ordering, manifest, approval or replay;
- no semantic change to legacy Story 11 Worker/result/materializer/Pass 2 behavior.

## Unresolved Risks and Placeholders

No unresolved design placeholder or user choice remains.

Residual implementation risks are bounded and have named proof seams:

- Microsoft Agent tool-loop/provider compatibility: explicit scripted provider and ten-turn dispatcher tests;
- PostgreSQL pair capacity races: pair-row lock and competing-claimer tests;
- active concurrency update/replay races: expected-version audit envelope plus 4->1/1->4 active-lease PostgreSQL vectors;
- v1/v2 knowledge coexistence: schema-discriminated read/commit paths and migration/legacy tests;
- classification echo drift: exact persisted Story 12.2 field comparison and single-field/mixed-kind mutation vectors;
- first bound refusal with no source receipt: metadata-only refusal/call-marker vectors prove deferred output without evidence authority;
- current-Document availability affecting explanatory wording: allowed by specification; stable observation identity and reconciliation tests preserve identity/Edge outcome;
- active provider probe portability: fixed source-free Chat Completions request and classified fixture/real-provider smoke evidence;
- safe target source binding for unselected files: navigation intersects only Story 12.2 safely eligible approved inventory and exact revision reader; outside inventory is unavailable.

## Inline Self-Review

| Lens | Evidence | Verdict |
| --- | --- | --- |
| Feasibility | Existing deterministic jobs, pair coordination, manifests/entries, typed analysis ports, knowledge entities, Microsoft AIFunction support and PostgreSQL transactions support additive implementation. Missing state/schema/byte seams are explicit corrections. | PASS |
| Invariants | Complete-or-none start, versioned shared capacity/update/order, bounded navigation with refusal-only proof, exact classification authority, v2 authority, exact anchors, atomic commit/replay, seed/budget, immutable reconciliation and exclusions are normative. | PASS |
| Failure behavior | Provider/job/integrity/security/config/cancellation/unclassified scopes have exact durable transitions; five probes and one retry survive restart; no invalid result writes partial state. | PASS |
| Test seams | Eleven public-seam slices name concrete tests and production owners; PostgreSQL and real-tool evidence are separate from economical fakes. | PASS |
| Migration/compatibility | One additive reversible migration, no backfill, v1/v2 coexistence, legacy rows/ports unchanged, exact accepted-gap proof and lifecycle tests. | PASS |
| Security | One closed function, server-bound scope, nondisclosing unavailable response, no raw tools/protocol/storage/source status/host path/secret, self-hosted provider boundary. | PASS |
| Determinism/replay | Exact pair/update/receipt envelopes, server UUIDs, canonical writer/order, literal vectors, independent reconstruction, completion/effective-concurrency exclusions and mutation-free start/update/job replay. | PASS |
| Constitution/ADR | All five Constitution principles pass; canonical Ubiquitous Language used; no ADR/context exists; no exception. | PASS |
| Authority/scope | All 56 FRs, 12 SCs, 28 scenarios covered; Story 12.1/12.2 preserved; August overlays/host correction explicit; Story 12.4/12.5 excluded. | PASS |
| Planned-change governance | Trigger/effect/intent/plan/PRD/remnant-gate disclosure is complete; independent plan review and explicit authorization remain before tasks. | PASS |
| Review correction | PR-001 active limit transition, PR-002 authoritative classification equality, and PR-003 refusal-proof fallback are closed normatively with public-seam RED vectors and no specification weakening. | PASS |
| Placeholder check | No unresolved clarification, template marker, user choice, task list, or implementation code is present. | PASS |

**Self-review verdict**: **PASS — ready for independent plan-only review.** Task generation and production implementation remain unauthorized.

## Complexity Tracking

No Constitution violation requires justification.
