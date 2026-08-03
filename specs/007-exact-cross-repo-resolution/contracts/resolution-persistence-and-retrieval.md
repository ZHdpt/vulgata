# Contract: Resolution Persistence and Retrieval

## Store port

```text
IExactCrossRepositoryResolutionStore.ApplyAsync(
  ExactResolutionDecisionBatch decisions,
  RepositoryAccessScope scope,
  CancellationToken cancellationToken)
  -> ExactResolutionBatchOutcome
```

The bounded batch contains one decision for every reviewed exact occurrence and unsupported observation, in profile identity order. The store accepts only validated decisions containing complete immutable internal identities and safe bounded values. Internal Repository/ScanRun/Document Version/Source Anchor/Edge/Uncertainty IDs remain typed relational and idempotency material; they are not interpolated into profile bytes or textual evidence/diagnostic/log/exception fields. The outcome reports one closed item result (`Applied`, `AlreadyApplied`, `UncertaintyRecorded`, or `AlreadyRecorded`) per input, or aggregate `Conflict`/`Refused`; it never exposes EF types, `DbSet`, `IQueryable`, SQL/provider diagnostics, tracked entities, or raw internal IDs.

## Authorization/currentness transaction

Within the configured EF execution strategy:

1. require both Repository IDs in current scope;
2. deterministically sort and lock both Logical Document owner rows (`FOR UPDATE` on PostgreSQL; accepted pre-read SQLite write reservation in tests);
3. re-read current pointers and require the supplied consumer/provider Document Versions remain current Code Logic versions;
4. verify Repository/ScanRun/Profiled Revision ownership and both Source Anchor composite memberships;
5. recheck both Repository authorizations;
6. validate the complete decision set before attachment, insert/verify every result, and commit once.

Any currentness, authorization, ownership, decision-count/order, check, unique, or FK failure rolls back the complete transaction. The exact Edge/evidence and reviewed unsupported Uncertainty therefore cannot be partially committed.

## Exact match write

An exact decision inserts as one atomic unit:

- one existing `DocumentEdge` with `EdgeType = References` and differing endpoint Repository IDs;
- one required `ExactCrossRepositoryEdgeEvidence` whose EdgeId is the Edge PK/FK and whose anchor tuples exactly match the Edge endpoints.

The deterministic Edge ID and evidence fingerprint cover the full canonical decision. If both rows already exist and every value matches, return `AlreadyApplied` without changing timestamps. Missing evidence, extra/drifted values, or ID reuse returns `Conflict`; replay never repairs immutable truth.

## Non-exact write

One safe anchored diagnostic inserts one `Uncertainty` with stable internal ID/fingerprint and lowercase closed values. Every automatic Story 11.4 write uses `status=unresolved`; `wontfix` remains reserved for a later explicit human-accepted Dangling Link, and `resolved` remains reserved for later resolution. `sub_status` is exactly one of canonical `cross_repo`, `unknown_target`, or `ambiguous`. Conditions such as variable target, malformed provider, normalization mismatch, multiple providers, or unsupported kind are recorded in `reason_code`; the unsupported observation additionally records its closed `observation_kind`. No plan, model, check constraint, result, or test may introduce another `sub_status`.

The reviewed `account-file-storage-oss-upload-v1` decision requires its authorized current owner Document Version and containing Source Anchor, then inserts nonterminal `unresolved`/`unknown_target` with `reason_code=unsupported_kind` and `observation_kind=file_storage`. Its key covers profile fingerprint, observation identity, internal owner identities, Profiled Revision, selector, kind, and safe locator. It never inserts an Edge/evidence row, remains eligible for later full-V1 resolution/reclassification, and cannot be automatically terminalized. Identical durable content returns `AlreadyRecorded`; drift conflicts. Binding/authorization/revision/owner-mapping failures never call this write.

## Retrieval

Extend the internal current relationship projection with an authorized serialization boundary:

```text
AuthorizedExactRelationship
  relationshipIdentity: opaque stable representation of EdgeId
  edgeType
  exactEvidence: ExactCrossRepositoryEvidenceReadModel?
```

Only when both endpoint Repositories are in the supplied scope, the model includes the opaque stable relationship identity, raw/normalized identifiers, protocol, profile/occurrence provenance, exact confidence, and both safe Evidence Locators. It does not serialize Repository, ScanRun, Document Version, or Source Anchor PK/FKs; the locators provide bounded Source Anchor provenance without embedding raw internal IDs in evidence text. It contains no source text, Git URL, absolute path, credential, System context, or raw diagnostic.

When either endpoint Repository is not authorized, retain the existing `RestrictedRelationship` only. It exposes no relationship identity, Edge ID/type, endpoint, profile, occurrence, anchor, locator, Repository, Document Version, title, content, or behavior. Existing fail-closed textual projection rules remain unchanged.

Uncertainty retrieval similarly requires its owner Repository and any known target Repository in scope. Unauthorized/missing source returns null/empty indistinguishably; unauthorized target metadata is omitted rather than partially scrubbed.

## Relational requirements

- `ExactCrossRepositoryEdgeEvidence.EdgeId` PK/FK with restricted delete.
- Composite consumer/provider Source Anchor FKs.
- Unique evidence fingerprint and closed checks for protocol/resolution/confidence/schema/hash lengths.
- `Uncertainties` owner composite FK, optional anchor composite FK, optional target Repository FK, unique fingerprint, and closed lowercase status/canonical-sub-status/reason/observation-kind checks.
- Existing DocumentEdge unique key/type constraints and all Document/Version Chain delete restrictions remain.

These PK/FK/index columns are required internal relational state and are not a violation of SC-006. SC-006 applies to serialized profile values, textual evidence/diagnostic/log/exception content, and unauthorized returned graph data; the authorized read model's sole serialized identity is the opaque stable `relationshipIdentity` above.

## Security inspection

Tests scan profile bytes, returned models, captured logs/exceptions, and new durable text columns for:

- credentials/tokens/private keys;
- absolute drive/UNC/Linux/WSL/container paths;
- Git URLs/remotes;
- plaintext source/code fences/stack traces/provider diagnostics;
- internal relational IDs embedded in profile or textual evidence/diagnostic/log/exception fields;
- any relationship identity or Repository/endpoint/evidence/business detail in an unauthorized result.

Any positive fixture is rejected before attachment/persistence and never echoed.
