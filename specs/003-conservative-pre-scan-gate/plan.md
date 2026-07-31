# Implementation Plan: Conservative Pre-Scan Gate

**Branch**: `story4.3` | **Date**: 2026-07-31 | **Spec**:
[spec.md](spec.md) | **Execution level**: `expedited-mvp`

## Summary

Correct the implemented atomic Repository profiling pipeline at three public
seams without changing the accepted Story 4.3 product scope: framework
detection consumes the existing verified committed-snapshot lease rather than
treating its archive-only `RootPath` as a Git working tree; every public
profiling construction path requires the real committed-snapshot and Pre-Scan
detection services; and final persistence accepts only the composite
`RepositoryProfileCommitDraft`. One production-composition regression crosses
committed snapshot acquisition, framework detection, Communication Kind
Classification, and atomic persistence through public interfaces.

The correction remains deterministic, offline, bounded, redacted, atomic,
idempotent, and bound to the exact Profiled Revision. It adds no LLM,
Human-in-the-Loop flow, Worker Agent tool or job, dispatch, scheduling, retry,
provider failover, endpoint resolution, REFERENCES Edge, external dependency,
or Communication Pattern Catalog locality refactor. The existing Story 4.3
migration and database model are unchanged; this remediation is
migration-neutral.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`)

**Primary Dependencies**: existing ASP.NET Core, EF Core, Npgsql, and
FluentValidation; no new package

**Storage**: PostgreSQL 17 through `VulgataDbContext`

**Testing**: xUnit in `tests/Vulgata.Tests`, existing SQLite integration
harness, production-composition regression, and PostgreSQL migration-neutral
model validation

**Target Platform**: Vulgata single-container Linux deployment and Windows
development

**Project Type**: Blazor Web App with Core, Infrastructure, Shared, and Web
project boundaries

**Performance Goals**: bounded local classification adds no network or LLM wait

**Constraints**:

- inspect committed Repository bytes only;
- pass `ICommittedRepositorySnapshotLease`, not a path with ambiguous Git
  semantics, into framework detection;
- never run Git against the materialized archive-only snapshot `RootPath`;
- reuse Story 4.2's 2,048-character Repository-relative path limit and
  committed-snapshot safety;
- reuse the existing 8 MiB per-artifact and 64 MiB aggregate committed-artifact
  bounds, then cap each persisted Code Unit source range at 64 KiB and all
  inspected source ranges at 16 MiB;
- retain at most 100 canonical classification/audit samples without truncating
  confirmed-kind truth;
- persist, log, and return no source text, absolute path, secret, or unbounded
  diagnostic;
- expose no public coordinator construction path without both the committed
  snapshot service and real Pre-Scan detector;
- expose no profile-only final persistence operation;
- add no migration or model change;
- commit no failing test.

**Scale/Scope**: one Repository and one ScanRun at a time; prepared RPC/HTTP
fixtures only for the August 15 acceptance path

## Authority Reconciliation

1. PRD section 5.16 and the approved July 30 August demonstration slice are the
   current acceptance overlay. They replace the older July pre-scan LLM/review
   behavior for this deadline story.
2. Full FR-5.x Worker-embedded Cross-Repository Communication Detection remains
   backlog. Story 4.3 records communication-kind hypotheses only.
3. The embedded delivery rule vocabulary is not the System's Communication
   Pattern Catalog. It cannot be edited by a SystemOwner and is not injected
   into a prompt.
4. `pre_scan_approved` is the state of the new `PreScanResult`, not a replacement
   for the existing Story 4.1/4.2 `ScanRunStatus.Completed`.
5. The approved July 31 Epic 4 Correct Course proposal reserves exactly three
   HIGH/MUST/Strong findings for this correction. Story 4.3 requirements and
   success criteria remain unchanged; the medium catalog-locality refactor is
   explicitly deferred.

## Constitution Check

### Before design

| Gate | Result | Evidence |
| --- | --- | --- |
| Core independence | PASS | Entities and domain service interfaces stay in Core; EF stays in Infrastructure. |
| Specific repository boundary | PASS | `IScanRunRepository` retains one composite final commit; no generic repository or `IQueryable`. |
| Deterministic and offline | PASS | One verified committed-snapshot lease, closed rules, no Git against archive `RootPath`, LLM, network, or package execution. |
| Security and authorization | PASS | Repository visibility precedes the Pre-Scan Handoff; payload is redacted and bounded. |
| Test-first delivery | PASS | Every production slice has transient RED then GREEN; no RED checkpoint. |
| Immutable traceable truth | PASS | Approved result is immutable; a changed replay requires a new ScanRun. |
| Canonical domain language | PASS | `Profiled Revision`, `Pre-Scan Result`, `Communication Kind Classification`, `Evidence Locator`, and `Pre-Scan Handoff` are defined in `docs/UBIQUITOUS_LANGUAGE.md`. |
| Deadline exception | PASS | Deferred families and stages remain excluded; observed RED/GREEN, combined review, E2E, migration-neutral model check, and Docker verification remain required. |

### After design

PASS. The correction changes no entity, table, migration, application payload,
project, dependency, authorization rule, or downstream graph truth. It narrows
existing public interfaces so the accepted profile-to-approval invariants
cannot be bypassed.

## Design

### 1. Closed deterministic detector

Keep the existing `ICommittedRepositorySnapshotLease` as the single and
narrowest snapshot contract. Its Repository ID, ScanRun ID, Profiled Revision,
private `RootPath`, identity verification, and lifetime already carry every
property framework detection needs; do not add a second snapshot/read-store
abstraction.

Change the public `IFrameworkEvidenceDetector` inventory operations to accept
`ICommittedRepositorySnapshotLease` rather than `string workingCopyPath`.
`FrameworkEvidenceDetector` verifies the live lease before and after its reads,
inventories only containment-checked regular files under the materialized
`RootPath`, and reads bounded artifact bytes from that representation. It does
not call `git rev-parse`, `git ls-tree`, `git cat-file`, or any other Git command
against `RootPath`. Existing parsing, closed catalog, audit outcomes, path
normalization, artifact limits, and applicability selection remain unchanged.
Path-based Git snapshot discovery is not a production or test entry point.

`GitWorkingCopyService` continues to reject a dirty reusable checkout and
capture the bounded lowercase Profiled Revision.
`CommittedRepositorySnapshotService` continues to hold the Repository
acquisition lock, safely materialize that exact tree from Git object data, and
reject escaping, absolute, control-bearing, symlink/reparse, duplicate, or
oversized archive entries. The coordinator verifies the lease before profiling,
passes the same lease to framework and communication detection, verifies it
again before persistence, and disposes it only after commit/failure.
CodeGraphContext and candidate enumeration may consume `lease.RootPath` as a
materialized directory; only the lease is passed across detector interfaces.
A dirty checkout, unchanged-HEAD tracked-byte mutation, snapshot mutation, or
revision mismatch fails closed and publishes no approved result.

Every public `RepositoryProfilingCoordinator` constructor requires both
`ICommittedRepositorySnapshotService` and
`IPreScanCommunicationDetector`. Remove the two compatibility constructors
that install `MissingCommittedRepositorySnapshotService` or
`EmptyPreScanCommunicationDetector`, and remove both adapters. The DI-primary
constructor remains canonical; a convenience overload may load only the
embedded communication rule catalog, but it must still require both real
services. Tests supply explicit public-interface fakes where isolation is
needed and cannot obtain approval from an empty detector.

Infrastructure owns an embedded, versioned
`communication-evidence-rules.v1.json`. Each rule has a stable rule ID, one
closed `Rpc` or `Http` kind, an allowed evidence category, exact normalized
tokens/keys, and a fixed disposition:

- exact supported annotation or matched configuration: `Confirmed`;
- package declaration, framework fact, or import without direct configuration:
  `Hint`;
- invalid, unsafe, oversized, truncated, or non-confirming material:
  `Warning`.

Package/framework evidence alone never confirms a kind. The detector derives
closed `ConfirmedCommunicationKinds` flags for Rpc/Http and total confirmed,
hint, and warning counts before sample truncation. Equivalent matches are
aggregated and sorted by kind, disposition, rule, path, and line. At most 100
child samples are retained: reserve one direct-evidence sample for every
confirmed kind and, when truncation occurs, one `BoundExceeded` warning, then
fill the remaining slots deterministically. The aggregate fingerprint streams
over all normalized locator metadata before truncation and includes the
Profiled Revision, flags, and total counts. Persistence never stores matched
source text.

### 2. Atomic profile-to-approval transition

Keep `RepositoryProfileDraft` profile-only. Run the detector after
language/framework profiling succeeds and before persistence; it returns a
`PreScanResultDraft`. Compose a new `RepositoryProfileCommitDraft` containing
the profile draft and result draft, and pass that composite to the sole final
`IScanRunRepository.PersistCompletedProfileAsync` operation. Remove the
`RepositoryProfileDraft` overload from the Core interface,
`ScanRunRepository`, and all decorators/fakes. The existing repository
transaction writes
Code Units, profiling facts, `ScanRun.ProfiledRevision`, `PreScanResult`, and
child evidence samples atomically.

`ScanRun.ProfiledRevision` and `PreScanResult.ProfiledRevision` must equal the
captured revision. One unique `PreScanResult.ScanRunId` enforces one result per
ScanRun. Replay:

- no existing result: insert result and evidence in the profile transaction;
- same canonical result fingerprint: return the existing identity without
  changing or inserting rows;
- different fingerprint: fail closed and require a new ScanRun because approved
  evidence is immutable.

Profile/provider failure occurs before approval persistence. A transaction
failure rolls back profile and approval rows together.

### 3. Production composition and public test surfaces

Keep the current `Program.cs` registrations for
`ICommittedRepositorySnapshotService`,
`IFrameworkEvidenceDetector`, `IPreScanCommunicationDetector`,
`IScanRunRepository`, and `IRepositoryProfilingCoordinator`. Add a
production-composition regression that resolves the coordinator through the
Web composition root, replaces only unavoidable external CodeGraph behavior
with a deterministic test double, and uses a temporary committed Repository
plus the real snapshot, framework, communication, and persistence
implementations.

The regression must prove that the acquired materialized snapshot has no
`.git` metadata, framework evidence and prepared RPC/HTTP evidence are detected
from the same Profiled Revision-bound lease, and the completed profile plus
approved Pre-Scan Result become visible through the one composite transaction.
All detector calls and final persistence calls cross their public interfaces.
Focused implementation tests may remain, but they do not substitute for this
composition path.

### 4. Typed handoff and authorization

Core owns an immutable `ApprovedPreScanHandoff` read model. Extend
`IScanRunRepository` with a materialized read returning
`ApprovedPreScanHandoff?` by Repository ID and ScanRun ID; Infrastructure maps
directly to that Core model and never exposes an EF entity. Shared owns
`ApprovedPreScanHandoffDto`. An authorized Web `PreScanHandoffService` applies
the existing System/standalone Repository visibility rules before and after
materialization, verifies the handoff's Profiled Revision and schema version,
and maps Core to Shared. Missing, wrong-System, revoked, mismatched-revision,
and unauthorized results use the existing non-disclosure/fail-closed behavior.

The existing system-scoped and standalone ScanRun GET routes may compose the
bounded DTO into their current authorized response; no new route is required.
Story 11.2 consumes the service contract, validates/acquires the exact Profiled
Revision, and fails closed if its current checkout differs. This is not an
agent tool and grants no dispatch authority.

### 5. Observability

Persist and structurally log one closed Chinese outcome:

- with warnings:
  `预扫描已批准并保留审慎警告——继续进入顺序演示运行器`;
- without warnings:
  `预扫描已批准——继续进入顺序演示运行器`.

Logs contain only Repository ID, ScanRun ID, counts, and rule-set version.

### 6. Migration neutrality

The already-applied `AddConservativePreScanGate` migration and current
`VulgataDbContextModelSnapshot` remain unchanged. This correction changes
service contracts, detector input representation, coordinator construction,
repository method shape, and tests only. It creates no table, column, index,
constraint, seed change, Document, retrieval, graph, endpoint, dispatch, or
uncertainty schema. Verification must prove there is no pending model change;
any required migration or model edit is a scope conflict and stops this plan.

## Project Structure

### Documentation

```text
specs/003-conservative-pre-scan-gate/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── communication-detection.md
│   ├── pre-scan-handoff.md
│   └── repository-profiling-composition.md
├── checklists/requirements.md
└── spec.md
```

### Planned source boundary

```text
src/dotnet/
├── Vulgata.Core/
│   ├── Entities/
│   │   ├── PreScanResult.cs
│   │   └── CommunicationKindClassification.cs
│   └── DomainServices/
│       ├── ApprovedPreScanHandoff.cs
│       ├── IPreScanCommunicationDetector.cs
│       ├── IScanRunRepository.cs
│       └── RepositoryProfilingContracts.cs
├── Vulgata.Infrastructure/
│   ├── CommunicationDetection/
│   │   ├── CommunicationKindDetector.cs
│   │   ├── CommunicationEvidenceRuleCatalog.cs
│   │   └── Catalog/communication-evidence-rules.v1.json
│   ├── FrameworkDetection/FrameworkEvidenceDetector.cs
│   ├── Data/
│       ├── Configurations/PreScanResultConfiguration.cs
│       ├── Configurations/CommunicationKindClassificationConfiguration.cs
│       ├── ScanRunRepository.cs
│       └── VulgataDbContext.cs
│   └── Git/
│       ├── CommittedRepositorySnapshotService.cs
│       └── GitWorkingCopyService.cs
├── Vulgata.Shared/Repositories/ApprovedPreScanHandoffDto.cs
└── Vulgata.Web/
    ├── Api/Repositories/RepositoryApiEndpoints.cs
    ├── Data/PreScanHandoffService.cs
    ├── Data/RepositoryProfilingCoordinator.cs
    └── Program.cs

tests/Vulgata.Tests/
├── PreScanCommunicationDetectionTests.cs
├── FrameworkDetectionTests.cs
├── RepositoryProfilingTests.cs
├── RepositoryApiTests.cs
├── Story43EndToEndTests.cs
└── MigrationTests.cs
```

**Structure Decision**: extend the existing profiling aggregate and repository
transaction. Reuse `ICommittedRepositorySnapshotLease`; add no snapshot
abstraction, coordinator, queue, generic storage abstraction, or migration.

### Architecture finding closure

| Audit finding ID | Exact plan remediation | Required observed RED/GREEN evidence |
| --- | --- | --- |
| `epic4-framework-snapshot-representation-mismatch` | Change `IFrameworkEvidenceDetector` inventory methods from path input to `ICommittedRepositorySnapshotLease`; enumerate/read the archive representation under `RootPath`; remove Git identity/inventory/blob calls from that path. | RED: a real archive-only lease without `.git` returns detector failure. GREEN: the same lease yields expected framework facts through `IFrameworkEvidenceDetector`, with before/after identity verification and no Git invocation against `RootPath`. |
| `epic4-empty-prescan-compatibility-path` | Remove both public compatibility constructors plus `EmptyPreScanCommunicationDetector` and `MissingCommittedRepositorySnapshotService`; every remaining constructor requires both service seams. | RED: a public-construction contract test finds a constructor lacking one of the required interfaces. GREEN: every public constructor requires both, affected tests use explicit interface fakes, and the production-composition run performs real Communication Kind Classification before approval. |
| `epic4-profile-only-persistence-path` | Remove the `RepositoryProfileDraft` overload from `IScanRunRepository`, `ScanRunRepository`, and all test decorators; retain only composite `RepositoryProfileCommitDraft` final persistence. | RED: a public-interface contract test finds the profile-only overload. GREEN: the interface exposes one composite final method, injected failure rolls back profile/result together, and replay remains idempotent. |

The production-composition regression is shared closure evidence: it resolves
the profiling coordinator from the production composition root and crosses
snapshot acquisition → framework detection → communication detection → one
atomic profile/Pre-Scan Result commit. It is not a refactor of framework or
communication catalog locality.

## Requirement-to-Design/Test Matrix

| Spec requirements | Design surface | RED/GREEN evidence |
| --- | --- | --- |
| FR-001–FR-003 | coordinator ordering, Profiled Revision capture, private committed-tree materialization, and one-snapshot profiling/detection | dirty checkout and unchanged-HEAD tracked-byte mutation publish no result; incomplete/failed profile never invokes detection; no LLM/network/package execution |
| FR-004–FR-007 | closed rules, independent confirmed-kind flags, deterministic sample reservation, streaming fingerprint | direct annotation/config confirms; import/package hints; both kinds survive >100 direct matches; warning and non-promotion |
| FR-008 | locator/value guards and DTO mapping | unsafe/oversized/path/source/secret inputs never persist or return |
| FR-009–FR-012 | result/evidence entities, composite commit draft, unique keys, transaction/replay | warning-only approval, no-op replay, changed replay rejection, rollback |
| FR-013–FR-015 | Core handoff read model with confirmed evidence, authorized Web service/DTO, structured Chinese log | working copy advances after approval but handoff stays at old revision; mismatched consumption fails closed; both-kind reserved confirmed evidence is returned; unauthorized non-disclosure; exact messages |
| FR-016–FR-019 | closed enums and excluded services/tables | zero jobs, Edges, endpoints, Uncertainties, or deferred confirmations |
| Architecture remediation only | lease-based framework interface, required coordinator dependencies, one composite repository final method, unchanged schema | archive-only no-`.git` framework success; public-constructor and repository-interface shape tests; production composition; no pending model changes |

## Verification Strategy

1. Snapshot/framework tests through `ICommittedRepositorySnapshotService` and
   `IFrameworkEvidenceDetector`: a real archive-only lease has no `.git`,
   produces expected framework evidence, never invokes Git against `RootPath`,
   and fails closed on identity mutation.
2. Public surface tests: every public profiling coordinator constructor
   requires snapshot and Pre-Scan detector services; `IScanRunRepository`
   exposes exactly one final composite persistence operation.
3. Production-composition integration: resolve
   `IRepositoryProfilingCoordinator` from the Web composition root and cross
   real committed snapshot acquisition, framework detection, communication
   detection, and atomic persistence through public interfaces.
4. Detector and profiling integration: exact annotation/config matching,
   bounds, sorting, deduplication, deterministic sample reservation, >100
   cross-kind direct matches, non-promotion, ordering, replay idempotency,
   immutable mismatch rejection, and failure rollback.
5. API/application integration: Core-to-Shared mapping including bounded
   confirmed evidence, ManagementAccess, Repository visibility, revoked access,
   redaction, stable old-revision handoff after checkout advancement, and
   mismatch rejection by the consumer.
6. Migration-neutral check: existing migration tests remain green and
   `dotnet ef migrations has-pending-model-changes` reports none; do not add,
   roll back, or edit a migration.
7. Complete focused tests, full tests, Web build, required Story 4.3
   clean-database two-Repository E2E, and Docker build under the August 15
   exception.

## Planned-Change Authorization Trigger

This correction materially changes Scan profiling strategy: framework evidence
consumes a different snapshot representation contract and invalid final
profiling paths are removed. After an independent plan-judge accepts, the root
must pause for explicit user authorization before `speckit-tasks`.
The request must cite this plan, the approved July 31 Correct Course proposal,
the affected Story 4.3/PRD material, the implementation intent, and effects.
It adds no Worker Agent tool, dispatch, scheduling, retry, provider failover,
PRD weakening, dependency, or migration. Authorization does not waive observed
RED/GREEN TDD, security, combined review, E2E, Docker, or final verification.

## Complexity Tracking

No Constitution violation or new architectural layer is introduced.
