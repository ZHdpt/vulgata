---
title: "Sprint Change Proposal: July 2026 Scanning Baseline Merge"
status: implemented
date: 2026-07-10
project: vulgata
scope: moderate
source_handoff: "C:/Users/13728/AppData/Local/Temp/vulgata-handoff-bmad-correct-course-2026-07-10.md"
---

# Sprint Change Proposal: July 2026 Scanning Baseline Merge

## 1. Issue Summary

The July 2026 scanning plan supersedes older BMAD scanning assumptions. The PRD and epics still described an LLM-led Orchestrator Agent, Pass 2 as one Worker Agent per System, and `index.md` as the Chat Agent's canonical retrieval input.

The new source of truth is:

- `docs/bmad/planning-artifacts/architecture/scanning-v1-technical-plan.md`
- `docs/bmad/planning-artifacts/architecture/scanning-v1-data-contracts.md`
- `docs/bmad/planning-artifacts/architecture.md`, section `July 2026 Scanning Update`

The correction was requested directly on 2026-07-10: merge the July plan into the BMAD planning baseline.

## 2. Impact Analysis

Epic Impact:

- Epic 3 needed provider role wording updated because the Orchestrator is deterministic and no longer consumes an LLM Provider.
- Epic 5 needed the largest correction: deterministic Orchestrator, typed Worker Agent dispatch, Document Version storage, `dispatch_sites`, flow traces, flow groups, `index_entries`, and `document_sections`.
- Epic 9 needed retrieval wording updated from `index.md` reads to database-backed typed retrieval tools.
- No new epic is required.
- No epic ordering change is required.

Story Impact:

- Story SP.1 now validates the Worker Agent execution adapter, not LLM-led Magentic orchestration.
- Story 3.1 and 3.2 now assign providers by LLM task type: pre-scan, worker, chat.
- Story 5.1 now reflects Logical Documents, immutable Document Versions, graph tables, flow-trace persistence, and retrieval indexes.
- Story 5.3 now defines a deterministic Orchestrator and typed Worker Agent jobs.
- Story 5.5 now defines graph-first Pass 2 flow tracing and one Worker Agent per flow group.
- Story 9.2 and 9.4 now use `search_documents`, `get_document_sections`, graph expansion, endpoint, uncertainty, flow-trace, and source-anchor context.

Artifact Conflicts:

- PRD glossary and FRs conflicted with July terminology and retrieval contracts.
- Epics repeated the same old assumptions in requirements inventory, mapping, epic summaries, and story acceptance criteria.
- Architecture already links the July scanning artifacts and required no additional patch in this correction.
- UX design artifacts do not conflict with the July scanning plan and required no patch.
- `sprint-status.yaml` required no patch because no epics or stories were added, removed, renumbered, or status-changed.

Technical Impact:

- Planning now expects a deterministic Orchestrator and LLM Worker Agents only for bounded analysis/synthesis.
- Planning now expects PostgreSQL-backed retrieval indexes and section-level retrieval by default.
- Existing code or completed implementation that still exposes `AgentType.Orchestrator` should be reviewed in a follow-up implementation story to decide whether that enum is renamed, hidden, migrated, or reinterpreted as a legacy/provider UI compatibility value.

## 3. Recommended Approach

Selected path: Direct Adjustment.

Rationale:

- The July scanning architecture already exists and is linked from the architecture document.
- The V1 goal and demo scope remain intact.
- The change is broader than a typo because it touches PRD, epic, and story-level acceptance criteria.
- It is not a fundamental replan because the updated plan preserves the same product goal and implementation path.

Scope classification: Moderate.

Effort estimate: Low to medium for planning edits; medium for any follow-up code/schema alignment that discovers old Orchestrator assumptions in implementation.

Risk level: Low for documentation; medium for implementation if existing completed Epic 3 code assumes a provider assignment for an LLM Orchestrator.

## 4. Detailed Change Proposals

### PRD

Glossary:

- OLD: Scan Coordinator spawns Orchestrator Agents; Orchestrator Agent is LLM-powered.
- NEW: Scan Coordinator triggers a deterministic per-Repository Orchestrator; Worker Agents remain LLM agents.

Document model:

- OLD: Document is a single immutable markdown file concept.
- NEW: Document is a generated knowledge artifact with Logical Document and immutable Document Version identity.

Pass 2:

- OLD: One Worker Agent per System synthesizes BL Docs and identifies flows autonomously.
- NEW: The Orchestrator performs path-aware flow tracing and deterministic flow-group clustering; one Worker Agent synthesizes one BL Doc per flow group.

Retrieval:

- OLD: Chat reads `index.md`, selects Documents, reads them in full, and follows links.
- NEW: Chat uses typed tools over `index_entries`, `document_sections`, graph relationships, flow traces, endpoint records, uncertainties, and source anchors. `index.md` is export/readability only.

LLM providers:

- OLD: Provider assignment includes an LLM Orchestrator role.
- NEW: Provider assignment is by LLM task type: pre-scan, worker, chat. The deterministic Orchestrator has no provider assignment.

### Epics And Stories

Epic 3:

- OLD: Global defaults and System overrides are per agent role, including Orchestrator.
- NEW: Defaults and overrides are per LLM task role: pre-scan, worker, chat.

Pre-Epic Gate:

- OLD: Validate Magentic orchestration between OrchestratorAgent and WorkerAgent.
- NEW: Validate the Worker Agent execution adapter under deterministic Orchestrator control.

Epic 5:

- OLD: Document Graph Store includes old document tables, `is_cross_repo`, and special SYSTEM `index.md`.
- NEW: Storage includes Logical Document/Document Version fields, endpoint/dispatch tables, flow-trace tables, `index_entries`, `document_sections`, and `scan_llm_usage`. Cross-Repository reachability is derived from Repository IDs, not a separate edge type.

Story 5.3:

- OLD: OrchestratorAgent batches and dispatches Worker Agents through Magentic.
- NEW: Orchestrator deterministically batches Code Units, dispatches typed Worker Agent jobs, runs bounded refinement, and starts Pass 2 flow planning.

Story 5.5:

- OLD: Pass 2 starts after all System CL Docs and assigns one Worker Agent per System.
- NEW: Pass 2 starts after a Repository Pass 1, persists graph traces, clusters flow groups, and assigns one Worker Agent per flow group.

Epic 9:

- OLD: Chat pipeline reads `index.md` and selected full Documents.
- NEW: Chat pipeline uses typed retrieval tools, section-level evidence, graph expansion, stale BL handling, and Chinese/mixed-language fallback search.

## 5. Checklist Record

| Item | Status | Notes |
| --- | --- | --- |
| 1.1 Triggering story | N/A | Trigger came from July scanning design consolidation, not a single implementation story. |
| 1.2 Core problem | Done | Old PRD/epic baseline contradicted July scanning architecture. |
| 1.3 Supporting evidence | Done | Handoff listed exact stale assumptions and July source artifacts. |
| 2.1 Current epic impact | Done | Epic 5 and Epic 9 required direct adjustment. |
| 2.2 Epic-level changes | Done | Existing epics modified; no new epic. |
| 2.3 Future epic impact | Done | MCP wording adjusted; no ordering change. |
| 2.4 Obsolete/new epics | Done | No obsolete or new epics. |
| 2.5 Priority/order | N/A | No resequencing required. |
| 3.1 PRD conflicts | Done | Glossary, FR-3, FR-4, FR-11, FR-13 patched. |
| 3.2 Architecture conflicts | Done | Architecture already links July artifacts; no extra edit needed. |
| 3.3 UX conflicts | N/A | UX behavior remains valid. |
| 3.4 Other artifacts | Done | `sprint-status.yaml` unchanged because no story status or numbering changed. |
| 4.1 Direct adjustment | Viable | Selected path. |
| 4.2 Rollback | Not viable | No implementation rollback needed for docs. |
| 4.3 MVP review | Not viable | V1 MVP remains achievable. |
| 4.4 Recommended path | Done | Direct Adjustment, Moderate scope. |
| 5.1 Issue summary | Done | Included above. |
| 5.2 Impact and adjustments | Done | Included above. |
| 5.3 Rationale | Done | Included above. |
| 5.4 MVP impact/action plan | Done | MVP unchanged; follow-up code review noted. |
| 5.5 Agent handoff | Done | Product Owner/Developer follow-up for backlog/code alignment. |
| 6.1 Checklist completion | Done | No blocking Action-needed items. |
| 6.2 Proposal accuracy | Done | Verified against stale-term sweep. |
| 6.3 User approval | Done | User directly requested the merge in this session. |
| 6.4 Sprint status update | N/A | No epic/story add/remove/renumber/status changes. |
| 6.5 Next steps | Done | See handoff below. |

## 6. Implementation Handoff

Artifacts patched by this correction:

- `docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md`
- `docs/bmad/epics.md`
- `docs/bmad/planning-artifacts/sprint-change-proposal-2026-07-10.md`

Recommended follow-up:

- Developer agent: inspect existing code and completed Epic 3 stories for any user-facing or persisted `Orchestrator` provider assignment. Decide whether it should become `pre_scan`, be hidden, or remain as a compatibility alias.
- Product Owner/Developer: when the next scanning implementation story is created, use the updated Epic 5 acceptance criteria as source of truth.

Success criteria:

- BMAD PRD and epics no longer describe an LLM-led Orchestrator.
- Pass 2 is described as one Worker Agent per flow group.
- Chat retrieval is database-backed and typed-tool based.
- `index.md` is export/readability only, not canonical retrieval input.
- No sprint-status mutation is needed for this correction.
