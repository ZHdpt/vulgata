# Research: Conservative Pre-Scan Gate

All planning unknowns are resolved. The decisions below are bounded by the
accepted specification, PRD section 5.16, the July 30 demonstration slice, the
approved July 31 Epic 4 Correct Course proposal, the three reserved audit
findings at source commit
`1b9c98bb4999e254ab19704643477732f92c842c`, and the current Story 4.1–4.3
source. No specification, PRD, ADR, or migration change is required.

## Decision 1: Attach the gate to the existing final profile transaction

**Decision**: keep `RepositoryProfileDraft` profile-only, compute a
`PreScanResultDraft` from that identity-valid draft plus the committed Profiled
Revision, then compose both into `RepositoryProfileCommitDraft` for the
sole final `IScanRunRepository.PersistCompletedProfileAsync` operation. Remove
the profile-only persistence overload from the interface and implementation.

**Rationale**: the current repository already commits Code Units, Edges,
framework facts, language summaries, and candidate outcomes in one explicit
transaction. Adding the result there is the only small design that satisfies
atomic profile/result visibility.

**Alternatives considered**:

- Run after profile commit: rejected because a crash exposes a completed
  profile without its required approved handoff.
- Create a second unit of work: rejected because it duplicates transaction and
  retry behavior.
- Retain the profile-only overload for compatibility: rejected because it is a
  public bypass of FR-010/FR-011 atomic profile-to-approval visibility.

## Decision 2: Use one Core aggregate per ScanRun

**Decision**: add `PreScanResult` plus child
`CommunicationKindClassification`; set result identity to ScanRun identity and
enforce unique Repository/ScanRun ownership.

**Rationale**: the specification requires one durable result and multiple
bounded classifications. A distinct aggregate keeps communication truth out
of structural `CodeUnitEdge` and `UnresolvedStructuralRelationshipCandidate`.

**Alternatives considered**:

- Add flags/JSON to `ScanRun`: rejected because classifications would not have
  independent invariants, provenance, or queryable typed fields.
- Reuse structural Edges/candidates: rejected because Story 4.3 does not create
  REFERENCES, endpoints, or unresolved Cross-Repository relationships.

## Decision 3: Closed evidence and truth vocabularies

**Decision**: only `Rpc`/`Http`, `Confirmed`/`Hint`/`Warning`, and closed
confidence, rule, provenance, and warning codes are domain values.

**Rationale**: this prevents attacker-controlled discriminators and keeps MQ,
file/storage, navigation, object-flow, Service Topology, and future catalog
families deferred.

**Alternatives considered**:

- Persist arbitrary strings: rejected as unsafe and scope-expanding.
- Implement Communication Pattern Catalog management: rejected because Epic 7
  owns it and the deadline input is version-controlled.

## Decision 4: Direct evidence confirms; package/import evidence only hints

**Decision**: exact supported annotation/route/client syntax or an exact
version-controlled configuration rule is `Confirmed/High`. Import, namespace,
package, and framework presence without a direct signature is
`Hint/Conservative`. Malformed, unsafe, oversized, contradictory, or
unsupported evidence is `Warning/None` or omitted.

**Rationale**: this is the specification's conservative truth boundary and
prevents weak framework presence from becoming a code-derived fact.

**Alternatives considered**:

- Confirm from a framework fact alone: rejected because a framework can be
  present without communication use.
- Treat every anomaly as a blocker: rejected because the accepted deadline
  boundary auto-approves and defers Human-in-the-Loop.

## Decision 5: Reuse the committed-snapshot lease as the framework seam

**Decision**: reject a dirty reusable checkout, capture the Profiled Revision,
and safely materialize that exact Git tree from Git object data into a private
per-ScanRun snapshot while holding the Repository acquisition lock.
Change `IFrameworkEvidenceDetector` inventory methods to accept the existing
`ICommittedRepositorySnapshotLease`. Framework detection verifies and reads
the lease's materialized archive representation with bounded filesystem
inventory; it never resolves Git identity or runs Git inventory/blob commands
against `RootPath`. CodeGraphContext and candidate enumeration may still
receive the materialized directory path. Pre-scan detection already consumes
the lease. Reuse Story 4.2 path, reparse, parsing, and aggregate bounds; source
ranges remain 64 KiB each and 16 MiB aggregate.

**Rationale**: `ICommittedRepositorySnapshotLease` already carries Repository,
ScanRun, Profiled Revision, lifetime, private root, and identity verification.
It is the narrowest sufficient contract and prevents the materialized archive
directory from being reinterpreted as a Git checkout. `HEAD` alone cannot bind
a mutable checkout because uncommitted tracked bytes leave the commit unchanged.

**Alternatives considered**:

- Add a second snapshot reader/store interface: rejected because the existing
  lease already exposes all required authority and would create overlapping
  snapshot meanings.
- Profile the reusable working-copy path: rejected because dirty or concurrent
  mutations can disagree with the captured commit.
- Check only `HEAD` before/after: rejected because unchanged-HEAD mutations are
  invisible.
- Pass `lease.RootPath` as a string and keep Git-backed framework discovery:
  rejected because it recreates the audited representation mismatch.
- Extend the CodeGraph provider contract: rejected as unnecessary scope and a
  larger compatibility risk.

## Decision 6: Profiled Revision and canonical fingerprints provide identity

**Decision**: capture `git rev-parse --verify HEAD` as the Profiled Revision;
reject dirty/changed acquisition state and verify the private committed
snapshot identity before profiling, after detection, and before persistence.
Canonicalize every closed field and safe locator in ordinal order, stream all
pre-truncation locator metadata through SHA-256, and bind the Profiled Revision,
confirmed-kind flags, and total counts into the aggregate fingerprint.

**Rationale**: result ID equals ScanRun ID, while fingerprints distinguish a
safe idempotent replay from inconsistent recomputation. No source or secret is
hashed or persisted.

**Alternatives considered**:

- Generate a new result ID for every attempt: rejected because it permits
  duplicates.
- Silently replace an existing result: rejected because it hides non-
  deterministic behavior.

## Decision 7: Core read model plus authorized application service

**Decision**: Core owns an immutable `ApprovedPreScanHandoff` read model,
Infrastructure materializes it through a specific repository query, Shared owns
the transport DTO, and Web's authorized `PreScanHandoffService` maps the two
while reusing system/standalone visibility and post-load revocation checks. No
new public route is required by Story 4.3.

**Rationale**: Story 11.2 receives a Pre-Scan Handoff without exposing EF or
creating a new public surface. Existing not-found redaction remains exact.

**Alternatives considered**:

- Expose `DbSet`/`IQueryable`: rejected by the Constitution.
- Add an unauthenticated internal endpoint: rejected because it weakens the
  Repository visibility boundary.

## Decision 8: Separate confirmed truth from bounded samples

**Decision**: persist closed Rpc/Http `ConfirmedCommunicationKinds` flags and
total confirmed/hint/warning counts before retaining at most 100 child samples.
Reserve one direct sample per confirmed kind and one `BoundExceeded` warning
when truncated, then fill deterministically. The Pre-Scan Handoff derives
`confirmedKinds` only from the flags.

**Rationale**: evidence sampling must not erase truth when one communication
kind sorts after more than 100 matches of another kind.

**Alternatives considered**:

- Derive confirmed kinds from retained rows: rejected because truncation can
  produce a false negative.
- Persist every evidence row: rejected because it violates the bounded
  expedited-MVP envelope.

## Decision 9: Reuse ScanRun terminal message for durable observability

**Decision**: store the exact warning-bearing Chinese approval message on the
ScanRun and emit one structured summary log with identifiers and counts.

**Rationale**: there is no current separate Scan-log aggregate. Reuse is
durable and avoids inventing the future append-only log subsystem.

**Alternatives considered**:

- Add a general event/log table: rejected as Epic 5 scope.
- Log only: rejected because logs alone do not satisfy durable visibility.

## Decision 10: Keep the correction migration-neutral

**Decision**: change no entity, EF configuration, `VulgataDbContext`, migration,
or model snapshot. Retain the already-applied Story 4.3 persistence model and
verify that EF reports no pending model changes.

**Rationale**: all three findings concern public service/construction seams.
The existing tables and constraints already represent the accepted aggregate;
a schema change would expand the approved Correct Course boundary and collide
with Story 11.1's migration hold.

**Alternatives considered**:

- Add a compatibility marker or constraint: rejected because no persisted
  shape changes.
- Edit/rebuild the existing migration: rejected because the deployed model is
  not the defect and migration history must remain stable.

## Decision 11: Planned-change authorization is mandatory

**Decision**: independent plan acceptance is followed by explicit user
authorization before tasks because the plan adds Scan classification behavior.

**Rationale**: `AGENTS.md` requires this pause for material Scan strategy
changes. The expedited exception does not waive it.

**Alternatives considered**: none compliant.

## Decision 12: Require real dependencies on every public construction path

**Decision**: remove the two public `RepositoryProfilingCoordinator`
compatibility constructors that substitute
`EmptyPreScanCommunicationDetector` or
`MissingCommittedRepositorySnapshotService`, then delete those adapters.
Retain only construction paths that explicitly require both
`ICommittedRepositorySnapshotService` and
`IPreScanCommunicationDetector`; an overload may differ only by loading the
embedded rule catalog.

**Rationale**: automatic approval is valid only after deterministic
Communication Kind Classification on the exact committed snapshot. A public
empty detector makes the invalid state constructible even when production DI
currently chooses the real detector.

**Alternatives considered**:

- Keep obsolete constructors but mark them deprecated: rejected because they
  still manufacture approval without evidence.
- Make empty approval fail instead of approve: rejected because the missing
  dependency remains a bypass/error-only construction path.
- Rely only on `Program.cs` DI: rejected because public constructors and tests
  are also supported composition surfaces.

## Decision 13: Use public contracts for composition evidence

**Decision**: add one regression that resolves
`IRepositoryProfilingCoordinator` through the Web production composition root,
uses a temporary committed Repository and the real
`ICommittedRepositorySnapshotService`, `IFrameworkEvidenceDetector`,
`IPreScanCommunicationDetector`, and `IScanRunRepository`, and replaces only
unavoidable external CodeGraph behavior with a deterministic test double.

**Rationale**: the audited mismatch escaped because focused tests manually
composed individual implementations. One public-interface path makes DI and
representation mismatches observable without replacing the existing focused
parser and detector suites.

**Alternatives considered**:

- Add only another direct `FrameworkEvidenceDetector` unit test: rejected
  because it would not prove production composition or atomic persistence.
- Exercise only the HTTP API: rejected as unnecessary for an internal
  profiling seam and more difficult to isolate from external CodeGraph.
- Refactor all catalog/parsing tests: rejected because the medium
  catalog-locality and broad test-surface work are outside this correction.

## Decision 14: Make interface shape failures observable before implementation

**Decision**: record RED then GREEN for the archive-only framework behavior,
for the public coordinator-constructor dependency invariant, and for the
single composite `IScanRunRepository` final method. Update all affected
interface fakes/decorators only after the corresponding RED is observed.

**Rationale**: two findings are forbidden public API shapes rather than an
alternative runtime branch. Small contract-shape tests provide a repeatable
RED, while the production-composition behavior test proves the runtime effect.

**Alternatives considered**:

- Treat compile errors after interface edits as the only RED: rejected because
  they do not capture the pre-change defect.
- Skip RED because the story was previously complete: rejected by the August
  15 expedited exception.
