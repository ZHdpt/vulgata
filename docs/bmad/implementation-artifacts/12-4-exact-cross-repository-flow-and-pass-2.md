---
story_key: 12-4-exact-cross-repository-flow-and-pass-2
title: "Story 12.4: Exact Cross-Repository Flow & Pass 2"
Status: done
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

### Planned-change authorization 01: ACCEPTED

The user explicitly approved scope
`683F10B848D93A843EF61F0CAC11518F9048E4FC7F5E23ACDAE116C3C8CF92E0`
on 2026-08-26. This authorizes dependency-ordered task generation from the
accepted plan. It does not authorize production implementation or waive the
combined definition review, implementation approval, TDD, convergence,
quality, E2E, verification, or completion gates.

## Task-generation result

The task author produced and self-reviewed 49 sequential tasks (`T001`–`T049`):
nine setup/foundation tasks, thirty-six tasks across five independently
testable user-story slices, and four production-qualification/final-barrier
tasks. The task set names separate test and production ownership, places
behavioral RED before GREEN implementation, covers FR-001–FR-043 and
SC-001–SC-012, includes the compatibility migration and disposable PostgreSQL
qualification, and excludes Story 12.5. The current on-disk task SHA-256 is
`5FA8B1074763B4D62BD9511BF1995018DB415587BDA99EB4F7ECBBF5462574DD`.
The next gate is one fresh combined definition/analyze review.

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

### Definition review attempt 1: REVISE

The combined definition review found one CRITICAL plan-owned gap and three
downstream task defects. The plan omitted the Constitution-mandated central
`MapVulgataApiEndpoints()` registration seam for the new Pass 2 API group. The
task set also allowed same-file parallel writes and ambiguous RED/GREEN order,
did not assign exact runtime embedding/loading of the accepted
`business-logic/flow-v1` schema, and attributed live qualification criterion
SC-012 to local test work instead of the approved two-Repository qualification.
The specification remains accepted. Correct the plan, regenerate tasks, and
run a fresh combined definition review before implementation approval.

The corrected plan now owns central registration through
`ApiEndpointRegistrationExtensions.MapVulgataApiEndpoints()`. The regenerated
49-task worklist serializes every same-file test owner and makes foundation RED
evidence precede GREEN work, uniquely embeds and verifies the accepted
`business-logic/flow-v1` schema for production loading, and assigns SC-012 only
to the durable sanitized live two-Repository qualification. Full task
self-review passed; definition-review attempt 2 is next.

### Definition review attempt 2: REVISE

Attempt 2 confirmed all four attempt-1 findings are closed without regression.
It found one HIGH tasks-only ownership gap: the planned Core
`IPreparedPass2WorkerDispatcher` abstraction and its RED contract test were not
assigned. Correct only `tasks.md` so foundation RED/GREEN work owns that Core
contract and the Agents/Web consumers depend on it, then run definition-review
attempt 3.

The task correction now assigns the dispatcher contract RED to T004, the Core
interface to T006, the concrete Agents implementation to T035, and the Web
consumer to T042 with explicit dependencies. All prior corrections remain
intact; definition-review attempt 3 is ready.

### Definition review attempt 3: ACCEPT

Attempt 3 accepted the complete corrected specification, plan set, and 49-task
worklist with zero findings. Every prior finding is explicitly closed without
regression, the accepted hashes match, and the delivery definition is ready
for the mandatory implementation-approval pause. No production write is
authorized by the review itself.

### Implementation approval: ACCEPTED

The user explicitly approved scope
`7D47E4DABBEC9A1C436FF3ABA003732A8E4D1A960EE0C539170D6ECE24BFEF7D`
on 2026-08-26. TDD implementation may begin at T001 while every downstream
convergence, quality, E2E, qualification, verification, and completion gate
remains mandatory.

### T001-T006 foundation checkpoint

The test owner observed 15 honest RED failures for the absent Pass 2 embedded
schema/prompt and Core public seams while six compatibility/opt-in assertions
passed. The minimal GREEN added the accepted external resources and Core v2
entities/contracts. After correcting one test-only repository-root traversal,
the focused suite passed 21/21 and the root's focused-plus-surrounding suite
passed 46/46 with zero errors. T007-T009 migration/relational work remains next.

### T007-T009 migration checkpoint

The migration owner observed four model/migration RED failures and two real
PostgreSQL RED failures before scaffolding additive migration
`20260826022909_ExactCrossRepositoryFlowPass2`. Two test-only provider defects
were corrected without weakening assertions. The root then verified 18/18
migration, disposable PostgreSQL, and Chat compatibility tests. Fresh apply,
rollback, reapply, nullable prepublication bindings, foreign keys, reusable
Edge uniqueness, legacy schema retention, no pending model changes, and
disposable database cleanup all passed; ordinary `vulgata` was not migrated.

### T010-T012/T014-T016 partial US1 checkpoint

Honest RED covered provider aggregation, safe HTTP rejection, deterministic
uncertainty, v2 persistence, replay drift, and the candidate-source seam. The
normalizer, resolver, decision/contribution store, and a persisted current-
candidate source are GREEN; stale tests were aligned to distinct-current-
Document provider cardinality. Root verification passed 64/64 tests with
disposable PostgreSQL cleanup. T013 remains intentionally open for terminal
Scan Execution/manifest/pair validation, deferred communication reconstruction,
revision-bound source/hash reread, and full catalog reproduction; T017 is not
claimed until those gaps close.

### T013/T017 US1 completion

Seven isolated behavioral REDs closed the remaining candidate-authority gaps:
terminal pair/manifests, persisted communication plus Cross-Repository deferred
evidence, revision-bound Source Anchor hashes, embedded catalog identities,
consumer endpoint derivation, provider XML endpoint reproduction, and manifest
proof bindings. Root verification passed 96/96 pure, disposable-PostgreSQL, and
legacy compatibility tests. Same-Repository provider-implementation Edge
traversal is retained for T019/T020 Flow Group completeness under AD-3; it is
not provider-selection evidence and no candidate-result seam was invented.

### T018-T023 US2 completion

Endpoint-centered planning now separates pair-specific Flow Group identities
from cross-scan `business-flow/v1` keys, preserves distinct decisions that share
one REFERENCES Edge, resolves the unique provider implementation through exact
same-Repository evidence, and performs canonical one-hop expansion. Invalid
core membership produces closed Incomplete reasons. Exact eight-Document,
sixteen-step, and ten-group bounds remain inclusive; plus-one cases are never
truncated, and the eleventh candidate becomes a content-dark omission. Atomic
PostgreSQL start, rollback, and replay create one job/budget only per Complete
group. Root verification passed 43/43 and EF reports no pending model changes.

### Completion checkpoint: 2026-08-27

Story 12.4 completed all forty-nine accepted tasks and every governed delivery
gate. Convergence attempt 1 found zero missing, partial, contradictory, or
unrequested obligations. Independent quality review attempt 1 returned ACCEPT
with zero findings. Required E2E predicates passed through the retained live
two-Repository qualification and current authenticated API, PostgreSQL, and
multi-module integration evidence.

Final verification passed 439 exact Cross-Repository/Pass 2 regressions with
42 explicit PostgreSQL opt-in skips and zero failures, 87/87 disposable
PostgreSQL checks, 64/64 authenticated composition/API checks, and 442 adjacent
regressions with one explicit prerequisite skip. The Web build completed with
zero errors. The approved live OpenCode Zen qualification completed one
Published Flow Group, one Pass 2 Job, one immutable Business Logic Document,
three `GENERATED_FROM` Edges, and three Source Anchors; its successful disposable
database and raw diagnostic outputs remain preserved as requested.

Completion checkpoint `bmad-12-4-complete-20260827T132524Z-d8e2d1` owns the
current Story 12.4 production, tests, specification, convergence, quality, E2E,
qualification, and verification boundary. Story 12.5 Chat and August release
evidence remain outside this completed boundary.
