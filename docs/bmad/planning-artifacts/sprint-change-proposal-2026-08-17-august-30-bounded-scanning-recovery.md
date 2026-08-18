# Sprint Change Proposal: August 30 Bounded Scanning Recovery

**Date:** 2026-08-17  
**Trigger:** The August 15 implementation path did not produce a reliable
general scan: CodeGraphContext indexing is operationally too slow and unstable,
the release qualification repeatedly failed, the prepared Pass1/Pass2 path is
too narrow, and Chat quality is insufficient for the competition evidence.  
**New deadline:** 2026-08-30  
**Mode:** Batch  
**Scope classification:** Major MVP course correction  
**Artifact status:** Approved  
**Approval:** Approved by the user on 2026-08-17, with an explicit pause before
Sprint Loop planning.  
**Design authority:**
`docs/bmad/planning-artifacts/architecture/august-30-bounded-scanning-design.md`

## 1. Issue Summary

The previous August 15 slice proved useful persistence, typed Worker execution,
exact prepared resolution, and citation seams, but it did not establish the
intended scanning product. CodeGraphContext became a release-critical bottleneck:
sample indexing can require hours, failures require expensive rescans, and the
qualification path consumed substantial schedule without yielding a reliable
general input contract.

Continuing the old backlog unchanged would spend the remaining deadline on CGC
recovery and broad orchestration rather than producing a credible real demo.
Replacing CGC with another monolithic profiler contract would preserve the same
coupling. Restricting the product to the six prepared Code Units would produce
screenshots but not a defensible automatic scanning claim.

The correction replaces CGC with a lightweight CodeGraph + Java LSP + safe XML
PreScan, schedules bounded File Code Units, generalizes the existing immutable
knowledge path, and turns Chat into deterministic non-vector retrieval followed
by grounded LLM synthesis.

## 2. Change Navigation Checklist

### Trigger and evidence

- [x] Deadline moved from August 15 to August 30, leaving a bounded recovery
  window rather than time for the full V1 backlog.
- [x] CGC qualification and multi-hour indexing are unacceptable release risks.
- [x] The existing prepared path generated real Documents but covered only six
  manually selected Code Units and one prepared flow.
- [x] Existing database-backed `DocumentSection`, `IndexEntry`, Source Anchor,
  Edge, and Uncertainty seams are reusable.
- [x] Local CodeGraph indexing is fast enough on the sample scale to retain as
  a revision-bound cache, subject to the new release gate.

### Epic impact

- [x] Epic 4 remains historical evidence but its CGC production path is
  superseded for the August 30 release.
- [x] Epic 11 remains the historical August 15 slice; completed story claims
  are not silently rewritten.
- [x] A new Epic 12 shall own the bounded production scan recovery.
- [x] Epics 5–10 remain the broad post-deadline backlog and are not falsely
  marked complete by Epic 12.
- [x] The Sprint tracker must stop presenting the old T072/CGC gate as the
  active release path after this proposal is approved.

### Artifact conflicts

- [x] PRD changes are required for the August 30 acceptance boundary.
- [x] Canonical architecture and scanning technical/data-contract supplements
  require an approved amendment or explicit supersession section.
- [x] Epics require new Epic 12 stories and mapping notes.
- [x] Ubiquitous language requires a canonical name for the lightweight
  System-level scan coordination record.
- [x] UX requires real pause/resume/progress/warning states and grounded Chat
  evidence; fake screen-recording state must remain isolated.
- [x] Deployment removes CGC/Python qualification from the critical path and
  adds pinned CodeGraph, Java 21, and Eclipse JDT LS requirements.
- [x] Tests replace T072 with tiered real-tool and real-LLM evidence.

### Path evaluation

- [N/A] Directly fixing CGC is rejected: it has already consumed excessive
  schedule and remains a systemic performance/reliability risk.
- [N/A] Reverting to screenshots over prepared fake state is rejected: it does
  not provide a defensible automatic scanning claim.
- [x] MVP re-scope is selected: bounded real Java/XML scan, exact selected
  Cross-Repository behavior, and grounded Chat.
- [x] Existing immutable knowledge/persistence, authorization, exact matching,
  Worker adapter, and UI seams are retained and generalized.

## 3. Recommended Approach

Deliver this path:

```text
two real Java/XML Repositories
-> exact Git snapshots
-> CodeGraph index + Eclipse JDT LS + safe XML catalog PreScan
-> deterministic 50-150 File Code Unit manifest
-> bounded resumable Pass 1
-> exact endpoint Cross-Repository Resolution
-> endpoint-centered Pass 2
-> database-backed non-vector LLM-Wiki retrieval
-> grounded Business/IT Chat with citations
```

The complete technical contract is in the associated design document. Key
course corrections are:

1. remove CodeGraphContext from production/release composition;
2. make CodeGraph and JDT LS revision-bound adapters, not domain authorities;
3. schedule one File Code Unit per bounded file instead of method-level units;
4. use deterministic business-seeded ordering and bounded Agent navigation;
5. persist authoritative structured JSON with derived Markdown/search sections;
6. capture immutable Global/System/Repository context per Scan Run;
7. add bounded concurrency, failure classification, durable pause/resume, and
   per-job persistence;
8. coordinate exactly two Repository scans under a lightweight persisted
   System-level record;
9. use exact catalog-driven endpoint matching and endpoint-centered Pass2;
10. implement section retrieval, one-hop persisted relationship expansion,
    bounded evidence packets, and short validated multi-turn context without a
    vector database.

## 4. PRD Impact

### Temporarily replaced for the August 30 release

| PRD clause | Existing direction | August 30 boundary |
| --- | --- | --- |
| FR-4.1 | CodeGraph/tree-sitter method-like Code Units and broad language/framework profiling | Complete Java/XML PreScan with one bounded File Code Unit per file; oversized-only safe splitting |
| FR-4.2 | Repository orchestrator, supersteps, broad queue | deterministic bounded queue, concurrency 1-4, exactly two release Repositories |
| FR-4.3 | Worker processes assigned Code Unit and detects communication | Worker processes a whole bounded file, recognizes internal units as sections, and emits typed observations |
| FR-4.4 | pre-allocation, immediate local edges, cross-repo uncertainty | current-doc-first navigation plus post-Pass1 deterministic reconciliation; no general pre-allocation |
| FR-4.5 | general graph-first path tracing and clustering | endpoint-centered bounded flow groups using persisted exact evidence |
| FR-4.9 | CodeGraph structural data and code-unit boundaries | CodeGraph index/cache plus JDT LS navigation; domain does not copy the full structural graph |
| FR-4.12 | same-superstep Worker cross-verification | deferred; source/citation validation and deterministic reconciliation remain |
| FR-5.8 | catalog dynamically injected into Worker prompt | catalog first drives safe deterministic XML evidence; bounded confirmed evidence reaches Workers |
| FR-6.1/6.2 | general unresolved and bidirectional post-scan resolution | exact Custom RPC and safe exact HTTP only; ambiguity remains Uncertainty |
| FR-11.6 | full typed LLM-Wiki tool suite | equivalent bounded server-side section retrieval, one-hop persisted expansion, packet construction, and synthesis; no open Agent tool loop |
| NFR-1.1 | approximately 10,000 source files | deferred; release proves 50-150 automatically selected File Code Units across two Repositories |

### Preserved or strengthened

- FR-4.6/4.7/4.10: virtual hierarchy, database-backed projections, and immutable
  Version Chains remain.
- FR-11.7: every material accepted claim remains cited to Documents and Source
  Anchors.
- NFR-1.2: Chat remains under 30 seconds for evidence spanning at most five
  Documents.
- NFR-3.1: an isolated transient job is retried once, then job-specific
  failure continuation uses an explicit bounded budget.
- NFR-3.2: provider unavailability gains classified backoff and durable pause.
- NFR-3.3: restart recovery gains persisted per-job state and idempotent resume.
- NFR-4.1: the single application Docker image plus PostgreSQL deployment
  remains.
- NFR-6.1/6.2: prompts remain externalized and catalog format documented.

The PRD should retain the full V1 requirements as post-August backlog while
adding a dated August 30 acceptance overlay; it should not delete the intended
long-term product.

## 5. Architecture and Data Impact

### Reused

- `LogicalDocument`, immutable `DocumentVersion`, `SourceAnchor`;
- `DocumentSection`, `IndexEntry`, `DocumentEdge`, `Uncertainty`, `FlowGroup`;
- Repository authorization, exact Git snapshot, LLM provider, and Worker
  execution seams;
- existing exact RPC/HTTP resolver logic where it conforms to the new
  catalog-driven contract;
- current Business/IT Chat page and evidence display foundations.

### Added or materially changed

- CodeGraph CLI adapter and Eclipse JDT LS client/runtime;
- safe XML parser and closed Communication Pattern Catalog selector grammar;
- File Code Unit manifest, ordering, and oversized-file policy;
- `navigate_code` Worker tool and deferred-observation result schema;
- `code-logic/file-v2` authoritative JSON and deterministic projections;
- immutable `ScanContextSnapshot`;
- per-job durable state, pause phases, retry schedule, lease/idempotent resume;
- lightweight System-level two-Repository coordination record;
- endpoint-centered Pass2 flow planner and bounded `retrieve_code_logic`;
- deterministic Chat retrieval/expansion/packet/synthesis coordinator.

### Persistence and migration

At least one additive EF migration is expected for Scan Context Snapshot,
per-job execution state, new Scan phases/status, and System-level coordination.
The accepted plan must decide whether these ship in one cohesive migration or
multiple dependency-ordered migrations. Clean apply/rollback/reapply and no
pending model changes are mandatory. Existing immutable Document rows and
legacy prepared-demo rows must remain readable unless a later approved plan
provides a bounded migration.

## 6. Epic and Story Changes

Create **Epic 12: August 30 Bounded Production Scan Recovery** with five bounded
stories:

### Story 12.1: Durable Scan Foundation and Tool Runtime

- additive persistence for context, jobs, pause/resume, and two-Repository
  coordination;
- pinned CodeGraph/JDT LS runtime and typed adapters;
- clean migration lifecycle and runtime health proof.

### Story 12.2: Lightweight Java/XML PreScan and File Manifest

- exact revision/file enumeration;
- CodeGraph index, Java LSP readiness, safe XML catalog;
- File Code Units, hashes, classification, seed ordering, atomic manifest;
- neutral and sample Repository fixtures.

### Story 12.3: Resumable File Pass1 and Code Logic Documents

- bounded concurrent dispatch and classified retry/pause/resume;
- `navigate_code`, knowledge-first navigation, deferred observations;
- `code-logic/file-v2`, Source Anchor validation, deterministic projections;
- post-Pass1 exact local reconciliation.

### Story 12.4: Exact Cross-Repository Flow and Pass2

- catalog-driven Custom RPC/safe HTTP matching;
- exact Edge/Uncertainty decisions;
- endpoint-centered bounded grouping;
- Pass2 Document/source retrieval and optional direct LSP/CodeGraph verification;
- complete `GENERATED_FROM` provenance.

### Story 12.5: Grounded LLM-Wiki Chat and Release Evidence

- deterministic section retrieval and one-hop persisted expansion;
- bounded evidence packet, grounded synthesis, citations, short multi-turn state;
- real production progress/pause/resume UI;
- Tier 1/2/3 release verification and evidence capture.

Stories execute in order. Story 12.1 and the non-persistence research portion of
12.2 may be planned in parallel, but migrations, shared domain contracts,
`VulgataDbContext`, and the model snapshot remain under one integration owner.
Story 12.5 may begin UI/evaluation-fixture preparation after 12.1 contracts are
accepted, but final retrieval implementation waits for 12.3/12.4 projections.

## 7. UX Impact

The Chinese UI shall distinguish real production states:

- preparing/indexing;
- scanning with completed/total counts;
- retrying provider connection with next attempt;
- paused with bounded reason and `继续扫描` action;
- completed;
- completed with warnings and accessible failed-file summary;
- failed integrity/security boundary.

Chat retains Business/IT mode, scoped Repository selection, answer Markdown,
Document citations, and Source Anchor line ranges. Fake Backup B scan state may
remain only as explicitly isolated development/demo code and must not back the
August 30 release evidence.

## 8. Deployment Impact

- Remove CGC/Python/pip/`requirements.lock` from the release-critical image
  path unless another unrelated shipped feature still proves a need.
- Install or copy a version/checksum-pinned CodeGraph CLI.
- Add Java 21 and a version/checksum-pinned stable Eclipse JDT LS archive.
- Use isolated bounded writable workspaces for Git, CodeGraph, and JDT LS.
- Preserve non-root runtime, encrypted provider credentials, and the existing
  PostgreSQL deployment.
- Docker failure reports must include the exact build command, stage, and
  blocked URLs so the user can diagnose proxy policy.

## 9. Verification Strategy

### Tier 1 — checkpoint

No external LLM/network: focused unit/integration tests for closed contracts,
projections, scheduling, retry/pause/resume, authorization, currentness,
retrieval, evidence validation, and deterministic fixtures. No failing test may
be committed.

### Tier 2 — local integration

Clean disposable PostgreSQL, migration lifecycle, real Git/CodeGraph/JDT LS,
neutral Java/XML fixtures, controlled LLM double, forced provider outage,
persisted pause, process restart, resume, and duplicate-prevention proof.

### Tier 3 — final release candidate

Production Docker image, clean database, two real sample Repositories, real
CodeGraph/JDT LS/LLM, 50-150 automatically selected File Code Units, all seeds
successful, bounded ordinary failures, exact Cross-Repository Edge, honest
Uncertainty, at least three Business Logic Documents, and 12-20 Chat evaluation
questions with at least 80% useful answers, 100% accepted technical claims
cited, zero authorization leakage, and answers under 30 seconds.

The final run records revisions, counts, durations, schema/prompt/tool versions,
and evidence fingerprints. Docker restart must preserve wiki and Chat access.

## 10. Delivery and Cut Strategy

Proposed order:

| Sequence | Outcome |
| --- | --- |
| 1 | approve correction, update PRD/architecture/epics/tracker, create Epic 12 specs |
| 2 | Story 12.1 durable foundation and runtime adapters |
| 3 | Story 12.2 complete lightweight PreScan |
| 4 | Story 12.3 real bounded File Pass1 |
| 5 | Story 12.4 Cross-Repository Pass2 path |
| 6 | Story 12.5 Chat quality, UI, release evidence, screenshots |

Cut first: direct Pass2 JDT LS, direct Pass2 CodeGraph, background auto-resume,
more than two Repositories, extra protocols, and advanced visualization.

Never cut: revision/hash identity, Source Anchors, citations, real CodeGraph and
JDT-LS Java PreScan, real Pass1/Pass2, deterministic exact resolution,
authorization, durable manual resume, migration lifecycle, and one real
two-Repository Docker release run.

## 11. Risks and Mitigations

| Risk | Mitigation / stop condition |
| --- | --- |
| CodeGraph CLI output changes | typed adapter, version pin, neutral contract fixture; no internal SQLite coupling |
| JDT LS import hangs | 60-second readiness bound; fail before LLM cost; isolated workspace |
| XML rules overfit samples | closed catalog grammar plus neutral fixture; no sample constants in code |
| Whole-file input exceeds model/provider bounds | explicit file caps and safe top-level split; never truncate silently |
| Bounded concurrency triggers rate limits | shared 1-4 limit, provider circuit, `Retry-After`, durable pause |
| Restart duplicates LLM work/Documents | deterministic Job IDs, leases, atomic result/job commit, idempotent resume |
| Partial completion hides important gaps | all seeds mandatory, bounded failure budget, visible warning inventory |
| Cross-Repository matching invents targets | exact normalized key, unique current provider only, otherwise Uncertainty |
| Pass2 expands beyond evidence | persisted Edge/Flow membership only; all additional Documents revalidated and added to `GENERATED_FROM` |
| Lexical Chat retrieval misses paraphrases | mode terms, symbols/endpoints, CJK bigrams, Business Logic fallback, curated 12-20 question evaluation; vector RAG deferred |
| LLM produces unsupported claims | opaque evidence handles, packet fingerprint, per-claim citation validation, reject whole invalid answer |
| Deadline compression recreates old monster | enforce cut order and closed contracts; do not rebuild CGC protocol |

## 12. Workflow and Authorization

The expired August 15 `expedited-mvp` exception does not authorize Epic 12.
Epic 12 should use the repository's Standard sprint-loop level unless a new
dated exception is separately approved and committed.

Each story follows SpecKit planning, independent plan review, test-first
implementation, combined quality/acceptance review appropriate to Standard,
economical E2E, and checkpoint verification.

All three planned-change authorization triggers apply. After each affected
story plan receives independent `ACCEPT` and before task generation, the
orchestrator must pause, identify the tool/Scan/PRD trigger, explain effects and
implementation intent, cite the accepted plan and affected PRD clauses, and
obtain explicit user approval. This proposal does not waive later gates.

## 13. Approval Decision

The user approved this Sprint Change Proposal on 2026-08-17, authorizing the
planning-artifact updates and creation of Epic 12. The user explicitly directed
the orchestrator to pause before Sprint Loop planning. No SpecKit feature,
story plan, task generation, or implementation is authorized by this approval;
the repository's plan-review and planned-change authorization gates remain
mandatory per story.
