# Story 12.3 DeepSeek Strict-Result Planned-Change Authorization 03

Decision ID: `story-12-3-planned-change-authorization-03`

Status: accepted

Decided at: `2026-08-24T04:28:47Z`

Accepted scope SHA-256: `e484af55b04ba17b6234dc368c93025e203e0b8dc3d1b07b89dcb5eb7606a97c`

Scope construction: SHA-256 of the UTF-8, LF-joined, ordinal path-sorted
`<repository-relative-path><TAB><lowercase-file-sha256>` manifest for the
accepted DeepSeek strict-result change proposal, current Story 12.3
specification, corrected plan/research/data-model/quickstart set, authoritative
`code-logic/file-v2` schema, runtime/navigation/strict-submission contracts,
and independent `plan-review-06` verdict.

Independent plan review: `ACCEPT`, attempt 6,
`reviews/plan-review/plan-review-06.json`.

## Decision requested

Authorize progression from the independently accepted corrected Story 12.3
plan to regenerated dependency-ordered tasks. This approval is limited to the
scope hash above. It does not authorize production implementation and waives no
later definition, TDD, convergence, review, E2E, security, migration,
provider, host-runtime, verification, checkpoint, or commit gate.

## Triggers, effects, and implementation intent

| Trigger | Effect and implementation intent | Affected plan material | PRD clauses |
| --- | --- | --- | --- |
| Worker Agent tool and result protocol | Add `submit_code_logic` so the Worker receives exactly two closed provider-strict functions. Keep `navigate_code` as the sole evidence-access function; make `submit_code_logic` execution-local, non-evidence, and incapable of durable mutation. Ignore free-form final JSON. | AD-15/16; `deepseek-strict-result-submission.md`; result capture/parser/session seams | FR-4.3, FR-4.4, FR-4.9, FR-4.12; FR-11.6; NFR-2.3 |
| Provider-facing schema projection | Link the one authoritative `code-logic/file-v2` schema as an embedded resource and derive one deterministic DeepSeek-supported strict subset. Pin the two local-only conditional `anyOf` omissions by exact pointer, node kind, ordered branches, semantic identity, and subtree hash. Keep complete local validation as sole acceptance/commit authority. | AD-15; strict-submission projection/drift contract; schema test slice | FR-4.3, FR-4.6, FR-4.7, FR-4.10; FR-11.7 |
| Streaming thinking-mode execution | Preserve complete `reasoning_content`, content, tool calls, and results for later function-carrying requests. Use five-minute resetting inactivity and a nonresetting 25-minute absolute cap, zero SDK retries, ten Vulgata service turns, and no request eleven. | AD-16; execution/session and fake-wire seams; runtime/quickstart qualification | NFR-3.1 through NFR-3.3; NFR-2.3 |
| Same-execution correction | Permit one initial and at most one corrected `submit_code_logic` invocation within the ten-turn execution. Return only a closed rejection category after the first invalid candidate; keep this separate from the one durable Worker retry and unchanged navigation budget. | Submission state table; parser typed-failure mapping; turn-limit tests | FR-4.2, FR-4.3; NFR-3.1 through NFR-3.3 |
| Provider capability and source disclosure | Run fake-wire proof, then one fixed source-free exact-hash beta capability handshake before any source-bearing DeepSeek qualification. Do not rewrite endpoint/model, reuse QR-002 durable provider health, weaken the schema, or remediate environment/configuration/network/provider failures. | Qualification order; strict-submission handshake; quickstart source-free gate | NFR-2.3; NFR-3.1 through NFR-3.3 |

## Preserved boundary

- The complete local `code-logic/file-v2` schema, binding, evidence, semantic,
  authorization, and atomic commit validation remains authoritative.
- No database entity, table, column, migration, durable submission ledger, new
  persistence authority, or user-facing workflow is added by this correction.
- Story 12.1 lease/replay/recovery and Story 12.2 manifest/order/hash/readiness/
  classification authority remain unchanged.
- Story 12.4 Cross-Repository Resolution, Pass 2, Business Logic Documents,
  Story 12.5 Chat/final UI/evaluation/release, and CodeGraphContext remain
  excluded.
- Provider source/context remains bounded to the approved two repositories and
  current job; raw source, prompts, reasoning, chunks, arguments, responses,
  credentials, host paths, and validator details remain nonlogged/nonpersisted.

## Remaining gates

Task generation and self-review; fresh combined definition review; separate
implementation approval for the accepted definition hashes; public-seam TDD
RED/GREEN; convergence; fresh quality review; required/covered E2E decision;
PostgreSQL zero-model-delta/atomicity, privacy/security, fake-wire, source-free
provider, isolated 3/3 source-job, full T062, and real-host qualification;
pinned final verification; and root-owned checkpoint/state/commit binding.

## Decision

The user explicitly approved the exact accepted scope on 2026-08-24. The root
may regenerate dependency-ordered tasks. This approval does not authorize
production implementation and remains valid only while the accepted scope
SHA-256 is unchanged.
