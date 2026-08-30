# Prepared Scan initiation regression verification

- Story: 12.5
- Task: T030
- Accepted scope: `f5690de4e4ca6b188fbeeab54745c12266585601583a05f9ffd81516c088ebd1`

## Results

- Track A production Scan status/resume plus Story 12.5 corpus/release contracts: `116 passed, 0 failed`. This run used normal host access because the ASP.NET Data Protection default key directory is intentionally outside the workspace sandbox.
- Track B Prepared Chat retrieval/currentness, advisory and relationship selection, evidence-packet v2.2, candidate extraction, claim/citation validation, bounded conversation, strict raw streaming, observation provenance, accepted-only publication, Business/IT UI and integration: `421 passed, 2 sandbox-only Data Protection failures`; the exact two default-path cases then passed `2/2` with normal host access. Product result: `423 passed, 0 failed`.
- Existing deterministic Prepared Scan and Story 12.2 identity/CodeGraph/JDT-LS/XML/selection/canonical-envelope suites that do not require absent external archives: `70 passed, 0 failed`.
- Corrected initiation/identity/PostgreSQL/atomic boundary: `100 passed, 1 environment-dependent skip, 0 failed`.
- Web build: succeeded with `0 errors`; existing NU1507, NU1900 and MSB3277 warnings remain.
- `git diff --check`: clean apart from line-ending informational warnings.

## Environment-separated observations

The broad exploratory filter also selected real-runtime qualification cases. They could not run because the local JDT-LS distribution archive is absent; this is an external asset prerequisite, not a regression in the corrected entry path. One unchanged Story 12.2 source-text assertion still searches `Program.cs` for the CodeGraph package literal even though current authority resides in `PreparedScanRuntimeIdentityResolver`; current resolver and production-literal tests are covered by the passing focused suites. The production-literal qualification rewrote its tracked build-hash artifact as a test side effect; that unrelated delta was restored and is excluded from this candidate.

## Scope guards

- No Web EF/`VulgataDbContext` access in the authorization rechecker.
- No migration, model snapshot, DbContext model-shape or project-file change.
- No mutation of `WorkerAgentDispatchIdentitySource`, either prompt, Agent tools, provider/dispatcher, retry or scheduling behavior.
- No CGC registration, legacy profiling coordinator restoration, `/scan-runs` repair, general Scan redesign, single-Repository/arbitrary-N entrance, Story 12.4 contract mutation, or later Worker/Pass 1/Pass 2/Document/Edge/Uncertainty/Chat/LLM action.
- Track A and Track B production behavior is unchanged and fully regressed.

Verdict: T030 passes for the accepted candidate. External real-runtime assets remain reserved for the separately unauthorized T034-T045/run-03 boundary.

## QR-PSI-001 correction regression

After the non-public Agents resource-provider seam and hostile tests were
added, the invalidated surrounding verification was rerun:

- corrected T029 boundary: `111 passed, 1 environment-dependent skip, 0 failed`;
- Track A Scan status/resume and release contracts: `116/116`;
- complete Track B prepared-chat boundary with normal Data Protection access:
  `423/423`;
- Web build: zero errors with existing warnings only;
- `git diff --check`: clean apart from line-ending information.

No Track A/B production file, public identity contract, prompt, dispatcher,
provider, Agent tool, migration/model, Story 12.4 contract, CGC/legacy path or
run-03 external boundary changed.

## QR-PSI-002 correction regression

After validation was moved from source construction to `GetCurrent`, the
broader initiation boundary passed `116` with one known environment-dependent
skip, Track A Scan/release contracts passed `121/121`, the prepared-chat
product boundary passed `455/455`, and the Web build completed with zero
errors. The intentionally broad Chat diagnostic also selected one pre-existing
fixture that requires the explicitly prohibited legacy profiling coordinator;
that authority defect is separated from the passing product boundary and was
not repaired in this scope.
