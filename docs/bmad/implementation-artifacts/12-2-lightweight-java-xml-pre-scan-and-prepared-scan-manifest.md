---
story_key: 12-2-lightweight-java-xml-pre-scan-and-prepared-scan-manifest
title: "Story 12.2: Lightweight Java/XML PreScan & Prepared Scan Manifest"
Status: done
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
- Allocated feature directory: `specs/011-java-xml-prescan-manifest`.
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

### Combined definition review iteration 5 acceptance

The explicitly authorized fifth review returned `ACCEPT`. The prepared-only
`PreparedScanManifestEntry` principal is EF Core/PostgreSQL feasible while
historical nullable `CodeUnit` fields remain non-key and require no backfill.
All qualification owners precede the sole final verification barrier. All 43
Functional Requirements and nine Success Criteria are covered, the August host
runtime pins and Story 12.3 exclusions remain intact, and no blocking finding
remains. DR5-001 is a nonblocking Medium implementation-partitioning note: T048
owns executable boundary tests and T051 owns the evidence record to avoid shared
file duplication. The raw verdict is preserved at
`specs/011-java-xml-prescan-manifest/reviews/definition-review/definition-review-05.json`.
The workflow is paused at the separate implementation-approval gate; production
implementation remains unauthorized.

### Task-generation outcome

`speckit-tasks` produced 45 dependency-ordered tasks across setup, foundational
contracts, four P1 vertical slices, migration, real-tool qualification, release
qualification, security boundaries, and final verification. Every behavioral
production task has an explicit public-seam RED predecessor and focused GREEN
owner; migration/model snapshot, persistence/replay, and API/DI shared ownership
is assigned solely to T036, T038, and T040. All 43 Functional Requirements and
nine Success Criteria are traced, and Story 12.3 Worker execution and Document
generation remain excluded. The task author self-review and `git diff --check`
passed. Fresh combined definition review is next; production implementation
remains unauthorized.

### Combined definition review iteration 1

Fresh independent combined review returned `REVISE`. DR-001 found that the
legacy Story 12.1 Scan Context Snapshot identifier includes `CapturedAt`, while
the Prepared Scan Manifest canonical envelope included that identifier in replay
identity; unchanged preparations at different instants could therefore diverge.
DR-002 through DR-005 require regenerated tasks to order XML ranges after the
safe XML projection, add explicit PostgreSQL atomic-store qualification, prove
three complete deterministic preparations and the two-minute per-Repository
bound, and correct nonbehavioral RED labels. The raw verdict is preserved at
`specs/011-java-xml-prescan-manifest/reviews/definition-review/definition-review-01.json`.
The plan owner is the earliest correction owner; tasks are downstream-invalid
and production implementation remains unauthorized.

### Plan correction after combined definition review iteration 1

The plan owner closed DR-001 with the additive
`PreparedScanContextIdentityHash` canonical domain over Repository, Scan Run,
Profiled Revision, snapshot schema, and content hash. Manifest identity now
references that stable hash; the legacy time-derived snapshot ID and
`CapturedAt` remain unchanged relational/audit metadata and are excluded from
replay identity. Historical Story 12.1 rows are neither backfilled nor
reinterpreted. The corrected plan also fixes the exact downstream test seams for
safe-XML-before-XML-split ordering, disposable PostgreSQL 17 store atomicity,
three-run/reversed-input release qualification with full per-Repository timing,
and nonbehavioral qualification labels. Task regeneration is next; production
implementation remains unauthorized.

### Task regeneration after combined definition review iteration 1

The task owner regenerated a 49-task dependency graph. It adds capture-time and
legacy-ID variance vectors, makes safe XML projection/range-index GREEN before
the sole XML split consumer, assigns disposable PostgreSQL 17 store atomicity to
an exact suite separate from migration lifecycle, requires three complete paired
preparations with reversed inputs and full per-Repository timing, and labels
workflow/security inspections as post-GREEN nonbehavioral qualification. All 43
Functional Requirements and nine Success Criteria remain traced. Fresh combined
definition review iteration 2 is next; production implementation remains
unauthorized.

### Combined definition review iteration 2

Fresh review returned `REVISE`. The stable Prepared Scan Context identity,
PostgreSQL atomic-store suite, full three-run qualification, and nonbehavioral
labels were accepted. DR2-001 found that XML split tasks were still physically
placed after their US4 consumers and release qualifications, with contradictory
parallel guidance. DR2-002 found the canonical audit-fingerprint production task
lacked an honest preceding RED owner. DR2-003 found two stale spec sentences
that still described glossary adoption as pending although the authoritative
glossary is already updated. The raw verdict is preserved at
`specs/011-java-xml-prescan-manifest/reviews/definition-review/definition-review-02.json`.
The spec is the earliest correction owner; plan and tasks are downstream-invalid,
and production implementation remains unauthorized.

### Plan-only review iteration 1

The independent reviewer returned `REVISE` with four High findings. PR-001
requires the specification to limit nonfatal fallback to post-readiness related-
file expansion. PR-002 requires distinct per-Repository manifest and pair-
selection ordinal domains. PR-004 requires a canonical Repository identity
comparator and exact high-value/cap semantics. PR-003 requires the regenerated
plan to define complete versioned canonical byte envelopes and UUID construction
without ID/fingerprint cycles. The raw verdict is preserved at
`specs/011-java-xml-prescan-manifest/reviews/plan-review/plan-review-01.json`.
Correction returns to the specification owner, then regenerates the complete
plan set before a fresh plan-only review. Tasks and production implementation
remain unauthorized.

### Definition correction after plan review iteration 1

The specification author closed PR-001, PR-002, and PR-004 without renumbering
the 43 Functional Requirements. It now distinguishes hard analysis readiness
from the sole post-readiness expansion fallback, defines Repository-local
Manifest Ordinals and pair-global Pair Selection Ordinals, fixes lowercase UUID
text comparison, and defines mandatory high-value seed/cap semantics with
golden cases. The plan architect regenerated every plan artifact and closed
PR-003 through the versioned noncircular canonical-envelope contract at
`specs/011-java-xml-prescan-manifest/contracts/canonical-envelope.md`, including
domain separators, exact scalar/list/nested encodings, field order, derived
exclusions, SHA-256, UUIDv8 construction, golden vectors, and independent
reconstruction tests. Fresh independent plan-only review iteration 2 is next.
Tasks and production implementation remain unauthorized.

### Planned-change authorization

The user explicitly approved scope
`a77c1118e1c4a33ab43094b554745fa2b1297c749a7853e63d9834e43a53e150`
on 2026-08-20. The workflow may proceed to dependency-ordered task generation.
The approval does not authorize production implementation; fresh combined
definition review and the separate implementation-approval gate remain
mandatory.

### Plan-only review iteration 2

The fresh independent reviewer returned `REVISE` with one High plan-owned
finding, PR2-001. PR-001, PR-002, and PR-004 from iteration 1 are independently
closed; the original PR-003 envelope cycle/grammar defect is mostly closed.
However, CodeGraph and Java readiness hashes still lack normative envelope
preimages and include volatile measured durations, so unchanged preparations
could produce different manifest fingerprints. The raw verdict is preserved at
`specs/011-java-xml-prescan-manifest/reviews/plan-review/plan-review-02.json`.
The plan owner must define stable proof-identity envelopes, move durations to
nonidentity audit telemetry, add proof golden vectors and independent replay
reconstruction, and regenerate downstream plan artifacts. Tasks and production
implementation remain unauthorized.

### Definition correction after plan review iteration 2

The plan architect regenerated every affected planning artifact for PR2-001.
CodeGraph completeness and Java readiness now have exact versioned canonical
proof-identity envelopes; the manifest references those stable hashes. Measured
duration, timestamps, process/resource telemetry, diagnostics, raw protocol
messages, absolute paths, tokens, and failed-attempt detail remain bounded audit
metadata and are excluded from replay identity. Literal proof vectors,
independent relational reconstruction, duration-variance replay, and stable-
predicate mutation conflict tests are required. Fresh independent plan-only
review iteration 3 is next. Tasks and production implementation remain
unauthorized.

### Plan-only review iteration 3 and authorization pause

Fresh independent review iteration 3 returned `ACCEPT` with no findings. It
independently closed PR-001 through PR-004 and PR2-001, revalidated all 43
Functional Requirements and nine Success Criteria, preserved the August host-
runtime architecture and Story 12.3 exclusion, and accepted the complete
planned-change disclosure. The raw verdict is preserved at
`specs/011-java-xml-prescan-manifest/reviews/plan-review/plan-review-03.json`.
The workflow is paused before task generation for explicit planned-change
authorization bound to scope
`a77c1118e1c4a33ab43094b554745fa2b1297c749a7853e63d9834e43a53e150`.
The pending request is at
`specs/011-java-xml-prescan-manifest/decisions/planned-change-authorization-01.md`.
Tasks and production implementation remain unauthorized.

### Specification outcome

`speckit-specify` allocated `specs/011-java-xml-prescan-manifest`. The author
self-review passed bounded intent, 43 measurable Functional Requirements, nine
Success Criteria, terminology, authority precedence, source-reference coverage,
Story 12.3 exclusions, and placeholder checks. No `[NEEDS CLARIFICATION]` item
remains. The root adopted Prepared Scan Manifest, File Code Unit, and Scan
Context Snapshot into the Ubiquitous Language glossary. Planning is the next
owner; task generation and production implementation remain unauthorized.

### Planning outcome

`plan-architect-high` completed the full `speckit-plan` set with an acceptable
inline self-review. The plan makes exact acquisition/hash, disposable analysis
workspace, closed CodeGraph/JDT-LS proof, safe XML/catalog, bounded split,
deterministic pair ordering, versioned prepared-package, pair-wide atomic
transaction/replay, migration, API, and verification decisions. All 43
Functional Requirements and nine Success Criteria are traced. The only
nonblocking capability risk is bounded CodeGraph relationship-type evidence;
unsupported behavior records a Warning and uses deterministic direct ordering,
never a broader tool. Independent plan-only review is the next owner. Tasks and
production implementation remain unauthorized.

### Combined definition review iteration 4 and mandatory pause

Fresh review returned `REVISE`. DR4-001 found that nullable historical-compatible
`CodeUnit` columns cannot simultaneously serve as the proposed EF Core alternate
principal key for selected-audit association; the next plan must use a feasible
prepared-only principal/entry shape or another explicit non-null principal
constraint while retaining every audit replay preimage. DR4-002 found final
verification physically precedes the production-literal, PostgreSQL atomicity,
and workflow/security qualifications whose evidence it requires. DR4-003 is a
stale quickstart provenance label. The raw verdict is preserved at
`specs/011-java-xml-prescan-manifest/reviews/definition-review/definition-review-04.json`.
Because this is the fourth definition-review `REVISE`, workflow policy pauses
before any fifth correction for explicit user direction. Production
implementation remains unauthorized.
