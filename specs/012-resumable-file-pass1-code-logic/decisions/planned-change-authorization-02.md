# Story 12.3 Corrected Planned-Change Authorization 02

Decision ID: `story-12-3-planned-change-authorization-02`

Status: pending explicit user authorization

Accepted scope SHA-256: `20da042c3c1ab95e062a86ccbb82c8d34e6f4fc476c69a15f1b847e46222e8d9`

Scope construction: SHA-256 of the UTF-8, LF-joined, ordinal path-sorted
`<repository-relative-path><TAB><lowercase-file-sha256>` manifest for the
current Story 12.3 specification, complete corrected plan set, and independent
`plan-review-04` verdict.

Independent plan review: `ACCEPT`, attempt 4,
`reviews/plan-review/plan-review-04.json`.

## Decision requested

Authorize progression from the independently accepted corrected Story 12.3
plan to regenerated dependency-ordered tasks. This approval is limited to the
scope hash above. It does not authorize production implementation and waives no
later definition, TDD, review, E2E, security, migration, host-runtime,
verification, checkpoint, or commit gate.

## Triggers, effects, and implementation intent

| Trigger | Effect and implementation intent | Affected plan material | PRD clauses |
| --- | --- | --- | --- |
| Material Scan dispatch and scheduling | Add AD-14: one explicitly activated, single-flight but capacity-filling Windows-host pair coordinator. It serially obtains durable claims, launches each claimed File Code Unit in an independent scope without awaiting prior Workers, fills limits 1-4, treats completion only as a wake-up, stops launches on durable closure, drains in-flight runners, and reconciles only after local drain plus fresh durable terminal exhaustion. A sequential coordinator is nonconforming. | `Planned production ownership`; AD-2/3/14; research R-11; data-model runtime projections; API Start/Resume/concurrency update; runtime Production activation/Claim/Terminal; test slices 3/10 | FR-4.2; dated NFR-1.1 scope; NFR-3.1/NFR-3.3 |
| Worker Agent execution and tool authorization | Register the closed prepared Worker executor/dispatcher/commit route and keep exactly one `navigate_code` function. Bind evidence disclosure and receipt mutation to the current pair/job version, lease token, expiry, Running state, and closed circuit. Stale/replaced/paused/terminal callers receive nondisclosing `Unavailable` with zero mutation. | AD-3/4/5/14; research R-4/6/11; `navigate-code-contract.md`; runtime claimed-packet and Worker-result sections | FR-4.3, FR-4.4, FR-4.9, FR-4.12; NFR-2.3 |
| Provider retry/probe scheduling | Replace configuration-resolution “health” with one fixed source-free active provider request driven by versioned durable tickets at exactly 10/30/60/120/300 seconds. Claims stop while open; no sixth probe, startup sweep, or automatic background resume is allowed; fifth failure persists the Chinese manual-resume pause. | AD-10/11/14; research R-10/11; runtime Provider circuit/Recovery; production outage tests | FR-4.2; NFR-3.1 through NFR-3.3 |
| Replay and immutable Document integrity | Completed replay reconstructs and compares every scalar, order, and cardinality in the persisted canonical Document graph: Source Anchors, Document Sections, Index Entry, direct REFERENCES Edges, and the pure reconciled Edge union. Missing, extra, mutated, reordered, or duplicate graph data fails without mutation. No migration is planned. | AD-6 through AD-9; research R-9; projection/canonical/reconciliation contracts; atomic commit slice | FR-4.3, FR-4.6, FR-4.7, FR-4.10, FR-11.7 |
| Production-composed analysis-tool qualification | Add authenticated disposable-PostgreSQL Start-to-terminal and outage tests through the real DI graph, with barriers proving simultaneous limits two/four, 4-to-1 lease preservation, 1-to-4 refill, deterministic release schedules, active probes, full graph replay, and host execution using pinned CodeGraph/JDT-LS/source ports. Static/runtime-version tests remain supporting evidence only. | Public-Seam slices 3/7/10/12; Verification Strategy; quickstart production/host barriers | FR-4.2, FR-4.9; NFR-2.3; dated NFR-4.1 host replacement |

## Preserved boundary

- Exactly Story 12.3; Story 12.4 Cross-Repository Resolution, endpoint grouping,
  `retrieve_code_logic`, Pass 2, and Business Logic Documents remain excluded.
- Story 12.5 Chat, evaluation, final release UI, and release run remain excluded.
- No CodeGraphContext, raw LSP/CodeGraph storage, shell, network, unrestricted
  filesystem/database Worker tool, or unrestricted Agent loop.
- Vulgata, CodeGraph `@colbymchenry/codegraph@1.5.0`, Java 21, and JDT LS
  `jdt-language-server-1.61.0-202608141332` remain on the Windows host;
  PostgreSQL 17 remains the only required Docker service.
- Story 12.1 leases/atomic replay and Story 12.2 manifest identity/order/hash/
  readiness/classification authority remain controlling. Documents stay
  immutable. No new table, column, or migration is planned.

## Options and recommendation

1. **Authorize (recommended):** regenerate tasks for exactly the accepted scope.
2. **Defer:** retain the accepted corrected plan and remain paused.
3. **Request revision:** state the required change; the scope hash becomes stale
   and planning returns to the earliest affected owner.

## Remaining gates

Task generation and self-review; fresh combined definition review; separate
implementation approval for the new accepted definition hashes; behavioral TDD
RED/GREEN; convergence; fresh quality review; required/covered E2E decision;
PostgreSQL atomicity/security/provider/real-host qualification; pinned final
verification; and a root-bound `review -> done` completion checkpoint.
