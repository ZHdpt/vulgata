---
story_key: 12-4-exact-cross-repository-flow-and-pass-2
title: "Story 12.4: Exact Cross-Repository Flow & Pass 2"
Status: in-progress
Epic: 12
Story: 4
created: 2026-08-26
depends_on:
  - 12-1-durable-scan-foundation-and-analysis-runtime
  - 12-2-lightweight-java-xml-pre-scan-and-prepared-scan-manifest
  - 12-3-resumable-file-pass-1-and-code-logic-documents
references:
  - docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-17-august-30-bounded-scanning-recovery.md
  - docs/bmad/planning-artifacts/architecture/august-30-bounded-scanning-design.md
  - docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md
  - docs/bmad/epics.md
  - docs/UBIQUITOUS_LANGUAGE.md
  - AGENTS.md
  - .specify/memory/constitution.md
---

# Story 12.4: Exact Cross-Repository Flow & Pass 2

## User story

As a **SystemOwner**,
I want exact communication evidence converted into bounded cross-Repository
business flows,
so that Vulgata explains a real process without inventing provider targets or
unsupported relationships.

## Delivery boundary

Story 12.4 consumes the current authorized Code Logic Documents, deferred
communication observations, Source Anchors, and exact same-Repository
`REFERENCES` Edges produced by Story 12.3. It owns deterministic
Cross-Repository Resolution for the two release Repositories, endpoint-centered
Flow Groups, bounded `retrieve_code_logic`, one Pass 2 Worker Agent job per
complete Flow Group, immutable Business Logic Documents, and complete
`GENERATED_FROM` provenance.

The release boundary includes catalog-driven Custom RPC and only safely
normalizable exact HTTP. MQ, file/storage, page navigation, fuzzy matching,
indirect object flow, raw CodeGraph proximity membership, and LLM-selected
provider decisions are excluded. Story 12.5 owns Chat, production progress UI,
and final August release evidence.

## Acceptance criteria

### AC-1: Exact deterministic Cross-Repository Resolution

Given current authorized consumer and provider evidence exists across the two
release Repositories, when Cross-Repository Resolution runs, it matches only by
protocol and exact normalized endpoint identifier. Exactly one unique current
provider may create a Cross-Repository `REFERENCES` Edge. No provider, multiple
providers, conflicts, stale evidence, or unsupported communication creates or
retains an honest Uncertainty.

### AC-2: Endpoint-centered bounded Flow Groups

After Cross-Repository Resolution, deterministic Pass 2 planning groups by
System, protocol, normalized endpoint, consumer entry, and provider Repository.
A Flow Group may contain the consumer Code Logic Document, exact Edge, provider
XML Document, provider implementation Document, and at most one hop of validated
same-Repository references. Every member is current, anchored, authorized, and
bound to the relevant Profiled Revision. Raw CodeGraph proximity and LLM output
cannot add members or change the provider decision.

### AC-3: Closed supporting-knowledge retrieval and provenance

The Pass 2 Worker Agent may use `retrieve_code_logic` to retrieve bounded
current Code Logic Document sections, Source Anchors, source excerpts, and
related Documents inside the authorized System. Every additional Document
actually used is revalidated and receives a `GENERATED_FROM` Edge from the
Business Logic Document Version. Direct Pass 2 JDT-LS/CodeGraph verification is
optional and may be cut without weakening mandatory Document/source retrieval.

### AC-4: Honest partial completion

If a Flow Group fails or lacks required Documents, Pass 2 omits that group with
a visible warning and never synthesizes from incomplete evidence. Remaining
complete groups may finish without changing the deterministic resolution facts
or fabricating Business Logic Documents.

## Authority constraints

- Cross-Repository Resolution is deterministic application code, not an LLM
  agent, and uses `(protocol, normalized endpoint identifier)`.
- Initial bounds are two Repositories, eight Code Logic Document Versions,
  sixteen evidence steps, ten Flow Groups, and one relationship hop.
- `retrieve_code_logic` is initially bounded to eight calls, ten results, four
  additional Documents, four sections per Document, and 64 KiB.
- Documents are immutable; persistence remains additive and transactional.
- Prompts remain externalized embedded resources and provider/source data
  stewardship follows the Constitution and configured provider boundary.

## Activation record

Activated from `6aed7057e9e371d35c4123c0a204dd4849803eea` on branch
`demo0831/story-12-4`. Story 12.1, 12.2, and 12.3 are tracker-complete. The
pre-existing modification to
`artifacts/qualification/story-12.2/production-literal-inspection.json` is
unrelated user work and is excluded from Story 12.4 ownership.

The next stage is bounded specification authoring. Planning must surface the
repository planned-change triggers for Cross-Repository Resolution strategy,
Pass 2 grouping/dispatch, and `retrieve_code_logic` before task generation.

## Specification checkpoint preparation

The bounded specification at `specs/013-exact-cross-repository-flow-pass2`
passed its author self-review in one iteration with five user scenarios,
twenty-four acceptance scenarios, forty-three functional requirements, and
twelve measurable success criteria. It retains the exact provider key,
unique-current-provider rule, honest Uncertainty outcomes, endpoint-centered
bounds, closed `retrieve_code_logic`, complete `GENERATED_FROM` provenance,
immutable Documents, and Story 12.5 exclusion. No placeholder, unresolved
clarification, or authority conflict remains. The next owner is the
architecture-sensitive planning stage.

## Plan-review preparation

The architecture-sensitive SpecKit plan and its research, data model,
quickstart, strict Business Logic result schema, exact-resolution/Flow Group,
runtime/commit, and `retrieve_code_logic` contracts passed author self-review.
The design deepens the accepted Story 12.1–12.3 stores and Worker execution
seams, uses one cohesive compatibility migration, preserves deterministic and
immutable replay behavior, and defines pure, PostgreSQL, authenticated-host,
and approved two-Repository qualification layers.

Independent plan review is mandatory because the plan materially changes Scan
strategy through Cross-Repository Resolution, endpoint-centered grouping,
scheduling, omission accounting, and Pass 2 transitions; adds the Worker Agent
tools `retrieve_code_logic` and `submit_business_logic`; and retains the dated
exact RPC/safe HTTP narrowing of PRD FR-4.5, FR-6.1, and FR-6.2. Task generation
cannot begin until that review accepts and the user explicitly authorizes the
accepted plan scope.

### Plan review attempt 1: REVISE

Independent review found two HIGH plan-owned defects. The corrected plan must
separate pair-specific Flow Group execution identity from the cross-scan stable
Logical Document key so later scan pairs append immutable Version Chains. It
must also aggregate duplicate evidence for the same provider and decide
uniqueness over distinct eligible current provider Document Versions, rather
than treating evidence multiplicity as provider ambiguity. Both corrections
remain inside the accepted Story 12.4 specification; all downstream definition
and implementation stages remain unstarted.

The correction now defines distinct pair-specific `FlowGroup.Id` and
cross-scan `business-flow-logical-key/1.0` identities, including current-pointer
locking, successor publication, endpoint evolution, replay, and concurrency.
It also aggregates provider evidence by distinct current provider XML Document
Version before uniqueness cardinality while retaining every contributing
observation as normalized decision evidence. The corrected companion set passed
the full plan self-review and is ready for plan-review attempt 2.

### Plan review attempt 2: REVISE

Attempt 2 confirmed the prior identity and provider-cardinality findings are
closed. It found one new HIGH plan-owned defect: exact endpoint/consumer-entry
decisions were still one-to-one with the existing document-level `REFERENCES`
Edge, whose source/target/type uniqueness cannot retain multiple endpoint
decisions between the same Documents. The plan must give exact-match decisions
their own durable identity, allow many decisions to reuse one Document Edge,
and seed distinct Flow Groups from those decisions with compatible replay,
migration, and concurrency behavior.

The corrected plan now models one immutable
`CrossRepositoryResolutionDecision` per exact consumer candidate/outcome and
one reusable graph-level `DocumentEdge` per source/target/type tuple. Multiple
endpoint or consumer-entry decisions may share that Edge while retaining their
own evidence contributions, fingerprints, replay identities, Flow Groups,
omission accounting, and Worker bindings. Unresolved decisions own an
Uncertainty and no Edge binding. The compatibility migration does not infer an
unsafe v2 backfill from legacy one-to-one evidence. Full self-review passed;
plan-review attempt 3 is next.

### Plan review attempt 3: ACCEPT

Attempt 3 accepted the complete corrected plan with no findings. All pinned
hashes remained unchanged, the three earlier plan findings are closed, and no
authority conflict, Constitution violation, placeholder, or unresolved design
gap remains. The accepted scope now pauses at planned-change authorization
`story-12-4-planned-change-authorization-01`; task generation has not begun.
