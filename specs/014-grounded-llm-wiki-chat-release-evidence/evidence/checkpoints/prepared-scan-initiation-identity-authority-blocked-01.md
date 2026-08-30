# Prepared Scan initiation identity-authority checkpoint

Disposition: **safe partial implementation; planned-change authorization required**

Base checkpoint: `fec5784c2eb206c0fc544d89528da3b4c133da00`.

## Completed evidence

- T018 tuple/handoff RED was observed against the prior seam.
- T019 exact-tuple preparation/recovery implementation is present.
- T020 compile RED identified only four tuple-less test fixture callers.
- T021 fixture callers now construct the immutable initiation tuple.
- T023 runtime authority RED was observed; the configured resolver now owns
  integrity and health delegates to it.
- T024 authorization/currentness suite is GREEN: 14/14.
- T025 application coordinator, fresh-scope authorization and configured
  runtime resolver compile and pass their focused behavior.
- T026 public vertical is an honest RED: 11 total, 2 boundary passes and 9
  authenticated-path failures, all HTTP 503 before the store.
- T027 composition is partial: endpoint/DI/health/no-legacy/tuple-only changes
  compile; T023 plus T024 is GREEN at 22/22.
- Test project and Web project build with zero errors; `git diff --check` is
  clean apart from line-ending notices.

## Proven blocker

`WorkerAgentDispatchIdentitySource` returns the legacy v1 prompt identity and
`vulgata.knowledge-document/1.0`. Prepared Scan initiation requires the current
Prepared Pass 1 v2 prompt identity and `code-logic/file-v2`. The coordinator
therefore fails closed before fresh authorization or persistence. This is not a
PostgreSQL, CodeGraph 1.5 or JDT-LS failure.

No source, lifecycle, Document, Edge, Uncertainty, Chat or LLM mutation occurs.
Fresh disposable PostgreSQL databases created by the public tests were cleaned;
the retained run-02 database was not contacted.

## Recovery

Use decision
`story-12-5-planned-change-prepared-pass1-identity-authority-02`. Do not edit
the legacy recovery identity source. After explicit approval, correct the plan
to introduce one narrow Agents-owned Prepared Pass 1 identity authority, rerun
the invalidated definition gates, and resume TDD. T028-T033 and run-03 remain
unstarted.
