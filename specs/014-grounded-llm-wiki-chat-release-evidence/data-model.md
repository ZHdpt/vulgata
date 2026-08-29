# Data Model: Story 12.5 Track C-MVP

Track C-MVP introduces no persistent application entity. The entities below are bounded evidence concepts represented in one Markdown report and adjacent screenshot files. Existing Track A/B and Story 12.4 domain/persistence authorities remain unchanged.

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

## 2. TrackCMvpPreflight

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

## 2A. Preserved Track B compatibility vocabulary

Track C-MVP retains the accepted historical/current compatibility literals without recalculating them:

- received logical Provider Chunk cap: **16,384** bytes;
- assembled received provider response cap: **65,536** bytes;
- independent raw response-body safety cap: **1,048,576** bytes;
- independent SSE line/event safety cap: **131,072** bytes;
- closed observation field names: `transportOutcome`, `rejectionCategory`, and `validationOutcome`.

The Markdown report may quote those fields from accepted Track B content-dark observations. No Track C-MVP entity recomputes the counter domains, signs them or promotes them into a new release receipt.

## 3. TrackCMvpCommandObservation

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

## 4. TrackCMvpQueryObservation

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

For a passed report, highValueSeedFailedCount is zero and highValueSeedSelectedCount equals highValueSeedSucceededCount. These are observations of existing Scan output; they do not add or alter seed selection behavior.

## 5. TrackCMvpChatEvaluationRow

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

## 6. TrackCMvpRestartObservation

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

## 7. TrackCMvpScreenshotObservation

Exactly one or more row in each category:

- real Scan;
- grounded Business Chat;
- grounded IT Chat with Source Anchors;
- exact Cross-Repository result;
- persisted wiki/Chat access after restart.

Each row records a repository-relative screenshot path, capture time, category, real-production-state confirmation and manual redaction result. It contains no required hash, signature, automated candidate binding or image-analysis verdict.

Redaction rejects visible secrets, credentials, connection strings, absolute host paths, fake Backup B state and unrelated candidate output.

## 8. TrackCMvpReleaseReport

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

Failure-correction boundary: evidence-file, operator-command and read-only-query corrections may remain within the approved run. Any correction requiring production source invalidates the no-production approval and returns to plan plus fresh review/scope-hash rebinding before TDD; it is not an alternate transition within this report aggregate.

## 9. Relationship overview

TrackCMvpReleaseReport
  -> 1 TrackCMvpPreflight
  -> N TrackCMvpCommandObservation
  -> N TrackCMvpQueryObservation
  -> 12 TrackCMvpChatEvaluationRow when provider available
  -> 0..1 TrackCMvpRestartObservation
  -> N TrackCMvpScreenshotObservation

The report points to existing product identifiers for review; it does not own or mutate those identities.

## 10. Historical deferred model

ReleaseEvidenceRunEnvelope, signed stream attestation, immutable supersession/closure graph, rigid ten-check inventory and exhaustive artifact binding are post-competition concepts. The legacy story125-release-evidence.schema.json remains marked deferred for historical compatibility only and is not a Track C-MVP runtime or acceptance entity.

## 11. Migration disposition

No migration. No VulgataDbContext or VulgataDbContextModelSnapshot change. No new table, entity configuration, repository or public API. A diff touching those paths fails the Track C-MVP guard and returns to plan.
