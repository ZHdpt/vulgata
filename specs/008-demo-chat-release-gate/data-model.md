# Phase 1 Data Model: Demo Chat and Citations

These are transient/read-model contracts. No new persistent entity or EF model
is introduced.

## Chat Mode

Closed values: `Business`, `It`.

- `Business`: Chinese business narrative; light theme; no inline code/path/line
  reference; citation detail remains expandable.
- `It`: Chinese technical narrative; dark theme; authorized Repository-relative
  paths and line ranges may be inline.

Switching mode clears the transient question/answer state. It does not save a
session in this deadline slice.

## Prepared Chat Scope

Fields:

- `SystemId`: non-empty.
- `RepositoryIds`: exactly two non-empty, distinct IDs in canonical order.
- `DisplayName` / Repository display names: Web-only authorized labels, never
  accepted as identity proof.

Invariants:

- both Repositories belong to `SystemId`;
- the current user can see the System and both Repositories;
- the pair is backed by current accepted-profile exact relationship evidence;
- access is verified before retrieval and again before render;
- invalid/unauthorized scope is missing-equivalent.

## Chat Question Request

Fields:

- `RequestId`: non-empty request-local ID.
- `Mode`: closed Chat Mode.
- `Scope`: Prepared Chat Scope.
- `Question`: normalized UTF-8 text, 1-2,000 characters, no control characters
  other than normalized whitespace.

The request contains no user-supplied file, automatic-scope flag, provider
credential, database identity, or durable session ID.

## Evidence Packet

A request-local immutable projection produced from one authorized bounded
retrieval read. It records the retrieval snapshot but is not proof of database
currentness after provider latency. Fields:

- `SchemaVersion`: exact supported version.
- `RequestId`, `Mode`, `SystemId`, canonical Repository IDs.
- `Documents`: 1-5 Evidence Documents.
- `Sections`: 1-20 Evidence Sections.
- `SourceAnchors`: 1-32 Evidence Anchors.
- `Relationships`: exactly the required directed accepted-profile
  cross-Repository `REFERENCES` fact plus bounded `GENERATED_FROM` provenance
  needed for the Business Logic Document.
- `FlowEvidence`: bounded ordered flow groups/traces/steps.
- `Fingerprint`: SHA-256 over the canonical packet excluding display-only
  labels.

The packet is rejected, not partially returned, when required evidence is
restricted, stale, superseded, out of scope, over bound, internally
inconsistent, or lacks an available Source Anchor line range.

### Evidence Document

- opaque request-local `DocumentHandle`;
- server-retained `LogicalDocumentId` and current `DocumentVersionId`;
- `RepositoryId`, `DocumentType`, safe title;
- current-version/profile revision facts needed for validation.

Exactly one current Document Version may exist per selected Logical Document.
Database IDs are retained server-side and are not accepted back from the model.

### Evidence Section

- opaque `SectionHandle` and owning `DocumentHandle`;
- kind, canonical order, safe heading/content;
- one or more `AnchorHandle` values;
- any authorized supporting Document handles.

Each section belongs to the same current Document Version as its owner and
contains no restricted target-derived projection.

### Evidence Anchor

- opaque `AnchorHandle`, owning `DocumentHandle`, and server-retained anchor ID;
- authorized Repository-relative path;
- inclusive positive `LineStart <= LineEnd`;
- source hash and Evidence Locator used only for binding/audit.

Absolute, traversal, URI, credential-bearing, missing, or unavailable paths are
invalid. Visible citation locators are derived from this object only.

### Evidence Relationship

- opaque `RelationshipHandle`;
- closed `REFERENCES` or `GENERATED_FROM` type;
- source and target Document handles;
- for the cross-Repository fact: accepted profile/fingerprint, opaque stable
  relationship identity, and exact direction.

Both endpoint Repositories must be selected and currently authorized. A
`RestrictedRelationship` is never valid Chat evidence.

## Chat Synthesis Result

Closed provider output:

- `SchemaVersion`;
- `RequestId`, `Mode`, and exact `EvidenceFingerprint` echo;
- `Selections`: 1-12 unique ordered Candidate Claim handles.

No provider-authored claim text, free-form answer, citation selection, Markdown
link, path, line, HTML, diagnostic, or unknown property is allowed.

### Candidate Claim

Server-owned immutable request projection:

- opaque `CandidateHandle`, bound to request fingerprint and mode;
- current `DocumentVersionId`, `StableSectionId`, `SpanStart`, and `SpanLength`;
- exact retained `Text` and `TextSha256`;
- fixed canonical `AnchorHandles` and required `RelationshipHandles`;
- `SupportSignature` over all preceding identity/support fields.

Business candidates are exact paragraph/list-item spans from safe current BL
`Overview`/`ProcessStep` Sections. IT candidates are exact spans from current
CL `Overview`/`ProcessStep`/`TechnicalDetail`/`Evidence` Sections. Each span is
a substring of persisted `ContentMarkdown`, not generated prose. It is
ineligible without an available same-version Source Anchor or when restricted,
unsafe, stale, out of scope, or mode-inappropriate. The server applies the
Business safe-text/no-code-locator predicate before a candidate is dispatched.

## Currentness Validation Request and Outcome

Provider-independent transient Core contract built by Web from the validated
selected Candidate Claims and their server-retained support. It is never sent to
or authored by the provider. Fields:

- `RequestId`, exact `EvidenceFingerprint`, `SystemId`, and the same two
  canonical selected Repository IDs;
- bounded selected `LogicalDocumentId`/expected-current `DocumentVersionId`
  pairs, including every relationship endpoint required by a selected claim;
- each selected `StableSectionId`, owning identities, `SpanStart`, `SpanLength`,
  and exact `TextSha256`;
- the complete fixed Source Anchor set with server-retained Anchor ID, owning
  Document Version/Repository, safe Repository-relative path, positive
  inclusive range, and source hash;
- the complete required `REFERENCES`/`GENERATED_FROM` support with stable Edge
  identity, type, exact direction/endpoints, and exact-evidence profile/
  fingerprint identities when applicable.

Infrastructure alone implements `ValidateCurrentAsync`. In one bounded
serializable read it requires exact request/fingerprint/scope bounds and proves:

1. every Logical Document still points to the expected current Document
   Version and that version is not superseded;
2. every Stable Section still belongs to that version and its persisted
   `ContentMarkdown` contains the same exact substring at the same offsets and
   SHA-256;
3. every fixed Source Anchor still exists, belongs to the same current Document
   Version and selected Repository, retains safe support data, and has
   `LineStart > 0` and `LineEnd >= LineStart`;
4. every required directed `REFERENCES`/`GENERATED_FROM` Edge, both current
   endpoints, and the accepted exact-evidence profile/direction remain present
   and authorized by the live `RepositoryAccessScope`.

Closed outcomes are `Current` and `MissingOrStale`. Any missing, extra, changed,
unsafe, out-of-scope, unauthorized, superseded, or inconsistent support returns
`MissingOrStale` for the whole answer with no claims or citations. Retrieval's
transaction ends before provider dispatch; this validation transaction begins
only after selection-only provider validation and ends before the final
Repository authorization check, so no transaction crosses provider latency.

### Material Claim

- `ClaimId`: server-created display identifier, unique.
- `Order`: provider-selected contiguous order starting at 1.
- `Text`: exact server-retained Candidate Claim span.
- `CitationHandles`: fixed Candidate Claim support, never provider-selected.

Selection-only validation recomputes the retained in-memory text/support hashes
and Business no-code-locator rule. The subsequent Infrastructure currentness
read proves the same Section substring, fixed handles, current Evidence
Documents, available Source Anchors, and required relationships still exist in
PostgreSQL. Any foreign, unrelated, contradictory-decoy, stale, changed, or
tampered handle/support invalidates the entire result. These predicates are
server-owned rules, not provider-output fields.

## Document Citation

Server-created render model:

- `CitationId`: request-local display ID.
- `ClaimIds`: one or more Material Claims.
- `DocumentTitle` and Document type.
- `RepositoryDisplayName` (authorized label).
- `RepositoryRelativePath`, `LineStart`, `LineEnd` from Evidence Anchor.
- optional authorized section heading.

Business Mode renders a Chinese Document label beside the claim and places the
locator only inside expandable detail. IT Mode may render the same locator
inline. Citation content is never taken from provider-authored text.

## Grounded Answer

Fields:

- mode and complete ordered Material Claims;
- complete Document Citation set;
- generated-at time for transient UI display;
- no durable identity.

State transition:

```text
Idle -> LoadingScope -> Ready
Ready -> Retrieving -> Synthesizing -> ValidatingSelection
ValidatingSelection -> ValidatingCurrentness -> Reauthorizing
Reauthorizing -> Loaded (atomic complete answer)
any active state -> Cancelling -> Ready
any invalid/failure/revoked state -> Error (bounded Chinese message, no answer)
mode/scope change -> Ready (prior transient answer cleared)
```

The UI maps these states to existing `LoadState` values. A stale answer is
cleared before a new request begins and is never restored after a later
failure.

## Ordinary Pass 1 Worker Job Identity

A derived operation identity, not a persisted entity and not a synthetic
runtime-flow member. It is the actual `WorkerAgentJobPacket.JobId` dispatched
for one newly created ordinary Pass 1 job.

### Bound inputs

| Tag | Field | Canonical representation |
|---:|---|---|
| `0x0001` | `ScanExecutionId` | non-empty UUID, 16 RFC-network-order bytes |
| `0x0002` | `ScanRunId` | non-empty UUID, 16 RFC-network-order bytes |
| `0x0003` | `RepositoryId` | non-empty UUID, 16 RFC-network-order bytes |
| `0x0004` | `CodeUnitId` | non-empty UUID, 16 RFC-network-order bytes |
| `0x0005` | `ProfiledRevision` identity/fingerprint | one full lowercase Git object ID of exactly 40 or 64 hexadecimal characters, decoded to exactly 20 or 32 raw bytes |

The identity envelope uses the existing canonical TLV mechanics identified by
`vulgata.prepared-pass2-binding-c14n/1` and a distinct versioned domain:

```text
pass1-job-envelope = ASCII("VULGATA-ORDINARY-PASS1-JOB") || 0x00 || 0x01 || field...
field              = tag:u16be || length:u32be || value:length-bytes
job-id             = UUIDv8-SHA256(pass1-job-envelope)
```

Tags are closed, unique, strictly increasing, and required. There is no text
UUID or text revision in the envelope. Empty UUIDs; abbreviated, uppercase,
odd-length, whitespace-padded, or otherwise malformed revision text;
unknown/duplicate/reordered tags; another version; JSON; and .NET
mixed-endian UUID bytes are noncanonical and rejected rather than normalized.
The UUID projection hashes the complete envelope, takes digest bytes 0-15,
sets byte 6 to `(byte6 & 0x0f) | 0x80`, sets byte 8 to
`(byte8 & 0x3f) | 0x80`, and interprets the result in RFC network order.

### Normative independent golden vector

| Input | Value |
|---|---|
| `ScanExecutionId` | `11111111-2222-3333-8444-555555555555` |
| `ScanRunId` | `66666666-7777-4888-9999-aaaaaaaaaaaa` |
| `RepositoryId` | `bbbbbbbb-cccc-4ddd-8eee-ffffffffffff` |
| `CodeUnitId` | `01234567-89ab-4cde-8f01-23456789abcd` |
| `ProfiledRevision` | `0123456789abcdef0123456789abcdef01234567` |

The complete canonical envelope is exactly 142 bytes and has this lowercase
hex encoding:

```text
56554c474154412d4f5244494e4152592d50415353312d4a4f4200010001000000101111111122223333844455555555555500020000001066666666777748889999aaaaaaaaaaaa000300000010bbbbbbbbcccc4ddd8eeeffffffffffff0004000000100123456789ab4cde8f0123456789abcd0005000000140123456789abcdef0123456789abcdef01234567
```

Its SHA-256 digest is exactly
`6b9362f7193b38d94d9cb2a21ecfe2fd5ffe6a3aaaa00bd8c787e8efccdc71da`.
After UUID version/variant projection the actual Pass 1 Job ID is exactly
`6b9362f7-193b-88d9-8d9c-b2a21ecfe2fd`.

Golden-vector tests construct the expected byte array independently and do not
call the production encoder to create the oracle. Mutation tests change each
bound input separately. Collision-domain tests compare delimiter-ambiguous
input tuples and the Pass 1 magic against every prepared Manifest/Flow magic;
any duplicate derived Job ID or one derived ID associated with different
canonical boundaries is refused.

### Runtime and compatibility rules

```text
canonical current boundary
-> sequential runner derives actual JobId
-> ordinary Pass 1 dispatch/commit
-> valid result clears ScanExecution.InFlightJobId as today
-> current Code Logic Document Version retains existing one-way ID projection
-> runtime planner independently reconstructs boundary and derives JobId
-> planner verifies DocumentVersionId ==
   CreateStableId("pass1-document-version", JobId.ToString("D"))
-> JobId becomes the server-owned Prepared Pass 2 member/trace Job identity
```

The same canonical boundary always yields the same actual Job ID. This is an
identity property only: it does not schedule or permit a retry, duplicate
dispatch, or duplicate commit. Changing any input yields a different derived
identity. A substituted ID, changed input, same-ID/different-boundary
collision, duplicate member identity, or projection mismatch has no resolved
state.

Pre-amendment ordinary Documents whose random historical Job ID cannot satisfy
the forward projection are unsupported legacy evidence. They are not
rewritten or backfilled and fail closed before Pass 2. The clean release run
creates all ordinary Pass 1 jobs under this contract. No EF entity,
configuration, migration, model snapshot, or eighth Pass 2 slot is added.

## Runtime Prepared Pass 2 Binding

A server-owned operation model, not a persisted entity. It binds the reviewed
exact-resolution profile to current product-created records after the accepted
directed relationship is committed and before Pass 2 dispatch.

### Binding request

- exact profile ID, version, and canonical fingerprint;
- live `RepositoryAccessScope`;
- one actual common `SystemId`;
- exactly two alias-bound Repository entries, one consumer and one provider;
- for each entry: stable Git identity, actual `RepositoryId`, `ScanRunId`,
  `ScanExecutionId`, accepted ref, full commit, and Profiled Revision.

No selector, path, database identity, Git URL, credential, or direction may be
supplied outside the accepted profile and these explicit runtime bindings.

### Resolved support

- consumer-owned runtime flow identity;
- exactly two distinct supporting Repository identities in canonical
  consumer/provider order;
- ordered actual deterministic ordinary Pass 1 Worker job, Code Unit, and
  Logical Document identities; every Job ID is independently recomputed from
  its current boundary and verified against the existing current Document
  Version ID projection;
- ordered current Code Logic Document Version identities actually used;
- every required current Source Anchor identity and safe locator/range;
- the one accepted exact directed `REFERENCES` relationship identity and
  evidence identity;
- canonical server-only support fingerprint covering the accepted profile and
  all identities above.

### Authoritative identities and canonical encoding

`PreparedFlowSchemaIdentity` is planner-created and exactly
`vulgata.prepared-pass2-flow/1.0`. It is the only closed mapping from accepted
exact-profile schema/version `1.0`; every other version is refused. It is not a
caller field.

`ManifestId` is the UUIDv8-SHA256 projection of the ASCII-magic
`VULGATA-PREPARED-PASS2-MANIFEST` version-1 envelope:

| Tag | Value |
|---:|---|
| `0x0001` | exact Prepared Flow schema identity text |
| `0x0002` | accepted exact-profile ID text |
| `0x0003` | accepted exact-profile version text |
| `0x0004` | accepted profile fingerprint decoded from exactly 64 lowercase hex characters to 32 bytes |

`FlowId` is the UUIDv8-SHA256 projection of the ASCII-magic
`VULGATA-PREPARED-PASS2-FLOW` version-1 envelope:

| Tag | Value |
|---:|---|
| `0x0001` | derived Manifest UUID, RFC-network-order bytes |
| `0x0002` | accepted occurrence ID text |
| `0x0003` | exact direction literal `consumer-to-provider` |
| `0x0004` | actual common System UUID |
| `0x0005` | consumer binding record |
| `0x0006` | provider binding record |
| `0x0007` | accepted exact relationship UUID |

Each binding record has strictly increasing nested tags: alias text `0x0001`,
stable Git identity text `0x0002`, Repository UUID `0x0003`, ScanRun UUID
`0x0004`, Scan execution UUID `0x0005`, exact ref text `0x0006`, raw decoded
full-commit bytes `0x0007`, and raw decoded Profiled Revision bytes `0x0008`.
The consumer record is always first; sorting by UUID or display name is
forbidden.

UUIDv8-SHA256 hashes the complete canonical envelope, takes digest bytes 0-15,
sets the UUID version-8 and RFC-variant bits in network-order bytes 6 and 8,
and interprets those bytes in RFC network order. .NET mixed-endian
`Guid.ToByteArray()` bytes are never input.

All envelopes use `vulgata.prepared-pass2-binding-c14n/1`:

```text
envelope = ascii-magic || 0x00 || 0x01 || field...
field    = tag:u16be || length:u32be || value
text     = strict UTF-8, no BOM, already NFC
uuid     = 16 RFC-network-order bytes
u32      = four unsigned big-endian bytes
list     = count:u32be || (item-length:u32be || item-bytes)...
```

Tags are unique, strictly increasing, and closed. Required values are
non-empty. Text is rejected rather than normalized. Hex input must already be
lowercase, even-length, and exact-length for its field before raw decoding.

The support fingerprint is lowercase hexadecimal SHA-256 over the
ASCII-magic `VULGATA-PREPARED-PASS2-SUPPORT` version-1 envelope with this exact
top-level tag order:

| Tag | Value |
|---:|---|
| `0x0001` | schema identity text |
| `0x0002` | derived Manifest UUID |
| `0x0003` | derived Flow UUID |
| `0x0004` | profile ID text |
| `0x0005` | profile version text |
| `0x0006` | raw 32-byte profile fingerprint |
| `0x0007` | occurrence ID text |
| `0x0008` | direction literal |
| `0x0009` | System UUID |
| `0x000a` | consumer binding record |
| `0x000b` | provider binding record |
| `0x000c` | relationship UUID |
| `0x000d` | RFC-byte-sorted unique relationship-evidence UUID list |
| `0x000e` | profile-ordered member-record list |
| `0x000f` | exact ordered trace-evidence-record list |

A member record uses nested tags for order `0x0001`, Worker job UUID
`0x0002`, Code Unit UUID `0x0003`, Logical Document UUID `0x0004`, Repository
UUID `0x0005`, current Code Logic Document Version UUID `0x0006`, and
RFC-byte-sorted unique Source Anchor UUID list `0x0007`. A trace-evidence
record uses order `0x0001`, member Worker job UUID `0x0002`, Code Unit UUID
`0x0003`, exact label text `0x0004`, and exact safe locator text `0x0005`.

The fingerprint representation is exactly 64 lowercase hex characters. It is
not a dispatch slot. Uppercase, `0x` prefixes, separators, whitespace,
duplicate/reordered fields or list items, alternative JSON/text
serialization, non-NFC text, unknown version/tags, or mixed-endian UUID bytes
are noncanonical and rejected.

Every required selector has cardinality exactly one. Missing, duplicate,
stale, unauthorized, wrong-System, wrong-profile, wrong-direction, wrong-ref,
wrong-commit, wrong-Profiled-Revision, or inconsistent support has no resolved
state and produces zero Pass 2 dispatches and zero generated Documents.

### Exact seven-slot dispatch projection

The resolved support projects to the existing closed Worker payload with
exactly these seven top-level slots:

```text
Pass2BusinessLogicJobPayload
  PreparedFlowSchemaIdentity
  ManifestId
  FlowId
  SystemId
  RepositoryId                 # consumer owner
  TraceEvidence
  CodeLogicDocumentVersions    # exact ordered versions actually used
```

There is no eighth slot. Scan execution, Worker job, Code Unit, Logical
Document, Source Anchor, relationship, profile/ref/commit, authorization, and
support-fingerprint identities remain server-side. The committer's
`GENERATED_FROM` targets equal the `CodeLogicDocumentVersions` actually used,
whose support spans exactly the two prepared Repositories.

State transitions:

```text
ExactRelationshipCommitted -> Binding
Binding -> Resolved -> DispatchedOnce -> Committed
Binding -> Refused
Resolved -> Refused          # drift before dispatch
DispatchedOnce -> Refused    # drift/rejection before commit
```

`Refused` is fail-closed and terminal for that attempt. The fixed
order/inventory manifest is not production authority; general discovery,
traversal, clustering, scheduling, concurrency, and retry are not states of
this model.

At both `Resolved -> DispatchedOnce` and `DispatchedOnce -> Committed`, the
server re-derives the schema/Manifest/Flow identities and canonical support
bytes from current authoritative state. Any old fixed-manifest identity,
substitution, drift, noncanonical encoding, or fingerprint mismatch takes the
`Refused` transition without repair.

## Final Image Binding

Test/release evidence concept, not a product entity:

- `BuildCount`: exactly `1`;
- `ImageId`: immutable Docker content ID in `sha256:<64 lowercase hex>` form;
- `SourceCommit`, `SourceTree`, `ProductionManifestSha256`: inspected labels;
- `ProvisionerContainerImageId`: must equal `ImageId`;
- `AppContainerImageId`: must equal `ImageId`;
- `ProvisionerRemovedBeforeAppStart`: must be `true`;
- `ProvisionerImageCreated`: must be `false`.

Any tag-only identity, second build, pull/commit, unequal container image ID,
or app start while the provisioner exists makes the run `Failed`.

## Provisioner Input and Result

`ProvisionerInput` is a permission-restricted nonce file mounted read-only. It
contains the nonce database connection, fixed stable container source URIs,
Worker/Chat loopback endpoints, Data Protection application discriminator,
and host-retained generated credentials. It is secret operational input, never
release evidence, and is deleted in cleanup.

`ProvisionerResult` is a closed, size-bounded sanitized record written by atomic
rename to the nonce output mount:

- schema identity and run nonce hash;
- migration/product-stage outcomes;
- product-created System/Repository/Scan identifiers required by the host;
- accepted refs/commits/profile and runtime-support fingerprints;
- ordinary Pass 1 and exact-seven-slot Pass 2 counts/outcomes;
- CodeGraphContext version/requirements-lock identity without process output;
- result timestamp and content hash.

It contains no credential, connection string, key, source/Document text, raw
prompt/response/diagnostic, absolute host path, or unauthorized metadata.
Unknown/duplicate fields, wrong nonce/schema, excess size, non-atomic partial
files, or forbidden values reject the whole result.

## Release-Owned Resource State

| Resource | Allowed transitions |
|---|---|
| Final image | `Absent -> BuiltOnce -> Inspected -> UsedByProvisioner -> UsedByApp -> Removed -> AbsenceProved` |
| Provisioner | `Absent -> CreatedFromImageId -> Completed -> ResultValidated -> Removed -> AbsenceProved` |
| App | `Absent -> CreatedFromSameImageId -> Healthy -> BrowserProved -> Removed -> AbsenceProved` |
| Nonce database | `AbsentProved -> Created -> MigratedAndProvisioned -> AppProofComplete -> ConnectionsTerminated -> Dropped -> AbsenceProved` |
| Source origins | `AcquiredAtExactCommit -> MountedReadOnly -> Unmounted -> Removed` |
| Config/output/work/key/env/browser state | `CreatedNonceOwned -> UsedWithDeclaredAccess -> Removed -> AbsenceProved` |

Failure at any state enters cleanup; it never skips a cleanup transition already
made applicable. Primary and individual cleanup failures remain distinct.

## Release Verification Run

An evidence concept, not a product entity. Fields recorded outside the product
database by the test/release harness:

- candidate commit/tree/production manifest hashes;
- random disposable database name (no credentials);
- accepted prepared aliases, refs, and commits;
- accepted exact-profile fingerprint and runtime Pass 2 support fingerprint;
- proof of one consumer-owned flow, exactly two supporting Repositories, the
  exact seven-slot dispatch shape, and one Pass 2 dispatch/commit;
- ordered gate outcomes and sanitized hashes/log references;
- final image ID and inspected source labels;
- one-build count, provisioner/app image-ID equality, and proof the provisioner
  is a removed container instance rather than a second image/artifact;
- provisioner driver inventory hash, fixed read-only source/driver/config mount
  identities, provider-namespace/PostgreSQL preflight, and sanitized result hash;
- Data Protection key-ring/application discriminator hash identity (never keys);
- Playwright/Chromium versions and interactive-circuit outcome;
- concrete curl health status;
- provisioner/app/provider/image/network/source/config/output/work/key/env/
  browser cleanup outcomes plus database cleanup/result/absence observation;
- known post-demo limitations.

Terminal states: `Passed`, `Failed`, `CleanupFailed`. A primary failure does not
skip cleanup; cleanup failure prevents acceptance even if feature checks passed.
