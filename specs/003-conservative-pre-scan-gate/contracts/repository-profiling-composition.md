# Contract: Repository Profiling Composition

## Purpose

This contract closes the three reserved Epic 4 architecture findings without
changing the Story 4.3 Pre-Scan Result or Pre-Scan Handoff schema.

## Committed snapshot to framework detection

`IFrameworkEvidenceDetector` exposes the committed snapshot lease as its
inventory authority:

```text
DetectAsync(
  ICommittedRepositorySnapshotLease committedSnapshot,
  CancellationToken cancellationToken)
  -> IReadOnlyList<PackageEvidence>

DetectDetailedAsync(
  ICommittedRepositorySnapshotLease committedSnapshot,
  CancellationToken cancellationToken)
  -> FrameworkEvidenceDetectionResult

SelectApplicable(
  FrameworkEvidenceDetectionResult detection,
  string codeUnitPath,
  string parserLanguage)
  -> FrameworkEvidenceDetectionResult
```

Required behavior:

- reject a null, disposed, or identity-invalid lease;
- call `VerifyIdentityAsync` before and after bounded artifact reads;
- use `ProfiledRevision` as the revision authority;
- inventory containment-checked regular files under `RootPath`;
- preserve the existing Repository-relative path, artifact-size,
  aggregate-size, record-count, parse-depth, audit-detail, and redaction bounds;
- never run `git rev-parse`, `git ls-tree`, `git cat-file`, or any Git command
  with the materialized archive-only `RootPath` as a working tree;
- return the existing closed success/failure and audit outcomes.

`RootPath` is a materialized directory capability within the live lease. It is
not a Git working-copy identity and is not accepted as a standalone public
detector argument.

## Coordinator construction

Every public `RepositoryProfilingCoordinator` constructor must require:

- `ICommittedRepositorySnapshotService`;
- `IPreScanCommunicationDetector`;
- all existing repositories/providers/detectors needed by the selected
  constructor.

A convenience overload may load
`CommunicationEvidenceRuleCatalog.LoadEmbedded()`, but it must still require
both services above. No public constructor may inject an empty detector or a
missing-snapshot adapter. `EmptyPreScanCommunicationDetector` and
`MissingCommittedRepositorySnapshotService` do not exist after the correction.

`Program.cs` keeps the production mappings:

```text
ICommittedRepositorySnapshotService -> CommittedRepositorySnapshotService
IFrameworkEvidenceDetector          -> FrameworkEvidenceDetector
IPreScanCommunicationDetector       -> CommunicationKindDetector
IScanRunRepository                  -> ScanRunRepository
IRepositoryProfilingCoordinator     -> RepositoryProfilingCoordinator
```

## Sole final persistence operation

`IScanRunRepository` exposes exactly one operation that makes a completed
Repository profile durable:

```text
PersistCompletedProfileAsync(
  RepositoryProfileCommitDraft draft,
  CancellationToken cancellationToken)
  -> PreScanResult
```

There is no overload accepting `RepositoryProfileDraft`. The implementation:

1. validates complete language/framework/profile facts;
2. validates Repository, ScanRun, Profiled Revision, and result identity;
3. starts one database transaction;
4. handles same-fingerprint replay as the existing idempotent no-op;
5. writes profiling facts and the Pre-Scan Result aggregate;
6. commits once, or rolls back all profile/result changes on failure.

Recovery and read operations remain unchanged and are not alternative final
profile commit paths.

## Production sequence

```text
IGitWorkingCopyService
  -> ICommittedRepositorySnapshotService.AcquireAsync
  -> lease.VerifyIdentityAsync
  -> candidate and CodeGraph profiling from lease.RootPath
  -> IFrameworkEvidenceDetector.DetectDetailedAsync(lease)
  -> RepositoryProfileDraft
  -> IPreScanCommunicationDetector.DetectAsync(..., lease)
  -> lease.VerifyIdentityAsync
  -> RepositoryProfileCommitDraft.Create(profile, result)
  -> IScanRunRepository.PersistCompletedProfileAsync(composite)
  -> Approved Pre-Scan Result
```

The lease remains live through final commit/recovery and is disposed on every
exit path.

## Failure contract

- missing/invalid snapshot identity: fail closed; no approved result;
- framework detector cannot safely inventory/read the archive: provider
  failure; no approved result;
- communication detector failure: no approved result;
- final persistence failure: zero partial profile/result visibility;
- different-fingerprint replay: fail closed and require a new ScanRun.

All diagnostics remain bounded and redacted. No failure path creates a Worker
Agent job, dispatch, scheduling/retry state, provider failover, Edge,
Uncertainty, or Cross-Repository Resolution.

## Required contract evidence

- archive-only lease test: no `.git`, expected framework evidence, zero Git
  invocation against `RootPath`;
- public-constructor shape test: every public constructor requires the snapshot
  and Pre-Scan detector interfaces;
- final-repository shape test: exactly one composite final method, no
  profile-only overload;
- production-composition regression: public interfaces cross real snapshot,
  framework, communication, and atomic persistence implementations;
- migration-neutral check: no pending model change.
