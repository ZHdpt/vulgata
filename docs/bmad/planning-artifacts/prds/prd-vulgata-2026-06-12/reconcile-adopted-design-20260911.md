---
title: "PRD input reconciliation: adopted generic design"
status: draft
date: 2026-09-11
review_boundary: product requirements against accepted AD-1 through AD-32
---

# Adopted-design input reconciliation

## Verdict and boundary

The PRD substantially represents the accepted generic Scan, Knowledge, Chat and operational product scope. Two narrow wording gaps remain: required retirement after proven source absence is stated as optional, and the truth-preserving transition of persisted pair-era file Documents and unavailable generic capabilities is not explicit. Neither calls for new product scope or architecture schema detail. The coordinating author accepted these narrow repairs for authoring after the pinned reports complete; the findings below still describe the reviewed pre-repair PRD.

This is independent input reconciliation, not a PRD rubric score, implementation review or final approval. The PRD was read in full, as were the final external spine and the approved September 11 proposal. Requirements were compared as product outcomes and constraints; the spine remains the owner of architecture HOW. The original unmet backlog is allowed and is not credited as fulfilled merely because an adjacent AD exists.

## Pinned sources and authority

| Source | Observed SHA-256 | Role |
| --- | --- | --- |
| [Canonical PRD](prd.md), 249 lines, draft updated September 11 | `305656DBC7D8DFE8F276CFC336F60730ED4122E39D36E390D3DBA8ED1009D1FA` | Subject of this comparison; line references below use this exact version. |
| [Final external spine](C:/Users/13728/Documents/VulgataSepAudtion/scan-architecture-2026-09-06/ARCHITECTURE-SPINE.md), 386 lines, final updated September 10 | `CC7E5AC92810734549607F38399720A027E3E237320ACEC30926FBF24D6FE093` | Accepted AD-1–AD-32 substance and explicit deferrals. |
| [September 11 proposal](../../sprint-change-proposal-2026-09-11-generic-product-reconciliation.md) | `C62312D92E5F32CFCCCE639D79AB7AA46C15F2223836679AAE09F842921D3BAA` | Records user approval of formal documentation/backlog adoption; its preserved approved pre-image is `EE7878FD8AF814BCFAE2D2ECA6C7305BB56BBF2618E4AD124F8CDB97432C675A`. |

The external spine's final sentence saying repository adoption remains future work describes its September 10 state. The approved September 11 proposal governs formal adoption now; that dated status difference is not a PRD contradiction. Neither source authorizes production implementation, migrations, Git operations or runtime qualification during this comparison.

## Reconciliation findings

### R-1 — Proven absence needs a required current-knowledge outcome

- **Location:** PRD FR-4.10, line 85; spine AD-19, lines 154–158.
- **Trigger:** A selected replacement snapshot and validated source evidence establish that an earlier Code Unit, communication observation or Logical Flow root no longer exists. FR-4.10 currently says proven deletion “may retire current knowledge.” AD-19 requires explicit withdrawal/retirement when this proof exists.
- **Product consequence:** The PRD permits obsolete behavior to remain eligible as current even after absence is proved, while the adopted design requires removal from current eligibility. Immutable history does not require obsolete current selection.
- **Suggested reconciliation:** State that, when validated selected replacement source proves absence, the product shall withdraw/retire obsolete current eligibility, update affected references/flow selections through bounded reconciliation as needed, and preserve history. Keep the existing prohibition on treating omission, exclusion, access loss, failure or unfinished analysis as deletion proof.
- **Why this belongs in WHAT:** Whether a reader continues to see removed behavior as current is an observable product guarantee. No retirement schema, transaction or matching algorithm needs to be copied into the PRD.

### R-2 — Legacy knowledge and unavailable capabilities need an explicit truth-preserving transition rule

- **Location:** PRD FR-4.3/4.6/4.10, lines 78/81/85, and §7, lines 238–249; spine AD-16, lines 136–140.
- **Trigger:** Existing pair-era file Documents are exposed or migrated into the generic semantic-Code-Unit experience. The PRD preserves historical delivery evidence and immutable versions, and prohibits unproven rename/split/merge identity reuse, but does not explicitly preserve the different meaning of those existing file Documents. Its broad prohibition on reintroducing fixed pairs also does not explicitly state the observable unavailable-capability behavior required by AD-16.
- **Product consequence:** A transition could present an old file-level result as newly discovered semantic-unit knowledge without new analysis while still retaining its old immutable body and history. Preserving completed story records alone does not prevent this misrepresentation to readers. Generic capabilities must also remain visibly unavailable while missing, not silently fall back to legacy pair execution.
- **Suggested reconciliation:** Add a concise transition statement: existing pair-era file Documents remain labelled historical results unless new accepted analysis establishes explicit lineage to semantic-unit Documents; they shall not be relabelled as newly analyzed generic results. Preserve their accessible history, and disclose unavailable generic capabilities without silent legacy-pair fallback. Detailed compatibility/versioning/migration mechanisms remain in AD-16 and the owning plans.
- **Why this belongs in WHAT:** The meaning and provenance claimed by displayed knowledge are product obligations, not merely migration implementation details. This does not reopen completed historical story status or require retaining legacy runtime routing.

## AD coverage check

The table identifies product-level representation, not one-to-one reproduction of every architectural field.

| Accepted decision | PRD representation | Result |
| --- | --- | --- |
| AD-1 Independent source ownership | §1–2; FR-2.4, FR-4.2, FR-12.1 | Independent one/several/standalone runs and organizational System membership retained. |
| AD-2 State ownership and composition | FR-4.2–4.4, FR-6.1–6.2, FR-11.2–11.3; §6 | Deterministic scheduling, application acceptance, Knowledge state and non-writing Chat retained; internal module/table ownership correctly stays architectural. |
| AD-3 Source Snapshot and Tool Workspace | FR-4.9, FR-9.2, FR-11.2; NFR-2.3 | Exact committed source, scoped retained access, identical-byte reacquisition and public provenance retained. |
| AD-4 Preparation and tools | FR-3.1, FR-4.1/4.9, FR-15.1–15.3 | Facts/advisory overview split, required CodeGraph and declared temporary LSP policy retained. |
| AD-5 Coverage and structured content | FR-4.1, FR-5.14; NFR-1.1 | Eligible formats, explicit supporting-source policy, unknown/malformed distinction and no silent exclusion retained. |
| AD-6 Native tools/application policy | FR-4.9, FR-13.1/13.3, FR-15.3; NFR-2.3 | Native required CodeGraph, application-selected capabilities, scope and no silent fallback retained; tool signatures remain architecture detail. |
| AD-7 File work/semantic knowledge | FR-4.3/4.6/4.10 | Per-file workstream versus Code Unit/Document identity and lineage safeguards retained. |
| AD-8 Durable progress/acceptance | FR-4.3–4.4; NFR-1.1, NFR-3.1/3.3 | Progressive large-file work, accepted-only knowledge, durable authoritative progress and truthful gaps retained. |
| AD-9 Generic communication evidence | FR-5.2–5.8/5.11–5.12, FR-6.1–6.2 | Evidence-supported roles/mechanisms/identifiers, unknowns and no confidence/name-only matches retained. |
| AD-10 Bounded reconciliation | FR-4.5, FR-6.2–6.3/6.6 | Pinned targets, late accepted external files, root completion, cycles and coalesced nonrecursive refresh retained. |
| AD-11 Flow identity/membership | FR-4.5–4.6; §2, §6 | Distinct source-backed roots, directed bounded slices and local/Cross-Repository views retained; separate-root merging deferred. |
| AD-12 Supported Pass 2 synthesis | FR-4.5; NFR-2.3 | Supported branches/effects/unknowns and exact actually-used dependencies retained. |
| AD-13 Publication/compatible reads | FR-4.7/4.10, FR-6.6, FR-9.4, FR-12.6; NFR-2.3 | Atomic per-flow visibility, exact historical provenance, relationship changes without CL rewrite, qualified mixed evidence and contributor access retained. |
| AD-14 One product workflow | FR-4.2, FR-12.1–12.4 | Single initiation, real progress, separate coverage/currentness and cancellation ownership retained. |
| AD-15 Runtime/recovery | FR-15.1–15.3; NFR-3.1–3.3, NFR-4.1 | Docker target, temporary no-import/no-restore, bounded provider retry/pause, explicit resume and no provider substitution retained. |
| AD-16 Explicit historical meaning | FR-4.3/4.6/4.10; §0, §6–7 | Broad history/qualification boundary retained; persisted legacy-Document distinction needs R-2. |
| AD-17 Evidence selection/visibility | FR-4.7, FR-6.2/6.6, FR-11.4 | Designated candidate, accepted-only inputs, labelled staged/history reads, coverage gaps and non-destructive scope narrowing retained. |
| AD-18 Ordered publication | FR-4.10; NFR-2.3 | Current-promotion order, history-only nonpromotion and live composite authority retained; transaction fields stay architectural. |
| AD-19 Withdrawal/retirement | FR-4.10; NFR-2.3 | Absence-proof and history safeguards retained; mandatory current-selection outcome needs R-1. |
| AD-20 Focused investigation | FR-5.7, FR-6.2; NFR-2.3 | Bounded source/evidence inquiry, unchanged-input deduplication, limitations and application acceptance retained. |
| AD-21 Pass 2 verification | FR-4.5/4.9, FR-15.1–15.3 | Authorized exact-source/tools, recorded additional dependencies, non-writing synthesis and no recursive repair retained. |
| AD-22 External facts deferred | FR-5.1/5.11/5.13, FR-10.2–10.3; §6 | No compulsory mappings or external-information placeholder; decisive missing facts remain Uncertainties. |
| AD-23 Bounded parallelism | FR-4.2, FR-12.2; NFR-3.1–3.2, NFR-4.1 | Fair shared limits, exclusive file ownership, no dependency-wait capacity and settlement obligations retained. |
| AD-24 Wiki-first/mode access | FR-11.1–11.2/11.6–11.8 | Useful multi-document synthesis, Business BL-only, IT exceptional source, shared wiki tools and no repair/Scan authority retained. |
| AD-25 Stable turn reads | FR-11.4/11.6–11.7 | Discovery/pagination/later-discovered content stability, fresh later turns, exact original references and live authorization retained. |
| AD-26 Durable dialogue/context | FR-11.3/11.7; NFR-2.3, NFR-3.3 | Durable conversations, bounded completed history, hypothesis/scope-preserving summaries and mode/context isolation retained. |
| AD-27 Provisional output/completion | FR-11.7; NFR-3.2–3.3 | Genuine deltas, first-text hypothesis labels, valid completion plus durable save, interrupted state and no visible-answer regeneration retained. |
| AD-28 Same-turn reconnect | FR-11.7; NFR-3.3 | Host continuation within original limits, lossless/nonduplicating same-generation reattachment and distinct host failure retained. |
| AD-29 Reader/scoped graph | §2; FR-9.4, FR-11.1–11.2/11.7, FR-12.6 | Knowledge-authorized reader/history/citations, exact source and manually refreshed version/mode/access-bound graph retained. |
| AD-30 Portable Docker | NFR-1.1, NFR-4.1; §6 | Two services, bundled tools, portable limits, durable source/data/keys and no host-profile dependence retained. |
| AD-31 Retention/backup | FR-4.9–4.10; NFR-3.3, NFR-4.1 | Conservative source retention, coherent maintenance-window backup/restore, provider-disabled qualification and explicit recovery retained. |
| AD-32 Qualified operations | FR-3.4, FR-4.8, FR-12.4; NFR-1.1–1.2, NFR-5.1; §5 | Separated readiness, sanitized trace/usage, measured representative qualification and content/citation quality retained. |

## Deferrals and distinctions confirmed

- Automatic/incremental changed-source Scans, impact/reuse, monitoring/pull/update triggers and caller-triggered partial library Scans remain deferred. Ordinary standalone explicit Scans, immutable history and bounded reconciliation of later accepted knowledge remain current.
- External operational-information tools, new database lookup tools and maintained service mappings are not smuggled into focused investigation. Preserved database administration and read-only/non-production safeguards remain distinct.
- Outward wiki MCP, attachments, product HITL and advanced/live portfolio graphs remain deferred. Native CodeGraph MCP, internal Chat wiki tools, authorized text context, operational pause/resume, visible Uncertainties, basic graphs and genuine streaming remain current.
- Multiple LSP sessions, dependency-aware import/build/restore, new origins and separate-business-root merging remain later scope. Temporary local-source operation does not assert network air-gapping.
- Prompt Workbench, dedicated unknown-library inference and general configurable MCP management/consumption are explicitly original unmet backlog, not newly deferred or implicitly implemented. The approved proposal permits these to remain outside Epics 13–17.
- Literal sanitized `log.md`, authorization defaults/auto-scope, retained administration/context, Chinese-only UI, Library Oak and the existing accessibility floor are not erased by adjacent architecture decisions.
- No need was found to copy canonical fingerprints, transaction compare fields, lease schemas, concrete tool method names, backup commands or numeric budgets into the PRD. Those belong to the accepted architecture and owning plans.

## Qualification limit

Only the pinned documents and narrow supporting instruction/memory pointers were read. No source-code assessment, repository/Git status, build, test, provider inference, browser, Docker, database, backup or restore operation was performed. No implementation readiness, runtime coverage or explanation quality is established. This draft report modifies only its own review file; the coordinating author owns any PRD amendment and final status.
