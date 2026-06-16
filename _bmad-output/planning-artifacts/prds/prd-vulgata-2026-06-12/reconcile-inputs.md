# Input Reconciliation: PRD vs. Source Documents

**PRD:** [prd.md](file:///g:/source/repos/vulgata/_bmad-output/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md)
**Date:** 2026-06-16

---

## 1. Product Brief (`brief-vulgata-2026-06-11/brief.md`)

### Coverage Summary

The PRD captures the brief's executive summary, problem statement, target users, solution architecture, success criteria, and scope almost completely. The Vision (Section 1), Why Now (Section 2), Target User (Section 4), Features (Section 5), Success Metrics (Section 7), and Out of Scope (Section 10) all trace directly to the brief.

### Gaps

| # | Gap | Severity | Detail |
|---|-----|----------|--------|
| B1 | **Shared databases as communication pattern** | Medium | The brief's RPC Detection Layer description lists "shared databases" alongside SOFA RPC, HTTP, and message queues as a cross-system communication mechanism. The PRD explicitly excludes "Database shared access as a cross-system communication pattern" (Out of Scope, line 319) and FR-5.1 through FR-5.5 do not list it. This is a deliberate scope reduction but is not explained — the PRD should note *why* shared-database detection was dropped. |
| B2 | **Magentic orchestration validation spike not referenced** | Low | The brief's scope section calls out a "Magentic orchestration validation spike (Week 1)" as an explicit in-scope activity. The PRD does not mention this spike, the fallback architecture plan, or the binary-gate nature of this decision. The PRFAQ treats this as a critical risk; the PRD is silent. |

### Other Observations

- The brief's "What Makes This Different" differentiators (cross-system by design, LLM-native, knowledge for everyone, lightweight, LLM-wiki) are all reflected in the PRD's Vision, Glossary, and Feature sections.
- The brief's near/medium/long-term vision is condensed into the PRD's Vision section (Section 1, final paragraph). No material loss.

---

## 2. PRFAQ (`prfaq-vulgata.md`)

### Coverage Summary

The PRD absorbs the PRFAQ's press release narrative into the Vision section and maps the customer FAQ personas into JTBDs and User Journeys. The three-state uncertainty model, RPC Detection Layer, and LLM-wiki approach are all captured. The PRFAQ's "Verdict" section (what survived, what keeps us up at night) maps partially to the PRD's Open Questions and Assumptions Index.

### Gaps

| # | Gap | Severity | Detail |
|---|-----|----------|--------|
| P1 | **Cross-verification between agents not captured** | Medium | The PRFAQ's Internal FAQ states: "documents are generated with cross-verification between agents where possible" as a verification strategy. The PRD does not include any cross-verification mechanism. The Out of Scope section explicitly excludes "Audition Agent (independent document verification)," but cross-verification between peer worker agents is a distinct, lighter-weight concept. |
| P2 | **Global background knowledge injection not captured** | Medium | The PRFAQ's Internal FAQ proposes injecting "global-level background knowledge about the organization's common RPC frameworks into all agent prompts" as a mitigation when per-system Communication Pattern Catalogs are incomplete. The PRD's FR-5.1 only describes per-system catalogs; there is no FR for global/organization-level prompt injection. |
| P3 | **Magentic spike risk absent from Open Questions** | Low | The PRFAQ's "What Keeps Us Up at Night" and Internal FAQ both treat the Magentic spike as a binary gate — "the single highest-risk decision in the first week." The PRD's Open Questions (Section 9) do not include this risk. OQ-1 through OQ-6 cover demo repos, communication mechanisms, document readability, fallback LLM, scan duration, and pattern discovery — but not the orchestration framework risk. |
| P4 | **Hallucination risk not in Open Questions** | Low | The PRFAQ explicitly calls out "Hallucination is systemic" as a key concern with mitigations (source links, cross-verification, HITL). The PRD does not list hallucination/document-correctness as an open question, despite the PRFAQ treating it as a top-tier risk. |
| P5 | **Demo structure not included** | Low | The PRFAQ defines a three-act demo structure (Problem → Experience → Under the Hood) with timing and judge-appeal strategy. The PRD does not include presentation guidance. This may be intentional (PRD is product spec, not demo script), but the PRFAQ treated it as a key output. |

### Other Observations

- The PRFAQ's Customer FAQ answers about audit artifacts, data sensitivity, and self-hosted deployment are reflected in the PRD's Security NFRs (NFR-2.1 through NFR-2.4).
- The PRFAQ's discussion of "dangling links are acceptable" is captured in FR-6.4 and the counter-metrics in Section 7.
- The PRFAQ's fallback LLM provider question maps to OQ-4.

---

## 3. Requirement Draft (`docs/requirement-draft.md`)

### Coverage Summary

The PRD captures all core features from the draft: web interface with auth, dashboard, system/repo management, scanning pipeline, chat interface, git monitoring, database tools, MCP customization, and human-in-the-loop. The draft's scanning architecture (orchestrator + workers, code units → documents, tree structure, uncertainty handling) is fully represented.

### Gaps

| # | Gap | Severity | Detail |
|---|-----|----------|--------|
| D1 | **Document search and filter capability missing** | Medium | The draft states: "User can also search for specific documents by keywords, and filter documents by type, system, repository, or other metadata." The PRD has no document search/browse feature. The chat interface uses LLM-wiki retrieval (FR-11.6) — the agent reads index.md and selects documents — but there is no user-facing keyword search or metadata filter for browsing generated documents outside of chat. |
| D2 | **Document version comparison missing** | Low | The draft states: "User can view the update history of each document, and can compare different versions of the document to see what has changed." The PRD's FR-9.4 preserves document history ("previous versions of updated Documents shall be archived and accessible") but does not include a version comparison/diff capability. |
| D3 | **Uncertainty agents concept replaced** | Low | The draft proposes "a group of special agents are responsible for handling these uncertainties, which can be called 'uncertainty agents.'" The PRD replaces this with system-level uncertainty resolution (FR-6.1 through FR-6.4) without dedicated agents. This is a deliberate architectural simplification — the PRD's approach is arguably cleaner — but the draft's concept of specialized agents for uncertainty is not addressed or explained. |
| D4 | **"Code-linked" vs "non-code-linked" document classification dropped** | Low | The draft distinguishes between "code-linked documents" (directly linked to code units) and "non-code-linked documents" (linked to other documents, e.g., architecture overviews). The PRD simplifies to two orthogonal classifications: Code Logic Document vs. Business Logic Document, with all documents linked to code units. The draft's non-code-linked document concept (synthesized documents built atop code-linked ones) is not present in the PRD. |

### Other Observations

- The draft's "all human submitted information should mark as 'human input'" is captured in FR-7.3.
- The draft's deadlock avoidance during cross-system scanning is captured in FR-6.3.
- The draft's "only process the changed code units" for incremental scan is captured in FR-9.3.
- The draft's database tools are captured in FR-10.1 through FR-10.3.
- The draft's MCP customization levels are captured in FR-13.5.

---

## 4. Cross-Source Patterns

### Items present in multiple sources but missing from PRD

| Pattern | Sources | Notes |
|---------|---------|-------|
| Agent cross-verification | PRFAQ (P1) | Mentioned in both PRFAQ Internal FAQ and as verification strategy |
| Magentic spike / orchestration risk | Brief (B2), PRFAQ (P3) | Both sources treat this as a critical early decision |

### Items present in one source, contradicted in PRD

| Item | Source | PRD Position |
|------|--------|--------------|
| Shared databases as communication pattern | Brief | Explicitly out of scope |

### Items in PRD not traceable to any source (scope additions)

| Item | PRD Reference | Notes |
|------|---------------|-------|
| LSP Support | FR-15.1–15.3 | New in PRD; marked "V1 only if time permits" |
| LLM Provider Management | FR-3.1–3.4 | Implied by self-hosted LLM discussions but formalized as a feature for the first time |
| User-Supplied Context application rules | FR-14.4 | Draft mentions supplementary info; PRD adds the rule that context changes queue during active scans |
| Prompt Workbench | FR-5.11 | Brief mentions it; PRD formalizes it as an FR |
| Service Topology MCP tool | FR-5.13 | PRFAQ mentions it; PRD formalizes it |

---

## 5. Summary

The PRD is **substantially complete** against all three source documents. The core vision, user journeys, feature set, success metrics, and out-of-scope boundaries are faithfully represented. The gaps identified are primarily:

1. **Omissions of risk/process items** (Magentic spike, hallucination risk, cross-verification) — these are more about project management than product requirements, but the PRFAQ treated them as critical.
2. **One deliberate scope contradiction** (shared databases as communication pattern) that should be explained.
3. **Two missing user-facing capabilities** from the requirement draft (document search/filter, version comparison).
4. **One architectural mechanism** from the PRFAQ not captured (global background knowledge injection).

None of the gaps are blockers for proceeding to architecture and epic creation. The most actionable items are B1 (explain the shared-database exclusion), D1 (decide whether document search/filter is needed), and P2 (decide whether global prompt injection is needed).
