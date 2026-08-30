# Data Model: Story 12.5 Track C-MVP

Track C-MVP introduces no new persistent entity or persistence shape. The bounded initiation correction creates one exact tuple from existing Scan Run, Scan Execution and Two-Repository Scan Coordination entities. The reviewed exact-profile seed correction adds only immutable in-memory values and one closed existing Manifest score; the remaining concepts are represented in one Markdown report and adjacent screenshot files. Existing Track A/B and Story 12.4 domain/persistence authorities remain unchanged.

## 1. Existing persistent authorities

The combined run observes, but does not reshape:

- System and exactly two Repositories;
- Scan Run, Scan Execution, Scan Job Execution, Prepared Scan Manifest and File Code Unit;
- Logical Document, immutable Document Version, Document Section and Source Anchor;
- REFERENCES and GENERATED_FROM Document Edges;
- accepted Cross-Repository Resolution decisions and their exact current Edge;
- Uncertainty;
- Flow Group, Flow Trace and flow steps;
- LLM provider configuration through non-secret identity only.

No release report row, signature, run graph or screenshot row is added to VulgataDbContext.

### ReviewedExactProfileSeedRequest (Core value, non-persistent)

| Field | Rule |
| --- | --- |
| repositories | exactly two distinct bindings; canonical result is independent of request order |
| repositoryId | non-empty server-observed Repository ID |
| stableGitIdentity | exact persisted `Repository.Name`, compared ordinally to existing profile `StableGitIdentity`; not caller input, `NormalizedName`, alias or Git URL |
| requestedRef | exact existing safe full Git ref |
| profiledRevision | exact lowercase commit bound to the initiated execution |

The public `PreparedScanRequest` does not expose this value. `PreparedScanPairBuilder` constructs it only from the accepted initiated tuple, loaded Repository records and revision bindings after the initiation transaction has closed.

### ReviewedExactProfileSeedAuthorityResult (Core value, non-persistent)

| Field | Rule |
| --- | --- |
| outcome | closed `NonMatching`, `Matched`, or `Unavailable` |
| profileId / profileVersion / profileFingerprint | present only for `Matched`; copied from the existing validated embedded profile; in-memory authority metadata, not a new database/report field |
| requiredSources | empty for `NonMatching`; canonical nonempty immutable set for `Matched` |
| repositoryId | exact runtime Repository selected by one-to-one stable-identity match |
| relativePath | existing profile-validated canonical Repository-relative path |
| roles | nonempty canonical union of `Consumer`, `ProviderDeclaration`, `ProviderImplementation`, `UnsupportedObservationOwner` |

An overlapping path has one required-source row with multiple roles. A path is never accepted without its matched Repository binding. Catalog integrity failure is `Unavailable`; a partial identity/ref/revision match is `NonMatching` and has no selection effect.

### Reviewed exact-profile candidate state (existing Manifest fields)

No new entity or column is added. For a matched profile, every exact bounded `PreparedFileCodeUnitDraft` segment whose `(RepositoryId, RelativePath)` is required uses:

| Existing field | Required value |
| --- | --- |
| DirectBusinessScore | `1100` |
| FinalBusinessScore | exact `1100`; existing related-file expansion cannot outrank the direct required score |
| SelectionReason | existing `DirectBusinessEvidence` |
| Manifest/Pair ordinal | existing stable score -> Repository -> path -> segment ordering |

The new closed `PreparedBusinessEvidence.ReviewedExactResolutionProfile` maps to 1,100. Existing values 1,000/900/800/700/400/100 remain unchanged. Existing Pass 1 high-value accounting (`DirectBusinessScore >= 400`, maximum 20 per Manifest) therefore includes every reviewed segment without a new Pass 1 field or policy.

Matched state transitions:

```text
exact identity match
  -> canonical required Repository/path set
  -> exact inventory/safe-split mapping
  -> all required candidate segments score 1100
  -> all required segments present in existing top-20 seeds and 50-150 plan
  -> existing atomic two-Manifest publication
  -> existing Pass 1 mandatory-seed accounting
  -> all required succeed: exact resolution eligible
  -> any required fails: FailedPass1, exact resolution/Pass 2 not started

nonmatch -> empty authority result -> existing generic candidates/scores/audits/fingerprints
matched incomplete/unsafe/ambiguous/split-invalid/over-bound -> rejected before Manifest publication
```

### PreparedPass1ContractIdentity (Core value, non-persistent)

One server-owned value crosses from the sole Agents implementation to Web initiation through `IPreparedPass1ContractIdentitySource.GetCurrent()`:

| Field | Rule |
| --- | --- |
| promptIdentity | exact `pass1-code-logic-worker.v2/sha256/{lowercase-64-hex}`; the hash covers the strict-UTF-8-decoded embedded v2 prompt string re-encoded as UTF-8 |
| documentSchemaIdentity | exact `code-logic/file-v2` |

The source has no caller/System/Repository/provider/tool input and returns no prompt content. Null, wrong version/prefix, malformed hash, wrong schema, missing/duplicate resource, invalid UTF-8 or schema-inconsistent prompt content is Not Ready and produces no initiation write. `WorkerAgentDispatchIdentitySource` is a separate unchanged legacy recovery authority and is not a value source for this model.

## 2. PreparedScanInitiationCommand (Core value contract)

One normalized server-owned command crosses from Web orchestration to the Infrastructure store.

| Field | Rule |
| --- | --- |
| authorizationUserId | exact authenticated user ID; never returned publicly |
| authorizationMode | exact `Administrator` or `SystemOwner` result from the fresh live recheck immediately before the store call; never copied from the initial cached request context |
| systemId | one non-empty visible System |
| repositories | exactly two, distinct, ordered by `Guid.CompareTo` |
| repositoryId | non-empty and belongs to `systemId` |
| requestedRef | existing safe full `refs/heads/*` or `refs/tags/*` validation |
| profiledRevision | exact lowercase 40-hex expected commit |
| promptIdentity | exact current Prepared Pass 1 identity from `IPreparedPass1ContractIdentitySource`; `pass1-code-logic-worker.v2/sha256/{lowercase-64-hex}` only |
| documentSchemaIdentity | exact current `code-logic/file-v2` returned by the same Agents-owned authority |
| runtimeIdentityJson | full canonical `analysis-runtime/1` JSON for this Repository/revision, no host path |
| runtimeIdentityHash | lowercase SHA-256 of that canonical JSON |
| providerProcessTimeoutSeconds | exact established value `1800` for `ScanRun.Start`; grants no CGC execution or configuration |
| initiatedAt | one UTC instant shared by both Scan Runs and executions |

Core `PreparedScanInitiationIdentityBuilder` owns `CanonicalEnvelope` domain `vulgata/story-12.5/prepared-scan-initiation-execution/1.0`; tags 1 through 10 are normalized user UUID, authorization mode, System UUID, Repository UUID, exact case-sensitive ref, lowercase Profiled Revision, prompt identity, Document schema identity, runtime JSON and runtime-hash bytes. The lowercase SHA-256 is passed to exact kind literal `PreparedScanInitiationExecution`. The public request supplies no server-owned identity.

## 3. PreparedScanInitiationTuple (existing persistent entities)

### Scan Run x2

Created only through `ScanRun.Start`, then `BindProfiledRevision` before persistence.

| Invariant | Rule |
| --- | --- |
| RepositoryId | one canonical command Repository |
| ProfiledRevision | exact command revision, immutable |
| Status | `Profiling`; this is an initiation carrier, not proof that legacy profiling ran |
| StartedAt | command `initiatedAt` |
| ProviderProcessTimeoutSeconds | established 1,800-second compatibility value only; CGC is not resolved, configured or called |

Historical uncoordinated Scan Runs, even for the same Repository/revision, are ignored and never replay candidates.

### New-path Scan Execution x2

Created only through `ScanExecution.CreateNewPath`.

| Invariant | Rule |
| --- | --- |
| Id | deterministic Repository-specific command identity |
| System/Repository/ScanRun/ProfiledRevision | exact tuple binding |
| Phase | `Prepared`, state version 0 |
| PromptIdentity / DocumentSchemaIdentity | exact current server authorities |
| AnalysisRuntimeIdentityJson/Hash | complete per-Repository canonical identity and matching hash |
| StartedAt / UpdatedAt | common `initiatedAt` |
| PreScanResultId | `Guid.Empty`, the accepted new-path compatibility marker; no legacy result is created or attached |

### Two-Repository Scan Coordination x1

Created only through `TwoRepositoryScanCoordination.Create(executionOne, executionTwo, initiatedAt)`. The factory canonicalizes Repository order and deterministically derives the coordination ID from System and the two deterministic execution IDs. Initial phase is `Preparing`, state version 0.

### Tuple state and visibility

```text
absent --atomic create--> Preparing initiation tuple
absent --rollback/cancel/lock timeout--> absent
Preparing exact tuple --identical authorized request--> same tuple (Replay)
Preparing exact tuple --mismatched/legacy/ambiguous request--> Conflict, unchanged
Preparing exact tuple --external preparation failure--> same replayable tuple
Preparing exact tuple --prepared publication--> existing Prepared Scan state/hand-off
Preparing exact tuple + exact hand-off --replay--> existing response, no analysis
advanced exact coordination + exact hand-off --replay--> existing response, no analysis
advanced coordination without exact hand-off --> Conflict, unchanged
```

The transaction exposes all five rows or none and reports the observed coordination phase plus `preparedArtifactPresence` (`None` or `Present`). Presence covers any Manifest, prepared-v2 Pre-Scan Result, context snapshot, Manifest Entry/audit or File Code Unit bound through the exact coordination/Run/Manifest IDs. It does not construct or validate a handoff. Outside the transaction, existing `IPreparedScanHandoffSource` validates a complete exact handoff. Only `Preparing + None` may enter/re-enter external preparation; `Present` or an advanced phase must recover the handoff or conflict without rerunning analysis. A later prepared context snapshot uses `initiatedAt` as `CapturedAt`, satisfying the existing snapshot/execution temporal invariant; its Manifest and proofs use the bound execution runtime hash. The accepted Prepared Scan store, not initiation, creates current Prepared Scan Manifests, File Code Units, prepared Pre-Scan Results and snapshots.

The 1,800-second value satisfies the existing durable `ScanRun` field only. It is not a CGC runtime identity or an analysis timeout selected by the obsolete profiling coordinator.

## 4. TrackCMvpPreflight

One report section recorded before product data access.

| Field | Rule |
| --- | --- |
| observedAt | ISO-8601 timestamp |
| candidateCommit | exact 40-character Git commit |
| story124Commit | exact accepted commit 4798b97d... |
| worktreeState | branch and clean/owned status; no secret paths |
| repositoryOneAlias / repositoryTwoAlias | non-secret display aliases |
| repositoryOneProfiledRevision / repositoryTwoProfiledRevision | exact Git commits, verified at source roots |
| databaseName | unique lowercase vulgata_story125_* only |
| databasePurpose | Story 12.5 Track C-MVP isolated release run |
| databaseOwner | non-secret operator/service identity |
| postgresVersion | observed PostgreSQL 17 identity |
| codeGraphVersionAndHash | accepted package/binary identity |
| javaVersion | Java 21 identity |
| jdtLsVersionAndHash | accepted JDT LS identity |
| providerApi / providerModel | non-secret configured identity |
| providerConfigurationIdentity | non-secret identifier/fingerprint; never key or endpoint credential |
| prerequisites | one row per prerequisite: ready or sanitized blocked |

Validation:

- database guard passes before any product connection factory, Vulgata process, migration or query;
- exactly two Repositories and two Profiled Revisions;
- no connection string, API key, token, password or absolute secret-bearing path;
- a blocked required prerequisite prevents dependent success claims.

## 4A. Preserved Track B compatibility vocabulary

Track C-MVP retains the accepted historical/current compatibility literals without recalculating them:

- received logical Provider Chunk cap: **16,384** bytes;
- assembled received provider response cap: **65,536** bytes;
- independent raw response-body safety cap: **1,048,576** bytes;
- independent SSE line/event safety cap: **131,072** bytes;
- closed observation field names: `transportOutcome`, `rejectionCategory`, and `validationOutcome`.

The Markdown report may quote those fields from accepted Track B content-dark observations. No Track C-MVP entity recomputes the counter domains, signs them or promotes them into a new release receipt.

## 5. TrackCMvpCommandObservation

One row per material operator command or application action.

| Field | Rule |
| --- | --- |
| sequence | positive increasing integer |
| purpose | bounded human description |
| sanitizedCommand | exact enough to reproduce, with secrets/connection details replaced |
| startedAt / finishedAt | ISO-8601 timestamps |
| exitResult | exit code or closed UI/API outcome |
| disposition | passed, failed, blocked or not-run |
| notes | sanitized bounded observation |

There is no hash graph, closure graph, supersession identity or signature. Rows remain ordered and truthful.

## 6. TrackCMvpQueryObservation

One bounded report table for each required read-only query.

| Field | Rule |
| --- | --- |
| queryId | stable descriptive identifier |
| purpose | exact release claim observed |
| sanitizedSqlOrDescription | no connection string or secret |
| observedAt | timestamp |
| exitResult | success/failure |
| resultRows | bounded identifiers, revisions, states and counts |
| thresholdResult | passed, failed, blocked or not-run |
| highValueSeedSelectedCount | nonnegative selected high-value-seed count from the existing Scan result |
| highValueSeedSucceededCount | nonnegative succeeded high-value-seed count |
| highValueSeedFailedCount | nonnegative failed high-value-seed count |

Required query identities:

1. prepared-manifests-and-file-code-units;
2. pass1-pass2-terminal-seed-and-failure-budget;
3. source-anchored-document-output;
4. exact-cross-repository-references-edge;
5. honest-uncertainty;
6. business-logic-documents-and-cross-repository-flow;
7. pre-restart-publication-counts;
8. post-restart-publication-counts.

The query observes existing authority. It does not infer an exact Edge from prose or a Flow Group from unrelated rows.

For a passed report, highValueSeedFailedCount is zero and highValueSeedSelectedCount equals highValueSeedSucceededCount. These report observations do not themselves add or alter selection behavior; the sole matched reviewed-profile selection exception is defined above.

## 7. TrackCMvpChatEvaluationRow

Exactly twelve observed rows selected from the accepted 12-20 case corpus.

| Field | Rule |
| --- | --- |
| caseId | existing stable corpus ID |
| mode | Business or IT |
| scope | selected System and exact two Repositories |
| question / followUpOf | copied from corpus; `followUpOf` is topical lineage only and carries no Conversation State |
| executionIsolation | `freshConversationState` for every real-run row |
| topicalLineage | copied corpus predecessor when present; never a server-state assertion |
| authorizationOracle | `authorized-scope` normally; `prompt-boundary-only` for `q12`; never `denied-access-exercised` without a concrete denied setup |
| tier1EvidenceReference | accepted test/evidence reference for same-mode multi-turn re-fetch/revalidation and actual authorization/currentness behavior |
| observedDisposition | accepted, refused, failed or blocked |
| useful | yes/no/not-applicable, scored against corpus rubric |
| acceptedMaterialClaims | nonnegative count |
| citedAcceptedMaterialClaims | equal to acceptedMaterialClaims for an accepted row |
| citationCoverage | 100% for accepted material technical claims |
| unauthorizedDisclosure | false for every row |
| streamingObserved | true for every accepted provider answer |
| completeRenderMilliseconds | 0-29,999 for every accepted answer |
| packetFingerprint | existing packet fingerprint when exposed; no recomputation |
| notes | bounded manual observation and limitation |

Aggregates:

- useful percentage uses the accepted corpus scoring rule and must be at least 80%;
- accepted material technical claim citation coverage is 100%;
- unauthorized disclosure count is zero;
- accepted non-streaming answer count is zero;
- every accepted complete render is under 30 seconds.

If the provider is unavailable, dependent rows are blocked/not-run and no metric is fabricated.

`q01` (Business), `q02` (IT) and `q03` (Business) form a topical sequence only. Each mode transition clears the accepted transient Conversation State, so these are three independent observations. `q12` uses the same authorized principal and exactly-two-Repositories scope; it proves only that an outside-scope request does not elicit confirmation or disclosure about that Repository. It does not prove an exercised denied principal, known protected Repository, revocation or true missing-equivalent result. Accepted Tier 1 tests remain the authority for those actual security/currentness and same-mode multi-turn behaviors.

## 8. TrackCMvpRestartObservation

| Field | Rule |
| --- | --- |
| databaseName | same owned database as preflight |
| processBefore / processAfter | non-secret Vulgata process observation |
| stopResult / startResult | closed result and timestamp |
| authenticatedAfterRestart | true for a passing report |
| persistedWikiAccessible | true for a passing report |
| groundedChatAccessible | a new authorized request succeeds or yields an honest product outcome |
| preCounts / postCounts | Document/Edge/Flow Group identities and counts |
| duplicatePublicationCount | zero restart-caused duplicates |

Transient conversation state is excluded from persistence expectations.

## 9. TrackCMvpScreenshotObservation

Exactly one or more row in each category:

- real Scan;
- grounded Business Chat;
- grounded IT Chat with Source Anchors;
- exact Cross-Repository result;
- persisted wiki/Chat access after restart.

Each row records a repository-relative screenshot path, capture time, category, real-production-state confirmation and manual redaction result. It contains no required hash, signature, automated candidate binding or image-analysis verdict.

Redaction rejects visible secrets, credentials, connection strings, absolute host paths, fake Backup B state and unrelated candidate output.

## 10. TrackCMvpReleaseReport

The report is the aggregate root for evidence presentation only.

| Field/section | Cardinality |
| --- | --- |
| disposition and candidate | exactly one |
| preflight | exactly one |
| commands | one or more |
| query observations | all required observations reached or honestly blocked |
| manual evaluation | twelve rows when provider available; otherwise explicit blocked rows/section |
| restart observation | one when prerequisites/run reach restart |
| screenshots | all five categories for passed |
| failures and limitations | always present, may say none |

State transitions:

planned -> running -> passed
                 -> failed
                 -> blocked

not-run is permitted only for work never started. Passed requires every retained release value. Failed/blocked reports retain completed truthful observations and never synthesize missing success values.

Failure-correction boundary: evidence-file, operator-command and read-only-query corrections may remain within the accepted run. Any production correction beyond the approved Prepared Scan initiation and reviewed exact-profile seed plans returns to correct-course and fresh review/scope-hash binding; it is not an alternate transition within this report aggregate.

## 11. Relationship overview

PreparedScanInitiationCommand
  <- PreparedPass1ContractIdentity (Core port, sole Agents authority)
  -> atomic 2 existing Scan Runs
  -> atomic 2 existing new-path Scan Executions
  -> atomic 1 existing Two-Repository Scan Coordination
  -> existing PreparedScanCoordinator/PreparedScanPairBuilder
  -> existing Prepared Scan Manifest publication

ReviewedExactProfileSeedRequest
  <- accepted initiated tuple + loaded Repository records
  -> IReviewedExactProfileSeedAuthority
  -> 0 required sources when NonMatching
  -> canonical matched required Repository/path/role set
  -> existing Prepared File Code Unit score/audit/Manifest fields
  -> existing Pass 1 high-value-seed outcome

TrackCMvpReleaseReport
  -> 1 TrackCMvpPreflight
  -> N TrackCMvpCommandObservation
  -> N TrackCMvpQueryObservation
  -> 12 TrackCMvpChatEvaluationRow when provider available
  -> 0..1 TrackCMvpRestartObservation
  -> N TrackCMvpScreenshotObservation

The report points to existing product identifiers for review; it does not own or mutate those identities.

## 12. Historical deferred model

ReleaseEvidenceRunEnvelope, signed stream attestation, immutable supersession/closure graph, rigid ten-check inventory and exhaustive artifact binding are post-competition concepts. The legacy story125-release-evidence.schema.json remains marked deferred for historical compatibility only and is not a Track C-MVP runtime or acceptance entity.

## 13. Migration disposition

No migration. No `VulgataDbContext`, entity configuration or `VulgataDbContextModelSnapshot` change. No new table or public request/response. The accepted Infrastructure initiation store remains unchanged; the new Infrastructure seed authority is read-only and does not use EF. A diff touching model/migration paths fails the Track C-MVP guard and returns to plan.
