# Contract: Story 12.3 Canonical Envelopes and Deterministic Identities

This contract extends, but does not change, Story 12.2 `canonical-envelope/1.0`. Implementations must reuse its `CENV` framing, type tags, big-endian lengths/integers, RFC/network-order UUID bytes, NFC strings, list/object rules, SHA-256 hashing, and UUIDv8 bit construction. JSON serialization, database ordering, reflection order, culture, timestamps, concurrency, completion order, lease tokens/expiry, job/pair StateVersions, activation identity, probe tickets, and `.NET Guid.ToByteArray()` are never content/Document/display identity inputs. Lease/version values remain mandatory conditional authorization inputs where the runtime and navigation contracts require them.

## 1. Domain prefix and enum encoding

Every domain string is ASCII and exact. Every enum is encoded by its exact case-sensitive name as a Story 12.2 canonical `Enum`. Nullable values use the Story 12.2 `Null` type; empty values are not substitutes for null.

## 2. Pass 1 pair execution

Domain: `vulgata/story-12.3/pass1-pair-execution/1.0`.

| Tag | Field | Type |
| --- | --- | --- |
| 1 | coordination ID | UUID |
| 2 | System ID | UUID |
| 3 | pair package fingerprint | Bytes(32) |
| 4 | pair selection fingerprint | Bytes(32) |
| 5 | ordered manifest binding list | List(Object) |
| 6 | ordered prompt identity binding list | List(String), one per manifest binding |
| 7 | document schema identity | String, exactly `code-logic/file-v2` |
| 8 | ordered analysis-runtime identity hash binding list | List(Bytes(32)), one per manifest binding |

Each manifest binding object has tags 1 Repository ID UUID, 2 Scan Run ID UUID, 3 Scan Execution ID UUID, 4 manifest ID UUID, 5 manifest fingerprint Bytes(32), 6 Profiled Revision Bytes(20 or 32), and 7 Prepared Scan Context identity hash Bytes(32). The manifest, prompt, and runtime lists use the same Story 12.2 canonical Repository order and each has exactly two entries. This preserves distinct accepted child bindings when the two Scan Executions do not share a prompt/runtime identity. SHA-256 is `Pass1PairFingerprint`; its UUIDv8 with kind `Pass1PairExecution` is the pair execution ID. Concurrency, state, counts, probe schedule, warning inventory, and timestamps are excluded.

### 2.1 Active concurrency change

Domain: `vulgata/story-12.3/pass1-concurrency-change/1.0`.

Tags: 1 pair execution ID UUID; 2 expected pair StateVersion UInt64; 3 previous limit UInt64; 4 effective limit UInt64; 5 resulting StateVersion UInt64; 6 resulting ConcurrencyRevision UInt64; 7 authorized principal identity String. SHA-256 is `ChangeFingerprint`; plus kind `Pass1ConcurrencyChange` produces the UUIDv8. Both limits are 1-4 and unequal, and resulting StateVersion is expected plus one. Timestamp, active lease count, job state, and completion order are excluded. Initial/effective concurrency remains excluded from pair, job, Document, projection, reconciliation, and display identities.

## 3. Job and Document identities

The `ScanJobExecution.Id` remains exactly the ID returned by accepted Story 12.1 `ScanJobExecution.Create(scanExecutionId, repositoryId, scanRunId, fileCodeUnitId, manifestOrdinal, ...)`; Story 12.3 does not introduce another job identity.

### 3.1 Logical Document

Domain: `vulgata/story-12.3/code-logic-logical-document/1.0`.

Tags: 1 Repository ID UUID; 2 canonical Repository-relative path String; 3 segment ordinal UInt64. SHA-256 plus deterministic kind `CodeLogicLogicalDocument` produces the UUIDv8. Profiled Revision, Scan Run, hashes, segment count, and job ID are excluded so later exact versions remain in one Version Chain.

### 3.2 Document Version

Domain: `vulgata/story-12.3/code-logic-document-version/1.0`.

Tags: 1 Scan Job Execution ID UUID; 2 File Code Unit identity fingerprint Bytes(32); 3 schema identity String `code-logic/file-v2`. SHA-256 plus kind `CodeLogicDocumentVersion` produces the UUIDv8. The result content and completion time are excluded; a different result for the same job is a conflict, not a new identity.

## 4. Source Anchor identity

Domain: `vulgata/story-12.3/source-anchor/1.0`.

| Tag | Field | Type |
| --- | --- | --- |
| 1 | Document Version ID | UUID |
| 2 | File Code Unit ID | UUID |
| 3 | canonical path | String |
| 4 | byte start, zero-based inclusive | UInt64 |
| 5 | byte end, zero-based exclusive | UInt64 |
| 6 | line start, one-based inclusive | UInt64 |
| 7 | line end, one-based inclusive | UInt64 |
| 8 | Source Hash | Bytes(32) |
| 9 | exact Evidence Locator | String |

SHA-256 plus kind `SourceAnchor` produces the UUIDv8. Anchor keys such as `a001` are local JSON references and are excluded.

## 5. Navigation request and receipt identities

### 5.1 Request

Domain: `vulgata/story-12.3/navigate-code-request/1.0`.

Tags: 1 job ID UUID; 2 tool-attempt ordinal UInt64; 3 operation Enum; 4 source File Code Unit ID UUID; 5 target locator object; 6 requested depth UInt64; 7 parent observation identity nullable Bytes(32).

Target locator object tags are: 1 nullable canonical path String; 2 nullable symbol String; 3 nullable position object (line UInt64, UTF-16 character UInt64); 4 nullable exact line range object; 5 nullable expected source hash Bytes(32). Operation-specific validation rejects unused or missing forms before hashing.

Tool-attempt ordinal is 1-8. Requested depth is 1-3 for an evidence-bearing call and exactly 4 only for a metadata-only Depth refusal; no fourth-hop analysis occurs.

### 5.2 Evidence descriptor

Each response descriptor is one closed object:

- `Document`: tags 1 kind Enum, 2 Logical Document ID UUID, 3 Document Version ID UUID, 4 path String, 5 target Source Hash Bytes(32), 6 section ID list, 7 Source Anchor ID list.
- `Source`: tags 1 kind Enum, 2 path String, 3 symbol nullable String, 4 line range object, 5 byte range object, 6 Source Hash Bytes(32), 7 Evidence Locator String.

Descriptors are sorted by kind (`Document` before `Source`), canonical path, start byte, end byte, symbol, then identity bytes. Duplicate semantic targets are removed before the result count is calculated.

### 5.3 Receipt

Domain: `vulgata/story-12.3/navigate-code-receipt/1.0`.

Tags: 1 nullable request digest Bytes(32); 2 job ID UUID; 3 receipt ordinal UInt64; 4 nullable tool-attempt ordinal UInt64; 5 nullable operation Enum; 6 nullable depth UInt64; 7 outcome Enum (`DocumentEvidence`, `SourceEvidence`, `Unavailable`, `BoundExceeded`); 8 nullable bound kind Enum (`Calls`, `Results`, `EvidenceBytes`, `Depth`); 9 descriptor List; 10 result count UInt64; 11 exact returned UTF-8 evidence byte count UInt64; 12 accepted-call-count-after UInt64; 13 evidence-bytes-after UInt64. SHA-256 is the receipt fingerprint and plus kind `NavigationReceipt` produces its UUIDv8.

For a Results/EvidenceBytes/Depth `BoundExceeded`, request digest, tool-attempt ordinal, operation, and depth are present, descriptors/results/evidence bytes are empty/zero, and the attempt counter—not the accepted-call/evidence counters—advances. When either accepted-call or tool-attempt count first reaches eight and disables further calls, the same budget transaction appends at most one Calls `BoundExceeded` marker with null request/tool-attempt/operation/depth and zero descriptors/results/evidence bytes; no ninth tool invocation occurs. All other outcomes have null bound kind and present request/tool-attempt/operation/depth. Raw excerpt text is included only in successful returned response bytes and excluded from the durable receipt; exact path/range/hash descriptors bind it for later reread.

## 6. Stable observation identity

Domain: `vulgata/story-12.3/reference-observation/1.0`.

| Tag | Field | Type |
| --- | --- | --- |
| 1 | source Document Version ID | UUID |
| 2 | source File Code Unit ID | UUID |
| 3 | relationship kind | Enum, exactly `References` |
| 4 | source claim locator | String |
| 5 | normalized target symbol | nullable String |
| 6 | target canonical path | nullable String |
| 7 | target Source Hash | nullable Bytes(32) |
| 8 | target line range | nullable Object |

SHA-256 is `ObservationFingerprint`; plus kind `ReferenceObservation` produces its UUIDv8. Whether evidence was a current Document or source fallback, navigation receipt ID, navigation depth, outcome classification, prose, timestamps, and completion order are excluded. At least target symbol or target path is required.

## 7. Projection identities

### 7.1 Fixed section groups

The projector creates only nonempty groups in this exact order:

1. `Overview`
2. `InternalUnits`
3. `ExecutionAndDataFlow`
4. `BusinessAndCodeRules`
5. `DataAccess`
6. `CommunicationEvidence`
7. `References`
8. `DeferredObservations`
9. `AssumptionsAndUnresolvedEvidence`

Section identity domain `vulgata/story-12.3/document-section/1.0` has tags 1 Document Version ID UUID and 2 group Enum. Its UUIDv8 kind is `DocumentSection`. Section order is the compact 1-based order after empty groups are omitted; group identity does not depend on that compact order.

### 7.2 Index Entry

Domain `vulgata/story-12.3/index-entry/1.0`: tag 1 Document Version ID UUID, tag 2 Enum `CodeLogic`. Its UUIDv8 kind is `IndexEntry`.

### 7.3 REFERENCES Edge

Domain `vulgata/story-12.3/references-edge/1.0`: tags 1 source Document Version ID UUID, 2 target Document Version ID UUID, 3 Enum `References`. Its UUIDv8 kind is `ReferencesEdge`. Observation identity is excluded so direct and reconciled evidence converge on the same Edge.

## 8. Content and commit fingerprints

`ContentJson` is written from the validated semantic object using the exact property order and array orders in `code-logic-file-v2.schema.json` and the projection contract. It is UTF-8 without BOM, compact (no insignificant whitespace), NFC, and LF-only. `ContentFingerprint` is SHA-256 of those exact bytes.

Domain `vulgata/story-12.3/code-logic-commit/1.0` contains:

1. pair execution ID UUID;
2. job ID UUID;
3. Logical Document ID UUID;
4. Document Version ID UUID;
5. ContentFingerprint Bytes(32);
6. ordered Source Anchor fingerprint list;
7. ordered direct REFERENCES target Document Version UUID list;
8. ordered Document Section projection fingerprint list;
9. Index Entry projection fingerprint Bytes(32);
10. ordered navigation receipt fingerprint list referenced by accepted claims;
11. ordered Communication Kind Classification fingerprint list referenced by accepted claims.

SHA-256 is `CommitFingerprint`. Version number, current/superseded pointer, database timestamps, JSONB physical form, provider text, and completion order are excluded.

## 9. Canonical ordering

- UUID ordering for lists is RFC/network-order bytes unless a Story 12.2 ordinal is named.
- Paths and normalized symbols use ordinal UTF-8 byte comparison after NFC.
- Source Anchors: byte start, byte end, line start, line end, Evidence Locator.
- Known references and deferred observations: observation fingerprint bytes.
- Direct and reconciled Edges: source Document Version UUID bytes, target UUID bytes, Edge type.
- Warning inventory: Pair Selection Ordinal.
- No database query without an explicit one of these orders is an identity/projection source.

## 10. Literal-vector and replay evidence

Tests must contain immutable literal envelope hex, digest, and UUID values for the smallest legal pair execution, active concurrency change, Logical Document, Document Version, Source Anchor, navigation request/source-bearing receipt/each bound-refusal receipt form, observation, section, index, Edge, and commit. Include an asymmetric UUID, Unicode path/symbol, 40- and 64-hex revisions, 4->1 concurrency change, null versus empty target fields, source versus current-Document evidence for one observation, first depth-four refusal, Calls marker, and reversed input orders.

One test path uses the production codec; an independent test-only reconstructor must reproduce every byte without calling it. PostgreSQL replay reconstructs all fingerprints from scalar rows and authoritative JSON rather than trusting stored digest columns. Missing, extra, reordered, renormalized, host-endian, duplicate, changed-hash/range/receipt/classification, or stored-digest corruption conflicts with zero mutation. Concurrency, completion time, provider timing, audit timestamps, and evidence form variation must leave the defined identities unchanged.
