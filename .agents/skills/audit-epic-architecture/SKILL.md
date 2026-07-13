---
name: audit-epic-architecture
description: Run the read-only architecture-discovery and report portion of improve-codebase-architecture for a pinned BMad epic-close gate. Use when close-bmad-epic needs independently classified, source-verifiable architecture findings without modifying code, context, or ADRs.
---

# Audit Epic Architecture

Adapt the installed `$improve-codebase-architecture` Explore/report process for a transaction-safe epic gate. Do not modify that upstream skill.

## Authorities and Boundary

Read `$improve-codebase-architecture` completely, including its language/report references. Also read the pinned epic/stories, `AGENTS.md`, Constitution, `CONTEXT-MAP.md` and selected contexts, ADRs, Ubiquitous Language, and the epic-audit contracts in `docs/agent-workflow/artifact-contracts.md`.

Operate only on the clean detached audit checkout and source identity supplied by `$close-bmad-epic`. Remain read-only in every filesystem except the root-owned HTML file in OS temp. Do not select a candidate, enter the grilling loop, update context/ADRs, implement, disposition findings, change lifecycle state, stage, or commit.

## Process

1. Execute the upstream skill's Explore process using a fresh direct-child `epic-architecture-auditor` as the independent Explore/classification judge. Preserve `agents.max_depth = 1`.
2. Bind the dispatch to the canonical audit root, Git common/worktree identities, full source commit, tree/index IDs, and hashes of every authoritative input.
3. Require `ACCEPT|REVISE|BLOCKED` plus every candidate with stable raw ID, strength, severity, all applicable categories, MUST/ADR relationships, and Git-verifiable byte-range citations.
4. Return the complete tool envelope unchanged to the root before normalization.
5. Have the root losslessly normalize the returned candidates and execute the upstream HTML-report process. Generate visible cards from the normalized payload, write/open only a fresh OS-temp report, and retain its absolute path/hash.
6. Return candidate selection and the upstream grilling loop as explicit follow-up. `$close-bmad-epic` either records category-valid dispositions or routes selected exploration and every side effect through BMad corrective work, then reruns this audit at the new commit.

## Completion Contract

Complete only when checkout/input proofs, raw response, candidate classifications, citations, normalized payload, visible report, hashes, and source identity satisfy the epic architecture evidence contract. A clean independent `ACCEPT` may advance without duplicate user approval. Reserved findings remain human-gated. Never weaken, merge, omit, or self-disposition a candidate.
