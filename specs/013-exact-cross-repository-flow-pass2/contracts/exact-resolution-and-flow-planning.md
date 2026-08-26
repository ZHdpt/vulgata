# Contract: Exact Resolution and Endpoint-Centered Flow Planning

## Entry boundary

The operation accepts only a server-resolved Story 12.3 pair that is `ReadyForResolution`, its current authorization scope, and a cancellation token. Caller input cannot supply System, Repository, Profiled Revision, endpoint, protocol, Document, Edge, catalog, provider role, or Flow Group membership.

The pair must reconstruct exactly:

- one Two-Repository Scan Coordination in one System;
- two distinct current Repositories and their accepted Prepared Scan Manifests;
- two exact Profiled Revisions and terminal eligible Pass 1 outcomes;
- current Code Logic Document Versions, Source Anchors, Document Sections and exact same-Repository REFERENCES Edges;
- current Confirmed Communication Kind Classifications and the exact closed Communication Pattern Catalog identity.

Any missing trusted relation, changed hash/revision/current pointer, or authorization loss rejects start. No partial decision or Flow Group is exposed.

## Candidate extraction

### Consumer

A Consumer candidate requires all of:

1. one current Code Logic Document Version in the pair;
2. one validated `communicationEvidence` item whose kind is supported by at least one exact current Confirmed classification binding;
3. one validated `deferredObservation` with `outcome = CrossRepository` and the same source evidence boundary;
4. at least one common current Source Anchor between the communication and deferred items;
5. a protocol-specific raw endpoint candidate from `targetSymbol` or `targetRepositoryRelativePath` that passes the closed normalizer;
6. a canonical execution consumer-entry identity derived from Document Version, Source Anchors, deferred observation order and fingerprint;
7. a separate `ConsumerEntryStableKey` derived from consumer Repository, normalized repository-relative path, closed source-derived containing qualified type/member signature or catalog-declared stable locator, closed communication-site kind, and structural occurrence ordinal among supported outbound sites within that owner, excluding protocol/endpoint target value and scan/current-evidence coordinates.

The free-form `observation`, Index Entry/search text, model prose, raw graph proximity, and optional direct analysis are excluded.

### Provider

A Provider candidate requires all of:

1. one current Confirmed matched-configuration classification in the pair;
2. a current XML Code Logic Document Version and Source Anchor containing the exact classification Evidence Locator;
3. successful exact revision-bound source read whose hash matches the anchor;
4. safe XML projection and closed catalog re-evaluation that reproduces the same catalog rule, locator, protocol and exactly one raw identifier;
5. successful protocol-specific normalization.

A Communication Kind Classification alone is never a Provider candidate. Each reproduced locator/classification/anchor match is a contributing provider evidence observation. For a given protocol/key, observations are canonically sorted and deduplicated by complete evidence identity, then aggregated by:

```text
ProviderIdentity =
  SystemId
  RepositoryId
  ScanRunId
  ProfiledRevision
  current provider XML DocumentVersionId
```

One aggregated Provider candidate retains the complete contributing observation set, rule/locator/anchor/classification identities and aggregate fingerprint. Evidence multiplicity for that identity is not provider multiplicity.

## Normalization

The match key is the ordered pair:

```text
(protocol, normalized endpoint identifier)
```

Both components are mandatory and compared ordinally.

### Custom RPC

- trim surrounding Unicode whitespace;
- normalize Unicode to NFC;
- remove a prefix only when the exact catalog rule declares it and it is present exactly once;
- require the remaining closed qualified-symbol/endpoint grammar;
- preserve case and complete qualification.

### Safe HTTP

- require a catalog-identified absolute `http`/`https` URI or origin-form path;
- reject userinfo, query, fragment, backslash, control, ambiguous dot segment, invalid UTF-8, and percent-encoded reserved character;
- lowercase scheme and ASCII host and remove the default port;
- preserve path case and escapes;
- change trailing-slash/path prefix only under an exact catalog normalization declaration.

Rejected/unsupported forms return `unsafe_normalization`; they are not errors that authorize a guess.

## Resolution algorithm

1. Validate and canonicalize the complete Consumer and evidence-granular Provider observation sets.
2. Group Provider observations by the full protocol/key and `ProviderIdentity`; deduplicate exact occurrences and construct one complete aggregated candidate/fingerprint for every distinct current provider Document Version.
3. For each Consumer in candidate-fingerprint order:
   - exactly one distinct current authorized Provider identity after aggregation -> `ExactMatch`;
   - zero -> `Uncertainty/no_provider`;
   - more than one distinct current provider Document Version -> `Uncertainty/multiple_providers`;
   - any closed conflict/unsupported/unsafe case -> the corresponding Uncertainty reason.
4. Evaluate both Repository directions using the same set and ordering.
5. Emit a canonical decision batch and fingerprint.

Graph distance, provider Repository order, evidence-row count, implementation availability, LLM output and optional direct analysis do not affect eligibility or tie-breaking. Removing, adding or changing a distinct contributing observation changes the aggregated evidence fingerprint for replay even when cardinality remains one.

## Resolution transaction

The store locks/revalidates the pair and every decision owner. It constructs one durable `CrossRepositoryResolutionDecision` per consumer candidate, then atomically:

1. groups ExactMatch decisions by `(consumer DocumentVersionId, provider DocumentVersionId, References)`;
2. acquires tuple-scoped locks in canonical order and inserts exactly one deterministic-ID directed document-level REFERENCES Edge when absent, or reuses the actual ID of a fully validated existing/legacy Edge already owning that tuple;
3. inserts/replays every ExactMatch decision with its independent pair/consumer-entry/protocol/endpoint/provider evidence and many-to-one Edge FK;
4. inserts/replays every unresolved decision with exactly one owned Uncertainty and no Edge;
5. inserts/replays every normalized provider-evidence-contribution row under its decision, never under the shared Edge;
6. lets a newer exact decision resolve a prior Uncertainty only through an explicit `ResolvedUncertaintyId` link with matching System/consumer stable entry/protocol/endpoint and full currentness validation;
7. persists no Flow Group if the complete decision/Edge/Uncertainty/contribution batch cannot be reconstructed.

Decision slot identity is pair + current consumer candidate/entry + protocol + normalized endpoint. Exact outcome shape adds the selected Provider identity; unresolved shape adds its closed reason. Edge identity contains only source Document Version + target Document Version + `References`. Therefore two endpoints or two consumer entries owned by the same consumer Document Version and targeting the same provider Document Version persist two decisions against one Edge.

Exact replay verifies the complete decision-to-Edge/Uncertainty graph and every contribution owner/provider/ordinal/classification/anchor/catalog/locator/normalization field, and writes nothing. A matching digest with a missing, extra, changed or reordered decision/contribution or a wrong Edge binding is not replay. Same decision slot with changed outcome/evidence returns conflict with zero writes. Concurrent identical starts converge on the same decisions/Edges; concurrent distinct decisions reuse an existing Edge only after exact tuple/type validation.

Legacy one-to-one `ExactCrossRepositoryEdgeEvidence` rows remain unchanged/readable and receive no v2 backfill because their endpoint/entry multiplicity cannot be reconstructed safely.

## Flow Group planning

Only current v2 `ExactMatch` decisions may seed a group. One decision seeds at most one Flow Group. A document Edge alone, including one shared by several decisions or one carrying only legacy evidence, is never a group seed.

### Execution-instance identity

Canonical identity includes:

- `SystemId`;
- protocol;
- normalized endpoint identifier;
- consumer entry identity;
- exact resolution decision identity;
- provider Repository;
- Pass 1 pair execution;
- planner schema identity.

It intentionally changes with the Pass 1 pair/current exact evidence and is the Flow Group row/job identity.

### Cross-scan Logical Document identity

Every candidate also receives `ConsumerEntryStableKey`, `LogicalKeySchemaIdentity = business-flow-logical-key/1.0`, and:

```text
LogicalDocumentStableKey =
  "business-flow/v1/" + lowercase_sha256(canonical(
    "business-flow-logical-key/1.0",
    SystemId,
    consumer RepositoryId,
    provider RepositoryId,
    protocol,
    normalized endpoint identifier,
    ConsumerEntryStableKey))
```

Pair/execution, Scan Run, Profiled Revision, current Document Version, Source Anchor, Edge, evidence and planner identities are excluded. Thus a later pair with new current Code Logic Document Versions but the same durable semantic entry/endpoint resolves the same consumer-owned Business Logic Logical Document. A changed endpoint/protocol/Repository/System/semantic entry yields a new key and leaves the old chain intact.

Execution/planner/resolution schema revisions do not change `business-flow-logical-key/1.0`. Changing semantic-locator or endpoint canonicalization rules requires a new logical-key schema identity; it cannot reuse an existing chain without an explicit reviewed equivalence alias/data migration.

### Required core

A complete group requires exactly:

1. the current consumer Code Logic Document Version;
2. the current exact-match resolution decision and its complete contribution evidence;
3. the decision's Cross-Repository REFERENCES Edge, which may be shared with other decisions;
4. the current provider XML Code Logic Document Version targeted by the Edge and selected by this decision;
5. exactly one current provider implementation Code Logic Document Version targeted by a validated same-Repository REFERENCES Edge from the provider XML Document.

A missing, multiple, stale, unauthorized, unanchored, cross-revision or cross-System core member yields `Incomplete`; it never changes the exact provider decision.

### Optional one-hop evidence

Optional members may be added only through current validated same-Repository REFERENCES Edges adjacent to a core member. They are sorted by role/repository/document/edge identity. Cycles and duplicates collapse by exact identity. No second hop is evaluated.

### Bounds and omission

- exactly two Repositories;
- at most eight distinct Code Logic Document Versions;
- at most sixteen ordered evidence steps;
- at most ten Flow Groups;
- one same-Repository relationship hop.

If one candidate's complete canonical membership exceeds a per-group bound, the group is `BoundExcluded`; no truncated complete group is emitted. At most the first ten canonical group candidates become rows. Later candidates become `Pass2Omission/flow-group-bound` records.

## Atomic start output

After successful resolution/planning, one transaction creates/replays:

- one `Pass2Execution`;
- zero to ten Flow Groups with exact Flow Trace steps;
- one unique exact resolution decision FK per group and a deliberately reusable document Edge FK;
- both the pair-specific Flow Group identity and cross-scan consumer-entry/Logical Document stable keys on every group;
- all bounded omission records;
- exactly one Pass 2 Job and retrieval budget for every `Complete` Flow Group;
- no job for `Incomplete` or `BoundExcluded` groups;
- coordination transition into Pass2.

The operation returns only execution ID, state version and sanitized counts. It never returns source, endpoint text, catalog contents, provider configuration, secrets, host paths, lease tokens, or hidden-resource distinctions.

## Determinism vectors

Implementations must prove byte-equivalent decision/group output over at least ten randomized Consumer/provider-observation/Repository-completion/enumeration orders. Required vectors include duplicate locators/classifications/anchors for one provider Document remaining one provider, the same evidence split/reordered without changing the aggregate, two distinct current provider Documents remaining ambiguous, two endpoints in one consumer Document targeting one provider Document producing two decisions/one Edge/two Flow Groups, two consumer entries doing the same, and exact duplicate/concurrent replay producing no extra decision or Edge. Identity vectors prove that later pair/current Document/anchor changes preserve the Logical Document stable key when the semantic consumer entry and endpoint are unchanged, while changed endpoint/entry produces a new key. UUID ordering uses RFC 4122 big-endian bytes and text ordering uses strict normalized UTF-8 ordinal bytes.
