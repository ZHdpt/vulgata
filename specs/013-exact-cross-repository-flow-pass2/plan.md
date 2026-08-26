# Implementation Plan: Exact Cross-Repository Flow & Pass 2

**Branch**: `demo0831/story-12-4` | **Date**: 2026-08-26 | **Spec**: [spec.md](spec.md)

**Input**: Story 12.4 specification and checklist, BMad Story 12.4, the August 30 bounded-scanning architecture sections 11-12, the approved 2026-08-17 recovery proposal, PRD FR-4.5/FR-6.1/FR-6.2, the Ubiquitous Language, Constitution, and the completed Story 12.1-12.3 contracts and source at `7f088c3085d20290e1ab11b6ab059b840282f2a8`.

## Summary

Consume one terminal Story 12.3 prepared pair and deterministically reconstruct current communication candidates from its immutable Code Logic Document Versions, Source Anchors, exact same-Repository REFERENCES Edges, Communication Kind Classifications, and the closed Communication Pattern Catalog. Resolve only the complete `(protocol, normalized endpoint identifier)` key. Canonically aggregate all eligible evidence occurrences owned by the same current provider XML Document Version before cardinality. Exactly one distinct current authorized provider Document Version creates one durable endpoint/consumer-entry exact-match decision and creates or reuses the single document-level consumer-to-provider REFERENCES Edge; many distinct decisions may share that Edge. Every zero, multiple-distinct-provider, conflicting, stale, unsupported, or unsafe-normalization result receives its own durable decision plus an honest Uncertainty.

From each exact-match decision—not merely each reusable document Edge—plan at most ten canonically ordered endpoint-centered Flow Groups. A complete group contains the consumer Code Logic Document Version, its exact resolution decision, the shared-or-new provider REFERENCES Edge, provider XML Code Logic Document Version, provider implementation Code Logic Document Version, and at most one validated same-Repository relationship hop, within eight Document Versions and sixteen evidence steps. Deepen the existing `FlowGroup`/`FlowTrace` model so a pair-specific Flow Group instance and its completeness state exist before publication, while a separately persisted cross-scan `business-flow-logical-key/1.0` joins later executions of the same durable endpoint-centered semantic flow into one immutable Business Logic Version Chain. Create exactly one durable Pass 2 Job for each complete group.

The Pass 2 Worker Agent runs through the Story 12.3 bounded streaming/strict-tool transport pattern with exactly two local functions: closed evidence access through `retrieve_code_logic` and non-evidence finalization through `submit_business_logic`. Retrieval is server-scoped, durably budgeted, and cannot alter resolution or membership. The accepted submission cites opaque evidence handles; Vulgata derives, revalidates, and atomically persists one immutable Business Logic Document Version, its Flow Group/trace publication, job completion, and one GENERATED_FROM Edge for every and only every distinct Code Logic Document Version actually used. Failed, incomplete, and release-bound-excluded groups create no Business Logic Document and remain visible in terminal accounting without rolling back successful groups or rewriting resolution facts.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`); strict UTF-8 JSON and canonical binary identity envelopes

**Primary Dependencies**: EF Core/Npgsql 10.0.9; Microsoft.Agents.AI/OpenAI-compatible streaming stack already used by Story 12.3; existing `IRevisionBoundSourceReader`, `IKnowledgeStoreRepository`, `RepositoryAccessScope`, safe XML/catalog evaluator, exact-resolution store, prepared Pass 2 packet/result validator, and knowledge materializer

**Storage**: PostgreSQL 17 `vulgata` schema; one cohesive additive/compatibility EF migration; SQLite remains an economical logic double and is not relational evidence

**Testing**: xUnit; pure randomized-order resolution/planning tests; strict schema/wire and scripted streaming tests; authenticated `WebApplicationFactory<Program>` API/composition tests; disposable PostgreSQL atomicity/concurrency/replay/migration tests; exact two-Repository source/catalog qualification; opt-in live configured-provider Pass 2 qualification

**Target Platform**: current Windows-host topology with PostgreSQL 17 and the Story 12.1 revision-bound source/runtime services; direct Pass 2 CodeGraph/JDT-LS is disabled by default and optional

**Project Type**: multi-project Blazor/ASP.NET Core application with Core contracts/entities, Infrastructure persistence/source/catalog services, Agents provider execution, Shared API DTOs/validation, and Web host/API coordination

**Performance Goals**: deterministic results across at least ten randomized enumeration/completion orders; at most ten Flow Groups, eight Document Versions and sixteen evidence steps per group, one same-Repository hop, one active job per group; serial canonical dispatch for the dated release; retrieval at most eight calls, ten results, four additional Documents, four sections per Document, and 65,536 cumulative returned UTF-8 bytes

**Constraints**: exactly two Repositories in one authorized System and their exact Profiled Revisions; Custom RPC mandatory, HTTP fail-closed unless the closed normalizer proves a safe exact form; no fuzzy/LLM provider or membership authority; initial provider packet <=131,072 UTF-8 bytes and accepted submission <=65,536 UTF-8 bytes under existing Worker bounds; no source content or provider payload persisted in operational receipts/logs; no Story 12.5 Chat/UI/release evidence

**Scale/Scope**: one completed Story 12.3 pair; zero to ten complete/incomplete Flow Groups plus bounded omission records; zero or one current Business Logic Document Version per successful Flow Group completion

No clarification remains. Research decisions are closed in [research.md](research.md).

## Constitution Check

### Pre-design gate

| Principle | Plan evidence | Result |
| --- | --- | --- |
| I. Layered Project Boundaries | Core owns closed entities/contracts; Infrastructure owns EF, catalog/source reads and transactions; Agents owns the strict provider loop/functions; Shared owns validated API DTOs; Web owns authorization, endpoints, activation and hosted execution. | PASS |
| II. Domain Purity & Ubiquitous Language | Uses Cross-Repository Resolution, REFERENCES Edge, Uncertainty, Flow Group, Document Version, GENERATED_FROM Edge, Source Anchor and Profiled Revision verbatim. No generic repository, `IQueryable`, `DbSet`, data annotation, or new Edge type is introduced. | PASS |
| III. Validation & API Contract Discipline | New `/api/pass2-executions` endpoints use FluentValidation, ProblemDetails, Chinese safe messages, ownership-filtered 404, and correct 401/403 behavior. The plural Pass 2 endpoint group is registered through the existing `MapVulgataApiEndpoints()` composition seam. | PASS |
| IV. Test-First Integration Harness | Every behavior has a public/pure seam and a named PostgreSQL boundary; migration lifecycle and real provider/source qualification are explicit. | PASS |
| V. Security & Data Stewardship | Server-owned scope/currentness, closed retrieval, opaque handles, configured provider boundary, content-dark receipts/logging, no raw graph/shell/network/database tool, and no unauthorized-content distinction. | PASS |

No Constitution exception is requested. No `CONTEXT-MAP.md`, `CONTEXT.md`, system ADR directory, or context-scoped ADR exists at the pinned HEAD, so no ADR/context conflict is present to waive.

### Post-design gate

The completed data model and contracts preserve all five principles. The migration is additive except for making the existing Flow Group-to-Business-Logic-Document association optional before publication and broadening legacy exact-evidence constraints through explicitly versioned compatibility shapes. Existing rows retain their meaning; no Document Version is mutated. Result: **PASS**.

## Project Structure

### Documentation (this feature)

```text
specs/013-exact-cross-repository-flow-pass2/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── spec.md
└── contracts/
    ├── business-logic-flow-v1.schema.json
    ├── exact-resolution-and-flow-planning.md
    ├── pass2-runtime-and-commit.md
    └── retrieve-code-logic.md
```

`tasks.md` is deliberately absent; task generation follows independent plan review and the repository-mandated planned-change authorization.

### Planned production ownership

```text
src/dotnet/
├── Vulgata.Core/
│   ├── Entities/
│   │   ├── ExactCrossRepositoryEdgeEvidence.cs   # unchanged legacy one-to-one profile evidence
│   │   ├── CrossRepositoryResolutionDecision.cs # v2 endpoint/entry outcome; many-to-one Edge
│   │   ├── ExactResolutionProviderEvidenceContribution.cs
│   │   ├── Uncertainty.cs                        # additive current-pair reasons/protocol
│   │   ├── FlowGroup.cs                          # pre-publication state/key/fingerprint
│   │   ├── FlowTrace.cs / FlowTraceStep.cs       # planned evidence path reused
│   │   ├── Pass2Execution.cs
│   │   ├── Pass2Omission.cs
│   │   ├── Pass2JobExecution.cs
│   │   ├── Pass2RetrievalBudget.cs
│   │   ├── Pass2RetrievalReceipt.cs
│   │   └── Pass2RetrievalResult.cs
│   └── DomainServices/
│       ├── ExactResolutionContracts.cs           # deepen, retain legacy adapter
│       ├── PreparedPass2Flow.cs                   # current endpoint-centered v2 shape
│       ├── PreparedPass2RuntimeBinding.cs         # current pair/group binding
│       ├── Pass2CanonicalCodec.cs
│       ├── Pass2ExecutionContracts.cs
│       ├── Pass2RetrievalContracts.cs
│       ├── IExactCrossRepositoryResolver.cs
│       ├── IExactCrossRepositoryResolutionStore.cs
│       ├── IPreparedPass2Store.cs
│       ├── IPass2CodeLogicRetrievalService.cs
│       ├── IPreparedPass2WorkerDispatcher.cs
│       └── IPass2BusinessLogicDocumentCommitter.cs
├── Vulgata.Infrastructure/
│   ├── CrossRepository/
│   │   ├── ExactCrossRepositoryResolver.cs       # production current-evidence overload
│   │   ├── CurrentCommunicationCandidateSource.cs
│   │   └── ExactEndpointNormalizer.cs
│   └── Data/
│       ├── ExactCrossRepositoryResolutionStore.cs
│       ├── PreparedPass2FlowPlanner.cs            # replaces demo planner in composition
│       ├── PreparedPass2Store.cs
│       ├── Pass2CodeLogicRetrievalService.cs
│       ├── Pass2BusinessLogicDocumentCommitter.cs
│       ├── Configurations/{exact,flow,pass2,retrieval}*.cs
│       ├── VulgataDbContext.cs
│       └── Migrations/{migration,designer,VulgataDbContextModelSnapshot}.cs
├── Vulgata.Agents/
│   ├── WorkerExecution/
│   │   ├── PreparedPass2WorkerDispatcher.cs
│   │   ├── PreparedPass2JobExecutor.cs
│   │   ├── PreparedRetrieveCodeLogicFunction.cs
│   │   ├── PreparedSubmitBusinessLogicFunction.cs
│   │   └── Pass2StrictToolDeclarations.cs
│   └── Prompts/pass2-business-logic-worker.v1.md
├── Vulgata.Shared/Scans/
│   ├── Pass2ExecutionContracts.cs
│   └── Pass2ExecutionValidators.cs
└── Vulgata.Web/
    ├── Api/
    │   ├── ApiEndpointRegistrationExtensions.cs # central MapVulgataApiEndpoints composition
    │   └── Scans/Pass2ExecutionEndpoints.cs
    ├── Data/PreparedPass2Coordinator.cs
    ├── Data/PreparedPass2ExecutionActivator.cs
    ├── Data/PreparedPass2ExecutionHostedService.cs
    ├── Data/PreparedPass2JobRunner.cs
    └── Program.cs
```

### Planned test ownership

```text
tests/Vulgata.Tests/PreparedPass2/
├── ExactCrossRepositoryResolutionTests.cs
├── ExactCrossRepositoryPostgreSqlTests.cs
├── Pass2FlowPlanningTests.cs
├── Pass2FlowPlanningPostgreSqlTests.cs
├── RetrieveCodeLogicContractTests.cs
├── RetrieveCodeLogicSecurityTests.cs
├── RetrieveCodeLogicPostgreSqlBudgetTests.cs
├── Pass2StrictSubmissionTests.cs
├── PreparedPass2JobExecutorStreamingTests.cs
├── Pass2DocumentCommitTests.cs
├── Pass2DocumentPostgreSqlAtomicTests.cs
├── Pass2ExecutionRecoveryTests.cs
├── Pass2PartialCompletionTests.cs
├── Pass2ExecutionApiTests.cs
├── Pass2MigrationTests.cs
├── Pass2ProductionCompositionTests.cs
└── Pass2ProductionQualificationTests.cs
```

**Structure Decision**: Deepen existing exact-resolution, Flow Group/trace, Pass 2 packet/result/committer and knowledge-store seams. The legacy embedded prepared-demo profile/catalog remains a characterized regression oracle and compatibility entry point but is not production resolver configuration. `PreparedDemoPass2FlowPlanner` remains readable for legacy callers; current Story 12.4 composition resolves `PreparedPass2FlowPlanner`. A specialized `Pass2JobExecution` is necessary because `ScanJobExecution` is relationally bound to one File Code Unit, Repository and Scan Run and cannot truthfully represent a cross-Repository Flow Group; both reuse the same lease/failure semantics rather than sharing an invalid table shape.

## Repository Fit and Compatibility Findings

1. `CommunicationKindClassification` is intentionally kind-only. It cannot become provider/consumer/endpoint authority. Story 12.4 reopens the exact Source Anchor-backed source under its Profiled Revision, re-evaluates the closed catalog rule, and binds every resulting evidence occurrence to its immutable Code Logic Document owner. Multiple locators/classifications owned by the same current provider XML Document Version are aggregated before provider cardinality.
2. Story 12.3 `deferredObservations` supplies a source-anchored cross-Repository target symbol/path but not a provider decision. A consumer candidate is eligible only when a Confirmed communication item and a `CrossRepository` deferred observation share exact Source Anchor evidence and the protocol-specific normalizer accepts the target identifier.
3. Existing `ExactCrossRepositoryEdgeEvidence` is a prepared-demo/profile-shaped one-to-one extension of a unique document-level Edge and cannot own v2 endpoint/entry multiplicity. It remains unchanged and readable for legacy `1.0` callers. New `CrossRepositoryResolutionDecision` rows own every v2 exact or unresolved endpoint/entry outcome; many exact decisions may reference one reusable REFERENCES Edge, while one unresolved decision owns one Uncertainty.
4. Existing `FlowGroup` can exist only under an already-created Business Logic Document. Its relationship becomes optional until publication, while existing `FlowTrace`/`FlowTraceStep` rows become the authoritative planned evidence path. Its pair-specific identity remains distinct from the existing consumer-owned `LogicalDocument.StableKey`, whose unique Repository/type/key index and concurrency-token current pointer already provide the cross-scan Version Chain seam.
5. Existing Pass 2 packet/validator/committer already understand Business Logic Documents and GENERATED_FROM. They require a v2 runtime binding, retrieved-evidence handles, all-and-only-used derivation, and atomic job completion; legacy entry points remain characterized.

## Architecture Decisions

### AD-1: Reconstruct production candidates from current persisted evidence

`CurrentCommunicationCandidateSource` reads only the exact terminal Story 12.3 pair, its two manifests, current Code Logic Document Versions, Source Anchors, supplied Confirmed classifications, `communicationEvidence`, `CrossRepository` deferred observations, and exact same-Repository REFERENCES Edges. It revalidates System, Repository, Scan Run, Profiled Revision, current Logical Document pointer, hashes, catalog identity, and authorization before returning a closed candidate set.

For Custom RPC consumers, only a grammar-valid target symbol/path from a source-anchor-overlapping deferred observation can become the raw endpoint. The projector also derives a cross-version `ConsumerEntryStableKey` from the consumer Repository, normalized repository-relative path, closed source-derived containing semantic owner (qualified type/member signature for code or catalog-declared stable XML locator), closed communication-site kind, and the structural occurrence ordinal among supported outbound sites within that owner. It excludes protocol/endpoint target value, pair, scan, Document Version, anchor range/hash, and evidence-row order. For providers, every safe XML catalog match reproduced at a persisted Evidence Locator becomes a contributing observation owned by the current XML Code Logic Document containing that anchor. HTTP candidates require the closed safe HTTP form in AD-2 on both roles. The free-form observation prose, Index Entry text, raw CodeGraph proximity, and LLM output are never candidate sources.

Alternative: persist endpoint/provider roles retroactively into Story 12.2 classifications. Rejected because the Ubiquitous Language says classifications are kind-only and Story 12.4 may not reinterpret an accepted Story 12.2 contract.

### AD-2: One closed protocol-aware normalization registry

The canonical key is exactly `(protocol, normalized endpoint identifier)` with ordinal comparison. Custom RPC normalization permits Unicode NFC, surrounding-whitespace removal, and only the exact fixed-prefix removal declared by the matched catalog rule; the remainder must satisfy the closed endpoint grammar. It does not case-fold or fuzzily shorten qualified names.

Safe HTTP accepts only catalog-identified, queryless and fragmentless HTTP/HTTPS absolute or origin-form endpoints with no userinfo, backslash, control character, ambiguous dot segment, or percent-encoded reserved character. Scheme and ASCII host are lowercased, a default port is removed, and path/trailing-slash behavior changes only when the catalog rule explicitly declares that normalization. Anything else yields `unsafe_normalization` and an Uncertainty.

Alternative: `System.Uri` canonical text, decoding, route-template similarity, or model-assisted matching. Rejected because implicit normalization can collapse distinct endpoints and violate the exact-key requirement.

### AD-3: Unique distinct current provider Document Version is the only Edge authority

The pure resolver first canonicalizes evidence-granular provider observations, then groups them by protocol/key and `ProviderIdentity = (SystemId, RepositoryId, ScanRunId, ProfiledRevision, current provider XML DocumentVersionId)`. Every valid locator, classification, Source Anchor, catalog rule and normalization rule for the same provider identity/key is sorted, deduplicated and retained in one aggregated provider candidate/fingerprint. Provider cardinality is counted only over these distinct provider identities, never over contributing evidence rows. Zero distinct providers, more than one distinct provider Document Version, role conflict, stale/unauthorized evidence, unsupported kind, or unsafe normalization emits a closed Uncertainty decision. Exactly one distinct eligible provider XML Document Version emits one exact-match decision and binds it to the canonical REFERENCES Edge for `(consumer DocumentVersionId, provider DocumentVersionId, References)`, creating that Edge only if absent. Two endpoints or consumer entries in the same source Document that resolve to the same provider Document therefore retain two decisions but share one graph Edge. Both Repositories are evaluated as potential consumer/provider so output is independent of scan completion and evidence-multiplicity order.

The provider implementation Document is not provider-selection evidence. It is obtained later through exactly one current same-Repository REFERENCES Edge from the provider XML Document and is a Flow Group completeness requirement.

Alternative: use graph distance, the implementation target, preferred Repository order, or LLM selection to break ties. Rejected because all would guess a provider.

### AD-4: First-class v2 decisions own exact evidence; document Edges remain reusable

`ExactCrossRepositoryResolutionStore` retains its legacy profile adapter and unchanged one-to-one `ExactCrossRepositoryEdgeEvidence`. It adds `CrossRepositoryResolutionDecision`, one durable `exact-cross-repository-resolution/2.0` row for every consumer candidate outcome. Its deterministic identity/unique slot includes Pass 1 pair, current consumer-entry identity, protocol, normalized endpoint and consumer candidate; exact-match shape additionally requires the distinct Provider identity and shared `DocumentEdgeId`, while unresolved shape requires one owned `UncertaintyId` and no Edge. The decision stores complete consumer/provider aggregate fingerprints and the exact document/revision bindings.

The document Edge semantic identity is only source Document Version, target Document Version and `References`, matching the existing uniqueness constraint. If absent, its new ID is deterministically derived from that tuple; if a valid existing/legacy Edge already owns the tuple with another ID, the decision reuses that actual row after full owner/type validation rather than attempting a duplicate. One transaction canonicalizes exact decisions, sorts unique Edge tuples, takes tuple-scoped locks, and creates or revalidates each Edge once before inserting every decision that references it. `ExactResolutionProviderEvidenceContribution` attaches only to its `ResolutionDecisionId`, retains provider identity/Document/anchor/classification/catalog/locator/normalization evidence in canonical ordinal order, and makes the complete aggregate reconstructable rather than trusting its digest. Two exact decisions sharing an Edge retain independent contribution sets and decision fingerprints.

Exact replay reconstructs the complete decision batch, each decision-to-Edge/Uncertainty owner, every contribution, and the deduplicated Edge set and writes nothing. Same decision identity/slot with changed endpoint/provider/evidence/Edge or missing/extra contribution conflicts with zero mutation. Concurrent identical starts converge on the same Edge and decisions; concurrent different pair decisions with the same document tuple may reuse the committed Edge only after complete tuple/type validation.

An unresolved v2 decision owns exactly one Uncertainty. It is immutable for that pair/current evidence. Only a later exact decision with new current decision identity may atomically mark that prior Uncertainty resolved through `ResolvedUncertaintyId`; it never overwrites or reuses the old decision. Legacy exact evidence/Uncertainties remain readable, receive no inferred v2 backfill, and never seed v2 Flow Groups because their endpoint/entry multiplicity cannot be reconstructed safely.

Alternative: keep endpoint evidence on the one-to-one Edge extension. Rejected because the graph's source/target/type uniqueness cannot represent two endpoint/entry decisions. Alternative duplicate Edges are rejected because they violate the accepted graph contract. The new decision is evidence about one graph Edge, not a competing Edge type or graph truth.

### AD-5: Flow Group instance identity and Logical Document stable identity are separate

`PreparedPass2FlowPlanner` starts only from current v2 exact-match decisions and produces two explicit canonical identities:

1. `FlowGroup.Id` is the execution-instance identity, UUIDv8/SHA-256 over `endpoint-flow-group/1.0`, Pass 1 pair execution, System, protocol, normalized endpoint, current consumer-entry identity, exact resolution decision, shared-or-new Edge, current provider XML Document Version, provider Repository and planner schema. It intentionally changes for a later scan pair/current evidence graph and remains distinct for two endpoint/entry decisions that share one Edge.
2. `LogicalDocumentStableKey` is the cross-scan semantic identity, encoded as `business-flow/v1/` plus the lowercase SHA-256 of the canonical `business-flow-logical-key/1.0` envelope: System ID, consumer Repository ID, provider Repository ID, protocol, normalized endpoint identifier, and `ConsumerEntryStableKey`. It excludes pair/execution/Scan Run/Profiled Revision/Document Version/Source Anchor/evidence/Edge/planner identity, so later current versions of the same semantic flow resolve to the same consumer-owned Business Logic Logical Document.

`ConsumerEntryStableKey` uses the source-derived v1 locator defined in AD-1; a file/member or catalog-locator semantic rename honestly creates a new flow slot unless a separately reviewed alias migration proves equivalence. Changing endpoint, protocol, consumer Repository, provider Repository, or System also creates a new Logical Document key and leaves the prior chain intact. Execution/planner/resolution schema changes do not change the logical key. A change to the semantic locator or endpoint canonicalization contract requires a new `business-flow-logical-key` schema version; it may reuse an old chain only through an explicit reviewed key-alias/data migration and never by silent fallback.

Canonical core membership is consumer Document, provider XML Document, and the unique current provider implementation Document, followed by at most one hop of exact same-Repository REFERENCES targets sorted by role, Repository UUID bytes, Document Version UUID bytes, Edge UUID bytes.

Every member and step is revalidated current, authorized, Source Anchor-backed where source is required, and bound to the pair Profiled Revision. Raw graph proximity and LLM output never enter the projector. A core member failure yields `Incomplete`; more than eight Documents or sixteen evidence steps yields `BoundExcluded` rather than truncating a supposedly complete group. At most the first ten canonical group candidates become Flow Group rows; later candidates become `Pass2Omission` rows with `flow-group-bound`. Repeated input yields identical group/step bytes.

Alternative: greedily trim optional members or cluster graph neighborhoods. Rejected because silent trimming can make a false complete explanation and general clustering is outside the dated overlay.

### AD-6: Reuse `FlowGroup`/`FlowTrace` as the pre-publication plan

The existing `FlowGroup.BusinessLogicDocumentVersionId` becomes nullable. New versioned execution-instance key, `ConsumerEntryStableKey`, `LogicalKeySchemaIdentity`, `LogicalDocumentStableKey`, state (`Complete`, `Incomplete`, `BoundExcluded`, `Running`, `Published`, `Failed`), plan fingerprint, exact resolution decision/Edge/protocol/endpoint/consumer/provider bindings, and optional bounded reason fields are additive. `ExactResolutionDecisionId` is unique among v2 Flow Groups, while `ExactResolutionEdgeId` is deliberately nonunique. Existing `FlowTrace` and at most sixteen `FlowTraceStep` rows persist the ordered planned evidence path before Worker dispatch; the cross-Repository relationship step adds the exact decision FK alongside its possibly shared Edge FK. A complete Flow Group receives exactly one `Pass2JobExecution`; incomplete/excluded groups never receive one.

Publication attaches the existing Flow Group to the accepted immutable Business Logic Document Version without changing its plan identity or steps. Legacy rows stay attached and retain a legacy shape.

Alternative: create a separate `PreparedFlowGroup` table and later copy into `FlowGroup`. Rejected because duplicate group models can drift and would weaken replay.

### AD-7: One durable Pass 2 execution and specialized jobs

`Pass2Execution` binds the Story 12.3 pair, coordination, resolution/planner fingerprints, prompt/schema/tool identities, exact candidate denominator, and terminal counts. Start atomically applies resolution, persists Flow Groups/omissions, creates exactly one job/budget per complete group, and advances coordination to Pass2 before any claim is observable.

`Pass2JobExecution` uses the Story 12.1/12.3 state, lease, state-version, one-retry, failure-category, and exact-replay rules but binds one Flow Group instead of one File Code Unit. The dated release claims jobs serially in canonical group order. A provider outage uses the same classified, source-free probe schedule and manual-resume behavior as Story 12.3 through shared policy helpers; job-local invalid output retries once, then fails only that group. Restart recovers only expired leases/due retries and skips published jobs.

Alternative: reuse `ScanJobExecution` by making File Code Unit/Repository/Scan Run nullable. Rejected because it would erase that accepted table's one-file identity and still cannot truthfully own a two-Repository group. Alternative in-memory jobs are rejected because restart and acknowledgement-loss replay are required.

### AD-8: `retrieve_code_logic` is the only evidence-expansion tool

The strict function exposes the closed operations `Search`, `Sections`, `SourceAnchors`, `SourceExcerpt`, and `RelatedDocuments`. Server-owned job scope fixes the System, pair Repositories, Profiled Revisions, current Flow Group, and authorization; caller arguments contain no scope override. Search is deterministic lexical matching over current Code Logic Document sections/index entries. Related Documents follow only current persisted REFERENCES Edges by one hop inside the authorized System. Source excerpts use `IRevisionBoundSourceReader` and must be contained by a current Source Anchor and hash.

Before each callback, the store durably reserves the next call ordinal. Results are canonicalized, currentness-checked, bounded, assigned opaque deterministic handles, and admitted atomically into receipts/results; no content is stored. A refusal consumes the call but adds zero result/Document/section/byte budget. Exact same call-ordinal replay rehydrates the same descriptors; a later repeated request consumes a new call and cannot reset bounds. Budgets aggregate across retry/restart.

Alternative: expose database, source, CodeGraph, JDT-LS, open search, or caller-supplied Repository IDs. Rejected for data stewardship, boundedness, and provider-decision integrity. Direct JDT-LS/CodeGraph remains an optional non-authoritative diagnostic capability and is disabled by default.

### AD-9: Strict two-function streaming conversation

`PreparedPass2JobExecutor` reuses the Story 12.3 streaming inactivity/absolute deadline, raw/request/response byte accounting, DeepSeek-compatible strict declaration projection, zero SDK retry, tool-call batch preflight, and terminal-correction pattern under characterization tests. It advertises only `retrieve_code_logic` and `submit_business_logic`; the latter has no evidence access and delegates the candidate to the local schema/semantic validator.

Provider turns one through eight may retrieve or submit; when no result is accepted, turn nine forces sole named submission and turn ten permits one correction after a locally rejected first candidate. No request eleven exists. Multi-call batches are legal only when every call is `retrieve_code_logic`, fit remaining durable call capacity, and pass whole-batch preflight before any callback. Provider text, strict-schema acceptance, and declared used Document IDs are not acceptance authority.

Alternative: extend legacy one-shot free-form Pass 2 JSON or use unrestricted Agent tools. Rejected because it cannot support bounded retrieval, local correction, or reliable all-and-only-used provenance.

### AD-10: Evidence handles, not returned membership, determine use

The initial packet and every retrieval result use opaque handles bound server-side to an exact Document Version, section/Source Anchor/source range, evidence fingerprint, and job/attempt. The submission schema requires every material section and main step to cite handles. Vulgata rejects unknown, stale, prior-attempt-only, unauthorized, or uncited material and derives each section's authorized Document Version IDs plus the global distinct used set from accepted handles.

A Document is not used merely because it was eligible, in the Flow Group, or returned. Every additional used Document must have a current admitted retrieval descriptor and is revalidated immediately before persistence. Retrieval suggestions cannot change the exact decision, its shared document Edge, endpoint, provider, execution-instance Flow Group identity, cross-scan Logical Document stable key, or membership fingerprint echoed by the job binding.

Alternative: trust `generatedFromDocumentVersionIds` authored by the model or create provenance for the whole packet. Rejected because either permits omission or overstates use.

### AD-11: One atomic immutable publication transaction

`Pass2BusinessLogicDocumentCommitter` deepens the existing Pass 2 validation/materialization path. Under the current job lease it revalidates the Flow Group's exact-match decision, that decision's reusable document Edge and complete contribution evidence, every cited handle and used Code Logic Document Version/Source Anchor, reconstructs canonical Business Logic content, derives GENERATED_FROM Edges, and checks that no unsupported provider or relationship is asserted. Another decision sharing the same Edge is not evidence for this group.

The Business Logic Logical Document is owned by the consumer Repository and uses the exact `business-flow/v1/<digest>` key persisted on the Flow Group, not the pair-specific Flow Group ID. The existing unique `(RepositoryId, BusinessLogic, StableKey)` index prevents a second logical slot. Under a stable-key-scoped PostgreSQL transaction/advisory lock, the committer loads or creates the deterministic Logical Document, locks/revalidates its concurrency-token `CurrentVersionId`, and uses that exact predecessor when appending the job-derived immutable Document Version. A later scan pair with the same semantic key appends and supersedes the prior current version in that same Version Chain; a changed endpoint/semantic key creates a separate Logical Document and never mutates the old chain. Competing cross-pair publications serialize on the stable key; after a winner, a loser reloads/revalidates the new current pointer before an allowed append, while exact same-completion replay returns the already-linked version.

Provider source remains anchored through its Code Logic Document; it is not copied into a consumer-owned Source Anchor. Section `DocumentVersionIdsJson`, GENERATED_FROM Edges, Flow Group publication, Flow Traces, job completion, predecessor `SupersededById`, and current-version pointer move occur in one PostgreSQL transaction. Old version bodies and provenance are never edited or deleted.

Exact replay reconstructs the complete Document, section, index, Flow Group/trace, provenance and job graph and returns the existing version with zero writes. Changed replay or concurrent mismatch conflicts with zero mutation.

Alternative: commit the Document then mark the job, or create GENERATED_FROM from the requested list. Rejected because acknowledgement loss could duplicate or expose partial provenance.

### AD-12: Honest independent terminal outcomes

Incomplete and release-bound-excluded groups never dispatch. A failed job marks only its Flow Group failed and processing continues canonically. Successful publications stay committed. Pass 2 never rewrites its preceding REFERENCES Edge or Uncertainty decision.

After every candidate is published, failed, incomplete, or omitted, one transaction stores terminal counts/warning inventory and completes the Two-Repository Scan Coordination as `Completed` or `CompletedWithWarnings`. Zero successful groups is `CompletedWithWarnings` with zero Business Logic Documents, unless an integrity/security condition closes the coordination more strongly. Warnings contain only group identity/fingerprint and closed reason codes, not source, secrets, endpoints requiring redaction, provider body, or host paths.

Alternative: roll back the whole pair or synthesize a reduced group after failure. Rejected because valid partial progress and honest omissions are explicit acceptance requirements.

### AD-13: Authenticated API activation, no Story 12.5 UI

Add one plural authenticated Pass 2 endpoint group for create/replay, inspect, and manual resume, and register that group through the existing `ApiEndpointRegistrationExtensions.MapVulgataApiEndpoints()` composition seam already invoked once by `Program.cs`. Story 12.4 does not map Pass 2 routes directly from `Program.cs` or create a second API-registration path. The server resolves ownership from the current user and pair; caller input cannot provide Repository/System/revision/provider/source identities. Unauthenticated requests remain 401, management-policy failures remain 403, and missing or outside-ownership executions are indistinguishable 404. Validation, state, replay, and concurrency conflicts use ProblemDetails with Chinese safe messages. `PreparedPass2ExecutionHostedService` is a wake-up mechanism only; PostgreSQL remains authority and process restart requires authenticated resume.

No Chat route, production progress UI, citation UI, or final release-evidence surface is added.

### AD-14: One cohesive migration with legacy-row compatibility

One migration changes the nullable pre-publication Flow Group association, adds its v2 decision/instance/stable-entry/logical-key/state fields and indexes, creates first-class Cross-Repository Resolution decisions plus normalized provider-contribution evidence, extends Uncertainty for decision ownership, and creates Pass 2 execution/job/retrieval/omission tables. The existing Document Edge source/target/type uniqueness and legacy one-to-one exact evidence remain unchanged; no unsafe endpoint/entry backfill is attempted. The existing Logical Document Repository/type/stable-key uniqueness and current-pointer concurrency token also remain authoritative. These structures participate in one resolution/start/claim/retrieval/commit/replay invariant and therefore share one integration owner.

`Down` removes only Story 12.4 tables/columns, restores the required Flow Group association after deleting no rows, and is allowed only after v2 pre-publication rows are absent; migration tests exercise rollback on clean and legacy-only fixtures. Existing legacy Documents, attached Flow Groups, exact evidence, Uncertainties, Story 12.1-12.3 rows and model semantics remain readable. No ordinary production data is destructively backfilled.

Alternative: several migrations or replacement tables. Rejected because intermediate schemas cannot satisfy the atomic invariant and duplicate graph truth.

## Failure and State Behavior

| Condition | Closed outcome | Mutation boundary |
| --- | --- | --- |
| zero/multiple-distinct/conflicting provider, protocol mismatch, unsupported kind, unsafe HTTP | Uncertainty | duplicate evidence for one provider is aggregated; distinct provider ambiguity creates no Edge/group job |
| stale/unauthorized/revision/hash drift before resolution | rejected or Uncertainty according to trusted-input validity | no guessed Edge; integrity contradiction stops start |
| required group member absent/multiple/unanchored | Flow Group `Incomplete` + warning | no Pass 2 Job or Business Logic Document |
| group/document/evidence/group-count bound exceeded | `BoundExcluded` Flow Group or `Pass2Omission` | no dispatch; canonical reason retained |
| retrieval scope/currentness failure | indistinguishable bounded refusal | call consumed; zero evidence disclosure/addition |
| retrieval result would exceed any bound | `BoundExceeded` response | call consumed; zero result rows/content |
| provider transient/configuration | shared probe pause/manual correction | existing Documents and resolution facts retained |
| first job-local invalid result | one durable retry | same group/job identity and remaining retrieval budget |
| terminal job-local failure | Flow Group `Failed` warning | other groups continue |
| stale/unknown handle, changed provider/group, unsupported claim, incomplete provenance | result rejected | zero Document/Edge/job-completion writes |
| exact decision/Edge or commit replay/ack loss | existing decision/shared Edge or immutable version returned | zero mutation |
| changed replay/concurrent currentness or Logical Document pointer drift | conflict/reload-and-revalidate | zero mutation until a stable-key-serialized append is valid |
| integrity/security boundary | fail closed | no new partial publication; prior valid Documents retained |

## Migration and Compatibility Decision

**Decision**: one cohesive additive/compatibility migration, owned with `VulgataDbContext`, all affected configurations, migration designer and model snapshot. It must prove clean apply, legacy apply, rollback/reapply where no v2 rows block rollback, pending-model-change absence, and continued readability of the legacy prepared-demo exact-resolution/Pass 2 graph.

No data rewrite or destructive migration is planned. Current Story 12.4 rows use explicit v2 decision/schema identities. Legacy one-to-one exact evidence and null/v1 shapes remain accepted only through existing APIs; they are never backfilled or silently promoted into production decisions because their endpoint/consumer-entry multiplicity is not reconstructable.

## Test and Qualification Strategy

1. **Pure resolution**: exact RPC, safe HTTP, protocol mismatch, zero/two distinct provider Documents, duplicate locators/classifications/anchors for one provider, two endpoints and two consumer entries sharing one source/target Document pair, conflicting roles, stale/unsupported/unsafe evidence, both directions, ten randomized evidence-multiplicity/orders, deterministic decision/aggregate/Edge IDs and fingerprints, no graph/LLM influence.
2. **PostgreSQL resolution**: complete decision batch plus every provider-contribution row and deduplicated Edge atomicity; two endpoint decisions and two entry decisions persisting against one Edge; duplicate observation deduplication; exact duplicate batch replay; missing/extra/reordered decision/contribution/Edge replay conflict; concurrent same-tuple Edge creation/reuse; currentness/authorization drift; new-decision-owned Uncertainty resolution; unchanged legacy one-to-one evidence compatibility without backfill.
3. **Flow planning**: one distinct Flow Group per exact-match decision even when two decisions share an Edge, required three Documents/shared Edge, unique implementation, one-hop cycle/duplicate handling, pair-specific instance identity versus cross-scan stable key, stable consumer entry across changed Document Versions/anchor ranges, changed endpoint/entry producing a new key, eight-Document/sixteen-step/ten-group exact and +1 bounds, byte-equivalent repeated planning, honest incompleteness/omission.
4. **Retrieval**: every operation and inclusive/+1 bound; durable call reservation; repeated request/replay; duplicate descriptors; four additional Documents/four sections; cumulative UTF-8 bytes; unknown/stale/cross-System/mismatched revision; no excluded-content disclosure; retrieval cannot alter provider/group.
5. **Worker protocol**: exact strict schemas, two-function declaration set, all-retrieve batch preflight, forced submission/correction, no request eleven, streaming inactivity/absolute cap, raw/request/response byte bounds, content-dark diagnostics, fake OpenAI-compatible wire.
6. **Commit and replay**: handle-derived used set, all-and-only GENERATED_FROM, unused-return exclusion, stale/unauthorized/mismatched used Document, unsupported relationship/provider change, atomic rollback injection, concurrent completion, acknowledgement loss, same semantic flow in a later pair appending/superseding under one Logical Document, changed endpoint creating a separate untouched chain, stable-key creation race/current-pointer reload, exact replay, immutable Version Chain, and full graph reconstruction.
7. **Execution/API**: a public authenticated `WebApplicationFactory<Program>` RED proves the plural Pass 2 routes are initially absent; GREEN owns both the endpoint group and its explicit registration through `MapVulgataApiEndpoints()`. Composition coverage proves create/status/resume are reachable through that single central seam and preserve 401 for unauthenticated requests, 403 for policy failures, ownership-filtered 404, ProblemDetails for validation/state conflicts, replay, serial canonical claims, one retry, provider pause, restart lease recovery, mixed published/failed/incomplete/omitted terminal accounting, and zero-success warning completion.
8. **Migration**: clean/legacy apply, rollback/reapply, constraints/indexes/FKs, old exact/Uncertainty/attached Flow Group rows, no pending model changes.
9. **Production qualification**: disposable PostgreSQL and approved two Repository roots at exact revisions; Story 12.3 handoff -> exact RPC decision/shared-or-new Edge -> complete Flow Group -> configured live Worker -> one Business Logic Document -> every claim traceable through its decision, GENERATED_FROM and Source Anchors. Direct Pass 2 JDT-LS/CodeGraph remains disabled and results must be identical without it.

## Planned-Change Authorization Triggers

Independent plan review is mandatory. After `ACCEPT` and before task generation, the root must request one explicit authorization covering all of the following:

| Repository trigger | Proposed material change and consequence | Affected authority |
| --- | --- | --- |
| Scan strategy | AD-1 through AD-7/AD-12 add exact Cross-Repository Resolution, provider-evidence aggregation before distinct-Document cardinality, one durable decision per endpoint/entry with reusable document Edges, endpoint-centered planning with separate execution/cross-scan identities, group/job scheduling, omission accounting, and Pass 2 phase transitions. Only the unique distinct exact provider Document produces an exact decision/Edge binding; incomplete/over-bound groups do not dispatch. | `AGENTS.md` planned-change policy; August design §§11-12; recovery proposal §§4-6/12; PRD FR-4.5, FR-6.1, FR-6.2 |
| Worker Agent tools | AD-8 through AD-10 add strict `retrieve_code_logic` and `submit_business_logic`, bounded durable retrieval accounting, and a two-function provider loop. The provider sees only authorized bounded evidence and never chooses provider/group membership. | `AGENTS.md`; Story 12.4 AC-3; PRD FR-4.5; Constitution V/NFR-2.3/NFR-6.1 |
| Dated PRD replacement/architecture decision | AD-2/AD-3/AD-5 retain the approved dated narrowing: exact Custom RPC/safe HTTP and endpoint-centered groups replace general graph-first clustering and general bidirectional resolution for this release; long-term V1 remains backlog. | recovery proposal §4; August design §§11-12; PRD FR-4.5/FR-6.1/FR-6.2 |

Authorization permits only progression from an accepted plan to task generation. It does not waive task/definition review, implementation approval, TDD, convergence, quality review, E2E, security, migration, live-provider qualification, or final verification.

## Requirements Traceability

| Plan decision/contracts | Specification | Authority |
| --- | --- | --- |
| AD-1 through AD-4; exact-resolution contract | FR-001-FR-010; SC-001/002 | Story AC-1; August design §11; PRD FR-6.1/6.2 |
| AD-5 through AD-7; planning/runtime contracts | FR-011-FR-019; SC-003/004 | Story AC-2; August design §12; dated FR-4.5 overlay |
| AD-8 through AD-10; retrieval contract | FR-020-FR-028; SC-005/006/010 | Story AC-3; August design §12; Constitution V |
| AD-9 through AD-11; submission schema/commit contract | FR-029-FR-037; SC-007-009/012 | PRD FR-4.5; immutable Document/GENERATED_FROM authorities |
| AD-12/AD-13; terminal/API contract | FR-038-FR-043; SC-011 | Story AC-4; recovery proposal partial-completion rule |

## Inline Architecture Self-Review

| Lens | Evidence reviewed | Result |
| --- | --- | --- |
| Feasibility | Existing exact resolver/store, catalog evaluator, revision reader, Flow Group/trace, knowledge projections, Pass 2 packet/result/committer, Story 12.3 strict transport, and `ApiEndpointRegistrationExtensions` all provide concrete seams; each required delta has one owner. | PASS |
| Repository fit | No new graph type or generic repository; legacy profile/demo path is characterized; current composition deepens rather than replaces the accepted source. | PASS |
| Invariants | Exact protocol+endpoint key, provider count over distinct current provider Document Versions after complete evidence aggregation, many endpoint/entry decisions to one source/target/type Edge, pair-specific Flow Group versus cross-scan Logical Document identity, currentness/authorization/revision/hash, closed group membership, hard release bounds, immutable Documents, and derived provenance are explicit at projector and transaction boundaries. | PASS |
| Resolution/graph cardinality | Every consumer candidate owns one durable v2 decision; exact decisions may share a tuple-unique REFERENCES Edge but retain independent evidence/contributions and Flow Groups; unresolved decisions own Uncertainties; legacy one-to-one evidence remains isolated. | PASS |
| Provider multiplicity | Evidence observations are canonicalized/deduplicated by complete identity, aggregated by current provider owner, fully persisted as normalized contribution rows, and only then counted; one Document with two locators remains one provider while two Documents remain ambiguous. | PASS |
| Cross-scan Version Chain | Flow Group IDs include pair/current evidence; `business-flow-logical-key/1.0` excludes them, has an exact consumer semantic-entry preimage/evolution rule, reuses existing uniqueness/current-pointer concurrency, and makes later same-flow pairs append rather than allocate unrelated logical slots. | PASS |
| Failure behavior | Every ambiguous decision has no Edge binding even if another decision already created the same document tuple, every incomplete/over-bound group refuses dispatch, retrieval refuses without disclosure, changed replay is mutation-free conflict, and valid earlier groups survive later failure. | PASS |
| Transaction/replay | Resolution persists the full decision-to-shared-Edge/Uncertainty/contribution graph under canonical tuple locks; start persists both group identity domains; retrieval is durable; publication serializes by stable key/current pointer; all exact replays reconstruct complete relational state. | PASS |
| Test seams | Pure resolver/normalizer/planner/schema projector plus stores, retrieval service, strict executor, committer, API/host and PostgreSQL qualification are independently observable. | PASS |
| Security/data stewardship | Scope is server-owned; provider receives bounded authorized context; no secrets/source/provider payload persist in receipts/logs; cross-System and stale content are never disclosed; no open tools. | PASS |
| Migration/compatibility | One versioned migration adds v2 decision/contribution and Flow Group decision/stable-key fields while retaining DocumentEdge uniqueness, legacy one-to-one exact evidence, and Logical Document uniqueness/current-pointer concurrency without unsafe backfill; clean/legacy lifecycle evidence is explicit. | PASS |
| Constitution/ADR | All five principles pass, including Constitution III registration of the plural Pass 2 group through `MapVulgataApiEndpoints()` with the established ProblemDetails and 401/403/ownership-filtered-404 behavior; no ADR/context exists; the dated PRD narrowing is disclosed rather than silently overriding long-term intent. | PASS |
| Story boundary | No Chat, vector retrieval, UI, final release evidence, general graph clustering, mandatory direct analysis, unsupported protocols, or provider/failover configuration change. | PASS |
| Planned-change governance | Scan strategy, two Worker tools, and dated PRD/architecture decisions are named with exact consequences and later authorization scope. | PASS |

**Self-review conclusion**: **PASS**. The plan is feasible, internally consistent with the companion artifacts, leaves no unresolved design question or template marker, and must proceed next to independent plan-only review rather than task generation.
