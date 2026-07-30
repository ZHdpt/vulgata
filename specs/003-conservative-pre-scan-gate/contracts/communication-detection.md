# Contract: Deterministic Communication-Kind Detection

## Operation

`IPreScanCommunicationDetector.DetectAsync` accepts:

- one completed, identity-valid `RepositoryProfileDraft`;
- the captured `ProfiledRevision`;
- a private, containment-checked committed snapshot materialized from that
  exact Git tree and shared by CodeGraph profiling and all detector readers;
- cancellation.

It returns one complete `PreScanResultDraft` or throws/cancels before
persistence. The coordinator combines the unchanged profile-only draft and
result draft into `RepositoryProfileCommitDraft`.
It performs no database write, network call, package execution, dispatch, or
LLM/Human-in-the-Loop operation.

## Input authority

Allowed:

- persisted/persistence-ready Code Unit path, range, language, symbol, and
  structural identity;
- persisted framework facts and their already-bounded locators;
- committed package-manifest/configuration candidates resolved through the
  existing safe snapshot boundary;
- exact bounded Code Unit source ranges solely for closed annotation/route/
  client matching.

Forbidden:

- dirty reusable checkouts, the mutable reusable working-copy root, unprofiled
  files, uncontained paths, reparse traversal, absolute
  paths, package/build execution, environment expansion, network/registry
  lookup, arbitrary regex/config supplied by a user, raw provider payloads,
  and source retention.

## Bounds

| Input/output | Limit |
| --- | ---: |
| Repository-relative path | 2,048 characters |
| Combined evidence locator | 4,096 characters |
| One artifact | 8 MiB |
| Aggregate committed artifacts | 64 MiB |
| JSON parse depth | 64 |
| Supported artifacts | 1,000 |
| Parsed package entries | 100,000 |
| Candidate facts | 250,000 |
| One Code Unit source range | 64 KiB |
| Aggregate source-range bytes | 16 MiB |
| Retained classifications/audit details | 100 |

An individual malformed/unsafe/oversized input is isolated as a closed Warning
or omitted. Repository-level inability to establish a safe complete input
fails detection and publishes no result. Bound overflow never confirms a kind.

## Classification rules

- Only `Rpc` and `Http` exist.
- Direct supported annotation/controller/client/route or exact matched
  version-controlled configuration: `Confirmed` + `High`.
- Import/namespace/direct package/persisted framework fact without direct use:
  `Hint` + `Conservative`.
- Malformed, unsafe, oversized, contradictory, unsupported: `Warning` +
  `None`.
- Exact duplicates collapse and increment `OccurrenceCount`.
- Direct evidence may add a Confirmed row but never promotes the hint evidence
  itself.
- Unsupported communication families can produce only
  `UnsupportedFamily/Warning` and no communication kind fact.

## Revision binding and determinism

`GitWorkingCopyService` rejects a dirty checkout and obtains the bounded
lowercase Profiled Revision with `git rev-parse --verify HEAD`.
`CommittedRepositorySnapshotService` holds the Repository acquisition lock,
rechecks cleanliness/revision, and safely materializes that exact tree from Git
object data into a private per-ScanRun snapshot. CodeGraphContext and the
detector consume only this shared snapshot. The coordinator verifies the
snapshot identity immediately before atomic persistence. Dirty state, a tracked
byte mutation without a HEAD move, unsafe archive content, snapshot mutation,
or revision mismatch publishes no result. For identical profile,
Profiled Revision, and committed inputs, the ordered safe classification fields
and aggregate fingerprint are identical. Current time and generated child IDs
are excluded. The aggregate fingerprint covers all normalized locator metadata
before sampling plus the revision, confirmed-kind flags, and total counts. A
persisted fingerprint mismatch for the same ScanRun fails closed.

## Bounded sampling

Confirmed Rpc/Http flags and total confirmed/hint/warning counts are computed
from the complete normalized evidence set. At most 100 child samples persist.
The selection reserves one direct sample for each confirmed kind and one
`BoundExceeded` warning when truncated, then fills the remaining slots in
canonical order. Therefore truncation can never remove a confirmed kind from
the result or handoff.

## Explicit exclusions

The result contains no provider/consumer role, endpoint target, route
resolution, Repository match, Uncertainty, REFERENCES Edge, Worker Agent job,
dispatch instruction, retry/scheduling data, LLM question, or manual approval
state.
