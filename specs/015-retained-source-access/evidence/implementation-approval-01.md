# Story 13.1 implementation approval

Decision ID: 13-1/implementation-approval/1
Status: pending user response
Requested at: 2026-09-14T11:02:49.940843Z
Scope SHA-256: `a68d54ba9b28a4d1dd6058c3a010a273f8f8b5e8bf23ad9f6949a145ba00dd50`
Accepted definition: [definition-review-boundary-05.json](definition-review-boundary-05.json)
Independent verdict: [definition-review-05.json](definition-review-05.json) — ACCEPT, no findings.
Tasks: [tasks.md](../tasks.md), 43 tasks T001-T043.

## Exact decision

Authorize implementation and scoped verification of the accepted Story 13.1 definition only. The earlier approval authorizes progression to tasks, not production writes. The root recommends proceeding; the alternative is to request a specific definition change and repeat affected gates.

## Implementation intent and effects

Implement the accepted source-only capability: Repository/full-commit identity, raw committed source capture, verified durable blob/manifest storage, one immutable SourceSnapshots record, atomic publication/retry/cleanup, mandatory live authorization for System and standalone Repositories, relative bounded reads with server-owned cumulative limits/deadlines, and an Infrastructure-only trusted workspace provider. Add only the source-owned configuration/migration/composition and the already-disclosed sanitized Chinese 409 Repository-deletion conflict. Preserve legacy pair consumers and historical meaning.

The accepted plan and application/data contracts disclose all architecture choices and internal seams; this decision binds their hashes together with the task list. It authorizes no other story and does not qualify a complete Scan, Knowledge, Chat, CodeGraph/LSP, provider, full Docker analysis stack, backup or restore.

## Approved test seams if accepted

- Public identity/value contracts and source acquisition, including configuration, exact bytes, failure/cancellation and live authority.
- SourceSnapshot persistence/publication and actual PostgreSQL migration/constraints, plus existing Repository deletion responses.
- Ordinary grant/open/list/metadata/range/text operations, shared budgets/deadlines, containment and disclosure.
- Trusted Infrastructure workspace acquisition/materialization/refusal/cleanup, without a Core or Agent path bypass.
- Production-composed separate-process reopening with the original process and remote unavailable, and source-only dependency/evidence checks.

Tests use the existing single Vulgata.Tests project. Root will establish exact owned local Git/source fixture directories, process identities and disposable non-production PostgreSQL resources before runtime work. Shared or production database mutations, unrelated process cleanup and real provider/tool calls are outside this request.

## Delivery discipline and remaining gates

Use configured Luna/high test author, Sol/medium production implementer, honest RED/minimal GREEN and independent test-author verification per coherent group; root alone owns state and explicit-path local checkpoints. Follow with Luna/high convergence, fresh Sol/medium quality review, the E2E predicate decision and configured Luna/high runner when required, then focused tests, Web build, full tests, security/migration checks and pinned verification. A passing definition is not implementation or runtime evidence.

Controlling rule: .agents/skills/run-bmad-sprint-loop/SKILL.md, Define: after the complete definition is independently accepted, pause once at implementation-approval before the first production write. Constitution IV and AGENTS preserve that gate. Earlier plan authorization is recorded in plan-authorization-01.md and remains accepted without expansion.

## Response

No implementation approval has been received or inferred. Record the user's exact response before any production write.
