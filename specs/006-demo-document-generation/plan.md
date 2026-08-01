# Implementation Plan: Demo Document Generation

**Branch**: `codex/story-11-3` | **Date**: 2026-08-01 | **Spec**: [spec.md](spec.md)
**Source boundary**: `c59171b2e5fb726f0ca40e15dcdeea2749744d01`
**Execution level**: `expedited-mvp`

## Summary

Complete the prepared August 15 document-generation path by extending the
existing Sequential Scan Runner with two closed Worker Agent variants. Pass 1
hydrates and verifies one bounded Code Unit source slice from the exact
Profiled Revision, invokes the centrally pinned Microsoft Agent Framework
OpenAI-compatible adapter with an embedded prompt, validates a strict Code
Logic result, and commits one authorization-scoped `KnowledgeCommit` before
progress changes. After every prepared Pass 1 commit, the Scan stays at
`Pass1Completed`, resolves exactly one embedded, version-controlled prepared
flow into a Pass 2 job containing exact Code Logic Document Versions, commits
one Business Logic Document with exact `GENERATED_FROM` Edges, and only then
reaches `Completed`.

The design reuses the Story 11.1 knowledge-store schema and Story 11.2 durable
execution rows. It adds no migration, public API, UI, Chat Agent tool, provider
fallback, retry policy, concurrency, flow discovery, clustering, or general
Cross-Repository Resolution. Story 11.2 quality finding F003 is corrected and
covered before production dependency injection makes the runner reachable.

## Technical Context

**Language/Version**: C# 14 on .NET 10 (`net10.0`)
**Primary Dependencies**: centrally pinned `Microsoft.Agents.AI.OpenAI` 1.13.0,
`Microsoft.Extensions.AI.Abstractions` 10.7.0, `System.Text.Json`, existing
Data Protection/provider configuration, EF Core/Npgsql 10, and existing Core
Worker Agent, committed-snapshot, Scan execution, and knowledge-store ports
**Storage**: existing PostgreSQL 17 `vulgata` schema; relational SQLite only in
the integration harness; embedded Markdown prompts and one embedded prepared
flow JSON manifest; no schema change
**Testing**: xUnit in `tests/Vulgata.Tests`; strict contract/unit tests, SQLite
relational integration, PostgreSQL knowledge-store integration where the
existing harness requires real locking, `WebApplicationFactory<Program>` DI
coverage, and a loopback OpenAI-compatible HTTP fixture (no live provider)
**Target Platform**: Linux container and Windows/Linux development hosts
**Project Type**: multi-project Blazor server application with internal
deterministic scan runner and bounded LLM adapter
**Performance Goals**: exactly one provider call and one knowledge commit per
prepared job; source hydration and response validation are linear in bounded
input size; no additional scheduling fan-out
**Constraints**: exact Repository/ScanRun/Code Unit/Profiled Revision binding;
65,536 UTF-8-byte hydrated source-slice cap; 2,000-line slice cap; 131,072
UTF-8-byte total provider-request cap; 65,536 UTF-8-byte provider-response cap;
closed schemas with no duplicate or unknown members; deterministic ordering;
bounded Chinese failures; commit-before-progress; no raw diagnostics,
credentials, absolute paths, or unrequested source
**Scale/Scope**: the finite version-controlled demonstration fixture, N
prepared sequential Pass 1 jobs, and exactly one Pass 2 job for one prepared
flow in one Repository Scan

All bounds are enforced before allocation/transmission where possible and
again after parsing. The canonical content, evidence, section, and collection
bounds already enforced by `KnowledgeCommit` remain the final persistence
ceiling; the adapter may be stricter but never looser.

## Constitution Check

*GATE: evaluated before research and re-evaluated after design.*

| Gate | Pre-research judgment | Post-design evidence |
| --- | --- | --- |
| I. Layered Project Boundaries | PASS | Provider execution, prompt loading, bounded hydration orchestration, and schema parsing stay in `Vulgata.Agents`; provider-independent contracts/ports stay in Core; the snapshot and knowledge-store implementations stay in Infrastructure; Web owns composition. No reverse dependency or EF type crosses Core. |
| II. Domain Purity & Ubiquitous Language | PASS | The plan uses Worker Agent, Pass 1, Pass 2, Profiled Revision, Code Unit, Logical Document, Document Version, Source Anchor, Edge, Version Chain, and Cross-Repository Resolution as defined. Documents remain immutable and persistence still uses the specific `IKnowledgeStoreRepository`. |
| III. Validation & API Contract Discipline | PASS | No public API/UI is added. Closed constructors validate internal packets, strict schema plus post-parse validation guards provider output, and bounded Chinese failures reuse the runner contract. Existing public FluentValidation rules are unchanged. |
| IV. Test-First Integration Harness | PASS | Every behavior group below begins with an observed failing test and ends green. Integration coverage includes authorization, commit ordering, real relational SQLite reservation, provider protocol, production DI, and full regression. |
| V. Security & Data Stewardship | PASS | The adapter resolves the existing authorized System/global Worker Agent assignment, decrypts only at the execution boundary, sends only an exact verified slice/context, keeps self-hosted execution on the configured organization-controlled endpoint, and emits no credential/raw diagnostic/absolute path. Prompts are embedded resources. |
| Fixed technology and quality gates | PASS | The centrally pinned packages and existing projects are reused; Web build and full tests are required. No migration or container change is planned. |
| ADR/peer-authority check | PASS | `docs/adr/` and context ADRs are absent at the pinned boundary. The design aligns with the canonical scanning plan/data contracts and the approved August 15 scope change. |

No Constitution violation requires justification; Complexity Tracking is
therefore empty. Any implementation discovery requiring a new table, migration,
provider abstraction, public tool, general flow planning, or weaker
authorization/source boundary must return to plan review rather than be absorbed
as implementation detail.

## Architecture

### 1. Closed job and result unions

Keep `WorkerAgentJobPacket` as the common immutable header and replace any
open/nullable payload shape with an exhaustive discriminated union:

- `Pass1CodeLogicJobPayload`: exact Code Unit locator, Repository-relative
  path, line range, source SHA-256, File Identity Hash, bounded neighboring
  structural context, approved communication evidence, and an allowlist of
  same-Repository target Document Versions that may receive `REFERENCES` Edges.
- `Pass2BusinessLogicJobPayload`: prepared-flow identity/schema version,
  ordered prepared trace evidence, and exact authorized current Code Logic
  Document Versions used for synthesis. It contains no source-discovery or
  graph-traversal instruction.

`WorkerAgentJobKind` has exactly `Pass1CodeLogicDocument` and
`Pass2BusinessLogicDocument` for this path. Construction rejects a kind/payload
mismatch, zero/multiple variants, unknown enum values, duplicate identifiers,
non-canonical ordering, absolute paths, and any bound violation.

`WorkerAgentResultPayload` is similarly exhaustive:

- `Pass1CodeLogicDocumentResultPayload` contains bounded canonical content,
  Source Anchors, permitted same-Repository reference requests, and prepared
  communication evidence.
- `Pass2BusinessLogicDocumentResultPayload` contains bounded non-technical
  canonical content, Source Anchors, exact prepared flow evidence references,
  and the exact Code Logic Document Version IDs used. It intentionally has no
  field capable of requesting a base Code Logic `REFERENCES` Edge.

The existing envelope remains the only outcome union: `Valid` carries exactly
one matching payload; every other status carries no payload and only a closed
failure code plus bounded Chinese message. Raw provider error bodies, exception
messages, prompts, response fragments, and diagnostics cannot enter the
envelope.

See [worker-agent-unions.md](contracts/worker-agent-unions.md).

### 2. Revision-bound source hydration

The production dispatcher does not accept a working-copy path or raw source in
the packet. A `RevisionBoundWorkerContextHydrator` in `Vulgata.Agents` consumes
the existing trusted committed Repository snapshot lease port. It:

1. verifies caller scope, Repository, ScanRun, System, Code Unit, and Profiled
   Revision before acquiring the lease;
2. requires the lease revision to equal the packet revision before any read;
3. normalizes and validates the Repository-relative path without resolving an
   absolute host path from provider-controlled data;
4. reads only the requested file and exact inclusive line range from the
   committed snapshot, never the mutable working copy;
5. computes the file identity hash and exact slice SHA-256, compares both in
   constant time, verifies the lease revision again after reading, and rejects
   drift before provider invocation;
6. assembles only allowlisted structural/context records, applies source and
   request byte limits, and returns an in-memory hydrated context that is never
   persisted or logged.

Pass 2 performs no arbitrary source hydration. Its context builder loads the
exact authorized Code Logic retrieval projections/sections and prepared trace
evidence named by the resolved manifest, checks Repository/ScanRun/currentness
and caller scope, and orders them exactly as the manifest requires.

See [source-and-provider-boundary.md](contracts/source-and-provider-boundary.md).

### 3. Pinned OpenAI-compatible Worker Agent adapter

`Vulgata.Agents` supplies the production `IWorkerAgentDispatcher` using the
centrally pinned Microsoft Agent Framework OpenAI adapter. It resolves the
existing System override for the Worker Agent task role, otherwise the existing
global Worker Agent assignment. It does not introduce a second provider model,
dynamic fallback, retry, failover, telemetry exporter, or credential-bearing
packet.

Web composition passes a scoped execution credential accessor/factory that
decrypts the configured API key immediately before client construction. The
key remains in memory at that boundary and is excluded from logs, exceptions,
packets, prompts, results, and durable evidence. The configured endpoint is the
only endpoint used. A self-hosted assignment therefore remains inside its
organization-controlled boundary; there is no external fallback.

Two versioned embedded resources are loaded by fixed allowlist name:
`Prompts/pass1-code-logic-worker.v1.md` and
`Prompts/pass2-business-flow-worker.v1.md`. Missing, ambiguous, empty, or
oversized resources fail before a provider call. Runtime callers cannot replace
prompt text.

The provider is asked for the schema matching the job variant. Before ordinary
deserialization, a bounded streaming preflight rejects oversized JSON,
duplicate properties, excessive depth, invalid UTF-8, and trailing content.
Strict `System.Text.Json` metadata rejects unknown properties and unknown enum
values. Post-parse validation then proves header/payload identity equality,
canonical ordering, collection uniqueness, text normalization and byte bounds,
allowed Source Anchors/Edge targets, absence of secrets/absolute paths/raw
source carriers, and the variant-specific graph rules. Validation failures map
to closed outcomes without echoing provider content.

### 4. Narrow commit-before-progress port

Add a provider-independent Core port with the single responsibility:

```text
IWorkerAgentResultCommitter.CommitAsync(
  WorkerAgentJobPacket packet,
  WorkerAgentResultEnvelope validResult,
  RepositoryAccessScope scope,
  CancellationToken cancellationToken)
  -> WorkerAgentResultCommitOutcome
```

Its Infrastructure implementation rechecks variant/identity/scope, maps the
validated payload to exactly one existing `KnowledgeCommit`, and delegates to
`IKnowledgeStoreRepository`. Stable operation and aggregate identifiers are
derived from the immutable job/prepared-flow identity so an identical replay
is identical at the Story 11.1 boundary. The committer adds no persistence
logic outside `KnowledgeCommit` and never returns a result that permits progress
for `Conflict`, `Rejected`, cancellation, or error.

Pass 1 maps only allowlisted same-Repository reference targets and exact Source
Anchors. Pass 2 maps `GENERATED_FROM` targets by set equality with the exact Code
Logic Document Versions supplied to synthesis. Because its result contract has
no base-reference request, narrative text cannot materialize a Code Logic
`REFERENCES` Edge. Existing `KnowledgeCommit` validation remains the final
atomicity, Version Chain, flow evidence, section, retrieval-projection, and
authorization guard.

The Sequential Scan Runner awaits a `Committed` outcome before calling the
existing durable valid-result/progress operation. A non-committed outcome uses
the existing bounded failure transition with the in-flight job preserved or
cleared only according to the existing failure contract; it never increments
completed progress. No distributed transaction is claimed: ordering plus
idempotent knowledge commit and the existing single-runner constraint make a
restart safe without adding replay policy.

See [result-commit-and-lifecycle.md](contracts/result-commit-and-lifecycle.md).

### 5. One prepared Pass 2 lifecycle

Add one version-controlled, embedded JSON manifest, for example
`PreparedFlows/demo-business-flow.v1.json`, validated by a closed schema. It
names one prepared flow, one expected Repository/System fixture identity,
ordered Pass 1 job/Code Unit identities, and finite trace steps/evidence. It
contains no credentials, raw source, arbitrary query, or general traversal
rules.

When and only when all prepared Pass 1 results have committed, the runner:

1. durably reaches the existing nonterminal `Pass1Completed` state;
2. selects exactly one configured manifest from a fixed allowlist;
3. under the caller-supplied Repository access scope, resolves every manifest
   member to the exact current Code Logic Document Version committed by this
   ScanRun and Profiled Revision;
4. fails closed on zero/multiple manifests, missing/extra/stale/unauthorized
   members, duplicate/out-of-order trace data, or any version mismatch;
5. creates one `Pass2BusinessLogicDocument` packet, dispatches it through the
   same adapter, and commits it through the same result committer;
6. transitions to `Completed` only after that commit returns `Committed`.

Pass 2 dispatch, validation, authorization, cancellation, conflict, rejection,
or persistence error leaves the Scan non-completed with the existing bounded
Chinese failure semantics. This path does not discover entry points, traverse
the graph, cluster flow groups, dispatch multiple Pass 2 jobs, or infer new
base graph truth.

See [prepared-pass2-flow.md](contracts/prepared-pass2-flow.md).

### 6. F003 production-reachability gate

Before registering the production runner, correct
`ScanExecutionRepository` so SQLite follows the accepted Story 11.1 relational
write-reservation pattern: begin the supported serializable transaction,
perform a parameterized no-op update over the deterministically ordered owner
row(s) to obtain SQLite's database-wide write reservation, and only then read,
revalidate, and mutate durable execution state. SQLite never emits PostgreSQL
`FOR UPDATE`. PostgreSQL keeps its existing row-lock path.

A real shared relational SQLite two-context regression must first reproduce the
stale-read/race condition and then prove one valid transition with the loser
observing/rejecting against post-reservation state. Production DI tests are not
enabled until this regression is green. This is a correction to existing
behavior, not a schema change or new scheduling policy.

## Project Structure and Ownership

```text
specs/006-demo-document-generation/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── contracts/
    ├── worker-agent-unions.md
    ├── source-and-provider-boundary.md
    ├── result-commit-and-lifecycle.md
    └── prepared-pass2-flow.md

src/dotnet/
├── Vulgata.Core/DomainServices/
│   ├── WorkerAgentJobKind.cs                 # extend closed kind allowlist
│   ├── WorkerAgentJobPacket.cs               # common header + typed job union
│   ├── WorkerAgentResultPayload.cs           # typed result union
│   ├── WorkerAgentResultEnvelope.cs          # closed outcome invariants
│   ├── IWorkerAgentResultCommitter.cs         # new narrow commit port
│   ├── PreparedPass2Flow.cs                   # provider-independent manifest model
│   └── SequentialScanRunner.cs                # commit ordering + one Pass 2 lifecycle
├── Vulgata.Agents/
│   ├── WorkerExecution/                       # dispatcher, hydration, schema/safety
│   ├── Prompts/                               # two embedded versioned prompts
│   └── PreparedFlows/                         # one embedded versioned manifest
├── Vulgata.Infrastructure/
│   └── Data/
│       ├── WorkerAgentResultCommitter.cs      # KnowledgeCommit mapping only
│       └── ScanExecutionRepository.cs         # F003 SQLite reservation correction
└── Vulgata.Web/Program.cs                     # accepted production composition only

tests/Vulgata.Tests/
├── WorkerAgentContractTests.cs
├── WorkerAgentHydrationTests.cs
├── WorkerAgentDispatcherTests.cs
├── WorkerAgentResultCommitterTests.cs
├── SequentialScanRunnerDocumentGenerationTests.cs
├── PreparedPass2FlowTests.cs
└── WorkerAgentProductionRegistrationTests.cs
```

**Ownership decision**: Core owns provider-independent closed contracts,
authorization-carrying ports, and deterministic lifecycle. Agents owns the LLM
protocol, prompt/manifest resources, hydration orchestration, and unsafe-output
defense. Infrastructure owns committed-snapshot/database implementations and
the sole mapping into the existing knowledge store. Web owns composition only.
Tests mirror those seams. Story 11.1/11.2 migrations, accepted workflow
evidence, specification/checklist, BMad lifecycle state, and tasks remain
untouched.

## Test-First Implementation Groups (planning input, not tasks)

| Group | First RED observation | GREEN design predicate |
| --- | --- | --- |
| G0 F003 | Two SQLite contexts can both act from pre-reservation state | Reservation occurs before read/revalidation; one transition wins and the other deterministically observes/rejects; PostgreSQL behavior regresses green |
| G1 closed unions | Wrong kind/payload, duplicate/unknown/out-of-order/oversized members are accepted by the old contract | Constructors and serializer metadata accept exactly the two variants and all finite bounds |
| G2 exact hydration | Mutable checkout, wrong revision/path/range/hash/File Identity Hash, secret, absolute path, or oversized source can reach dispatch | Trusted committed lease is verified before/after exact read and every invalid fixture yields zero provider calls |
| G3 provider adapter | No production dispatcher/prompt/schema safety exists | Loopback OpenAI-compatible fixture proves pinned adapter selection, embedded prompts, strict JSON, closed diagnostics, cancellation, and no fallback/credential leakage |
| G4 Pass 1 commit | Valid results can advance without durable knowledge or invalid results can write | One scoped atomic `KnowledgeCommit` succeeds before progress; replay is idempotent; conflicts/rejections/cancellation/authorization mismatch produce zero valid progress |
| G5 prepared Pass 2 | Pass 1 terminal behavior cannot create exactly one prepared synthesis | `Pass1Completed` resolves one manifest, dispatches one exact-version packet, commits exact flow evidence/`GENERATED_FROM`, and reaches `Completed`; all failures remain non-completed |
| G6 composition/security | Production DI lacks the dispatcher/committer or can expose provider material | Web resolves the accepted graph after G0, uses existing encrypted assignment rules, sends minimal context, and logs no secret/raw diagnostics/absolute path |
| G7 integrated regression | Cross-module prepared flow has no complete observable proof | Current-boundary integration proves Pass 1 through Pass 2 predicates; focused suite, Web build, and full test suite pass |

## Requirement → Design → Test Trace

| Requirement | Design owner | Required proof |
| --- | --- | --- |
| FR-001, FR-002 | closed unions; strict adapter | G1/G3 exact variant and identity tests |
| FR-003 | revision-bound hydrator | G2 exact committed-snapshot fixtures and zero-call failures |
| FR-004 | streaming preflight, strict schema, post-parse safety, closed envelope | G1/G3 malformed/unsafe/bounds/diagnostic matrix |
| FR-005 | Pass 1 committer mapping | G4 aggregate shape and atomic visibility |
| FR-006 | runner awaits commit outcome | G4 ordered barrier/interceptor test |
| FR-007 | adapter/committer/runner failure mapping | G2-G5 zero-write and zero-progress tests |
| FR-008 | stable materialization + existing KnowledgeCommit | G4 identical replay and Version Chain regression |
| FR-009 | durable lifecycle extension | G5 `Pass1Completed` nonterminal assertions |
| FR-010 | one embedded manifest and fixed resolver | G5 zero/one/multiple manifest and dispatch-count tests |
| FR-011 | authorized exact-version Pass 2 packet | G5 missing/stale/extra/cross-scope version matrix |
| FR-012 | Pass 2 strict schema and prompt | G3/G5 bounded non-technical section tests |
| FR-013 | Pass 2 committer mapping | G5 atomic flow/section/projection/Edge assertions |
| FR-014 | Pass 2 schema omits base-reference request | G1/G5 unknown-field rejection and zero narrative-created `REFERENCES` assertion |
| FR-015 | commit-gated terminal transition | G5 success/failure/cancellation barrier tests |
| FR-016 | existing assignment resolver + one configured endpoint | G3/G6 System override/global fallback-within-assignment tests; no runtime failover |
| FR-017 | configured self-hosted boundary | G3/G6 endpoint-capture test proves zero external calls |
| FR-018 | scope at hydrate/read/commit | G2/G4/G5 cross-Repository and cross-System rejection |
| FR-019 | embedded fixed-name resources | G3 resource discovery/hash/absence-of-hardcoded-text tests |
| FR-020 | reuse of runner/store/configuration | architecture/DI tests plus diff guard: no API/UI/migration/retry/scheduler |
| SC-001 | G4/G5 integration | all recorded-valid Pass 1 jobs have one prior complete commit |
| SC-002 | all fail-closed seams | invalid matrix yields zero visible aggregate rows and zero valid jobs |
| SC-003 | prepared lifecycle | exactly one Pass 2 dispatch/Business Logic Document; terminal after commit |
| SC-004 | set-equal provenance | exact `GENERATED_FROM` targets; no narrative base `REFERENCES` |
| SC-005 | Pass 2 schema/prompt | fixture asserts purpose, ordered main steps, outcome, and evidence references |
| SC-006 | security capture | request/log/exception/database scans contain no credential, extra slice, absolute path, or raw diagnostic |

## E2E Predicates and Inputs

The story-level E2E is an economical current-boundary integration, not the
Story 11.5 final slice rehearsal. Inputs are: one temporary Git Repository with
a known committed revision and a later dirty/moved HEAD; an approved handoff;
prepared Code Units with exact line/source/file hashes; one caller-supplied
Repository scope; encrypted test provider configuration; a loopback
OpenAI-compatible server returning deterministic Pass 1/Pass 2 JSON; one
embedded prepared-flow manifest; and relational test storage.

The integrated assertions are:

1. only committed-revision requested slices reach the loopback server;
2. every valid Pass 1 result has one complete immutable aggregate and retrieval
   projection before its durable progress increment;
3. the Scan reaches `Pass1Completed`, emits exactly one Pass 2 request containing
   the exact current Code Logic Document Version set, then stays nonterminal
   until the Business Logic commit is observable;
4. the Business Logic aggregate contains exact flow evidence and set-equal
   `GENERATED_FROM` Edges, and no new base Code Logic `REFERENCES` Edge;
5. malformed/unsafe/stale/unauthorized/conflicting/cancelled variants produce
   bounded Chinese failure, zero affected knowledge rows, and no valid progress;
6. captured HTTP, logs, exceptions, and durable rows contain no credential,
   raw diagnostic, unrelated source, or absolute host path.

The final clean-database two-Repository end-to-end rehearsal and final Docker
image build remain Story 11.5 because this story changes neither migration nor
container/runtime packaging. PostgreSQL concurrency tests remain required for
the reused `KnowledgeCommit` boundary; the new F003 correction additionally
requires real relational SQLite coverage.

## No-Migration Rationale

Both required durable aggregates already exist: Story 11.1 owns Document
Versions, sections, Source Anchors, Edges, flow evidence, retrieval projections,
and Version Chain semantics; Story 11.2 owns Scan execution phase/progress and
includes `Pass1Completed`. This story changes typed in-memory contracts,
adapter behavior, a prepared embedded resource, commit mapping, and transition
ordering only. F003 changes transaction ordering, not table shape. Therefore no
entity configuration, migration, or model snapshot change is authorized. Any
missing durable field discovered during implementation is a plan/authorization
conflict and must stop the story.

## Planned-Change Authorization (mandatory pause after accepted plan review)

Two repository triggers apply:

1. **Worker Agent tool/execution change** — the plan supplies the production
   Worker Agent execution adapter, strict Pass 1/Pass 2 schemas, exact source
   hydration, and embedded prompts. Effect: the configured provider receives
   one minimal verified context and may return only one of two closed payloads;
   credentials and arbitrary source/diagnostics remain excluded. Implementation
   intent: add this adapter behind existing `IWorkerAgentDispatcher`, not a new
   public or Chat Agent tool. Plan citations: Architecture sections 1-3 and
   contracts `worker-agent-unions.md` / `source-and-provider-boundary.md`.
   PRD citations: §5.3 FR-3.1/FR-3.3; §5.4 FR-4.3, FR-4.4, FR-4.11;
   §6.2 NFR-2.2/NFR-2.3; §6.6 NFR-6.1. Spec citations: FR-001-FR-004,
   FR-016-FR-019.
2. **Scan strategy change** — the plan extends deterministic sequencing from
   Pass 1 through the existing `Pass1Completed` boundary to exactly one
   manifest-prepared Pass 2 dispatch and commit-gated `Completed`. Effect: one
   new closed job kind and terminal transition, with no general tracing,
   clustering, concurrency, retry, or failover. Implementation intent: keep
   lifecycle ownership in `SequentialScanRunner` and reuse the existing durable
   execution store. Plan citations: Architecture sections 4-6 and contract
   `prepared-pass2-flow.md`. PRD citations: §5.4 FR-4.2 and FR-4.5;
   §5.16 lines 299-304 and explicit deferrals at lines 316-318. Spec citations:
   FR-006, FR-009-FR-015, FR-020.

The accepted deadline change, PRD §5.16, explicitly defers the ordinary full-V1
retry/continue behavior in NFR-3.1 and general graph-first planning while
preserving them in backlog. This plan does not delete or claim those
requirements complete and introduces no additional PRD weakening.

After independent plan-review acceptance and before `speckit-tasks`, the root
orchestrator must ask the user to authorize these two changes, identifying the
effects and implementation intent above. Approval does not waive TDD, combined
quality review, security, integration, or verification gates.

## Risks and Fail-Closed Responses

| Risk | Planned response |
| --- | --- |
| SDK structured-output behavior differs at runtime | Loopback qualification pins observed request/response behavior; strict local preflight/post-validation remains authoritative; mismatch blocks production DI |
| Snapshot changes while hydrating | Lease revision checked before and after exact archive read; mismatch yields zero provider calls |
| Provider returns plausible but unauthorized facts | Result targets must be members of server-built allowlists and exact packet identities; provider cannot select Repository/System |
| Secret or raw source appears in output | Bounded sensitive-data/raw-source-carrier guard rejects before commit and never echoes content |
| Commit succeeds but progress write fails | Re-entry uses the same stable `KnowledgeCommit`; idempotent replay precedes progress retry without duplicate truth |
| Pass 2 manifest drifts from Pass 1 fixture | Closed schema/version and exact set/current-version resolution fail at `Pass1Completed`; no Pass 2 dispatch or `Completed` |
| F003 remains reachable | Production registration test is gated on the SQLite reservation regression; no accepted DI graph before correction |
| Hidden schema need appears | No migration is authorized; stop and return to plan review/planned-change authorization |

## Complexity Tracking

No Constitution violations.
