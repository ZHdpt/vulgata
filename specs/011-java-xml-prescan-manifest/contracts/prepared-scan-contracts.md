# Contract: Prepared Scan Planning, Atomic Commit, and Handoff

Prepared Scan Manifest, File Code Unit, and Scan Context Snapshot are authoritative glossary terms. Their semantics are not locally provisional.

## Pure preparation planner

Core accepts two immutable Repository preparation drafts. Each draft contains exact source inventory/audits, closed analysis proofs, safe classifications, candidate File Code Units, catalog/runtime/context identities, and no host paths or source content.

The planner must:

1. reject cross-Repository identity reuse, revision/snapshot/hash mismatch, unsafe/noncanonical paths, duplicate/case-colliding paths, nonexact ranges, and incomplete proofs;
2. compute direct score by the fixed 1000/900/800/700/400/100 table; direct score >=400 is a high-value candidate;
3. select as mandatory seeds only the first at most 20 high-value candidates per Repository by descending direct score, path, segment ordinal; use only those seeds for bounded expansion;
4. merge by descending final score, lowercase UUID `D` Repository text with ordinal comparison, path, segment ordinal;
5. include all mandatory seeds, fill to at most 150 from all nonmandatory candidates in pair order, fail below 50, and audit every cap exclusion;
6. assign pair-global `PairSelectionOrdinal 1..M`, then Repository-local `ManifestOrdinal 1..N` in FR-031 order;
7. compute every envelope, fingerprint, and deterministic UUID only through [canonical-envelope.md](canonical-envelope.md).

Completion/process order and timestamps cannot affect the result.

Canonical candidate/filter/skip/selected audit construction is a public Core seam. `PreparedScanAuditContractTests` must observe RED for the complete outcome/order/closed-reason, selected/nonselected nullable fingerprint/ordinal shapes, DetailFingerprint envelope/digest, deterministic UUID, and replay-mutation contract before the sole production owner `PreparedScanAuditFactory` is implemented. Enumerators provide closed inputs; the factory alone constructs audit identity. The factory's focused and surrounding-suite GREEN is a prerequisite of this planner, manifest construction, store/replay, coordinator/API, SC-001, and SC-008 qualification.

Any package containing an XML File Code Unit is invalid unless its ranges came through the completed safe-XML → XML-split chain in `safe-analysis-contracts.md`. Store/coordinator/API and qualification tasks are consumers, never parallel predecessors of that chain.

## `IPreparedScanStore`

`CommitPairAsync(authorizationScope, pairPackage, expectedCoordinationVersion, cancellationToken)` is the sole durable write. It returns `Committed`, `Replay`, `Conflict`, `Rejected`, `Cancelled`, or `Failed` plus bounded reason codes.

Infrastructure performs one transaction:

1. ownership-filter and lock the coordination and both Scan Runs in canonical Repository order;
2. revalidate exact Repository/Scan Run/Profiled Revision and expected versions;
3. if either manifest exists, independently reconstruct canonical-envelope bytes from both complete PostgreSQL packages—including typed proof/context fields and every Prepared Scan Audit tag 1-10 preimage column—and return `Replay` only when every recomputed identity byte, digest, UUID, scalar, both ordinal domains, selected association, and ordered child equals the request; otherwise return `Conflict`; stored digests are checked outputs, not reconstruction inputs;
4. insert both Scan Context Snapshots;
5. insert both Prepared Scan Manifests and ordered audits;
6. insert all deterministic pending File Code Units;
7. insert all retained classifications and complete totals;
8. insert both v2 Pre-Scan Results and approved prepared-handoff eligibility;
9. save once and commit once.

Every FK, uniqueness constraint, conditional shape, and fingerprint is checked before visibility. A catch/cancellation rolls back and clears tracked state. No package row is updated or deleted after commit.

The authoritative store qualification is `tests/Vulgata.Tests/PreparedScanning/PreparedScanAtomicPostgreSqlTests.cs` (`FullyQualifiedName~PreparedScanAtomicPostgreSqlTests`) against a uniquely named disposable PostgreSQL 17 database. It is separate from migration lifecycle tests and must inject failure before/after every insert group, cancellation at each durable boundary, stale expected versions, and concurrent identical/changed writers; it proves one commit plus replay/conflict and zero partial visibility. SQLite results cannot satisfy this contract.

Audit persistence uses a prepared-only `PreparedScanManifestEntry` principal with non-null manifest/FileCodeUnitId/fingerprint/pair-ordinal fields and a valid unique principal constraint over all four. `PreparedScanManifestEntryConfiguration` maps its PK/FKs/unique keys/checks. `PreparedScanAuditConfiguration` maps canonical fingerprint/ordinal columns plus nullable `PreparedScanManifestEntryId`; selected rows use the four-column composite FK, while nonselected rows use the exact null shapes. No nullable historical `CodeUnit` property is an alternate-key member. PostgreSQL schema/lifecycle tests prove migration creation/rollback/reapply and legacy null-row readability; store tests reconstruct/mutate every audit preimage and association independently.

## Replay identity

Included: exactly the artifact-tagged fields in `canonical-envelope.md`, including all schema identities; Repository/coordination/Scan Run/Profiled Revision; requested ref; snapshot/runtime/catalog identities and hashes; every stable typed CodeGraph/Java field and proof hash; Prepared Scan Context Repository/Scan Run/Profiled Revision/schema/content bindings plus `PreparedScanContextIdentityHash`; complete counts; every ordered audit/classification/manifest entry; scores/reasons; both ordinal domains; PairSelectionFingerprint; both ManifestFingerprints; and PairPackageFingerprint.

Excluded: exactly the volatile, derived, and nonidentity fields named in `canonical-envelope.md`, including legacy/time-derived ContextSnapshotId, CapturedAt, measured CodeGraph/Java duration, all other timestamps, progress/process/resource/retry telemetry, configured absolute paths, temporary workspace/lease tokens, raw diagnostics/status/LSP/proof JSON, source content, database-generated metadata, and every artifact ID/fingerprint from its own source envelope. File Code Unit identity also excludes both ordinals, scores, reason, and state under FR-033. Excluded values may be bounded first-commit audit metadata but never participate in equality or mutate during replay.

## Prepared handoff read

A new `IPreparedScanHandoffSource` uses an ownership-filtered query and returns a materialized immutable v2 read model only when manifest, result, context, classifications, audits, and File Code Units are complete and mutually consistent. It returns no partial/unapproved state. The existing `IApprovedPreScanHandoffSource` and historical v1 projection remain readable and do not project v2.

The v2 read model is not consumed by any runner in Story 12.2. It certifies preparation only and creates no `ScanJobExecution` or dispatch authority.

## Failure behavior

| Failure | Result | Durable prepared effect |
| --- | --- | --- |
| authorization/ownership miss | `Rejected/NotFound` without existence disclosure | none |
| exact source/ref/hash drift | `Rejected/SourceIdentityMismatch` | none |
| CodeGraph index/rebuild/status/runtime/identity/binding/trusted-path/completeness/readiness failure or timeout; JDT hard readiness failure | `Rejected/AnalysisNotReady` | none |
| XML/catalog/split hard failure | file Warning/skip or package rejection per bound | no partial file/package |
| post-complete-readiness related-file expansion timeout/failure only | deterministic Warning + direct-score/path order | package may approve |
| fewer than 50 / invalid high-value cap | `Rejected/SelectionBounds` | none |
| stale coordination/Scan Run | `Conflict` | none |
| exact acknowledged-loss retry | `Replay` | zero mutation |
| any changed/missing/reordered value | `Conflict` | zero mutation |
| persistence/cancellation before commit | `Failed`/`Cancelled` | complete rollback |
