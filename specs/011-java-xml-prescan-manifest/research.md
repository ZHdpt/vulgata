# Phase 0 Research: Lightweight Java/XML PreScan & Prepared Scan Manifest

**Pinned correction boundary:** source commit `6fe7d4a88ff344b1d71329d19fdb9d6d6d64b7e6`; specification SHA-256 `c15d03b2b10e4eaa452a59e3b96acafdbb04160de556ca93c28380117a085ad3`; checklist SHA-256 `070affb70472c8d264cdf06ec5e06412e0ef06119bb7be388ce385351a5d8150`; accepted fifth-correction decision `story-12-2-definition-review-fifth-correction-authorization-01`; raw definition review 4 verdict `REVISE`.

All design choices are resolved. Real-tool qualification may discover a closed-operation capability mismatch; the defined outcome is a bounded Warning plus deterministic direct-order fallback, not a design clarification or permission to expose a broader tool.

## R-1: Exact source acquisition and lifetime

**Decision:** Extend the Story 12.1 exact-revision acquisition seam to accept only canonical full `refs/heads/...` or `refs/tags/...` references plus an exact 40-character lowercase commit. Acquire each Repository into a private contained snapshot, hold both leases until the paired prepared-state transaction finishes, and verify origin, ref-to-commit binding, archive inventory, paths, and bytes before enumeration, after analysis, and immediately before commit.

**Rationale:** The current service already proves private archive materialization and ordinary-checkout isolation, but is tag-only. A small closed full-ref grammar satisfies FR-001/FR-002 without accepting pseudorefs, abbreviated objects, refspecs, options, lock refs, or arbitrary Git expressions.

**Alternatives considered:** Trusting the ordinary checkout violates the exact-revision invariant; accepting arbitrary revision syntax permits ambiguous or unsafe Git interpretation; persisting an absolute snapshot path leaks machine state and becomes stale after cleanup.

## R-2: Deterministic inventory and exact hashes

**Decision:** Add a Story 12.2 enumerator rather than changing the historical broad enumerator in place. It walks without following reparse points, canonicalizes every Repository-relative path before extension/filter decisions, fails the Repository on ordinal-ignore-case collisions, records one closed audit outcome per supported or rejected candidate, and reads each accepted file once into a bounded raw-byte artifact. `FileHash` and segment `SourceHash` are lowercase SHA-256 over exact raw bytes.

**Rationale:** The current `ScanCandidateEnumerator` filters different segments and reads prefixes; the current `FileIdentityHash` is a rename aid, not exact file identity. A separate versioned path preserves historical behavior while making every Story 12.2 decision replayable.

**Alternatives considered:** Mutating the v1 enumerator would change historical scans; content-based language guessing violates the `.java`/`.xml` contract; Unicode or newline normalization would destroy exact source identity.

## R-3: Revision-bound CodeGraph workspace

**Decision:** Copy the verified source inventory into a contained, revision-bound CodeGraph analysis workspace, bind it to the immutable snapshot token, and permit CodeGraph to own only that workspace's `.codegraph` child. Before and after index/query operations, verify that every non-`.codegraph` file equals the immutable source inventory and no other path appeared. Dispose the analysis workspace after the atomic commit; the manifest retains typed proof, not CodeGraph private state.

**Rationale:** CodeGraph writes `.codegraph` beneath its project root, which would invalidate the immutable Git snapshot. A separate verified workspace preserves exact source authority and keeps private index storage rebuildable.

**Alternatives considered:** Ignoring `.codegraph` mutations in the Git snapshot weakens identity; persisting the private database couples domain state to CodeGraph; indexing the ordinary checkout reintroduces revision drift.

## R-4: Complete CodeGraph proof and bounded expansion

**Decision:** Enrich the closed status result with every FR-011 predicate and add one PreScan-only related-file request that accepts a bounded seed set, the fixed `Calls|Imports|Inherits` allowlist, maximum two hops, 200 unique eligible files, and a five-second deadline. Persist the stable typed predicates/bindings and hash only their versioned CodeGraph proof envelope; retain measured duration separately as bounded first-commit audit metadata. Index/rebuild, status, runtime, identity, Repository/Profiled Revision binding, trusted-path, completeness, and readiness failures—including their timeouts—reject approval. Only the post-readiness related-file expansion may record a Warning and return direct-score/path order.

**Rationale:** The existing adapter parses most status fields but collapses them to `ready/stale/count`. The ordering contract needs typed, auditable predicates and relationship provenance without exposing raw queries or private storage.

**Alternatives considered:** Treating `ready=true` as complete loses required evidence; exposing arbitrary query text violates the closed-port security boundary; applying fallback to a hard-readiness failure contradicts AC-2; failing the whole PreScan solely on post-readiness expansion contradicts corrected FR-031.

## R-5: JDT-LS readiness and PreScan-only Java analysis

**Decision:** Extend the existing isolated JDT-LS session with one typed readiness result that proves runtime/workspace identity, initialize/initialized, usable import state, and nonempty document symbols for the deterministic readiness file within 60 seconds. Persist and hash only the stable versioned Java-readiness envelope; retain duration and volatile diagnostic/process observations outside identity. XML-only uses the envelope's explicit `NotApplicable` null/default shape. Reuse closed document-symbol and implementation operations for exact Java split evidence and score-700 implementation seeds. Do not expose `navigate_code`, raw LSP, or any Worker-facing tool.

**Rationale:** Story 12.1 owns the process/runtime boundary; Story 12.2 needs stronger readiness evidence and bounded deterministic uses of already-approved operations. Compilation diagnostics remain observations and do not veto readiness.

**Alternatives considered:** A clean compilation requirement would reject usable projects; a general LSP client broadens authority; simulating readiness for XML-only scope is false evidence.

## R-6: Safe XML parse and exact byte ranges

**Decision:** Strictly decode only BOM/declaration-consistent UTF-8/UTF-16 while retaining a decoded-character-to-raw-byte map. Perform one bounded `XmlReader` parse with `DtdProcessing=Prohibit`, `XmlResolver=null`, no validation/schema resolver, and explicit rejection of XInclude; build one bounded immutable node projection and evaluate all validated catalog rules against it. A separate non-evaluating lexical range index maps direct-child element spans back to raw bytes and must agree exactly with the parsed projection. Its `SafeXmlDirectChildRangeSet` reaches GREEN before XML split RED/GREEN; that split GREEN is then a hard prerequisite of every US4 consumer and every qualification that observes XML ranges. No phase/parallel declaration may weaken this order.

**Rationale:** `XmlReader` supplies safe structure but not reliable exact raw-byte end offsets. The byte/character map plus a closed range index provides hashes without reparsing once per catalog rule or enabling Repository-controlled execution.

**Alternatives considered:** XPath/XSLT or schema validation enables forbidden capability; DOM-only line information cannot prove exact byte slices; regex element splitting is unsafe; truncating an oversized XML file is forbidden.

## R-7: Closed Communication Pattern Catalog

**Decision:** Add a separate embedded, versioned `communication-pattern-catalog/1.0` resource with a strict loader for the FR-019 grammar. Store its schema identity and raw SHA-256 in the manifest. Keep the legacy token catalog and v1 detector readable for historical behavior, but do not use them as Story 12.2 XML authority.

**Rationale:** The current catalog is a fixed token/key list and cannot express namespace-qualified XML selectors, ancestors, attribute tests, or bounded extraction. A new catalog avoids an incompatible reinterpretation of historical rules.

**Alternatives considered:** SystemOwner-authored executable selectors violate the closed grammar; hardcoded sample paths/endpoints overfit the release repositories; dynamic regex/XPath plugins violate FR-018/FR-020.

## R-8: Conservative classifications and retention

**Decision:** Evolve classification persistence additively with catalog rule identity, exact parsed-node or line/column Evidence Locator, and the expanded closed provenance matrix. Canonically aggregate occurrences, sort with the fixed ranks and comparisons in `contracts/canonical-envelope.md`, and when over 256 retain the first 255 plus one deterministic Bound Exceeded Warning while preserving complete totals. Classification fingerprints use the versioned canonical envelope and contain no source text or host path.

**Rationale:** Existing v1 classification rows and 100-sample handoffs must remain readable, while Story 12.2 requires a different closed contract and full 256-observation bound.

**Alternatives considered:** Deleting v1 rows breaks compatibility; persisting source snippets violates stewardship; silently dropping observations makes totals untruthful.

## R-9: File Code Unit representation and splitting

**Decision:** Add a prepared-file factory and nullable new-path fields to `CodeUnit`: deterministic ID, Prepared Scan Manifest ID, exact `FileHash`, byte start/end-exclusive, exact line range, segment ordinal/count, direct/final score, selection reason, contiguous per-Repository `ManifestOrdinal`, and distinct pair-global `PairSelectionOrdinal`. Both ordinals are excluded from File Code Unit identity but included in manifest/pair fingerprints and replay. Historical Code Units keep their existing shape. Oversized Java uses exact top-level JDT-LS ranges; oversized XML uses exact direct-child ranges. The file is accepted only if the full selected content is represented without overlap/reordering and all non-unit framing is proven non-evidence-bearing and explicitly audited; otherwise the whole file is skipped.

**Rationale:** Existing Code Units lack byte/segment/score/ordinal identity and use random IDs. Additive fields preserve historical rows and make File Code Units deterministic.

**Alternatives considered:** A parallel scheduling-unit table duplicates the canonical Code Unit; approximating ranges violates exact anchors; partial segment creation hides lost content.

## R-10: Business-seeded ordering and paired selection

**Decision:** Pure Core planning code uses corrected FR-029 semantics: direct score at least 400 makes a high-value candidate; only the first at most 20 per Repository by descending direct score/path/segment are mandatory high-value seeds and CodeGraph expansion roots. Pair comparison uses descending final score, canonical lowercase UUID `D` text with ordinal comparison, path, and segment ordinal. It retains all mandatory seeds, fills remaining capacity to 150 from all nonmandatory candidates in canonical pair order, fails below 50, assigns `PairSelectionOrdinal 1..M`, then assigns each manifest `ManifestOrdinal 1..N` in Repository-local FR-031 order. Reversed input, UUID ties, interleaving, high-value over-cap, and expansion fallback use the corrected spec’s normative golden cases.

**Rationale:** Central pure planning makes replay tests independent of process/database timing and prevents completion order from changing selection.

**Alternatives considered:** `Guid.CompareTo`, PostgreSQL UUID order, RFC byte order, or input order can disagree with the required UUID-text comparator; making all score-400 candidates mandatory can exceed the defined 40-seed maximum; per-Repository independent caps cannot prove the combined 50-150 claim; LLM/PageRank/sample profiles violate the accepted design.

## R-11: Versioned manifest/result/handoff compatibility

**Decision:** Add `PreparedScanManifest` schema `prepared-scan-manifest/1.0` and a Story 12.2 Pre-Scan Result/handoff schema alongside v1. Existing v1 readers remain unchanged and must not project a v2 package. A new read model returns the approved prepared handoff, including manifest fingerprint, stable Prepared Scan Context Snapshot identity/content hash, relational legacy ContextSnapshotId, classification totals, and schema identities, but no Story 12.2 component consumes it for dispatch. The time-derived legacy ID and CapturedAt never enter canonical identity.

**Rationale:** Reinterpreting v1 as prepared approval would let incomplete historical profiles satisfy the new gate. A versioned additive form makes compatibility and Story 12.3's future consumption explicit.

**Alternatives considered:** Replacing v1 destroys historical readability; returning v2 through the old sequential-runner seam risks unauthorized dispatch; using only a manifest JSON blob weakens relational identity and auditability.

## R-12: Pair-wide atomic commit and replay

**Decision:** `IPreparedScanStore.CommitPairAsync` receives two fully validated immutable packages and expected coordination/state versions. Infrastructure locks the two Scan Runs and coordination row in canonical lowercase UUID-text order and uses one PostgreSQL transaction to insert both manifests, stable typed CodeGraph/Java proof fields plus first-commit audit durations, audits, File Code Units, classifications/totals, both Scan Context Snapshots with additive stable prepared identities, both v2 Pre-Scan Results, and both approved prepared-handoff eligibilities. Exact replay independently reconstructs every canonical-envelope byte sequence from PostgreSQL rows, including proof and prepared-context identities, and compares every canonical scalar, both ordinal domains, and ordered child. A retry's differing duration/telemetry/capture time/legacy context ID is ignored and does not overwrite the first commit; a changed stable proof/context predicate, binding, schema, or content hash conflicts. Any identity difference or persistence failure returns conflict/rollback with zero prepared mutation.

**Rationale:** Selection is pair-wide, so approving one Repository before the other would expose an invalid partial release scope. One transaction is stronger than compensating two commits and directly proves SC-008.

**Alternatives considered:** Separate Repository commits expose half-approved pairs; sagas require deletion/compensation of immutable evidence; serializing the package only as JSON prevents FK and uniqueness enforcement.

## R-13: Authorization and public seam

**Decision:** Add a ManagementAccess-protected `POST /api/systems/{systemId}/prepared-scans` endpoint accepting exactly two distinct Repository IDs, safe full refs, and exact commits. The coordinator performs ownership-filtered lookup before Git access and rechecks visibility immediately before atomic approval. API errors use ProblemDetails and never reveal whether an out-of-scope Repository exists.

**Rationale:** The story is a SystemOwner action and requires exact reference inputs. The existing profiling endpoint has neither paired scope nor exact reference/commit fields.

**Alternatives considered:** Reusing the old endpoint would silently change its historical semantics; a CLI-only seam omits product authorization; accepting host paths from the caller violates containment.

## R-14: Verification boundary

**Decision:** Use public-seam TDD with neutral Java/XML fixtures, real disposable PostgreSQL 17, and exact installed CodeGraph/JDT-LS qualification. The XML producer → XML split → every US4 consumer → XML-observing qualification chain is strict and nonparallelizable. Canonical audit construction receives its own preceding `PreparedScanAuditContractTests` RED and sole `PreparedScanAuditFactory` GREEN owner before manifest/store/replay consumers. An explicitly PostgreSQL-backed prepared-store suite remains separate from migration lifecycle. A release qualification performs three complete paired preparations, including reversed inputs, compares every SC-001 output, and measures complete per-Repository PreScan. Workflow-boundary and post-GREEN security scans remain nonbehavioral qualification.

**Rationale:** Fakes prove deterministic logic but cannot prove process output, XML safety, PostgreSQL atomicity, migration compatibility, or real-tool timing.

**Alternatives considered:** Release repositories as the only fixtures encourage overfitting; SQLite cannot prove the pair transaction/concurrency; unit-only markup or parser assertions are insufficient security evidence.

## R-15: Versioned canonical envelope and deterministic UUIDs

**Decision:** Adopt `contracts/canonical-envelope.md` as the sole byte grammar. It fixes magic/version, exact domain separators—including CodeGraph proof, Java readiness, and Prepared Scan Context Snapshot identity—type bytes, big-endian lengths/counts/integers/tags, RFC/network-order UUID bytes, enum/string/null/bool/list/object encoding, field tables, normalization, collection order, volatile/derived exclusions, acyclic fingerprint dependencies, SHA-256, and version-8 deterministic UUID construction. Manifest fingerprint depends on both stable proof hashes, stable prepared-context hash, and Pair Selection fingerprint; pair-package fingerprint depends on both manifest fingerprints; manifests never depend on pair-package fingerprint.

**Rationale:** The prior plan said only “length-prefixed UTF-8” and left valid implementations with different bytes, `Guid` ordering, null/default behavior, and a manifest ID/fingerprint cycle. The new directed dependency graph and fixed field tags make independent reconstruction and replay exact.

**Alternatives considered:** Canonical JSON still needs a complete number/property/escaping contract and invites property-order drift; protobuf/MessagePack defaults are library/version-sensitive; including a derived ID in its own fingerprint is circular; `.NET Guid.ToByteArray()` is not RFC network order.

## R-16: Stable readiness proof identity versus audit telemetry

**Decision:** Treat readiness proof identity and operational observation as separate concepts. The CodeGraph and Java envelopes contain only versioned stable inputs and pass/fail predicates required to bind approval; their SHA-256 digests are manifest inputs. Measured duration and all timestamps, progress, process, diagnostic, resource, retry, and raw protocol/status observations are bounded audit metadata, excluded from every envelope, fingerprint, UUID, replay comparison, and identity API projection. On exact replay, the first committed audit telemetry remains unchanged.

**Rationale:** FR-035/FR-040 and SC-001 require unchanged inputs to replay despite natural timing variation, while FR-011/SC-003 still require retained proof fields and performance evidence. Separate channels preserve both deterministic authority and auditable qualification.

**Alternatives considered:** Hashing canonical JSON leaves field/default grammar open; quantizing duration still changes at thresholds and makes time identity; dropping duration loses SC-003 evidence; overwriting duration on retry violates mutation-free replay.

## R-17: Stable Prepared Scan Context Snapshot identity

**Decision:** Preserve Story 12.1 `ScanContextSnapshot.Id` and `CapturedAt` unchanged as relational/audit metadata, and add a nullable new-path `PreparedIdentityHash` for Story 12.2 snapshots. Its canonical envelope contains exactly RepositoryId, ScanRunId, ProfiledRevision, `scan-context-snapshot/1.0`, and ContentHash. Manifest/handoff identity uses that digest and content hash; it excludes legacy ID and capture time.

**Rationale:** The legacy ID deliberately includes CapturedAt and cannot satisfy timestamp-invariant replay. A stable additive digest avoids historical migration/backfill and preserves exact Story 12.1 semantics.

**Alternatives considered:** Replacing the legacy ID breaks historical FKs; hashing CapturedAt preserves the defect; omitting context bindings weakens FR-037; backfilling historical rows silently reinterprets completed work.

## R-18: Dependency-graph and canonical-audit TDD ownership

**Decision:** Downstream tasks must be physically ordered so safe XML RED/GREEN is immediately followed by XML split RED/GREEN, then all US4 consumers, then XML-observing qualification. Separately, a public coordinator/planner seam test must remain RED for canonical candidate/filter/skip outcomes, order, closed reason, literal DetailFingerprint envelope/digest, deterministic audit UUID, and replay-relevant mutation until the single Core `PreparedScanAuditFactory` production change makes it GREEN. Focused and surrounding-suite GREEN evidence are both mandatory.

**Rationale:** Prose dependencies do not constrain phase-order or parallel executors, and post-implementation tests cannot provide honest preceding RED evidence for replay-relevant audit identity.

**Alternatives considered:** Letting the split planner parse XML duplicates security ownership; placing XML split after consumers makes GREEN unverifiable; folding audit identity into enumeration hides its public seam; labeling post-hoc qualification RED falsifies TDD evidence.

## R-19: Relationally reconstructable Prepared Scan Audit identity

**Decision:** Persist every canonical audit tag as a typed column. Add prepared-only `PreparedScanManifestEntry` rows with non-null ManifestId, FileCodeUnitId, fingerprint, scores/reason, and both ordinals. Their four-column non-null unique key is the valid EF/PostgreSQL principal for selected audit association. Audits retain nullable fingerprint/ordinal and `PreparedScanManifestEntryId`; `Selected` requires the composite FK, `Eligible` requires fingerprint only, and other outcomes require all null. Store independently rebuilds tags 1-10 and recomputes DetailFingerprint.

**Rationale:** Path cannot distinguish multiple segments, and a digest without its preimage cannot prove corruption or replay equality. Persisted scalars plus selected FK make every case exact while retaining nonselected candidates with no Code Unit row.

**Alternatives considered:** Persisting only DetailFingerprint is unverifiable; joining by path is ambiguous; a principal alternate key on nullable historical CodeUnit properties is infeasible in EF Core; forcing an entry for excluded eligible candidates would persist unselected units; putting entry ID into the envelope would duplicate canonical identity.

## R-20: Exact replay-suite and production-literal qualification ownership

**Decision:** Contract replay uses the task-owned `PreparedScanAtomicReplayTests` identity; relational reconstruction/atomicity remains separately mandatory in `PreparedScanAtomicPostgreSqlTests`. A post-GREEN `PreparedScanProductionLiteralQualificationTests` inspects production assemblies/resources and public output for neutral/release Repository names, paths, endpoints, and fixture line numbers, emitting a hashed JSON evidence record. Any match routes to a new behavioral regression RED/GREEN with a named production owner.

**Rationale:** OR filters can pass when one nonexistent test identity matches nothing, and generic security labels do not prove FR-024's four literal categories. Separate commands plus a concrete artifact make test discovery and zero-match evidence auditable.

**Alternatives considered:** Folding PostgreSQL into an OR filter can mask nonexecution; source-only grep misses compiled resources/output; treating a discovered literal as qualification cleanup bypasses honest regression TDD.

## R-21: EF-feasible prepared entry and final qualification barrier

**Decision:** Move manifest membership, scores, reasons, and ordinals from nullable historical CodeUnit association fields into a prepared-only principal table. CodeUnit keeps nullable exact-file fields/fingerprint but is referenced only by its existing non-null primary key. The final verification task is a barrier after production-literal, PostgreSQL migration/atomicity/replay, workflow/security, release, and all behavioral qualifications; it alone runs final quickstart/build/full test.

**Rationale:** EF alternate-key members are required and cannot preserve historical null rows. A non-null prepared principal is schema-feasible and gives selected audits an exact FK. A physically last barrier prevents quickstart from racing required qualification owners.

**Alternatives considered:** Filtered unique indexes cannot be FK principals in PostgreSQL; triggers add unnecessary persistence complexity; multiple final-verification tasks can race or omit evidence.
