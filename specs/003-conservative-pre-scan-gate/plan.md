# Implementation Plan: Conservative Pre-Scan Gate

**Branch**: `story4.3` | **Date**: 2026-07-30 | **Spec**:
[spec.md](spec.md) | **Execution level**: `expedited-mvp`

## Summary

Extend the existing atomic Repository profiling pipeline with a deterministic,
committed-snapshot detector for the prepared RPC/HTTP demonstration vocabulary.
Persist one immutable approved result and bounded child evidence samples per
ScanRun, then expose an authorized Pre-Scan Handoff for Story 11.2. The result,
handoff, and fingerprints are bound to the exact Profiled Revision. The feature
adds no LLM, Human-in-the-Loop flow, Worker Agent job, endpoint resolution,
REFERENCES Edge, or Communication Pattern Catalog management.

Story 4.3 owns one additive migration. It is the integration owner for
`VulgataDbContext`, `VulgataDbContextModelSnapshot`, and the new pre-scan
migration until Story 4.3's completed-story checkpoint contains the implemented
migration. Story 11.1 must hold migration/snapshot changes and rebase or branch
from that exact checkpoint.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`)

**Primary Dependencies**: existing ASP.NET Core, EF Core, Npgsql, and
FluentValidation; no new package

**Storage**: PostgreSQL 17 through `VulgataDbContext`

**Testing**: xUnit in `tests/Vulgata.Tests`, existing SQLite integration harness,
plus PostgreSQL migration validation

**Target Platform**: Vulgata single-container Linux deployment and Windows
development

**Project Type**: Blazor Web App with Core, Infrastructure, Shared, and Web
project boundaries

**Performance Goals**: bounded local classification adds no network or LLM wait

**Constraints**:

- inspect committed Repository bytes only;
- reuse Story 4.2's 2,048-character Repository-relative path limit and
  committed-snapshot safety;
- reuse the existing 8 MiB per-artifact and 64 MiB aggregate committed-artifact
  bounds, then cap each persisted Code Unit source range at 64 KiB and all
  inspected source ranges at 16 MiB;
- retain at most 100 canonical classification/audit samples without truncating
  confirmed-kind truth;
- persist, log, and return no source text, absolute path, secret, or unbounded
  diagnostic;
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

## Constitution Check

### Before design

| Gate | Result | Evidence |
| --- | --- | --- |
| Core independence | PASS | Entities and domain service interfaces stay in Core; EF stays in Infrastructure. |
| Specific repository boundary | PASS | Extend `IScanRunRepository`; no generic repository or `IQueryable`. |
| Deterministic and offline | PASS | Committed local snapshot, closed rules, no LLM/network/package execution. |
| Security and authorization | PASS | Repository visibility precedes the Pre-Scan Handoff; payload is redacted and bounded. |
| Test-first delivery | PASS | Every production slice has transient RED then GREEN; no RED checkpoint. |
| Immutable traceable truth | PASS | Approved result is immutable; a changed replay requires a new ScanRun. |
| Canonical domain language | PASS | `Profiled Revision`, `Pre-Scan Result`, `Communication Kind Classification`, `Evidence Locator`, and `Pre-Scan Handoff` are defined in `docs/UBIQUITOUS_LANGUAGE.md`. |
| Deadline exception | PASS | Deferred families and stages remain excluded; migration and review gates remain. |

### After design

PASS. The design adds profiling-owned tables and one application contract, does
not create a project or dependency, does not move downstream graph truth into
pre-scan, and leaves Story 11.1's knowledge-store tables untouched.

## Design

### 1. Closed deterministic detector

Add one canonical `IPreScanCommunicationDetector` contract to Core and
implement it in Infrastructure. `GitWorkingCopyService` first rejects a dirty
reusable checkout, captures a bounded lowercase commit identity with
`git rev-parse --verify HEAD`, and returns it as
`GitWorkingCopyResult.ProfiledRevision`. A new
`CommittedRepositorySnapshotService` holds the Repository acquisition lock,
rechecks cleanliness/revision, and safely materializes that exact Git tree into
a private per-ScanRun snapshot from Git object data. It rejects escaping,
absolute, control-bearing, symlink/reparse, duplicate, or oversized archive
entries and rechecks the reusable checkout before releasing acquisition.

The coordinator gives this one immutable snapshot root to both
`CodeGraphContextProvider` and every profile/detector reader; no component
profiles the mutable reusable checkout. It builds a profile-only,
identity-valid `RepositoryProfileDraft`. The detector receives that draft, the
Profiled Revision, and the same containment-checked snapshot. The snapshot
lease remains held through detection and the pre-persistence revision check,
then is disposed after commit/failure. A dirty checkout, unchanged-HEAD tracked
byte mutation, snapshot mutation, or revision mismatch fails closed and
publishes no approved result.

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
the profile draft and result draft, and pass that composite to the extended
`PersistCompletedProfileAsync`. The existing repository transaction writes
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

### 3. Typed handoff and authorization

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

### 4. Observability

Persist and structurally log one closed Chinese outcome:

- with warnings:
  `预扫描已批准并保留审慎警告——继续进入顺序演示运行器`;
- without warnings:
  `预扫描已批准——继续进入顺序演示运行器`.

Logs contain only Repository ID, ScanRun ID, counts, and rule-set version.

### 5. Migration and parallel-story ordering

Story 4.3 owns `AddConservativePreScanGate`, creating only the pre-scan result
and evidence tables, foreign keys, unique constraints, and indexes. It creates
no Document, retrieval, graph, endpoint, dispatch, or uncertainty table.

Shared-file serialization:

1. Story 11.1 may continue domain/spec/plan work but must not edit
   `VulgataDbContext`, `VulgataDbContextModelSnapshot`, or migrations.
2. Story 4.3 owns those paths through its accepted-delivery-definition
   checkpoint.
3. User authorization and task generation may create the earlier
   accepted-delivery-definition checkpoint, but it does not release shared EF
   ownership because the Story 4.3 migration does not exist yet.
4. Story 4.3 retains ownership through implementation, migration verification,
   acceptance, and the completed-story checkpoint; the root then publishes
   that checkpoint SHA.
5. Story 11.1 branches/rebases from that exact completed-story SHA before
   generating its knowledge-store migration, then owns later snapshot
   evolution.

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
│   └── pre-scan-handoff.md
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
│   ├── Data/
│       ├── Configurations/PreScanResultConfiguration.cs
│       ├── Configurations/CommunicationKindClassificationConfiguration.cs
│       ├── Migrations/<timestamp>_AddConservativePreScanGate*
│       ├── Migrations/VulgataDbContextModelSnapshot.cs
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
├── RepositoryProfilingTests.cs
├── RepositoryApiTests.cs
└── MigrationTests.cs
```

**Structure Decision**: extend the existing profiling aggregate and repository
transaction. Add no coordinator, queue, or generic storage abstraction.

## Requirement-to-Design/Test Matrix

| Spec requirements | Design surface | RED/GREEN evidence |
| --- | --- | --- |
| FR-001–FR-003 | coordinator ordering, Profiled Revision capture, private committed-tree materialization, and one-snapshot profiling/detection | dirty checkout and unchanged-HEAD tracked-byte mutation publish no result; incomplete/failed profile never invokes detection; no LLM/network/package execution |
| FR-004–FR-007 | closed rules, independent confirmed-kind flags, deterministic sample reservation, streaming fingerprint | direct annotation/config confirms; import/package hints; both kinds survive >100 direct matches; warning and non-promotion |
| FR-008 | locator/value guards and DTO mapping | unsafe/oversized/path/source/secret inputs never persist or return |
| FR-009–FR-012 | result/evidence entities, composite commit draft, unique keys, transaction/replay | warning-only approval, no-op replay, changed replay rejection, rollback |
| FR-013–FR-015 | Core handoff read model with confirmed evidence, authorized Web service/DTO, structured Chinese log | working copy advances after approval but handoff stays at old revision; mismatched consumption fails closed; both-kind reserved confirmed evidence is returned; unauthorized non-disclosure; exact messages |
| FR-016–FR-019 | closed enums and excluded services/tables | zero jobs, Edges, endpoints, Uncertainties, or deferred confirmations |

## Verification Strategy

1. Snapshot/detector tests: safe committed-tree materialization, dirty and
   unchanged-HEAD mutation rejection, exact annotation/config matching, bounds,
   sorting, deduplication, deterministic sample
   reservation, >100 cross-kind direct matches, truncation warning, and
   non-promotion.
2. Profiling integration: ordering, atomic persistence, replay idempotency,
   immutable mismatch rejection, and failure rollback.
3. API/application integration: Core-to-Shared mapping including bounded
   confirmed evidence, ManagementAccess, Repository visibility, revoked access,
   redaction, stable old-revision handoff after checkout advancement, and
   mismatch rejection by the consumer.
4. PostgreSQL migration: clean apply, rollback to the preceding Story 4.2
   migration, re-apply, and `dotnet ef migrations has-pending-model-changes`.
5. Full non-final-image tests and Web build. The final clean-database
   two-Repository rehearsal and Docker build remain Story 11.5 responsibilities
   unless Story 4.3 changes container/runtime behavior.

## Planned-Change Authorization Trigger

The plan materially changes Scan classification and gate behavior and adds an
authorized typed read plus an additive migration. After an independent
plan-judge accepts, the root must pause for explicit user authorization before
`speckit-tasks`. Authorization does not waive TDD, migration, security,
combined review, E2E decision, or final verification.

## Complexity Tracking

No Constitution violation or new architectural layer is introduced.
