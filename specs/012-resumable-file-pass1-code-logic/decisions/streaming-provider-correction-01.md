# Story 12.3 Streaming Provider Correction 01

- Decision ID: `story-12-3-streaming-provider-correction-01`
- Status: Approved
- Decided on: 2026-08-24
- Trigger: the real DeepSeek Worker request can exceed the existing fixed whole-response timeout while model reasoning is still progressing, and the production Worker path currently waits for a non-streamed response.

## Approved correction

The user explicitly approved this bounded Story 12.3 compatibility correction:

1. Execute the existing Worker Agent request with `RunStreamingAsync` and aggregate the updates into the same final `AgentResponse` before the existing strict `code-logic/file-v2` validation and commit path.
2. Apply a five-minute inactivity timeout that is reset by every received streaming update, including reasoning updates.
3. Apply a 25-minute absolute Worker-execution cap, leaving a five-minute margin beneath the existing 30-minute job lease.
4. Remove the fixed whole-response `HttpClient` timeout while retaining zero SDK retries.
5. Do not log or persist streamed chunks, reasoning, tool arguments, partial output, or raw provider responses.
6. Map inactivity and absolute-cap expiry to the existing provider-unavailable/transient failure path. Preserve caller cancellation as cancellation.
7. Prove the behavior through the public `IPreparedPass1JobExecutor.ExecuteAsync` seam with fake SSE before any live qualification.

## Unchanged authority

This correction does not change Worker input or output schemas, the `navigate_code` tool contract, concurrency, provider retry/probe policy, ordinary-failure policy, lease duration, Scan strategy, Source Anchor validation, Document immutability, persistence boundaries, or source-disclosure authorization. It does not weaken strict result validation.

The implementation remains test-first. After local focused and surrounding verification, qualification proceeds with three bounded live probes and then a fresh disposable-database T062 run. Convergence, quality review, conditional E2E, final verification, and checkpoint evidence are invalidated and must be rerun before Story 12.3 can complete.

## Decision

Approved by the user in this task after the exact streaming, inactivity, absolute-cap, privacy, failure-mapping, and test-seam design was presented.
