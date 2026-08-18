# Contract: SystemOwner Manual Resume API

## Endpoint

`POST /api/scan-executions/{scanExecutionId}/resume`

Authorization is evaluated in two ordered layers. Authentication/policy returns 401 for an unauthenticated caller and 403 only for an authenticated caller who fails `ManagementAccess`. After policy success, the endpoint queries Scan Execution through the caller's System/Repository ownership filter; no matching visible row returns 404 whether the ID is nonexistent or belongs to another owner's scope, and the endpoint performs no unfiltered existence probe. The route ID must be non-empty. No caller-supplied phase, retry schedule, lease, path, revision, provider, executable, or runtime value is accepted.

## Success response

`200 OK`, JSON:

```json
{
  "scanExecutionId": "00000000-0000-0000-0000-000000000000",
  "phase": "Prepared",
  "pendingJobCount": 0,
  "runningJobCount": 0,
  "retryScheduledJobCount": 0,
  "completedJobCount": 0,
  "failedJobCount": 0,
  "cancelledJobCount": 0,
  "recoveredExpiredLeaseCount": 0,
  "releasedDueRetryCount": 0,
  "stateChanged": false,
  "reasonCode": null
}
```

Counts are bounded non-negative integers. `stateChanged=false` is the normal idempotent replay. The response exposes no absolute path, prompt/source content, provider secret, raw runtime diagnostic, lease token, or internal database identity beyond the requested Scan Execution.

## ProblemDetails mapping

| Status | Type/code | Meaning |
| --- | --- | --- |
| 400 | `invalid-scan-execution-id` | malformed/empty route identity |
| 401 | standard API unauthorized | no authenticated principal; no cookie redirect |
| 403 | `scan-resume-forbidden` | authenticated principal fails `ManagementAccess`; ownership lookup has not run |
| 404 | `scan-execution-not-found` | policy-authorized principal's ownership-filtered lookup finds no execution, including nonexistent and other-System IDs; no unfiltered existence probe |
| 409 | `scan-resume-conflict` | state changed after revalidation; retry from fresh state |
| 422 | `scan-resume-integrity-rejected` | revision/hash/prompt/schema/runtime or durable-link mismatch |
| 503 | `scan-provider-unavailable` or `analysis-runtime-not-ready` | bounded correctable readiness failure |

ProblemDetails `detail` is Chinese for the V1 UI/API consumer and sanitized. Extensions may include only bounded `reasonCode` and `retryAt` when already persisted; no raw exception, stack, stderr, host path, source, or provider payload.

Required behavioral tests prove: unauthenticated requests return 401 without cookie redirect; authenticated callers who fail `ManagementAccess` return 403 without ownership lookup; policy-authorized callers receive the same 404 body for a random nonexistent ID and an existing execution outside their owned scope; and an authorized owner reaches recovery. No test may distinguish hidden-resource existence through status, body, timing-dependent secondary lookup, or extensions.

## Behavioral boundary

The endpoint performs revalidation and durable eligibility transitions only. It does not dispatch a Worker, calculate retry/backoff, initialize CodeGraph/JDT-LS against a Repository, generate a manifest/Document, or implement the Story 12.5 progress UI. Later dispatch observes `Prepared`/`Pending` state through the durable store.
