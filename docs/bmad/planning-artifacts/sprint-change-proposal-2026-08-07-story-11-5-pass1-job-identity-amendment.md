# Sprint Change Proposal Amendment: Story 11.5 Pass 1 Job Identity

**Date:** 2026-08-07
**Trigger:** T051 preflight proved that ordinary successful Pass 1 does not
retain an authoritative Worker job identity that the accepted runtime Pass 2
binding contract requires.
**Mode:** Incremental
**Approval:** Incremental Edit 5 approved by the user on 2026-08-07.
**Parent proposal:**
`sprint-change-proposal-2026-08-07-story-11-5-runtime-pass2-binding.md`
**Scope classification:** Moderate bounded amendment; no data-model migration.

## 1. Issue Summary

The accepted runtime Pass 2 plan requires each prepared member to retain its
actual Pass 1 `WorkerAgentJobPacket.JobId`. The current ordinary Pass 1 path
creates a random job ID, stores it temporarily as
`ScanExecution.InFlightJobId`, clears that value after a valid result, and
persists only one-way values derived from the job ID. No post-Pass-1 product
query can recover the historical job ID.

`PreparedPass2FlowMember.JobId`, `PreparedPass2TraceEvidence.MemberJobId`, the
resolver, and the existing Worker payload treat this value as the actual Pass 1
job identity. Replacing it with a newly invented planner member ID would change
that meaning and weaken independent runner/committer validation. Persisting the
random ID would require a model/migration change prohibited by the accepted
deadline correction.

## 2. Approved Correction

Replace the ordinary random Pass 1 job identity with a deterministic,
server-owned identity derived from the current product execution boundary:

- `ScanExecutionId`;
- `ScanRunId`;
- `RepositoryId`;
- `CodeUnitId`; and
- the accepted `ProfiledRevision` identity/fingerprint.

The corrected plan must define a versioned, domain-separated, canonical byte
encoding and deterministic UUID derivation. The sequential runner derives the
identity immediately before Pass 1 dispatch. The runtime prepared-flow planner
independently recomputes the same historical identity from persisted current
records. Runner and committer reject substituted, drifted, malformed, or
noncanonical identities before Pass 2 dispatch or persistence.

This preserves the existing meaning of `JobId`, the exact seven-slot Pass 2
payload, and the no-migration boundary.

## 3. Rejected Alternatives

- **Synthetic planner member identity:** rejected because it would no longer
  identify the actual Pass 1 Worker job and would contradict the accepted
  member/trace contract.
- **Reverse the Document Version hash:** impossible because the stored value is
  a one-way derivation.
- **Persist the random ID in a new field or entity:** rejected because it would
  require an EF model and migration change.
- **Fixture-only mapping, SQL, `DbSet`, reflection, or hard-coded identity:**
  rejected because it would conceal the production gap and violate the release
  contract.

## 4. Scope and Effects

### Included

- A bounded Core canonical Pass 1 job-identity derivation contract.
- Sequential runner use of that derivation for ordinary Pass 1 dispatch.
- Runtime planner independent recomputation from current persisted records.
- RED/GREEN golden vectors and changed-input, collision-domain, retry,
  substitution, stale, and noncanonical rejection coverage.
- Minimal corrections to Story 11.3 and Story 11.5 SpecKit artifacts.
- Regeneration of unfinished T051-T065 only after independent plan acceptance
  and the subsequent planned-change authorization.

### Excluded

- EF entities, configuration, `DbContext`, migrations, or model snapshot.
- An additional payload slot or a semantic rename of existing `JobId` fields.
- General Scan scheduling, concurrency, retry policy, failover, clustering, or
  matching changes.
- Changing already committed Document Versions or backfilling historical data.
- Fixture SQL, direct `DbSet` provisioning, reflection, or fabricated IDs.

## 5. Compatibility and Failure Semantics

- The derivation applies to newly dispatched ordinary Pass 1 jobs. Existing
  persisted Documents are not rewritten.
- The deadline final run starts from a clean database, so every Pass 1 member in
  the accepted two-Repository path uses the deterministic identity contract.
- Repeating the same logical job boundary produces the same identity; changing
  any bound execution, Repository, Code Unit, or Profiled Revision input
  produces a different identity.
- Retry policy remains out of scope. Deterministic identity does not itself
  schedule, retry, or permit duplicate commits.
- Any unsupported legacy member without recomputable identity fails closed and
  produces zero Pass 2 dispatches and zero generated Documents.

## 6. Required Workflow

1. Correct the Story/specification/technical-plan authorities only.
2. Obtain a fresh independent plan verdict.
3. If accepted, request explicit planned-change authorization because ordinary
   Pass 1 job identity generation materially changes Scan dispatch behavior.
4. Regenerate only the unfinished T051-T065 task boundary.
5. Resume vertical RED-GREEN TDD; no production edit precedes the observed RED.
6. Retain combined quality review, security, migration, clean-database
   two-Repository E2E, Docker, browser, and cleanup gates.

## 7. Success Criteria

- The same canonical inputs always produce the pinned golden Pass 1 Job UUID.
- Every bound input participates in the derivation with unambiguous byte
  encoding and domain/version separation.
- The runner dispatches the derived identity and the planner independently
  recomputes it from current product records.
- Changed, substituted, stale, malformed, mixed-endian, or noncanonical inputs
  yield zero Pass 2 dispatches and zero generated Documents.
- The exact seven-slot Pass 2 payload and actual Pass 1 Worker job semantics are
  preserved.
- No EF model, migration, snapshot, general Scan, retry-policy, or fixture
  bypass change is introduced.
