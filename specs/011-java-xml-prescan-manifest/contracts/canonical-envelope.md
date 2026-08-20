# Contract: Story 12.2 Canonical Envelope 1.0

This contract is the sole byte grammar for Story 12.2 deterministic fingerprints and UUIDs. Implementations must not substitute JSON, database ordering, `Guid.ToByteArray()`, host-endian integers, culture-sensitive formatting, or reflection/property order.

Prepared Scan Manifest, File Code Unit, and Scan Context Snapshot use their current authoritative glossary definitions. Canonical audit envelope behavior must be proven through the public-seam RED owned by `PreparedScanAuditContractTests` before `PreparedScanAuditFactory` provides the sole production GREEN.

## 1. Envelope and value grammar

`CENV(domain, root)` is the concatenation below:

1. magic/version bytes `56 47 43 45 01` (`VGCE` plus byte version `1`);
2. `String(domain)`;
3. one tagged root value, always `Object` for Story 12.2 artifacts.

Every length/count is an unsigned 32-bit big-endian integer. Every field tag is an unsigned 16-bit big-endian integer. Exceeding either range rejects construction.

| Value | Type byte | Payload |
| --- | --- | --- |
| Null | `00` | none |
| Boolean | `01` | exactly `00` false or `01` true |
| Int64 | `02` | signed two's-complement 8-byte big-endian |
| UInt64 | `03` | unsigned 8-byte big-endian |
| UUID | `04` | exactly 16 RFC 4122/network-order bytes parsed from validated lowercase UUID `D` text; never `.NET Guid.ToByteArray()` order |
| Enum | `05` | `u32 byteLength` + nonempty case-sensitive ASCII closed member name |
| String | `06` | `u32 byteLength` + strict UTF-8 bytes |
| Bytes | `07` | `u32 byteLength` + exact bytes |
| List | `08` | `u32 itemCount` + each complete tagged value in normative order |
| Object | `09` | `u32 fieldCount` + repeated `u16 fieldTag` + complete tagged value, with tags strictly ascending and unique |

Null is distinct from an empty String, empty Bytes, or empty List. There is no floating-point, decimal, timestamp, map, set, optional-field omission, implicit default, or untagged nested value. A nullable field is always present at its assigned tag and encodes Null when absent. Unknown tags/schema domains reject decoding.

## 2. Normalization and ordering rules

- The encoder performs no Unicode normalization or case folding. Domain validators supply canonical values before encoding.
- Repository IDs used for sorting are lowercase 36-character UUID `D` strings compared with ordinal character comparison. UUID envelope values use the corresponding RFC network-order 16 bytes.
- Git paths retain exact validated Git path code points, use `/`, and are strict UTF-8. They are compared by ordinal Unicode scalar/code-unit contract already defined by the feature; they are not NFC/NFKC-normalized or case-folded.
- SHA-256 values and Profiled Revisions are lowercase ASCII hex. Schema/domain identifiers, full refs, rules, reasons, provenance, selection reasons, and enum names are case-sensitive validated strings/enums.
- No unordered collection enters an envelope. Callers sort first using the artifact-specific rules below; List preserves that order and includes its count.
- Nested objects are encoded inline. They may reference another artifact only by its already-computed fingerprint/UUID scalar. No parent/child envelope may recursively embed the other, so cycles are impossible.

## 3. Exact domain separators

The following ASCII domain strings are fixed:

```text
vulgata/story-12.2/file-code-unit-identity/1.0
vulgata/story-12.2/codegraph-completeness-proof/1.0
vulgata/story-12.2/java-readiness/1.0
vulgata/story-12.2/prepared-scan-context-snapshot/1.0
vulgata/story-12.2/communication-classification/1.0
vulgata/story-12.2/prepared-scan-audit/1.0
vulgata/story-12.2/pair-selection/1.0
vulgata/story-12.2/prepared-scan-manifest/1.0
vulgata/story-12.2/prepared-scan-pair-package/1.0
vulgata/story-12.2/deterministic-uuid/1.0
```

## 4. Artifact field tables

All listed fields are required in the exact tag order. “Fingerprint bytes” means the 32 decoded bytes of validated lowercase SHA-256, not 64 hex-text bytes.

### 4.1 File Code Unit identity

Domain `.../file-code-unit-identity/1.0`:

| Tag | Field | Encoding |
| --- | --- | --- |
| 1 | RepositoryId | UUID |
| 2 | ScanRunId | UUID |
| 3 | ProfiledRevision | String |
| 4 | canonical path | String |
| 5 | FileHash | Bytes(32) |
| 6 | ByteStart | UInt64 |
| 7 | ByteEndExclusive | UInt64 |
| 8 | LineStart | UInt64 |
| 9 | LineEnd | UInt64 |
| 10 | SegmentOrdinal | UInt64 |
| 11 | SegmentCount | UInt64 |
| 12 | SourceHash | Bytes(32) |

SHA-256 of the envelope is `FileCodeUnitIdentityFingerprint`. `ManifestOrdinal`, `PairSelectionOrdinal`, scores, selection reason, status, timestamps, and derived File Code Unit ID are excluded. This exactly implements FR-033.

### 4.2 CodeGraph completeness-proof identity

Domain `.../codegraph-completeness-proof/1.0`:

| Tag | Field | Encoding and approved-state invariant |
| --- | --- | --- |
| 1 | RepositoryId | UUID |
| 2 | ScanRunId | UUID |
| 3 | ProfiledRevision | String, exact lowercase 40-hex |
| 4 | SnapshotIdentity | Bytes(32) |
| 5 | SnapshotRootBindingHash | Bytes(32), SHA-256 of the trusted logical snapshot binding token; never an absolute path |
| 6 | RuntimeIdentityHash | Bytes(32) |
| 7 | IndexIdentity | String, bounded case-sensitive machine identity |
| 8 | ProofSchemaIdentity | String, exactly `codegraph-completeness-proof/1.0` |
| 9 | Initialized | Boolean, exactly true |
| 10 | IndexState | Enum, exactly `Complete` |
| 11 | PendingAdded | UInt64, exactly zero |
| 12 | PendingModified | UInt64, exactly zero |
| 13 | PendingRemoved | UInt64, exactly zero |
| 14 | WorktreeMismatch | Boolean, exactly false |
| 15 | ReindexRecommended | Boolean, exactly false |
| 16 | PendingReferences | UInt64, exactly zero |
| 17 | IndexedFileCount | UInt64 |
| 18 | LogicalProjectBinding | String, bounded Repository/Profiled-Revision logical binding; never a host path |
| 19 | LogicalIndexBinding | String, bounded index identity/binding; never a host path |
| 20 | RepositoryBindingMatched | Boolean, exactly true |
| 21 | ScanRunBindingMatched | Boolean, exactly true |
| 22 | RevisionBindingMatched | Boolean, exactly true |
| 23 | RuntimeBindingMatched | Boolean, exactly true |
| 24 | SnapshotBindingMatched | Boolean, exactly true |
| 25 | ProjectPathTrusted | Boolean, exactly true |
| 26 | IndexPathTrusted | Boolean, exactly true |

SHA-256 of this envelope is `CodeGraphProofIdentityHash`. The envelope has no nullable fields and no implicit defaults: every zero/false/true above is explicitly encoded. Measured duration, start/finish/observation times, process ID/exit timing, CPU/memory/IO telemetry, retry count, raw status JSON, diagnostics, stderr/stdout, absolute project/index paths, temporary workspace/lease tokens, and the derived identity hash are excluded. They cannot affect a manifest fingerprint or replay result.

### 4.3 Java-readiness identity

Domain `.../java-readiness/1.0`:

| Tag | Field | Encoding and approved-state invariant |
| --- | --- | --- |
| 1 | RepositoryId | UUID |
| 2 | ScanRunId | UUID |
| 3 | ProfiledRevision | String, exact lowercase 40-hex |
| 4 | SnapshotIdentity | Bytes(32) |
| 5 | RuntimeIdentityHash | Bytes(32) |
| 6 | WorkspaceBindingHash | Bytes(32), SHA-256 of the trusted logical Repository/Profiled-Revision workspace token; never an absolute path |
| 7 | ReadinessSchemaIdentity | String, exactly `java-readiness/1.0` |
| 8 | Outcome | Enum, exactly `Ready` or `NotApplicable`; `Failed` is never manifest identity |
| 9 | JdtLsRelease | String, exactly `jdt-language-server-1.61.0-202608141332` |
| 10 | JdtLsDistributionArchiveHash | Bytes(32), the Story 12.1 approved archive checksum |
| 11 | JavaMajor | UInt64, exactly 21 |
| 12 | StreamJsonRpcVersion | String, exactly `2.25.29` |
| 13 | Transport | Enum, exactly `Stdio` |
| 14 | WorkspaceExclusive | Boolean, exactly true for `Ready`; exactly false for `NotApplicable` |
| 15 | ReadinessFile | nullable String; canonical path for `Ready`, Null for `NotApplicable` |
| 16 | InitializeCompleted | Boolean, exactly true for `Ready`; false for `NotApplicable` |
| 17 | InitializedNotificationSent | Boolean, exactly true for `Ready`; false for `NotApplicable` |
| 18 | ImportState | Enum, exactly `Usable` for `Ready`; `NotApplicable` otherwise |
| 19 | DocumentSymbolCount | UInt64, at least one for `Ready`; zero for `NotApplicable` |
| 20 | RepositoryBindingMatched | Boolean, exactly true |
| 21 | ScanRunBindingMatched | Boolean, exactly true |
| 22 | RevisionBindingMatched | Boolean, exactly true |
| 23 | RuntimeBindingMatched | Boolean, exactly true |
| 24 | SnapshotBindingMatched | Boolean, exactly true |

SHA-256 of this envelope is `JavaReadinessIdentityHash`. Every Boolean/default is explicitly encoded, and tag 15 is the sole nullable field. `NotApplicable` is legal only after the exact inventory proves no eligible Java; it still carries stable Repository/Scan Run/revision/snapshot/runtime bindings and pinned runtime identifiers. Measured duration, timestamps, import progress timing, diagnostic/compilation counts or text, process/exit telemetry, raw LSP messages, absolute roots, data/configuration paths, workspace/lease tokens, failure code, and the derived identity hash are excluded. A failed readiness attempt has bounded audit metadata but no approved readiness envelope.

### 4.4 Prepared Scan Context Snapshot identity

Domain `.../prepared-scan-context-snapshot/1.0`:

| Tag | Field | Encoding and invariant |
| --- | --- | --- |
| 1 | RepositoryId | UUID |
| 2 | ScanRunId | UUID |
| 3 | ProfiledRevision | String, exact lowercase 40-hex |
| 4 | ContextSchemaIdentity | String, exactly `scan-context-snapshot/1.0` |
| 5 | ContentHash | Bytes(32), SHA-256 of the immutable bounded context content under the Story 12.1 content contract |

SHA-256 of this envelope is `PreparedScanContextIdentityHash`. The legacy `ScanContextSnapshot.Id`, whose Story 12.1 derivation includes capture time, is a relational foreign-key value only and is excluded from this envelope, the manifest envelope, pair fingerprints, UUID derivation, and replay equality. `CapturedAt`, creation/approval timestamps, database metadata, and raw context content are likewise excluded. Story 12.2 persists the stable hash additively for its new snapshot row; historical Story 12.1 rows remain valid with no backfill or reinterpretation.

### 4.5 Communication classification

Domain `.../communication-classification/1.0`: tags 1 PreScanResult/ScanRun UUID; 2 nullable CommunicationKind Enum; 3 level Enum; 4 confidence Enum; 5 rule Enum; 6 provenance Enum; 7 canonical path String; 8 Evidence Locator String; 9 occurrence count UInt64; 10 catalog rule identity String. Derived classification ID/fingerprint and ordinals are excluded. The digest is the classification fingerprint.

Retained classification List order uses numeric ranks `Confirmed=0`, `Hint=1`, `Warning=2`, then nullable kind rank `Rpc=0`, `Http=1`, null=255, then rule ASCII ordinal, path ordinal, locator ordinal, and fingerprint-byte lexicographic order.

### 4.6 Prepared Scan audit

Domain `.../prepared-scan-audit/1.0`: tags 1 RepositoryId UUID; 2 ScanRunId UUID; 3 canonical path String; 4 outcome Enum; 5 reason Enum; 6 nullable FileHash Bytes(32); 7 nullable raw byte count UInt64; 8 nullable logical line count UInt64; 9 nullable File Code Unit identity fingerprint Bytes(32); 10 nullable PairSelectionOrdinal UInt64. Derived audit ID/fingerprint, audit ordinal, and timestamps are excluded. The digest is `DetailFingerprint`.

All ten preimage fields are persisted as typed columns; the stored `DetailFingerprint` is never a substitute for tags 1-10. A prepared-only manifest-entry table has non-null manifest, FileCodeUnitId, tag-9 fingerprint, and pair ordinal plus valid unique principal constraint `(ManifestId, FileCodeUnitId, fingerprint, pairOrdinal)`. `Selected` requires non-null tags 9/10 and dependent `PreparedScanManifestEntryId`, with composite FK to that key. `Eligible` requires tag 9 and Null tag 10/entry ID; `Filtered`, `Skipped`, and `Warning` require all three null. The entry ID/FK is excluded from the envelope because tags 9/10 are canonical. No nullable historical CodeUnit property participates in a principal key.

Audit List order is path ordinal; outcome rank `Eligible=0`, `Filtered=1`, `Skipped=2`, `Selected=3`, `Warning=4`; reason ASCII ordinal; DetailFingerprint byte-lexicographic. `AuditOrdinal` is then assigned `1..N` and is encoded by the manifest audit-entry object, not the audit identity envelope.

### 4.7 Pair selection

Domain `.../pair-selection/1.0`: tag 1 coordination UUID; tag 2 selected-entry List in PairSelectionOrdinal order. Each selected-entry Object has tags: 1 RepositoryId UUID; 2 File Code Unit identity fingerprint Bytes(32); 3 final score Int64; 4 selection reason Enum; 5 ManifestOrdinal UInt64; 6 PairSelectionOrdinal UInt64. Both ordinal domains are therefore direct pair-fingerprint inputs, while File Code Unit identity excludes them.

The List must contain exactly PairSelectionOrdinal `1..M`, with canonical FR-032 order. The digest is `PairSelectionFingerprint`.

### 4.8 Prepared Scan Manifest

Domain `.../prepared-scan-manifest/1.0`:

| Tag | Field | Encoding |
| --- | --- | --- |
| 1-4 | RepositoryId, ScanRunId, coordination ID, ProfiledRevision | UUID, UUID, UUID, String |
| 5-8 | requested ref, manifest schema, snapshot identity, runtime identity | String, String, Bytes(32), Bytes(32) |
| 9-12 | CodeGraphProofIdentityHash, JavaReadinessIdentityHash, catalog schema, catalog hash | Bytes(32), Bytes(32), String, Bytes(32) |
| 13 | PreparedScanContextIdentityHash | Bytes(32) |
| 14 | context content hash | Bytes(32) |
| 15-21 | candidate, eligible, filtered, skipped, selected, classification-total, retained-classification counts | UInt64 each |
| 22 | evidence truncated | Boolean |
| 23 | PairSelectionFingerprint | Bytes(32) |
| 24 | manifest-entry List | Repository-local ManifestOrdinal order |
| 25 | audit-entry List | AuditOrdinal order |
| 26 | retained-classification fingerprint List | normative classification order |
| 27 | approval state | Enum (`Approved`) |

Each manifest-entry Object uses tags: 1 File Code Unit identity fingerprint; 2 direct score Int64; 3 final score Int64; 4 selection reason Enum; 5 ManifestOrdinal UInt64; 6 PairSelectionOrdinal UInt64. Each audit-entry Object uses tags 1 AuditOrdinal UInt64 and 2 DetailFingerprint Bytes(32).

Manifest ID, manifest fingerprint, legacy/time-derived ContextSnapshotId, Pre-Scan Result ID/fingerprint, classification/audit/File Code Unit derived IDs, CapturedAt/created/approved timestamps, absolute paths, workspace tokens, and raw proof JSON are excluded. The digest is `ManifestFingerprint`; this breaks every ID/fingerprint/time cycle.

### 4.9 Pair package

Domain `.../prepared-scan-pair-package/1.0`: tag 1 coordination UUID; tag 2 PairSelectionFingerprint Bytes(32); tag 3 manifest List sorted by canonical lowercase UUID text, where each item Object is tag 1 RepositoryId UUID and tag 2 ManifestFingerprint Bytes(32); tag 4 total selected UInt64; tag 5 package schema String `prepared-scan-pair-package/1.0`.

The digest is `PairPackageFingerprint`. Manifest envelopes include PairSelectionFingerprint, never PairPackageFingerprint; the pair package includes manifest fingerprints. This directed dependency is acyclic.

## 5. Deterministic UUID construction

For each persisted deterministic ID, create `CENV("vulgata/story-12.2/deterministic-uuid/1.0", Object{1: Enum(kind), 2: Bytes(artifactFingerprint32)})`, where kind is exactly one of `FileCodeUnit`, `CommunicationClassification`, `PreparedScanAudit`, or `PreparedScanManifest`. SHA-256 the envelope, copy digest bytes `0..15`, set byte 6 to `(byte6 & 0x0f) | 0x80` (UUID version 8), set byte 8 to `(byte8 & 0x3f) | 0x80` (RFC variant), and construct the UUID from those 16 RFC/network-order bytes (`new Guid(bytes, bigEndian: true)` in .NET). The UUID is never an input to its own artifact fingerprint.

Pre-Scan Result identity remains ScanRun ID. Pair coordination identity is inherited from Story 12.1 and is not rederived here.

## 6. Required golden and independent tests

Tests must commit literal envelope hex and SHA-256 vectors for the smallest legal `Complete` CodeGraph proof, `Ready` Java proof, XML-only `NotApplicable` Java proof, and Prepared Scan Context Snapshot identity, plus stable-predicate mutations. The context vector is repeated with two distinct `CapturedAt` values and legacy time-derived IDs but must have identical context, manifest, and pair bytes/digests. The fixture inputs and expected bytes/digests are immutable constants in test source, not values produced during the assertion. The broader vectors cover every scalar including Null/empty distinction; nested objects/lists; each artifact’s smallest legal value; multi-entry manifest/pair; both ordinal swaps; Unicode Git path; UUID with asymmetric bytes; and all corrected specification golden selection cases. One test builder uses the production writer; an independent test-only reader/reconstructor assembles bytes without calling the writer. They must agree with the literal vectors.

Replay tests persist a package, reconstruct every envelope—including both proof identities and the prepared context identity—independently from relational rows, and compare exact bytes/digests/UUIDs. Repeating preparation with different measured durations/telemetry or different context capture time/legacy snapshot ID must produce byte-identical proof/context/manifest/pair identities and return replay; first-commit audit metadata remains unchanged. Missing, extra, reordered, differently normalized, ordinal-swapped, changed stable proof/context binding or content hash, changed-null, or host-endian/Guid-byte-order mutations must conflict without mutation. Mutating excluded duration/telemetry/capture time/legacy ID must not change any identity.

Audit golden evidence specifically records literal canonical bytes and digest for every selected/nonselected nullability shape, each closed outcome/reason shape, deterministic audit UUID, canonical ordering under reversed input, and mutations of tag 9, tag 10, selected FK agreement, and another replay-relevant field. PostgreSQL replay independently reconstructs tags 1-10 from row columns, recomputes the digest, and rejects stored-digest corruption, missing/extra/null-swapped fingerprints, ordinal changes, mismatched selected associations, and ambiguous same-path segments. This evidence precedes the production factory; a post-implementation-only assertion does not satisfy the contract.
