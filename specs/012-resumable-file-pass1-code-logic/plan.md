# Implementation Plan: Resumable File Pass 1 & Code Logic Documents

**Branch**: `demo0831/story-12-3` | **Date**: 2026-08-22 | **Spec**: [spec.md](spec.md)

**Input**: Accepted Story 12.3 intent plus the authorized Quality Review 01 correction at source checkpoint `c13827b1f370fcc62df49da92c7c8d33c4a78112`.

## Summary

Consume one exact authorized Story 12.2 prepared pair, atomically create the complete Story 12.1 deterministic job set, and explicitly activate one bounded Windows-host capacity-filling coordinator that carries authenticated Start through durable claims, independently scoped lease-bound Worker dispatch up to the pair limit, atomic result/failure handling, the exact active provider-probe program, in-flight drain, and terminal reconciliation. The prepared Worker exposes only one lease/version-bound durable-budgeted `navigate_code` function over accepted CodeGraph/JDT-LS/source/knowledge ports. It accepts only strict `code-logic/file-v2` JSON with exact Story 12.2 Communication Kind Classification bindings, derives every display/search representation server-side, atomically commits one immutable Code Logic Document with its job, reconstructs the entire canonical persisted Document graph on replay, applies the high-value-seed/10% partial-completion rule, and idempotently reconciles exact current same-Repository REFERENCES Edges without rewriting Documents. Story 12.4/12.5 remain excluded.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`); strict UTF-8 JSON; PowerShell qualification on Windows

**Primary Dependencies**: EF Core/Npgsql 10.0.9; Microsoft.Agents.AI.OpenAI 1.13.0; Microsoft.Extensions.AI.Abstractions 10.7.0; OpenAI-compatible Chat Completions; CodeGraph `@colbymchenry/codegraph@1.5.0`; Java 21; JDT LS `jdt-language-server-1.61.0-202608141332`; StreamJsonRpc 2.25.29; existing Story 12.1 typed analysis/source ports and Story 12.2 canonical prepared contracts

**Storage**: PostgreSQL 17 via `VulgataDbContext`; SQLite only for economical non-locking harnesses; immutable Document graph plus additive Pass 1 operational tables

**Testing**: xUnit in `tests/Vulgata.Tests`; pure literal vectors, public-seam integration, authenticated `WebApplicationFactory<Program>` production composition, disposable PostgreSQL 17 atomic/migration tests, controllable OpenAI-compatible provider fixture, fake-time probe scheduling, and real Windows-host CodeGraph/Java/JDT-LS full-pair qualification

**Target Platform**: Windows host for Vulgata and analysis tools; PostgreSQL 17 is the only required Docker service; no application/tool container is required

**Project Type**: multi-project Blazor/ASP.NET Core Web application with Core domain ports, Infrastructure persistence/analysis, Agents provider execution, Shared DTO/validation, and Web API/host composition

**Performance Goals**: enforce at most four concurrent pair-wide Workers from durable leases; navigation max 8 calls, 20 results/call, 65,536 cumulative evidence bytes and depth 3; provider recovery exactly five active source-free probes at the durable 10/30/60/120/300-second nominal windows; deterministic outputs across concurrency 1/2/4 and ten completion schedules; no busy polling

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

The complete data model and contracts preserve the five principles. The original additive compatibility corrections remain in AD-12 and [data-model.md](data-model.md) §§7-10. Quality Review 01 adds only a bounded host composition and stricter use of existing job/lease/pair/Document rows: Core-specific ports, Infrastructure persistence/analysis, Agents provider/function execution, and Web hosted activation remain layered; no new persistence or authority exception is required. Result: **PASS**.

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
│       ├── IPreparedPass1ClaimedPacketSource.cs
│       ├── IPreparedPass1ProviderHealthProbe.cs
│       ├── IPass1NavigationService.cs
│       ├── IPreparedPass1WorkerDispatcher.cs
│       └── IPass1CodeLogicDocumentCommitter.cs
├── Vulgata.Infrastructure/
│   ├── Analysis/PreparedNavigation/PreparedPass1NavigationService.cs
│   └── Data/
│       ├── PreparedPass1Store.cs
│       ├── PreparedPass1ClaimedPacketSource.cs
│       ├── Pass1CodeLogicDocumentCommitter.cs
│       ├── PreparedPass1ReconciliationProjector.cs
│       ├── Configurations/Pass1*.cs
│       ├── Configurations/{DocumentVersion,SourceAnchor,ScanExecution,TwoRepositoryScanCoordination}Configuration.cs
│       ├── VulgataDbContext.cs
│       └── Migrations/{migration,designer,VulgataDbContextModelSnapshot}.cs
├── Vulgata.Agents/
│   ├── WorkerExecution/
│   │   ├── PreparedPass1WorkerDispatcher.cs
│   │   ├── PreparedPass1JobExecutor.cs
│   │   ├── PreparedNavigateCodeFunction.cs
│   │   ├── PreparedPass1ResultParser.cs
│   │   ├── PreparedPass1ProviderHealthProbe.cs
│   │   └── WorkerAgentProviderHealthProbe.cs       # timing policy only
│   ├── Prompts/pass1-code-logic-worker.v2.md
│   └── Vulgata.Agents.csproj
├── Vulgata.Shared/Scans/
│   ├── Pass1ExecutionContracts.cs
│   └── Pass1ExecutionValidators.cs
└── Vulgata.Web/
    ├── Api/Scans/Pass1ExecutionEndpoints.cs
    ├── Data/PreparedPass1Coordinator.cs
    ├── Data/PreparedPass1ExecutionActivator.cs
    ├── Data/PreparedPass1ExecutionHostedService.cs
    ├── Data/PreparedPass1JobRunner.cs
    ├── Data/PreparedPass1ProbeDriver.cs
    ├── Data/PreparedPass1DispatchWorker.cs          # one claimed job adapter
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
├── Pass1ProductionExecutionTests.cs
├── Pass1ProductionProviderOutageTests.cs
├── Pass1RecoveryAndResumeApiTests.cs
├── Pass1ReconciliationTests.cs
├── Pass1MigrationTests.cs
├── Pass1HostRuntimeQualificationTests.cs
└── Pass1ProductionBoundaryQualificationTests.cs
```

**Structure Decision**: This follows the enforced project boundaries and completes the existing prepared path. Web owns activation/host lifetime, Agents owns OpenAI-compatible execution and its only function, Infrastructure owns claimed packet hydration/EF/analysis/replay projection, and Core owns only specific closed contracts. Existing legacy Worker/result/materializer files receive no semantic change. Shared deterministic reconciliation or knowledge comparison code may be factored only under characterization coverage; public legacy ports remain unchanged.

## Architecture Decisions

### AD-1: One prepared pair execution, one atomic complete job set

`Pass1PairExecution` binds the exact Story 12.2 pair and both accepted Story 12.1 Scan Executions. Start requires their already-persisted nonempty prompt identity, exact `code-logic/file-v2` schema identity, and accepted runtime identities; it never invents or rewrites them. It reconstructs both packages and inserts the pair, every job, and every navigation budget in one PostgreSQL transaction before any claim. Concurrency is stored but excluded from all content/identity ordering. Alternative lazy creation or start-time rebinding is rejected because it exposes partial queue state or breaks replay identity.

### AD-2: Pair-row locking owns shared capacity, active reconfiguration, and canonical claim order

One versioned pair row serializes capacity accounting. Claim first recovers strictly expired leases and releases due nonexhausted Story 12.3 retries, counts current unexpired Running leases across both children, and selects the smallest eligible Pair Selection Ordinal. Its server-only result includes current job/pair versions, lease token/expiry, ordinals, and next durable eligibility; a specific packet source revalidates that claim after revision-bound source hydration. A separate authorized expected-version command changes effective limit 1-4, appends a canonical audit record, preserves existing leases, and affects only later claims. Initial concurrency remains for exact Start replay; effective concurrency remains outside scan/content/display identity. Existing Manifest Ordinal remains Repository-local display order. Alternative independent semaphores/`SKIP LOCKED`, caller-built packets, lease revocation, or changed-Start mutation are rejected because they violate the shared cap, current lease authority, accepted leases, or replay meaning.

### AD-3: One claimed-job prepared dispatcher, not legacy sequential mutation

A Story 12.3-specific claimed packet source builds one v2 packet only after validating the current lease; the dispatcher runs the explicit bounded provider/function loop for that one claimed job. The production job runner, not the dispatcher, owns durable claim/commit/failure sequencing. Legacy `WorkerAgentDispatcher`, retry decorator, v1 result and Pass 2 prepared-demo seams remain unchanged. Alternative caller-supplied multi-job batches or in-place legacy mutation is rejected because they cannot prove current lease ownership and would silently alter accepted Story 11/12.1 behavior.

### AD-4: Exactly one closed `navigate_code` AIFunction

The Agent sees one function and eight operation enum values. The server binds all scope identity and composes only current-Document reads, accepted JDT LS/CodeGraph typed operations, Story 12.2 approved inventory, and exact revision-bound source reads. Absence/out-of-scope are indistinguishable. No other tool is registered. Alternative raw commands or many tools is rejected as unnecessary exposure.

### AD-5: Durable attempt/call/evidence/depth budget and receipt proof

Ten provider turns and eight total function attempts bound the full loop. Accepted calls use a persisted 8/20/65,536/depth-3 budget shared across retries. Every function instance is server-bound to the current expected job version and lease token/expiry. Scope resolution checks the Running job and Running/closed pair before analysis; receipt commit locks pair/job/budget, repeats those checks, and releases evidence only after commit. Results/bytes/depth refusal creates a zero-evidence metadata receipt; reaching accepted-call or tool-attempt count eight creates one server marker without a ninth attempt. Stale/replaced/expired/paused/terminal navigation returns empty `Unavailable` and writes no receipt. Alternative identity-only binding, in-memory/reset-per-retry counting, or receiptless BoundExceeded is rejected because a stale Worker could disclose source, restart could evade limits, or fallback would be unrepresentable.

### AD-6: Strict v2 grammar with server-owned canonical JSON

The JSON Schema plus semantic contract rejects unsafe/unknown/duplicate/unbound values. Communication kind is closed to `Rpc|Http`; every classification binding must exactly equal the referenced Story 12.2 ID, fingerprint, kind, strength, rule/provenance, Catalog Rule Identity, and Evidence Locator, and mixed/mismatched kinds reject. The Worker never authors server IDs, ContentMarkdown, Document Sections, search text, Index Entry, Edge IDs, or fingerprints. A closed writer emits canonical ContentJson. Alternative arbitrary JSON/JCS, free-form communication labels, ID-only classification trust, or LLM-authored projections is rejected because authority/replay would diverge.

### AD-7: Pure deterministic projector

One Core projector maps the validated JSON into fixed Chinese Markdown headings, at most nine group Sections, bounded Index Entry terms/search text, anchors, and direct same-Repository Edges. It escapes/arranges labels but never summarizes or paraphrases. Alternative separate model calls is rejected by FR-029/030.

### AD-8: Server-owned identities preserve Version Chains and completion invariance

Reuse Story 12.1 job ID. Logical Document is Repository/path/segment; Document Version is job/File Code Unit/schema; observations exclude evidence form; Edge is source/target/type. Exact bytes are in [canonical-envelope.md](contracts/canonical-envelope.md). Alternative LLM stable keys or timing/concurrency inputs is rejected.

### AD-9: Specific atomic v2 commit port over the accepted transaction invariant

`IPass1CodeLogicDocumentCommitter` validates and commits the immutable v2 graph plus job completion together. Already-Completed exact replay precedes lease validation and reconstructs canonical ContentJson/Markdown plus every persisted Source Anchor, Document Section, the single Index Entry, and direct REFERENCES Edge field/order/cardinality from scalar rows. If reconciliation already completed, one pure shared projector reconstructs the exact additional Edge set; stored fingerprints never substitute for row equality. Missing/extra/mutated/reordered/duplicate graph state rejects without mutation. Alternative scalar-only replay, two transactions, or unchanged legacy materializer is rejected.

### AD-10: Pair-wide provider circuit with deterministic bounded probes

Provider transient stops claims across the pair. Persist exactly five eligibility windows (10/30/60/120/300 seconds), deterministic <10% nonnegative jitter, bounded Retry-After delay, and a versioned server-only probe ticket. A single pair probe driver waits through `TimeProvider`, sends one fixed source-free Chat Completions request with SDK retries/tools/logging disabled, discards response text, and conditionally advances the ticket. Fifth transient failure pauses pair/children and removes the activation; success closes the circuit; provider configuration pauses for correction. Alternative configuration-resolution health, per-job provider retry, polling, or a sixth/background-resume probe is rejected.

### AD-11: Scope-based job/failure policy and structured outcome

One isolated provider-authored job defect may retry once; ordinary terminal failures count once. Trusted binding contradiction is Integrity; forbidden disclosure/access is Security; provider configuration pauses; unknown systemic pauses inspection. High-value seeds are reconstructed from manifest scores/order. One immutable outcome/warning inventory plus local reconciliation decides success. Alternative exception-only classification or prose-only warnings is rejected.

### AD-12: Explicit additive compatibility correction

Add warning-free new-path Complete, `FailedPass1`/`Pass1Policy` atoms, v2 Document schema allowance, and nullable v2 Source Anchor byte bounds. These gaps are proven in the pinned current source and are required for truthful/valid Story 12.3 state. Historical values/rows and accepted behavior are unchanged. Alternatives that mislabel ordinary failure or store v2 under v1 identity are rejected.

### AD-13: Existing coordination `Resolving` is handoff, not absorbed scope

After successful terminal accounting and local same-Repository reconciliation, Story 12.3 advances the accepted coordination `Pass1 -> Resolving` transition and stops. It creates no Cross-Repository Resolution/Pass 2 work. Alternative adding another coordination phase is unnecessary; running Story 12.4 inline violates scope.

### AD-14: Explicitly activated single-flight host execution service closes the production call graph

Authenticated Start/eligible exact replay, manual Resume, and concurrency update signal a bounded in-process pair activation. One Windows-host `BackgroundService` deduplicates the pair into one single-flight coordinator, but that coordinator is normatively capacity-filling: it serially obtains durable claims and launches each claim in an independent DI-scoped job runner without awaiting an earlier runner, repeating until the store reports `AtCapacity`, future/ineligible work, or a circuit/terminal state closes dispatch. Runner completion is only a wake-up to reload durable state and fill newly available slots; it never chooses the next job or contributes to identity/order. A 4-to-1 change preserves all four leases and suppresses replacements until durable active count falls below one; a 1-to-4 change wakes Fill and launches up to three more eligible runners while the first may remain blocked. A closed circuit/pause/failure/cancellation stops launches immediately, then already-launched runners drain to closed durable outcomes. `ReconcileAsync` is legal only after the local runner set is empty and a fresh durable claim proves zero active/Pending/future-retry work. PostgreSQL remains the execution authority; the channel and local completion handles contain no File Code Unit authority and cannot choose order/capacity/retry/terminal state. A coordinator that awaits one Worker before making the next claim is explicitly nonconforming. The service does not discover persisted work at startup and stops permanently for that activation after the fifth provider failure, so the accepted manual `继续扫描` boundary remains intact. Alternative sequential claim-to-completion execution, long-running HTTP execution, per-file in-memory queue, startup database sweep, or automatic paused-pair recovery is rejected.

## Data, Persistence, and Migration

The authoritative entity/relationship/transition model is [data-model.md](data-model.md). One cohesive additive migration creates pair execution, concurrency-change audit, navigation budget/receipt, outcome/warning tables, adds nullable anchor byte bounds, and changes exact closed checks for v2 schema and honest Pass 1 failure atoms. No historical backfill or reinterpretation is allowed.

The Quality Review 01 correction adds no persistent entity: activation, claim authorization, claimed packet, probe observation/ticket use existing persisted pair/job/lease/probe fields and transient server-only projections. Lease-bound navigation and complete replay require stricter reads/conditional checks only. Any new schema delta requires a new plan decision rather than being inferred during implementation.

Migration verification must prove clean PostgreSQL 17 apply, rollback, reapply, model snapshot parity, v1/v2 coexistence, legacy null-byte anchors, new v2 required-byte enforcement, every check/FK/index, and zero pending model changes. SQLite tests do not substitute for this evidence.

## Failure, Recovery, and Security Invariants

- no start mutation until both exact manifests and current authorization reconstruct successfully;
- every created/eligible explicitly replayed Start reaches the single-flight production activation path; no activation may create or select a job;
- the single-flight pair coordinator must fill durable capacity by launching independently scoped runners until `AtCapacity`/ineligible/closed state; strictly sequential claim-to-completion execution is forbidden;
- no claim can exceed pair capacity or bypass pair ordinal; completion order and local runner count never authorize a claim;
- a Worker packet, navigation call, result commit, or failure transition must carry and revalidate the same current job version/unexpired lease;
- active limit updates are authorization/version conditional, preserve valid leases, and affect new claims only;
- navigation returns all bounded evidence or none; it cannot schedule/await another file;
- bound refusals persist metadata proof but disclose/source-authorize nothing;
- provider probing stops new dispatch, uses the fixed source-free active request at durable eligibility, and cannot exceed five probes;
- fifth probe failure removes the activation and only authenticated `继续扫描` can reactivate it; host startup never discovers dormant pairs;
- retry is the same job, at most once, with remaining durable navigation budget;
- no invalid result can partially write Document/projection/Edge/job state;
- committed Documents survive pause, restart, ordinary failure, and pair failure;
- exact Completed replay reconstructs the full persisted canonical Document graph and is zero-write; any missing/extra/reordered/duplicate/mutated graph row is zero-write conflict or Integrity failure;
- restart/manual resume revalidates both children plus pair/manifest/provider state;
- failed seed or over-budget ordinary failure cannot authorize the coordination handoff;
- reconciliation writes only exact same-Repository current REFERENCES Edges and operational outcome;
- terminal reconciliation is invoked by the production pump exactly when durable claim state proves no active/pending/future-retry job; the pump never starts Story 12.4;
- reconciliation additionally requires every locally launched runner to be drained; a circuit/terminal closure stops launches immediately but never treats an in-flight runner as absent;
- raw source/provider/tool diagnostics, credentials, lease tokens and host paths never persist or enter API/Document output;
- CodeGraphContext and all unrestricted tools remain absent from new production composition.

Exact behavior is normative in [pass1-runtime-contracts.md](contracts/pass1-runtime-contracts.md), [navigate-code-contract.md](contracts/navigate-code-contract.md), and [reconciliation-contract.md](contracts/reconciliation-contract.md).

## Public-Seam Test-First Slices

These are architecture seams for later task generation, not tasks or implementation authorization.

1. **Canonical identity RED -> GREEN**: `Pass1CanonicalEnvelopeTests` asserts literal pair/concurrency-change/job-associated/Document/anchor/source-and-bound-receipt/observation/projection/commit vectors with an independent reconstructor before `PreparedPass1CanonicalCodec` exists.
2. **Atomic start RED -> GREEN**: `Pass1PairStartTests` drives `IPreparedPass1Store.StartAsync`; then PostgreSQL fault injection proves complete-or-none and exact replay/conflict.
3. **Shared claim/reconfiguration RED -> GREEN**: `Pass1ConcurrencyAndOrderingTests` and `Pass1ConcurrencyUpdateTests` compete claimers at limits 1/2/4, update 4->1 and 1->4 with active leases, replay/stale/authorization races, due/future retries and reversed inputs; claim assertions include current job/pair versions, opaque lease authorization, earliest durable eligibility, exactly-once due retry release, and terminal exhaustion. Barrier-controlled store/runner integration proves a held lease does not prevent claims up to limits two/four, 4->1 preserves leases and suppresses replacements, and 1->4 exposes three new claimable slots before the original completes. Production owners are the specific store claim/update methods and PATCH endpoint.
4. **Lease-bound navigation RED -> GREEN**: `NavigateCodeContractTests` and `NavigateCodeSecurityTests` call `IPass1NavigationService` through a server binding. Current lease succeeds; expired lease, replaced token/version, paused/probing circuit, Completed job, terminal pair, and an expiry/owner race between analysis and receipt commit return nondisclosing `Unavailable`, run no forbidden source/tool call, and write no receipt. Existing zero/prior-receipt, first depth-four refusal, results/bytes refusal, call-eight marker, and current-Document/source-fallback vectors remain green.
5. **Bounded Agent loop RED -> GREEN**: dispatcher tests use a scripted provider that emits valid/invalid/repeated tool calls and final JSON, proving ten turns/eight attempts and durable remaining budget.
6. **Strict result/projector RED -> GREEN**: schema single-field mutations and pure golden outputs precede parser/projector implementation; kind, strength, classification ID/fingerprint, catalog identity, rule/provenance, Evidence Locator, mixed-kind, and bound-receipt mutations reject, and no LLM-authored projection property exists.
7. **Full-graph replay RED -> GREEN**: public commit-port and PostgreSQL tests first remove/mutate/reorder/duplicate each Source Anchor, Document Section, Index Entry, and direct REFERENCES Edge field/cardinality after a completed commit. Every case conflicts or fails integrity with zero writes; exact lost-ack replay and exact post-reconciliation Edge union replay remain zero-write GREEN.
8. **Active provider request/probe driver RED -> GREEN**: fake HTTP transport and fake time prove the fixed request contains no source/identity/tools, SDK retry is zero, response text is discarded, 401/403 maps configuration, and transient observations invoke exactly one versioned store transition at each 10/30/60/120/300-second nominal window plus accepted jitter/Retry-After. Claims stay stopped, conflicts do not double-count, and the fifth failure persists pause with no sixth/background wake-up.
9. **Resume API RED -> GREEN**: authentication/policy/ownership nondisclosure, pair revalidation, Chinese action, expired lease/due retry, the same active provider request (not configuration resolution), exact no-op behavior, and explicit execution activation precede endpoint/coordinator code.
10. **Production Start-to-terminal RED -> GREEN**: authenticated `WebApplicationFactory<Program>` plus disposable PostgreSQL and a controllable OpenAI-compatible transport resolves the real DI graph, posts two approved manifests, and observes the capacity-filling coordinator through durable claim -> exact packet -> independently scoped Worker -> canonical Document/job commit/closed failure -> refill/drain -> reconciliation -> terminal status. A controllable Worker barrier blocks inside execution and fails on timeout unless two and four runners overlap simultaneously when enough work exists; a counter proves the cap is never exceeded. While four are blocked, 4->1 preserves those leases and launches no replacement until active count drops below one; while one is blocked, 1->4 launches three more before the first completes. Forward, reverse, and at least ten permuted barrier releases preserve identities/order/reconciliation. Circuit/terminal closure proves immediate launch stop, runner drain, and no reconciliation before both local drain and durable exhaustion. A sibling outage test drives the same composition through five probes and provider-unavailable pause.
11. **Terminal/reconciliation RED -> GREEN**: budget/seed boundaries and ten completion permutations precede outcome/reconciler implementation; the production pump calls `ReconcileAsync` only after terminal claim exhaustion, and Edge set/warning inventory are byte-identical with Documents unchanged.
12. **Migration/host/security qualification**: PostgreSQL lifecycle, real typed tools, exact host topology, an approved-pair host execution through the production DI graph, production literal/composition inspection, Web build and surrounding/full suites form the final barrier.

Test-only behavior that already exists must be labeled characterization/qualification, not fabricated RED. Pure documentation or fixture work is nonbehavioral. Any production behavior change requires an observed public-seam RED before its production owner.

## Verification Strategy

### Focused deterministic suites

- all 56 FRs and 28 acceptance scenarios have direct named tests or trace to one of the public seams above;
- literal canonical vectors and independent reconstruction;
- barrier-proven simultaneous execution reaches two and four, never exceeds the effective durable limit, and retains identical outputs across at least ten deterministic completion permutations;
- active 4->1 drain/no-revoke and 1->4 immediate-fill behavior through production composition;
- zero/limit/over bounds for navigation, provider probes, retry and failure budget;
- current/expired/replaced/paused/completed/terminal lease navigation matrices at both scope-read and receipt-commit races;
- production activation/claim/dispatch/commit/failure/probe/reconciliation call-graph assertions through resolved DI rather than constructor/literal inspection alone;
- full scalar graph replay removal/mutation/reorder/duplicate-cardinality vectors;
- every strict schema/identity/authorization/hash/range/reference/classification/receipt mutation;
- direct-current versus source-fallback observation convergence;
- projection byte identity and no claim absent from JSON.

### PostgreSQL 17 integration

Use a disposable dedicated database/service. Fault injection covers pair start, capacity races, due-retry claim release, claimed packet double revalidation, lease-bound budget/receipt conditional updates, Document/job transaction, Version Chain, full-graph exact replay, warning outcome, reconciliation transaction, stale/concurrent writers, cancellation, rollback and zero partial visibility. Row-removal/mutation/duplicate-cardinality replay cases must use PostgreSQL constraints and transactions, not only SQLite/in-memory projections. The migration lifecycle remains a separate suite and must prove the correction created no unintended model delta.

### Real tool/provider qualification

On Windows, verify exact CodeGraph 1.5.0, Java 21, pinned JDT LS archive/release over stdio and StreamJsonRpc 2.25.29. Exercise all eight navigation operations possible for neutral Java/XML fixtures; unsupported operation/language combinations must fail closed. A controllable OpenAI-compatible fixture proves the real prepared job executor/function protocol and outage classification without external source transfer. A final authenticated approved-pair host run resolves the production DI graph, invokes at least one real typed Java navigation call plus exact XML source evidence, commits immutable Documents, reconciles, and reaches the Story 12.3 terminal/handoff state. It proves PostgreSQL 17 is the only required Docker service and no CodeGraphContext production dependency exists. Runtime/version-only checks remain supporting evidence, not execution proof.

### Final barrier

Run focused PreparedPass1 tests including production Start-to-terminal/outage composition, adjacent DurableScanning/PreparedScanning/KnowledgeStore/AnalysisRuntime suites, Web build, full test suite, migration checks, full-graph replay mutations, production boundary/literal/DI inspection, `git diff --check`, and verify production/test paths are unchanged from the freshly accepted corrected quality-review boundary. Story 12.4/12.5 evidence is not required or claimed.

## Requirement and Authority Traceability

| Plan/contract coverage | Specification | Authority/source |
| --- | --- | --- |
| AD-1/2/3/14; pair start, identity, claimed packet, capacity-filling production activation, active concurrency/order | FR-001-FR-008; SC-001/002 | BMad 12.3 first group; Story 12.1 jobs/leases; Story 12.2 pair/ordinals; PRD overlay FR-4.2; QR-001/005; PRC3-001 |
| AD-3/4/5/14; exact packet, current-lease navigation/security/refusal proof | FR-009-FR-020; SC-003/004 | August design §6; Story 12.1 analysis/source contracts; Story 12.2 classification vocabulary/identity; PRD FR-4.3/4.4/4.9/4.12, NFR-2.3; QR-003 |
| AD-6/7/8/9; v2 JSON/projections/full canonical-graph atomic replay | FR-021-FR-034; SC-005-SC-007 | August design §7; immutable Document Constitution; Story 12.1 atomic commit steps 2/5; PRD FR-4.3/4.6/4.7/4.10/11.7; QR-004 |
| AD-9/10/11/12/14; production failure path, active provider request/driver, retry/resume | FR-035-FR-044; SC-007-SC-009 | Story 12.1 durable/recovery contracts; Story 12.2 immutable pair; August design §9; PRD NFR-3.1-NFR-3.3; QR-001/002/005 |
| AD-11/13/14; seed/budget/outcome/production reconciliation handoff | FR-045-FR-052; SC-010/011 | Story 12.2 seed order; August design §§6/9; PRD overlay FR-4.4/4.12; QR-001/005 |
| Host boundary, production-composed qualification, and exclusions | FR-053-FR-056; SC-012 | August host-runtime proposal; Story 12.1 runtime; Story 12.4/12.5 boundaries; NFR-4.1 dated replacement; QR-005 |

Coverage count: **56/56 FRs, 12/12 SCs, and 28/28 acceptance scenarios**. No requirement is deferred inside Story 12.3.

## Planned-Change Authorization Disclosure

Independent plan-only review is required before the root requests authorization. The later request must disclose this exact matrix.

| Trigger | Effect and implementation intent | Affected plan material | PRD/authority clauses |
| --- | --- | --- | --- |
| Adds/materially changes Worker Agent tool | one closed `navigate_code` function; eight operations; durable 8/20/64KiB/depth-3 budget; metadata-only bound receipts; ten-turn loop; no other/raw tool | AD-3-5; navigate contract; research R-4-6; test seams 4-5 | FR-4.3/4.4/4.9/4.12; FR-11.6 dated replacement; NFR-2.3 |
| Material Scan dispatch/scheduling | exact pair atomic start, explicitly activated single-flight capacity-filling host coordinator, independently scoped current-lease runners, fill-to-`AtCapacity`, completion-as-wake-up, due-retry claim release, versioned pair-wide concurrency 1-4 preserving leases, drain-before-reconcile, canonical claims/order | AD-1-3/14; runtime Production activation/Claim/Terminal; API Start/Resume/Update; research R-11; data model §3 runtime projections | FR-4.2; NFR-1.1 dated 50-150 scope; NFR-3.1/NFR-3.3; QR-001/005; PRC3-001 |
| Material retry/pause/resume/failure | pair provider circuit, fixed source-free active request, one versioned driver over five windows, bounded Retry-After, one job retry, classified pauses/fails, no post-fifth/background resume, manual pair resume | AD-10-12/14; runtime Provider circuit/Recovery; research R-10/11; data model transitions | NFR-3.1-3.3; FR-4.2; QR-002/005 |
| Material result/Document behavior | authoritative v2 JSON, exact Story 12.2 communication bindings, server-derived Markdown/sections/index, exact anchors, atomic job/Document commit, full scalar graph replay including reconciled Edge union | AD-6-9/12; schema/projection/canonical/reconciliation contracts | FR-4.3/4.6/4.7/4.10; FR-11.7; Story 12.1 atomic result commit; QR-004 |
| Material reconciliation/handoff | exact local post-Pass1 Edges, immutable Documents, `Resolving` handoff only | AD-11/13; reconciliation contract | FR-4.4 and FR-4.12 dated replacement; FR-6.1/6.2 remain Story 12.4 |
| Explicit compatibility correction | warning-free Complete, honest FailedPass1 atoms, v2 schema allowlist, nullable exact byte anchors; historical rows untouched | AD-12; data model §§8-10; migration | accepted Story 12.1 state representation and immutable Document constraints; no requirement weakening |
| Host analysis-tool use | typed CodeGraph/JDT-LS/source facade only; pinned host runtime; production-composed approved-pair qualification; no CodeGraphContext | AD-4/14; real-tool qualification | FR-4.9; NFR-2.3; NFR-4.1 August replacement; QR-005 |

Quality Review correction decision `story-12-3-quality-review-correction-01` authorized this architecture to be planned, not accepted or implemented. The new AD-14 host execution service plus the materially stricter dispatch/probe/navigation/replay behavior are a fresh planned-change trigger under `AGENTS.md`. After independent plan-only ACCEPT, the root must bind these corrected artifact hashes to the required planned-change authorization before task regeneration. That authorization proceeds to tasks only and does not waive task self-review, full definition review, implementation approval, TDD RED/GREEN, convergence, quality review, required economical E2E, security/migration/real-tool qualification, final verification, or root-owned checkpoint/state/commit.

## Plan-Review Correction Closure

| Finding | Normative closure | Test seam |
| --- | --- | --- |
| PR-001 | AD-2; data model §§1-2; canonical envelope §2.1; runtime Active concurrency update; API Update concurrency. Initial/effective limits are distinct, the command is authorized/versioned/audited/replayable, valid leases survive, and only later claims use the new limit. | `Pass1ConcurrencyUpdateTests` plus PostgreSQL claim/update races: 4->1, 1->4, equal/no-op, lost-ack replay, stale/changed actor/target, hidden ownership, paused/reconciling/terminal. |
| PR-002 | AD-6; strict schema `communicationClassificationEvidence`; projection Semantic validation. Kind is closed `Rpc|Http`; every echoed ID/fingerprint/kind/level/confidence/rule/provenance/Catalog Rule Identity/Evidence Locator must exactly equal the supplied Story 12.2 row and enclosing kind. | `CodeLogicFileV2SchemaTests` single-field and mixed-kind mutations plus `Pass1DocumentCommitTests` zero-row checks. |
| PR-003 | AD-5; data model §§4-5; canonical envelope §5; navigate Bounds/receipt; deferred `BoundExceeded` schema/projection semantics. Results/bytes/depth refusals and accepted-call/tool-attempt exhaustion have metadata-only proof that cannot authorize evidence or a relationship. | `NavigateCodeContractTests` first depth-four/no-prior-receipt, results, bytes, call/attempt-eight marker, retry/replay/stale marker, and positive-claim misuse vectors. |

These corrections preserve the accepted specification intent and require a fresh independent plan-only review. They do not authorize tasks or implementation.

## Quality Review 01 Correction Closure

| Finding | Normative closure | Public proof seam |
| --- | --- | --- |
| QR-001 | AD-2/3/11/13/14; research R-11; runtime Production activation, Claim, Worker result, and Terminal sections. Authenticated Start/eligible replay explicitly signals one capacity-filling pair coordinator; Fill launches each durable claim in an independent scope without awaiting prior Workers, Observe uses completion only as wake-up, dispatch closure stops launches, Drain closes in-flight runners, and fresh terminal exhaustion invokes `ReconcileAsync`. All concrete ports are required in production DI. | `Pass1ProductionExecutionTests` resolves `Program` DI, posts two approved manifests, uses held Worker barriers to require simultaneous two/four execution, and observes Start through claims, Documents, retries, drain, reconciliation, and terminal status. Static boundary qualification fails if any executor/dispatcher/committer/driver registration or call edge is absent. |
| QR-002 | AD-10/14; research R-10/11; runtime Provider circuit and Recovery. Configuration resolution is not health. One versioned probe ticket drives a fixed source-free Chat Completions request at durable 10/30/60/120/300-second nominal eligibility with accepted jitter/Retry-After; fifth transient failure pauses and removes activation; no sixth or startup/background resume exists. | `Pass1ProductionProviderOutageTests` uses fake HTTP/fake time through production composition and asserts exact request count/times/content, zero claims while probing, 401/403 correction pause, fifth-failure provider pause/Chinese action, and no sixth call after arbitrary time advance/restart. |
| QR-003 | AD-5; research R-4/6; navigate Durable receipt contract; data-model §§3-5. The server binding/request/scope carries pair/job/version/token/expiry outside model arguments. Scope read and receipt commit both revalidate current Running unexpired lease plus Running/closed pair before evidence release or mutation. | `NavigateCodeSecurityTests` and PostgreSQL receipt races cover expired/replaced lease, paused/probing pair, Completed job, terminal pair, and authorization change between analysis and commit; all return nondisclosing `Unavailable`, disclose no evidence, invoke no forbidden read when rejected early, and write zero receipt/counter rows. |
| QR-004 | AD-9; research R-9; projection Independent replay and runtime Atomic Document commit. Completed replay regenerates canonical content and compares every Anchor/Section/Index/direct-Edge scalar/order/cardinality; a pure reconciliation projector validates any post-Pass1 Edge union. Fingerprints are outputs, not trust inputs. | `Pass1DocumentPostgreSqlAtomicTests` remove/mutate/reorder/duplicate every graph class and relationship cardinality and assert zero-write conflict/integrity; exact pre- and post-reconciliation replays remain mutation-free. |
| QR-005 | test slices 10/12 and Verification Strategy. Constructor/literal/runtime-version tests remain supporting only. One authenticated PostgreSQL production-composed test and one outage sibling must traverse the real host DI graph; final host qualification drives an approved pair and real typed navigation to Story 12.3 terminal state. | `Pass1ProductionExecutionTests`, `Pass1ProductionProviderOutageTests`, strengthened `Pass1ProductionBoundaryQualificationTests`, and `Pass1HostRuntimeQualificationTests` jointly prove the reachable pipeline, outage policy, pinned host tools, PostgreSQL-only Docker topology, and exclusions. |

All five findings retain the accepted Story 12.3 specification intent. QR-001 introduces the disclosed AD-14 architecture decision and invalidates downstream tasks, definition review, implementation, convergence, quality review, E2E decision, and verification until rerun. No finding authorizes Story 12.4/12.5 work.

## Plan Review 03 Correction Closure

| Finding | Normative closure | Public proof seam |
| --- | --- | --- |
| PRC3-001 | AD-14, research R-11, data model §3 runtime coordinator projection, runtime Production activation/Active concurrency/Claim/Terminal contracts, API Start/Update, and test slices 3/10 define one pair coordinator with mandatory Fill -> Observe -> Drain -> Reconcile behavior. Fill launches independently scoped runners until durable `AtCapacity`/ineligible/closed state and cannot await one runner before the next claim. Completion only wakes a durable reload. Dispatch closure immediately stops launches; 4->1 preserves leases and blocks replacements; 1->4 fills three additional slots; reconciliation requires local drain plus fresh durable terminal exhaustion. | `Pass1ProductionExecutionTests` uses authenticated production composition, disposable PostgreSQL, and controllable barriers/counters. It must deadlock/fail a sequential implementation, observe simultaneous two/four Workers, prove never over limit, exercise held-runner 4->1 and 1->4, stop-launch/drain-before-reconcile, and compare deterministic identities/order/Edges/warnings across at least ten release schedules. |

The correction narrows AD-14 to the concurrency behavior already required by FR-006/SC-001 and does not add a new datastore, API, tool, provider policy, or Story 12.4/12.5 behavior. QR-002 through QR-005 remain unchanged and retain their accepted contracts/proof seams. Fresh independent plan-only review is required; no acceptance or task/implementation authority is claimed.

## Explicit Exclusions

- no Cross-Repository Resolution or Cross-Repository REFERENCES/Uncertainty decision;
- no endpoint-centered flow grouping or `retrieve_code_logic`;
- no Pass 2 or Business Logic Document;
- no Chat, final release UI/dashboard, evaluation questions, or release run;
- no CodeGraphContext, raw LSP/CodeGraph storage, shell/network/filesystem/database Worker tool;
- no automatic background resume or broad provider failover;
- no database startup sweep/polling of dormant pairs, no sixth provider probe, and no provider health request containing source/context/Document content;
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
- activation loss at process shutdown: channel is deliberately non-authoritative; authenticated Resume revalidates durable state and re-signals, while startup discovery remains excluded;
- pair-pump lifetime/DI scope: singleton hosted service holds only activator/scope factory/single-flight metadata; every database/provider job runs in a fresh scope and PostgreSQL remains authoritative;
- capacity-filling coordinator races: local handles are lifetime/wake-up only; production barriers at two/four, active 4->1/1->4, closure/drain, and varied release schedules prove overlap without creating a second capacity/order authority;
- full replay after reconciliation: the same pure reconciliation projector reconstructs the separately authorized Edge union; row-level mutation/duplicate tests prevent fingerprint-only acceptance;
- safe target source binding for unselected files: navigation intersects only Story 12.2 safely eligible approved inventory and exact revision reader; outside inventory is unavailable.

## Inline Self-Review

| Lens | Evidence | Verdict |
| --- | --- | --- |
| Feasibility | Current source already has atomic Start/Claim/failure/probe/reconcile/commit methods, deterministic packets/projectors, typed analysis ports, and Web hosting. AD-14 uses a standard `BackgroundService`/scope-factory coordinator with independent runner scopes and completion handles; no new datastore or framework is required. | PASS |
| Invariants | Complete-or-none start, durable pair capacity/order, mandatory fill-to-capacity, completion-as-wake-up, drain-before-reconcile, exact current claim authorization, double lease checks, bounded navigation/refusal proof, exact classification/v2 authority, atomic commit/full-graph replay, seed/budget and immutable reconciliation are normative. | PASS |
| Failure behavior | Every production dispatcher/commit/probe outcome has a closed durable route; circuit/terminal closure stops launches immediately and drains current runners; the exact five probes and one job retry use persisted state; fifth failure removes activation; process loss requires explicit Resume; no abandoned Running lease is treated as success. | PASS |
| Test seams | Twelve public-seam slices name concrete production owners, authenticated barrier-based overlap/drain and outage tests, PostgreSQL graph corruption races, and real-host proof. Static/runtime-version qualification is explicitly supporting only. | PASS |
| Migration/compatibility | The accepted additive migration/no-backfill/v1-v2 coexistence remains unchanged; the correction adds no EF model delta, preserves Story 12.1 job/lease semantics and Story 12.2 prepared identity, and leaves legacy ports untouched. | PASS |
| Security | One closed function is bound to current lease/version outside model arguments; stale navigation cannot disclose evidence or mutate receipts; health request is source-free; no raw tools/protocol/storage/status/host path/secret enters provider-visible/status/Document content. | PASS |
| Determinism/replay | Durable pair row remains sole capacity/order authority; activation and completion signals carry no job-selection authority; due retries and next eligibility are store-derived; barrier-release permutations preserve identity/order; replay compares scalar graph rows and pure reconciled Edge union rather than trusting fingerprints. | PASS |
| Constitution/ADR | All five Constitution principles pass; canonical Ubiquitous Language used; no ADR/context exists; no exception. | PASS |
| Authority/scope | All 56 FRs, 12 SCs, 28 scenarios covered; Story 12.1/12.2 preserved; August overlays/host correction explicit; Story 12.4/12.5 excluded. | PASS |
| Planned-change governance | AD-14 and material dispatch/probe/navigation/replay effects are disclosed with PRD clauses. Correction planning was authorized; independent plan review and hash-bound planned-change authorization remain before regenerated tasks. | PASS |
| Review correction | Prior PR-001..003 remain closed. PRC3-001 now forbids sequential conformance and requires observable overlap/drain tests. QR-001 is strengthened; QR-002..005 retain their accepted normative contracts and proof seams without specification weakening. | PASS |
| Placeholder check | No unresolved clarification, template marker, user choice, task list, or implementation code is present. | PASS |

**Self-review verdict**: **PASS — ready for independent plan-only review.** Task generation and production implementation remain unauthorized.

## Complexity Tracking

No Constitution violation requires justification.
