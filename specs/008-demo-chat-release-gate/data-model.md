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

## Release Verification Run

An evidence concept, not a product entity. Fields recorded outside the product
database by the test/release harness:

- candidate commit/tree/production manifest hashes;
- random disposable database name (no credentials);
- accepted prepared aliases, refs, and commits;
- ordered gate outcomes and sanitized hashes/log references;
- final image ID and inspected source labels;
- Data Protection key-ring/application discriminator hash identity (never keys);
- Playwright/Chromium versions and interactive-circuit outcome;
- concrete curl health status;
- database cleanup attempted/result and absence observation;
- known post-demo limitations.

Terminal states: `Passed`, `Failed`, `CleanupFailed`. A primary failure does not
skip cleanup; cleanup failure prevents acceptance even if feature checks passed.
