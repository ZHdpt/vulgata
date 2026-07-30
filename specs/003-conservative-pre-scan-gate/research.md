# Research: Conservative Pre-Scan Gate

All planning unknowns are resolved. The decisions below are bounded by the
accepted specification, PRD section 5.16, the July 30 demonstration slice, and
the current Story 4.1/4.2 source.

## Decision 1: Attach the gate to the existing final profile transaction

**Decision**: keep `RepositoryProfileDraft` profile-only, compute a
`PreScanResultDraft` from that identity-valid draft plus the committed Profiled
Revision, then compose both into `RepositoryProfileCommitDraft` for the
extended `PersistCompletedProfileAsync`.

**Rationale**: the current repository already commits Code Units, Edges,
framework facts, language summaries, and candidate outcomes in one explicit
transaction. Adding the result there is the only small design that satisfies
atomic profile/result visibility.

**Alternatives considered**:

- Run after profile commit: rejected because a crash exposes a completed
  profile without its required approved handoff.
- Create a second unit of work: rejected because it duplicates transaction and
  retry behavior.

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

## Decision 5: Materialize one private immutable committed tree

**Decision**: reject a dirty reusable checkout, capture the Profiled Revision,
and safely materialize that exact Git tree from Git object data into a private
per-ScanRun snapshot while holding the Repository acquisition lock.
CodeGraphContext profiling, framework/language readers, and pre-scan detection
all consume that same snapshot root. Reuse Story 4.2 path, reparse, parsing, and
aggregate bounds; source ranges remain 64 KiB each and 16 MiB aggregate.

**Rationale**: `HEAD` alone cannot bind a mutable checkout—uncommitted tracked
bytes leave the commit unchanged. One private committed tree binds structural
profile facts and detector evidence to the Profiled Revision.

**Alternatives considered**:

- Profile the reusable working-copy path: rejected because dirty or concurrent
  mutations can disagree with the captured commit.
- Check only `HEAD` before/after: rejected because unchanged-HEAD mutations are
  invisible.
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

## Decision 10: One serialized Story 4.3 migration

**Decision**: Story 4.3 exclusively owns the domain DbContext, model snapshot,
and its additive migration through its completed-story checkpoint. Story 11.1
may plan non-overlapping work meanwhile, but rebases or branches from that exact
checkpoint before any migration, DbContext, or snapshot mutation.

**Rationale**: both stories need shared EF files. Serial ownership prevents
snapshot and migration divergence.

**Alternatives considered**:

- Parallel migrations: rejected because order and snapshot merge become
  ambiguous.
- Delay Story 4.3 durability to Story 11.1: rejected because durability is an
  explicit Story 4.3 acceptance requirement.

## Decision 11: Planned-change authorization is mandatory

**Decision**: independent plan acceptance is followed by explicit user
authorization before tasks because the plan adds Scan classification behavior.

**Rationale**: `AGENTS.md` requires this pause for material Scan strategy
changes. The expedited exception does not waive it.

**Alternatives considered**: none compliant.
