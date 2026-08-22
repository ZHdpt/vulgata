# API Contract: Prepared Pass 1 Execution

## Start

`POST /api/systems/{systemId}/pass1-executions`

Authentication and `ManagementAccess` are evaluated before ownership filtering. The caller must own both Repositories through the SystemOwner scope.

Request:

```json
{
  "coordinationId": "00000000-0000-0000-0000-000000000000",
  "pairPackageFingerprint": "64-lowercase-hex",
  "concurrencyLimit": 2
}
```

`concurrencyLimit` may be omitted and then equals two. Only integers 1-4 are accepted. Unknown properties, empty IDs, caller-supplied manifests/jobs/paths/revisions/provider/runtime values, and malformed fingerprints are rejected.

`201 Created` for atomic new start and `200 OK` for exact replay:

```json
{
  "pass1ExecutionId": "00000000-0000-0000-0000-000000000000",
  "coordinationId": "00000000-0000-0000-0000-000000000000",
  "pairPackageFingerprint": "64-lowercase-hex",
  "phase": "Running",
  "concurrencyLimit": 2,
  "concurrencyRevision": 0,
  "stateVersion": 0,
  "dispatchedFileCount": 80,
  "ordinaryFailureBudget": 5,
  "pendingJobCount": 80,
  "runningJobCount": 0,
  "completedJobCount": 0,
  "failedJobCount": 0,
  "manualResumeAction": null
}
```

No response contains source/context/classification text, host/tool path, provider secret/raw result, lease token, or database row details.

An exact replay of Start compares `concurrencyLimit` with the immutable initial start value, not the possibly updated effective value. It returns the current status/effective limit with zero writes. A different start limit is `409`; active reconfiguration uses the endpoint below.

After the atomic transaction commits, `Created` and an exact replay whose current pair remains `Running` with a closed circuit signal the bounded host execution service. The API does not await 50-150 Worker jobs. The signal is idempotent and carries no source/provider content; one single-flight pair coordinator repeatedly fills durable capacity with independently scoped runners and owns claim-to-terminal continuation. If the process stops after commit but before signaling, the durable pair remains recoverable and the explicit Resume endpoint is the recovery path.

## Update concurrency

`PATCH /api/pass1-executions/{pass1ExecutionId}/concurrency`

Request:

```json
{
  "concurrencyLimit": 3,
  "expectedStateVersion": 7
}
```

The caller must be authenticated, satisfy `ManagementAccess`, and currently own both Repositories through SystemOwner scope. Only integer limit 1-4 and a nonnegative exact version are accepted; unknown properties are rejected. The pair must be pre-reconciliation and nonterminal. Success returns `200 OK` with the ordinary status representation, current `stateVersion`, effective limit, and `concurrencyRevision`.

The update is one pair-row-locked version transition. Existing leases remain valid. A decrease issues no new lease until the number of unexpired Running leases is below the new limit; an increase applies only after commit. Equal limit at the current version is a zero-write no-op. Exact lost-ack replay of the deterministic `(pair, expected version, previous limit, effective limit, authorized principal)` change is a zero-write `200`; stale/mismatched change is `409`. It never rewrites the immutable initial start limit or any scan/content/display identity. An eligible success wakes the same pair coordinator: 1-to-4 fills up to three new slots while the original runner may still be blocked, whereas 4-to-1 preserves all four leases and launches no replacement until durable active count falls below one.

## Status

`GET /api/pass1-executions/{pass1ExecutionId}`

Returns the same identity and canonically ordered bounded progress/warning summary. When paused, `manualResumeAction` is exactly `继续扫描` and `nextOperatorStep` is bounded Chinese text. The failed-file inventory contains only Pair Selection Ordinal, Repository ID, canonical path, segment ordinal, and machine failure code; no source or host path.

## Resume

`POST /api/pass1-executions/{pass1ExecutionId}/resume`

The body is empty. It accepts no caller-selected phase, retry/probe schedule, lease, path, revision, provider, runtime, or force flag. It performs current ownership filtering, both-child Story 12.1 revalidation, pair/manifest revalidation, and the same fixed active source-free provider request used by the probe driver; configuration resolution alone is not availability. Success returns the status representation. `Continued` and an already-eligible Running no-op signal the pair pump idempotently. A fifth-failure paused pair remains dormant until this explicit authenticated action succeeds; no background timer or host startup reactivates it.

This pair endpoint is additive. The accepted `POST /api/scan-executions/{scanExecutionId}/resume` endpoint and its 401/403/404 behavior remain unchanged for existing consumers.

## ProblemDetails

| Status | Code | Meaning |
| --- | --- | --- |
| 400 | `invalid-pass1-request` | malformed ID/fingerprint/concurrency or unknown input |
| 401 | standard API unauthorized | no authenticated principal; no cookie redirect |
| 403 | `pass1-forbidden` | authenticated principal fails ManagementAccess; no ownership lookup |
| 404 | `pass1-not-found` | policy-authorized ownership-filtered lookup finds nothing, including hidden scope |
| 409 | `pass1-conflict` | changed pair, concurrency, state version, or nonidentical replay |
| 422 | `pass1-integrity-rejected` | stale/mismatched manifests, hashes, readiness, jobs, or durable links |
| 503 | `pass1-provider-unavailable` | active probe unavailable; bounded retry eligibility may be exposed |

ProblemDetails is Chinese and sanitized. Extensions are limited to machine `reasonCode`, bounded `retryAt`, and `manualResumeAction` when already persisted. Existence timing/body must not distinguish hidden from absent scope.
