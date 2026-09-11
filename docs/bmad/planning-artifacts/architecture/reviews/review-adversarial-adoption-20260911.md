# Architecture adoption adversarial review — 2026-09-11

## Verdict

No material findings in this bounded independent compliance-pair pass. No tested pair of independently implemented modules can produce the identified conflict while also obeying the complete spine, its shared ownership table, and the gateway's pre-split contract gate.

This supports architecture document adoption only. It does not finalize the wider adoption review, validate the in-progress PRD reconciliation, authorize implementation, or demonstrate runtime readiness.

## Reviewed boundary and method

Read the complete repository adoption draft spine and gateway. Verified their SHA-256 values:

| Artifact | SHA-256 |
| --- | --- |
| `architecture/ARCHITECTURE-SPINE.md` | `13504EB83C7822B996E278113DFA31D5A70D07C2407035ADDBA3645BCDEB5A8B` |
| `architecture.md` | `2F40212D9FF4F90593A6FC9D478E1237E6630064E466F2369CA62CF5B1FC98DC` |

Applied the configured architecture adversarial lens: construct two modules one level below the architecture that obey every applicable decision yet conflict over a shared contract. The finalized AD-1–AD-32 decisions and explicit deferrals were held fixed. A failure scenario already prohibited by an AD is a future implementation/test obligation, not a new architecture finding. Likewise, independently inventing schemas is not compliant where one owning contract must be accepted before dependent builders split.

## Attempted conflicts and existing guards

| Module pair / attempted conflict | Why the conflicting pair is not compliant |
| --- | --- |
| Scan Control / Knowledge: a retry, lost acknowledgement or cancelled attempt accepts duplicate or late semantic results. | AD-2/8/14/18 and the processing contract bind Infrastructure to one composite fenced acceptance transaction, idempotent replay, request-owned cancellation and conditional publication. Accepted history remains distinct from unpublished request-owned work. |
| Resolver / Knowledge: a narrower discovery scope or access loss overwrites broader accepted relationships, or fresh resolution silently retargets historical Business Logic provenance. | AD-9/13/17/19/20 give Knowledge accepted-evidence and selection authority, pin matching inputs, prohibit scope-only negative conclusions and preserve publication-specific relationship history and exact dependencies. Resolver and investigation Workers only propose. |
| Flow Planner / publisher: an older completed synthesis displaces a newer desired flow, or publication creates unbounded reconciliation feedback. | AD-10/11/13/18/23 require fixed active inputs, distinct Logical Flow identity, coalesced refresh demand, current desired-target/head checks and per-flow atomic visibility. Document publication cannot start a resolution epoch. |
| Source Snapshot / Tool Runtime / Workers: an independently prepared tool reads another revision, leaks physical paths or cleanup removes pinned source. | AD-3/4/6/15/20/21/30/31 bind grants and tools to application-selected immutable snapshots, separate canonical source from disposable workspaces, constrain LSP sessions and preserve pinned source. A model-selected workspace or source-only fallback after required CodeGraph failure is expressly disallowed. |
| Knowledge / Chat / reader UI: a stable turn mixes changing discovery results, or revocation exposes already-generated composite prose through history, references or a graph. | AD-13/24/25/26/29 require one version-bound discovery/content/relationship read context, live authorization over all contributors and server-side mode/scope enforcement. Stability freezes knowledge selection, not permission. |
| Chat / Web / persistence: reconnect regenerates an answer, loses attachment-time updates or marks interrupted output completed. | AD-26/27/28 distinguish conversation/turn/context identity, host-owned bounded execution, idempotent durable completion and lossless/nonduplicating same-turn reattachment. Host failure is interruption, not browser detachment or transparent generation recovery. |
| Infrastructure restore / Scan Control / Chat: restored running rows dispatch paid work or old leases remain valid. | AD-31 and the operational seed require coherent source/database/key/configuration recovery, disabled provider dispatch during qualification, durable completion reconciliation, invalidated pre-restore leases and no automatic saved-running-state dispatch. Scan work becomes recoverable/paused; unfinished Chat becomes interrupted. |

## Deliberate exclusions and remaining delivery obligations

Canonical selectors, schema versions, lifecycle fields, identity/lineage details, storage/index mechanics, limits, permits, retention thresholds and fixtures remain required owning-feature work. AD-7/23/25, the shared ownership table, the delivery sequence and the gateway explicitly forbid splitting dependent implementation before those common contracts are accepted. Their absence as executable schemas at this adoption stage is therefore not evidence of two permitted incompatible implementations.

No new service, upgrade, runtime claim, waiver, product feature or deferral is recommended. The existing verification obligations still require real future evidence, especially concurrency, source/tool binding, publication/history/access changes, same-generation Chat reattachment and isolated restore. This review ran no build, tests, provider calls, database operations, services or Git commands.

## Findings

`[]`
