# Contract: Same-Repository Post-Pass1 Reconciliation

## Input boundary

Reconciliation runs only after every job in the exact pair start set is terminal and seed/budget policy permits success. It reads accepted authoritative `code-logic/file-v2` ContentJson, current Document Versions/Source Anchors, direct REFERENCES Edges, and their known/deferred observations. It does not query Worker/provider output, raw CodeGraph storage, another Repository, or Story 12.4 endpoint resolution.

## Canonical evaluation

Candidates are reconstructed and sorted by:

1. source Repository lowercase UUID `D` text ordinal;
2. source Pair Selection Ordinal;
3. source Document Version RFC UUID bytes;
4. observation fingerprint bytes;
5. target path, range, symbol, and target Document Version identity.

For each observation, an Edge may be created only when exactly one target is:

- a current Code Logic Document Version in the same Repository and Scan Run;
- bound to the same Profiled Revision;
- supported by current exact target Source Anchors/source hash;
- authorized under the pair's current SystemOwner scope;
- not the source Document itself;
- consistent with the observation's stable target locator.

Direct known references are evaluated through the same target predicate and Edge identity. An existing exact Edge is replay, not a duplicate.

## Commit

One transaction locks the pair execution and relevant Edge uniqueness keys, revalidates all source/target currentness, inserts the canonically ordered missing Edges, writes the reconciliation fingerprint/count on the immutable `Pass1Outcome`, and advances the coordination handoff. An injected failure writes no subset and leaves Documents unchanged. Exact replay independently reconstructs the candidate decisions and existing Edge set; equality is mutation-free, divergence conflicts and prevents handoff.

## Rejections

Self, duplicate, stale, ambiguous, unsupported, cross-Repository, cross-Scan-Run, cross-revision, changed-hash, unauthorized, absent, or multiple-current targets create no Edge. The deferred observation remains unchanged in ContentJson. Story 12.3 does not create or resolve Uncertainty, endpoint/provider/consumer identity, Cross-Repository Edge, flow group, Pass 2 job, or Business Logic Document.

## Immutability

Reconciliation never updates, supersedes, reparses into new prose, or otherwise mutates a Document Version, ContentJson, ContentMarkdown, Document Section, Index Entry, or Source Anchor. Only separate REFERENCES Edge rows and the operational Pass 1 outcome are written.
