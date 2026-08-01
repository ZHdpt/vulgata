# Planned-Change Authorization: Story 11.2

**Date**: 2026-07-31 | **Story**: 11-2-sequential-scan-runner-and-typed-agent-dispatch |
**Execution level**: expedited-mvp | **Plan-review verdict**: ACCEPT at `2384511`

## Authorization

User authorized proceeding to task generation for **both** planned-change
triggers identified in
[plan.md §Plan Review and Planned-Change Authorization](../plan.md):

1. **Scan strategy — dispatch behavior.** The Sequential Scan Runner is a
   new Scan execution strategy with deterministic sequential dispatch. It
   introduces no concurrency, lease, replay, failover, retry, or
   multi-Repository scheduling.
2. **Worker Agent tool / agent execution adapter.** The typed
   `WorkerAgentJobPacket`, `WorkerAgentResultEnvelope`,
   `WorkerAgentResultPayload`, `WorkerAgentJobKind`, and
   `IWorkerAgentDispatcher` port are a new Worker Agent tool / agent
   execution adapter contract. Story 11.2 ships only a fake/test dispatcher;
   no LLM call is executed.

## Scope of authorization

This authorization is approval to proceed from the accepted plan to task
generation only. It does **not** waive any later TDD, review, E2E, security,
migration, or verification gate. It does not authorize product-scope
expansion, ADR changes, or unrelated cleanup.

## Orchestrator implementation intent

Proceed to `speckit-tasks`, then dispatch fresh `tdd-implementer` groups
G1–G6 in order (contracts → start → dispatch → restart → failure →
migration), then the combined expedited `quality-review`, then conditional
E2E (persistence + migration), then verify (Web build + full non-final-image
suite), then complete. Integration-owner-gated paths (EF migrations,
`VulgataDbContext.cs`, `Program.cs`, `sprint-status.yaml`) are coordinated
before mutation.

## Authorities

- `AGENTS.md` Planned-change authorization section
- `docs/agent-workflow/august-15-expedited-mvp-exception.md`
- `specs/005-sequential-scan-runner/plan.md`
- `specs/005-sequential-scan-runner/reviews/plan-review/plan-review-20e6c83.json`
