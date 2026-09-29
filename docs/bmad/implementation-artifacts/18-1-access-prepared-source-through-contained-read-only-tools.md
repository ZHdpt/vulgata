# Story 18.1 development record

Status: review
Feature directory: specs/016-contained-source-tools
Workflow state: specs/016-contained-source-tools/workflow-state.json

## Accepted delivery definition

### Story 18.1: Access Prepared Source Through Contained Read-Only Tools

As a batch operator,
I want approved prepared directories exposed through bounded list, read and search operations,
So that analysis can inspect selected source without Git acquisition or arbitrary host access.

**Requirements:** O-FR01, O-FR04; O-NFR01. **Completion dependencies:** none.
**Parallel start:** immediately after the source/tool F0 contract is agreed in this plan; no database/provider prerequisite.

**Acceptance Criteria:**

**Given** an application/operator-approved batch, Repository and root binding,
**When** source access is requested,
**Then** roots are selected outside model arguments and public locations are Repository-relative,
**And** labels are not verified Git revisions; no copying, Git inspection, hashing or retention service is introduced.

**Given** files of known or unfamiliar extensions under the granted root,
**When** listing, ranged reading or text search exceeds one response,
**Then** accurate locations and explicit continuation/limit information permit further access,
**And** incomplete/unreadable results are distinct from complete empty results; listing makes no admission decision.

**Given** a foreign binding, path/symlink escape or requested write/command,
**When** the caller invokes the capability,
**Then** it is denied without access outside the root,
**And** missing inputs and cancellation return sanitized truthful outcomes and release owned resources.

**Given** small owned prepared fixtures,
**When** the production capability is invoked directly,
**Then** its normal and containment paths are observable without DB, LLM, CodeGraph or LSP,
**And** existing normal Git-bound source behavior is unchanged; no UI or management CLI is required.

## Activation and ownership

Observed 2026-09-28T03:33:29.3096814Z in the Codex-assigned managed worktree at `0c0bf0cfb05edc5b28a346d8f3a63002d1e781ef`. The worktree was clean and detached under the host's explicit native handoff; no branch switch is authorized or required. Canonical activation is `docs/bmad/implementation-artifacts/workflow-activations/18-1-access-prepared-source-through-contained-read-only-tools.json`.

The bounded accepted backlog definition supplies readiness. Only Story 18.1 is being activated. Root owns lifecycle, state, evidence, approvals, staging and commits. Workers may not delegate, stage, commit or modify lifecycle state.

## Context evidence

- No `docs/project-lessons.md` exists, so there are no recorded active lessons to apply.
- Current reusable `ISourceReadContext` and `RetainedSourceReadContext` seams provide grant-bound Repository-relative metadata, paginated listing and ranged reads with budgets, deadlines, authorization rechecks and sanitized outcomes.
- Those seams are retained Git-snapshot capabilities, not the prepared-directory contract: there is no text-search operation, live prepared-root traversal or proven filesystem symlink-escape containment.
- Existing retained-source tests provide patterns for cursors, budgets, cancellation, authorization and range results. Story 18.1 still needs direct owned-directory fixtures and a regression boundary preserving normal Git-bound reads.
- October prepared labels are not Profiled Revisions. The specification and plan must not silently add Git acquisition, copying, hashing, Source Snapshot identity or retention.
- AD-2/AD-3 and the Constitution require injected Core contracts, Infrastructure-owned filesystem authority and no arbitrary host/process authority in Agent Execution.
- The planned Worker-facing source capability triggers the repository's early plan-review and explicit planned-change authorization before tasks. No approval is requested before an independently accepted plan exists.

## Workflow Bookkeeping

Specification self-review passed with all 16 requirements-checklist items complete and no clarification marker. The actual feature mapping is `specs/016-contained-source-tools`; the optional after-specify agent-context hook was not executed because this feature has no plan yet. Lifecycle advanced `backlog -> ready-for-dev -> in-progress`. Epic 18 started with this first activation. Pending activation checkpoint: `bmad-18-1-context-20260928T034100Z-7f2ba2`. The next stage is plan with the retained definition author.

The architecture plan added the Worker Agent list/read/search surface and received one CRITICAL plan finding for missing live authorization. The retained architecture-plan author added an injected current-authority verifier with revocation/unavailability fail-closed behavior, continuation invalidation and DB-free test seams. The retained independent reviewer accepted the corrected boundary in `specs/016-contained-source-tools/evidence/plan-review-03.json`. On 2026-09-28T04:25:28.9936733Z the user explicitly authorized progression from that accepted plan to task generation with the instruction `authorize`. Decision record: `specs/016-contained-source-tools/evidence/plan-authorization-01.json`. This authorization does not permit implementation.

The retained definition author generated 26 dependency-ordered tasks covering all accepted requirements and all 19 R/I/S/A/F/L/T design IDs. The first complete-definition review accepted the semantic boundary, after which checkpoint validation found trailing whitespace in the plan. That byte-only issue invalidated the old hashes without changing content. The retained plan author removed only the whitespace; independent plan review accepted the corrected bytes, the user's earlier plan authorization was mechanically rebound to that unchanged semantic scope, and the retained task author revalidated the unchanged tasks. Read-only SpecKit analysis and the refreshed independent complete-definition review found no unresolved placeholder, unmapped task, ordering conflict or Constitution violation. `specs/016-contained-source-tools/evidence/definition-review-result-02.json` is ACCEPT. The pre-production recovery episode remains closed with its original one failed result retained in history. Pending accepted-definition checkpoint: `bmad-18-1-definition-review-20260928T043922Z-a605ed`. Production remains blocked on `18-1/implementation-approval/2`.

On 2026-09-28T05:03:16.4560797Z the user explicitly approved the accepted implementation boundary with the instruction `Approve`. Decision record: `specs/016-contained-source-tools/evidence/implementation-approval-02.json`. Governed implementation may now begin; the approval does not broaden scope or waive convergence, review, conditional E2E or final verification.

T001 and T002 completed by direct verification. Before T003 wrote a guessed test API, its test owner found that the accepted design named the trusted issuer but omitted its callable signature. Independent plan-review-05 confirmed the HIGH definition defect. The retained plan owner specified the existing intended seam (`PreparedSourceContextIssuer`, constructor-injected verifier, `IssueAsync`, typed sanitized result), and the retained task owner clarified T003/T004 without changing their behavior or order. Independent plan-review-06 and definition-review-03 accepted the corrected boundary and confirmed both existing user approvals remain semantically applicable. Root rebound their hashes on 2026-09-28T06:02:11.3091704Z; the implementation recovery episode remains open until final verification, as required for a defect found after production entry.

T003-T015 then completed their governed slices through the contained Core/Infrastructure seam. Before T016 invented an Agent factory, its test owner found that the definition fixed tool names and schemas but omitted the callable construction interface. Independent plan-review-08 confirmed this second HIGH definition defect in the same open recovery episode. The retained plan and task owners fixed only that interface (`PreparedSourceFunctions(context).Create()`, exactly three ordered borrowed-context functions). Definition-review-04 and plan-review-09 accepted the correction and confirmed unchanged scope; root rebound the existing approvals on 2026-09-28T08:23:40.6628207Z. T016 may resume without a new user decision.

T016-T025 completed the remaining Agent projection, authority-lifecycle, truthful-failure, cancellation and budget slices. The final prepared-source boundary passed 115 focused tests with zero skips on Microsoft Windows NT 10.0.26220.0 under the actual workspace user, including Windows handle-relative no-follow link and replacement-race cases. The Agent function subset passed 15 tests and the adjacent Agent regression subset passed 28 tests. The Web project built successfully with existing package-source and Entity Framework assembly-version warnings and no errors.

T026 also attempted the selected retained Git-source regressions. All 63 selected cases were blocked before behavioral assertions because the owned PostgreSQL 17 administrator prerequisite `VULGATA_STORY131_POSTGRES_ADMIN` is not configured in this checkout. That run is recorded as an external-prerequisite blocker, not a passing or product-failing result. Static source inspection found no accidental Git, database, provider, CodeGraph, LSP, UI or CLI dependency in the six new production files, and `git diff --check` passed apart from Git's working-tree line-ending notices. Direct controlled tests qualify the prepared-source contract and Windows containment; they do not claim a real Worker/model/provider pipeline integration.

Quality-review-01 returned REVISE with two implementation-owned behavioral defects. The candidate incorrectly rejected every non-root list request, and range reads compared only length rather than the already-planned observed file identity/metadata. The causal assumption was that root-only listing plus recursive search satisfied discovery and that equal length was sufficient stability evidence; both contradict the accepted list contract and changed-input invariant. The bounded repair adds public-seam regression RED for nested-directory listing and same-length in-flight rewrite, then changes only the existing handle-relative list/read implementation before rerunning focused and full prepared-source tests, convergence, lookback and quality review.

The retained test owner observed both intended REDs: nested listing returned `Incomplete` instead of `Complete`, and an equal-length in-flight rewrite returned `Complete` instead of `ChangedInput`. The retained implementer changed only `PreparedSourceReadContext`: nested listing now opens and disposes the requested child directory through existing held-root no-follow traversal, prefixes Repository-relative child locations and re-observes the directory before disclosure; range reads now compare the existing file identity/metadata observation before, after and immediately before disclosure. Final test-owner verification on a freshly rebuilt qualified Windows assembly passed the two regressions 2/2, their surrounding List/Failure classes 15/15 and the complete prepared-source suite 117/117 with no skips. This remains live-source detection at bounded observation points, not an immutable snapshot guarantee.

Quality-review-02 confirmed both earlier findings resolved, then identified a separate lifecycle race: operations can pass their initial authority/disposal check, queue or approach a file open, and resume after `DisposeAsync` has cancelled activity and closed the root. Those operations may return a source failure instead of the accepted sanitized `Cancelled` outcome. This is the fourth failed result in recovery episode `18-1/implementation/1`; workflow is paused before repair and requires an accepted finite extension. The proposed bounded approach is regression RED for queued and approval-to-open disposal, then reuse the context lifetime cancellation token and post-wait/post-approval disposal checks without changing the public contract or architecture.

On 2026-09-28T11:00:33.9622954Z the user approved that bounded approach and raised the episode's finite recovery limit from four to six failed results with the instruction `Approve`. Decision record: `specs/016-contained-source-tools/evidence/recovery-extension-01.json`. The extension applies only to QR-18.1-03 and does not waive TDD, convergence, refreshed lookback, independent quality review, E2E or final verification.

The retained test owner added deterministic public-seam queued List and Search regressions. Qualified RED was distinct and exact: queued List threw `ObjectDisposedException` on the closed root, while queued Search returned `Incomplete` instead of `Cancelled`. The retained implementer changed only `PreparedSourceReadContext` to link gate waits to the existing lifetime token, recheck disposal after waits and around read open/use, and map disposal-time handle exceptions to sanitized `Cancelled`. Final qualified Windows verification passed the two regressions 2/2, adjacent lifecycle tests 27/27 and the complete prepared-source suite 119/119 with no skips. A final-shape audit confirmed late linking from the stored cancelled token is supported on the installed .NET runtime and canceled gate acquisition cannot over-release the semaphore; no additional production edit was needed.

## Story Lookback

Story 18.1 reconverged without additional implementation tasks after quality review exposed and corrected three behavioral gaps. Nested directories are listed through the held-root no-follow traversal, range reads reject observed same-length metadata drift before disclosure, and queued/opening operations observe context disposal as sanitized `Cancelled` instead of resuming against a closed root. The contained list, range-read and literal-search boundary is covered through the accepted public issuer/context/function seams, including live authority loss, Windows no-follow containment, bounded disclosure, cancellation and resource settlement. The final controlled correction suite passed 119/119 with no skips; this evidence remains deliberately narrower than real Worker/model/provider integration or immutable snapshot behavior.

One reusable definition lesson was promoted to `docs/project-lessons.md`: before behavioral TDD begins, every planned public production seam that tests must call needs a concrete callable construction and invocation shape. This story discovered that omission twice—first at the trusted issuer and then at the Agent function factory—before test authors invented an API. No canonical PRD, architecture or glossary synchronization is required because both corrections clarified already-approved scope without changing product intent.

The user later supplied and explicitly authorized owned PostgreSQL 17 test resources plus an alternate-volume temporary directory. Focused infrastructure qualification passed the retained-source isolated database lifecycle, the disposable direct database reset and the prepared-scan isolated database lifecycle. The configured full suite then exposed a deterministic historical test-fixture defect outside Story 18.1 production behavior: `ExactCrossRepositoryMigrationTests` omitted the repository-standard `public.__EFMigrationsHistory` mapping, so a second context replayed `InitialVulgataDomain` and collided with `GlobalContexts`. The test owner made only that fixture correction; the isolated repro passed and the complete migration-test class passed 4/4, after which Story 18.1 reconverged without new tasks.

A second reusable lesson was promoted to `docs/project-lessons.md`: direct PostgreSQL migration fixtures must pin the public migration-history schema consistently with production and prove lifecycle corrections against newly created owned databases. No PRD, architecture or glossary synchronization is required because the correction changes neither Story 18.1 product behavior nor its accepted public boundary.

The corrected configured full-suite run then passed 3,251 tests but exposed three further historical qualification failures outside Story 18.1 production: a stale reviewed-profile fingerprint and two PostgreSQL read timeouts under parallel suite load. At recovery count six the user approved a final finite extension to seven. Diagnosis proved that the accepted and embedded profile copies agreed but lacked an LF checkout policy and that the hard-coded fingerprint matched neither canonical nor Windows bytes. The two PostgreSQL cases passed serially against fresh owned databases (retained source 10/10 and durable recovery 1/1), identifying full-suite resource contention rather than product-state defects.

The bounded correction pins both reviewed profile files to LF, restores the canonical fingerprint and places the two PostgreSQL-heavy classes in one disabled-parallelization collection without weakening their assertions or cleanup. The combined correction boundary passed 27/27, and Story 18.1 reconverged without task reuse or append. A third reusable lesson was promoted to `docs/project-lessons.md`: hash-bound embedded resources need an explicit canonical EOL policy, while expensive owned-database classes should declare their nonparallel resource boundary when the suite shares one local server. The exact configured full-suite rerun remains the final verification obligation; this lookback does not claim it has passed.

After rebasing onto the repository's isolated-suite policy, the Story 18.1 PreparedSource tests and their shared helper were classified as `SelfContained` without changing assertions or product behavior. The policy validator accepted all 283 test sources, and the complete PreparedSource selection passed 119/119 with zero skips through `eng/test.ps1`. This is adoption of the already-canonical policy in `docs/agents/testing.md`, so it does not warrant a duplicate project lesson: future tests must be classified when added, the configured default proves only the complete `SelfContained` suite, and excluded qualification suites must be run and reported separately when their behavior is required.

Fresh convergence identified that the retained-source PostgreSQL regression boundary accepted by T026 had previously been blocked before assertions and therefore was not proven by the smaller serial fixture checks. T027 closed that exact obligation with a runner-owned PostgreSQL 17 container and volume: all 63 selected retained-source read-context, range-read and capture tests passed with zero failures and zero skips, and the run-owned resources were removed. No ambient `.env` database value was used. Story 18.1 then reconverged cleanly with no additional task reuse or append. The configured build and complete `SelfContained` command remain final-verification obligations; no External, LocalTools, E2E or HistoricalBoundary suite is claimed by this lookback.

The first configured `SelfContained` verification then exposed a Story 18.1 test-execution isolation defect: 88 of the 119 PreparedSource cases reached context issuance as `RootUnavailable` only while sharing the full parallel suite, although the fixture ownership test succeeded and the same boundary had passed 119/119 alone. The correction assigns every PreparedSource class to one disabled-parallelization xUnit collection, following the repository's existing resource-boundary pattern without changing test bodies, assertions, suite traits or production code. Policy validation again accepted all 283 classified sources and the corrected focused boundary passed 119/119 with zero skips. Reconvergence found no remaining implementation task. The exact configured suite rerun remains pending and this lookback does not claim it passed.
