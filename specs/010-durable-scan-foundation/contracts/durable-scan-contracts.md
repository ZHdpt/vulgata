# Contract: Durable Scan State, Commit, and Recovery

## Durable store ports

Core defines specific ports; no generic repository, `IQueryable`, `DbSet`, EF entry, transaction object, or SQL crosses the boundary.

### `IDurableScanStore`

- insert one immutable Scan Context Snapshot, returning `Created|Replay|Conflict`;
- create/read one new-path Scan Execution with canonical prompt/schema/runtime bindings;
- insert an ordered set of deterministic Scan Job Executions atomically after a later prepared manifest is accepted;
- read an immutable `ScanRecoverySnapshot` by execution ID and authorized scope;
- conditionally apply a recovery transition against every captured `StateVersion`;
- conditionally claim one Pending job with a lease token/expiry (policy supplied later by Story 12.3);
- conditionally transition Running to Retry Scheduled/Failed/Cancelled;
- read bounded progress counts without exposing persistence internals.

All commands return `Applied`, `Replay`, `Conflict`, `Rejected`, or `NotFound` with a closed bounded reason. Replays must compare canonical identity and state rather than accepting an ID collision.

### `ITwoRepositoryScanCoordinationStore`

- create/read one deterministic exactly-two coordination record;
- conditionally transition phase using expected `StateVersion`;
- verify both Repositories belong to the bound System and both ScanRun/ScanExecution/Profiled Revision tuples match;
- refuse combined success until both child executions are valid terminal successes;
- never duplicate or mutate per-file job state.

## Atomic result commit

`IScanJobResultCommitter.CommitAsync` accepts:

- authorized Repository scope;
- Scan Execution and deterministic Scan Job Execution IDs;
- expected job `StateVersion` and current `LeaseToken` for a new Running commit; a lost-response replay may carry their now-stale original values;
- exact File Code Unit, ScanRun, Profiled Revision, prompt/schema/runtime binding;
- one already schema-validated bounded Worker result and deterministic Document identities.

Infrastructure performs one ordered transaction:

1. authorize the Repository scope and load/lock the deterministic job by ID without first requiring its caller-supplied version or lease;
2. when the durable job is already `Completed`, load its one linked persisted Document Version and associated canonical Document graph, compare Scan Execution, Repository, Scan Run, File Code Unit, Profiled Revision, prompt/schema/runtime bindings, deterministic Document Version ID, stored commit fingerprint, and canonical content against the incoming result, then:
   - return `Committed` without mutation for an exact match, regardless of the cleared original lease or advanced `StateVersion`;
   - return `Conflict` without mutation for every non-identical identity, fingerprint, content, linkage, or cardinality;
3. only for a new commit, require the durable job to be `Running` with the caller's expected `StateVersion` and current `LeaseToken`; any other state/version/token returns `Conflict`;
4. verify the new commit's immutable Scan Execution, Repository, Scan Run, File Code Unit, Profiled Revision, prompt/schema/runtime bindings, deterministic Document Version ID, and canonical incoming commit fingerprint;
5. materialize and persist the immutable Document Version, Source Anchors, Edges, sections, and indexes through the shared knowledge write core;
6. set job `Completed`, its one `CommittedDocumentVersionId`, clear lease/failure/retry fields, advance `StateVersion`;
7. commit once.

Outcomes:

- `Committed`: new atomic Running commit or fully identical already-Completed replay;
- `Conflict`: stale new-commit version/lease, non-Running new-commit state, or any non-identical already-Completed replay;
- `Rejected`: authorization, identity, bounds, state, or source-currentness failure;
- `Cancelled`: caller cancellation before transaction commit;
- `Failed`: bounded infrastructure failure with transaction rollback.

No outcome may leave only one side committed.

## Recovery coordinator

`IScanRecoveryCoordinator.ResumeAsync(executionId, actor, cancellationToken)`:

1. require the API/authentication layer to return 401 for unauthenticated callers and 403 only for authenticated callers who fail `ManagementAccess`;
2. for a policy-authorized caller, load the recovery snapshot through an ownership-filtered query; return 404 for both a nonexistent execution and one outside the caller's owned System/Repository scope, without an unfiltered existence probe;
3. fail closed on cross-row contradictions, including Completed without one matching Document or non-Completed with a committed Document;
4. revalidate Repository access, exact Profiled Revision, affected File Code Unit source hashes, prompt identity, schema identity, provider availability, canonical runtime identity, and any Two-Repository binding;
5. conditionally apply against the captured versions:
   - expired Running -> Pending, preserving deterministic identity/attempt count;
   - due Retry Scheduled -> Pending only when not already terminal/exhausted by persisted state;
   - future Retry Scheduled unchanged;
   - Completed/Failed/Cancelled unchanged;
   - eligible paused execution -> Prepared;
6. return a bounded progress/result view.

Repeated invocation with unchanged data returns the same view and performs no additional transition. It never starts a background loop or computes retry windows, attempt budgets, lease durations, concurrency, ordering, or Worker dispatch.

## Revalidation failure mapping

| Failure | Execution effect | Job/Document effect |
| --- | --- | --- |
| Unauthenticated request | API 401 before store access | unchanged |
| Authenticated `ManagementAccess` policy failure | API 403 before ownership lookup | unchanged |
| Policy-authorized ownership-filtered lookup miss | API 404 for nonexistent or out-of-scope execution; no unfiltered existence probe | unchanged |
| Visible Repository access/currentness mismatch | no transition; bounded rejection | unchanged |
| Profiled Revision/file hash/prompt/schema/runtime mismatch | conditional `FailedIntegrity` or `PausedOperatorCorrection` according to closed reason | committed Documents retained; no job reopened |
| Provider unavailable | `PausedProviderUnavailable` | jobs unchanged |
| Durable linkage contradiction | `FailedIntegrity` if version-conditional write succeeds | committed valid Documents retained |
| Stale state version | `Conflict`; caller reloads | unchanged |
| Cancellation before commit | `Cancelled` result, not Scan phase | unchanged |

Diagnostics are sanitized and bounded; raw provider/process/source details are not persisted as reason codes.
