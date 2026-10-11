# Scan integration and Business synchronization — October 11

The user authorized integration of completed Scan work and synchronization of
Business, followed by a user-operated Business handoff. This does not resume
autonomous management or authorize provider, database or host operations.

## Integrated boundary

Starting main checkpoint: `9309ecaf`. Scan source checkpoint: `c473ad15`, including
existing-CL continuation implementation `5c220d55` and its final handoff.
The merge retains Scan history and the already integrated Chat/Explorer work.
All twelve conflicted paths had main blobs identical to Scan ancestor `99ce34a2`;
their incoming final Scan versions were selected. The resulting production,
test and engineering tree differs from Scan only in the eight preserved
Chat/Explorer improvement files. No competing Business implementation was added.

The new exact-publication route loads accepted CL, admits a fresh Business attempt
with exclusive receipts, reuses validated resolution outcomes and produces a
separate BL publication. It does not Prepare, re-run CL or replace the original
publication. No schema migration is introduced. Indeterminate attempts remain
blocked; there is no automatic receipt clearing or crash takeover.

Business inspected the pinned APIs and reported no concrete compatibility blocker.
Its older missing-publication-to-corpus and original-Generate-only restrictions
are superseded for this route, not erased as historical observations.

## Verification and qualification limits

Full combined SelfContained verification selected 4,917 tests: **4,915 passed,
2 failed, zero skipped**; 6,036 discovered and build zero errors. Evidence:
`TestResults/isolation-7d1d42bbbca34259a31a625b2ff5adca/results.trx`.
This is not full-suite GREEN. One failure is the CodeGraph integrity readiness
test: the integration launcher's process-local shortened PATH omitted the
installed CodeGraph directory required by that legacy test's lookup.
The unchanged test passed **1/1** after adding `G:\packages\npm\prefix` only
to that child process PATH. Evidence:
`TestResults/isolation-8380bb7b7e7642128368f379145b0c56/results.trx`.
No persistent environment change or source fix was made. This targeted replay
does not rewrite the original full-suite result.
The known
Scan report-parser failure is retained: `OctoberGeneratedClReport` requires an
exact older console field set while the current Generator emits additional
diagnostics. This is a qualification-helper compatibility gap, not evidence of
invalid retained CL data; it still prevents claiming that helper is ready.

Scan's handoff reports a real 77-file, 163-CL-Document publication in 2,765.81
seconds. This integration does not independently query or browser-qualify that
data. Scan's owned PostgreSQL verification and focused tests remain lane evidence;
they are not new runtime executions by the integration owner.

## Business next boundary

Use `specs/20-1-Oct14-scan/handoff-business-generation.md` and the reconciled
`specs/20-1-Oct14-business/existing-cl-handoff.md`. Business owns BL continuation;
its competing extension remains held. Select exact stored roots, verify a genuine
ordinary-reader binding, and establish a fresh authorized execution configuration
before BL. Do not reuse consumed CL grants or fixture authority.

Retain the successful CL database and publication. BL must use the explicit
existing-publication route, not a fresh CL generation or replacement publication.
Keep environment-specific paths and credentials outside tracked configuration.
No paid BL call, retained database write, host startup or ordinary-user browser
qualification is performed by this integration task.
