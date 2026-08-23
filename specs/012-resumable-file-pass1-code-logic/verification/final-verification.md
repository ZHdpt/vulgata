# Story 12.3 final verification

## T062 harness correction and current blocker

Observed at `2026-08-23T02:44:46Z` on branch
`demo0831/story-12-3`. Independent quality-review attempt 4 accepted the
test-only correction with no findings. The authenticated Pass 1 API lane now
passes 11/11; no production path changed.

The real approved-pair qualification has not called an LLM. It remains blocked
until Story 12.2 prepares and approves manifests for the detached, clean source
worktrees below and the DeepSeek provider inputs are supplied:

- `G:/AIplayground/vulgata/sample/mbs_account` at
  `19c60cd3f3976b7697c487c7bafd96142e3a7625`
- `G:/AIplayground/vulgata/sample/mbs_channel` at
  `47f6b627a7598f862873610413eec1e1e7bf46bf`

`VULGATA_T062_WORKER_PROVIDER_MODEL` is required and is passed to the existing
OpenAI-compatible provider model seam. No synthetic `t062-worker-provider`
model identifier is used. `VULGATA_T062_MANIFEST_ONE_ID` is positionally bound
to account/root one, and `VULGATA_T062_MANIFEST_TWO_ID` to channel/root two;
each manifest retains its own Repository and Profiled Revision identity.

## T069 correction requalification

Observed at `2026-08-22T20:04:00Z` on branch
`demo0831/story-12-3`, with production pinned to full HEAD
`60d3911fa4d63a345d4eab12beba5cb55f14868d`.

Quality-review attempt 2 corrections QR2-001 and QR2-002 are locally qualified:

| Boundary | Result |
|---|---|
| reconciliation, provider, lease, shutdown, recovery, and PostgreSQL atomic focus | 134 passed, 0 failed, 1 authenticated real-provider skip |
| `Pass1MigrationTests` migration/no-delta | 7 passed, 0 failed, 0 skipped; no migration path changed |
| `Pass1ProductionBoundaryQualificationTests` | 1 passed, 0 failed; refreshed artifact verdict `pass` |
| navigate-code security/exclusion and production composition | 33 passed, 0 failed, 0 skipped |
| Web build | succeeded, 0 errors, 9 warnings |

The normal drained path now uses `ReconcileAsync` as the sole atomic terminal
transaction and reaches `ReadyForResolution`; the PostgreSQL boundary tests still
prove complete rollback of Pass 1 outcome, warning inventory, deterministic
same-Repository REFERENCES Edges, child states, coordination, and approved
handoff. Provider-transient work atomically relinquishes the exact claim without
retry/failure budget use, active or future-eligible work is nonterminal and waits
on activation or `TimeProvider`, and shutdown retains pair-owned revision
bindings until local Workers drain. The System Owner and approved-manifest
defense-in-depth gate remains enforced.

The refreshed production-boundary inspection is
`artifacts/qualification/story-12.3/production-boundary-inspection.json`. It
contains no forbidden literal and reports every composition check true. The
Story 12.2 qualification artifact remains unrelated and is not Story 12.3
evidence.

Conditional host E2E remains unavailable rather than failed or replaced. None
of `VULGATA_T046_CODEGRAPH_COMMAND`, `VULGATA_T046_JAVA_EXECUTABLE`,
`VULGATA_T046_JDT_LS_ROOT` / `VULGATA_T046_JDT_LS_ARCHIVE`,
`VULGATA_T062_APPROVED_PAIR_REAL`, or `VULGATA_T062_SOURCE_BINDING_READY` is
present. Therefore this correction does not claim a new real CodeGraph/JDT-LS
or authenticated approved-pair E2E run. The prior full-suite repository and
environment baseline classification below is unchanged and is not relabeled as
Story 12.3 success.

The post-quality conditional E2E decision is recorded in `e2e-decision.json`
as **required / blocked**. Its focused PostgreSQL and lifecycle boundary passed
95/95, and provider-outage composition passed 9/9 with one intentional
approved-pair skip. The authenticated API lane passed 2/11; nine tests stopped
at the existing login-form extractor before product assertions and are
classified as test-harness failures. Host qualification passed one, failed one
for the missing T046 CodeGraph command, and skipped two unavailable approved-pair
paths. No coverage override is claimed. Story 12.3 implementation and quality
review are accepted, but lifecycle completion remains blocked until the named
T046/T062 prerequisites are supplied and the required E2E gate passes.

## T064 qualification record

Observed at `2026-08-22T18:24:08.2257694Z` on branch
`demo0831/story-12-3`, pinned to full HEAD
`7a0e6a3c29c08148796f27b395097592939cb6bf`.

## Result

T064 qualification is **complete with explicit environment and repository-baseline
limitations**. The focused Story 12.3 migration, production-boundary,
security/exclusion, and build checks passed. The available environment did not
contain the pinned T046 host-tool paths or the approved real-pair inputs, so this
record does not claim real CodeGraph/JDT-LS or authenticated prepared-pair E2E.
The unfiltered suite was run, but it is not reported as passing: its completed
sandbox run contained environment/baseline failures and its unsandboxed retry was
interrupted before final totals. No unexpected Story 12.3 production failure was
observed.

## Qualification evidence

| Boundary | Result |
|---|---|
| `Pass1MigrationTests` migration/no-delta | 7 passed, 0 failed, 0 skipped |
| `Pass1ProductionBoundaryQualificationTests` after bounded harness correction | 1 passed, 0 failed; artifact verdict `pass` |
| security/exclusion/provider-focused checks | 33 passed, 0 failed, 1 real-provider skip |
| `Pass1HostRuntimeQualificationTests` | 1 passed, 1 missing-prerequisite failure, 2 real-pair skips |
| Web build | succeeded, 0 errors, 9 warnings |
| sandbox full suite | 1875 passed, 163 failed, 5 skipped; 2043 total; 4m33s |
| unsandboxed full-suite retry | interrupted after the last observed progress at 3m10s; no final totals |

The production-boundary artifact records no forbidden literals, a closed prompt,
all composition checks true, and verdict `pass`. The earlier false
`ReconcileImplementation` result was a test-harness argument-order defect in
`CountOrdinal(value, literal)`; the bounded correction is included in the pinned
HEAD and root reran the exact qualification class 1/1 GREEN.

## Runtime and E2E limitations

The PostgreSQL topology check passed, preserving PostgreSQL 17 as the only
required Docker service. These prerequisites were absent from the qualification
process:

- `VULGATA_T046_CODEGRAPH_COMMAND`
- `VULGATA_T046_JAVA_EXECUTABLE`
- `VULGATA_T046_JDT_LS_ROOT` / `VULGATA_T046_JDT_LS_ARCHIVE`
- `VULGATA_T062_APPROVED_PAIR_REAL`
- `VULGATA_T062_SOURCE_BINDING_READY`

Accordingly, the exact host-run CodeGraph `@colbymchenry/codegraph@1.5.0`, Java
21, and JDT LS `jdt-language-server-1.61.0-202608141332` stdio integration was
not re-executed here. The two authenticated real-pair tests and the provider
outage test were honestly skipped rather than replaced with synthetic evidence.

## Full-suite classification

The completed sandbox run's 163 failures were dominated by restricted Windows
Event Log, Data Protection AppData, ACL, and JDT workspace access. It also
contained known missing-input/baseline lanes: the separate disposable
`VULGATA_TEST_POSTGRES` database, a legacy provider image, absent host-runtime
paths, real-pair prerequisites, and the subsequently corrected boundary harness.

The unsandboxed retry did not complete. Before interruption, its observed
non-Story-12.3 failures included two legacy `QualificationPreflightTests`
`plan_revision` assertions, the existing Story 4.3 fail-closed
runtime-unavailable mismatch, and the repository-management `最近扫描时间` UI-text
assertion. At 3m10s it reported the expected real provider-outage and approved-pair
tests skipped. These results are evidence of execution, not a full-suite PASS.

## Scope and security conclusion

Focused checks found no `CodeGraphContext`, raw LSP/CodeGraph storage, shell,
network, unrestricted filesystem access, startup sweep, background resume, Story
12.4 Cross-Repository Resolution/Pass 2/Business Logic Document behavior, or Story
12.5 Chat/release behavior. No EF model or migration delta was introduced by the
Story 12.3 correction lane.

T064 is qualification evidence only. Root retains sole authority for quality
review, E2E disposition, lifecycle state, staging, and commit.
