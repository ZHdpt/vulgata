# Story 12.4 implementation approval 01

- Decision ID: `story-12-4-implementation-approval-01`
- Status: accepted
- Requested at: `2026-08-26T01:59:12.3396279Z`
- Decided at: `2026-08-26T02:02:53.3951345Z`
- Scope SHA-256: `7D47E4DABBEC9A1C436FF3ABA003732A8E4D1A960EE0C539170D6ECE24BFEF7D`
- Specification SHA-256: `50327089B48F6ACCFBA1267E762CD2AF29E9F33A53E249B7FB8590C06D671517`
- Plan SHA-256: `8DE291B2AFF5B793DD6413532DEC1A3D8EDD191150B24B497B6D9B8894A10B92`
- Tasks SHA-256: `32BDF28A535B67C01ECE45508EE6F3FDB849D16090F2EC27888DEEA1DBD40198`
- Accepted definition verdict SHA-256: `FC0F9BED4660B40A4D2C2683DEC1A6EFEDA07E01DB6869A12E4C1F7886D502FF`

## Decision requested

Authorize production implementation of exactly the independently accepted
Story 12.4 definition and its 49 dependency-ordered tasks. The approved scope
would implement deterministic exact Cross-Repository Resolution decisions,
reusable REFERENCES Edges, endpoint-centered Flow Groups, bounded
`retrieve_code_logic`, the strict two-function Pass 2 Worker loop, immutable
Business Logic Documents and Version Chains, all-and-only-used GENERATED_FROM
provenance, durable replay/recovery and honest partial completion, the
authenticated Pass 2 execution API, its additive compatibility migration, and
the approved two-Repository live qualification.

The implementation follows the disclosed Core-to-Infrastructure-to-Agents-to-
Web seams, including the Core `IPreparedPass2WorkerDispatcher`, exact embedded
`business-logic/flow-v1` schema, and central `MapVulgataApiEndpoints()` API
registration. It preserves ownership-derived scope, 401/403/ownership-filtered
404 behavior, ProblemDetails, bounded provider context, content-dark logs and
receipts, and the existing Story 12.1 through 12.3 authorities.

This authorization excludes Story 12.5, Chat/retrieval UI, production progress
UI, final release/evaluation evidence, general graph clustering, vector search,
unrestricted Agent tools, CodeGraphContext, direct Pass 2 JDT-LS/CodeGraph
analysis, provider/failover configuration changes, and system changes. It does
not waive TDD, convergence, quality review, conditional E2E, security,
migration, qualification, final verification, or checkpoint gates.

## Scope hash construction

The scope hash is SHA-256 over the four repository-relative path, TAB,
lowercase-file-hash rows for the specification, plan, tasks, and accepted
definition verdict, sorted by repository path and joined by LF without a
trailing newline.

## Decision

The user explicitly approved production implementation of this exact scope on
2026-08-26. No excluded scope or downstream gate was waived.
