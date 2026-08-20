---
story_key: 12-2-lightweight-java-xml-pre-scan-and-prepared-scan-manifest
title: "Story 12.2: Lightweight Java/XML PreScan & Prepared Scan Manifest"
Status: in-progress
Epic: 12
Story: 2
created: 2026-08-20
depends_on:
  - 12-1-durable-scan-foundation-and-analysis-runtime
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

# Story 12.2: Lightweight Java/XML PreScan & Prepared Scan Manifest

## User story

As a **SystemOwner**,
I want a complete fast deterministic PreScan over my Java/XML Repositories,
so that Worker Agents receive an automatically selected, revision-bound File
Code Unit manifest without waiting for CodeGraphContext.

## Delivery boundary

Story 12.2 prepares an authorized Repository at one exact Profiled Revision. It
owns deterministic Java/XML enumeration, filtering and exact hashes;
revision-bound CodeGraph indexing; JDT-LS readiness; safe catalog-driven XML
parsing; Communication Kind Classifications with Evidence Locators and
provenance; bounded File Code Units; deterministic business-seeded ordering;
and one atomic Prepared Scan Manifest, Scan Context Snapshot, and approved
Pre-Scan Handoff.

PreScan performs no LLM call and grants no Worker dispatch authority. Story
12.3 owns Worker execution, `navigate_code`, Pass 1 policy, retry/lease policy,
and Code Logic Document generation. Stories 12.4-12.5 own Cross-Repository
Resolution, Pass 2, grounded Chat, and final release evidence.

## Acceptance criteria

### AC-1: Exact deterministic prepared scope

**Given** an authorized Repository and requested Git reference
**When** PreScan begins
**Then** Vulgata acquires one exact Profiled Revision, enumerates only contained
files, applies deterministic filters, identifies supported Java/XML inputs, and
calculates documented exact file/source hashes
**And** no partial or stale prepared scope becomes dispatchable.

### AC-2: Revision-bound analysis readiness

**Given** the prepared scope contains Java
**When** analysis services start
**Then** CodeGraph `@colbymchenry/codegraph@1.5.0` builds and reports a complete
revision-bound index
**And** JDT LS `jdt-language-server-1.61.0-202608141332` on Java 21 over stdio
initializes and returns document symbols for a known eligible Java file within
the bounded readiness timeout
**And** any readiness failure stops PreScan before LLM cost.

### AC-3: Safe closed XML communication evidence

**Given** relevant XML configuration is present
**When** the safe XML pipeline evaluates it
**Then** it prohibits DTDs, external entities, XInclude, schema-network fetches,
and custom resolvers; enforces file/depth/element/attribute bounds; and accepts
only the closed Communication Pattern Catalog selector grammar
**And** Confirmed, Hint, and Warning evidence retains exact Evidence Locators
and provenance without creating a final Cross-Repository Edge
**And** neutral fixtures contain no sample Repository names, paths, endpoints,
or line numbers.

### AC-4: Bounded deterministic File Code Units and atomic handoff

**Given** eligible bounded source files are retained
**When** the Prepared Scan Manifest is built
**Then** each file becomes one pending `CodeUnitType.File` File Code Unit
**And** only oversized files split at safe Java or parsed XML top-level
boundaries; unsafe files are explicitly skipped and never silently truncated
**And** business-seeded ordering uses bounded general evidence and CodeGraph
expansion with a stable path fallback
**And** the complete Prepared Scan Manifest, Communication Kind
Classifications, Scan Context Snapshot, and approved Pre-Scan Handoff commit
atomically before any later Worker dispatch may be considered.

## Inherited bounds and runtime authority

- The release scope is exactly two Java/XML Repositories and 50-150
  automatically selected File Code Units.
- Source bounds are 65,536 bytes and 2,000 lines; internal units are at most 64,
  rendered sections at most 32, and communication observations at most 256.
- Business-seeded ordering uses at most 20 seeds, two CodeGraph hops, 200
  expanded files, and five seconds after index readiness; timeout falls back
  deterministically and does not fail PreScan.
- CodeGraph targets less than 30 seconds per Repository, JDT-LS readiness less
  than 60 seconds, and PreScan less than two minutes per Repository.
- Vulgata, CodeGraph, Java 21, and JDT LS run on the Windows host. PostgreSQL 17
  is the only required Docker service. CodeGraphContext remains excluded.

## Authority reconciliation and specification questions

- Current schema-v3 workflow authority is unified and supersedes the requested
  historical `Standard` execution label. Its economical default profiles and
  all current gates apply.
- The dated Epic 12 overlay temporarily replaces legacy PRD FR-4.1 method-like
  Code Units and configuration-data exclusion with bounded Java/XML File Code
  Units. The specification must make the precedence and exact filter rules
  explicit.
- The legacy first-4,096-byte File Identity Hash does not satisfy the new exact
  hash requirement. The specification must define file/source hash algorithms,
  byte/encoding/newline semantics, and replay comparison without ambiguity.
- The specification must define complete CodeGraph index proof, eligible Java
  readiness-file selection, safe XML/catalog bounds, Java/XML split boundaries,
  stable ordering scores and ties, manifest identity/schema/ordinal/replay, and
  the transaction that prevents partial prepared state.
- Existing `PreScanResult` and approved-handoff seams are narrower than the
  Epic 12 design. Specification and plan must decide whether to extend or
  replace them without importing Backup B/demo-only state.
- `Prepared Scan Manifest`, `Scan Context Snapshot`, and `File Code Unit` are
  required terms but are not current glossary entries. Specification must
  resolve the terminology gap before planning completes.
- No repository ADR, `CONTEXT.md`, or `CONTEXT-MAP.md` exists in this checkout.

## Planned-change triggers

The plan necessarily changes Scan parsing, filtering, ordering, splitting, and
handoff behavior and may define analysis-tool use. After an independent
plan-only `ACCEPT`, the root must pause before task generation and request
explicit planned-change authorization. The request must explain triggers,
effects, implementation intent, affected plan sections and PRD clauses, exact
scope, and all remaining gates.

## Ownership and exclusions

The active sprint-loop root is the sole workflow-state writer and committer.
Definition ownership is limited to this story record, canonical activation,
Sprint tracker, the allocated `specs/011-*` feature, managed SpecKit pointer,
and terminology updates required by accepted definition artifacts. Production
and test ownership will be made exact by the accepted plan. Story 12.3 Worker
execution and Document generation, Stories 12.4-12.5, Backup B/demo-only state,
and unrelated work remain excluded.

## Dev Agent Record

### Context reference

- Canonical activation:
  `docs/bmad/implementation-artifacts/workflow-activations/12-2-lightweight-java-xml-pre-scan-and-prepared-scan-manifest.json`
- Proposed SpecKit short name: `java-xml-prescan-manifest`.
- Expected free allocation: `specs/011-java-xml-prescan-manifest`; the
  specification author owns the actual allocation.
- Workflow schema/execution: `3.0` / unified.
- Authority source commit:
  `29e889badc00db671fcabf80f50918e15381052c`.

### Context synthesis outcome

The independent read-only context synthesis returned `READY_FOR_SPEC` with no
unresolved authority contradiction. Story 12.1 is complete and bound to
ancestral commit `be821b58f55dcfd18a605435e55df96a9a76b0d7`. Current historical
PreScan code may supply seams but cannot override the dated Epic 12 authority
or reintroduce CodeGraphContext. Specification is the next owner; tasks and
production implementation remain unauthorized.
