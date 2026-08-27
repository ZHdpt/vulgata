---
story_key: 12-5-grounded-llm-wiki-chat-and-august-30-release-evidence
title: "Story 12.5: Grounded LLM-Wiki Chat & August 30 Release Evidence"
Status: in-progress
Epic: 12
Story: 5
created: 2026-08-27
depends_on:
  - 12-1-durable-scan-foundation-and-analysis-runtime
  - 12-2-lightweight-java-xml-pre-scan-and-prepared-scan-manifest
  - 12-3-resumable-file-pass-1-and-code-logic-documents
  - 12-4-exact-cross-repository-flow-and-pass-2
references:
  - docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-17-august-30-bounded-scanning-recovery.md
  - docs/bmad/planning-artifacts/architecture/august-30-bounded-scanning-design.md
  - docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md
  - docs/bmad/epics.md
  - docs/UBIQUITOUS_LANGUAGE.md
  - AGENTS.md
  - .specify/memory/constitution.md
---

# Story 12.5: Grounded LLM-Wiki Chat & August 30 Release Evidence

## User story

As a **user**,
I want multi-turn Business/IT answers grounded in the generated LLM-Wiki,
so that I can understand the demonstrated cross-Repository business flow and
verify every material technical claim.

## Delivery boundary

Story 12.5 is a bounded delta over the existing Prepared Chat implementation.
It owns the release Chat integration with current generated Documents, bounded
one-hop persisted relationship expansion, production Scan progress UI,
evaluation fixtures and question corpus, and Tier 1/2/3 release evidence. It
does not resume or inherit lifecycle state from
`specs/008-demo-chat-release-gate`; production code and tests from that feature
are reusable implementation evidence only.

Story 12.4 remains an in-progress dependency in another worktree. Definition,
planning, independent UI work, evaluation fixtures, the question corpus, and
release-evidence preparation may proceed in parallel. Final relationship
expansion, final retrieval integration, and Tier 3 real-LLM verification remain
blocked until Story 12.4 has an accepted final checkpoint and its final commit
is integrated into this branch.

## Acceptance boundary

- Retrieval uses deterministic lexical scoring over current authorized Index
  Entries and Document Sections, selects at most five Documents, and expands
  only one hop through current persisted `REFERENCES`, `GENERATED_FROM`, exact
  Cross-Repository Edges, and Flow Groups.
- Evidence packets remain bounded and fingerprinted, contain opaque handles,
  current sections, Source Anchors, required relationships, and bounded
  conversation state, and reject semantic evidence failures after at most one
  syntax repair when time remains.
- Previous answers are context, never evidence; cited Documents are re-fetched
  and revalidated. Business and IT modes, authorization, currentness, citation
  validation, Source Anchors, and the under-30-second target are preserved.
- Chinese production UI reads real persisted Scan state for preparation,
  running, retry, pause, resume, warning completion, and failure. Isolated fake
  Backup B state cannot support release evidence.
- Tier 3 uses a disposable `vulgata_story125_*` PostgreSQL database, two real
  sample Repositories, real CodeGraph/JDT LS/LLM, 50-150 automatically selected
  File Code Units, restart verification, screenshots, and durable revisions,
  counts, durations, versions, and fingerprints.

## Planning constraints

The plan must classify Story 12.5 obligations as already implemented,
partially implemented, obsolete, or missing. It must explicitly replace the
retriever's legacy `ExactCrossRepositoryEdgeEvidence` assumptions with the
accepted Story 12.4 v2 authority only after that dependency is integrated:
current `CrossRepositoryResolutionDecision` records, reusable current
`DocumentEdge` relationships, complete `GENERATED_FROM` provenance, and
`FlowGroup` plans/publications.

Tasks must be separated into:

- **A:** dependency-independent definition, UI, evaluation, question corpus,
  and evidence preparation;
- **B:** dependency-sensitive retrieval and relationship integration;
- **C:** final Tier 3 real-LLM, restart, screenshot, and release-evidence
  verification.

No B or C production work may begin before the Story 12.4 final-checkpoint
integration gate. Prefer no EF migration. `VulgataDbContext`, migrations, and
`VulgataDbContextModelSnapshot` are excluded unless an accepted plan proves a
change unavoidable and ownership is explicitly coordinated. `Program.cs`, DI
registration, project files, and central endpoint registration are shared
integration files. Embeddings, vector search, autonomous multi-hop RAG, and
unrestricted Agent tool loops remain excluded.

## Ownership

The active root is the sole lifecycle/state writer and committer. This worktree
owns only the new Story 12.5 definition and its explicitly approved later
implementation paths. The separate Story 12.4 worktree, qualification database,
temporary directories, dirty state, index, and commits are never modified,
cleaned, staged, or incorporated implicitly.

## Activation

Fresh activation began from
`0d835c5f65f70a3c6a9561c6ff600069bb93e832` on branch
`demo0831/story-12-5`. The base contains committed Story 12.4 v2 candidate
contracts and implementation seams but not its accepted final checkpoint. The
next stage is bounded Story 12.5 specification authoring after read-only context
synthesis. The user-approved parallel split is an execution-order authority;
it is neither planned-change authorization nor implementation approval.

## Dev Agent Record

### Context reference

- Canonical activation:
  `docs/bmad/implementation-artifacts/workflow-activations/12-5-grounded-llm-wiki-chat-and-august-30-release-evidence.json`.
- Proposed SpecKit short name: `grounded-llm-wiki-chat-release-evidence`.
- Expected feature directory:
  `specs/014-grounded-llm-wiki-chat-release-evidence`.
- Workflow schema/execution: `3.0` / unified.
- Authority source commit:
  `0d835c5f65f70a3c6a9561c6ff600069bb93e832`.

### Activation checkpoint correction

The initial context checkpoint ID appeared in two adjacent local commits after
a formatting-only correction, so it was not uniquely resolvable. History was
preserved. The activation record supersedes that invalid pending binding with
the unique checkpoint `bmad-12-5-context-20260827T043300Z-9a24bf`.

### Context synthesis outcome

The read-only source synthesis found the Story 12.4 v2 candidate contracts at
the authority base and no inseparable ownership or required change to an
unaccepted dependency contract. Existing Prepared Chat already supplies opaque
packet handles and fingerprinting, authorization/currentness checks, Source
Anchors, mode ranking, grounded claim/citation validation, and reusable UI and
test seams. Retrieval remains coupled to legacy
`ExactCrossRepositoryEdgeEvidence`; v2 relationship selection, bounded
conversation state, packet byte/cycle guards, syntax repair, real persisted
Scan UI, and current release evidence are partial or missing.

The context is ready for specification and planning. Track B and Track C remain
hard-gated on Story 12.4's accepted final checkpoint and integrated final
commit.

### Specification result

The fresh bounded-delta specification at
`specs/014-grounded-llm-wiki-chat-release-evidence` passed author self-review
with 26 acceptance scenarios, 52 functional requirements, and 14 measurable
success criteria. It classifies 8 obligations as already implemented, 8 as
partial, 5 as obsolete, and 8 as missing; preserves the A/B/C dependency gates;
and contains no unresolved placeholder or authority conflict. The next owner is
architecture-sensitive planning.

### Plan-review preparation

The complete architecture-sensitive plan and companion research, data model,
quickstart, packet/Chat/Scan UI/release contracts, and three executable JSON
schemas passed author self-review. The plan deepens Prepared Chat instead of
creating a second stack, requires no EF migration, assigns one owner to shared
integration files, and maps all 52 functional requirements and 14 success
criteria to observable seams.

Track A owns dependency-independent real Scan UI, corpus, evaluation, and
evidence preparation. Track B replaces legacy
`ExactCrossRepositoryEdgeEvidence` authority with the accepted Story 12.4 v2
decision/current directed Edge/published Flow Group authority only after the
dependency checkpoint is integrated. Track C remains gated on accepted Track B
and the same pinned candidate.

Independent plan-only review is mandatory because the plan contains
architecture decisions. The author also identifies an AGENTS.md Agent-tool
trigger: AD-2 through AD-6 materially change the existing governed
`IChatEvidenceRetriever` contract, while adding no provider-callable tool or
open loop. Scan strategy and PRD-conflict triggers are assessed as not
applicable. No tasks may be generated until an independent reviewer accepts
the plan and the user explicitly authorizes that accepted retrieval-tool
delta.

### Plan review attempt 1: REVISE

The independent reviewer accepted the bounded Prepared Chat delta, no-migration
decision, shared-file ownership, hard Track B/C dependency gates, and the
Agent-tool trigger classification. It found three plan-owned defects in the
release-evidence contract: non-passing run states could not be represented
honestly, passed aggregate claims were not conditionally bound to exact
command/provider/artifact evidence, and mandatory screenshot categories were
not enforced. Task generation remains prohibited. Correct the plan and release
contract/schema, then run plan review attempt 2 without requesting routine user
approval.

The correction preserves the accepted architecture and replaces only the
defective evidence design. Immutable run envelopes now represent planned,
attempted, failed, blocked, not-run, and passed states honestly; only passed
runs may carry a release receipt. Passed receipts bind zero-exit redacted
commands, provider/model identity, packet/result artifacts, and explicit Scan,
Edge, Uncertainty, Business Logic Document, Cross-Repository flow, restart, and
database-ownership evidence. Five separate real screenshot categories are
mandatory. Schema positive/negative vectors and the complete plan self-review
pass; independent plan review attempt 2 is next.

### Plan review attempt 2: REVISE

Attempt 2 confirmed all three first-review findings are closed without
regression. It found two new HIGH plan-owned integrity gaps: passed receipts
must recompute and cross-check every derived release metric against bound
same-candidate artifacts, and a passed envelope must contain one closed,
complete, correctly tiered mandatory Tier 1/2/3 check inventory. Correct those
two evidence-contract gaps, add adversarial semantic vectors, and run plan
review attempt 3. Task generation remains prohibited.

The second correction defines one closed ten-check inventory spanning four Tier
1 checks, one Tier 2 check, and five Tier 3 checks; a passed envelope contains
each exact ID once with its fixed tier and same-envelope evidence bindings. The
semantic validator recomputes all derived Scan, Chat, restart, and database
aggregates from bound same-candidate artifacts. Chat usefulness uses exact
two-decimal round-half-up arithmetic. Complete positive and adversarial vectors,
FR/SC traceability, schema validation, and plan self-review pass; independent
plan review attempt 3 is next.

### Plan review attempt 3: ACCEPT

Attempt 3 accepted the complete corrected plan with zero findings. Every prior
release-evidence defect is closed without regression, all 52 functional
requirements and 14 success criteria remain traceable, and the no-migration,
shared-ownership, dependency, security, and failure boundaries are feasible.
No unaccepted Story 12.4 contract must change.

The accepted plan materially changes the repository-governed Chat Agent
retrieval tool `IChatEvidenceRetriever.RetrieveAsync` through AD-2 to AD-6. It
does not add a provider-callable tool or unrestricted loop, change Scan
strategy, or conflict with/weaken the dated PRD overlay. Task generation is
paused for explicit planned-change authorization of scope
`8133a1bc6e5f7e3a9915c12092b29c2822a40e7831db80554d953c99ac3dff75`.
That decision authorizes progression to tasks only; mandatory definition review,
implementation approval, TDD, convergence, quality, E2E, and verification
remain, and Track B/C retain their Story 12.4 checkpoint/integration gates.

### User revision: explicit LLM Chat streaming

Before task generation, the user requested explicit streaming mode for LLM
Chat. This replaces the accepted specification/plan boundary and invalidates
plan-review attempt 3 plus pending authorization scope
`8133a1bc6e5f7e3a9915c12092b29c2822a40e7831db80554d953c99ac3dff75`.

The correction must specify provider-side streaming with bounded UTF-8 chunk
assembly, inactivity/overall deadlines, cancellation, overflow and malformed
stream handling, and deterministic final parsing. Streamed provider chunks are
untrusted transport data: no partial claim, citation, handle, or relationship
may render before the complete answer passes syntax, packet, citation,
authorization, currentness, and semantic evidence validation. After validation,
the existing Unicode-safe progressive reveal may present the accepted answer.
Revise the specification first, regenerate the complete plan set, and rerun the
independent plan-only review. No task or production work is authorized.

The revised specification now makes streaming an explicit acceptance boundary.
Both the primary grounded-chat request and its one syntax-repair request must
use provider streaming. The server assembles the stream with strict UTF-8
handling, 16 KiB per-chunk and 64 KiB total limits, a shared under-30-second
budget, inactivity detection, cancellation, exactly one terminal event, and
fail-closed handling for malformed, overflowed, truncated, duplicate-terminal,
or post-terminal data. Provider chunks remain untrusted and invisible until the
whole response passes syntax, packet, citation, authorization, currentness, and
semantic validation; only then may the existing Unicode-safe progressive reveal
display the accepted answer. Release evidence must prove real streaming without
recording answer content, secrets, or protected evidence. Specification and
requirements-checklist self-review pass; the complete architecture plan is now
being regenerated from this corrected boundary.

The regenerated architecture plan passed author self-review. It introduces a
dedicated Agents-only Chat provider-streaming seam for both primary and syntax
repair, one coordinator-owned 29-second deadline, strict incremental UTF-8,
received-byte and terminal framing rules, content-dark stream receipts, and a
hard publication barrier before the existing validated UI reveal. It preserves
the previously corrected release-evidence integrity model, A/B/C gates,
Story 12.4 v2 authority, no-migration decision, and shared-file ownership.
Independent architecture-sensitive plan review attempt 4 is now required.

### Plan review attempt 4: REVISE

Attempt 4 preserved the complete 60-FR/17-SC trace, prior evidence-integrity
corrections, no-migration decision, A/B/C gates, and Story 12.4 boundary. It
found two HIGH plan-owned streaming gaps. First, the transport contract must
separate raw wire bytes, SSE events, JSON events, decoded answer deltas, and the
final answer so each UTF-8, size, inactivity, terminal, and exact-limit rule has
one implementable owner. Second, Tier 3 evidence must bind content-dark metrics
to the resolved production raw-stream adapter, canonical streaming request,
response headers/protocol, and physical EOF; internally consistent synthetic
chunks derived from an aggregate response must fail. Correct these plan and
contract gaps, add adversarial vectors, and rerun independent plan review.

The plan-only correction now defines a six-stage raw-read, strict wire UTF-8,
SSE, JSON/escape, decoded-delta, and final-answer pipeline. The Story acceptance
caps apply to decoded provider deltas and the assembled answer; separate raw and
SSE safety caps contain protocol overhead. Tier 3 receipts can originate only
from the resolved production response-header streaming adapter and are signed by
an ephemeral same-process authority bound to candidate, case, request, provider,
adapter assembly, `stream=true` request fingerprint, response headers, physical
read, and EOF. An aggregate handler with perfectly consistent synthetic chunks
must fail for absent authorized provenance. Self-review and JSON parsing pass;
independent plan review attempt 5 is next.

### Plan review attempt 5: REVISE

Attempt 5 accepted the raw-adapter provenance correction and found one remaining
narrow consistency defect. The plan failure table still described the 16 KiB
and 64 KiB acceptance caps as pre-decoding counts, while AD-7 correctly assigns
them to post-JSON-unescape Provider Chunk bytes and concatenated decoded bytes.
The data model also retained the old rejection vocabulary instead of schema
2.1's staged raw, wire, SSE, JSON, answer, terminal, and deadline outcomes.
Align those two authorities and add a cross-artifact vocabulary check before
independent plan review attempt 6.

The narrow correction is complete. The failure table now measures
`chunk-overflow` only on a post-JSON-unescape strict-UTF-8 Provider Chunk and
`total-overflow` only on concatenated decoded Provider Chunk bytes. The data
model uses schema 2.1's exact stage-owned rejection and validation vocabulary,
and a cross-artifact vector fails on any synonym, missing category, differing
cap/domain, or reintroduced pre-decoding acceptance instruction. Independent
plan review attempt 6 is now required.

### Plan review attempt 6: ACCEPT

Attempt 6 accepted the complete corrected plan with zero findings. All streaming
protocol stages, byte domains, failure vocabularies, signed raw-adapter
provenance, aggregate-forgery rejection, 60 FRs, 17 SCs, prior release-evidence
integrity corrections, Story 12.4 gates, and no-migration/security/ownership
boundaries remain intact.

The accepted plan materially changes the governed Chat Agent retrieval tool
through AD-2 to AD-6 and materially changes Chat Agent provider dispatch through
AD-7: primary synthesis and the one permitted syntax repair must use the strict
server-only response-header streaming adapter. Scan strategy is unchanged and
the current PRD is strengthened rather than weakened. Task generation is paused
for explicit planned-change authorization of scope
`7581b411951e52fa08dd979bcc3b6a286645c1373ff60657044d851448fda05c`.
That authorization permits progression to tasks only; definition review,
mandatory implementation approval, TDD, convergence, quality, E2E, and final
verification remain, and Track B/C stay gated on Story 12.4's accepted final
checkpoint and integrated final commit.

### Planned-change authorization 02: ACCEPTED

The user approved scope
`7581b411951e52fa08dd979bcc3b6a286645c1373ff60657044d851448fda05c`
on 2026-08-27. This authorizes task generation for the accepted retrieval-tool,
packet/currentness, mandatory primary/repair provider-streaming dispatch, and
post-validation reveal-separation plan. It does not authorize production
implementation. Tasks and independent definition review proceed next; the
mandatory implementation-approval pause remains, and Track B/C retain their
Story 12.4 accepted-checkpoint and integrated-commit gates.

### Task authoring

The task author generated 50 dependency-ordered tasks. Track A covers
dependency-independent production Scan UI, corpus, evaluation, and release
preparation. Track B contains the Story 12.4-gated current-v2 retrieval,
relationship, packet, conversation, and mandatory provider-streaming work.
Track C contains accepted-Track-B-gated Tier 2/Tier 3 real-tool, real-LLM,
restart, screenshot, and release verification. All 60 FRs and 17 SCs are mapped;
behavioral work is ordered RED before GREEN, existing behavior is characterized
without fabricated RED, and the task author self-review passes. Independent
combined definition review attempt 1 is next. No production work is authorized.

### Definition review attempt 1: REVISE

The independent reviewer confirmed nominal coverage of all 60 FRs and 17 SCs
but found four tasks-owned defects: the repository SpecKit resolver still points
at feature 013; several task paths contradict live source and the accepted plan;
existing Unicode/reduced-motion/render-scroll behavior was incorrectly labeled
RED while remaining-deadline behavior was omitted; and corpus/adversarial tests
were ordered after their fixtures or implementations. The accepted spec, plan,
planned-change authorization, no-migration decision, and Story 12.4 gates remain
valid. Correct tasks only and rerun independent definition review.

The tasks-only correction now pins and verifies feature 014 before implementation,
uses the accepted plan's exact live/new paths and single-owner integration
files, keeps existing reveal/scroll behavior as characterization, adds the real
remaining-deadline RED/GREEN seam, and moves corpus/adversarial tests before
their fixtures or GREEN owners. A second path audit also corrected all new
Story 12.5 release tests and the corpus to the accepted
`tests/Vulgata.Tests/ReleaseGate/Story125/` inventory. All 50 task IDs and the
60-FR/17-SC trace remain intact. Definition review attempt 2 is next.

### Definition review attempt 2: REVISE

Attempt 2 closed the reveal finding and confirmed feature 014 routing, but found
four remaining tasks-owned defects: T001 needs executable PowerShell environment
syntax and restoration; accepted scoring/streaming/DI/Scan integration paths and
owners are incomplete; several `[P]` flags permit same-file or dependency-breaking
concurrency; and no task owns the accepted cross-artifact streaming vocabulary
vector. The root verified and adopted `.specify/feature.json` as the required
feature-014 per-worktree definition routing metadata. Correct tasks only and
rerun definition review; no production implementation is authorized.

The second tasks-only correction makes the feature-routing command executable
and self-restoring, completes the accepted ranking/projector/Core-streaming/DI
owners, provides independent Track A composition before the Story 12.4 gate,
removes conflicting parallel markers, and assigns the cross-artifact streaming
vocabulary RED/GREEN. Root follow-up verification also tightened T019, T020, and
T021 so their claimed paths and owners are explicit. Definition review attempt
3 is next.

### Definition review attempt 3: REVISE

Attempt 3 closed the routing, Track A/streaming composition, scoring/projector,
parallelism, and cross-artifact streaming findings. Two tasks-owned gaps remain:
the accepted currentness, result-validator, candidate-extractor, prompt-catalog,
and embedded Business/IT prompt paths need explicit characterization/GREEN
owners and the neutral fixture task needs exact reuse/provenance paths; T047 must
also run after T046 rather than in parallel. Correct tasks only and run attempt
4. A fourth REVISE would require a user pause; ACCEPT advances only to the
mandatory implementation-approval gate.

The final automatic tasks correction now assigns every accepted currentness,
result-validator, candidate-extractor, prompt-catalog, and embedded Business/IT
prompt path to explicit characterization/RED and GREEN owners; it names the
exact existing neutral fixtures and manifest boundary; and it requires T047 to
run after T046. Definition review attempt 4 is next. If it revises again, the
workflow pauses for user direction; if it accepts, the workflow pauses at
mandatory implementation approval.

### Definition review attempt 4: ACCEPT

Attempt 4 accepted the complete definition with zero findings. All ten findings
from attempts 1-3 are closed. The accepted boundary contains 60 mapped FRs,
17 mapped SCs, and 50 exact-path, dependency-ordered tasks with no unresolved
placeholder or ownership ambiguity. Track A is independently composable after
implementation approval; Track B remains additionally gated on Story 12.4 final
acceptance and integration; Track C remains gated on accepted Track B.

The workflow is now paused for mandatory implementation approval of scope
`787352f9562b45f62e0474806bd33bd6e7bf11d56f807a99fdd54c16335dda4d`.
No production or test write is authorized before that decision. The accepted
definition checkpoint is pending Git binding.
