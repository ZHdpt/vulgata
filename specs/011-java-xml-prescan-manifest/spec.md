# Feature Specification: Lightweight Java/XML PreScan & Prepared Scan Manifest

**Feature Branch**: `demo0831/story-12-2`

**Created**: 2026-08-20

**Status**: Draft

**Input**: Story 12.2 prepares exact-revision Java/XML Repository scope as a deterministic, bounded, atomically approved manifest before any Worker Agent or LLM work.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Prepare One Exact Repository Revision (Priority: P1)

As a SystemOwner, I can prepare an authorized Repository at the exact Git revision I requested, so later Scan work cannot silently use a moved reference, ordinary working copy, stale file, or path outside that revision.

**Why this priority**: Every later classification, File Code Unit, Document, and Source Anchor depends on an immutable and reproducible source boundary.

**Independent Test**: Prepare a Repository from a requested full Git reference and commit, mutate the ordinary checkout and reference before and during preparation, and verify that the prepared inventory and hashes still describe only the exact acquired commit or that preparation fails closed without an approved handoff.

**Acceptance Scenarios**:

1. **Given** an authorized Repository, a safe full Git reference, and its exact 40-character lowercase commit, **When** PreScan begins, **Then** it acquires a private contained Profiled Revision without changing or depending on the ordinary checkout.
2. **Given** a moved, missing, ambiguous, unauthorized, or mismatched reference or a changed acquired snapshot, **When** identity is verified, **Then** PreScan fails before approval and exposes no partial prepared scope.
3. **Given** the same Repository, Profiled Revision, catalog, context, and source bytes on repeated runs, **When** candidates are enumerated and hashed, **Then** paths, filter outcomes, hashes, split ranges, and canonical ordering are identical.
4. **Given** a symlink, reparse point, path traversal, absolute path, case-colliding path, non-regular entry, or file outside the acquired snapshot, **When** enumeration occurs, **Then** the entry is rejected or explicitly audited and is never eligible.

---

### User Story 2 - Prove Revision-Bound Analysis Readiness (Priority: P1)

As a SystemOwner, I can see that structural analysis is complete and Java navigation is usable for the prepared revision before any provider cost is incurred, so the Scan does not begin from a stale or partially imported analysis state.

**Why this priority**: Business-seeded ordering and later source navigation are trustworthy only when their analysis services are bound to the same complete source snapshot.

**Independent Test**: Build analysis state for a neutral Repository revision, prove the closed completeness and Java readiness predicates, then introduce each stale, mismatched, incomplete, timed-out, or wrong-runtime condition and verify that approval and all LLM work remain blocked.

**Acceptance Scenarios**:

1. **Given** any prepared Repository, **When** its CodeGraph index completes, **Then** the index reports the exact revision-bound root and identity, complete state, no pending changes, no worktree mismatch, no recommended reindex, and no pending references.
2. **Given** at least one eligible Java file, **When** Java readiness is evaluated, **Then** the pinned server initializes on Java 21 over stdio, receives the initialized notification, reaches usable project-import state, and returns at least one document symbol for the deterministic readiness file within 60 seconds.
3. **Given** an XML-only prepared scope, **When** readiness is evaluated, **Then** complete CodeGraph readiness remains required and Java readiness is recorded as not applicable rather than simulated.
4. **Given** an analysis version, checksum, revision, root, workspace, status, response, or timeout failure, **When** PreScan evaluates readiness, **Then** it fails before any LLM call and leaves no approved handoff.

---

### User Story 3 - Extract Safe Communication Evidence (Priority: P1)

As a SystemOwner, I can obtain conservative Communication Kind Classifications from relevant Java/XML evidence without executing or trusting Repository-supplied XML features, so preparation identifies useful business seeds without inventing Cross-Repository relationships.

**Why this priority**: Communication evidence drives the highest-value manifest ordering and later exact resolution, while unsafe or sample-specific parsing would compromise both security and credibility.

**Independent Test**: Evaluate neutral Java/XML fixtures that cover every allowed catalog selector and every prohibited XML feature or bound, and verify exact Evidence Locators, provenance, classifications, deterministic truncation, and zero final Edge creation.

**Acceptance Scenarios**:

1. **Given** relevant contained XML, **When** it is parsed, **Then** DTDs, external entities, XInclude, schema-network fetches, custom resolvers, scripts, transforms, plugins, arbitrary expressions, and network access are unavailable.
2. **Given** a valid closed catalog selector, **When** it matches, **Then** the resulting Confirmed, Hint, or Warning classification retains its Communication Kind when known, exact Repository-relative Evidence Locator, rule, provenance, occurrence count, and deterministic fingerprint.
3. **Given** malformed, excessive, unsafe, contradictory, or unsupported evidence, **When** it is evaluated, **Then** it produces a bounded Warning or explicit file skip and never becomes a Confirmed classification.
4. **Given** neutral fixtures and the release Repositories, **When** behavior is compared, **Then** production behavior contains no sample Repository name, path, endpoint, or line-number special case.

---

### User Story 4 - Approve One Atomic Prepared Manifest (Priority: P1)

As a SystemOwner, I can receive an automatically selected, business-seeded File Code Unit manifest with an immutable context snapshot and approved Pre-Scan Handoff, so the next story can consider dispatch only from one complete and replay-safe prepared state.

**Why this priority**: A partially persisted manifest or unstable order could cause missing, duplicated, stale, or differently prioritized Worker work after a restart.

**Independent Test**: Prepare the same two-Repository release scope repeatedly, force failures at each persistence boundary, and verify exact ordinals, bounded selection, complete rollback, identical replay, conflict on changed input, and no Worker or Document activity.

**Acceptance Scenarios**:

1. **Given** an eligible source file within both source bounds, **When** the manifest is created, **Then** it contributes exactly one pending File Code Unit with exact path, whole-file range, language, file hash, source hash, selection reason, contiguous per-Repository Manifest Ordinal, and distinct pair-global Pair Selection Ordinal.
2. **Given** an oversized Java or XML file, **When** every part can be represented at safe top-level boundaries within the bounds, **Then** it contributes deterministic non-overlapping File Code Unit segments; otherwise the entire file is explicitly skipped and no content is silently truncated.
3. **Given** two release Repositories with enough eligible work, **When** business-seeded selection completes, **Then** their approved manifests contain together at least 50 and at most 150 File Code Units, ordered by fixed evidence scores, bounded graph expansion, and normalized path ties.
4. **Given** all preparation predicates pass, **When** approval commits, **Then** the Prepared Scan Manifest, its File Code Units and audits, Communication Kind Classifications, Scan Context Snapshot, Pre-Scan Result, and approved Pre-Scan Handoff eligibility become visible atomically.
5. **Given** any persistence failure, stale version, changed canonical input, or incomplete package, **When** approval is attempted, **Then** none of the package becomes dispatchable; an exact prior package replays without mutation and a different package conflicts.

### Edge Cases

- The requested reference resolves correctly at acquisition and then moves; the acquired Profiled Revision remains authoritative and the reference is reverified before approval.
- The exact snapshot contains case-only duplicate paths, Unicode-equivalent but byte-distinct names, symlinks, reparse points, unsupported archive entries, or a path that normalizes outside the Repository.
- A `.java` or `.xml` file is empty, BOM-only, invalidly encoded, exactly on a byte/line bound, one byte/line over it, or changes during preparation.
- A Repository contains no eligible Java/XML file, fewer than the paired 50-unit release minimum, or more than the paired 150-unit maximum.
- Java readiness candidates are empty, descriptor-only, oversized, or return no document symbols; selection cannot silently move to a nondeterministic file.
- CodeGraph reports complete but also reports pending changes, a worktree mismatch, a recommended reindex, pending references, or paths for another snapshot.
- XML is deeply nested, has excessive elements or attributes, contains a prohibited declaration, uses an unsupported encoding, or exceeds the hard audit bound before safe splitting.
- A catalog glob or selector attempts traversal, regular expressions, XPath, XSLT, scripts, custom code, network lookup, or an unbounded wildcard.
- More than 256 communication observations exist; retention and the bound-exceeded Warning remain deterministic and totals remain truthful.
- An oversized file has one top-level unit larger than the bounds, overlapping or incomplete symbol ranges, mixed XML content that cannot be attributed safely, or no top-level boundary.
- Several files receive the same business score, input Repository order is reversed, the pair order interleaves Repositories by score, more than 150 units qualify, bounded post-readiness CodeGraph related-file expansion times out, or graph evidence references an ineligible/out-of-scope file.
- An identical manifest commit is retried after an acknowledgement loss, or a prior manifest exists for the same Scan Run with one changed hash, ordinal, classification, snapshot, or schema identity.

## Requirements *(mandatory)*

### Functional Requirements

#### Exact source scope, filtering, and hashing

- **FR-001**: PreScan MUST require current SystemOwner authorization for the Repository and MUST bind one Scan Run to one safe full Git reference and one exact 40-character lowercase Profiled Revision.
- **FR-002**: PreScan MUST use the inherited exact-revision acquisition boundary to materialize a private, contained, immutable snapshot without changing or trusting the ordinary checkout, and MUST verify the reference, origin, commit, and snapshot identity again before approval.
- **FR-003**: Enumeration MUST consider only regular files in the exact snapshot and MUST emit one canonical Repository-relative path using `/`, without leading `/`, empty, `.`, `..`, control, drive, absolute, or backslash segments. Any case-insensitive path collision MUST fail preparation rather than select one path by host behavior.
- **FR-004**: Supported inputs MUST be files whose final extension is `.java` or `.xml` under ordinal-ignore-case comparison. Extension matching MUST NOT inspect content or use Repository-specific aliases.
- **FR-005**: A supported input MUST be deterministically filtered when any path segment is `.git`, `.idea`, `.vscode`, `.settings`, `.gradle`, `.mvn`, `target`, `build`, `out`, `dist`, `generated`, `node_modules`, `vendor`, `vendors`, `packages`, `third_party`, or `third-party`, or when it lies below `src/test`, `test`, `tests`, `fixture`, or `fixtures`. Segment comparison is ordinal-ignore-case; substring matches do not filter.
- **FR-006**: Reparse points, links, non-regular entries, binary Java input, invalid Java UTF-8, unsupported XML encoding, and unsafe paths MUST be explicitly audited with a closed reason and MUST NOT become File Code Units. XML under otherwise eligible production paths MUST NOT be excluded merely because it is configuration data.
- **FR-007**: PreScan MUST compute `FileHash` as lowercase SHA-256 of the complete raw file byte sequence from the exact snapshot, including any byte-order mark and original newline bytes. It MUST NOT use the legacy first-4,096-byte File Identity Hash as exact identity.
- **FR-008**: PreScan MUST compute `SourceHash` as lowercase SHA-256 of the exact raw byte interval assigned to a File Code Unit. For an unsplit file, `SourceHash` MUST equal `FileHash`; for a split file, each segment hash MUST preserve the original bytes with no encoding or newline normalization.
- **FR-009**: Hashing, text decoding, line counting, parsing, analysis, and approval MUST all verify the same immutable snapshot identity. A byte, path, or revision change at any later verification MUST invalidate the complete preparation.

#### Analysis readiness

- **FR-010**: Every prepared Repository MUST build a fresh revision-bound index with CodeGraph `@colbymchenry/codegraph@1.5.0` on the Windows host; PreScan MUST NOT use CodeGraphContext or its private storage/protocol.
- **FR-011**: Complete CodeGraph proof MUST bind the expected Repository, Scan Run, Profiled Revision, runtime identity, snapshot root, and index identity and require initialized status, index state `complete`, zero added/modified/removed pending changes, no worktree mismatch, no recommended reindex, zero pending references, and trusted project/index paths. All proof fields and the indexed-file count MUST be retained in the manifest audit.
- **FR-012**: If eligible Java exists, the JDT LS readiness file MUST be the ordinal-first normalized path after excluding `module-info.java`, `package-info.java`, empty files, and files outside the hard audit bound. If no such file exists, Java readiness MUST fail with a bounded reason.
- **FR-013**: Java readiness MUST use `jdt-language-server-1.61.0-202608141332` on Java 21 over stdio in the isolated Repository/Profiled Revision workspace established by Story 12.1, complete initialize/initialized and usable import state, and return at least one document symbol for the readiness file within 60 seconds.
- **FR-014**: Java compilation errors and diagnostics MUST NOT by themselves fail readiness, but a version/checksum/workspace/revision mismatch, malformed response, empty document-symbol result, cancellation, or timeout MUST stop PreScan before any LLM call.
- **FR-015**: XML-only scope MUST record Java readiness as not applicable. CodeGraph readiness remains mandatory for every Repository even when its indexed-file count is zero.

#### Safe XML and Communication Kind Classifications

- **FR-016**: The safe XML pipeline MUST accept at most 4,194,304 raw bytes per XML file, nesting depth 64, 100,000 elements, 200,000 total attributes, 128 attributes per element, and 16,384 decoded characters per attribute or text value used as evidence. Exceeding any hard bound MUST stop parsing that file and produce an explicit bounded outcome.
- **FR-017**: XML decoding MUST be limited to XML-defined UTF-8 and UTF-16 forms identified by a valid byte-order mark or declaration. Conflicting, unsupported, or malformed encoding declarations MUST be rejected; raw-byte hashes remain unchanged.
- **FR-018**: XML parsing MUST prohibit DTDs, external and parameter entities, XInclude, schema-network fetches, custom resolvers, network access, XPath, XSLT, scripts, plugins, and execution of Repository-supplied code.
- **FR-019**: The closed Communication Pattern Catalog grammar MUST allow only: (a) contained case-sensitive path globs composed of literal segments plus `*`, `?`, or one `**` segment; (b) a selector of at most eight exact element steps expressed as namespace URI plus local name; (c) at most one optional exact ancestor chain of the same form; (d) at most eight exact attribute names with presence or literal-equality tests; (e) extraction from one named attribute or the matched element's bounded text; and (f) fixed catalog-declared prefix removal and case-preserving trim for endpoint or implementation identifiers.
- **FR-020**: Catalog values MUST reject path traversal, absolute paths, empty steps, unbounded element wildcards, regular expressions, arbitrary predicates, functions, executable expressions, resolver declarations, or network locations. One file MUST be parsed once and evaluated only through validated catalog rules.
- **FR-021**: A Communication Kind Classification MUST be Confirmed only for direct structural evidence or an exact catalog match allowed by the closed rule/provenance matrix; incomplete import/framework evidence MAY be a Hint; malformed, unsafe, excessive, contradictory, or unsupported evidence MUST be a Warning. A classification MUST NOT decide provider, consumer, final endpoint ownership, or a Cross-Repository Edge.
- **FR-022**: Every retained classification MUST carry its known Communication Kind, level, confidence, closed rule, provenance, canonical Repository-relative path, exact line/column or parsed-node Evidence Locator, occurrence count, and lowercase SHA-256 fingerprint over its canonical evidence identity. It MUST contain no source content or absolute host path.
- **FR-023**: A Repository MUST retain at most 256 communication observations in canonical order by level, kind, rule, path, locator, and fingerprint. If more exist, it MUST retain the first 255 plus one deterministic Bound Exceeded Warning, preserve complete counts, and mark evidence truncated.
- **FR-024**: Neutral Java/XML fixtures MUST exercise every catalog grammar form, classification level, prohibited XML feature, and bound. Production behavior MUST contain zero literal sample Repository names, paths, endpoints, or line numbers.

#### Bounded File Code Units and deterministic selection

- **FR-025**: An eligible unsplit source file of at most 65,536 raw bytes and at most 2,000 logical lines MUST produce exactly one pending `CodeUnitType.File` File Code Unit covering the complete file. Line counting MUST treat CRLF as one terminator, lone CR and lone LF as one terminator, and a nonempty final unterminated sequence as a line.
- **FR-026**: A supported file larger than either source bound but no larger than the 4,194,304-byte hard audit bound MAY split only when all resulting segments fit both source bounds and safe boundaries cover the selected source without overlap, duplicate bytes, reordered content, or silent truncation.
- **FR-027**: Java split boundaries MUST be exact JDT LS top-level type ranges. XML split boundaries MUST be exact safely parsed child-element ranges directly under the document element. Segment order MUST follow source byte position; each segment MUST retain its parent `FileHash`, exact byte and line range, one-based segment ordinal/count, and its own `SourceHash`.
- **FR-028**: If any required range is missing, approximate, overlapping, out of order, out of bounds, or itself exceeds a source bound, the entire oversized file MUST be skipped with a closed reason. PreScan MUST NOT create a partial set of segments for that file.
- **FR-029**: Each candidate File Code Unit MUST record a stable direct business score equal to the maximum applicable evidence score: 1000 for Confirmed communication evidence, 900 for an exact XML communication declaration, 800 for an endpoint/controller/application-entry symbol, 700 for a JDT-LS-resolved implementation of a candidate scored 800 or higher, 400 for a communication Hint, and 100 for any other eligible production file. A **high-value candidate** is any candidate with direct score 400 or higher. A **high-value seed** is one of the first at most 20 high-value candidates within one Repository after descending direct score, ascending normalized path, and ascending segment ordinal. Thus at most 20 high-value seeds per Repository are mandatory; additional high-value candidates retain their score but are not mandatory seeds.
- **FR-030**: CodeGraph expansion MUST start only from those at most 20 high-value seeds per Repository and MUST be limited to Calls, Imports, and Inherits relationships, at most two hops, at most 200 unique eligible files, and five seconds after complete index readiness. An expanded file receives the maximum source-seed score minus 100 per hop, never below its direct score. Qualification of an implementation for the 700 direct score uses the deterministic higher-scored candidate set and does not increase the 20-seed expansion bound.
- **FR-031**: Repository-local ordering MUST sort by descending final score, then ascending normalized Repository-relative path, then ascending segment ordinal. CodeGraph index/rebuild, status, runtime, identity, Repository/Profiled Revision binding, trusted-path, completeness, or readiness failure—including any timeout in those operations—MUST reject approval under FR-010 through FR-015. Only the bounded related-file expansion performed after complete readiness under FR-030 may fail or time out nonfatally; that case MUST record a Warning and use the direct-score/path order without failing PreScan.
- **FR-032**: For the approved two-Repository release scope, the canonical pair order MUST be descending final score, then ascending canonical Repository identity, normalized path, and segment ordinal. Canonical Repository identity is the lowercase 36-character UUID `D` representation (`8-4-4-4-12`, including hyphens), compared character-for-character with ordinal comparison; host `Guid` comparison, database UUID ordering, input order, and platform byte order MUST NOT define the result. Selection MUST first retain every high-value seed defined by FR-029, then fill the remaining capacity to 150 from all nonmandatory candidates in canonical pair order, and finally assign Pair Selection Ordinals by canonical pair order over the retained set. Because the bound is at most 20 seeds per Repository, mandatory seeds cannot exceed 40. Additional high-value candidates beyond each Repository's first 20 retain their score but are nonmandatory and MAY be excluded at the cap. Every excluded candidate MUST be explicitly audited. Approval MUST fail if fewer than 50 units remain.
- **FR-033**: Each retained File Code Unit MUST have deterministic identity inputs consisting of Repository, Scan Run, Profiled Revision, canonical path, parent `FileHash`, exact byte/line range, segment ordinal/count, and `SourceHash`; neither Manifest Ordinal nor Pair Selection Ordinal participates in File Code Unit identity. It MUST begin Pending and MUST NOT be dispatched by this feature.

#### Atomic Prepared Scan Manifest and handoff

- **FR-034**: The **Prepared Scan Manifest** MUST use schema identity `prepared-scan-manifest/1.0` and contain exactly one Repository, Scan Run, and Profiled Revision plus exact snapshot identity, runtime identity, CodeGraph proof, Java readiness outcome, catalog identity/hash, ordered File Code Unit entries, candidate/filter/skip audit, classification references and totals, Scan Context Snapshot identity/hash, created time, and approval state. Every retained entry MUST carry two noninterchangeable ordinal fields: `ManifestOrdinal`, contiguous one-based `1..N` within that Repository's selected manifest in Repository-local FR-031 order; and `PairSelectionOrdinal`, contiguous one-based `1..M` and unique across the two selected manifests in pair-global FR-032 order.
- **FR-035**: Prepared Scan Manifest identity and fingerprint MUST include the complete ordered entry list with both Manifest Ordinal and Pair Selection Ordinal. The pair-level selection fingerprint and replay comparison MUST include each canonical Repository identity and every Pair Selection Ordinal in `1..M` order. File Code Unit identity excludes both ordinals under FR-033. Per-Repository uniqueness applies to `(ManifestId, ManifestOrdinal)`; pair-global uniqueness applies to `(TwoRepositoryScanCoordinationId, PairSelectionOrdinal)`. API and audit projections MUST expose both fields with their domains named; exact replay MUST compare both, and a missing, duplicate, gapped, swapped, or changed ordinal MUST conflict. The approved Pre-Scan Handoff supplied to Story 12.3 MUST carry both: Pair Selection Ordinal is the canonical cross-Repository work order, while Manifest Ordinal proves per-Repository completeness and replay; neither field grants dispatch authority. Fingerprints remain lowercase SHA-256 over the plan-defined versioned canonical envelope; timestamps and machine-specific absolute paths MUST NOT affect replay identity.
- **FR-036**: The existing Pre-Scan Result and Pre-Scan Handoff version-1 seams MUST remain readable for historical data but MUST NOT satisfy Story 12.2 approval. The Story 12.2 approved form MUST bind the Prepared Scan Manifest fingerprint, full 256-observation contract, Scan Context Snapshot, Profiled Revision, and schema identities.
- **FR-037**: The Scan Context Snapshot MUST be captured once through the Story 12.1 immutable snapshot contract and MUST match the Repository, Scan Run, Profiled Revision, context schema, and content hash recorded by the manifest.
- **FR-038**: The Prepared Scan Manifest, candidate/filter/skip audit, all retained File Code Units, Communication Kind Classifications and totals, Scan Context Snapshot, Pre-Scan Result, and approved Pre-Scan Handoff eligibility MUST commit in one atomic durable transaction after all exact-source and readiness predicates pass.
- **FR-039**: No partial package, unapproved manifest, stale predecessor, or approval attempt in progress may be returned as an approved Pre-Scan Handoff or treated as dispatchable. Transaction failure or concurrency conflict MUST leave zero partial prepared mutation.
- **FR-040**: Retrying the exact canonical package MUST return mutation-free replay only when every stored identity, ordinal, hash, classification, snapshot, readiness proof, and audit value is equal. Any missing, extra, reordered, or changed value MUST return conflict without mutation.
- **FR-041**: PreScan MUST perform zero LLM calls. The approved Pre-Scan Handoff certifies deterministic preparation but grants no Worker dispatch authority by itself.
- **FR-042**: Vulgata, CodeGraph, Java 21, and JDT LS MUST run on the documented Windows host; PostgreSQL 17 MUST be the only required Docker service. Machine installation paths are configuration only and CodeGraphContext MUST remain outside production startup and release acceptance.
- **FR-043**: Story 12.2 MUST NOT execute Worker Agents, expose or use `navigate_code`, define Pass 1 concurrency/retry/lease/dispatch policy, generate or pre-allocate Documents, create final `REFERENCES` Edges or Uncertainties, perform Cross-Repository Resolution or Pass 2, implement Chat/retrieval, or use Backup B/demo-only prepared state.

### Deterministic Selection Golden Cases

The following cases are normative acceptance vectors. `Repository A` has canonical identity `00000000-0000-0000-0000-000000000002`; `Repository B` has `00000000-0000-0000-0000-000000000001` and therefore sorts first when all earlier keys tie.

1. **Repository identity tie**: Given `B:src/B.java` and `A:src/A.java` with equal final score and segment ordinal, Pair Selection Ordinals MUST be `B=1`, `A=2`, regardless of input order, host UUID comparison, or database row order. Each single-entry manifest has Manifest Ordinal `1`.
2. **Cross-Repository interleaving**: Given `A:src/Confirmed.java=1000`, `B:src/Endpoint.java=800`, `A:src/Hint.java=400`, and `B:src/Fallback.java=100`, Pair Selection Ordinals MUST be `A/Confirmed=1`, `B/Endpoint=2`, `A/Hint=3`, `B/Fallback=4`. Repository A Manifest Ordinals are `Confirmed=1`, `Hint=2`; Repository B Manifest Ordinals are `Endpoint=1`, `Fallback=2`.
3. **Reversed input**: Reversing Repository order and every candidate input sequence in cases 1 and 2 MUST produce byte-identical selected identities, both ordinal domains, audits, and fingerprints.
4. **High-value over-cap**: Given 25 score-400 high-value candidates and 55 score-100 fallback candidates in each Repository (160 total), the first 20 score-400 candidates per Repository by path are the 40 mandatory high-value seeds. Pair selection MUST retain all 50 score-400 candidates, then all 55 Repository B fallback candidates and the first 45 Repository A fallback candidates by normalized path, and audit Repository A's remaining 10 fallback candidates as over-cap. No candidate ranked 21–25 within its Repository is mandatory merely because it remains a high-value candidate.
5. **Expansion fallback boundary**: With identical complete-ready inputs, a post-readiness related-file expansion timeout MUST preserve the direct-score selected set/order and add the same Warning. The same timeout during index/rebuild, status, identity/path/completeness verification, or readiness MUST produce no approved manifest.

### Ordinal Participation Matrix

This matrix is normative and prevents either ordinal from becoming an accidental semantic source identity.

| Artifact or comparison | Manifest Ordinal | Pair Selection Ordinal |
| --- | --- | --- |
| File Code Unit identity/fingerprint | Excluded | Excluded |
| Communication classification or candidate-audit identity/fingerprint | Excluded | Excluded; audit projection may reference it after selection |
| Prepared Scan Manifest entry | Included and unique within the manifest | Included and bound to the two-Repository coordination |
| Prepared Scan Manifest fingerprint and derived manifest ID | Included directly | Included directly |
| Pair-selection fingerprint | Included through each manifest entry | Included directly in pair-global order |
| Pre-Scan Result identity | Excluded; identity remains the Scan Run | Excluded; identity remains the Scan Run |
| Pre-Scan Result fingerprint and approved Pre-Scan Handoff content | Included transitively through the bound manifest fingerprint and exposed explicitly | Included transitively through the bound manifest/pair fingerprints and exposed explicitly |
| API/audit projection and exact replay | Exposed, scope-named, and compared | Exposed, scope-named, and compared |
| Story 12.3 handoff use | Per-Repository completeness/replay proof | Canonical cross-Repository work order |

Neither ordinal alone is authorization or a semantic job/Document identity. Story 12.3 remains responsible for its own deterministic job identity and dispatch policy; it must consume, not reinterpret, the two ordering domains.

### Key Entities and Canonical Terminology

- **Profiled Revision**: The canonical glossary term for the exact immutable Git commit acquired for one Scan Run. It remains the source identity for every manifest member.
- **Prepared Scan Manifest**: The immutable, schema-versioned, canonically ordered and fingerprinted result of complete deterministic PreScan for one Repository. It binds the exact source inventory, selection/audit outcomes, File Code Units, analysis-readiness proof, classification references, Scan Context Snapshot, contiguous Repository-local Manifest Ordinals, and the entries' distinct pair-global Pair Selection Ordinals. `docs/UBIQUITOUS_LANGUAGE.md` now contains the authoritative entry for this term, and this feature definition applies that canonical meaning.
- **Manifest Ordinal**: The contiguous one-based position of a selected File Code Unit within its one-Repository Prepared Scan Manifest. It is a feature contract field, not a replacement for a canonical glossary term.
- **Pair Selection Ordinal**: The contiguous one-based position of a selected File Code Unit across exactly two coordinated Prepared Scan Manifests. It defines stable pair-global order without redefining either Repository Scan or Prepared Scan Manifest as System-level.
- **File Code Unit**: A `CodeUnitType.File` scheduling unit representing either one complete bounded file or one exact safe segment of an oversized file. It is not a method-level Code Unit and does not imply Worker execution or a Document. `docs/UBIQUITOUS_LANGUAGE.md` now contains the authoritative entry for this term, and this feature definition applies that canonical meaning.
- **Scan Context Snapshot**: The existing Story 12.1 immutable Global/System/Repository advisory context captured once for the same Repository Scan Run and Profiled Revision. It is context, not source evidence.
- **Communication Kind Classification**: The canonical conservative Confirmed, Hint, or Warning statement about a communication kind. It does not resolve endpoint ownership or create an Edge.
- **Evidence Locator**: The canonical bounded Repository-relative location plus rule/provenance identity supporting a classification, without source content or an absolute path.
- **Pre-Scan Result**: The canonical immutable ScanRun-scoped result. For Story 12.2 its approved schema is bound to the complete Prepared Scan Manifest rather than the narrower historical profile alone.
- **Pre-Scan Handoff**: The canonical versioned authorized view of the approved Pre-Scan Result. “Approved Pre-Scan Handoff” describes its state; it is not a new domain term and it grants no dispatch authority.

### Scope Boundaries, Dependencies, and Authority Reconciliation

- **Included**: exact Profiled Revision acquisition; contained deterministic Java/XML enumeration and filtering; exact raw-byte hashes; revision-bound complete CodeGraph indexing; real JDT LS readiness; safe catalog-driven XML parsing; Communication Kind Classifications; bounded File Code Units; deterministic business-seeded ordering and two-Repository 50–150 selection; atomic Prepared Scan Manifest, Scan Context Snapshot, Pre-Scan Result, and approved Pre-Scan Handoff.
- **Inherited from Story 12.1**: exact Git snapshot acquisition, closed CodeGraph/JDT-LS/source-reader ports, pinned runtime identity and isolated JDT-LS workspace, immutable Scan Context Snapshot, durable transaction/replay discipline, and two-Repository coordination.
- **Dated precedence**: the approved August 17/18 Epic 12 overlay temporarily replaces legacy PRD FR-4.1 method-like Code Units, configuration-data exclusion, broad filtering, and CodeGraph-boundary behavior for this release. The broader PRD remains post-deadline V1 scope.
- **Host runtime**: `@colbymchenry/codegraph@1.5.0`, `jdt-language-server-1.61.0-202608141332`, Java 21, and the Story 12.1 StreamJsonRpc 2.25.29 stdio boundary are fixed dependencies, not choices reopened by this feature.
- **Story 12.3 exclusion**: Worker execution, `navigate_code`, Pass 1 concurrency/retry/lease/dispatch behavior, `code-logic/file-v2`, Source Anchor validation for generated content, deterministic Document projections, and post-Pass1 reconciliation remain wholly owned by Story 12.3.
- **Later exclusions**: final Cross-Repository Resolution and Pass 2 (Story 12.4), Chat/UI/release evidence (Story 12.5), CodeGraphContext, Backup B/demo-only state, method-level scheduling, dynamic LLM PreScan questions, arbitrary catalog plugins, XML LSP, and general 10,000-file acceptance.
- **Authorization gate**: The later plan necessarily changes Scan parsing, filtering, ordering, splitting, and handoff behavior and may define analysis-tool use. After independent plan-review `ACCEPT`, the orchestrator must pause before task generation for explicit planned-change authorization citing the plan sections, affected PRD clauses, effects, implementation intent, exact Story 12.2 boundary, and every remaining TDD/review/E2E/verification gate.

### Source Reference Coverage

- **Story obligation and exclusion authority**: `docs/bmad/implementation-artifacts/12-2-lightweight-java-xml-pre-scan-and-prepared-scan-manifest.md`; `docs/bmad/epics.md` Stories 12.2 and 12.3; `docs/bmad/implementation-artifacts/sprint-status.yaml`.
- **Approved dated overlay**: `docs/bmad/planning-artifacts/architecture/august-30-bounded-scanning-design.md`; the 2026-08-17 bounded-scanning recovery proposal; the 2026-08-18 host-runtime/JDT-LS proposal.
- **PRD and canonical architecture reconciliation**: PRD FR-4.1–FR-4.5, FR-4.9, FR-4.12, FR-5.8, FR-6.1/6.2, FR-11.6, NFR-1.1, NFR-2.3, NFR-3.1–NFR-3.3, and NFR-4.1; `docs/bmad/planning-artifacts/architecture.md` with dated-overlay precedence.
- **Governance and language**: `AGENTS.md`; `.specify/memory/constitution.md`; `docs/UBIQUITOUS_LANGUAGE.md` definitions for Profiled Revision, Pre-Scan Result, Communication Kind Classification, Evidence Locator, Pre-Scan Handoff, Worker Agent, Code Unit, Scan, Edge, and Cross-Repository Resolution.
- **Inherited Story 12.1 definition**: `docs/bmad/implementation-artifacts/12-1-durable-scan-foundation-and-analysis-runtime.md`; `specs/010-durable-scan-foundation/spec.md`; `specs/010-durable-scan-foundation/plan.md`.
- **Observed inherited seams**: exact Git revision acquisition and tests; CodeGraph/JDT-LS adapters and real qualification tests; `ScanContextSnapshot`; `CodeUnit`/`CodeUnitType.File`; `PreScanResult`; `CommunicationKindClassification`; `ApprovedPreScanHandoff`; candidate enumeration; durable Scan store and replay tests. These seams constrain compatibility but do not override the dated Story 12.2 requirements.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Across at least three preparations of unchanged inputs—including reversed Repository and candidate input order—100% of candidate paths, filter/skip reasons, raw-byte hashes, split ranges, classification fingerprints, scores, selected units, Manifest Ordinals, Pair Selection Ordinals, audits, and fingerprints are identical.
- **SC-002**: In a mutation matrix covering reference movement, snapshot mutation, path escape/collision, hash drift, stale index, wrong runtime/workspace, malformed analysis response, and readiness timeout, 100% of cases produce no approved Pre-Scan Handoff and zero LLM calls.
- **SC-003**: CodeGraph complete-index proof finishes in under 30 seconds per release Repository and records every required completeness predicate; Java readiness finishes in under 60 seconds whenever eligible Java exists.
- **SC-004**: PreScan completes in under two minutes per release Repository on the documented demonstration host, excluding later Worker/LLM time.
- **SC-005**: The two approved release manifests contain together 50–150 automatically selected pending File Code Units; 100% of the at most 20 high-value seeds per Repository defined by FR-029 are included, both ordinal domains are gap-free and unique in their declared scopes, no unit exceeds 65,536 raw bytes or 2,000 lines, and no skipped oversized file is partially represented.
- **SC-006**: Neutral fixture verification covers 100% of allowed catalog grammar forms, prohibited XML capabilities, XML hard bounds, classification levels, Java/XML split outcomes, and ordering fallback, with zero sample-specific production literals.
- **SC-007**: Every retained Communication Kind Classification has one valid exact Evidence Locator and provenance identity; at most 256 observations are retained, complete totals remain truthful, and zero final Cross-Repository Edges are created.
- **SC-008**: In forced failures at every prepared-state persistence boundary, 0 partial manifests, File Code Units, classifications, snapshots, results, or handoffs become dispatchable. Exact retry produces one mutation-free replay; any changed package produces one conflict and zero mutation.
- **SC-009**: Story-boundary inspection finds 0 Worker Agent executions, `navigate_code` calls, Pass 1 dispatch/retry behavior, generated Documents, final Edges/Uncertainties, Cross-Repository Resolution, Chat behavior, CodeGraphContext dependencies, or Backup B state introduced by Story 12.2.

## Assumptions

- The active two-Repository coordination and durable persistence foundation from completed Story 12.1 are available and bound to the same Profiled Revisions used by this feature.
- Release Repositories contain enough eligible Java/XML source to satisfy the combined 50-unit minimum; insufficient scope is an approval failure rather than authorization to lower the release claim.
- Git file bytes are the hash authority. Decoded text is used only for validation, line/range calculation, Java/XML analysis, and evidence extraction, never to redefine exact byte identity.
- UTF-8 is the accepted Java source encoding for this bounded release. XML accepts only its defined UTF-8/UTF-16 forms. Other encodings are explicitly skipped rather than guessed.
- The closed catalog is authorized, versioned, and available before PreScan. Authoring UI, arbitrary plugins, and SystemOwner-defined executable selectors are outside this story.
- Timestamp capture is auditable metadata but is excluded from canonical manifest replay identity; all source-, context-, schema-, catalog-, runtime-, and ordering-bearing values are included.
