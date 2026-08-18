---
story_key: 12-1-durable-scan-foundation-and-analysis-runtime
title: "Story 12.1: Durable Scan Foundation & Analysis Runtime"
Status: in-progress
Epic: 12
Story: 1
created: 2026-08-18
depends_on: []
references:
  - docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-17-august-30-bounded-scanning-recovery.md
  - docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-18-august-demo-host-runtime-and-jdtls-pin.md
  - docs/bmad/planning-artifacts/architecture/august-30-bounded-scanning-design.md
  - docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md
  - docs/bmad/planning-artifacts/architecture.md
  - docs/bmad/epics.md
  - docs/UBIQUITOUS_LANGUAGE.md
  - AGENTS.md
  - .specify/memory/constitution.md
---

# Story 12.1: Durable Scan Foundation & Analysis Runtime

## User story

As a **SystemOwner**,
I want Scan progress, context, and analysis-tool identity persisted durably,
so that a bounded two-Repository Scan can pause, restart, and resume without
losing trusted work.

## Delivery boundary

This is the first dependency-ordered story in the approved Epic 12 August 30
overlay. It establishes durable Scan foundations, analysis runtime identity and
health, typed analysis ports, and recovery/resume semantics. Story 12.2 owns
complete lightweight Java/XML PreScan and the Prepared Scan Manifest; Stories
12.3-12.5 own Worker generation, Cross-Repository Pass 2, and grounded Chat.

The August 30 bounded-scanning proposal and the August 18 host-runtime
correction are already approved. Those approvals establish planning authority;
they do not authorize task generation or implementation. Story 12.1 must
receive an independent plan-only `ACCEPT`, then explicit planned-change
authorization, before tasks are generated.

Historical Story 11.5 and `specs/008-demo-chat-release-gate` are excluded from
this story's definition and ownership.

## Acceptance criteria

### AC-1: Additive durable model and migration lifecycle

**Given** the August 30 migration is applied to a clean PostgreSQL database
**When** the model is inspected
**Then** it persists one immutable Scan Context Snapshot per Repository Scan
Run, deterministic per-file Scan Job Execution state, the required paused and
completed-with-warning phases, and one lightweight System-level coordination
record binding exactly two release Repositories
**And** existing immutable Document, Source Anchor, Edge, Uncertainty, and
prepared-demo rows remain readable
**And** apply, rollback, reapply, concurrent update, and no-pending-model-change
verification pass.

### AC-2: Reproducible host analysis runtime

**Given** production composition starts on the Windows demonstration host
**When** analysis dependencies are verified
**Then** CodeGraph CLI `@colbymchenry/codegraph@1.5.0`, its Windows x64 bundle,
and Eclipse JDT LS `jdt-language-server-1.61.0-202608141332` are pinned to the
approved identities and checksums
**And** JDT LS runs on Java 21 over stdio through StreamJsonRpc `2.25.29`, with
an isolated workspace per Repository revision
**And** PostgreSQL 17 is the only required Docker service for the August release
**And** CodeGraphContext, its Python runtime, and its qualification harness are
not required by production startup or the release gate.

### AC-3: Typed bounded analysis ports

**Given** Vulgata invokes analysis services
**When** Core or Worker code requests structural evidence
**Then** it uses typed CodeGraph, Java LSP, and revision-bound source-reader
ports
**And** raw CLI, raw LSP messages, internal CodeGraph storage, shell access,
network access, and unrestricted filesystem reads do not cross those ports.

### AC-4: Durable restart and manual resume

**Given** a Scan Execution is persisted with completed, pending, running, or
retry-scheduled jobs
**When** the application process restarts or an operator resumes it against the
same PostgreSQL database
**Then** committed Documents are retained, expired leases return to Pending,
and committed jobs do not duplicate Documents
**And** remaining jobs resume only after Repository access, Profiled Revision,
file hashes, prompt/schema identity, provider availability, and analysis-runtime
identity are revalidated.

## Authority reconciliation

- The August 18 correction replaces the August 30 application's Docker-runtime
  gate with a host-run Vulgata/CodeGraph/Java/JDT-LS topology and PostgreSQL-only
  Docker operation. PRD NFR-4.1 remains post-deadline V1 work.
- The dated August 30 design supersedes conflicting legacy CGC-dependent,
  method-level scheduling, orchestration, retry, retrieval, deployment, and
  release-gate details for Epic 12. Broader Epics 5-10 remain backlog.
- `delivery_scope.execution_level: standard` was stale metadata from the prior
  workflow version. This activation repairs it to `unified`; the new activation
  uses `schema_version: "2.0"` and `execution_level: "unified"`.
- `Scan` continues to target exactly one Repository. The approved design's new
  lightweight record coordinates two Repository Scan lifecycles; it is not a
  System-level Scan. Its final canonical Ubiquitous Language name must be
  decided and added during specification.
- The non-ancestral activation checkpoint `d91772004cf6ad56d4fcd38b37917958733f693e`
  is preserved as superseded evidence only. No files are imported from it.
- No repository ADR, `CONTEXT.md`, or `CONTEXT-MAP.md` exists in this checkout.

## Planned-change triggers

The plan-review gate must classify every applicable repository trigger:

1. material additions or changes to Worker Agent tools;
2. material Scan strategy changes, including parsing, filtering, scheduling,
   grouping, retry, resume, or dispatch behavior; and
3. conflicts with or temporary replacements of legacy PRD requirements.

The accepted Story 12.1 plan must explain runtime, persistence, migration,
retry/resume, and Scan effects, and cite the affected PRD/design clauses. The
subsequent human authorization permits progression to task generation only.

## Observed host qualification inputs

- `codegraph` resolves to `G:/packages/npm/prefix/codegraph.ps1` and reports
  `1.5.0`; its three approved installed-file SHA-256 values match the August 18
  correction.
- Java is pinned by machine configuration to
  `C:/Program Files/Java/latest/jdk-21` and reports `21.0.12`.
- JDT LS is supplied at
  `C:/Users/13728/.local/jdt-language-server-1.61.0-202608141332`; the observed
  core bundle is `org.eclipse.jdt.ls.core_1.61.0.202608141332.jar` with SHA-256
  `A58994113B7E493AA8CED51AD02DAB3E424973223E7213F7ED5B9D70861582E2`.
- No adjacent JDT LS distribution archive was observed. The plan must retain the
  authority requirement to record and verify the exact installed archive
  checksum before readiness passes; it must not treat the core-bundle hash as a
  substitute for that archive checksum.
- Absolute installation paths are host configuration only and must not enter
  domain identity, prompts, or persisted release identity without the approved
  normalization described by the August 18 correction.

## Existing implementation seams

- `ScanRun` and the narrow historical `ScanExecution` already persist a
  Repository-bound profiled revision and sequential prepared-run state.
- `VulgataDbContext`, `ScanExecutionRepository`, and the existing migration
  tests provide additive persistence, transactional, locking, concurrency, and
  reversibility seams that the plan must preserve or deepen.
- Production composition and Docker still include CodeGraphContext/Python
  qualification behavior. Epic 12 replaces that release-critical composition
  with version/checksum-pinned host CodeGraph and Java 21/JDT LS adapters.
- No current Scan Context Snapshot, Scan Job Execution, Java LSP adapter, or
  provider-unavailable paused phase exists.

## Ownership and exclusions

The active sprint-loop root is the sole workflow-state writer and committer.
Migrations, shared domain contracts, `VulgataDbContext`, and the model snapshot
remain under this single Story 12.1 integration owner. Dispatched workers must
not stage, commit, delegate, or change Git history.

Definition-stage ownership is limited to this story record, its activation,
the Sprint tracker, the allocated `specs/010-durable-scan-foundation` feature,
the managed AGENTS.md SpecKit pointer at the appropriate planning stage, and
the Ubiquitous Language addition required by the accepted specification.
Production/test ownership will be made exact by the accepted plan before task
generation. Story 11.5, `specs/008-demo-chat-release-gate`, Backup B/demo-only
UI state, and unrelated files remain excluded.

## Dev Agent Record

### Context reference

- Canonical activation:
  `docs/bmad/implementation-artifacts/workflow-activations/12-1-durable-scan-foundation-and-analysis-runtime.json`
- Proposed SpecKit short name: `durable-scan-foundation`.
- Proposed feature directory: `specs/010-durable-scan-foundation`; final mapping
  is written atomically after `speckit-specify` returns the actual directory.
- Workflow schema/execution: `2.0` / `unified`.
- Current workflow stage: `context`.
- Authority source commit:
  `f8b56ed4ab081d83bbdcfa050a6ff69a76ccba5d`.

### Context synthesis status

Pending fresh synthesis against the current authority commit. The earlier
non-ancestral context/spec/plan work is superseded and cannot advance this
activation.
