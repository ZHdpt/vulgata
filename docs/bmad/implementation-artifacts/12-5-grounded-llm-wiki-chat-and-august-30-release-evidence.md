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
