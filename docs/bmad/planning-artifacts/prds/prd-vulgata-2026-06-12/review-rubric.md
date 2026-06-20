# PRD Quality Review — PRD: Vulgata

## Overall verdict

This is a strong PRD. The thesis — closing the gap between business understanding and code reality through LLM-powered cross-system analysis — is clear and drives every section. The glossary is comprehensive (30+ terms), scope is honestly delimited with explicit deferrals and an Out of Scope section, and the document is well-structured for downstream architecture and story creation. The main risk is that LLM-generated output quality (document readability, business logic identification accuracy) is inherently fuzzy and the PRD acknowledges rather than resolves this — OQ-3 is doing real work here. The PRD is ready for architecture. **Verdict: Pass.**

## Decision-readiness — strong

The PRD makes clear, honest decisions. The competition context (9-week timeline, rubric dimensions) is stated upfront in §2 and drives prioritization throughout. Features are explicitly tiered: core (Scanning Pipeline, Cross-System Detection, Chat), deferrable (Git Monitoring §5.9, MCP Integration §5.13), and optional (LSP Support §5.15). Counter-metrics in §7 name what is *not* being optimized (scan duration, token cost, dangling link count). Open Questions (§9) are genuinely open — OQ-3 ("Will Business Logic Documents actually be readable by a non-technical reviewer?") is a real risk, not a rhetorical question.

The one gap: no `[NOTE FOR PM]` callouts exist at genuine tensions (e.g., scan duration vs. demo deadline, LLM output quality vs. non-technical readability). The PRD uses Open Questions and Assumptions to surface these instead, which is adequate but less direct.

### Findings

- **[low]** Missing `[NOTE FOR PM]` callouts (§2, §5.4, §7) — Several genuine tensions exist (scan duration acceptability, LLM output quality thresholds, demo fallback strategy) but are surfaced only through Open Questions rather than direct PM-facing callouts. *Fix:* Add 2–3 `[NOTE FOR PM]` markers at the highest-risk tensions, particularly around OQ-3 (document readability) and OQ-4 (LLM provider fallback).

## Substance over theater — strong

No theater detected. The Vision (§1) is product-specific — it names banks, cross-system risk evaluation, and the "six or more systems" problem. It could not swap into another PRD. The UJs (§4.2) have four named protagonists (Lin, Chen, Wang, Zhang) each driving a distinct product surface: business-mode chat, new-hire exploration, technical trace, and system onboarding. No persona exists solely for decoration. NFRs (§6) have specific thresholds: 30-second chat response (NFR-1.2), 5-second graph update (NFR-1.3), bcrypt/Argon2 (NFR-2.1), encrypted-at-rest keys (NFR-2.2). The "Why Now" section (§2) makes a concrete economic argument (LLM costs <1 CNY/million tokens) rather than generic market trends.

## Strategic coherence — strong

The thesis is explicit: LLM-powered agents can extract and link business logic across system boundaries, making it accessible to non-technical users. Every feature section serves this thesis:
- §5.4 (Scanning Pipeline) — the extraction engine
- §5.5 (Cross-System Communication Detection) — the cross-system differentiator
- §5.6 (Uncertainty Resolution) — handles the inherent incompleteness
- §5.11 (Chat Interface) — the consumption surface

Success Metrics (§7) validate the thesis: SM-1 tests cross-system tracing, SM-2 tests non-technical readability, SM-3 tests cross-system accuracy, SM-4 tests zero-training usability. Counter-metrics prevent metric gaming. The scope kind is clearly "problem-solving MVP for a competition demo" — the scope logic matches.

## Done-ness clarity — adequate

Most FRs have testable mechanical consequences: register/login (FR-1.1–1.2), CRUD operations (FR-2.1–2.4), git URL validation (FR-2.5), LLM provider configuration (FR-3.1–3.4), index/log generation (FR-4.7–4.8), document immutability (FR-4.10), dashboard status displays (FR-12.1–12.5). NFRs use bounds, not adjectives: 30 seconds (NFR-1.2), 5 seconds (NFR-1.3), specific algorithms (NFR-2.1).

The gap is LLM-generated output quality. FR-4.3 ("identify the business logic, and produce a structured Document"), FR-4.4–4.5 (Code Logic vs. Business Logic Documents), and FR-5.2–5.6 (communication detection) describe *what* agents should produce but not *how good* the output must be. The PRD acknowledges this honestly through OQ-3, but downstream story creation will need acceptance criteria for document quality that don't exist yet. This is inherent to LLM-based products and not a PRD failure — but it's the dimension where implementation risk is highest.

### Findings

- **[medium]** LLM output quality criteria are undefined (§5.4, §5.5) — FR-4.3 through FR-4.5 and FR-5.2 through FR-5.8 describe agent outputs but lack quality thresholds (accuracy rate, readability standard, false-positive tolerance). OQ-3 acknowledges this but doesn't propose a resolution path. *Fix:* Add a note in §9 linking OQ-3 to a planned benchmarking phase during development, or define minimum acceptable quality gates (e.g., "Business Logic Documents must be rated understandable by at least 2 of 3 non-technical reviewers on a sample of 5 documents").

- **[medium]** Superstep batching is underspecified (§5.4, FR-4.2) — "Each superstep fans out a batch of code units to workers, then fans in results before dispatching the next batch." Batch size, dependency model between batches, and fan-in semantics are deferred to architecture but have significant implications for scan duration and resource usage. *Fix:* Add an Open Question about batching strategy or note it as a deferred architecture decision.

## Scope honesty — strong

Omissions are explicit and well-organized. The Out of Scope section (§10) lists 12 items including specific exclusions (gRPC detection, production database access, webhook-based git detection). Deferrable features are marked inline: §5.9 ("Deferrable — may be dropped if time is tight"), §5.13 ("Lowest priority — may be deferred"), §5.15 ("V1 only if time permits"). Six `[ASSUMPTION]` tags are indexed in §8 with full roundtrip verification (see Mechanical notes). Counter-metrics in §7 name what's acceptable to leave unoptimized. The open-items density (6 OQs + 6 assumptions) is appropriate for a competition-demo PRD.

## Downstream usability — strong

The PRD is explicitly written to feed BMad architecture, epics, and stories (§0). It delivers:
- **Glossary** (§3): 30+ terms, every domain noun defined once and used consistently. Terms like "Communication Pattern Catalog," "Uncertainty," and "LLM-wiki" carry precise meaning throughout.
- **ID continuity**: FR-1.1 through FR-15.3 (82 FRs, no gaps), UJ-1 through UJ-4, SM-1 through SM-5, NFR-1.1 through NFR-6.2, OQ-1 through OQ-6, A-1 through A-6. All contiguous.
- **Cross-references**: FR-5.13 → "Service Topology" (glossary), FR-4.9 → "CodeGraph" (glossary), FR-11.6 → "LLM-wiki" (glossary), SM-5 → "FR-9.x" (resolves correctly). All glossary-backed.
- **UJ protagonists**: All four UJs have named protagonists with roles (Lin/PM, Chen/new hire, Wang/developer, Zhang/System Owner).
- **Section independence**: Each section is self-contained via glossary terms; no "see above" dependencies.

## Shape fit — strong

This is an internal-tool competition demo with multi-role stakeholders. The PRD shape fits: four UJs with named protagonists cover the role diversity without over-formalizing. The FR density (82 FRs across 15 feature sections) is appropriate for the technical complexity. The PRD is not over-formalized (no excessive UJs, no filler sections) and not under-formalized (glossary, assumptions, open questions, and out-of-scope are all present). The chain-top nature (feeds architecture → epics → stories) is acknowledged in §0 and the document is structured accordingly.

## Mechanical notes

- **Glossary drift**: No drift detected. "System," "Repository," "Document," "Scan," "Code Unit," "System Owner," "Business Mode," "IT Mode," "LLM-wiki," "Communication Pattern Catalog," "Prompt Workbench," "Pre-Scan Profiling," "CodeGraph," "LLM Provider," "LSP," "MCP" — all used with consistent capitalization and meaning throughout. Minor: "HITL" is defined in the glossary but the body consistently uses the full form "Human-in-the-Loop" — not a drift, just an unused acronym.
- **ID continuity**: All ID ranges verified contiguous. No gaps, no duplicates, no unresolved cross-references.
- **Assumptions Index roundtrip**: All 6 inline `[ASSUMPTION]` tags appear in §8 (A-1 through A-6). All 6 index entries trace back to inline tags. Verified: A-1↔FR-1.4, A-2↔FR-2.5, A-3↔FR-5.13, A-4↔FR-9.1, A-5↔FR-10.3, A-6↔NFR-5.2.
- **UJ protagonist naming**: UJ-1 (Lin), UJ-2 (Chen), UJ-3 (Wang), UJ-4 (Zhang). All carry role context inline.
- **Required sections**: Document Purpose, Vision, Why Now, Glossary, Target User (JTBD + UJs), Features (FRs), Non-Functional Requirements, Success Metrics, Assumptions Index, Open Questions, Out of Scope — all present.
