# Data Model: Exact Cross-Repository Flow & Pass 2

All Story 12.4 persistence is additive or explicitly versioned compatibility work. Existing immutable Document Versions, Source Anchors, Document Edges, attached Flow Groups, legacy exact-resolution evidence/Uncertainties, Story 12.1 durable state, Story 12.2 prepared manifests, and Story 12.3 Pass 1 rows remain readable and retain their meaning.

## 1. Exact current communication projections

These are server-owned transient Core projections. They are canonical inputs, not entities and not model-authored data.

### `CurrentProviderEvidenceObservation`

One row-shaped transient observation represents one reproduced provider classification/locator occurrence. It contains protocol/raw+normalized endpoint, System/Repository/Scan Run/Profiled Revision/current provider XML Document Version, Source Anchor, classification, catalog rule, Evidence Locator, normalization rule, and an evidence fingerprint. It is never counted as a provider.

### `CurrentCommunicationCandidate`

| Field | Rules | Meaning |
| --- | --- | --- |
| `CandidateId`, `CandidateFingerprint` | UUIDv8 + SHA-256 canonical envelope | Consumer observation or aggregated provider replay/order identity |
| `Role` | `Consumer` or `Provider` | Closed evidence role |
| `Protocol` | `CustomRpc` or `Http` | Complete first component of match key |
| `RawEndpointIdentifier` | 1-512 UTF-8 bytes | Extracted current source value |
| `NormalizedEndpointIdentifier` | 1-512 UTF-8 bytes | Closed normalizer output |
| `NormalizationRuleIdentities` | 1-32 canonical safe identities | Every rule contributing to the normalized result |
| `SystemId`, `RepositoryId`, `ScanRunId`, `ScanExecutionId` | nonempty; pair-bound | Current ownership |
| `ProfiledRevision` | full lowercase Git object ID | Exact source revision |
| `DocumentVersionId`, `LogicalDocumentId` | current Code Logic Document | Evidence owner |
| `SourceAnchorIds` | 1-32 canonical UUIDs | Complete deduplicated current source binding |
| `CommunicationClassificationIds` | 1-32 canonical UUIDs | Complete deduplicated Confirmed kind evidence |
| `ConsumerEntryIdentity` | required for Consumer | Pair/current-version source-anchored identity |
| `ConsumerEntryStableKey` | required for Consumer; lower SHA-256 | Cross-version semantic locator key |
| `ProviderIdentity` | Provider only; UUIDv8/SHA-256 envelope | System + Repository + Scan Run + Profiled Revision + current provider XML Document Version |
| `ContributingProviderEvidence` | Provider only; 1-32 canonical observation fingerprints plus locators/rules/anchors/classifications | Complete aggregated evidence for this provider/key |
| `EvidenceFingerprint` | SHA-256 | Complete consumer evidence or complete aggregated provider-evidence preimage |

For one protocol/key, all valid provider observations with the same `ProviderIdentity` collapse into exactly one Provider candidate before cardinality. Exact duplicate occurrences collapse, while every distinct contributing occurrence remains in the sorted aggregate and fingerprint. The candidate source returns all aggregated candidates in strict canonical order and returns a typed integrity failure rather than omitting a trusted-row contradiction.

### `ExactResolutionDecisionV2`

| Field | Rules |
| --- | --- |
| `DecisionId`, `DecisionFingerprint` | deterministic from pair + consumer candidate + protocol/endpoint + outcome/provider set |
| `Kind` | `ExactMatch`, `Uncertainty` |
| `ReasonCode` | `unique_provider`, `no_provider`, `multiple_providers`, `protocol_mismatch`, `role_conflict`, `stale_evidence`, `unsupported_kind`, `unsafe_normalization` |
| `Consumer` | exactly one current Consumer |
| `Provider` | exactly one aggregated distinct provider identity only for `ExactMatch` |
| `Protocol`, `NormalizedEndpointIdentifier` | exact complete key |
| `DocumentEdgeId` | nonempty only for `ExactMatch`; canonical source/target/References identity and reusable by other decisions |

No decision contains graph distance, score, preferred Repository, LLM text, or raw CodeGraph identity. `multiple_providers` means multiple distinct eligible current provider Document Versions remain after aggregation; evidence multiplicity for one provider cannot produce that outcome.

## 2. Durable resolution outcome and evidence

### Legacy `ExactCrossRepositoryEdgeEvidence`

The existing entity/table remains unchanged as the one-to-one profile-v1 evidence extension of a REFERENCES Edge. Its consumer/provider Document, Repository, Scan Run, Source Anchor, endpoint, protocol, locator, profile, confidence, resolution-kind, fingerprint and timestamp fields retain their current semantics. It receives no v2 rows or inferred backfill because one legacy row cannot safely represent endpoint/consumer-entry multiplicity.

### `CrossRepositoryResolutionDecision`

One durable v2 row owns every consumer-candidate outcome independently of graph Edge cardinality.

| Field | Rules |
| --- | --- |
| `Id` | UUIDv8 from resolution schema + pair + consumer candidate + outcome/provider identity |
| `ResolutionSchemaIdentity` | exact `exact-cross-repository-resolution/2.0` |
| `Pass1PairExecutionId`, `CoordinationId`, `SystemId` | required exact current-pair FKs |
| `ConsumerCandidateId`, `ConsumerEntryIdentity`, `ConsumerEntryStableKey` | exact candidate/entry bindings |
| `ConsumerDocumentVersionId`, `ConsumerRepositoryId`, `ConsumerScanRunId`, `ConsumerProfiledRevision` | exact current consumer owner |
| `Protocol`, `NormalizedEndpointIdentifier`, `NormalizationRuleIdentity` | complete deterministic key/rule |
| `OutcomeKind`, `ReasonCode` | `ExactMatch/unique_provider` or `Uncertainty/<closed reason>` |
| `ProviderIdentity`, `ProviderDocumentVersionId`, `ProviderRepositoryId`, `ProviderScanRunId`, `ProviderProfiledRevision` | required only for ExactMatch |
| `DocumentEdgeId` | required only for ExactMatch; FK many decisions to one REFERENCES Edge |
| `UncertaintyId` | required only for Uncertainty; unique FK one decision to one Uncertainty |
| `ResolvedUncertaintyId` | optional only for ExactMatch; distinct prior Uncertainty owned by an older decision identity |
| `ConsumerEvidenceFingerprint`, `ProviderEvidenceSetFingerprint`, `DecisionFingerprint` | complete lower SHA-256 values |
| `CreatedAt` | UTC; immutable |

Shape checks require exactly one outcome owner: ExactMatch has one Provider and `DocumentEdgeId`, no `UncertaintyId`; Uncertainty has one `UncertaintyId`, no `DocumentEdgeId`/selected Provider. Unique decision slot `(Pass1PairExecutionId, ConsumerCandidateId, Protocol, NormalizedEndpointIdentifier)` prevents two outcomes for the same pair/current consumer observation. `DecisionFingerprint`, non-null `UncertaintyId`, and non-null `ResolvedUncertaintyId` are independently unique. A changed provider/evidence/outcome for an occupied slot is conflict, not a second decision.

The document Edge semantic identity is only `(SourceDocumentVersionId, TargetDocumentVersionId, References)` under the existing unique tuple index. If no row exists, its ID is UUIDv8 over that tuple. If a valid existing/legacy row already owns the tuple with a different ID, `DocumentEdgeId` reuses that actual row after complete source/target/Repository/Scan Run/type validation. Pair, consumer entry, protocol, endpoint and decision are excluded from Edge identity. Consequently any number of distinct exact decisions may reference the same Edge, while every decision retains independent evidence and Flow Group eligibility.

### `ExactResolutionProviderEvidenceContribution`

One normalized durable row preserves each distinct provider observation in the aggregate used by an exact or ambiguous v2 decision.

| Field | Rules |
| --- | --- |
| `Id` | UUIDv8 from decision owner + ProviderIdentity + observation fingerprint |
| `ResolutionDecisionId` | required owner FK; every contribution belongs to exactly one v2 decision |
| `ProviderIdentity` | complete current provider owner UUIDv8 |
| `ProviderDocumentVersionId`, `RepositoryId`, `ScanRunId`, `ProfiledRevision` | exact current provider binding |
| `ProviderOrdinal`, `EvidenceOrdinal` | contiguous canonical ordinals |
| `CommunicationClassificationId`, `SourceAnchorId` | exact contributing evidence FKs |
| `ProviderRawIdentifier`, `NormalizedEndpointIdentifier` | reproduced bounded provider observation identity used by canonical sorting, hashing and replay |
| `CatalogRuleIdentity`, `EvidenceLocator`, `NormalizationRuleIdentity` | bounded reproduced safe-catalog evidence |
| `EvidenceFingerprint` | lower SHA-256 over the complete observation |

Exact duplicate observations collapse before persistence. Unique decision/provider/evidence fingerprint and decision/ordinal indexes make the full set reconstructable. An exact-match decision owns every contribution for its one distinct Provider identity. `multiple_providers`/role-conflict decisions own every contribution for every remaining distinct identity even though they select none. Zero-provider/unsupported pre-provider outcomes own none. Decisions sharing one Document Edge never share contribution ownership. A digest without the exact complete child-row set never qualifies as replay.

## 3. `Uncertainty` compatibility extension

Existing rows retain status/sub-status/profile fields. Story 12.4 adds:

| Field | Type/rules |
| --- | --- |
| `ResolutionSchemaIdentity` | nullable legacy; v2 constant for current pair |
| `Pass1PairExecutionId`, `CoordinationId`, `SystemId` | nullable legacy; required v2 bindings |
| `Protocol` | nullable only where unsupported kind has no accepted protocol; otherwise `CustomRpc`/`Http` |
| `ConsumerCandidateId` | required for v2 |
| `ConsumerEvidenceFingerprint` | lower SHA-256 for v2 |
| `NormalizationRuleIdentity` | nullable only for unsupported/unsafe pre-normalization outcome |
| `DecisionFingerprint` | lower SHA-256, unique |
| `ProviderEvidenceSetFingerprint` | null when no eligible Provider observation exists; otherwise complete aggregate lower SHA-256 |

Every v2 Uncertainty is referenced by exactly one `CrossRepositoryResolutionDecision.UncertaintyId`; it is not the decision identity itself. The duplicated bounded identification fields support existing Uncertainty views but must equal their owning decision.

Closed v2 reason codes add `no_provider`, `multiple_providers`, `protocol_mismatch`, `role_conflict`, `stale_evidence`, `unsupported_kind`, and `unsafe_normalization`. Existing reason codes remain valid only for legacy shape.

### State transition

```text
unresolved
  -> resolved (only a later exact decision with matching System, consumer stable entry, protocol and endpoint links it through ResolvedUncertaintyId)
  -> wontfix (existing human workflow, not Story 12.4 automation)
```

Rows and their owning unresolved decisions are never deleted or rewritten. `resolved` requires `ResolvedAt` and `ResolvedByDocumentVersionId`; v2 additionally requires one newer exact decision/Edge whose `ResolvedUncertaintyId` links this row and whose current evidence passes full revalidation. The old decision stays `Uncertainty`. Exact replay performs no update.

## 4. `Pass2Execution`

One row per terminal Story 12.3 pair.

| Field | Rules |
| --- | --- |
| `Id` | UUIDv8 from pair execution + schema |
| `Pass1PairExecutionId`, `CoordinationId`, `SystemId` | nonempty unique FKs; exact pair |
| `ManifestOneId`, `ManifestTwoId` | distinct, canonical order |
| `RepositoryOneId`, `RepositoryTwoId` | distinct, same System, canonical order |
| `ProfiledRevisionOne`, `ProfiledRevisionTwo` | exact full commits |
| `ResolutionSchemaIdentity` | `exact-cross-repository-resolution/2.0` |
| `FlowPlannerSchemaIdentity` | `endpoint-flow-group/1.0` |
| `LogicalKeySchemaIdentity` | `business-flow-logical-key/1.0` |
| `WorkerPromptIdentity` | exact embedded resource identity/hash |
| `SubmissionSchemaIdentity` | `business-logic/flow-v1` |
| `ToolDeclarationFingerprint` | lower SHA-256 |
| `ResolutionFingerprint`, `PlanningFingerprint`, `ExecutionFingerprint` | lower SHA-256; exact replay |
| `CandidateCount` | 0+; exact terminal resolution decisions |
| `FlowGroupCount` | 0-10 |
| `CompleteGroupCount`, `IncompleteGroupCount`, `BoundExcludedGroupCount` | nonnegative; sum = FlowGroupCount |
| `OmittedCandidateCount` | nonnegative |
| `PublishedCount`, `FailedCount` | nonnegative; <= CompleteGroupCount |
| `Phase` | closed state below |
| `CircuitState`, probe fields | same bounds/schedule semantics as Story 12.3 |
| `StateVersion` | nonnegative concurrency token |
| timestamps | UTC; monotonic |

### Phase transitions

```text
Planning
  -> Running
  -> ProbingProvider -> Running
  -> PausedProviderUnavailable -> Running (manual resume)
  -> PausedOperatorCorrection -> Running (manual resume)
  -> Completing
  -> Completed | CompletedWithWarnings
  -> FailedIntegrity | FailedSecurity | Cancelled
```

Start inserts the execution, resolution decisions, Flow Groups/traces/omissions, complete jobs and budgets in one transaction. `Running` cannot be visible with an incomplete job set.

## 5. `FlowGroup` compatibility extension

Existing `FlowGroup`, `FlowTrace`, and `FlowTraceStep` become the durable endpoint-centered plan and later published graph.

### Existing fields

`Id`, consumer-owned `RepositoryId`/`ScanRunId`, `GroupOrder`, `Title`, `CreatedAt` remain. `BusinessLogicDocumentVersionId` becomes nullable for v2 pre-publication; legacy rows remain non-null.

### Additive v2 fields

| Field | Rules |
| --- | --- |
| `SchemaIdentity` | nullable legacy; `endpoint-flow-group/1.0` for v2 |
| `Pass2ExecutionId` | nullable legacy; nonempty FK for v2 |
| `SystemId` | required v2 |
| `Protocol` | `CustomRpc` or `Http` |
| `NormalizedEndpointIdentifier` | 1-512 UTF-8 bytes |
| `ConsumerEntryIdentity` | bounded pair/current-version canonical identity |
| `ConsumerEntryStableKey` | lower SHA-256 from cross-version semantic locator |
| `LogicalKeySchemaIdentity` | exact `business-flow-logical-key/1.0` |
| `LogicalDocumentStableKey` | exact `business-flow/v1/<lower-sha256>`; excludes execution/current evidence identity |
| `ConsumerDocumentVersionId` | current member |
| `ProviderRepositoryId` | differs from consumer Repository |
| `ProviderXmlDocumentVersionId` | current exact Edge target |
| `ProviderImplementationDocumentVersionId` | current unique same-Repository target |
| `ExactResolutionDecisionId` | current v2 ExactMatch decision; unique among v2 Flow Groups |
| `ExactResolutionEdgeId` | current cross-Repository REFERENCES Edge; deliberately nonunique across groups |
| `State` | closed state below |
| `ReasonCode` | nullable only for complete/running/published; closed code otherwise |
| `PlanFingerprint`, `MembershipFingerprint` | lower SHA-256 |
| `PublishedAt`, `FailedAt` | state-shaped UTC timestamps |

### Two identity domains

`FlowGroup.Id` is execution-specific and includes Pass 1 pair execution, current consumer-entry identity, exact resolution decision, shared-or-new Edge/current provider Document and planner schema. Two decisions sharing one Edge retain different Flow Groups. `LogicalDocumentStableKey` has this canonical preimage:

```text
business-flow-logical-key/1.0
SystemId
consumer RepositoryId
provider RepositoryId
protocol
normalized endpoint identifier
ConsumerEntryStableKey
```

`ConsumerEntryStableKey` is derived from consumer Repository, normalized repository-relative path, closed source-derived containing qualified type/member signature or catalog-declared stable locator, closed communication-site kind, and structural occurrence ordinal among supported outbound sites within that owner. It excludes protocol/endpoint target value, pair, Scan Run, Profiled Revision, Document Version, Source Anchor coordinates/hash, exact Edge, evidence order and planner identity.

The key stays stable across current Code Logic Document/anchor revisions when those source semantics remain. Endpoint/protocol/Repository/System or semantic entry changes produce a different key. A locator/canonicalization contract change requires a new logical-key schema identity and cannot reuse an old chain without an explicit reviewed alias/data migration.

### State transitions

```text
Complete -> Running -> Published
                    -> Failed
Incomplete          (terminal, no job)
BoundExcluded       (terminal, no job)
```

`Published` requires a Business Logic Document Version; all other v2 states require null except exact replay of the publication transition. State, membership, execution-instance identity, consumer stable entry and logical stable key do not change after publication.

### Flow Trace shape

Exactly one v2 Flow Trace per Flow Group, with one to sixteen ordered steps. Step kinds reuse `Document`, `Relationship`, and `Termination`:

- Document steps identify canonical member Document Versions.
- Relationship steps bind both the group's exact resolution decision and its shared-or-new Cross-Repository Edge through an additive nullable `ExactResolutionDecisionId` FK, or identify a validated same-Repository REFERENCES Edge with that FK null.
- Termination records the complete/incomplete/bound reason without source content.

Every decision/Document/Edge FK is current and pair-bound at planning; publication revalidates the exact decision evidence rather than treating any other decision on the same Edge as equivalent.

## 6. `Pass2Omission`

One row per canonical group candidate after the ten-Flow-Group release limit.

| Field | Rules |
| --- | --- |
| `Id` | deterministic from Pass2 execution + exact resolution decision fingerprint |
| `Pass2ExecutionId`, `ExactResolutionDecisionId` | unique pair; decision remains distinct even when Edge is shared |
| `DecisionFingerprint` | lower SHA-256 |
| `Protocol`, `NormalizedEndpointIdentifierFingerprint` | protocol plus content-dark endpoint hash |
| `ConsumerEntryIdentity` | bounded |
| `ProviderRepositoryId` | nonempty |
| `ReasonCode` | exact `flow-group-bound` |
| `CreatedAt` | UTC |

Raw endpoint text is omitted from the warning row; the decision evidence remains available through authorized graph reads.

## 7. `Pass2JobExecution`

Exactly one row per complete Flow Group.

| Field | Rules |
| --- | --- |
| `Id` | UUIDv8 from Pass2 execution + Flow Group |
| `Pass2ExecutionId`, `FlowGroupId` | nonempty; each unique; same execution |
| `GroupOrder` | 1-10; unique within execution |
| `State` | reuse `Pending`, `Running`, `RetryScheduled`, `Completed`, `Failed`, `Cancelled` semantics |
| `AttemptCount` | 0-2 |
| `StateVersion` | nonnegative concurrency token |
| `LeaseToken`, `LeaseExpiresAt` | present only Running; 30-minute bounded lease |
| `NextAttemptAt` | present only RetryScheduled |
| `FailureCategory`, `FailureCode` | both null or both present; existing closed categories |
| `CommittedDocumentVersionId` | present only Completed; immutable Business Logic version |
| `CompletionFingerprint` | present only Completed; lower SHA-256 |
| `StateChangedAt` | UTC monotonic |

Only one job in one Pass2 execution is Running at a time for this release. An exact Completed replay is checked before current-lease checks and performs zero writes.

## 8. `Pass2RetrievalBudget`

Exactly one row per Pass 2 Job.

| Field | Rules |
| --- | --- |
| `Pass2JobExecutionId` | PK/FK |
| `CallsReserved` | 0-8 |
| `ResultsReturned` | 0-10 |
| `AdditionalDocumentCount` | 0-4 distinct Documents outside initial group |
| `ReturnedUtf8Bytes` | 0-65,536 |
| `StateVersion` | nonnegative concurrency token |
| `UpdatedAt` | UTC monotonic |

Four-sections-per-Document is reconstructed from admitted `Pass2RetrievalResult` rows and constrained during admission. Budget does not reset across provider retry, process restart, or acknowledgement loss.

## 9. `Pass2RetrievalReceipt`

One content-dark durable record per reserved call.

| Field | Rules |
| --- | --- |
| `Id` | deterministic from job + call ordinal |
| `Pass2JobExecutionId`, `AttemptOrdinal`, `CallOrdinal` | call ordinal 1-8, unique per job; attempt 1-2 |
| `ToolCallIdFingerprint`, `RequestFingerprint` | lower SHA-256; no raw argument/tool ID |
| `Operation` | `Search`, `Sections`, `SourceAnchors`, `SourceExcerpt`, `RelatedDocuments` |
| `Outcome` | `Accepted`, `NoResults`, `BoundExceeded`, `Refused`, `Unavailable` |
| `ResultCount`, `ReturnedUtf8Bytes`, `NewAdditionalDocumentCount` | nonnegative deltas; zero unless Accepted |
| `ResultSetFingerprint` | lower SHA-256 for accepted/no-results; no content |
| `CreatedAt` | UTC |

Same call ordinal/request fingerprint replays; different request for an existing ordinal conflicts. A crash after reservation may leave no receipt; the call stays consumed and cannot be reused.

## 10. `Pass2RetrievalResult`

One content-dark descriptor per admitted result.

| Field | Rules |
| --- | --- |
| `Id` | deterministic opaque handle UUID |
| `ReceiptId`, `ResultOrdinal` | contiguous; unique |
| `Pass2JobExecutionId`, `AttemptOrdinal` | exact owner |
| `Kind` | `Document`, `Section`, `SourceAnchor`, `SourceExcerpt`, `Relationship` |
| `DocumentVersionId`, `RepositoryId`, `ScanRunId` | current authorized Code Logic Document |
| `SectionId`, `SourceAnchorId`, `EdgeId` | nullable according to kind |
| `ByteStart`, `ByteEnd` | only SourceExcerpt; contained by anchor |
| `DescriptorFingerprint`, `EvidenceFingerprint` | lower SHA-256 |
| `ReturnedUtf8Bytes` | exact serialized provider-visible result bytes |
| `CreatedAt` | UTC |

No source text, section text, provider response, query, path, endpoint, secret, or absolute host path is persisted. A result handle is valid only for its job and attempt; commit rehydrates and revalidates content/currentness from the descriptor.

## 11. Transient provider/session projections

| Projection | Closed meaning |
| --- | --- |
| `PreparedPass2WorkerPacket` | exact decision/shared-Edge/group/pair binding plus consumer stable-entry/logical stable-key fingerprints, initial evidence handles, current prompt/tool/schema fingerprints, lease/state authorization, <=131,072 UTF-8 bytes |
| `Pass2InitialEvidenceHandle` | deterministic handle for one current group Document/section/anchor/Edge fact; rederived from group plan |
| `PreparedPass2SubmissionSession` | 0-2 submissions; only complete local parse/semantic success captures <=65,536 bytes |
| `PreparedPass2FunctionLoopState` | provider turns 1-10; calls remaining from durable budget; forced submit/correction rules |
| `Pass2EvidenceUseLedger` | current-attempt admitted initial/retrieved handles and their exact backing Document Versions |
| `Pass2ValidationResult` | success or closed `Shape`, `Binding`, `Evidence`, `Semantics`, `Provenance` rejection |

These projections never persist content and never become provider/provider-selection authority.

## 12. Business Logic Document publication

Reuse existing `LogicalDocument`, `DocumentVersion`, `DocumentSection`, `IndexEntry`, `DocumentEdge`, `FlowGroup`, `FlowTrace`, and `FlowTraceStep`.

### Identity and ownership

- Flow Group ID: pair/current-evidence-specific execution instance; never used as the Logical Document stable key.
- Logical Document stable key: the Flow Group's exact `business-flow/v1/<digest>` over the `business-flow-logical-key/1.0` semantic preimage; consumer Repository owner.
- Document Version ID: deterministic job completion identity.
- Schema: existing `vulgata.knowledge-document/1.0` canonical storage, authored through the `business-logic/flow-v1` submission contract.
- A later pair completion with the same semantic key resolves the existing unique `(RepositoryId, BusinessLogic, StableKey)` Logical Document, appends a successor and moves its current pointer; prior version remains preserved.
- A changed endpoint or other stable-key component resolves a different Logical Document and leaves the prior chain/current pointer unchanged.
- `business-flow-logical-key/1.0` remains independent of execution/planner/resolution schemas; a future semantic-key schema cannot silently adopt an old chain.

### Provenance

For each accepted section/main step, cited handles map to exact used Code Logic Document Versions. The union becomes:

1. each section's `DocumentVersionIdsJson` authorized set;
2. one distinct GENERATED_FROM Edge from the Business Logic Document Version to each used Code Logic Document Version;
3. no Edge for grouped, eligible or returned-but-uncited Documents.

Provider source is traced through its Code Logic Document's Source Anchors. Business Logic source anchors, if any, may only belong to the consumer Repository/Scan Run and cannot copy provider ownership.

### Atomic transaction

One transaction first revalidates the Flow Group's exact decision, its independent contribution set and its source/target/type Edge; another decision sharing the Edge cannot substitute. It then acquires a lock scoped to consumer Repository/document type/stable key, resolves or creates the unique Logical Document, locks and revalidates its concurrency-token `CurrentVersionId`, and writes or replays:

1. Logical Document/current Version Chain transition, including the exact predecessor `SupersededById` and current pointer;
2. immutable Document Version, sections and index;
3. exact GENERATED_FROM set;
4. attachment/publication of the existing Flow Group and its already-persisted trace;
5. Pass 2 Job Completed state and completion fingerprint;
6. Pass 2 Execution published counter/state version.

Any mismatch rolls back the entire transaction. Concurrent creation cannot produce two logical slots because the existing Repository/type/stable-key unique index is authoritative. Concurrent cross-pair publication serializes; a loser reloads/revalidates the winner's current pointer before appending. Exact same-completion replay finds the existing linked version and writes nothing.

## 13. Relationships

```text
Pass1PairExecution 1 -- 1 Pass2Execution
TwoRepositoryScanCoordination 1 -- 1 Pass2Execution
legacy ExactCrossRepositoryEdgeEvidence 1 -- 1 DocumentEdge
DocumentEdge 1 -- 0..N CrossRepositoryResolutionDecision (ExactMatch decisions only)
CrossRepositoryResolutionDecision 1 -- 0..1 Uncertainty (Uncertainty outcome only; Uncertainty owned once)
CrossRepositoryResolutionDecision 1 -- 0..N ExactResolutionProviderEvidenceContribution
Pass2Execution 1 -- 0..10 FlowGroup
Pass2Execution 1 -- 0..N Pass2Omission
FlowGroup 1 -- 1 FlowTrace -- 1..16 FlowTraceStep
FlowGroup 1 -- 1 CrossRepositoryResolutionDecision (v2; decision unique per group)
FlowGroup 1 -- 0..1 Pass2JobExecution
Pass2JobExecution 1 -- 1 Pass2RetrievalBudget
Pass2JobExecution 1 -- 0..8 Pass2RetrievalReceipt
Pass2RetrievalReceipt 1 -- 0..10 Pass2RetrievalResult (job-wide total <=10)
FlowGroup 0..1 -- 1 Business Logic DocumentVersion after publication
consumer Repository + BusinessLogic + LogicalDocumentStableKey 1 -- 1 LogicalDocument -- 1..N immutable DocumentVersion
Business Logic DocumentVersion N -- N Code Logic DocumentVersion via GENERATED_FROM DocumentEdge
Consumer Code Logic DocumentVersion N -- N Provider XML Code Logic DocumentVersion via REFERENCES DocumentEdge
```

## 14. Canonical ordering

- UUID ordering uses RFC 4122 big-endian bytes, not .NET mixed-endian `Guid.CompareTo`.
- Text ordering uses normalized strict UTF-8 ordinal bytes.
- Resolution decisions: consumer candidate fingerprint.
- Provider aggregation: protocol/key, ProviderIdentity, then complete contributing-evidence fingerprint set.
- Flow Groups: protocol, normalized endpoint bytes, consumer Repository/Document/entry, provider Repository.
- Members/steps: required role rank, Repository UUID, Document Version UUID, Edge UUID.
- Retrieval results: operation-specific exact score/key, then Document Version/section/anchor UUID.
- GENERATED_FROM targets: Document Version UUID bytes.

Every fingerprint includes schema identity and complete ordered preimages; stored fingerprints are checked outputs, never sole replay proof.

## 15. Migration details

One cohesive migration will:

1. make `FlowGroups.BusinessLogicDocumentVersionId` nullable and add v2 exact-decision FK, execution-instance, consumer-stable-entry, logical-key-schema, logical-stable-key, plan/state columns and indexes; add the cross-Repository `FlowTraceStep.ExactResolutionDecisionId` binding; make decision unique per v2 Flow Group while allowing repeated Edge FK values;
2. leave `DocumentEdges` source/target/type uniqueness and legacy `ExactCrossRepositoryEdgeEvidence` schema/one-to-one rows unchanged; perform no endpoint/entry v2 backfill;
3. create `CrossRepositoryResolutionDecisions` with outcome-shape checks, unique pair/candidate/protocol/endpoint slot, unique decision fingerprint, many-to-one optional Edge FK, one-to-one optional Uncertainty FK and optional resolved-Uncertainty link; create `ExactResolutionProviderEvidenceContributions` with required decision owner and canonical uniqueness/order indexes;
4. create `Pass2Executions`, `Pass2Omissions`, `Pass2JobExecutions`, `Pass2RetrievalBudgets`, `Pass2RetrievalReceipts`, and `Pass2RetrievalResults` with Restrict/NoAction FKs;
5. add unique indexes for decision slot/fingerprint, provider aggregate identity/evidence fingerprint where persisted, execution/pair, group decision/order/fingerprint, one job/group, call ordinal, result handle and completed Document Version; retain the existing unique Document Edge tuple and Logical Document Repository/type/stable-key indexes plus current-pointer concurrency token;
6. update designer/model snapshot.

`Down` drops new tables/columns and restores the prior non-null Flow Group FK only when no v2 pre-publication row exists. Migration qualification uses fresh and legacy fixtures and proves no pending model changes.

## 16. Compatibility invariants

- Legacy profile v1 exact resolver/store and prepared-demo Pass 2 rows remain readable and are not production candidates.
- Story 12.3 Documents/ContentJson/anchors/edges are read-only inputs.
- No Code Logic Document is rewritten, superseded or deleted by Story 12.4.
- A Cross-Repository relationship remains an ordinary REFERENCES Edge.
- Multiple v2 exact decisions and Flow Groups may reuse one document-level REFERENCES Edge without sharing endpoint/entry decision evidence.
- Legacy `ExactCrossRepositoryEdgeEvidence` remains one-to-one and receives no inferred v2 rows or backfill.
- Existing Business Logic Documents and attached Flow Groups retain their current IDs/relationships.
- A later v2 Flow Group may have a different execution identity while appending to the same consumer-owned Logical Document stable key; no legacy row is rekeyed.
- No source/provider payload or secret is introduced into durable operational state.
