# Story 12.3 T064 qualification verification

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
