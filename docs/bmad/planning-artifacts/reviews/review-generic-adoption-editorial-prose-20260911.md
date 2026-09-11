# Generic adoption editorial prose review

Date: 2026-09-11. Lens: `bmad-review` Editorial Prose, dependent on the completed [structure review](review-generic-adoption-editorial-structure-20260911.md). Content class: docs. Audience: human decision-makers and agent builders. Style: Microsoft Writing Style Guide. No length target.

These documents exist to help readers identify authoritative product requirements, preserved platform foundations and delivery boundaries without confusing adopted design with implementation or qualification.

## Scope and calibration

- [Canonical PRD](../prds/prd-vulgata-2026-06-12/prd.md): **5,265 words**; SHA-256 `AC4C5C5BAAA677AF2D63901CC33AB2A652CD8F94C4E781256E9AC0C94DD2DB25`.
- [Architecture gateway](../architecture.md): **1,901 words**; SHA-256 `2BBACCA8DEBB46F043AA05320C43B99783E98B5530152F66DC0CA3D19ACCCDFC`.
- Combined baseline: **7,166 words**, measured with the skill's `word_metrics.py`. The PRD's affected section, §3.11, contains **439 words**.

The current snapshots include the coordinator's structured-JSON canonical wiki-content clarification, which postdates the structure review. That clarification and its distinction from Chat completion remain intact. The inherited structure models are Strategic/Context (Pyramid) plus a Reference/Database register for the PRD, and Explanation (Conceptual) plus reference tables for the gateway. No passages were marked CUT, MERGE or MOVE.

Preserve the direct normative voice, stable identifiers, canonical technical terms, compact reference style and locally attached safeguards. Repeated authority, history and qualification limits serve different reader decisions; they are not stylistic duplication. Code blocks, frontmatter and structural markup were excluded from copy-editing. Customization resolved successfully using the established OS Temp UV cache after the default cache was inaccessible; the configured `project-context.md` pattern matched no file.

## Findings

| Pass | Original Text | Revised Text | Changes |
| --- | --- | --- | --- |
| prose | PRD §3.11, FR-11.7: “Preserve useful authorized interrupted text with its reason, not normal completed factual history.” | “Preserve useful authorized interrupted text with its reason, not as normal completed factual history.” | Add “as” to make the intended classification explicit. Without it, the second phrase can be read as excluding normal completed history from preservation. No completion, interruption or history rule changes. |

No prose changes are recommended for the architecture gateway. No further minor fixes are withheld.

## Summary

**1 suggested prose correction; no source edits applied.** Accepting it would add **1 word (+0.01% of the combined baseline)**, with no reduction or comprehension trade-off. No length target applies. The structure pass's preservation recommendations remain unchanged. All requirements, statuses, qualifiers and design/implementation/qualification boundaries remain sacrosanct; this review supplies no implementation or runtime qualification.
