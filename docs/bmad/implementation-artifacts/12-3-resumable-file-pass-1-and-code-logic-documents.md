---
story_key: 12-3-resumable-file-pass-1-and-code-logic-documents
title: "Story 12.3: Resumable File Pass 1 & Code Logic Documents"
Status: in-progress
Epic: 12
Story: 3
created: 2026-08-21
depends_on:
  - 12-1-durable-scan-foundation-and-analysis-runtime
  - 12-2-lightweight-java-xml-pre-scan-and-prepared-scan-manifest
references:
  - docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-17-august-30-bounded-scanning-recovery.md
  - docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-18-august-demo-host-runtime-and-jdtls-pin.md
  - docs/bmad/planning-artifacts/architecture/august-30-bounded-scanning-design.md
  - docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md
  - docs/bmad/epics.md
  - docs/UBIQUITOUS_LANGUAGE.md
  - AGENTS.md
  - .specify/memory/constitution.md
---

# Story 12.3: Resumable File Pass 1 & Code Logic Documents

## User story

As a **SystemOwner**,
I want bounded File Code Units analyzed concurrently with durable recovery,
so that Vulgata produces trustworthy Code Logic Documents without losing an
entire Scan to an LLM outage or one peripheral file failure.

## Delivery boundary

Story 12.3 consumes exactly two approved Prepared Scan Manifests and owns the
bounded shared Pass 1 queue, one Worker Agent job per prepared File Code Unit,
the closed `navigate_code` tool, authoritative `code-logic/file-v2` results,
deterministic Document projections, durable provider/job failure policy,
truthful partial completion, and exact same-Repository post-Pass1
reconciliation.

Story 12.4 Cross-Repository Resolution, endpoint-centered grouping,
`retrieve_code_logic`, Pass 2, and Business Logic Documents remain excluded.
Story 12.5 Chat, final release UI, evaluation questions, and release run remain
excluded. CodeGraphContext and unrestricted Agent tool loops remain excluded.

## Inherited authority

- Story 12.1 supplies deterministic Scan Job Execution identity, leases,
  atomic result/job commits, replay, restart recovery, revalidation, and manual
  resume contracts.
- Story 12.2 supplies exact Prepared Scan Manifest identity, canonical pair and
  Repository ordering, exact hashes, analysis-readiness proofs, Communication
  Kind Classifications, File Code Units, and pair-wide atomic replay.
- Vulgata, CodeGraph `@colbymchenry/codegraph@1.5.0`, Java 21, and JDT LS
  `jdt-language-server-1.61.0-202608141332` over stdio run on the Windows host.
  PostgreSQL 17 is the only required Docker service.
- Accepted Story 12.1 and Story 12.2 behavior may change only through a proven,
  explicitly routed compatibility correction.

## Planned-change triggers

The plan necessarily adds or materially changes a Worker Agent tool and Scan
scheduling, concurrency, retry, pause/resume, and dispatch behavior. It also
implements the dated Epic 12 replacement for legacy PRD clauses. After an
independent plan-only `ACCEPT`, the root must pause before task generation for
explicit planned-change authorization identifying triggers, effects,
implementation intent, affected plan sections, PRD clauses, and remaining
gates.

## Ownership

The active root is the sole lifecycle-state writer and committer. Workers may
write only within their explicit dispatch boundary and must not stage, commit,
delegate, switch branches, merge, rebase, reset, push, pull, or rewrite
history. Detailed evidence belongs in durable artifacts and Git; compact
workflow state uses schema 3.0.

## Dev Agent Record

### Context reference

- Canonical activation:
  `docs/bmad/implementation-artifacts/workflow-activations/12-3-resumable-file-pass-1-and-code-logic-documents.json`.
- Proposed SpecKit short name: `resumable-file-pass1-code-logic`.
- Expected feature directory: `specs/012-resumable-file-pass1-code-logic`.
- Workflow schema/execution: `3.0` / unified.
- Authority source commit:
  `433834aca53fae868812685a85a9c3c830824025`.

### Context synthesis outcome

The independent read-only context synthesis returned `READY_FOR_SPEC` with no
unresolved authority contradiction. No `CONTEXT-MAP.md`, `CONTEXT.md`, or
repository ADR exists in this checkout. Specification must make exact the
paired job and Document identities, authorized classification access, exact
Story 12.2 hash consumption, current-Document/source navigation behavior,
deferred-observation reconciliation, provider/failure accounting, and partial
completion rules. Architecture-bearing planning is expected; task generation
and production implementation remain unauthorized.
