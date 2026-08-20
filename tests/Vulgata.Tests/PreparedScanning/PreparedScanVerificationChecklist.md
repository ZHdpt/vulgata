# Story 12.2 Final Verification Checklist

This record is the T052 single final verification barrier for the accepted
Story 12.2 boundary. Commands ran serially on the Windows host. PostgreSQL
17.10 was the only Docker service started for this barrier; CodeGraph 1.5.0,
Java 21, JDT LS 1.61.0, and Vulgata ran on the host.

## Prerequisite evidence

- [x] PreparedScanning suite: 101 passed, 0 failed, 1 justified SQLite-only
  skip, 102 total after convergence correction. The Windows Event Log provider was disabled for the test
  process because the sandbox cannot write the system log; no product logger
  or test assertion changed.
- [x] Prepared Scan audit contract: 2/2 passed.
- [x] Canonical envelope plus atomic replay: 13/13 passed.
- [x] Real exact-Git, CodeGraph CLI, JDT-LS, and prepared analysis quickstart
  filter: 13/13 passed in 14 seconds with host tool/network access.
- [x] Retained JDT-LS implementation navigation correction: unchanged RED was
  empty implementation evidence; the adapter now retries only empty
  `textDocument/implementation` responses within the existing request timeout.
  Three fresh host repetitions passed, and JDT/prepared-real surroundings
  passed 10/10. Other navigation remains single-shot.
- [x] PostgreSQL migration lifecycle: 2/2 passed in 3 seconds.
- [x] PostgreSQL store atomicity/replay/concurrency: 21/21 passed in 15
  seconds; discovered count is 21 and no test was skipped.
- [x] Prepared Scan API/authorization: 5/5 passed.
- [x] Paired release and convergence qualification: 7/7 passed in 72 seconds.
  Reversed Repository/binding/candidate input, SC-001 identity equality,
  bounded Java/XML splitting, classifications, related-file expansion/fallback,
  complete inventory audits, and PreScan/CodeGraph/JDT-LS timing assertions all
  passed.
- [x] Boundary plus safe XML security: 7/7 passed. The repository-wide literal
  search reports pre-existing later-story Worker, Document, Backup B, and
  CodeGraphContext text outside the Story 12.2 prepared boundary; executable
  T048/T051 inspection found zero forbidden matches in 28 prepared production
  files.
- [x] Production-literal qualification: 1/1 passed. The regenerated artifact
  inspected 6 production assemblies, 9 embedded resources, and 1 public output,
  with 0 matches and verdict `zero-match`. Artifact SHA-256:
  `ddecfbe6da9744cec962d1f051ee550beb91fdbe12516ee46029ef078a885d79`.
- [x] Workflow/security record SHA-256:
  `0b9b88f642468b79034853329a1b063576243c8477b58365392bc814da0c629d`.

## Physically final barrier

- [x] Web project build completed with 0 errors and 9 warnings. Warnings are
  existing NU1507 package-source and MSB3277 EF assembly-version conflicts.
- [x] Complete test project completed: 1,629 passed, 6 failed, 1 skipped,
  1,636 total in 2 minutes 46 seconds. All Story 12.2 tests passed. The six
  failures are outside this story boundary and are preserved for review:
  Story 4.3 runtime-unavailable composition; stale sequential-scan migration
  check-list equality; two CodeGraph qualification fixtures missing
  `plan_revision`; absent legacy `VULGATA_PROVIDER_CONTRACT_IMAGE`; and a
  management-page assertion expecting `最近扫描时间`. The one skip is the
  intentional SQLite exclusion for the PostgreSQL-only identical-writer
  regression, which passed in the mandatory 21/21 PostgreSQL suite.
- [x] `git diff --check` passed on the complete candidate boundary; only
  existing LF-to-CRLF notices were emitted.
- [x] Disposable PostgreSQL databases and the final-barrier container were removed;
  the existing project PostgreSQL service was not contacted or changed.

No Story 12.3 Worker execution, Document generation, final Edges,
Uncertainties, Cross-Repository Resolution, Pass 2, Chat, or LLM behavior is
part of this checklist.
