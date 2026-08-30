# Planned-change decision: Prepared Pass 1 identity authority

Decision ID: `story-12-5-planned-change-prepared-pass1-identity-authority-02`

Status: **accepted**

Approved decision SHA-256: `7a9e3fc5fc4b9485cd7538e61f2b2153a81bcc8388a89935750d5a29dbf931b2`

Decided at: `2026-08-30T05:59:15.4510419Z`

## User authorization

The user explicitly approved this decision and authorized return to plan to
introduce the narrow Agents-owned Prepared Pass 1 prompt/schema identity
authority, preserve `WorkerAgentDispatchIdentitySource` unchanged, run
independent plan review, regenerate affected tasks and run independent
definition review. This authorization does not permit production
implementation; a newly bound implementation approval remains mandatory.

## Trigger

T026 reached the production Prepared Scan initiation coordinator and proved a
feasibility mismatch in the accepted plan. The plan assigns current Pass 1
prompt and Document schema identity to `IScanRecoveryDispatchIdentitySource`.
Its sole production implementation, `WorkerAgentDispatchIdentitySource`, is an
accepted legacy recovery authority: it hashes the v1 Worker prompt and returns
`vulgata.knowledge-document/1.0`. Prepared Pass 1 instead executes the v2
prompt resource and requires `code-logic/file-v2`.

The coordinator correctly fails closed with HTTP 503 before authorization
recheck or persistence. PostgreSQL observation confirms zero initiation rows.
Changing the legacy source would alter accepted durable-recovery identity
behavior. Hardcoding a replacement in the coordinator would create a second,
drift-prone authority. Neither is acceptable under the current plan.

This triggers the AGENTS.md planned-change gate because it changes the planned
Scan identity-composition strategy after plan review.

## Recommended bounded correction

Return to the plan owner and define one narrow, server-owned Prepared Pass 1
contract-identity authority. It will expose only the current Prepared Pass 1
prompt identity and exact `code-logic/file-v2` Document schema identity. The
Agents implementation will own the v2 prompt/resource binding; Web initiation
will consume the narrow Core port. It is not an Agent tool and has no source,
provider, dispatch, retry, scheduling, parsing, persistence, or public DTO
surface.

Expected production boundary:

- add a narrow Core Prepared Pass 1 identity-source contract;
- add its sole Agents implementation beside the existing v2 prompt owner and
  register it through existing Agents composition;
- replace only the initiation coordinator's legacy recovery-identity
  dependency with the new source;
- retain `WorkerAgentDispatchIdentitySource` and every accepted recovery test
  unchanged;
- extend public/composition tests to prove the v2 / `code-logic/file-v2`
  binding and fail closed on missing or mismatched authority;
- rerun plan review, tasks, independent definition review and implementation
  approval binding before the first production write for this correction.

## Rejected alternatives

1. **Change `WorkerAgentDispatchIdentitySource`** — rejected because it changes
   accepted v1 recovery identity semantics and existing recovery tests.
2. **Hardcode `pass1-code-logic-worker.v2` and `code-logic/file-v2` in the Web
   coordinator** — rejected because it duplicates the Agents-owned prompt
   authority and permits resource/identity drift.
3. **Accept the generic knowledge schema** — rejected because it weakens the
   accepted Prepared Pass 1 `code-logic/file-v2` binding.

## Preserved boundaries

- no EF migration, `VulgataDbContext` shape or model-snapshot change;
- no Story 12.4 contract or accepted Track A/Track B behavior change;
- no CGC or historical profiling registration;
- no legacy recovery source mutation;
- no new Agent tool, Worker behavior, provider call, retry or dispatch path;
- no general, single-Repository or arbitrary-N Scan lifecycle;
- no Worker, Pass 1/2, Document, Edge, Uncertainty, Chat or LLM execution during
  initiation/preparation;
- no retained run-02 database contact and no run-03 release work.

## Implementation intent after approval

Correct the earliest affected plan/design artifacts, run an independent
plan-only review, regenerate the affected T025-T033 tasks, run independent
definition review, obtain a newly bound implementation approval, then resume
TDD from an identity-authority RED. Existing T011-T026 evidence and production
work remain reusable only where the corrected definition leaves them intact.

## Requested decision

Approve the recommended narrow Prepared Pass 1 identity authority and authorize
return to plan/spec-task correction. This decision does not itself authorize
the resulting production write; the regenerated definition must pass the
ordinary review and implementation-approval gates.
