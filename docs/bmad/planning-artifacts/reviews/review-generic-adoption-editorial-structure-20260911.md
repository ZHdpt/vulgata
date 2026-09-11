# Generic adoption editorial structure review

Date: 2026-09-11. Lens: `bmad-review` Editorial Structure only, after the coordinator's reported PRD/architecture rubric floors passed. Content class: docs. Reader type: humans. Style: Microsoft Writing Style Guide. No length target.

These documents exist to help the user and future builders understand authoritative product requirements, preserved platform foundations and delivery boundaries without confusing adopted design with implementation or qualification.

## Scope and method

- [Canonical PRD](../prds/prd-vulgata-2026-06-12/prd.md): **5,240 words**; SHA-256 `466CF34CBAAAF7442392999C99CBBEBFD27E547DF1F84DCF604B2A2C7AD23D11`.
- [Architecture gateway](../architecture.md): **1,859 words**; SHA-256 `2F40212D9FF4F90593A6FC9D478E1237E6630064E466F2369CA62CF5B1FC98DC`.
- Combined baseline: **7,099 words**. Counts are exact outputs from the skill's `word_metrics.py`, run through `uv` with `UV_CACHE_DIR` set to OS Temp's `vulgata-architecture-uv-cache`. Section counts below use that same metric.
- PRD model: **Strategic/Context (Pyramid)**, with a **Reference/Database** requirement register. Architecture gateway model: **Explanation (Conceptual)**, with reference tables for authority and project boundaries.
- Read the full skill, structure lens, editorial common ground and structure models; resolved customization. The configured `{project-root}/**/project-context.md` pattern matched no file, so no additional persistent-facts file was loaded. Supplied repository instructions and review constraints remained in force.

This is organization/expression review, not a second product, architecture or runtime assessment. The immutable AD spine was outside scope. No source document, decision, requirement, status or guard was edited. A read-only identifier count confirmed 89 FR and 15 NFR entries.

## Findings

No structural change is recommended. The following explicit preservation findings explain why apparent opportunities to shorten these documents should not become edits.

| Pass | Original Text | Revised Text | Changes |
| --- | --- | --- | --- |
| structure | PRD §0, “Purpose, authority and scope” (230 words); gateway “Authority and adoption status” (312 words) | **PRESERVE** the opening adoption boundary, status legend and subject-based authority routing. | Both documents front-load their conclusion and reading contract. Repeated warnings distinguish product obligation, implementation permission and qualification; removing them would collapse different boundaries. Word impact: **0 words**. |
| structure | PRD §3.4, “Scan and knowledge production” (806 words), and §3.11, “Chat and knowledge consumption” (439 words) | **PRESERVE** the stable-ID register and locally attached qualifiers. | These are the densest sections, but their clauses state distinct acceptance, lifecycle, access and evidence limits. Existing requirement bullets provide random access; extracting safeguards into a separate generic caveat would weaken the reader's ability to understand an individual requirement. No high-value cut or merge was identified. Word impact: **0 words**. |
| structure | PRD §7, “Historical scope and supersession” (205 words); gateway “Historical architecture and evidence” (263 words) | **PRESERVE** the compact historical routing near the end. | These sections distinguish exact historical meaning from current authority instead of reproducing archived documents. They support safe navigation for future builders and do not delay the main requirements or platform guidance. Word impact: **0 words**. |

## Reduction and handoff

**0 change recommendations; 3 PRESERVE findings.** Estimated reduction if accepted: **0 words (0.00% of 7,099)**. No length target applies. No reader scaffolding, traceability, safeguard or historical distinction is sacrificed for brevity.

The dependent prose lens can review the documents unchanged. Keep all 104 requirement identifiers, Current/Deferred/Original unmet backlog/History dispositions, qualifiers, adoption status and historical evidence boundaries intact. This review does not finalize adoption or authorize implementation.
