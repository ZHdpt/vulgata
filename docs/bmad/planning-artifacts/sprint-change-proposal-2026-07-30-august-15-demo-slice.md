# Sprint Change Proposal: August 15 Demonstration Slice

**Date:** 2026-07-30
**Trigger:** The fixed competition deadline is 2026-08-15 with 34 defined
backlog stories remaining and prior Epic 4 stories consuming the available
schedule.
**Mode:** Batch
**Approval:** Approved by the user on 2026-07-30.
**Scope classification:** Major MVP delivery replan.

## 1. Issue Summary

Completing the current V1 backlog by August 15 is not feasible. Reducing peer
review alone cannot close the gap because the remaining scope includes the
Document graph store, Scan Coordinator, deterministic Orchestrator, Worker
Agents, Cross-Repository detection, dashboard, chat, and incremental scanning.

The deadline must be treated as a demonstration MVP boundary. The full V1
remains visible as post-demo backlog and is not falsely marked complete.

## 2. Impact Analysis

### Checklist results

| Item | Result |
| --- | --- |
| Trigger and evidence | Done — deadline, remaining story count, and prior delivery duration are concrete. |
| Current epic impact | Done — Story 4.3 is reduced to deterministic automatic approval. |
| Future epic impact | Done — Epics 5–10 remain post-demo backlog; a deadline overlay implements only reusable minimal seams. |
| PRD impact | Done — an explicit August 15 acceptance boundary is added without deleting full V1 requirements. |
| Architecture impact | Done — canonical component ownership remains; concurrency and advanced policies are deferred. |
| UX impact | Done — minimal Chinese Business/IT chat remains; dashboard, graph, notification center, and file upload are deferred. |
| Direct adjustment | Not sufficient — 34 stories cannot be completed by workflow tuning alone. |
| Rollback | Not viable — completed Stories 4.1 and 4.2 are required foundations. |
| MVP review | Selected — a five-story vertical slice is the smallest credible demonstration. |
| Handoff | Done — Story 4.3 then Epic 11, using the temporary `expedited-mvp` level. |

## 3. Recommended Approach

Deliver this path:

```text
two prepared Repositories
-> deterministic pre-scan approval
-> minimal knowledge store
-> sequential typed Worker Agent execution
-> Code Logic and Business Logic Documents
-> exact configured RPC/HTTP Cross-Repository relationship
-> database-backed Business/IT chat answer with source citations
```

The five Epic 11 stories correspond to storage, sequential execution, document
generation, exact RPC/HTTP matching, and chat/release verification.

## 4. Scope Changes

### Included

- Story 4.3 automatic conservative pre-scan approval.
- Minimal immutable Document/source/reference/trace/retrieval persistence.
- Sequential one-Scan-at-a-time execution and typed Worker Agent jobs.
- One Pass 1/Pass 2 path for prepared demo Repositories.
- Exact configured RPC/HTTP matching with no guessed edge.
- Minimal Chinese Business/IT chat with grounded citations.
- Clean-database E2E and one final Docker build.

### Deferred

- Pre-scan LLM questions and the approval lifecycle, preserved as Story 8.7
  with the existing Epic 8 Human-in-the-Loop stories.
- General concurrency, leases, replay, retries, and failover.
- Database tools, advanced uncertainty resolution, and third-party libraries.
- MQ, file, page-navigation, indirect object flow, Service Topology MCP, and
  Prompt Workbench.
- Full dashboard, notification center, SignalR, live graph, and graph details.
- Chat file upload, automatic scope selection, and incremental re-scan.

## 5. Quality and Workflow Adjustment

Use the dated `expedited-mvp` workflow only for Story 4.3 and Epic 11:

- concise SpecKit artifacts;
- conditional plan review and mandatory planned-change authorization whenever
  a repository trigger applies;
- one combined acceptance-and-quality review;
- no routine spec judge, analysis pass, evidence ledger, freeze, or audit;
- three planned checkpoints per story;
- one final integrated E2E and Docker build; and
- no failing committed tests.

Included authorization, security, migration, deterministic matching, build,
and final demonstration checks remain mandatory. Deferred edge cases become
documented limitations, not RED tests in the delivery branch.

## 6. Delivery Sequence

| Dates | Outcome |
| --- | --- |
| July 30–31 | Baseline correction, Story 4.3, demo inputs frozen |
| August 1–3 | Story 11.1 minimal store |
| August 4–5 | Story 11.2 sequential runner |
| August 6–8 | Story 11.3 document agents |
| August 9–10 | Story 11.4 RPC/HTTP resolution |
| August 11–13 | Story 11.5 chat and citations |
| August 14 | Clean-database E2E, Docker build, rehearsal |
| August 15 | Release-blocking correction buffer |

## 7. Success Criteria

- Story 4.3 and Stories 11.1–11.5 are `done`.
- The prepared two-Repository flow passes from profiling to a cited chat
  answer.
- Included tests, migrations, authorization checks, Web build, E2E, and final
  Docker build pass.
- Epics 5–10 remain backlog with no false completion claims.
- The `expedited-mvp` exception expires after Epic 11 or the deadline.
