# Implementation Plan: Durable Scan Foundation & Analysis Runtime

**Branch**: `demo0831/main` | **Date**: 2026-08-18 | **Spec**: [spec.md](spec.md)

**Input boundary**: source commit `dd3111bebbe295aa43a01e95be7bbdb97d3eecff`; specification SHA-256 `DA24F90063D0B66D50D642BED8D1E1709257C6BF8B0B42E4AE89929CE5FEEFB2`.

## Summary

Story 12.1 replaces the historical single in-flight Scan slot with an additive durable foundation: one immutable Scan Context Snapshot, a per-file Scan Job Execution ledger, expanded per-Repository Scan Execution phases, and one bounded Two-Repository Scan Coordination record. A SystemOwner-initiated recovery coordinator revalidates authorization, revision, hashes, prompt/schema/provider/runtime identity before it conditionally returns expired or due work to an eligible state. Document persistence and the matching job-completion transition share one EF transaction, so recovery cannot observe a completed job without its one committed Document Version.

The analysis boundary is a set of Core-owned closed typed ports implemented only in Infrastructure. CodeGraph is the pinned `@colbymchenry/codegraph@1.5.0` Windows CLI; Java analysis is the pinned Eclipse JDT LS `1.61.0-202608141332` on Java 21 through StreamJsonRpc `2.25.29` over stdio. Host paths remain configuration. Persisted runtime identity contains only normalized release identifiers, approved relative file/archive components, hashes, Java/client versions, and Repository-revision workspace binding. Ordinary Web startup verifies integrity without indexing or importing a Repository. The JDT LS distribution archive checksum remains a fail-closed readiness input and is not replaced by the observed core JAR hash.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`); PowerShell only for operator validation commands

**Primary Dependencies**: EF Core 10.0.9, Npgsql 10.0.3, ASP.NET Core/Blazor, StreamJsonRpc 2.25.29, `@colbymchenry/codegraph@1.5.0`, Eclipse JDT LS `1.61.0-202608141332`, Java 21

**Storage**: PostgreSQL 17, schema `vulgata`, through `VulgataDbContext`; SQLite remains a focused test double but is not migration evidence

**Testing**: xUnit, existing `tests/Vulgata.Tests`, `WebApplicationFactory<Program>`, real disposable PostgreSQL 17, real CodeGraph/JDT-LS neutral-fixture qualification

**Target Platform**: Windows demonstration host for Vulgata.Web and analysis processes; PostgreSQL 17 is the only required Docker service

**Project Type**: Layered .NET web application with Core domain/ports, Infrastructure adapters/persistence, Shared API contracts, and Web composition/endpoints

**Performance Goals**: ordinary startup performs no Repository indexing; CodeGraph release target under 30 seconds per Repository and JDT-LS readiness under 60 seconds are preserved for later PreScan; recovery work is bounded by one Scan Execution and its manifest jobs

**Constraints**: exactly two Repositories for the dated coordination record; no automatic background resume; no raw shell/LSP/graph/filesystem/network access across public analysis ports; immutable Documents; additive migration; no CGC/Python release dependency; no machine-specific absolute path in persisted identity, prompts, or generated evidence

**Scale/Scope**: foundation for two Java/XML Repositories and 50-150 later File Code Unit jobs; Story 12.1 represents durable state and runtime seams but does not implement the Story 12.2 manifest or Story 12.3 dispatch policy

## Constitution Check

*GATE: passed before Phase 0 and rechecked after Phase 1.*

| Principle | Design response | Result |
| --- | --- | --- |
| I. Layered Project Boundaries | Entities and ports remain in Core without EF/process dependencies; EF, process control, hashing of installed files, and host paths remain in Infrastructure; Web owns composition/API; Shared owns request/response validation. | PASS |
| II. Domain Purity & Ubiquitous Language | Uses Scan, Repository, Profiled Revision, Document Version, Source Anchor, and Two-Repository Scan Coordination verbatim. EF mappings remain separate. Documents are not mutated by recovery. | PASS |
| III. Validation & API Contract Discipline | Manual-resume input is bounded and authorized; failures use ProblemDetails; no wrapping envelope or raw diagnostics. | PASS |
| IV. Test-First Integration Harness | Public seams permit honest RED tests before production code; real PostgreSQL migration/concurrency/restart and real-tool qualification are mandatory. | PASS |
| V. Security & Data Stewardship | Source access is revision-contained and range-bounded; process and protocol handles never cross ports; diagnostics are sanitized; no source is sent over a new network path. | PASS |

No Constitution exception is required. No system-wide or context ADR/`CONTEXT.md` exists in the pinned tree; the Constitution, AGENTS.md, Ubiquitous Language, approved proposals, dated design, and current source are the applicable authorities.

## Project Structure

### Documentation (this feature)

```text
specs/010-durable-scan-foundation/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── analysis-runtime-contracts.md
│   ├── durable-scan-contracts.md
│   └── manual-resume-api.md
└── tasks.md                         # later speckit-tasks output; not created here
```

### Intended Source Code (repository root)

```text
src/dotnet/
├── Vulgata.Core/
│   ├── Entities/                   # ScanContextSnapshot, ScanJobExecution,
│   │                               # evolved ScanExecution, TwoRepositoryScanCoordination
│   └── DomainServices/             # durable stores/recovery and closed analysis ports
├── Vulgata.Infrastructure/
│   ├── Analysis/CodeGraph/         # pinned CLI adapter, parser, integrity probe
│   ├── Analysis/Java/              # closed JDT-LS stdio adapter and workspace lease
│   ├── Analysis/Source/            # revision-contained bounded source reader
│   └── Data/
│       ├── Configurations/         # one EF configuration per entity
│       ├── Migrations/             # one cohesive additive migration + snapshot
│       ├── DurableScanRepository.cs
│       └── WorkerAgentResultCommitter.cs / KnowledgeStoreRepository.cs
├── Vulgata.Shared/                 # bounded manual-resume request/response + validator
└── Vulgata.Web/
    ├── Api/Scans/                  # ManagementAccess manual-resume endpoint
    └── Program.cs                  # host runtime options, health, DI; no production CGC registration

tests/Vulgata.Tests/
├── DurableScanning/                # domain, store, recovery, atomicity, authorization
├── AnalysisRuntime/                # contract doubles and real-tool qualification
├── Migrations/                     # PostgreSQL apply/rollback/reapply/compatibility
└── Fixtures/AnalysisRuntime/       # neutral contained Java/source fixture
```

**Structure Decision**: deepen the existing Core/Infrastructure/Web boundaries rather than add a project. The legacy CGC code and historical tests may remain, but the new production composition and release gate do not resolve or execute them.

## Architecture Decisions

### AD-1: Additive durable model on the existing per-Repository Scan Execution

Keep the existing `ScanExecutions` table and legacy phase values/columns readable. Add normalized prompt/schema/runtime bindings, pause information, update time, and a monotonic `StateVersion` concurrency token. Add `ScanContextSnapshots`, `ScanJobExecutions`, and `TwoRepositoryScanCoordinations`. New-path invariants are enforced by entity factories, EF constraints, composite foreign keys, unique indexes, and conditional checks; historical rows remain valid without fabricated backfill values.

Consequences: the historical sequential runner compiles and its rows remain readable, while the Epic 12 path stops treating `InFlightJobId` as its ledger. Some nullable compatibility columns remain until a later cleanup story; they are not read as new-path authority.

### AD-2: One cohesive additive migration owned by one integration owner

Use one migration because the new tables, expanded Scan Execution constraint, composite foreign keys, filtered uniqueness, and atomic commit invariant form one relational unit. `Up` adds tables/columns/indexes and replaces only the phase check. `Down` drops only Story 12.1 structures/columns and restores the prior check; it never edits or deletes existing Document, Source Anchor, Edge, Uncertainty, prepared-demo, Repository, or ScanRun rows.

Consequences: migration review is concentrated and rollback is mechanically testable. The migration, `VulgataDbContext`, configurations that establish shared keys, and model snapshot must be changed by the same integration owner.

### AD-3: Atomic Document/job completion is one Infrastructure transaction

Extend the knowledge-store write path with an internal transaction-aware commit primitive. The public durable commit port accepts the deterministic job identity, expected `StateVersion` and lease token for a new commit, bounded validated Document material, and authorization scope. Infrastructure authorizes and loads the deterministic job first. If it is already `Completed`, it verifies the full canonical job identity and the linked persisted Document graph/commit fingerprint against the incoming canonical result; an exact match returns `Committed` without mutation even though the original lease was cleared and `StateVersion` advanced, while every non-identical completed replay returns `Conflict`. Only a non-completed new commit proceeds to the `Running` expected-version/lease-token checks, shared `VulgataDbContext` transaction, knowledge materialization/write, and transition to `Completed` with one `CommittedDocumentVersionId`. No nested transaction is allowed.

Consequences: no completed-without-Document window exists and duplicate Document Versions are prevented by deterministic IDs plus unique/composite constraints. The shared knowledge write needs a small internal refactor but immutable Document behavior and existing public callers remain compatible.

### AD-4: Optimistic state versions plus short row locks, never external work under a DB lock

Each mutable execution/job/coordination row carries `StateVersion` as an EF concurrency token. Lease acquisition uses a conditional transition with a random lease token and expiry. PostgreSQL operations lock only the affected rows or use version-qualified updates inside short transactions. Authorization, Git/hash, provider, and runtime checks happen between an immutable read snapshot and a conditional write; any version drift returns `Conflict` and requires a fresh attempt.

Consequences: two writers cannot commit contradictory transitions, and slow process/provider checks do not hold database locks. SQLite remains useful for state tests but PostgreSQL is authoritative for concurrency evidence.

### AD-5: Recovery is explicit, idempotent, and policy-neutral

`IScanRecoveryCoordinator.ResumeAsync` is the only Story 12.1 resume entry point. The API returns 401 for an unauthenticated caller and 403 only when an authenticated caller fails `ManagementAccess`; after policy success, an ownership-filtered lookup returns 404 for both a nonexistent execution and an execution outside the caller's owned scope. For a visible execution, recovery verifies Repository access, Profiled Revision, affected File Code Unit hashes, prompt/schema identity, provider availability, normalized analysis-runtime identity, and durable cross-row integrity. Only after all checks pass does one transaction: reset expired `Running` jobs to `Pending`; move due non-exhausted `RetryScheduled` jobs to `Pending`; leave future retries, `Completed`, `Failed`, and `Cancelled` untouched; and move an eligible paused Scan Execution to `Prepared`. Repeated calls become no-ops. Invalid leases, mismatched Document links, or partial-state contradictions fail the execution closed without changing valid committed Documents.

Story 12.1 does not calculate retry windows, attempt budgets, lease duration, concurrency, or dispatch order. Story 12.3 supplies those policies and claims `Prepared`/`Pending` jobs. Automatic background resume is excluded.

### AD-6: Canonical runtime identity is separate from host configuration

Host options contain configured roots/executables, including Java home `C:/Program Files/Java/latest/jdk-21` and JDT-LS install root `C:/Users/13728/.local/jdt-language-server-1.61.0-202608141332`. A canonical identity serializer emits versioned UTF-8 JSON with sorted fields and lowercase normalized values for package/release IDs, approved relative executable/archive components, SHA-256 values, Java major 21, StreamJsonRpc 2.25.29, and Repository/Profiled Revision workspace binding. Its SHA-256 is stored with the Scan Execution. Absolute roots, usernames, drive layout, and temporary workspace paths are never persisted or sent to prompts.

Consequences: moving a valid installation does not change durable identity, but a version/hash/workspace-binding change does. The exact retained JDT-LS distribution archive and expected checksum remain required; the core JAR hash is diagnostics only.

### AD-7: Closed CodeGraph process adapter

Core exposes only index, rebuild, status, symbol search, callers, callees, and related-files operations. Infrastructure maps them to fixed argument templates: `init`, `index --quiet`, `status --json`, `query --json`, `callers --json`, `callees --json`, and `impact --depth 1 --json`. Repository roots and limits come from trusted bindings, not Agent strings. Output is byte-, item-, depth-, path-, and time-bounded before conversion to typed evidence. `explore`, `node`, daemon, unlock, install, upgrade, telemetry mutation, arbitrary query text/flags, shell interpolation, and `.codegraph` storage are not exposed.

Ordinary startup runs identity/hash/version checks only. It does not invoke `init`, `index`, or any command that creates `.codegraph`.

### AD-8: Closed JDT-LS stdio adapter with exclusive revision workspace

Infrastructure launches the one exact Equinox launcher from the pinned distribution through configured Java 21, using `config_win` and a workspace derived under a configured root from Repository ID plus Profiled Revision. An exclusive workspace lease and identity marker prevent concurrent reuse and any reuse across different revisions. StreamJsonRpc 2.25.29 owns stdio JSON-RPC framing behind the adapter; only document symbols, definition, references, and implementations cross the Core port. Startup, initialize/initialized, request cancellation, bounded response parsing, shutdown/exit, stderr sanitization, and forced process-tree cleanup are adapter responsibilities.

Ordinary startup verifies files, archive checksum, Java major, and configuration without importing a project. Explicit real-tool qualification may start JDT LS on the neutral fixture. Story 12.2 later makes initialized/import/document-symbol readiness a PreScan gate.

### AD-9: Revision-contained source reader

The source port accepts a Repository/Profiled Revision binding, a canonical Repository-relative path, an exact one-based range, and a byte cap. Infrastructure resolves beneath the already verified working-copy root, rejects links/escapes/stale bindings, reads once, verifies the expected source hash, and returns a bounded UTF-8 excerpt with line metadata. It exposes no directory listing, arbitrary file handle, or raw filesystem path.

### AD-10: Host composition and historical CGC isolation

Web composition registers the new runtime options, health service, closed ports, durable stores, recovery coordinator, and manual-resume endpoint. Production/default release composition does not register `CodeGraphContextProvider` or require its HTTP/Python qualification path. Historical code/tests may stay. `docker-compose.yml` places the application service behind an explicit `post-deadline-app` profile, so default composition starts only PostgreSQL while Vulgata.Web runs on the host. `docker/Dockerfile` is not modified or used as a Story 12.1 release artifact.

## Persistence and Migration Detail

- `ScanContextSnapshots`: unique `(ScanRunId, RepositoryId)`; immutable after insert; bound by composite FK to the exact Scan Run; bounded context fields plus schema/hash/time. `CapturedAt` must be on/after the bound `ScanRun.StartedAt` and on/before the associated new-path `ScanExecution.StartedAt`; snapshot insertion and execution creation each validate the ordering when they are the second record written.
- `ScanJobExecutions`: unique deterministic ID, `(ScanExecutionId, FileCodeUnitId)`, and `(ScanExecutionId, ManifestOrdinal)`; composite FKs preserve Repository/ScanRun ownership; filtered unique committed Document Version; closed state/failure checks; cross-column checks for lease, retry, and completion shape.
- `ScanExecutions`: retain all old columns and phase atoms; add new phase atoms and nullable new-path binding columns, `PauseCategory`, `PauseCode`, `UpdatedAt`, and `StateVersion`. New entity factories require complete bindings; old rows are not silently upgraded.
- `TwoRepositoryScanCoordinations`: exactly two distinct Repository/Scan Run/Scan Execution/Profiled Revision bindings in one System, no per-file state, closed combined phases, bounded reason, timestamps, and `StateVersion`.
- PostgreSQL uses the existing `vulgata` schema and UTC `timestamp with time zone`; all deletes are Restrict/NoAction for durable evidence.
- Migration verification must exercise clean apply, rollback, reapply, no pending model changes, protected legacy fixture readability before/after, and concurrent stale writes. A uniquely named disposable database is dropped after evidence capture.

## Runtime, Retry/Resume, and Scan Effects

| Area | Exact implementation intent | Observable effect |
| --- | --- | --- |
| Runtime | Pin CodeGraph package/command/bundle and all three approved hashes; pin JDT LS, Java 21, StreamJsonRpc, archive checksum, and isolated workspace identity. | Runtime mismatch is a classified NotReady result before Worker dispatch; startup creates no index. |
| Persistence | Replace the single in-flight slot as new-path authority with per-file durable rows and immutable context; retain legacy columns for compatibility. | Restart reconstructs progress from PostgreSQL and never from process memory. |
| Migration | One additive migration with protected legacy fixtures and reversible constraints. | Existing knowledge and historical demo data remain readable and semantically unchanged. |
| Retry/resume | Persist attempts, next-attempt time, failure classification, lease token/expiry, committed Document ID; manual resume recovers only expired/due eligible work after revalidation. | Recovery is idempotent; future retries and terminal jobs remain untouched; automatic resume remains absent. |
| Scan | Add durable per-Repository phases and exactly-two coordination without redefining Scan or replacing the Scan Coordinator. | Later stories gain restart-safe foundations; no Java/XML selection, Worker policy, Pass 2, Chat, or production progress UI is implemented here. |

## Authority and Requirement Traceability

| Plan decisions | Spec requirements | Authority clauses |
| --- | --- | --- |
| AD-1 through AD-5, migration detail | FR-001-FR-013, FR-028-FR-030; SC-001-SC-003, SC-006-SC-009 | August 30 design §§2, 8, 9.1, 10, 14-16; August 17 proposal §§4-6, 9-12; PRD overlay lines 24-45 and NFR-3.1-NFR-3.3 |
| AD-6 through AD-8, AD-10 | FR-014-FR-024, FR-027-FR-028; SC-004, SC-008 | August 30 design §§3, 3.1, 5, 14-17; August 18 proposal §§1, 3-7; PRD NFR-4.1 August 30 exception |
| AD-7 through AD-9 | FR-023-FR-027; SC-005 | August 30 design §§2-3, 5; PRD NFR-2.3 and dated FR-4.9 replacement |
| AD-5 and explicit exclusions | FR-031-FR-032 | August 30 design §§4, 6-13, 15-16; Epic 12 Story 12.1 and dependency order |

No specification requirement is unassigned. Detailed field and transition mapping is in [data-model.md](data-model.md); port behavior is in [contracts](contracts/).

## Planned-Change Authorization Triggers

This plan must not advance to tasks until independent plan-only review returns ACCEPT and the root obtains explicit authorization for all applicable triggers:

1. **Worker Agent/analysis tools**: AD-7 through AD-9 add closed CodeGraph, Java LSP, and source-reading capabilities and replace the production CGC boundary. Affected design §§3 and 5; PRD FR-4.9 and optional FR-15.1/15.2 are materially replaced/activated for the dated release.
2. **Scan strategy**: AD-1 through AD-5 change durable job representation, phase transitions, leases, recovery, and exactly-two coordination. Affected design §§8-10; PRD FR-4.1-FR-4.5 and NFR-3.1-NFR-3.3.
3. **PRD conflict/replacement**: AD-7/AD-8 replace CGC/tree-sitter assumptions, AD-5 defers automatic/background behavior, and AD-10 uses host runtime/PostgreSQL-only Docker for August. Affected PRD overlay lines 24-50, FR-4.1-FR-4.5, FR-4.9, NFR-1.1, NFR-4.1 and its dated exception.

The later request must state that approval permits only progression to task generation and does not waive TDD, independent definition review, convergence, quality review, E2E, security, migration, or final verification gates.

## Implementation Ownership and Dependency Order

| Owner | Exclusive paths/responsibility | Must follow |
| --- | --- | --- |
| Durable-domain owner | New/evolved Core entities and Core durable recovery/store contracts | Accepted behavioral REDs; no EF/process references |
| Analysis-runtime owner | Infrastructure CodeGraph/JDT-LS/source adapters and adapter-focused tests | Closed Core contracts; pinned package/runtime decisions |
| Recovery/API owner | Recovery coordinator, Shared manual-resume DTO/validator, Web API authorization tests | Durable store and revalidation ports |
| **Single integration owner** | `VulgataDbContext`, all new EF configurations, migration/designer/model snapshot, knowledge/job atomic commit refactor, shared Core contracts when integration requires them, central package version, Infrastructure package reference, `Program.cs`, and the `docker-compose.yml` application profile change | Domain/runtime/API branches green; owns all cross-cutting resolution |
| E2E owner | Real PostgreSQL restart/migration/concurrency, real CodeGraph/JDT-LS neutral fixture, host composition, security-negative evidence | Integrated candidate only |

No two dispatched implementers may concurrently edit migration/model-snapshot/`VulgataDbContext`, shared contracts, `Directory.Packages.props`, `Program.cs`, or composition files. Dependency order is: behavioral contract REDs -> Core state/identity -> Infrastructure stores/adapters -> atomic integration and migration -> recovery/API -> composition -> focused suite -> PostgreSQL/tool/restart E2E. Task generation may refine grouping but must preserve this single-writer ownership.

## Test, E2E, Security, and Failure Seams

- Domain tests cover every legal/illegal transition, deterministic identity, canonical hash, exactly-two invariants, lease boundaries, retry dates, completion linkage, and idempotent replay.
- Store tests use stale `StateVersion`/lease tokens, competing claim/complete/cancel operations, and deliberate cross-row contradictions.
- Atomicity tests inject failure before/after Document materialization and before job completion, proving rollback leaves neither a partial Document graph nor a completed job. A lost-response retry against an already Completed job must return `Committed` without mutation despite the cleared lease/advanced version; changing any canonical job or Document identity must return `Conflict`.
- Recovery tests restart a real application process against the same PostgreSQL database with mixed job states and repeat manual resume.
- Migration tests use clean and legacy fixtures; apply/rollback/reapply; inspect pending model changes; and verify immutable Documents, Source Anchors, Edges, Uncertainties, and prepared-demo rows.
- Runtime contract tests use fake launchers to assert exact executable/argument allowlists, byte/item/time limits, cancellation, malformed output, sanitized diagnostics, shutdown, and process-tree cleanup.
- Real CodeGraph qualification verifies package/version/bundle/three hashes, allowed JSON commands on a neutral Repository, and no `.codegraph` creation during ordinary startup health.
- Real JDT-LS qualification is mandatory once the exact distribution archive and checksum are supplied: Java 21, initialize/initialized, neutral document symbols plus definition/references/implementations, cancellation, shutdown/exit, forced cleanup, and cross-revision workspace rejection.
- Security tests reject traversal, links/escapes, stale revision, cross-Repository evidence, unbounded ranges/results, raw flags/messages/commands, environment leakage, network requests, and unsanitized stderr. API tests separately prove unauthenticated -> 401, authenticated policy failure -> 403, and policy-authorized ownership-filtered miss (including another System's execution) -> indistinguishable 404.
- Temporal persistence tests prove snapshot capture at/after `ScanRun.StartedAt` and at/before the associated new-path `ScanExecution.StartedAt` for both snapshot-first and execution-first insertion orders; either out-of-order timestamp is rejected without a partial insert.
- Any missing archive/checksum, wrong version/hash/Java major, malformed response, lifecycle timeout, workspace collision, revalidation mismatch, or integrity contradiction yields a bounded classified result and no Worker dispatch.

## Plan-Review Remediation

| Finding | Corrected semantics | Affected sections |
| --- | --- | --- |
| PR-001 | Already Completed replay is evaluated before the new-commit Running lease/version branch. Full canonical job and persisted Document identity match returns mutation-free `Committed`; every non-identical completed replay returns `Conflict`. | AD-3; atomicity tests; `contracts/durable-scan-contracts.md` |
| PR-002 | 401 is unauthenticated; 403 is authenticated but fails `ManagementAccess`; after policy success, ownership-filtered lookup miss is 404 for nonexistent or out-of-scope execution without an unfiltered existence probe. | AD-5; security tests; `contracts/manual-resume-api.md`; `contracts/durable-scan-contracts.md` |
| PR-003 | Snapshot `CapturedAt` is on/after bound `ScanRun.StartedAt` and on/before associated new-path `ScanExecution.StartedAt`; whichever record is inserted second validates the cross-record ordering. | persistence detail; temporal tests; `data-model.md` |

## Remaining Qualification Input

The supplied JDT-LS install root and Java 21 runtime are usable planning inputs, but the exact retained `jdt-language-server-1.61.0-202608141332` distribution archive and its expected SHA-256 are not present. Readiness and successful real JDT-LS qualification must remain NotReady until both are supplied and verified. The observed `org.eclipse.jdt.ls.core_1.61.0.202608141332.jar` hash is explicitly non-substitutive.

## Inline Author Self-Review

| Lens | Review | Result |
| --- | --- | --- |
| Feasibility | Existing `ScanExecution`, `VulgataDbContext`, deterministic Document commit, authorization, and PostgreSQL seams support additive evolution. Completed replay can compare existing canonical commit identity before entering the Running mutation branch. StreamJsonRpc is added only to Infrastructure. | PASS |
| Repository fit | The design deepens existing specific repositories, knowledge materialization, ownership-filtered API patterns, composite keys, and Web composition without adding a project or exposing EF/process types. | PASS |
| Invariants | Composite FKs, unique indexes, timestamp ordering checks, state checks, canonical identities, state versions, lease tokens, and one transaction make the specification invariants enforceable at domain and relational layers. | PASS |
| Failure behavior | Exact Completed replay is mutation-free; non-identical replay conflicts. External checks occur outside locks; conditional writes detect drift; runtime/process failures are bounded and fail closed; valid committed Documents are never rolled back by recovery. | PASS |
| Test seams | Clock, process launcher, filesystem resolver, provider/runtime probes, ownership-filtered lookup, stores, and recovery coordinator are injectable. Lost-response replay, all three API authorization outcomes, both temporal insertion orders, real PostgreSQL, and real-tool boundaries are separately testable. | PASS |
| Migration/compatibility | One additive migration retains old phase atoms/columns and protected rows; Down restores the previous constraint and removes only new Story 12.1 data. Historical CGC code may remain but is absent from release composition. | PASS |
| Authorization/security alignment | 401/403/404 semantics are deterministic and privacy-preserving; no unfiltered existence probe is allowed. Source/runtime containment and sanitized diagnostics remain unchanged. | PASS |
| Constitution/ADR alignment | All five principles pass. No ADR/CONTEXT authority exists to contradict the plan. Ubiquitous Language and dated correction authorities are used verbatim. | PASS |
| Story boundary | Durable representation/runtime/recovery seams are complete; Story 12.2/12.3 policies and all 12.4/12.5 behavior remain excluded. | PASS |
| Placeholder/clarification audit | No `NEEDS CLARIFICATION`, template token, or unresolved design choice remains. The missing JDT archive checksum is an explicit readiness prerequisite, not a planning ambiguity. | PASS |

**Self-review verdict: PASS.** The plan is ready for independent plan-only definition review. It does not authorize tasks or implementation.

## Complexity Tracking

No Constitution violation or additional project is introduced.
