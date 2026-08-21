# Story 12.3 Implementation Notes

## Accepted upstream seams

- Story 12.1 durable authority remains `ScanExecution`, `ScanJobExecution`,
  deterministic Job Identity, versioned leases, atomic commit/replay, restart
  recovery, and the Chinese manual-resume contract. Story 12.3 adds only the
  accepted Pass 1 atoms; it does not reinterpret historical rows.
- Story 12.2 supplies exactly two approved `PreparedScanManifest` instances,
  their `PreparedScanManifestEntry` File Code Units, exact File Hash and Source
  Hash values, Profiled Revision, stable Prepared Scan Context Snapshot,
  CodeGraph/Java readiness proofs, Communication Kind Classifications, and the
  canonical Manifest Ordinal / Pair Selection Ordinal ordering.
- The approved pair is the complete dispatch inventory. Pass 1 never enumerates
  the Repository or schedules a File Code Unit through navigation.

## Runtime identity

- Vulgata, CodeGraph, Java 21, and JDT LS run on the Windows host.
- PostgreSQL 17 is the only required Docker service.
- CodeGraph is `@colbymchenry/codegraph@1.5.0`.
- JDT LS is `jdt-language-server-1.61.0-202608141332` over stdio.
- `CodeGraphContext` is forbidden.

## Story 12.3 ownership

- Consume one approved two-Repository prepared pair and create one durable job
  for each prepared File Code Unit with pair-wide concurrency 1-4 (default 2).
- Expose only the closed revision-bound `navigate_code` operations and budgets.
- Accept only authoritative `code-logic/file-v2` JSON; derive Markdown,
  Sections, Index Entries, Source Anchors, and Edge proposals server-side.
- Persist provider circuit, classified failure, retry, pause/resume, partial
  completion, warning inventory, and exact same-Repository reconciliation.

## Forbidden scope vocabulary

The implementation must not add Cross-Repository Resolution,
endpoint-centered flow grouping, `retrieve_code_logic`, Pass 2, Business Logic
Documents, Chat, final release UI, evaluation questions, release run, raw LSP,
raw CodeGraph storage/query access, shell, network, unrestricted filesystem,
database tools, or an unrestricted Agent tool loop.
