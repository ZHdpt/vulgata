---
stepsCompleted: [1, 2, 3, 4, 5, 6]
project_name: vulgata
user_name: zhdpt
date: 2026-07-10
status: ready-for-sprint-planning
documentsUsed:
  prd: docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md
  architecture: docs/bmad/planning-artifacts/architecture.md
  architecture_companions:
    - docs/bmad/planning-artifacts/architecture/scanning-v1-technical-plan.md
    - docs/bmad/planning-artifacts/architecture/scanning-v1-data-contracts.md
  epics: docs/bmad/epics.md
  ux:
    - docs/bmad/planning-artifacts/ux-designs/ux-vulgata-2026-06-22/DESIGN.md
    - docs/bmad/planning-artifacts/ux-designs/ux-vulgata-2026-06-22/EXPERIENCE.md
  change_proposal: docs/bmad/planning-artifacts/sprint-change-proposal-2026-07-10.md
---

# Implementation Readiness Assessment Report

**Date:** 2026-07-10
**Project:** vulgata

## Step 1: Document Discovery

### Documents Selected For Assessment

| Type | File | Size | Modified |
| --- | --- | ---: | --- |
| PRD | `docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md` | 47,619 bytes | 2026-07-10 10:29:17 |
| Architecture | `docs/bmad/planning-artifacts/architecture.md` | 88,378 bytes | 2026-07-10 10:32:17 |
| Architecture companion | `docs/bmad/planning-artifacts/architecture/scanning-v1-technical-plan.md` | 15,988 bytes | 2026-07-09 19:33:22 |
| Architecture companion | `docs/bmad/planning-artifacts/architecture/scanning-v1-data-contracts.md` | 12,487 bytes | 2026-07-09 19:33:22 |
| Epics and stories | `docs/bmad/epics.md` | 145,697 bytes | 2026-07-10 13:23:44 |
| UX design | `docs/bmad/planning-artifacts/ux-designs/ux-vulgata-2026-06-22/DESIGN.md` | 14,885 bytes | 2026-06-22 14:44:33 |
| UX experience | `docs/bmad/planning-artifacts/ux-designs/ux-vulgata-2026-06-22/EXPERIENCE.md` | 22,400 bytes | 2026-07-10 10:32:36 |
| Change proposal | `docs/bmad/planning-artifacts/sprint-change-proposal-2026-07-10.md` | 9,644 bytes | 2026-07-10 09:50:02 |

### Discovery Notes

- No whole/sharded duplicate document formats require resolution.
- `docs/bmad/epics.md` is outside `planning-artifacts`, but it is the authoritative BMAD epics/story breakdown for this repository.
- UX is represented by `DESIGN.md` and `EXPERIENCE.md` inside the UX design folder rather than a single `*ux*.md` file.
- The July scanning technical plan and data contracts are architecture companions and are required for scanning readiness validation.

## Step 2: PRD Analysis

### Functional Requirements

The PRD currently defines 89 Functional Requirements:

| Group | FR Range | Count | Scope |
| --- | --- | ---: | --- |
| Authentication & User Management | FR-1.1 to FR-1.4 | 4 | Registration, login/logout, profile, roles |
| System & Repository Management | FR-2.1 to FR-2.5 | 5 | System CRUD, Repository CRUD, standalone repositories, git URL validation |
| LLM Provider Management | FR-3.1 to FR-3.4 | 4 | Global providers, multiple providers, per-System task-role overrides, connection test |
| Scanning Pipeline | FR-4.1 to FR-4.12 | 12 | Pre-scan, deterministic Orchestrator, Worker Agents, Pass 1, flow-group Pass 2, retrieval indexes, log, CodeGraph, immutability, adapter spike, cross-verification |
| Cross-Repository Communication Detection | FR-5.1 to FR-5.14 | 14 | Communication Pattern Catalog, RPC/HTTP/MQ/file/page navigation detection, provider/consumer roles, Service Topology, extensibility |
| Uncertainty Resolution | FR-6.1 to FR-6.7 | 7 | Uncertainty states, Cross-Repository Resolution, deadlock breaking, Dangling Links, summaries, stale notices, impact analysis |
| Human-in-the-Loop | FR-7.1 to FR-7.4 | 4 | Worker Agent questions, answers, lower-priority human input, pending question dashboard |
| Third-Party Library Handling | FR-8.1 to FR-8.5 | 5 | Standalone libraries, partial scans, locking, allowlist, unknown-library inference |
| Git Monitoring & Incremental Scan | FR-9.1 to FR-9.5 | 5 | Polling, pull, incremental re-scan, history, same-Repository scan guard |
| Database Connection Tools | FR-10.1 to FR-10.3 | 3 | Repository DB connections, schema inspection, sample data queries |
| Chat Interface | FR-11.1 to FR-11.8 | 8 | Business/IT mode, context selection, upload, typed retrieval, citations, auto-scope |
| Dashboard & Scan Progress | FR-12.1 to FR-12.6 | 6 | Status, active workers, files, errors, HITL questions, live graph |
| MCP Integration | FR-13.1 to FR-13.5 | 5 | MCP tool consumption, approvals, knowledge serving, configuration levels |
| User-Supplied Context | FR-14.1 to FR-14.4 | 4 | Global/System/Repository context and active-scan application rules |
| LSP Support | FR-15.1 to FR-15.3 | 3 | Optional symbol resolution, type information, Repository-level config |

### Non-Functional Requirements

The PRD defines 15 Non-Functional Requirements:

| Group | NFR Range | Count | Scope |
| --- | --- | ---: | --- |
| Performance | NFR-1.1 to NFR-1.3 | 3 | Scan duration, chat response time, graph update latency |
| Security | NFR-2.1 to NFR-2.4 | 4 | Password hashing, secret encryption, code/data handling, read-only DB tools |
| Reliability | NFR-3.1 to NFR-3.3 | 3 | Worker retry, API degradation, scan-state persistence |
| Deployability | NFR-4.1 | 1 | Single-container application deployment with database decision in architecture |
| Usability | NFR-5.1 to NFR-5.2 | 2 | Non-technical first-use success, Blazor accessibility baseline |
| Maintainability | NFR-6.1 to NFR-6.2 | 2 | Externalized prompts, documented Communication Pattern Catalog |

### Additional PRD Constraints

- Assumptions A-1 through A-6 remain explicit and traceable to relevant FR/NFR entries.
- Open Questions OQ-1 through OQ-7 remain non-blocking but should influence demo preparation and scan-quality validation.
- Out-of-scope list excludes full RBAC, multi-language expansion beyond demo repositories, audit/compliance reporting, HA deployment, enterprise SSO, Audition Agent, answer confidence scoring, gRPC detection, database shared-access communication detection, production database access, webhook git detection, and WCAG compliance.

### PRD Completeness Assessment

The PRD is complete enough for implementation readiness after the July correction. During extraction, one traceability mismatch was found and fixed before coverage validation: auto-scope behavior now exists in the PRD as FR-11.8, and database-tool requirements are consistently represented as FR-10.1 to FR-10.3 rather than stale Epic-only IDs.

## Step 3: Epic Coverage Validation

### Coverage Statistics

- Total PRD FRs: 89
- FRs covered in epics: 89
- Extra FRs in epics but not PRD: 0
- Coverage percentage: 100%

### Coverage Matrix

| FR Number | Requirement Summary | Epic Coverage | Status |
| --- | --- | --- | --- |
| FR-1.1 | User registration with email and password | Epic 1 | Covered |
| FR-1.2 | User login and logout | Epic 1 | Covered |
| FR-1.3 | View and edit own profile | Epic 1 | Covered |
| FR-1.4 | Role-based access (Administrator, SystemOwner, User) | Epic 1 | Covered |
| FR-2.1 | Create, edit, delete Systems | Epic 2 | Covered |
| FR-2.2 | Add Repositories to a System | Epic 2 | Covered |
| FR-2.3 | Remove Repositories from a System | Epic 2 | Covered |
| FR-2.4 | Create standalone Repositories | Epic 2 | Covered |
| FR-2.5 | Validate git URL reachability | Epic 2 | Covered |
| FR-3.1 | Configure global LLM Providers with default task-role assignments | Epic 3 | Covered |
| FR-3.2 | Support multiple LLM Providers simultaneously | Epic 3 | Covered |
| FR-3.3 | Override default provider per System LLM task role | Epic 3 | Covered |
| FR-3.4 | Test each LLM Provider connection | Epic 3 | Covered |
| FR-4.1 | Pre-Scan Profiling and approval gate | Epic 4 | Covered |
| FR-4.2 | Scan Coordinator and deterministic Orchestrator dispatch | Epic 5 | Covered |
| FR-4.3 | Worker Agent Code Unit processing | Epic 5 | Covered |
| FR-4.4 | Pass 1 Code Logic Documents and Uncertainties | Epic 5 | Covered |
| FR-4.5 | Pass 2 flow tracing and Business Logic Documents | Epic 5 | Covered |
| FR-4.6 | Virtual document tree by source path | Epic 5 | Covered |
| FR-4.7 | Database-backed retrieval indexes; `index.md` export only | Epic 5 | Covered |
| FR-4.8 | Append-only `log.md` | Epic 5 | Covered |
| FR-4.9 | CodeGraph structural integration | Epic 5 | Covered |
| FR-4.10 | Immutable Documents and Version Chain | Epic 5 | Covered |
| FR-4.11 | Worker Agent execution adapter validation spike | Spike Gate | Covered |
| FR-4.12 | Worker cross-verification of related Code Units | Epic 5 | Covered |
| FR-5.1 | Communication Pattern Catalog configuration | Epic 7 | Covered |
| FR-5.2 | Cross-Repository Communication Detection | Epic 7 | Covered |
| FR-5.3 | Provider and consumer role identification | Epic 7 | Covered |
| FR-5.4 | Consumer count tracking | Epic 7 | Covered |
| FR-5.5 | External references for absent provider/consumer | Epic 7 | Covered |
| FR-5.6 | Low-confidence page navigation detection | Epic 7 | Covered |
| FR-5.7 | Object flow analysis | Epic 7 | Covered |
| FR-5.8 | Dynamic catalog injection into Worker prompts | Epic 7 | Covered |
| FR-5.9 | Prompt Workbench | Epic 7 | Covered |
| FR-5.10 | RPC endpoint named entities | Epic 7 | Covered |
| FR-5.11 | File-based communication detection | Epic 7 | Covered |
| FR-5.12 | Cross-repo page navigation detection | Epic 7 | Covered |
| FR-5.13 | Service Topology resolution via MCP tool | Epic 7 | Covered |
| FR-5.14 | Extensible detection catalog | Epic 7 | Covered |
| FR-6.1 | Unresolved cross-repo Uncertainty recording | Epic 5 | Covered |
| FR-6.2 | Cross-Repository Resolution after Repository scan | Epic 5 | Covered |
| FR-6.3 | Deadlock-Breaking Protocol | Epic 5 | Covered |
| FR-6.4 | Dangling Links and `wontfix` terminal state | Epic 5 | Covered |
| FR-6.5 | Chat-accessible Uncertainty summary | Epic 5 | Covered |
| FR-6.6 | Cross-Repo Stale Notices | Epic 5 | Covered |
| FR-6.7 | Impact Analysis query | Epic 5 | Covered |
| FR-7.1 | Worker Agent ambiguity questions | Epic 8 | Covered |
| FR-7.2 | System Owner answers through dashboard | Epic 8 | Covered |
| FR-7.3 | Human input priority rules | Epic 8 | Covered |
| FR-7.4 | Pending question dashboard count/actions | Epic 8 | Covered |
| FR-8.1 | Org-owned libraries as standalone Repositories | Epic 6 | Covered |
| FR-8.2 | Partial on-demand standalone Repository scans | Epic 6 | Covered |
| FR-8.3 | Standalone Repository scan locking | Epic 6 | Covered |
| FR-8.4 | Well-known library allowlist | Epic 6 | Covered |
| FR-8.5 | LLM inference for unknown libraries | Epic 6 | Covered |
| FR-9.1 | Periodic git monitoring | Epic 10 | Covered |
| FR-9.2 | Pull changes when clone is behind | Epic 10 | Covered |
| FR-9.3 | Incremental re-scan of affected Code Units | Epic 10 | Covered |
| FR-9.4 | Preserve Document history | Epic 10 | Covered |
| FR-9.5 | Same-Repository scan guard | Epic 10 | Covered |
| FR-10.1 | Repository database connection configuration | Epic 3 | Covered |
| FR-10.2 | Worker Agent schema inspection tool | Epic 5 | Covered |
| FR-10.3 | Worker Agent sample data query tool | Epic 5 | Covered |
| FR-11.1 | Business Mode light theme and business narrative | Epic 9 | Covered |
| FR-11.2 | IT Mode dark theme and code references | Epic 9 | Covered |
| FR-11.3 | Mode switching | Epic 9 | Covered |
| FR-11.4 | Context selection | Epic 9 | Covered |
| FR-11.5 | Supplementary file upload | Epic 9 | Covered |
| FR-11.6 | Typed database-backed LLM-Wiki retrieval | Epic 9 | Covered |
| FR-11.7 | Citations to Documents and source lines | Epic 9 | Covered |
| FR-11.8 | Auto-determine scope when none selected | Epic 9 | Covered |
| FR-12.1 | Real-time Scan status | Epic 8 | Covered |
| FR-12.2 | Active Worker Agent count | Epic 8 | Covered |
| FR-12.3 | Files processed / total files | Epic 8 | Covered |
| FR-12.4 | Error count | Epic 8 | Covered |
| FR-12.5 | Pending HITL question management | Epic 8 | Covered |
| FR-12.6 | Live knowledge graph | Epic 8 | Covered |
| FR-13.1 | MCP tool consumption for scanning | Deferred | Covered |
| FR-13.2 | MCP tool consumption for chat | Deferred | Covered |
| FR-13.3 | Separate MCP approvals for scanning and chat | Deferred | Covered |
| FR-13.4 | Knowledge base exposure as MCP server | Deferred | Covered |
| FR-13.5 | Repository/System/Global MCP configuration | Deferred | Covered |
| FR-14.1 | Global user-supplied context | Epic 2 | Covered |
| FR-14.2 | System-level user-supplied context | Epic 2 | Covered |
| FR-14.3 | Repository-level user-supplied context | Epic 2 | Covered |
| FR-14.4 | Safe timing for context application | Epic 2 | Covered |
| FR-15.1 | Optional LSP symbol resolution | Deferred | Covered |
| FR-15.2 | Optional LSP type information | Deferred | Covered |
| FR-15.3 | Repository-level LSP configuration | Deferred | Covered |

### Missing Requirements

None.

### Validation Notes

- The earlier database-tool mismatch was corrected before final validation: `FR-3.5`, `FR-4.13`, and `FR-4.14` no longer appear in the PRD or epics; the canonical IDs are `FR-10.1`, `FR-10.2`, and `FR-10.3`.
- `FR-11.8` is now present in PRD, epics, and the architecture mapping.

## Step 4: UX Alignment Assessment

### UX Document Status

UX documentation is present:

- `docs/bmad/planning-artifacts/ux-designs/ux-vulgata-2026-06-22/DESIGN.md`
- `docs/bmad/planning-artifacts/ux-designs/ux-vulgata-2026-06-22/EXPERIENCE.md`

### UX to PRD Alignment

| Area | Evidence | Status |
| --- | --- | --- |
| Chinese-only UI | `DESIGN.md` defines Chinese (Simplified) as the only V1 UI language; repo instructions and epics repeat this. | Aligned |
| Chat-first IA | UX defines `对话` and `管理后台` as the two top-level routes with chat as default after login; PRD JTBD and epics preserve this. | Aligned |
| Business/IT modes | UX locks Business Mode to light theme and IT Mode to dark theme; PRD `FR-11.1` and `FR-11.2` match this. | Aligned |
| Mode switching | UX requires confirmation and a new chat session on mode switch. PRD `FR-11.3` previously implied mid-session prompt/theme mutation; it was corrected to the confirmed-new-session behavior. | Fixed |
| System/Repository selector | UX specifies inline multi-select and first-use default to all authorized Systems; PRD `FR-11.4` and Epic 9 match. | Aligned |
| Auto-scope when selection is empty | PRD `FR-11.8` and Epic 9 require autonomous scope selection. UX `EXPERIENCE.md` now includes the empty-selection feedback and adjust action. | Fixed |
| Citations and answer rendering | UX requires citation links; PRD `FR-11.7` and Epic 9 preserve citation linking. | Aligned |
| Graph visualization | UX specifies a full-screen Z.Blazor.Diagrams graph page; PRD `FR-12.6` and architecture use Blazor.Diagrams. | Aligned |

### UX to Architecture Alignment

- Architecture uses Blazor interactive server render mode with Microsoft Fluent UI Blazor as the primary UI library, matching `EXPERIENCE.md`.
- Architecture keeps Z.Blazor.Diagrams as the graph-only non-Fluent exception, matching `DESIGN.md` and `EXPERIENCE.md`.
- Architecture supports the chat-first layout, management routes, notification panel, graph page, and document viewer named in the UX.
- Architecture supports database-backed retrieval and auto-scope through typed retrieval services over `index_entries`, `document_sections`, graph relationships, flow traces, endpoints, uncertainties, and source anchors.

### Alignment Issues

No unresolved UX alignment issues remain after the corrections above.

### Warnings

- The UX docs predate the July scanning correction, so future UX work should avoid reintroducing `index.md` as a chat retrieval source. The architecture and PRD now make `index.md` export-only.
- Mockup files are referenced by UX docs but were not validated as part of this readiness pass.

## Step 5: Epic Quality Review

### Best Practices Checklist

| Area | Assessment | Status |
| --- | --- | --- |
| Epic user value | Epics 1-9 now have user, SystemOwner, or platform-operability outcomes. Infrastructure-heavy stories are tied to first consumers: scanning, retrieval, graph, HITL, and chat. | Ready |
| Epic independence | Epic order is coherent: identity -> management -> provider/db config -> profiling -> scanning foundation -> libraries -> cross-repo detection/resolution -> dashboard/graph -> chat. | Ready |
| Story sizing | The former Story 5.1 mega-story has been split into Document Version/source anchors, graph facts, flow provenance, and retrieval/export artifacts. | Ready |
| Acceptance criteria | Epics 1-9 now have implementable Given/When/Then criteria. Epic 10 remains explicitly deferrable. | Ready with deferral |
| Forward dependencies | Epic 5 now clearly owns infrastructure and best-effort matching; Epic 7 owns final Cross-Repository Detection and Resolution completion. | Ready |
| Traceability | FR coverage remains complete, and story-level readiness is now complete for non-deferred implementation scope. | Ready |

### Resolved Findings

1. **Epic 7 is now story-ready.**
   - Evidence: `docs/bmad/epics.md` now includes Stories 7.1-7.9 covering Communication Pattern Catalog management, Worker prompt injection and result contracts, RPC/HTTP detection, MQ/file detection, page navigation, object flow analysis, Service Topology resolution, Prompt Workbench, and Cross-Repository Resolution finalization.

2. **Epic 8 is now story-ready.**
   - Evidence: `docs/bmad/epics.md` now includes Stories 8.1-8.6 covering scan progress dashboard, HITL notification center, answer/dismiss metadata flow, SignalR replay/authorization, live graph projection, and graph filtering/details/error states.

3. **Story 5.1 is no longer a schema mega-story.**
   - Evidence: Epic 5 now splits the storage foundation into Stories 5.1-5.4: Document Version/source anchors, graph edge/endpoint/Uncertainty store, flow trace/provenance store, and retrieval/virtual tree/export artifacts.

4. **`FR-6.x` ownership is clarified.**
   - Evidence: The FR coverage map now marks `FR-6.2`, `FR-6.3`, `FR-6.6`, and `FR-6.7` as Epic 5 + Epic 7 work where appropriate. Epic 5 owns infrastructure and best-effort matching; Epic 7 owns final Cross-Repository Resolution after full detection evidence exists.

### Remaining Non-Blocking Deferral

- **Epic 10 remains deferrable.** It covers `FR-9.1`-`FR-9.5` for git monitoring and incremental re-scan. This is acceptable for Sprint Planning if the first sprint plan explicitly excludes Epic 10 or schedules it after the core demo path.

### Epic Readiness Summary

| Scope | Readiness |
| --- | --- |
| Pre-Epic Gate SP.1 | Ready and required before Worker Agent implementation |
| Epics 1-4 | Ready |
| Epic 5 | Ready after Story 5.1 split and `FR-6.x` boundary clarification |
| Epic 6 | Ready |
| Epic 7 | Ready |
| Epic 8 | Ready |
| Epic 9 | Ready |
| Epic 10 | Deferred, not a blocker for initial Sprint Planning |

## Step 6: Summary and Recommendations

### Overall Readiness Status

**READY FOR SPRINT PLANNING**, with Epic 10 explicitly deferred.

The planning artifacts are aligned on the July scanning model, role names, chat retrieval source, Business/IT mode switching, auto-scope behavior, story sizing, and non-deferred epic completeness. The prior blockers identified by BMAD Help and the first readiness run have been addressed.

### Sprint Planning Guidance

1. Start Sprint Planning with the Pre-Epic Gate `SP.1` Worker Agent execution adapter validation spike.
2. Plan Epics 1-4 before Worker Agent execution work.
3. In Epic 5, implement storage slices before Orchestrator/Worker stories: 5.1 -> 5.2 -> 5.3 -> 5.4 -> 5.5+.
4. Treat Epic 7 as required before claiming full Cross-Repository Detection and Resolution.
5. Treat Epic 8 as required before claiming dashboard/HITL/live graph readiness.
6. Keep Epic 10 out of the initial sprint unless the user explicitly re-prioritizes git monitoring.

### Corrections Applied During This Assessment

- PRD/epics/architecture now align on 89 FRs.
- `FR-11.8` auto-scope is present in PRD, epics, architecture, and UX behavior.
- Business/IT mode switching now consistently starts a confirmed new chat session.
- System CRUD now allows SystemOwners, matching `FR-2.1`.
- Role naming now uses `Administrator`, `SystemOwner`, and `User` consistently across reviewed planning docs.
- Blazor WASM references in architecture were replaced with Blazor interactive server wording.
- Story 5.1 was split into four implementation-ready storage/retrieval/provenance slices.
- Epic 7 and Epic 8 placeholders were replaced with implementation-ready stories.
- `FR-6.x` completion boundaries now distinguish Epic 5 infrastructure from Epic 7 final resolution.

### Recommended Next Step

Run `[SP] Sprint Planning` with `bmad-sprint-planning`.

### Final Note

This reassessment found **no blocking readiness issues** for Sprint Planning. The only remaining caveat is the intentional Epic 10 deferral, which should be made explicit in the sprint plan.

**Assessment date:** 2026-07-10
**Assessor:** Codex using BMAD implementation readiness workflow
