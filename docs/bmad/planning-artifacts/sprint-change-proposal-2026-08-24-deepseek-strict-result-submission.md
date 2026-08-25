# Sprint Change Proposal: DeepSeek Strict Result Submission for Story 12.3

**Date:** 2026-08-24
**Scope:** Story 12.3 only — Worker Agent provider execution and T062 qualification
**Source branch / HEAD:** `demo0831/story-12-3` / `29459174348511bb813c4aa9672ccffee61d82a6`
**Status:** Approved for bounded definition correction and independent plan review; production implementation is not authorized
**Change size:** Moderate, architecture-sensitive correction within the accepted Story 12.3 boundary

## 1. Issue Summary

The streaming correction removed the original transport timeout blocker for the real DeepSeek qualification. Six authorized `deepseek-v4-flash` calls completed in 3:30–5:24 without an inactivity timeout, absolute timeout, network failure, host failure, database failure, or provider-availability failure. All six were nevertheless rejected at the strict `code-logic/file-v2` dispatcher seam. Closed diagnostics varied between property-shape and UUID violations, so the remaining failure is nondeterministic semantic-contract noncompliance rather than one stable malformed field.

The normal DeepSeek endpoint is already invoked with JSON mode. DeepSeek documents JSON mode as a valid-JSON guarantee and notes that it can occasionally return empty content; it does not guarantee conformance to an application schema. More prompt changes or a weaker parser therefore cannot provide the deterministic handoff required by Story 12.3.

DeepSeek documents strict function calling as a beta capability. It requires:

- the effective base URL `https://api.deepseek.com/beta`;
- `strict: true` on every supplied function;
- a function schema accepted by DeepSeek's supported JSON Schema subset; and
- in thinking mode, complete `reasoning_content` round-tripping on every subsequent request carrying tools.

Official provider references:

- <https://api-docs.deepseek.com/guides/json_mode>
- <https://api-docs.deepseek.com/guides/tool_calls>
- <https://api-docs.deepseek.com/guides/thinking_mode>

## 2. Change Navigation Checklist

### Trigger and evidence

- [x] The isolated real-provider gate failed 0/6 for strict result acceptance.
- [x] The requests completed beyond the old fixed timeout, proving streaming progress and the extended inactivity/absolute limits removed the transport blocker.
- [x] Closed diagnostics showed more than one semantic rejection class.
- [x] Full T062 was not started because the required isolated gate was 0/3; starting 150 jobs would spend the ordinary-failure budget without correcting the contract.
- [x] The provider offers a documented strict function capability that is not used by the current implementation.
- [x] The installed Microsoft.Extensions.AI OpenAI adapter can translate a tool strictness value from `AITool.AdditionalProperties` to OpenAI's strict function-schema flag; an outgoing-wire test is still required as the authoritative proof.

Evidence authority: `specs/012-resumable-file-pass1-code-logic/evidence/e2e/streaming-provider-correction-01.json`.

### Story and epic impact

- [x] Story 12.3 remains the owning story.
- [x] Story 12.3 keeps one Worker Agent job per File Code Unit, bounded `navigate_code`, the same durable retry/failure-budget policy, and `code-logic/file-v2` as the sole commit authority.
- [x] Story 12.4 cross-Repository resolution and Story 12.5 chat remain excluded.
- [x] No new user-facing behavior, database entity, migration, or persistence path is required.
- [x] A second closed Worker Agent function is introduced, so the repository's planned-change authorization rule is triggered.

### Artifact conflicts to correct

The current Story 12.3 definition repeatedly says the Worker has exactly one function, `navigate_code`, and accepts final JSON from ordinary assistant text. Those statements conflict with the proposed strict-submission transport and must be corrected together in:

- `specs/012-resumable-file-pass1-code-logic/spec.md`;
- `specs/012-resumable-file-pass1-code-logic/plan.md`;
- relevant Story 12.3 research, runtime-contract, quickstart, and schema-companion material; and
- later, only after authorization, the affected tasks in `tasks.md`.

The PRD does not need amendment. Its bounded current-task context, configurable provider endpoint, Worker-adapter fallback, one durable retry, and graceful provider-unavailability requirements remain controlling.

### Path evaluation

| Path | Decision | Reason |
|---|---|---|
| Keep normal JSON mode and tune the prompt | Reject | Six completed live calls produced varying strict semantic failures; JSON mode guarantees valid JSON, not the Vulgata contract. |
| Repair UUIDs, fields, or shapes server-side | Reject | This would silently change model claims, weaken binding authority, and hide provider noncompliance. |
| Weaken `code-logic/file-v2` validation | Reject | This breaks Story 12.3 determinism, provenance, and commit safety. |
| Replace the bounded loop with unrestricted Agent tools | Reject | Unrestricted tool loops are explicitly deferred and would expand privacy and control surface. |
| Add a closed strict result-submission function | Select | It uses the provider's schema-constrained path while keeping evidence access, semantic validation, persistence, and retries bounded. |

## 3. Recommended Approach

### 3.1 Preserve two distinct function roles

The Worker Agent receives exactly two server-owned functions:

1. `navigate_code` remains the sole evidence-access function. Its existing call, result, byte, result-count, and depth budgets do not change.
2. `submit_code_logic` is a non-evidence finalization function. It cannot read source, navigate code, call the network, mutate the database, or expose additional context.

No other tool is exposed. This is a closed validation transport, not an unrestricted Agent tool loop.

### 3.2 Use a provider-facing strict shape, retain one local authority

`code-logic/file-v2` remains the only authoritative document schema and full acceptance validator. It cannot be sent verbatim as a DeepSeek strict-function schema because it contains keywords outside DeepSeek's documented subset, including `oneOf`, `allOf`, and array bounds.

The implementation plan must define a deterministic provider-facing submission schema that:

- preserves the complete property tree, JSON types, required-property set, closed-object rules, enums, and supported patterns needed to transport a candidate `code-logic/file-v2` document;
- uses only the DeepSeek-supported strict-mode subset;
- marks every object property required and every object with `additionalProperties: false` as DeepSeek requires;
- replaces single-value `const` constraints with equivalent one-value enums where necessary;
- uses supported `anyOf` nullable shapes instead of unsupported `oneOf` forms where necessary;
- does not pretend provider-side strictness enforces unsupported size, range, uniqueness, cross-field, binding, anchor, receipt, or semantic rules; and
- is drift-checked against the authoritative schema by tests.

After the provider invokes `submit_code_logic`, Vulgata applies the existing full schema, size, depth, packet-binding, source-anchor, navigation-receipt, and semantic validation. Provider strictness is an early shape guarantee, never the commit authority.

### 3.3 Capture and correction behavior

- The function callback receives the candidate only in execution-local memory.
- It never trusts model-supplied server-owned identities; all current Job, Scan Execution, Repository, revision, File Code Unit, manifest, packet, prompt, schema, and runtime bindings must exactly match the current prepared packet.
- A valid candidate is canonicalized through the existing validator and captured as the only success result.
- Invalid candidates receive only a closed rejection category suitable for one bounded same-execution correction; raw source, prompts, provider content, reasoning, tool arguments, host paths, credentials, and validator internals are not returned, logged, or persisted.
- At most two `submit_code_logic` calls are allowed in one provider execution: the initial submission and one semantic correction. This does not consume or alter the one durable Worker failure retry.
- The existing ten-provider-turn ceiling, eight `navigate_code` call ceiling, five-minute streaming inactivity timeout, twenty-five-minute absolute cap, thirty-minute lease relationship, and zero SDK retries remain in force.
- A captured valid submission terminates result collection. Free-form assistant text is ignored and can never authorize persistence.
- If no valid submission is captured within the bounded execution, the existing closed Worker-result rejection path applies.

### 3.4 Provider and thinking-mode compatibility

- Provider endpoint selection remains configuration-owned; application code must not silently rewrite a configured endpoint.
- DeepSeek T062 qualification uses `https://api.deepseek.com/beta` and `deepseek-v4-flash`.
- Both supplied functions must serialize with `strict: true`, because DeepSeek requires strictness on every function in a strict-mode request.
- Every request remains streaming.
- The complete assistant message, including streamed `reasoning_content`, content, and tool calls, must be round-tripped on subsequent requests carrying tools. A missing reasoning round-trip must fail a fake-transport regression before any live source-bearing qualification.
- A source-free strict-capability handshake precedes the T062 source-bearing gate. Endpoint, network, authentication, unsupported-schema, or provider-availability failure halts qualification and is reported as environment/provider configuration; it is not repaired automatically.

## 4. Detailed Artifact Changes

### Specification

Correct the success contract so that a Worker succeeds only after one locally accepted `submit_code_logic` invocation. Distinguish `navigate_code` as the sole evidence function from `submit_code_logic` as the sole finalization function. Retain all current fail-closed binding, privacy, retry, timeout, and result-schema requirements.

### Plan and companions

The corrected plan must specify:

- the two-function boundary and the exact ownership of the finalization callback;
- provider-facing schema generation/maintenance and drift checks;
- execution-local accepted-result capture and termination;
- closed semantic-rejection feedback and the two-submission ceiling;
- beta endpoint composition without endpoint rewriting;
- reasoning-content round-trip behavior;
- source-free capability preflight for qualification;
- exact fake-wire, contract, streaming, privacy, and live qualification tests; and
- no persistence or migration delta.

### Tasks

Do not edit `tasks.md` in this correction phase. After an independent plan-only ACCEPT and explicit planned-change authorization, regenerate or append only the tasks invalidated by this correction. Existing completed work remains historical evidence and must not be relabeled as if never performed.

### PRD, epic, UX, and data

- PRD: no amendment.
- Epic 12: no story split or dependency change.
- UX: no impact.
- Database/schema: no impact and no migration.
- Deployment: DeepSeek qualification configuration changes to the beta base URL; credentials remain external and are not persisted in evidence.

## 5. Verification and Success Criteria

Before any real source-bearing retry:

1. A fake HTTP transport proves the effective request route is the configured beta endpoint and Chat Completions path.
2. The outgoing request has `stream: true` and exactly `navigate_code` plus `submit_code_logic`.
3. Both functions serialize `strict: true` and DeepSeek accepts their schemas in a source-free handshake.
4. The submission schema is closed, complete for the provider-facing property tree, compatible with DeepSeek's supported subset, and drift-checked against `code-logic/file-v2`.
5. Streaming tests prove reasoning-only updates reset inactivity but not the absolute cap.
6. Multi-turn tests prove `reasoning_content` is preserved with tool calls and that omission would surface the provider's closed 400/configuration failure.
7. Contract tests prove free-form JSON is ignored, the first invalid submission receives only a closed category, a second valid submission can succeed, a third submission is refused, and no valid submission is rejected.
8. Binding and privacy tests prove model-supplied identity drift, raw logging, raw persistence, and server-side semantic repair remain impossible.
9. Existing bounded `navigate_code`, retry, dispatcher, atomic commit, pause/resume, and failure-budget tests remain green.

Live qualification then proceeds in gates:

1. source-free strict-capability handshake;
2. three isolated representative File Code Unit jobs, all accepted;
3. full T062 qualification only after the isolated gate is 3/3;
4. existing PostgreSQL atomicity and production-boundary evidence refresh.

If a gate fails because of host configuration, endpoint configuration, authentication, network, or provider availability, stop and report immediately. Do not widen scope or attempt host/network remediation.

## 6. Risks and Mitigations

| Risk | Mitigation |
|---|---|
| DeepSeek beta behavior changes | Pin current official requirements in the plan, assert the outgoing wire contract, and fail the source-free handshake closed. |
| Provider-facing schema drifts from `code-logic/file-v2` | Generate or compare it deterministically and run explicit property/required/closed-object drift tests. |
| Strict shape is mistaken for semantic authority | Keep the full existing local validator and packet-binding checks as the only acceptance gate. |
| Result submission becomes an evidence or repair channel | Give it no source access, return only closed categories, and forbid server-side document repair. |
| Thinking-mode multi-turn requests fail with HTTP 400 | Preserve full `reasoning_content` and prove the serialized conversation at the fake transport boundary. |
| Extra function expands the loop | Retain ten total provider turns and allow at most two submissions, with `navigate_code` budgets unchanged. |
| Provider/configuration failure is mistaken for a product defect | Run the source-free capability gate first and stop/report on environment, configuration, network, authentication, or availability failures. |

## 7. Workflow and Authorization

The user's 2026-08-24 approval authorizes this bounded correct-course, Story 12.3 specification/plan correction, and independent plan-only review. It does not authorize task generation or production implementation.

This proposal triggers planned-change authorization because it adds a Worker Agent function and materially changes the Worker dispatch/result protocol. Therefore:

1. correct and self-review the Story 12.3 specification;
2. produce and self-review an architecture-sensitive plan;
3. obtain a fresh independent plan-only definition review;
4. if the verdict is ACCEPT, pause and request explicit planned-change authorization, citing this proposal and the affected PRD/spec/plan material;
5. only after that authorization, generate corrected tasks;
6. pause again before the first production write for implementation approval; and
7. retain all later TDD, convergence, review, E2E, security, and final-verification gates.

## 8. Approval Decision

**Decision:** Proceed with the bounded definition correction and independent plan review described above. Do not generate tasks or write production implementation until the later repository-required authorization gates are satisfied.
