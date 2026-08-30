# Implementation Plan: Story 12.5 Track C-MVP with Prepared Scan Initiation

**Branch**: `demo0831/story-12-5-trackC`
**Date**: 2026-08-30
**Specification**: [spec.md](spec.md), current corrected candidate SHA-256 `469f72c4e7fc62874e082a7f425351d9f46cf422ae8641d4bee7b64915dd67c0`; the orchestrator rebinds the accepted hash after independent review
**Definition source commit**: Prepared Scan authority checkpoint `34c6cb8e3d2ea0858cf8a2711aa17d46526df1b9`
**Accepted baseline**: Track A and Track B checkpoint `f292caec427f9844be3c0f65b4cb00a9292080a5`
**Immutable dependency**: Story 12.4 commit `4798b97d9f23aef587829175df7ab7ebc3daf995`
**Correction authorities**: [Track C-MVP proposal](../../docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-29-story-12-5-track-c-mvp.md) and [Prepared Scan initiation proposal](../../docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-30-story-12-5-prepared-scan-initiation.md)

## Summary

Track C-MVP proves the August 30 product path without building a second release-proof product. The bounded correction adds the one missing production entrance from an authenticated authorized System and exactly two ordinary Repositories into the existing Prepared Scan path. A new `PreparedScanInitiationCoordinator` validates exact Repository/ref/Profiled Revision and current runtime/prompt/schema identities, then calls one Infrastructure port whose short PostgreSQL transaction creates or replays two `ScanRun` records, two `ScanExecution.CreateNewPath` records and one canonical `TwoRepositoryScanCoordination.Create` record. The transaction closes before the existing `PreparedScanCoordinator` and `PreparedScanPairBuilder` consume that exact tuple to perform source acquisition, CodeGraph 1.5, JDT-LS, safe XML, selection and atomic Prepared Scan Manifest publication.

After the corrected entrance is accepted, one pinned candidate runs Vulgata on the documented Windows host against one new lowercase `vulgata_story125_*` PostgreSQL database, scans exactly two real Java/XML Repositories at exact Profiled Revisions through the accepted CodeGraph, JDT LS, safe XML, Pass 1, exact Cross-Repository Resolution and Pass 2 path, exercises the accepted mandatory-streaming Prepared Chat path, restarts the Vulgata application process once against the same database, and records the result in one human-readable Markdown report.

The report contains a lightweight preflight, sanitized commands and exit results, straightforward database query results, a manually completed twelve-question table, five manually captured/redacted real screenshots, restart observations, failures and limitations. Track C-MVP may quote existing Track B content-dark streaming observations, but it neither recomputes transport counters nor adds attestation, signatures, a rigid release envelope, a duplicate orchestration harness, outage injection, screenshot automation or a release-summary product feature.

Accepted Track A/B production and tests remain immutable. Production work is limited to this initiation seam and its composition into the existing Prepared Scan endpoint. It is a material Scan scheduling/grouping/retry entrance change, so independent plan-only review must be followed by the explicit AGENTS.md planned-change authorization before task generation. No current or expired implementation approval covers this delta.

## Technical Context

**Language/runtime**: .NET 10, C#, Blazor Interactive Server, PowerShell operator commands, PostgreSQL 17.
**Existing production seams**: `PreparedScanEndpoints`, `PreparedScanCoordinator`, `PreparedScanPairBuilder`, `PreparedScanStore`, `IScanRecoveryDispatchIdentitySource`, `AnalysisRuntimeIdentity`, `ScanRun.Start`, `ScanExecution.CreateNewPath`, `TwoRepositoryScanCoordination.Create`, `Pass1ExecutionEndpoints`, `Pass2ExecutionEndpoints`, `ManualResumeEndpoints`, `PreparedPass1ExecutionHostedService`, `PreparedPass2ExecutionHostedService`, `ProductionScanStatusReader`, `ChatEvidenceRetriever`, `PreparedChatCoordinator`, `OpenAiCompatibleChatProviderStreamingTransport`, and `ChatPage`.
**New production seams**: one Web application coordinator `PreparedScanInitiationCoordinator` and fresh authorization rechecker; one internal Web `IPreparedScanRuntimeIdentityResolver` with sole configured default; one Core persistence port/result `IPreparedScanInitiationStore` plus Core canonical identity builder; one Infrastructure `PreparedScanInitiationStore`; endpoint/DI/exact-handoff composition only.
**Existing Track C support seams**: `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarness.cs`, `Story125Tier2PostgreSqlTests.cs`, the fixed `story125-question-corpus.json`, and `Story125EvaluationContractTests.cs`.
**Persistent authority**: existing `ScanRuns`, `ScanExecutions`, and `TwoRepositoryScanCoordinations` plus the existing prepared tables in the `vulgata` schema. No table, column, index, migration, `VulgataDbContext`, configuration, or model-snapshot change.
**External prerequisites**: PostgreSQL 17; two accessible real Repository roots at exact Git commits; `@colbymchenry/codegraph@1.5.0` and its accepted hashes; Java 21; `jdt-language-server-1.61.0-202608141332`; configured OpenAI-compatible streaming provider; authenticated Vulgata user authorized for the selected System and both Repositories; a local browser/screenshot capability.
**Performance/quality bounds**: 50-150 automatically selected File Code Units; every high-value seed successful; ordinary failures within the accepted Scan budget; twelve representative Chat questions when the provider is available; useful answer rate >=80%; accepted material technical claim citation coverage 100%; unauthorized disclosure 0; each accepted complete rendered answer <30 seconds.
**Evidence format**: one Markdown report plus relative screenshot files. No release JSON envelope is required for Track C-MVP.

## Constitution Check

| Principle | Pre-design result | Plan consequence |
| --- | --- | --- |
| I. Layered Project Boundaries | PASS | Web owns request orchestration, Core owns the closed initiation port/contracts and existing factories, Infrastructure alone owns the EF transaction/advisory lock, and Program owns composition. No `DbSet` or `IQueryable` crosses Infrastructure. |
| II. Domain Purity & Ubiquitous Language | PASS | Queries observe existing System, Repository, Profiled Revision, File Code Unit, Document, Source Anchor, REFERENCES Edge, GENERATED_FROM Edge, Uncertainty, Cross-Repository Resolution and Flow Group authorities. No new domain synonym or mutable Document path is introduced. |
| III. Validation & API Contract Discipline | PASS | The existing `POST /api/systems/{systemId}/prepared-scans` request and FluentValidation contract remain closed. Existing Chinese `ProblemDetails` outcomes gain only a bounded initiation conflict/unavailable classification; no new public endpoint or request field is required. |
| IV. Test-First Integration Harness | PASS | Behavioral RED begins at the authenticated production endpoint with no seeded lifecycle/content records. PostgreSQL tests own advisory-lock concurrency and rollback; existing Prepared Scan/API suites guard compatibility; the real run remains required E2E evidence. |
| V. Security & Data Stewardship | PASS | Authorization is checked initially, freshly reloaded after identity resolution immediately before the store, and rechecked before prepared publication. Administrator/SystemOwner/assignment/repository revocation is missing-equivalent with zero writes; secrets stay out of identities/evidence, and no source or LLM work occurs inside initiation. |

No Constitution exception or Complexity Tracking entry is required.

## Accepted Baseline and Delta

### Preserved unchanged

- Track A production Scan-status UI, corpus and evaluation preparation.
- Track B deterministic lexical selection, current-v2 relationship/Flow Group expansion, Evidence Packet 2.2, bounded conversation re-fetch, claim/citation validation, mandatory raw streaming, strict UTF-8/SSE/JSON/cap/deadline handling, accepted-only publication, cancellation/reset and Chinese Business/IT behavior.
- Story 12.4 exact `CrossRepositoryResolutionDecision`, current exact `DocumentEdge`, complete `GENERATED_FROM` and published Flow Group authority.
- Existing Scan parsing, filtering, selection, scheduling, retry, dispatch and persistence.
- Existing Prepared Scan request/response shape, CodeGraph 1.5/JDT-LS/XML analysis behavior, final authorization recheck, and atomic Prepared Scan Manifest/handoff publication.

### Preserved Track B compatibility vocabulary

The accepted streaming compatibility literals remain: one received logical Provider Chunk is bounded at **16,384** bytes; the assembled received provider response is bounded at **65,536** bytes; the independent raw response-body safety cap is **1,048,576** bytes; and the independent SSE line/event safety cap is **131,072** bytes. Historical/current compatibility artifacts also retain the closed observation field names `transportOutcome`, `rejectionCategory`, and `validationOutcome`.

Track C-MVP only preserves or quotes these accepted Track B values and fields. It does not recompute any raw-read/body, SSE, received Provider Chunk, assembled-response, decoded-delta or final-answer domain and does not turn the quoted observations into a new release authority.

### Removed from the August 30 boundary

- ECDSA/key/attestation sinks and sealed raw-adapter admission.
- Track C transport-counter recomputation and forgery qualification.
- The fixed ten-check release envelope and schema-2.2 semantic graph.
- Supersession/closure graphs and exhaustive hashing/cross-linking.
- A separate Tier 2 orchestration run, live outage injection and automated screenshot binding.
- A dedicated release-summary product feature.

These remain explicit post-competition backlog; the old schema file is retained only as a marked historical compatibility artifact so accepted Track A test history is not silently erased.

### New bounded Prepared Scan initiation work

1. Compose the existing authenticated Prepared Scan endpoint through `PreparedScanInitiationCoordinator`.
2. Validate one visible System, exactly two visible distinct Repositories, safe full refs, exact lowercase Profiled Revisions, current Pass 1 prompt/Document schema identities, and one canonical full Analysis Runtime Identity per Repository before writes.
3. In one short Infrastructure transaction, lock the System initiation domain, create or replay the complete two-Repository tuple from existing factories/tables, and close the transaction before every external analysis operation.
4. Reject incomplete, legacy, ambiguous or mismatched durable state; exact committed retries reuse the same tuple after later preparation failure.
5. Add the no-preseed vertical production-composition and PostgreSQL atomicity/concurrency tests specified below.

### Remaining Track C-MVP release work

1. Correct the release contract so the MVP authority is a Markdown report, not the historical JSON envelope; preserve accepted Track A/B tests unchanged and add only new Track C-MVP contract/guard coverage.
2. Reuse or minimally narrow the existing database-name guard and prove it rejects protected/malformed names before a connection factory/product host is invoked.
3. Record a lightweight preflight and run command log without secrets.
4. Execute one real product run and straightforward read-only queries.
5. Before provider execution, pin corpus cases `q01` through `q12`, then execute each as an independent real-run row with fresh `ChatConversationState` through the real configured streaming Chat path when available.
6. Restart only the Vulgata application process against the same database and re-observe persisted wiki/Chat access and duplicate counts.
7. Manually capture/redact five real screenshots and complete the report.

## Architecture Decisions

### AD-PSI-1 — Deepen the existing Prepared Scan endpoint with one initiation coordinator

**Decision**: keep `POST /api/systems/{systemId}/prepared-scans` and its request/response contract. Replace its direct dependency on `PreparedScanCoordinator` with `PreparedScanInitiationCoordinator`. The new coordinator performs an initial visibility/ownership check, resolves server identities, performs a second fresh live authorization/ownership recheck immediately before the first store call, and only then creates/replays or recovers the prepared handoff. The established third check immediately before Prepared Scan publication remains unchanged.

**Exact call sequence**:

1. Endpoint authentication, `ManagementAccess`, route/request FluentValidation.
2. Initiation coordinator performs the initial check from the authenticated `IUserContext`: one visible System, two distinct Repositories belonging to it, safe refs and exact expected Profiled Revisions.
3. Outside any transaction, reuse `IScanRecoveryDispatchIdentitySource` for the current Pass 1 prompt and `code-logic/file-v2` Document schema identities. Inject narrow internal `IPreparedScanRuntimeIdentityResolver`; its sole default production implementation, `ConfiguredPreparedScanRuntimeIdentityResolver`, reuses the configured readiness integrity logic and returns one checked canonical `AnalysisRuntimeIdentity` (`analysis-runtime/1` JSON/hash) per Repository/Profiled Revision or Not Ready. The interface and implementation are internal Web composition seams, never public DTOs, caller/Agent tools, CGC adapters or general analysis ports.
4. After all prompt/schema/runtime resolution, `PreparedScanInitiationAuthorizationRechecker` creates a fresh DI scope, reloads the user and roles with `UserManager<ApplicationUser>`, rechecks both Repositories still belong to the System, and accepts only either a current Administrator role or both a current SystemOwner role and current `SystemOwnerAssignment` for that System. It returns the newly observed effective mode used by the request fingerprint. Revocation, deletion, membership drift or repository movement maps to missing-equivalent `NotFound`; the coordinator does not call the store.
5. Immediately after that live recheck, the initiation coordinator submits one normalized command and its freshly observed effective authorization scope to `IPreparedScanInitiationStore`. No cached `IUserContext.IsAdministrator` or earlier `SystemOwnerFilter` decides this write.
6. The Infrastructure store executes AD-PSI-2/3 and returns only `Created`, `Replay`, `NotFound`, `Conflict`, `Rejected`, `Cancelled`, or `Unavailable` plus an immutable successful tuple projection containing exact Run/Execution/coordination identities, current phase, prepared-artifact presence, bound identities and the common initiation time.
7. The transaction is closed. The initiation coordinator follows AD-PSI-3's phase table: for prepared-artifact presence or an advanced phase it calls `PreparedScanCoordinator.RecoverPreparedAsync(authorizationScope, tuple, cancellationToken)`, which reuses existing `IPreparedScanHandoffSource` and response mapping without analysis. It calls `PrepareInitiatedAsync(systemId, request, user, tuple, cancellationToken)` only for a created tuple or exact `Preparing` tuple with no prepared artifact. This replaces the tuple-less production call.
8. `PrepareInitiatedAsync` calls `IPreparedScanPairBuilder.BuildAsync(tuple, repositories, revisionBindings, cancellationToken)`. The builder loads and validates the exact coordination, both Scan Runs and both executions by tuple ID rather than discovering candidates by Repository IDs, uses each execution's Analysis Runtime Identity hash in its `AnalysisScope`, and uses the common initiation time as the prepared context capture time. The existing coordinator performs the third live authorization check immediately before `PreparedScanStore.CommitPairAsync` and maps the unchanged handoff/response.

**Rationale**: this is the smallest production entrance that uses the accepted endpoint and preparation code. It does not repair `/scan-runs`, create a parallel scanner, or move persistence into Web.

**Alternatives rejected**:

- restore/register `RepositoryProfilingCoordinator` or `CodeGraphContextProvider` — violates Story 12.1 AD-10 and the current runtime;
- make `PreparedScanPairBuilder` create lifecycle records — mixes external analysis with persistence ownership and cannot guarantee a short transaction;
- create a second endpoint or release-only harness — duplicates the product entrance and would not prove normal production composition;
- extend the public request with runtime/prompt/schema values — lets callers choose server authority.

**Consequence**: `PreparedScanInitiationCoordinator`, its fresh authorization rechecker, `PreparedScanCoordinator`, `PreparedScanPairBuilder`, `PreparedScanEndpoints`, `Program.cs`, and their tests are shared integration files with one coordinated owner. Pair-builder analysis/selection logic does not change; its composition signature requires the initiated tuple and it stops discovering an assumed pair by Repository IDs. Production composition exposes no tuple-less fallback.

### AD-PSI-2 — One schema-free atomic tuple transaction

**Decision**: add one Core port, `IPreparedScanInitiationStore`, implemented by Infrastructure with the existing scoped `VulgataDbContext`. Under one short PostgreSQL `ReadCommitted` transaction, serialized by AD-PSI-3's transaction advisory lock, it creates or replays exactly:

- two `ScanRun.Start` aggregates using the existing established **1,800-second** compatibility value, followed by immutable `BindProfiledRevision`; this value only satisfies the existing durable `ScanRun.ProviderProcessTimeoutSeconds` field and neither resolves nor configures CGC;
- two `ScanExecution.CreateNewPath` aggregates in `Prepared`, bound to System, Repository, Scan Run, Profiled Revision, current prompt identity, `code-logic/file-v2`, and the complete canonical `analysis-runtime/1` JSON/hash;
- one `TwoRepositoryScanCoordination.Create` aggregate in `Preparing`, whose built-in ordering is the canonical Repository order.

The store calls `SaveChangesAsync` once and commits once. A validation, cancellation, lock, EF, or database failure rolls back the entire tuple and clears tracked additions. It does not call the existing three single-aggregate stores sequentially because that would expose partial state.

`ScanExecution.CreateNewPath` deliberately retains `PreScanResultId == Guid.Empty`. In the accepted model, the non-null prompt/schema/runtime fields identify the new path; there is no foreign key from that compatibility column. Initiation creates no `PreScanResult` and never attaches a historical v1 result. The existing Prepared Scan publication later owns creation of the current `ScanContextSnapshot`, prepared `PreScanResult`, File Code Units, and Prepared Scan Manifests.

Both Scan Runs and both new-path executions use one UTC initiation instant. `PreparedScanPairBuilder` binds the later `ScanContextSnapshot.CapturedAt` to that same instant, rather than the later end of external analysis, preserving the existing temporal rule that the context snapshot does not post-date execution start. The builder reads the tuple's exact executions and uses their per-Repository runtime hashes for CodeGraph/JDT-LS proofs and Manifest packages; this preserves the later Pass 1 requirement that execution, Manifest and proof runtime identities match. It does not change file selection, parsing, dispatch or analysis behavior.

**Transaction boundary**: authorization/identity resolution precedes the transaction. The transaction contains only bounded relational reads, advisory locking, factory construction, tuple validation, insert/replay, one save, and commit. It ends before Git acquisition, snapshot enumeration, CodeGraph, JDT-LS, XML parsing, selection, Worker, Pass 1, Cross-Repository Resolution, Pass 2, Document/Edge/Uncertainty, Chat, LLM, or evidence operations.

**Alternatives rejected**:

- call `IScanRunRepository`, `IDurableScanStore`, and `ITwoRepositoryScanCoordinationStore` in sequence — not all-or-nothing;
- add a tuple table, idempotency column, unique index, EF model change or migration — prohibited and unnecessary;
- create a legacy `PreScanResult` to satisfy the column — would make old v1 profiling appear authoritative;
- hold the transaction through preparation — risks long locks and external side effects inside rollback scope.

**Consequence**: the only new persistence class is `PreparedScanInitiationStore.cs`; existing DbContext/model/configuration/migration files are read-only.

### AD-PSI-3 — PostgreSQL advisory serialization plus exact tuple replay

**Decision**: the store uses a transaction-scoped PostgreSQL advisory lock before inspecting or creating initiation state. The signed 64-bit lock key is the first eight bytes, big-endian, of SHA-256 over the versioned UTF-8 preimage `vulgata/story-12.5/prepared-scan-initiation-system-lock/1\n{system-id-lowercase-D}`. It executes `SET LOCAL lock_timeout = '5s'` before `pg_advisory_xact_lock`; PostgreSQL lock-timeout (`55P03`) or caller cancellation returns `Unavailable` with zero writes. System scope, rather than revision scope, serializes both identical and competing pair requests so a changed pair cannot race into a second tuple.

Within the lock, Repository IDs are ordered by `Guid.CompareTo`, matching `TwoRepositoryScanCoordination.Create`. Core-owned `PreparedScanInitiationIdentityBuilder` frames each execution request with the existing `CanonicalEnvelope` grammar under exact domain `vulgata/story-12.5/prepared-scan-initiation-execution/1.0`; tags **1..10 respectively** are user UUID, effective mode enum (`Administrator` or `SystemOwner`), System UUID, Repository UUID, exact case-sensitive safe full ref, lowercase Profiled Revision, prompt identity, Document schema identity, canonical runtime JSON, and runtime-hash bytes. `PreparedScanIdentityBuilders.CreateDeterministicIdentity` receives the resulting lowercase SHA-256 and exact case-sensitive kind literal **`PreparedScanInitiationExecution`**. `TwoRepositoryScanCoordination.Create` then derives the coordination ID from the canonical execution pair.

The store queries all coordinations for the System and behaves as follows:

- **none**: create one complete tuple; ignore historical uncoordinated Scan Runs and never reuse them;
- **exactly one exact new-path tuple**: validate the coordination, both Scan Runs, both executions, refs/revisions represented by the request, prompt/schema/runtime identities, deterministic execution IDs, empty legacy `PreScanResultId`, canonical pair and effective authorization scope fingerprint; return `Replay` without writes together with current coordination phase and whether any prepared artifact exists;
- **any mismatch, old-path execution, partial row set, extra coordination, corrupt prepared state or ambiguous candidate**: return `Conflict` or missing-equivalent rejection without attaching, repairing, deleting, or disclosing tuple details. A valid advanced coordination phase is not itself a conflict.

Because refs are not stored in the initiation tuple, exact replay binds them through the deterministic request/execution identity and through the later Prepared Scan Manifest `RequestedRef`; a ref change derives a different expected execution identity and conflicts with the System's existing tuple. The same rule applies to a changed authorization scope. A rolled-back attempt exposed no tuple and may safely create one on retry.

**Phase-aware replay table**:

| Exact tuple observation | Required outcome |
| --- | --- |
| newly `Created`, `Preparing`, no prepared artifact | invoke the existing external preparation once for this request |
| `Replay`, `Preparing`, no prepared artifact | induced-failure/crash retry: re-enter preparation with the same tuple; no initiation row changes |
| `Replay`, `Preparing`, any prepared artifact | call `RecoverPreparedAsync`; return 200 Replay when the existing handoff is complete/exact, otherwise conflict as partial/corrupt; never rerun external preparation |
| `Replay`, phase advanced beyond `Preparing`, complete exact handoff | call `RecoverPreparedAsync` and return the existing 200 Replay response; never rerun external preparation or create state |
| `Replay`, phase advanced beyond `Preparing`, no complete exact handoff | conflict as corrupt/partial/incompatible; never rewind phase or rerun preparation |
| any identity mismatch, legacy execution, partial tuple, extra coordination or ambiguous state | conflict/missing-equivalent without disclosure or mutation |

The store reports prepared-artifact **presence**, not a handoff DTO. Presence means any `PreparedScanManifest`, prepared-v2 `PreScanResult`, `ScanContextSnapshot`, `PreparedScanManifestEntry`, `PreparedScanAudit`, or File Code Unit bound through the exact coordination/Scan Run/Manifest IDs. Outside the transaction, `PreparedScanCoordinator.RecoverPreparedAsync` uses existing `IPreparedScanHandoffSource` as the sole complete-handoff validator and the existing response mapper. This prevents both the initiation store and new coordinator from duplicating prepared-package semantics.

**Golden identity vectors**: Core tests freeze the domain, tag order and kind literal above with one complete fixed command. They assert both expected execution GUID constants, the same two GUIDs when Repository request order is reversed, and the expected `TwoRepositoryScanCoordination.Create` GUID constant. Separate one-field mutation vectors cover ref, revision, runtime, prompt/schema and effective authorization mode. No test may compute its expected GUID by calling the production builder under test.

**Alternatives rejected**:

- `Serializable` isolation alone — existing schema has no unique System/pair constraint, so write-skew/duplicate pair prevention would depend on retry behavior rather than an explicit lock domain;
- pair+revision lock key — competing pair or revision requests could acquire different locks and both create;
- process-local semaphore — does not protect multiple host processes;
- session advisory lock — risks a leaked lock across pooled connections; transaction-scoped lock releases on commit/rollback.

**Consequence**: PostgreSQL is the concurrency authority. SQLite may retain focused coordinator/endpoint characterization, but concurrency/atomicity acceptance requires the existing disposable PostgreSQL test infrastructure.

### AD-PSI-4 — Closed public outcomes and no later-stage side effects

**Decision**: preserve the current endpoint body and response. Map initiation outcomes before invoking preparation:

| Initiation outcome | HTTP/result |
| --- | --- |
| unauthenticated | existing 401 |
| policy denied | existing 403 `prepared-scan-forbidden` |
| invisible System/Repository or unauthorized scope | 404 `prepared-scan-not-found`, with zero writes and no identity detail |
| malformed request | existing 400 `invalid-prepared-scan-request` |
| mismatched/ambiguous/legacy tuple | 409 `prepared-scan-initiation-conflict` |
| identity authority not Ready or tuple store unavailable/lock timeout | 503 `prepared-scan-initiation-unavailable` |
| newly created tuple followed by first prepared commit | existing 201 Created and unchanged `PreparedScanResponse` |
| exact pre-publication initiation replay followed by first prepared commit | existing 201 Created; public status follows package commit, not initiation-row age |
| exact complete prepared/advanced replay | recover existing handoff, 200 OK, no external analysis |
| partial/corrupt prepared state or advanced phase without exact handoff | 409 `prepared-scan-initiation-conflict`, no rerun/rewind |
| downstream preparation rejection/conflict | existing 409/422/503 mapping |

The initiation response does not expose Scan Execution IDs, authorization fingerprints, advisory keys, runtime JSON, or existing tuple details. Only the existing prepared handoff is public. No Worker/Pass/Document/Edge/Uncertainty/Chat/LLM service is injected into or called by initiation.

**Consequence**: endpoint tests can assert the status and database effects without creating a new DTO or weakening missing-equivalent authorization behavior.

### AD-CMVP-1 — Evidence execution, not a product subsystem

**Decision**: after the bounded initiation delta above, Track C-MVP adds no second release service, endpoint, persistence entity or UI feature. Release execution uses the corrected normal Prepared Scan endpoint and the existing product path; only the already planned test/evidence support remains outside production.

**Rationale**: after AD-PSI-1 through AD-PSI-4 supply the missing entrance, the existing application exposes the complete Scan and Prepared Chat path. Duplicating that path in a release harness would test a second implementation rather than the candidate shown to judges.

**Alternatives rejected**: retaining the attestation/release-envelope subsystem, adding a release-summary page/API, and copying Story 12.4 qualification orchestration. Each is outside the approved MVP boundary.

**Consequence**: real-run failures are classified as prerequisite, operator, evidence, or product failures. Any product change beyond the accepted initiation plan requires a new correct-course decision; missing external prerequisites produce honest blocked evidence.

### AD-CMVP-2 — Database ownership is a zero-product-access gate

**Decision**: before constructing a product database connection, starting Vulgata, applying startup migrations, or issuing any query, preflight requires a new unique lowercase name matching `^vulgata_story125_[a-z0-9_]{8,48}$`, explicitly rejects `vulgata`, PostgreSQL system databases and every `vulgata_story124_*` name, and records the intended owner/purpose. Creation uses the administrator connection only after this guard passes.

**Reusable seam**: `Story125ReleaseHarness.CreateRunIdentity` and `IsOwnedDatabaseName`, with `Story125Tier2PostgreSqlTests` as the public behavioral boundary. A minimal extension may accept a connection-factory spy to prove zero calls for rejected names; it must not recreate a database provisioner or product host.

**Alternatives rejected**: trusting a manually typed connection string or cloning Story 12.4 qualification data. Both violate the preconnection/clean-database boundary.

**Consequence**: inability to create or own the database is a hard blocked disposition, not permission to reuse another database.

### AD-CMVP-3 — One combined host integration and release run

**Decision**: start the existing Vulgata host with the owned database, invoke the existing authenticated preparation/Pass 1/Pass 2/resume surfaces and UI, and let the existing hosted services and provider adapters perform the work. There is no separately automated Tier 2 run.

**Run order**:

1. Pin candidate and Repository Profiled Revisions; capture tool/provider identities.
2. Prove database ownership and create the clean database.
3. Start Vulgata; production-bootstrap/authenticate the Administrator, System, provider assignment and exactly two ordinary Repositories without lifecycle/content seeding.
4. Submit the corrected existing Prepared Scan endpoint with exact refs/revisions, replay it once, and verify the same complete tuple plus real CodeGraph, JDT LS and safe XML evidence in two persisted Prepared Scan Manifests.
5. Run the accepted Pass 1, exact Cross-Repository Resolution and Pass 2 paths to terminal states.
6. Query minimum output facts.
7. Before the first provider call, record fixed corpus IDs `q01` through `q12`; execute each row with fresh `ChatConversationState` through the existing Business/IT page and manually score outcomes. `followUpTo` is topical lineage only and never permission to carry state across rows.
8. Stop only the Vulgata process, restart it with the same database, re-authenticate, open persisted wiki/Chat, and rerun duplicate-count queries.
9. Capture/redact screenshots and finish the report.

**Consequence**: a prerequisite or product failure stops dependent observations but preserves earlier truthful command/query evidence. The run is not rerun unchanged after an external failure until the prerequisite is diagnosed.

### AD-CMVP-4 — Lightweight, human-auditable report

**Decision**: the release authority is one candidate-bound Markdown document using [contracts/release-evidence-contract.md](contracts/release-evidence-contract.md). It contains bounded tables, not a machine-enforced ten-check envelope.

**Minimum sections**: disposition and candidate; exact Repository Profiled Revisions; tool/provider/non-secret configuration/database identities; sanitized commands/exits; required query results; twelve manual Chat rows; restart observation; five screenshot paths/redaction results; failures and limitations.

**Consequence**: no signature, per-artifact hash graph, supersession graph or automated screenshot validation is required. Candidate/revision identity remains explicit in the report itself.

### AD-CMVP-5 — Straightforward queries observe existing authority

**Decision**: use read-only SQL/EF queries over existing `vulgata` tables. Query output includes exact identifiers/revisions needed to review the claim; it does not reconstruct relationship truth or add a new semantic validator.

**Required observations**:

- two approved `PreparedScanManifests` and 50-150 selected `PreparedScanManifestEntries` total, with exact Profiled Revisions and real tool bindings;
- terminal Pass 1/Pass 2 execution/job counts, explicit high-value-seed selected/succeeded/failed counts, proof that failed is zero and selected equals succeeded, and the ordinary failure budget;
- current source-anchored Code Logic and Business Logic `DocumentVersions` plus `SourceAnchors`;
- at least one accepted current `CrossRepositoryResolutionDecision` bound to one cross-Repository REFERENCES `DocumentEdges` row;
- at least one honest `Uncertainties` row;
- at least three current Business Logic Documents and one published `FlowGroups` row bound to the accepted decision/edge authority;
- pre/post-restart counts proving no restart-caused duplicate publication.

**Consequence**: a stale query may be corrected to the model; a missing required persisted fact is a product/result failure and cannot be manufactured.

### AD-CMVP-6 — Restart proves persistence, not transient conversation survival

**Decision**: stop and restart the Vulgata application process only; keep the same PostgreSQL service/database. After restart, re-authenticate and prove the generated wiki and a new grounded Chat request are available. Transient `ChatConversationState` is intentionally not expected to survive.

**Consequence**: compare pre/post persisted Document, Edge and Flow Group counts/identities. Do not claim saved conversation history.

### AD-CMVP-7 — Streaming is verified by existing behavior plus real observation

**Decision**: Tier 1 continues to verify the accepted strict raw streaming boundary. The real run records that the configured provider path used `OpenAiCompatibleChatProviderStreamingTransport`, the answer completed or failed under the existing closed outcome, and the complete rendered result duration. Track C quotes existing Track B observation fields where available; it does not recompute raw/SSE/provider-chunk/decoded/final counters.

**Consequence**: a non-streaming result, operational deterministic rescue, provider fragment, or unvalidated result cannot count as an accepted release answer. Provider unavailability yields blocked real-LLM evidence rather than fake Backup B evidence.

### AD-CMVP-8 — Manual screenshots are controlled evidence

**Decision**: a human captures one redacted real screenshot for each category: Scan, grounded Business Chat, grounded IT Chat with Source Anchors, exact Cross-Repository result, and persisted wiki/Chat access after restart. A checklist verifies no secret, connection string, absolute host path, fake Backup B state or unrelated candidate output is visible.

**Consequence**: no image parser, candidate-binding automation or product screenshot feature is built. A missing/unreviewed category prevents a passed report.

### AD-CMVP-9 — Corpus lineage is not Conversation State

**Decision**: the real run pins `q01` through `q12` and executes every case as an independent row with fresh `ChatConversationState`. The `followUpTo` field records topical lineage for human evaluation only. In particular, `q01` is Business, `q02` is IT and `q03` is Business; each mode transition invokes the accepted mode/scope reset boundary, so no prior question, evidence or citation context is retained between those rows.

**Authorization oracle**: `q12` runs under the same authenticated principal and the same exactly-two-authorized-Repositories scope as the other rows. It can honestly prove only that a prompt asking about a Repository outside that selected scope does not cause the answer to confirm the outside Repository's existence or disclose its Business Rules, Documents, relationships, citations or Source Anchors. Its oracle is `prompt-boundary-only`, never `denied-access-exercised` or a proof of revocation, a denied principal, a known protected Repository or true missing-equivalent behavior.

**Tier 1 authority**: accepted Track B tests remain the authority for same-mode multi-turn re-fetch/revalidation and for actual authorization/currentness denial behavior. Track C-MVP does not add a third Repository, denied principal or protected-scope fixture because that would broaden the accepted exactly-two-authorized-Repositories public seam.

**Alternatives rejected**: carrying Conversation State from `q01` to `q03` across mode changes contradicts the accepted reset contract; inventing a denied-access setup or treating a prompt assertion as access-denial proof would manufacture an unaccepted security oracle.

**Consequence**: the report records `freshConversationState`, topical lineage and the applicable authorization oracle for every row, and cites the retained Tier 1 evidence for security/currentness claims the real-run corpus does not exercise.

### Alternatives and consequence summary

| Decision | Rejected alternative | Consequence of the selected option |
| --- | --- | --- |
| AD-PSI-1 | legacy `/scan-runs`, a second endpoint, or builder-owned initiation | One authenticated production entrance composes the existing preparation pipeline. |
| AD-PSI-2 | sequential existing stores, legacy Pre-Scan Result, or a migration | One short all-or-nothing tuple write uses existing factories/tables and ends before analysis. |
| AD-PSI-3 | isolation only, pair/revision lock, or process semaphore | A System-scoped transaction advisory lock serializes exact and competing requests; deterministic identities prove replay. |
| AD-PSI-4 | expose initiation internals or add a response DTO | Existing API response remains stable; mismatch/denial/unavailability stay bounded and fail closed. |
| AD-CMVP-1 | Track C product service or copied qualification orchestration | Real product path is authoritative; a required source correction invalidates approval and returns to plan/rebinding before TDD. |
| AD-CMVP-2 | operator-trusted or post-connection database validation | Unsafe targets fail before product access; blocked ownership cannot be bypassed. |
| AD-CMVP-3 | separate Tier 2 and Tier 3 orchestration | One candidate/database/run reduces ceremony; every dependent observation shares the same real state. |
| AD-CMVP-4 | reduced but still rigid JSON release envelope or release-summary page | Markdown remains human-auditable; signatures/closure/hash graphs are not acceptance work. |
| AD-CMVP-5 | release query API or semantic recomputation harness | Existing persisted rows remain authority; missing facts fail honestly. |
| AD-CMVP-6 | database/container recreation or conversation-history persistence | Same-database process restart proves persisted wiki state only. |
| AD-CMVP-7 | Track C byte-counter recomputation or SDK aggregate acceptance | Track B tests retain transport authority; real run records only real composition/outcome/render time. |
| AD-CMVP-8 | automated image validation/candidate binding | Manual capture/redaction remains required; missing categories block a passed report. |
| AD-CMVP-9 | cross-mode retained Conversation State or invented denied-access fixture | Every corpus row is independent; topical lineage and the limited `q12` oracle are reported honestly, while Tier 1 owns actual reset/authorization/currentness proof. |

## External-Prerequisite Failure Model

| Failure | Classification | Required behavior |
| --- | --- | --- |
| Missing PostgreSQL administrator access or owned database creation failure | blocked | Record sanitized command/endpoint and result; do not select another database. |
| Docker engine unavailable for PostgreSQL | blocked-environment | Record engine/pipe failure once; do not repeatedly rerun unchanged commands. |
| Wrong Java major, CodeGraph/JDT LS version/hash, missing executable or Repository revision | blocked | Stop before the Scan path; record expected and observed non-secret identity. |
| Provider endpoint/model absent, credential unavailable or network route blocked | blocked | Do not expose the credential; do not substitute fake provider output; retain completed Scan evidence. |
| Provider returns a closed product rejection | product/provider result | Record the category. If correction requires production source, invalidate the no-production approval and return to plan/review/rebinding before TDD. |
| Required database output absent after terminal Scan | product/result failure | Preserve query result; do not edit database or manufacture rows. |
| Initiation advisory-lock timeout or cancellation before commit | retryable unavailable | Roll back/clear tracking, expose no tuple, and allow an exact authorized retry. |
| Existing partial, old-path, ambiguous or mismatched initiation rows | product/integrity conflict | Return bounded 409/missing-equivalent behavior; do not attach, repair, delete or continue to analysis. |
| Initiation committed; Git/CodeGraph/JDT-LS/XML preparation later fails | retryable downstream failure | Preserve the tuple and exact failure; an identical authorized retry replays it before re-entering preparation. |
| Evidence template, operator command or read-only query is wrong | evidence/operator/query defect | Correct only the owned evidence/operator/query artifact, rerun the affected observation, and preserve the original failure record. |
| Browser/screenshot capability unavailable | blocked-evidence | Preserve technical run results but do not claim complete release proof. |

## Project Structure and Ownership

```text
specs/014-grounded-llm-wiki-chat-release-evidence/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── contracts/
    ├── prepared-scan-initiation.md
    ├── release-evidence-contract.md
    ├── story125-release-evidence.schema.json # deferred historical contract
    ├── story125-evaluation-case.schema.json  # retained corpus contract
    ├── grounded-chat-v2.md                   # retained Track B contract
    ├── chat-provider-streaming-v1.md         # retained Track B transport
    ├── chat-evidence-packet-v2.schema.json   # retained Track B packet
    └── production-scan-ui.md                 # retained Track A UI
```

Proposed production ownership after all gates:

- Core owner: new `src/dotnet/Vulgata.Core/DomainServices/IPreparedScanInitiationStore.cs` for the closed command/result/tuple port and new `PreparedScanInitiationIdentityBuilder.cs` for the canonical execution fingerprint. `CanonicalEnvelope.cs` receives only the new Story 12.5 domain whitelist entry; its grammar and existing domains remain unchanged. No EF dependency.
- Infrastructure owner: new `src/dotnet/Vulgata.Infrastructure/Data/PreparedScanInitiationStore.cs`; it alone uses `VulgataDbContext`, Npgsql advisory locking and the existing tables/factories.
- Web application owner: new `src/dotnet/Vulgata.Web/Data/PreparedScanInitiationCoordinator.cs`, `PreparedScanInitiationAuthorizationRechecker.cs`, and `PreparedScanRuntimeIdentityResolver.cs` containing internal `IPreparedScanRuntimeIdentityResolver` plus sole default `ConfiguredPreparedScanRuntimeIdentityResolver`; plus the smallest exact-tuple/handoff consumption edits in `PreparedScanCoordinator.cs`, `PreparedScanPairBuilder.cs`, `PreparedScanEndpoints.cs`, and `Program.cs`. The current health probe and initiation share the configured resolver. The public vertical replaces only the internal resolver seam with deterministic valid identities; default composition and real integrity/CLI tests retain the configured implementation. `PrepareInitiatedAsync` and `IPreparedScanPairBuilder.BuildAsync` require the immutable tuple, and default production DI has no tuple-less fallback. Pair-builder parsing/selection/analysis algorithms remain unchanged.
- Test owner: `tests/Vulgata.Tests/PreparedScanning/PreparedScanInitiationIdentityTests.cs`, `PreparedScanInitiationCoordinatorTests.cs`, `PreparedScanInitiationPostgreSqlTests.cs`, `PreparedScanInitiationApiTests.cs`, and `PreparedScanRuntimeIdentityResolverTests.cs`, plus minimal production-composition assertions in the existing Prepared Scan test host/DI tests. The test/evidence harness owner preserves accepted Track A/B tests and later writes only bounded Track C-MVP release support.
- Shared integration owner: `Program.cs`, endpoint registration and any project file are single-owner files. No project-file change is expected.

Explicit forbidden production paths: `VulgataDbContext.cs`, every `Data/Configurations/*`, every migration and `VulgataDbContextModelSnapshot`, Story 12.4 source/contracts, `RepositoryProfilingCoordinator`, `CodeGraphContextProvider`, legacy `/scan-runs`, Worker/Pass 1/Pass 2/Chat behavior, and accepted Track A/B behavior. Adding one allowed `CanonicalEnvelope` domain does not modify the grammar or any accepted Story 12.2 identity output.

## Test and Verification Strategy

### Definition/Tier 1

1. **Public vertical production composition**: through `WebApplicationFactory<Program>` bound to disposable PostgreSQL, start only with an authenticated authorized user, one ordinary System, exactly two ordinary Repository records, exact Git-ref/Profiled-Revision bindings, and ready accepted CodeGraph 1.5/JDT-LS ports. Pre-seed no Scan Run, Pre-Scan Result, Scan Execution, coordination, Prepared Scan Manifest, File Code Unit, or Document. Replace only internal `IPreparedScanRuntimeIdentityResolver` with deterministic valid identities; retain production endpoint/coordinator/store/pair-builder/handoff composition. Assert the endpoint creates/replays the exact tuple, the existing pair builder receives it, and two Prepared Scan Manifests publish.
2. Repeat the identical request before publication after an induced builder failure and assert the same Scan Runs, Scan Executions and coordination re-enter preparation. Repeat after complete publication and after advancing coordination phase; assert the existing prepared handoff/response is returned with no external analysis calls and no duplicate state. Seed partial/corrupt prepared artifacts and assert conflict without rerun or repair.
3. Change each revision/pair/ref/runtime/prompt/schema/effective-authorization dimension and assert bounded conflict or missing-equivalent rejection, zero mutation, and no existing tuple details.
4. Use unauthorized principals plus deterministic resolver callbacks that revoke Administrator role, SystemOwner role, SystemOwner assignment, or Repository membership after the initial check but before the fresh pre-store recheck. Assert missing-equivalent 404, zero store calls and zero `ScanRuns`, `ScanExecutions`, or coordination rows. Preserve and separately prove the existing third authorization recheck immediately before prepared publication.
5. Inject failure after the initiation commit but before manifest publication; assert the committed complete tuple, no partial prepared output, and successful exact retry with the same tuple. Assert each published context snapshot uses the tuple initiation time and each Manifest/proof runtime hash equals its bound new-path execution.
6. **PostgreSQL concurrency/atomicity**: race identical requests from independent scopes/connections and assert one tuple; race competing revision/pair requests and assert at most one winner plus bounded conflicts. Inject cancellation, lock timeout and save failure before commit and assert zero visible tuple. These are PostgreSQL acceptance tests, not SQLite substitutes.
7. **Composition/security**: default production DI resolves one scoped `IPreparedScanInitiationStore`, the fresh authorization rechecker, and exactly one internal `IPreparedScanRuntimeIdentityResolver` mapped to `ConfiguredPreparedScanRuntimeIdentityResolver`. The endpoint resolves only the initiation coordinator, and reflection/composition assertions prove tuple-required coordinator/builder signatures with no fallback. Configured-integrity tests exercise the default resolver and the retained real CodeGraph CLI qualification remains supporting 2/2 evidence. `ICodeGraphAnalysisPort`/`IPreparedCodeGraphAnalysisPort` resolve to `CodeGraphAnalysisAdapter`; neither `CodeGraphContextProvider`, `ICodeGraphProvider`, nor historical `RepositoryProfilingCoordinator` resolves/executes.
8. **No later-stage effects**: counting/hostile test ports prove initiation/preparation invokes no Worker, Pass 1/2, Cross-Repository Resolution, Document, Edge, Uncertainty, Chat or LLM operation and performs no source/document/edge/LLM mutation beyond the existing Prepared Scan store's accepted Manifest/File Code Unit/Pre-Scan publication.
9. Core golden tests pin canonical domain `vulgata/story-12.5/prepared-scan-initiation-execution/1.0`, exact kind `PreparedScanInitiationExecution`, tag order, two execution GUID constants, Repository-reversal stability and the derived coordination GUID constant; mutation vectors cover every replay-only dimension. Contract tests also prove the fixed 12-20 corpus remains valid and contains no desired provider prose; database guard tests prove unique lowercase names and zero connection/product-host invocation for rejected targets; Markdown report checks prove the required shape.
10. Existing focused Track A/B regression, mandatory streaming, same-mode multi-turn re-fetch/revalidation, authorization/currentness, citation, accepted-only reveal, cancellation/mode-scope reset and UI suites remain green. Web build plus forbidden-path/model-diff guards prove no EF/Story 12.4/accepted Track A/B drift.

Existing tests for the historical 2.2 envelope remain unchanged and green as accepted Track A compatibility evidence, but regenerated Track C-MVP tasks do not implement their deferred attestation, ten-check, supersession/closure, counter-recomputation, outage or screenshot-automation concepts. New current-boundary tests target only the Markdown report shape and preconnection guard. The historical schema is clearly marked deferred; those ideas remain backlog.

### Required E2E

The combined host run is required because it begins from a fresh guarded lowercase `vulgata_story125_*` database, production-bootstraps the Administrator/System/provider/exact two Repositories, calls the corrected Prepared Scan entrance, and triggers real PostgreSQL, Git, CodeGraph, JDT LS, safe XML, Worker provider, Pass 1/Pass 2, Cross-Repository Resolution, browser/render, restart and screenshot predicates. The retained blocked database remains unchanged as historical evidence. The run may not be coverage-skipped by mocked tests and cannot continue to Pass 1 until real Prepared Scan succeeds.

**Duration estimate after authorization**: implementation plus focused TDD, PostgreSQL concurrency qualification, convergence and independent quality review is approximately **6-10 engineering hours** if no authority correction is required. The fresh-database real run is approximately **2-4 elapsed hours**: up to 60 minutes for the bounded two-Repository Scan, 15-30 minutes for twelve real-LLM questions, and 45-90 minutes for bootstrap, queries, restart, screenshots and report completion, with the remainder reserved for bounded diagnosis. External provider/tool/database unavailability stops this estimate rather than authorizing fake evidence or repeated unchanged runs.

### Pinned verification

- candidate HEAD and accepted Story 12.4 ancestry;
- clean owned diff and no migration/DbContext/snapshot/Story 12.4 changes;
- focused Track A/B/Track C guard tests;
- no-preseed vertical initiation and PostgreSQL replay/concurrency/rollback tests;
- Web build and applicable full/bounded test baseline;
- real-run report disposition and required evidence checklist;
- same database/revision bindings before and after restart;
- five redacted screenshot categories;
- zero secret/fake/unauthorized evidence.

## Requirement Coverage

| Scope | Plan coverage |
| --- | --- |
| FR-001-FR-036, FR-048-FR-060 | Preserved accepted Track A/B and Story 12.4 implementation; regression and migration/source guards only. |
| FR-037-FR-038 | Existing fixed corpus/schema retained; `q01`-`q12` execute independently with fresh Conversation State, and `followUpTo` is topical lineage only. |
| FR-039 | AD-CMVP-4 Markdown report. |
| FR-040 | Tier 1 strategy. |
| FR-041, FR-047 | AD-CMVP-2 preconnection database guard. |
| FR-042 | AD-CMVP-3 combined real product run and prerequisite gate. |
| FR-043 | AD-CMVP-5 queries. |
| FR-044 | AD-CMVP-7 and AD-CMVP-9 plus the manual twelve-row table and honest `q12` prompt-boundary oracle. |
| FR-045 | AD-CMVP-6 same-database application restart. |
| FR-046 | AD-CMVP-8 manual screenshots/redaction. |
| FR-049-FR-052 | Ownership/source/migration guards. |
| FR-061 | Explicit deferred boundary and legacy-schema marker. |
| FR-062 | External-prerequisite failure model. |
| FR-063-FR-064 | AD-PSI-1 pre-write validation and effective authorization; existing pre-publication recheck retained. |
| FR-065-FR-067 | AD-PSI-2 all-or-nothing tuple and closed transaction boundary. |
| FR-068 | AD-PSI-1 exact-tuple handoff into the existing coordinator/builder/store behavior. |
| FR-069-FR-073 | AD-PSI-3 deterministic replay, mismatch/concurrency rejection, post-init retry and legacy exclusion. |
| FR-074-FR-075 | AD-PSI-1/4 production composition and no later-stage effects. |
| FR-076 | Required public vertical plus PostgreSQL test matrix. |
| FR-077 | Fresh-database E2E continuation after implementation acceptance; retained blocked database unchanged. |
| FR-078 | Explicit general/single/N-Repository and legacy-consolidation backlog. |
| SC-009-SC-014, SC-018 | Required E2E report/query/restart/screenshot/blocked evidence. |
| SC-019-SC-025 | Initiation vertical, exact/concurrent replay, mismatch/authorization failure, retry, composition and transaction-timing evidence. |

All corrected FRs/SCs have an observable seam. No unresolved design placeholder remains.

## Post-Design Constitution Recheck

PASS. The plan adds one closed Core port, one Infrastructure store and one Web coordinator while preserving dependency direction. EF remains confined to Infrastructure; no model or migration changes. Existing FluentValidation/API/Chinese ProblemDetails behavior remains. Authorization is checked before initiation and before publication. The transaction contains no external analysis, Worker/LLM, or Document mutation. Integration-first public and PostgreSQL tests cover happy, denial, mismatch, concurrency, rollback and retry behavior. Documents, terminology, Story 12.4, Track A/B security and release evidence boundaries remain unchanged.

## Planned-Change Trigger Disposition

**Architecture-decision trigger: YES, current review required.** AD-PSI-1 through AD-PSI-4 add a production application/persistence boundary, define schema-free concurrency, and compose the existing Prepared Scan pipeline. A fresh independent plan-only review must accept this exact design.

**Repository planned-change trigger: YES, must pause after review.** The plan materially changes Scan scheduling/grouping/retry at the production entrance: it creates the exactly-two-Repository durable pair and defines exact replay after preparation failure. This is the sole exception authorized by PRD August 30 overlay and Story 12.5 FR-049/FR-063-FR-078; it neither adds nor changes a Worker Agent tool and does not weaken the PRD. Under AGENTS.md, task generation must pause for explicit user authorization after plan-review `ACCEPT`.

**Implementation intent at the gate**: after authorization, regenerate tasks, obtain independent definition acceptance and implementation approval, then use TDD to add only the Core port/contracts, Infrastructure atomic store, Web initiation coordinator/runtime-identity reuse, endpoint/DI composition, and specified tests. Do not begin production writes under this plan review itself.

## Judge-rubric value trace

| Rubric dimension | Candidate evidence |
| --- | --- |
| Innovation and creativity | real Cross-Repository business-flow extraction and grounded Business/IT explanation across two Java/XML Repositories |
| Technical implementation and complexity | exact Profiled Revisions, CodeGraph, JDT LS, safe XML, source-anchored Pass 1/Pass 2, current exact Edge/Flow Group authority, mandatory streaming and security regression |
| Practical value and deployability | one clean PostgreSQL database, host-run product, straightforward queries, application-process restart and explicit limitations |
| User experience and interaction design | Chinese production Scan feedback, Business/IT Chat, citations/Source Anchors and five real redacted screenshots |

## Inline Architecture Self-Review

Plan-review attempt 01 disposition:

| Finding | Closure |
| --- | --- |
| PSI-PLAN-001 | AD-PSI-1 now requires a fresh isolated-scope Administrator/SystemOwner/assignment/Repository recheck after identity resolution and immediately before store, with revocation zero-write tests. |
| PSI-PLAN-002 | AD-PSI-3 defines exact phase/artifact/handoff outcomes for pre-publication retry, complete prepared replay, advanced replay and corrupt/partial conflict. |
| PSI-PLAN-003 | Core owns exact canonical domain/tags, case-sensitive kind `PreparedScanInitiationExecution`, and literal golden execution/reversal/coordination vectors. |
| PSI-PLAN-004 | Internal `IPreparedScanRuntimeIdentityResolver` is the sole replaceable test seam; configured resolver is the only default and remains covered by composition/integrity/real CLI tests. |

| Lens | Result | Evidence |
| --- | --- | --- |
| Feasibility | PASS | Existing request validation, fresh Identity/System ownership reads, prompt/schema authority, runtime identity construction, factories, tables, Npgsql, handoff source and Prepared Scan pipeline supply every required capability. One new store is required because existing per-aggregate stores cannot create the tuple atomically. |
| Repository fit | PASS | Web orchestrates, Core declares the specific port, Infrastructure owns EF/Npgsql, and Program composes. No generic repository, second endpoint, second Scan stack or public runtime authority is introduced. |
| Invariants | PASS | Exactly one System/two Repositories, canonical pair order, exact refs/revisions/runtime/prompt/schema/fresh effective authorization, three authorization checks, complete tuple visibility, phase-aware replay, no legacy profiling satisfaction, and unchanged downstream publication are explicit. |
| Failure behavior | PASS | Revocation before store creates nothing; advisory timeout/cancel/save failure rolls back; partial/legacy/mismatch fails closed; pre-publication failure retains the tuple for exact retry; complete/advanced replay returns the handoff without analysis; external prerequisite failures remain sanitized. |
| Test seams | PASS | Public no-preseed endpoint composition replaces only internal runtime identity resolution; revocation callbacks prove the fresh pre-store check; disposable PostgreSQL covers advisory concurrency/rollback; phase/handoff and golden identity vectors cover replay drift; hostile/counting ports cover no later-stage effects. |
| Migration/compatibility | PASS | Existing tables/factories/constraints are sufficient. `PreScanResultId == Guid.Empty` remains the accepted new-path compatibility marker until current Prepared Scan publication creates its own result. One CanonicalEnvelope domain whitelist entry changes no existing envelope output. No DbContext/configuration/migration/snapshot/Story 12.4 change is planned. |
| Historical streaming compatibility | PASS | Plan/data/report contracts retain 16,384, 65,536, 1,048,576 and 131,072 plus `transportOutcome`, `rejectionCategory` and `validationOutcome`; Track C-MVP quotes rather than recomputes them. |
| Security | PASS | Initial, fresh pre-store and pre-publication authorization checks are distinct; Administrator/SystemOwner revocation and Repository drift fail missing-equivalent with zero writes. Runtime resolution is internal/server-owned. Secrets/paths remain excluded, database ownership is preconnection, screenshots are redacted and no provider fragments cross the client boundary. |
| Manual evaluation protocol | PASS | `q01`-`q12` are pinned and independently executed with fresh Conversation State; `q01`-`q03` retain topical lineage only across mode resets; `q12` is explicitly limited to `prompt-boundary-only`, while Tier 1 remains authority for same-mode multi-turn and actual authorization/currentness denial. |
| Constitution/ADR | PASS | Constitution I-V and AGENTS.md are satisfied. Story 12.1 AD-10 remains intact: production resolves CodeGraph 1.5 ports and does not register/execute CGC or the historical profiling coordinator. |
| Judge value | PASS | Work is concentrated on the rubric-visible real two-Repository Scan, Cross-Repository flow, grounded Business/IT Chat, restart and screenshots. |
| Retained/deferred boundary | PASS | Every mandatory retained value is mapped to the real run; every removed item is named as post-competition backlog. |
| Alternatives/consequences | PASS | Each architecture decision records rejected alternatives and operational consequences. |
| Placeholders | PASS | None. External values are runtime prerequisites with fail-closed handling, not design unknowns. |

**Self-review conclusion after plan-review correction 01**: `PLAN_READY_FOR_INDEPENDENT_PLAN_REVIEW_ATTEMPT_02`. PSI-PLAN-001 through PSI-PLAN-004 are closed by the three-check authorization sequence, phase-aware handoff replay table, pinned Core canonical identity/kind/golden vectors, and narrow internal replaceable runtime resolver with one configured default. The transaction, failure, compatibility, public outcome, test and release-continuation seams remain concrete. The plan intentionally triggers the Scan planned-change gate; no task or production write is authorized until review acceptance and explicit user authorization. Any later need for a migration, DbContext/model change, Story 12.4 mutation, CGC/legacy profiling, general Scan redesign, single/N-Repository support, Track A/B change or new release-proof subsystem is an authority contradiction.

## Downstream Task-Author Instructions (closed until planned-change authorization)

1. Route the initiation vertical through honest public RED, then minimal Core port/Infrastructure store/Web coordinator/exact-tuple builder integration GREEN. Do not test a direct store-only path as the vertical substitute.
2. Assign one coordinated owner to `Program.cs`, `PreparedScanEndpoints.cs`, `PreparedScanCoordinator.cs` and runtime identity composition. Keep DbContext/configurations/migrations/snapshot explicitly forbidden.
3. Separate PostgreSQL advisory-lock/rollback tests from faster coordinator/API characterization; do not claim SQLite proves production concurrency.
4. Prove every FR-076 outcome, including zero-preseed state; role/assignment/Repository revocation between initial and fresh pre-store checks; exact retry after induced pre-publication failure; no-analysis handoff recovery after publication/phase advance; corrupt partial conflict; fixed canonical identity vectors; default configured resolver/CodeGraph 1.5/no-CGC composition; and zero later-stage actions.
5. Preserve the manual-evaluation rules: pin `q01`-`q12`, fresh `ChatConversationState` per row, topical lineage only, and `q12` as `prompt-boundary-only` with Tier 1 owning actual denial/currentness proof.
6. After implementation acceptance, keep the retained blocked database unchanged; create a fresh guarded lowercase `vulgata_story125_*` database and begin the real run only through the corrected endpoint.
