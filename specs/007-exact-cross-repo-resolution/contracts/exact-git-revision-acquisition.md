# Contract: Isolated Exact Git Revision Snapshot

## Port and non-interference boundary

Exact post-Pass-1 resolution uses a new port; it does not add an overload to or mutate `IGitWorkingCopyService`:

```text
IExactGitRevisionSnapshotService.AcquireAsync(
  repository,
  scanRunId,
  expectedRef,
  expectedCommit,
  cancellationToken)
  -> ICommittedRepositorySnapshotLease | bounded failure
```

`IGitWorkingCopyService.AcquireAsync` and its stable per-Repository Vulgata-managed directory remain byte-for-byte behaviorally unchanged. The exact service must never run `checkout`, `switch`, `reset`, `fetch`, `pull`, `clean`, remote mutation, or file writes in that ordinary directory. Therefore an exact acquisition cannot detach or strand the normal managed checkout.

## Isolated acquisition

The caller must already authorize the managed Repository. `repository.GitUrl` is the only deployment source location; the profile supplies none.

For each acquisition, the service:

1. validates `expectedRef` as one complete `refs/tags/...` ref with no whitespace, control, refspec metacharacter, traversal, or option prefix;
2. validates `expectedCommit` as exactly 40 lowercase hexadecimal characters;
3. creates a unique non-reparse operation directory beneath the Vulgata-owned `.exact-revisions/{RepositoryIdN}/` root; the directory name is runtime-only and never logged, persisted, or placed in the profile;
4. creates a private no-checkout Git repository/clone in that directory, binds `origin` from the already-authorized managed Repository, and fetches only the explicit tag ref with `GIT_TERMINAL_PROMPT=0` and `ProcessStartInfo.ArgumentList`;
5. resolves `expectedRef^{commit}` in the private repository and requires ordinal equality with `expectedCommit`;
6. materializes the commit through the existing bounded archive, entry, aggregate, traversal, reparse-point, and snapshot-identity controls into a private committed snapshot;
7. returns a lease whose `ProfiledRevision` is exactly `expectedCommit` and whose identity verification rechecks the private object plus materialized snapshot; and
8. owns both the private Git directory and snapshot until idempotent lease disposal.

No external production Repository and no ordinary managed checkout is checked out or modified. A missing/moved tag, unavailable object, similarly named ref, shallow omission, remote failure, malformed output, cancellation, archive error, identity drift, or cleanup failure returns no usable snapshot. Diagnostics are bounded Chinese categories and contain no Git URL, credential, host path, raw command, stderr, ref contents, or private operation path.

## Cleanup and concurrency

- **Success**: async lease disposal terminates any remaining child process, deletes the snapshot, deletes the exact private Git directory, then removes empty parents. Disposal is idempotent and releases only the lease's own resources.
- **Failure before lease return**: one `finally` path terminates the process tree and deletes every partial archive/snapshot/private Git path created by that operation before returning the bounded failure.
- **Cancellation**: cancellation kills the complete Git/archive process tree, awaits stdout/stderr drains without echoing them, runs the same cleanup under a short non-caller cleanup token, and rethrows/returns `Cancelled` only after the operation no longer exposes a lease.
- **Concurrent exact acquisitions**: every operation has a distinct private directory. Cancelling/disposing one cannot delete another. No shared `HEAD`, index, worktree, or ref is mutated.
- **Concurrent ordinary acquisition**: `IGitWorkingCopyService.AcquireAsync` may run before, during, or after exact acquisition because their writable directories and Git state are disjoint. Any optional in-process map is for cleanup bookkeeping only, not a shared-checkout lock.
- **Cleanup failure**: the service reports a closed cleanup failure and retains only a bounded in-memory retry marker keyed by an opaque operation token; it never returns a snapshot lease or writes a host path to durable state/logs. A subsequent bounded sweeper may retry deletion only beneath the validated `.exact-revisions` root.

## End-to-end revision equality

Before candidate extraction, every value must be ordinal-equal to the configured full commit:

```text
isolated exact-object Profiled Revision
snapshot lease Profiled Revision
ScanRun.ProfiledRevision
ScanExecution.ProfiledRevision
selected DocumentVersion.ProfiledRevision
selected SourceAnchor.ProfiledRevision
```

Any mismatch is a pre-evidence refusal: zero candidate reads, Edge rows, evidence rows, and Uncertainties.

## Required regressions

1. Prepare the ordinary managed checkout on an attached branch and prove `AcquireAsync` succeeds.
2. Acquire and dispose an older/different exact tagged commit through the isolated service.
3. Prove the ordinary checkout's path, branch attachment, `HEAD`, origin, index, and clean status were not changed by exact acquisition.
4. Run `AcquireAsync` again and prove its normal `pull --ff-only` path succeeds.
5. Repeat with ordinary acquisition concurrent with two exact leases; cancel one exact lease and dispose the other; prove only their private roots are cleaned and ordinary acquisition still succeeds.
6. Inject Git, archive, identity, cancellation, and delete failures at each boundary; prove no usable lease, source read, graph write, or cross-operation deletion occurs.

## Non-goals

- no branch tracking, merge, push, tag creation, remote mutation, or normal-checkout detachment;
- no external production Repository checkout/stage/commit;
- no general Git monitoring or incremental Scan behavior;
- no fallback to `HEAD`, similarly named ref, abbreviated commit, another remote, or a stale cached object.
