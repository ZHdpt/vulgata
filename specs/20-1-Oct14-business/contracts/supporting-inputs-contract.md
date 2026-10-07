# October supporting-input provenance agreement

Status: canonical declaration ed0f2600ecc9bd4896a2c91bc14c36eec93fe35f accepted by Scan through Integrator on 2026-10-07; integrated at af5520d2868b310df9444b272ed4cb5175f38628. Behavioral implementation and qualification remain separate.
This corrects the existing whole-Repository source-tool/Knowledge boundary. It does
not add a tool, source root, Git capture, repository copy, tree-wide hash, provider
allowance, general provenance framework or October table.

## Primary ownership and verified supporting content

Keep the existing positional `Contributor` constructor and enum values 0–3. Add a
non-positional nullable `PreparedOrigin SourceOrigin`, omitted from JSON when null.
A legacy primary Source contributor retains its existing bytes/digest. A supporting
Source contributor carries its full exact origin: Repository, approved root binding,
normalized relative path, source digest and prepared version. Mirrored contributor
Repository/root/digest fields must agree. Reject the property on advisory or metadata
contributors. Equal bytes at different paths are distinct dependencies.

Primary-null and an explicit exact-primary SourceOrigin are canonical aliases and
must not count twice. Normalize their paths and representation before hashing and
persistence; do not erase a different path merely because its bytes match. Preserve
old single-file proposal digest bytes with golden compatibility tests. Existing exact
raw-duplicate rejection is not a license to omit a distinct source dependency.

The host constructs the complete actual-input contributor ledger, including uncited
support, before computing the proposal digest and issuing completion. Model output
cannot supply authority or decide that the ledger is complete. Business's existing
source verifier checks every distinct content origin against exact stable bytes,
identity, digest, version and line bounds under the original authorized lifetime.
The proposed ceiling is 128 TOTAL unique content origins including the primary;
existing time/read/input budgets narrow it. Overflow or unverifiable content stops
before input disclosure; there is no truncation or additional crawl/budget grant.

Semantic-unit anchors, identity, admission and FileCoverage remain primary-file owned.
Only claim, observation and Uncertainty evidence may use a verified supporting anchor.
Such an anchor requires the exact full-origin contributor, not a legacy primary
contributor that happens to share its digest. No supported source evidence is created
from a filename, directory result or metadata digest.

## Existing prepared binding; no new snapshot platform

The existing October setup records batch, Repository and RootBindingIdentity, plus
source-approval identity/subject/purpose/expiry. It has no immutable tree-version
registry. For the Scan producer, the proposed `PreparedVersion` mapping is its existing
ApprovalIdentity as a host-derived root-session provenance label, bound by the trusted
adapter to the complete batch/Repository/root/subject/purpose/approval tuple.
It is neither an immutable tree snapshot, Git revision nor authorization token.
Context renewal under that exact approval does not invent another version. Changed
file bytes or identity invalidate content verification even when the label is unchanged.
Other producers are not required to use an equal version string; their trusted verifier
must establish their documented exact existing binding semantics.

Initial acceptance requires current source grants and live complete-contributor
permission. Committed replay uses retained provenance and fresh Knowledge authorization;
it does not reopen an expired source grant or re-read current bytes to rewrite history.

## Metadata is a separate dependency kind

Append `ContributorKind.SourceMetadata = 4`. Add non-positional nullable
`Contributor.MetadataOrigin`, omitted when null and mutually exclusive with SourceOrigin:

```csharp
SourceMetadataOrigin(Guid RepositoryId, string RootBindingIdentity,
    string PreparedVersion, SourceMetadataOperation Operation,
    string NormalizedScopePath, string ResultDigest)
```

Closed operations: List=0, ReadTextRange=1, SearchText=2, JavaDefinition=3,
JavaReferences=4, CodeGraphExplore=5. Preparation/discovery remains host control.
The explicit scope `.` means Repository root; otherwise require a normalized contained
relative path without absolute paths or dot segments. Null never means root.

`OctoberKnowledgeInputPolicy.ComputeMetadataResultDigest(origin, safeRequestJson, admittedProjectionJson)` will compute ResultDigest from canonical JSON containing domain
`october/source-metadata/1`, the Repository/root/version/operation/scope identity,
canonical SAFE REQUEST parameters (at most 64 KiB, further narrowed by the actual tool schema/grant), and the exact bounded filtered metadata projection
actually admitted as input. Parameters include the actual query/literal, byte range,
Java position and page/max bounds where applicable. Include status, returned names and
typed locations, omissions and limitations; preserve array order. Hashing an earlier
unfiltered/untruncated result or omitting request parameters is not equivalent.
Hidden/excluded paths and counts must not enter the disclosed projection.

The exact digest object is `{domain:"october/source-metadata/1",repositoryId,rootBindingIdentity,preparedVersion,operation,scopePath,request,projection}`. `request` and `projection` are parsed JSON objects from the two helper arguments; `ResultDigest` itself is excluded. Use `OctoberKnowledgePolicy.SerializeCanonical` and `Hash`, preserving array order and exact string contents, with integer enum values. Canonical request and projection UTF-8 byte counts must each fit their declared ceilings; the original tool schema/grant may be smaller.

The trusted completion ledger authenticates the record and every returned location's
association with the approved prepared root; a scope path alone is not proof. Mirrored
Contributor Repository/root/ContentDigest fields must equal metadata identity/ResultDigest.
Here ContentDigest denotes this explicitly typed metadata-result digest, never file bytes.
At most 128 metadata observations and the existing 64-KiB result ceiling apply, further
narrowed by the original grant (including its operation/time/aggregate limits). There
is no requirement to read/hash a directory, missing path or unreadable file as content.

Mixed results retain both metadata observations and verified content origins. Ordinary
typed index/navigation metadata does not require hashing all underlying Repository files.
Actual source text/excerpts and behavioral source evidence require verified content origins.
Do not infer identities by parsing arbitrary free text. The current native CodeGraph
adapter returns contained typed Locations/Excerpts and leaves Text null; verify only
actual excerpt files, with metadata provenance for locations/status/limitations. A future
structure-only Text result would require a positive trusted producer/root-association
contract; no such future adapter work is included here. Unsupported free-form Text is
withheld before model input with an honest limitation.

SourceMetadata supplies disclosure lineage only. It cannot satisfy a SourceAnchor,
positive SourceSupported evidence, file completion, a REFERENCES proof or a claim that
unobserved content is absent. An evidence-empty qualified Uncertainty is permitted if
its complete metadata dependency remains retained and its observational limits are honest.

## Acceptance, retention and consumer effects

Existing contributor and operation JSON columns retain the full new fields. No migration
or competing consumer schema is needed. The proposal hash and completion proof bind the
complete ledger; canonical null omission preserves legacy single-file bytes. Every source/
advisory branch must handle SourceMetadata explicitly: structural validation, setup/live
permission, replay, publication, persisted reads and protected dependency capsules. Never
fall through a non-Source branch and reinterpret metadata as advisory.

Live metadata authorization uses the same current Repository/root/subject/purpose/mode
boundary as source dependencies. It does not grant exact source access or bypass BL-only
projection. Chat and Explorer receive owner-protected complete unions, not reconstructed
citation sets. Stored replay authorizes the retained complete union before conflict/result
disclosure and preserves same-key exact reconciliation without source re-verification.

Required controlled verification: old digest compatibility; primary alias deduplication;
same-byte distinct paths; uncited content/metadata retention; wrong root/version/path/line
bounds; forged/missing metadata ledger and request binding; limits without truncation;
metadata-only missing/directory results without file hashing; exclusion/non-disclosure;
replay after source-verifier expiry; full publication/read/capsule/live revocation behavior.
Actual Scan mapping and source-frame qualification remain owner prerequisites. Business controlled implementation and review are recorded in quickstart.md; actual Scan/native/provider qualification remains separate.
