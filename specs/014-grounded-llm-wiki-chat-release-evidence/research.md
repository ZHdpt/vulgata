# Research: Story 12.5 Track C-MVP

## Sources inspected

- corrected Story 12.5 specification and approved 2026-08-29 Track C-MVP, 2026-08-30 Prepared Scan initiation, and 2026-08-31 exact-profile seed-selection proposals;
- PRD Section 0.1, Story 12.5, August 30 architecture and docs/judge-rubric.txt;
- Constitution, AGENTS.md, Ubiquitous Language and accepted Track B final verification;
- existing Track A/B release-gate tests, fixed question corpus and database-name guard;
- production Prepared Scan endpoint/coordinator/builder/store, Scan Run/new-path Execution/Two-Repository coordination factories and EF mappings, `WorkerAgentDispatchIdentitySource`, embedded Prepared Pass 1 v2 prompt/dispatcher, runtime/prompt/schema identity sources, Pass 1/Pass 2, production Scan-status and Prepared Chat composition;
- accepted Prepared Pass 1 identity decision/history plus current exact-profile seed-selection authorization `story-12-5-run-03-all-remaining-tasks-approval-02`;
- current contracts under contracts/, treated as downstream artifacts rather than authority.
- run-03 immutable Prepared Scan/Pass 1/audit evidence and the production `ExactResolutionProfileCatalog`, `PreparedScanPairBuilder`, `PreparedScanPlanner`, `PreparedRelatedFileExpansion`, `PreparedPass1Store`, and exact-resolution coordinator/source seams.

No unresolved design unknown remains. Machine- and credential-specific values are runtime prerequisites with fail-closed outcomes.

## R-1: Track C-MVP should operate the product, not duplicate it

**Decision**: execute the existing authenticated Vulgata endpoints, hosted services and Blazor pages in one host run.

**Rationale**: PreparedScanEndpoints, Pass1ExecutionEndpoints, Pass2ExecutionEndpoints, ManualResumeEndpoints, the Pass 1/Pass 2 hosted services, ProductionScanStatusReader, PreparedChatCoordinator and ChatPage already compose the accepted path. A second orchestration harness would not prove the judge-visible product.

**Alternatives considered**:

- copy the Story 12.4 production qualification into Story 12.5 — rejected because it clones another story's orchestration and database assumptions;
- add a Track C release-orchestration coordinator — rejected as a new proof product; the bounded Prepared Scan initiation coordinator in R-2 is a distinct required product entrance;
- keep a separate Tier 2 controlled-provider run — explicitly deferred.

## R-2: The production delta is one bounded Prepared Scan entrance

**Decision**: add `PreparedScanInitiationCoordinator`, one Core-specific initiation store port, one Infrastructure atomic store, and the minimum endpoint/DI/exact-tuple pair-builder composition. The endpoint resolves only the initiation coordinator; the existing coordinator and pair builder require the returned immutable tuple rather than retaining a tuple-less production bypass. Retain the existing Prepared Scan request/response, analysis, selection and publication behavior.

**Rationale**: the clean-database run proved the current endpoint reaches `PreparedScanPairBuilder` without the two Scan Runs and Preparing coordination it assumes. Existing tests seed those records. The product therefore lacks a normal entrance even though CodeGraph 1.5 and downstream preparation are valid.

**Alternatives considered**:

- add no production code and seed release rows — rejected because it would not prove production behavior;
- restore the historical `/scan-runs` coordinator/CGC — rejected by Story 12.1 AD-10;
- create a separate story or release-only endpoint/harness — rejected by the approved in-place correction;
- add persistence for release evidence — still rejected because the report remains a repository artifact.

**Historical gate implication**: this material Scan entrance change completed its own independent review and authorization cycle. The current correction preserves that accepted implementation and opens a new material selection trigger under R-22.

## R-3: Database safety must precede every product connection

**Decision**: reuse Story125ReleaseHarness.CreateRunIdentity and IsOwnedDatabaseName; add only enough test seam to prove rejected names cause zero connection/product-host calls.

**Rationale**: the current regex already creates unique lowercase vulgata_story125_* identities and rejects common protected/malformed targets. A spy-able preconnection boundary closes the remaining ordering proof without becoming a provisioner.

**Alternatives considered**:

- inspect the database after connection — too late;
- trust operator naming — unsafe;
- reuse the main or Story 12.4 database — prohibited.

## R-4: One Markdown report is the current evidence contract

**Decision**: replace the Track C acceptance role of story125-release-evidence/2.2 with the human-readable Markdown contract.

**Rationale**: the approved correction explicitly removes rigid ten-check semantics, attestation, closure/supersession graphs and exhaustive artifact hashing. Judges need inspectable revisions, commands, query results, Chat outcomes, restart and screenshots, not a second semantic proof engine.

**Alternatives considered**:

- reduce the JSON schema but keep it authoritative — still creates a rigid release envelope and duplicate maintenance;
- persist a release-summary entity or page — explicitly deferred;
- delete historical schema/tests immediately — rejected because accepted Track A history should remain visible. The schema is marked deferred and no longer used as Track C-MVP acceptance authority.

## R-5: Straightforward queries are enough

**Decision**: run read-only observations against existing vulgata tables for Prepared Scan Manifests/entries, Scan/job terminal state including explicit high-value-seed selected/succeeded/failed counts, Documents/Source Anchors, exact Cross-Repository Resolution decisions/REFERENCES Edges, Uncertainties, Business Logic Documents and Flow Groups.

**Rationale**: these rows are already authoritative. Track C should report them, not reconstruct a new relationship or release authority.

**Alternatives considered**:

- add a release query API — unnecessary product surface;
- recompute decision and Flow Group semantics in a harness — duplicates Story 12.4 and Track B validators;
- edit rows to satisfy minimums — forbidden fabrication.

## R-6: Application restart is the correct persistence proof

**Decision**: keep PostgreSQL running, stop/restart only Vulgata, then re-authenticate and inspect the same wiki/Chat scope and pre/post publication counts.

**Rationale**: the August 30 architecture explicitly replaces container restart with process restart. Persistent Documents, Edges and Flow Groups must survive; transient ChatConversationState is intentionally scoped and need not.

**Alternatives considered**:

- restart/recreate the database — destroys the continuity being proved;
- require conversation restoration — contradicts accepted transient conversation design.

## R-7: Streaming proof remains owned by Track B

**Decision**: retain all Track B raw streaming, UTF-8/SSE/JSON/cap/deadline/terminal/cancellation tests. Compatibility artifacts continue to quote 16,384, 65,536, 1,048,576 and 131,072 plus `transportOutcome`, `rejectionCategory` and `validationOutcome`. In the real run, record provider identity, transport composition, final closed outcome and complete-render duration; quote existing neutral observations when available.

**Rationale**: recomputing byte domains or signing observations in Track C was the removed proof subsystem. Mandatory streaming itself is retained and cannot be replaced by aggregate/non-streaming or deterministic rescue output.

**Alternatives considered**:

- recompute every raw/SSE/received/decoded counter — explicitly deferred;
- accept provider SDK aggregate streaming — violates accepted transport authority;
- accept fake provider evidence — prohibited.

## R-8: Manual evaluation preserves the fixed corpus without prescribing prose

**Decision**: pin `q01` through `q12` from the existing corpus and execute every case as an independent real-run row with fresh `ChatConversationState`. Preserve `followUpTo` as topical lineage for human scoring only. Thus `q01` (Business), `q02` (IT) and `q03` (Business) do not share server conversation/evidence state across their mode changes. Record observation, usefulness, citations, disclosure and duration manually.

**Rationale**: the accepted mode/scope contract clears Conversation State on a mode change. Tier 1 already proves same-mode multi-turn re-fetch/revalidation and actual authorization/currentness denial behavior. Under the real run's one authenticated principal and exactly-two-authorized-Repositories scope, `q12` can prove only the `prompt-boundary-only` outcome: asking about a Repository outside selected scope does not make the answer confirm its existence or disclose its Business Rules, Documents, relationships, citations or Source Anchors. It cannot honestly prove a denied principal, known protected Repository, revocation or true missing-equivalent behavior.

**Alternatives considered**:

- automate a second LLM judge — adds cost and non-deterministic authority;
- cherry-pick only successful questions — violates repeatability;
- carry `q01` state into `q02`/`q03` — contradicts accepted mode reset;
- add a third Repository or denied-principal fixture — broadens the accepted public seam and is unnecessary because Tier 1 owns that security proof;
- copy planned fields into results or label `q12` `denied-access-exercised` — fabricates execution.

## R-9: Screenshots are manual evidence with a redaction checklist

**Decision**: capture five real views manually and record relative paths plus redaction disposition in the Markdown report.

**Rationale**: visual proof is judge-facing; automated candidate binding and image validation are explicitly deferred.

**Alternatives considered**:

- add a screenshot product feature — deferred;
- hash/cross-link every image — deferred;
- accept fake Backup B images — prohibited.

## R-10: External prerequisites fail closed

**Decision**: missing PostgreSQL/Docker, Repository revision, CodeGraph/JDT LS/Java, provider credential/network, authentication or browser capability yields a sanitized blocked report section and no success claim for dependent work.

**Rationale**: a release run cannot manufacture unavailable external observations. Diagnosing prerequisites before rerunning an unchanged expensive run preserves time and evidence integrity.

**Alternatives considered**:

- silently skip the check — dishonest;
- substitute fixtures/fake provider — cannot support release evidence;
- print full environment/connection details — secret leakage risk.

## R-11: No migration, Story 12.4 or downstream Scan-strategy change

**Decision**: freeze `VulgataDbContext`, entity configurations, migrations, model snapshot, Story 12.4 contracts, and all parsing/filtering/selection/Worker/Pass behavior. Use the existing `ScanRuns`, `ScanExecutions`, and `TwoRepositoryScanCoordinations` tables for only the approved exactly-two-Repository entrance.

**Rationale**: existing factories, keys and foreign keys express the complete tuple. The missing property is transaction ownership, not storage shape.

**Alternatives considered**: a tuple table, idempotency column, unique pair index, changed model constraint, Story 12.4 contract, or general lifecycle consolidation is outside authorization and unnecessary for the bounded entrance.

## R-12: Planned-change trigger

**Decision**: require fresh independent plan-only and definition review for the reviewed-profile seed architecture. After acceptance, persist the normal scope-bound decision artifacts and use current authorization `story-12-5-run-03-all-remaining-tasks-approval-02` to continue task generation and implementation without another human pause.

**Rationale**: the plan adds no Agent tool and does not weaken the PRD, but it materially changes matched-pair Scan seed selection. AGENTS.md explicitly gates Scan strategy changes. The current user authorization covers remaining in-scope human gates and implementation, but it does not replace the independent reviews, TDD, convergence, quality, E2E, isolation or final-verification gates.

## R-13: One short store transaction is required

**Decision**: add `IPreparedScanInitiationStore` and implement it with one short PostgreSQL `ReadCommitted` `VulgataDbContext` transaction, one save and one commit over two `ScanRun.Start`/`BindProfiledRevision`, two `ScanExecution.CreateNewPath`, and one `TwoRepositoryScanCoordination.Create` aggregate. R-14's transaction advisory lock serializes every writer in this bounded production entrance.

**Rationale**: `IScanRunRepository.AddAsync`, `IDurableScanStore.CreateScanExecutionAsync`, and `ITwoRepositoryScanCoordinationStore.CreateAsync` are separate commit seams. Calling them sequentially cannot provide complete-tuple-or-none visibility.

**Alternatives considered**:

- compose existing stores sequentially — partial state can escape;
- hold one ambient transaction across those repositories and external preparation — couples contexts and creates long locks;
- move the transaction into Web — violates layering.

## R-14: Transaction-scoped advisory serialization is the schema-free concurrency mechanism

**Decision**: inside the transaction, execute `SET LOCAL lock_timeout = '5s'`, acquire one PostgreSQL transaction-scoped signed-64-bit advisory lock derived from a versioned System-scoped SHA-256 preimage, then inspect all System coordinations. Create only when none exists; replay only one exact complete new-path tuple; otherwise conflict without mutation. PostgreSQL `55P03` or caller cancellation maps to unavailable after rollback.

**Rationale**: the existing schema has no unique System/pair constraint. Serializable isolation alone can still require write-skew retry reasoning, a pair/revision lock permits competing mismatched pairs to create, and a process semaphore does not protect multiple hosts. A System lock is conservative, bounded to this one-pair MVP, automatically releases on commit/rollback, and adds no schema.

**Alternatives considered**:

- session advisory lock — unsafe with pooled connections;
- pair/revision advisory lock — does not serialize changed-pair replay;
- deterministic IDs alone — detects some duplicates but cannot make the whole multi-row tuple atomic;
- migration-backed unique constraint — prohibited.

## R-15: Deterministic identities bind replay dimensions that are not columns

**Decision**: canonicalize Repository order with `Guid.CompareTo`. Core `PreparedScanInitiationIdentityBuilder` uses the exact CanonicalEnvelope domain/tags and case-sensitive deterministic kind pinned in R-20 to bind freshly rechecked authorization scope, System, Repository, exact ref/revision, prompt/schema and full runtime identity. `TwoRepositoryScanCoordination.Create` derives its ID from those execution IDs. The store validates those identities on replay.

**Rationale**: Git ref and initiating authorization scope are not columns in the initiation tuple. Deterministic execution IDs bind them without a migration. A changed ref, identity or scope derives a different expected tuple and conflicts with the one System pair.

**Alternatives considered**:

- omit ref/scope from replay — permits a mismatched caller to attach;
- persist raw ref/scope in a new table/column — prohibited;
- reuse an uncoordinated or historical Scan Run — cannot prove new-path authority.

## R-16: Preserve new-path temporal and runtime compatibility

**Decision**: keep `ScanExecution.PreScanResultId` empty, use one initiation instant for both Scan Runs and executions, and have `PreparedScanPairBuilder` consume the exact initiated executions. It uses their per-Repository runtime hashes for CodeGraph/JDT-LS/Manifest packages and their initiation instant for the later context-snapshot `CapturedAt`.

**Rationale**: `CreateNewPath` intentionally distinguishes the new path with non-null prompt/schema/runtime fields while leaving the legacy required column empty and without a foreign key. `VulgataDbContext` requires the context snapshot not to post-date execution start, and Prepared Pass 1 requires execution/Manifest runtime identity equality. The current builder's end-of-analysis capture time and global readiness hash would violate those invariants for newly initiated executions unless it consumes the exact tuple.

**Alternatives considered**:

- create/attach a legacy Pre-Scan Result — forbidden and semantically false;
- use a future execution start time — dishonest temporal data;
- modify `VulgataDbContext` validation/model — prohibited;
- keep the builder's assumed Repository lookup/global hash — loses exact execution/runtime binding.

## R-17: The existing endpoint contract is sufficient

**Decision**: retain `POST /api/systems/{systemId}/prepared-scans`, `PreparedScanRequest`, `PreparedScanResponse`, and 201/200 prepared success. Add only bounded initiation conflict/unavailable failure codes before the existing downstream mapping.

**Rationale**: all server-owned identities can be resolved internally; exposing tuple, runtime or lock details would expand attack surface and caller authority. A public no-preseed endpoint test can prove the full product entrance.

**Alternatives considered**: new initiation endpoint/DTO, caller-supplied runtime identity, or returning internal tuple IDs on failure are unnecessary and weaken the closed boundary.

The current `ConfiguredAnalysisRuntimeHealthProbe` already performs the complete tool/file integrity checks and creates the canonical Repository/Profiled-Revision-specific `AnalysisRuntimeIdentity`, but retains only its hash in readiness state. A narrow internal `IPreparedScanRuntimeIdentityResolver` makes runtime resolution replaceable by the public vertical test; sole default `ConfiguredPreparedScanRuntimeIdentityResolver` contains the extracted checked implementation and remains covered by composition, configured-integrity and real CLI tests. The abstraction is internal Web composition only, not public, caller-selected, Agent-facing, CGC-backed or a general analysis port. Prepared Pass 1 prompt/schema identity is a separate Core value port with one Agents implementation under R-21; it does not widen the runtime resolver.

## R-18: Authorization is checked three times, including a fresh pre-write check

**Decision**: retain the initial endpoint/coordinator visibility check, then after prompt/schema/runtime resolution perform a fresh isolated-scope Web authorization check immediately before the store call. Reload the user and roles, verify both Repository memberships, and accept only current Administrator or current SystemOwner role plus current SystemOwner assignment. Retain the existing final recheck immediately before Prepared Scan publication.

**Rationale**: `IUserContext.IsAdministrator` is request-scoped/lazy and can be stale across integrity resolution. The first check gives fast rejection, the second protects initiation writes from intervening revocation, and the third protects publication after long external analysis.

**Tests**: use the replaceable runtime resolver as a deterministic barrier/callback to revoke Administrator role, SystemOwner role, assignment and Repository membership between checks; each must produce missing-equivalent NotFound, zero store calls and zero initiation rows.

## R-19: Replay is phase-aware and reuses a complete handoff

**Decision**: exact `Preparing` state with no prepared artifact is the only replay state allowed to re-enter preparation. If any prepared artifact exists, or coordination has advanced, validate/recover through existing `IPreparedScanHandoffSource`; exact complete state returns the existing replay response without analysis, while partial/corrupt/incompatible state conflicts.

**Rationale**: treating every exact tuple as a reason to rerun Git/CodeGraph/JDT-LS/XML wastes work and becomes invalid after coordination advances. The store reports only phase and artifact presence; the existing handoff source remains the semantic validator.

## R-20: Core owns the canonical execution identity and golden vectors

**Decision**: add Core `PreparedScanInitiationIdentityBuilder`, allow exact `CanonicalEnvelope` domain `vulgata/story-12.5/prepared-scan-initiation-execution/1.0`, and pass its SHA-256 to exact kind `PreparedScanInitiationExecution`. Golden tests freeze two execution GUID constants, Repository-reversal stability and the derived coordination GUID constant.

**Rationale**: an unowned delimiter preimage or unpinned kind literal permits silent replay identity drift. Reusing the established tagged canonical grammar gives one framing authority; literal expected outputs detect changes independently of the builder under test.

## R-21: Prepared Pass 1 identity must be owned beside the v2 prompt

**Decision**: add Core `PreparedPass1ContractIdentity` and `IPreparedPass1ContractIdentitySource.GetCurrent()`. Add one production implementation, Agents-owned `EmbeddedPreparedPass1ContractIdentitySource`, which resolves the exact embedded logical resource `Vulgata.Agents.Prompts.pass1-code-logic-worker.v2.md`, decodes it as strict UTF-8, validates that the text is nonempty and declares `code-logic/file-v2`, hashes the exact decoded string re-encoded as UTF-8, and returns `pass1-code-logic-worker.v2/sha256/{lowercase-64-hex}` plus `code-logic/file-v2`. `PreparedScanInitiationCoordinator` consumes this port and rejects null, legacy-v1, malformed-hash or wrong-schema output as unavailable before fresh authorization or persistence.

**Rationale**: source inspection proves `WorkerAgentDispatchIdentitySource` is the accepted legacy recovery authority: it hashes `pass1-code-logic-worker.v1.md` and returns `vulgata.knowledge-document/1.0`. Source also proves Prepared Pass 1 dispatch loads `pass1-code-logic-worker.v2.md` and its validator requires `code-logic/file-v2`. Reusing the recovery source cannot produce a valid new-path identity. Agents already owns and embeds the executed prompt, so it is the only layer that can authoritatively bind the prompt bytes without a Web hardcode. Core exposes only the dependency-free two-string value/port.

**Alternatives considered**:

- modify `WorkerAgentDispatchIdentitySource` — rejected because it changes accepted recovery identity behavior and compatibility evidence;
- hardcode v2/schema in `PreparedScanInitiationCoordinator` — rejected because Web would become a second drift-prone prompt authority;
- accept the generic knowledge schema — rejected because it violates FR-066 and the Prepared Pass 1 parser/committer contract;
- inject `PreparedPass1WorkerDispatcher` or a provider — rejected because initiation needs identity only and must not acquire an Agent execution surface;
- add a public request field — rejected because callers cannot select server contract identity.

**Consequences**: default DI has exactly one Core-port-to-Agents mapping. The existing v2 prompt, dispatcher, provider, tools and retry/scheduling behavior remain unchanged; the legacy source and recovery tests remain unchanged. Authority/composition tests independently compute the resource hash, pin the exact prefix/schema, prove coordinator constructor separation, prove legacy output preservation, and prove hostile identity values fail before authorization recheck/store/external analysis. No EF, project-file, public DTO or persistence change follows from this decision.

## R-22: The embedded reviewed exact profile may contribute mandatory seeds only after a complete identity match

**Decision**: add Core `IReviewedExactProfileSeedAuthority` and one stateless Infrastructure `ReviewedExactProfileSeedAuthority` over the unchanged `ExactResolutionProfileCatalog`. The input is exactly two server-observed bindings: Repository ID, persisted `Repository.Name` as the profile's ordinal stable identity, safe full requested ref and lowercase Profiled Revision. The adapter returns `NonMatching` with no paths unless both bindings map one-to-one to both profile repositories and every identity/ref/revision equals the reviewed value. On a match it derives the canonical union of consumer files, provider declaration files, provider implementation Document paths and unsupported-observation owner Document paths, bound to the matched Repository IDs.

`PreparedScanPairBuilder` marks every exact bounded candidate segment for those required Repository/path pairs as new `PreparedBusinessEvidence.ReviewedExactResolutionProfile`, score 1,100. That is the single closed tier above confirmed communication 1,000 and therefore survives the existing per-Repository top-20 seed choice before optional high-value evidence. All existing 20-per-Repository, 150-pair, CodeGraph expansion, stable ordering, audit, Manifest fingerprint and Pass 1 `>=400` mandatory-seed behavior remains in force.

**Rationale**: run-03 selected 150 units but zero high-value seeds; all selected units belonged to one Repository. Three safe reviewed sources were excluded by the pair cap, so exact resolution correctly refused for lack of current source-anchored Documents. The profile already is the reviewed source of truth for exact consumer/provider/unsupported selectors. Reusing it through a server-owned read-only adapter closes the cross-stage prerequisite without duplicating sample values or changing Story 12.4. Score 1,100 is necessary because a tie at 1,000 could still allow unrelated earlier paths to occupy a full 20-seed set. Existing Pass 1 reconciliation already turns any selected high-value failure into `FailedPass1`, so no second failure policy is required.

**Exact failure boundary**:

- embedded profile unavailable/invalid: `Unavailable`, zero Manifest;
- complete identity nonmatch: neutral `NonMatching`, no classification, score, audit or fingerprint effect;
- matched required path missing or duplicated in inventory, filtered, unsafe, empty/hash-invalid, without an exact bounded Java/XML split, or producing more than 20 required segments in a Repository: closed rejected preparation before CodeGraph expansion/Manifest commit;
- matched valid path: every segment is required, rather than guessing one symbol/line segment or adding a second selector parser;
- required Pass 1 job failure: existing `FailedPass1`, zero exact resolution/Pass 2 start.

**Alternatives considered**:

- keep generic selection and increase the pair cap — rejected because it does not guarantee reviewed evidence and violates the 150 bound;
- hardcode four paths/sample names in Core or Web — rejected by PRD/architecture and duplicates the profile;
- add paths/profile to the public request or operator configuration — rejected because untrusted input may not select evidence;
- reuse score 1,000 — rejected because top-20 ties do not make reviewed candidates mandatory;
- insert candidates after planning — rejected because it bypasses canonical audit/fingerprint/Pass 1 accounting;
- parse profile selectors in Web and pick one split by line/symbol — rejected because it creates a second exact-resolution parser and cannot safely identify every required source segment;
- change `ExactResolutionProfile`, its catalog/resource, exact resolver, Pass 1 store/policy, Story 12.4 contracts or persistence model — rejected as unnecessary and prohibited.

**Consequences**: the architecture trigger is a material matched-pair Scan-selection change and requires independent plan review. Implementation ownership is limited to the new Core port/value, new Infrastructure adapter, one planner enum/score, builder validation/promotion, `Program.cs`, and their tests. Neutral fixtures must preserve exact prior selections and fingerprints. Run-03 remains historical diagnostic evidence; after technical acceptance the release restarts from a fresh guarded database.
