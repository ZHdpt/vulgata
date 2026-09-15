# Story 13.1 development record

Status: in-progress
Feature directory: specs/015-retained-source-access
Workflow state: specs/015-retained-source-access/workflow-state.json

## Accepted delivery definition

### Story 13.1: Retain exact Repository source with scoped read access

As a System Owner,
I want the source used for authorized Repository analysis to be pinned, retained and accessible through a common read-only interface,
So that analysis and later knowledge reads can use the same committed code despite working-copy changes, temporary cleanup or process restarts.

**Acceptance Criteria:**

**Given** an existing authorized Repository, including an eligible standalone Repository, and a permitted Git ref,
**When** a trusted application service acquires a Source Snapshot,
**Then** it checks Repository authority and resolves/pins one full Profiled Revision before capture,
**And** the resulting source identity is Repository plus the exact Git commit, with integrity metadata and an internal storage locator,
**And** it requires neither a Repository pair/PairExecutionId nor a parent System solely to capture standalone source. (FR-2.4/2.5, FR-4.9; AR-1/3.)

**Given** a completed capture and a server-granted read context,
**When** an ordinary consumer lists directories/files or reads a bounded source range/content after a working copy or remote ref has changed,
**Then** the common interface returns metadata/content from the captured committed bytes, with Repository-relative locations,
**And** it never substitutes the mutable checkout, changes canonical bytes through decoding or uses tool-index data as the source authority,
**And** unavailable text/entry capabilities are explicit rather than silently altered or omitted. (FR-4.9; AR-3/5.)

**Given** a completed capture stored under deployment-configured durable source storage,
**When** the original consumer lease is disposed and a fresh process/application instance reopens the source while the original remote is unavailable,
**Then** a newly authorized consumer can verify and read the same retained bytes without relying on the former in-memory token registry, temporary operation directory or a new remote clone,
**And** source retention/configuration is independent of CodeGraph's cache/data-root lifetime. (FR-4.9; source portions of NFR-3.3/4.1; AR-3/30/31.)

**Given** a source read request,
**When** its grant has expired, access has been revoked, its Repository/revision binding is wrong, or its path is absolute, escaping or resolves outside the captured source,
**Then** the application denies the operation without disclosing unauthorized source or metadata,
**And** ordinary read contracts and sanitized errors do not expose the infrastructure's physical RootPath or credentials,
**And** any necessary physical-path seam remains restricted to trusted adapters rather than becoming an ordinary Scan/Agent file API. (FR-4.9; NFR-2.3; AR-3/6.)

**Given** a server-issued source-access budget and deadline,
**When** multiple operations exhaust the cumulative permitted work or cancellation occurs,
**Then** the operation stops with an explicit bounded limit/cancellation outcome and releases its owned transient resources,
**And** repeated calls cannot bypass the shared read-context limits by receiving an automatic fresh budget,
**And** cancellation never promotes a partial capture to a completed source object. (FR-4.9; NFR-3.1; AR-3/8/15.)

**Given** retries or multiple authorized consumers referring to the same Repository and Profiled Revision,
**When** capture completion is published, a verified capture is reopened, or one lease is released,
**Then** source identity/integrity remain stable and no reader observes a partly published capture,
**And** cleanup of one operation does not invalidate another consumer or delete verified canonical source,
**And** the initial policy performs no automatic pruning of retained canonical source; only safe unretained/disposable operation state is cleaned. (FR-4.9; NFR-3.3/4.1 source portions; AR-3/31.)

**Given** Git authentication/transport/ref failure, missing or corrupted retained bytes, or unavailable storage capacity,
**When** acquisition or reopening cannot meet the exact-source contract,
**Then** it returns a truthful sanitized failure/limitation with enough logical Repository/revision context for an authorized caller to respond,
**And** it neither silently selects another ref/source nor labels failed/partial data as a usable completed capture,
**And** any later reacquisition/retry requires fresh authority and exact-byte verification rather than trusting a new temporary path. (FR-2.5/4.9; NFR-2.3/3.1/3.3.)

**Given** this story's source capability and isolated local Git fixtures,
**When** its acquisition, bounded listing/reading, denial, concurrent-lifetime and fresh-process reopening behavior is verified against declared persistent-storage configuration,
**Then** it works independently without invoking CodeGraph, LSP, an LLM or future semantic Code Unit/Document/flow tables,
**And** only source-owned records/configuration needed for this capability are added or adapted,
**And** no test or result labels this source-only capability as a completed Scan, generated wiki or newly qualified full Docker analysis stack. (AR-2/3/16/30/32.)

**Scope and dependencies:**

- **User-visible value:** exact source survives beyond the former disposable lease, so later authorized evidence reads have a trustworthy committed input. This is delivered as a callable application/source capability; no new public Snapshot UI is required.
- **Prior dependencies:** retained Repository management/authorization and existing Git acquisition primitives only. This story does not depend on any later Story 13.x.
- **Included:** source identity/atomic capture, durable retention/reopen, generic bounded relative metadata/list/read operations, source authority and safe operation cleanup. Define the source contract before consumers depend on it.
- **Compatibility boundary:** implement a new generic durable source owner using reusable acquisition/reading mechanics. Preserve existing legacy pair consumers unless a minimal compatibility adapter is needed; wholesale pair-registry migration or rewriting every RootPath consumer is outside this story. Ordinary consumers of the new interface still receive only scoped relative access.
- **Excluded:** CodeGraph/JDT-LS installation or invocation, detailed parsing/semantic units, advisory LLM overview, file scheduling, Document/Edge/flow generation, Chat, full Scan activation and operator backup/restore. Their requirements remain in Epic 13 or later approved epic scope; they are not credited as complete here.
- **Scope of requirement credit:** completes the source acquisition/read/retention contribution to FR-4.9 and the corresponding source/privacy/durability portions of the cited NFR/ARs. It does not complete all composite FR-4.9 or NFR-4.1 obligations.
- **Later gates:** story approval accepts this delivery definition only. Technical planning must still pass independent review and any triggered Agent-tool/Scan-strategy/PRD authorization before tasks/implementation; data/schema changes remain owned and minimal.

**Definition approval:** The user approved Story 13.1 on 2026-09-11. Approved proposal SHA-256: `2FAB3D492CA0DD5958B60DB5BF07DAEBEF4BEFA337FB59897A3C8FFD89003F83`. This is story-definition approval, not implementation, feature activation or runtime qualification.


## Activation and ownership
Observed 2026-09-14T09:01:02.3617369Z on branch dev at bff540980800eb67254170a0c82e38678b63576d.
Assigned checkout: G:/source/repos/vulgata. Initial index/worktree clean; no unrelated paths.
Canonical activation: workflow-activations/13-1-retain-exact-repository-source-with-scoped-read-access.json
The accepted bounded backlog story supplies readiness. Only Story 13.1 is activated. No production or task-generation approval is inferred.
Root owns lifecycle, state, decisions, evidence, staging and local checkpoints. Workers cannot delegate or commit.

## Context evidence

- Current workflow validator passed before activation mapping (`python .agents/skills/run-bmad-sprint-loop/scripts/validate_workflow.py --repo .`).
- Initial Git porcelain v2/index reported no changed paths. Git warned that the optional user-level ignore file was unreadable; repository status still completed successfully.
- Activation write command created the canonical activation and development record, then failed on a stray trailing command name. Readback confirmed both artifacts; no retry or duplicate activation was performed.
- Current Story 13.1 and source-related PRD/AD obligations were read. Static inspection confirms existing RepositoryAuthorizationVerifier rejects standalone Repositories and carries pair-era constraints. Planning must define source-specific live authorization without broad legacy migration.
- Architecture escalation: the new generic retained source owner must settle storage/publication, integrity, grant and budget contracts under AD-2/3/30/31. Dispatch plan-architect-high (Sol/high), then independent plan-only review and user authorization before tasks. Other role profiles remain configured defaults.
- The current skill/state-machine/review-and-correction use the fourth consecutive revision stop. The dispatch reference's older three-revision wording does not override the explicitly invoked current skill.

## Specification dispatch

1. role_and_objective: spec-author, Sol/medium; run speckit-specify and self-review for Story 13.1 only.
2. authoritative_inputs: raw Story 13.1 in docs/bmad/epics.md; this development record; canonical activation with pinned authority hashes; AGENTS, project state, architecture gateway/spine, PRD, glossary, constitution.
3. workflow_boundary: specify, 13-1-retain-exact-repository-source-with-scoped-read-access, specs/015-retained-source-access; dev at bff540980800eb67254170a0c82e38678b63576d; only activation/development additions, no production diff.
4. constraints_and_skills: current speckit-specify and run-bmad-sprint-loop; no delegation or Git operations; preserve other writers; optional context refresh waits until this feature has a plan; no historical plans or waivers.
5. write_boundary: new feature spec.md, checklists/requirements.md and canonical .specify/feature.json selector only. Root owns lifecycle/state/evidence/Git.
6. output_and_evidence: actual directory/files/hashes and self-review object with source/stage/time, checklist result and hook disposition returned to root.
7. completion_and_failure: finish after self-review passes; return conflicts/blockers without advancing lifecycle or claiming independent acceptance.


Activation mapping completed at 2026-09-14T09:07:01.6134605Z. Lifecycle: backlog -> ready-for-dev (bounded accepted definition materialized above) -> in-progress. Epic 13 is in-progress; every other story remains backlog. Pending activation checkpoint: bmad-13-1-context-20260914T090701Z-1e6853. Manifest: this record, canonical activation, sprint-status.yaml, .specify/feature.json, and the new feature workflow-state.json. Specification/checklist remain outside the activation checkpoint pending definition acceptance.

Selector persistence note: .specify/feature.json is intentionally ignored by .specify/.gitignore. It remains the local canonical selector; the durable activation/state/development mapping reproduces it on recovery. The checkpoint manifest therefore excludes this local-only selector.

## Plan dispatch

1. role_and_objective: plan-architect-high, Sol/high; run speckit-plan plus self-review. Architecture trigger: new generic shared source owner must settle storage/publication, integrity, source-specific live authorization, grants/budgets and ordinary/trusted interfaces under AD-2/3/30/31.
2. authoritative_inputs: spec.md SHA256 6400232780739b7574760afd74b0dcba02c19b17d19383f3b0954b85bee88bca; raw Story 13.1; AGENTS; project-state/development/testing/domain guides; architecture gateway/spine; canonical PRD/glossary/constitution and activation authority hashes; applicable current source/tests/package versions.
3. workflow_boundary: plan, Story 13.1, specs/015-retained-source-access, HEAD cd82c8994063fa522253264f59c13b1cbc08a3f2. Activation commit changed metadata only. Specification/checklist and root state/evidence are expected uncommitted inputs.
4. constraints_and_skills: speckit-plan/current sprint loop; no delegation, production/tests/runtime/DB/provider execution, historical plans or waivers. Preserve other writers, source-only scope and legacy consumers. Root handles explicit-path optional context refresh after plan return.
5. write_boundary: feature plan.md, research.md, data-model.md, quickstart.md, contracts/* and implementation-design.md only if required. No spec/checklist/state/lifecycle/Git writes.
6. output_and_evidence: complete plan file/hash manifest; source references; author self-review; precise approval triggers/effects; alternatives/consequences and scoped verification/infrastructure needs. Resolve failure/cancellation/concurrency/integrity/auth/encoding/entry limitations and configurable budgets concretely, with only source-owned records.
7. completion_and_failure: finish after feasible source-grounded complete plan/self-review; return authority conflicts/blockers, never ask for user approval or claim independent acceptance. Root dispatches fresh plan reviewer before approval/task generation.

## Activation checkpoint verification

The checkpoint trailer resolved uniquely to cd82c8994063fa522253264f59c13b1cbc08a3f2 and was verified ancestral to HEAD. Its four-file manifest contains only the development record, activation record, sprint tracker and new workflow state. Staged whitespace validation and the workflow validator passed before commit. The initial restricted git-add attempt failed to create .git/index.lock; the explicitly owned staging and local commit then succeeded through sandbox escalation. No external publication occurred. The source/specification authority set remained unchanged.

## Plan completion and context pointer

The six-file plan set passed author self-review at plan.md#inline-plan-self-review. Root verified all returned SHA-256 values and persisted evidence/plan-self-review-01.json. The selected feature remains specs/015-retained-source-access. The author reported a rejected all-in-one patch that made no changes, then completed incremental writes.

The optional explicit-path agent-context helper ran but skipped because its Python fallback lacks PyYAML. Root performed the bounded managed-block update directly using the selected plan and workflow-state paths. Only that block changed in AGENTS.md; baseline activation hashes remain historical preflight evidence, and the current review packet pins the updated AGENTS bytes. No workflow rule or approval gate changed.

## Plan-only independent review dispatch

1. role_and_objective: fresh definition-reviewer, Sol/medium; independent plan-only review before user plan authorization/task generation.
2. authoritative_inputs: every raw artifact in specs/015-retained-source-access/evidence/plan-review-boundary-01.json, including raw BMad story/spec, full plan set, constitution, gateway/spine, PRD/glossary/AGENTS/project guides; current relevant source/tests; current workflow contracts and verdict schema.
3. workflow_boundary: plan-review; Story 13.1; specs/015-retained-source-access; dev HEAD cd82c8994063fa522253264f59c13b1cbc08a3f2. Diff identity f5d98b71fc95d8a48514d1b6ecdbb23959356a537399529c91257422f0fb7593; canonical manifest algorithm and all raw hashes in review packet. Application source unchanged.
4. constraints_and_skills: current plan-only discipline; no delegation or writes/runtime/Git mutation. Inspect raw authorities and feasibility/security/bounds/atomicity/recovery/compatibility/verification; do not substitute author conclusions. speckit-analyze's tasks prerequisite belongs to later definition review.
5. write_boundary: read-only; return raw verdict to root for unchanged persistence.
6. output_and_evidence: exactly verdict.schema.json object with gate plan-review, verified reviewed_boundary, raw artifact paths, required findings and earliest_owner; precise evidence/correction for blockers. No inflated cosmetic severity or ACCEPT with blockers.
7. completion_and_failure: independent ACCEPT/REVISE/BLOCKED; ordinary repair to earliest author; no user question or implementation authority.

## Plan correction 1

Independent plan-review-01 returned REVISE with one HIGH finding, PR-13.1-001 (earliest owner: plan). Raw verdict is preserved unchanged in specs/015-retained-source-access/evidence/plan-review-01.json and passed runtime verdict validation. This is the first consecutive plan-phase revision; no human approval is requested for ordinary correction.

1. role_and_objective: plan-architect-high corrects PR-13.1-001 and self-reviews the coherent plan set.
2. authoritative_inputs: unchanged raw finding/full verdict at evidence/plan-review-01.json, current Story/spec/authority set.
3. workflow_boundary: plan correction, Story 13.1, same feature and HEAD cd82c8994063fa522253264f59c13b1cbc08a3f2; no tasks exist.
4. constraints_and_skills: current speckit-plan/sprint-loop, Sol/high, no delegation/Git/production/tests; preserve other writes and bounded source scope.
5. write_boundary: six existing plan artifacts only; root retains all state/evidence/lifecycle/Git ownership.
6. output_and_evidence: coherent single-owner capture limits across signature/configuration/failures/planned tests; full updated manifest and self-review/disposition returned to root.
7. completion_and_failure: finish after correction/self-review, then independent re-review; blockers returned unchanged, no user question or acceptance claim.

## Plan-only re-review dispatch 2

1. role_and_objective: independent definition-reviewer, Sol/medium, re-review corrected boundary after first REVISE.
2. authoritative_inputs: raw plan-review-01.json and all raw artifacts pinned in plan-review-boundary-02.json; revised plan/research/quickstart/application contract plus unchanged complete definition and authorities.
3. workflow_boundary: attempt 2, Story 13.1, same feature and HEAD; diff identity 0dd320e5015dae3efecb7721c862b4c9a479c320c770b5f925904cbb97955e4b. No tasks/production/tests changed.
4. constraints_and_skills: same read-only plan review; reassess PR-13.1-001 and correction consistency/regression; no delegation/runtime/Git mutations.
5. write_boundary: none; return raw verdict to root.
6. output_and_evidence: schema-valid verdict with new verified boundary, raw artifact list, exact findings and earliest owners; acceptance excludes all blockers.
7. completion_and_failure: one independent ACCEPT/REVISE/BLOCKED, no user question or task generation.

## Current pause: planned-change authorization

Plan-review attempt 2 independently ACCEPTED the corrected six-file plan with no findings. PR-13.1-001 is resolved; the consecutive plan revision count resets after acceptance. Raw verdict and reviewed hashes are in specs/015-retained-source-access/evidence/plan-review-02.json and plan-review-boundary-02.json. Root reverified every accepted artifact hash and validated the raw verdict plus compact workflow state.

The pending user decision is specs/015-retained-source-access/evidence/plan-authorization-01.md, scope SHA-256 0dd320e5015dae3efecb7721c862b4c9a479c320c770b5f925904cbb97955e4b. Approval is solely to progress to tasks; fresh combined definition review and separate implementation approval follow. No user approval has been inferred, no tasks were generated, and no application/test code or runtime resources were changed.

Activation checkpoint remains cd82c8994063fa522253264f59c13b1cbc08a3f2. Current definition/evidence/pointer changes remain uncommitted until the accepted-definition checkpoint. All other stories remain backlog; no unrelated work was present or changed.

## Plan authorization accepted

At 2026-09-14T10:18:58.368839Z, the user replied exactly: "Approval authorizes only progression to tasks". Root verified the accepted plan boundary, selector/mapping, compact schema, Git branch/HEAD and unique ancestral activation checkpoint. Scope remains 0dd320e5015dae3efecb7721c862b4c9a479c320c770b5f925904cbb97955e4b. The decision is recorded in evidence/plan-authorization-01.md. Only task generation and subsequent definition review proceed; production implementation remains gated. Current changes are the owned definition/evidence/pointer files from this story; index is empty and no unrelated changes were observed.

## Task author dispatch

1. role_and_objective: task-author, configured Luna/medium; run speckit-tasks and self-review Story 13.1 only.
2. authoritative_inputs: raw Story 13.1/spec, complete accepted six-file plan set, plan-review-02 ACCEPT and recorded task-only user authorization; AGENTS/constitution/architecture/PRD/glossary/project guides and current relevant source/tests.
3. workflow_boundary: tasks; specs/015-retained-source-access; dev HEAD cd82c8994063fa522253264f59c13b1cbc08a3f2; accepted scope 0dd320e5015dae3efecb7721c862b4c9a479c320c770b5f925904cbb97955e4b; no application/test changes or historical activation.
4. constraints_and_skills: speckit-tasks/current sprint loop; mandatory vertical behavioral TDD, source-only scope, current model strategy and E2E predicates. No delegation/runtime/Git writes. Preserve other writers. Current skill overrides stale Terra/low E2E and three-revision reference prose; the configured runner is Luna/high and revision pause is fourth consecutive.
5. write_boundary: tasks.md only. Root retains state/evidence/lifecycle/Git; no spec/plan/code/tests edits.
6. output_and_evidence: strict checklist tasks, exact paths, dependency order, FR/SC/source coverage, coherent TDD groups and separate test/production ownership; truthful separate-process/PostgreSQL verification; file hash/counts/self-review/hooks returned to root.
7. completion_and_failure: actionable self-reviewed tasks or raw upstream defect/blocker, no implementation/independent acceptance claim.

## Combined definition review dispatch 1

Task author returned 38 tasks and a passing self-review. Root verified the returned hash and sequential checklist IDs; these mechanical checks do not establish semantic acceptance.

1. role_and_objective: fresh definition-reviewer, Sol/medium; combined independent definition review plus read-only speckit-analyze.
2. authoritative_inputs: all raw paths/hashes in specs/015-retained-source-access/evidence/definition-review-boundary-01.json, including Story/spec/full plan/tasks/authorities/task-only user authorization; relevant current source/tests; current workflow/analyze/verdict contracts.
3. workflow_boundary: definition-review attempt 1; Story 13.1; feature specs/015-retained-source-access; dev HEAD cd82c8994063fa522253264f59c13b1cbc08a3f2; nonrecursive diff 2644ebf01e273507b052a59c5f1e90a6062ab0dc76b4ed359cc8822de38c5bb7; no production/test writes.
4. constraints_and_skills: read-only analyze/review, no delegation/runtime/Git mutation or user question; judge coverage, source fit, executable dependency order, honest vertical public-seam TDD, exact paths/parallel ownership, and required qualification. Apply current models/fourth-revision threshold over stale reference prose.
5. write_boundary: none; return raw verdict and analysis to root.
6. output_and_evidence: schema-valid definition-review verdict with verified packet boundary and earliest-owner findings; separate bounded analyze coverage/metrics/prerequisite/hook evidence.
7. completion_and_failure: independent ACCEPT/REVISE/BLOCKED, correction routing to earliest author, no implementation execution or approval claim.

## Definition correction 1

Independent definition review returned REVISE: DR-13.1-001 (CRITICAL behavioral TDD ordering) and DR-13.1-002 (HIGH Core-facing trusted-workspace contradiction). Raw verdict: evidence/definition-review-01.json; analyze report: evidence/definition-analysis-01.md; rejected tasks preimage retained under evidence/history/tasks-definition-01.md. Both findings are task-owned, so accepted spec/plan hashes and the user's task-only plan authorization remain valid.

1. role_and_objective: task-author Luna/medium corrects both raw findings and self-reviews tasks.
2. authoritative_inputs: unchanged raw definition-review-01 findings; accepted spec/plan/authorization; current Constitution/TDD/workflow.
3. workflow_boundary: first definition revision; tasks stage; same feature/HEAD; no upstream or implementation edits.
4. constraints_and_skills: tasks-only current loop; no delegation; compilation failures are intermediate diagnostics, not sufficient behavioral RED. Recheck executable dependencies, shared-file parallelism, ownership, single-project separate-process fixture, and tested delete/logging behavior.
5. write_boundary: tasks.md only.
6. output_and_evidence: explicit RED/GREEN/verification groups, tested Infrastructure-only workspace boundary, corrected hash/counts/dispositions/self-review.
7. completion_and_failure: return after self-review for independent re-review; no user request or acceptance/implementation claim; upstream defects routed to root.

The first task correction handoff (47 tasks, hash 5cb6ad8ed6077f1b50d6597787898232effbffc3e8dba7fe84f8c208812c875a4) failed root mechanical/dependency validation before independent re-review. Exact findings are in evidence/task-author-validation-02.md; its raw preimage is evidence/history/tasks-author-correction-01.md. The same task author was instructed through the seven-slot contract to rewrite tasks coherently and report results for all eight findings. This continues definition revision 1; no independent gate attempt or user-approved scope changed.

## Definition re-review dispatch 2

1. role_and_objective: independent definition-reviewer Sol/medium re-reviews rewritten tasks and refreshed analyze coverage.
2. authoritative_inputs: unchanged raw first verdict plus all raw artifacts in evidence/definition-review-boundary-02.json; accepted upstream artifacts and authorization unchanged.
3. workflow_boundary: definition-review attempt 2; same feature/HEAD; diff SHA256 2ad54fbb7628b9b121aa3ee05bbe8ad7e5edff5fc2bed6f4a093636453bbc573; task hash 23d3583d309accb734ccc3a597f92cf35b95bef48c16df11db23b2382b2e1f9b; 45 sequential tasks.
4. constraints_and_skills: current read-only unified review/analyze, no delegation/runtime/user question/Git mutation. Check actual public-test prerequisites for every GREEN, not merely RED/GREEN labels. Compile/nonexistent-helper failures are insufficient behavioral RED.
5. write_boundary: none.
6. output_and_evidence: raw schema-valid verdict with exact updated boundary, earliest-owner findings and refreshed analysis evidence.
7. completion_and_failure: independent ACCEPT/REVISE/BLOCKED, no implementation execution or approval claim.

## Definition correction 2

Attempt 2 returned REVISE with DR-13.1-001 still CRITICAL: broad public tests could not become GREEN after their partial implementation tasks, and the separate-process RED preceded its harness. DR-13.1-002 is resolved. Raw verdict/analysis: evidence/definition-review-02.json and definition-analysis-02.md. Spec/plan remain unchanged; the existing plan authorization remains valid.

1. role_and_objective: task-author Luna/medium resolves executable TDD dependency order.
2. authoritative_inputs: unchanged raw second verdict/finding and accepted spec/plan/approval.
3. workflow_boundary: second consecutive definition revision; tasks stage; same feature/HEAD; no production/test writes.
4. constraints_and_skills: current loop/TDD; group one public RED with every production prerequisite, claim GREEN only after the last; defer detailed capture/open cases until that seam exists; complete child scaffolding before runtime proof; allow truthful already-correct test-only GREEN; contract declarations cannot hide validation behavior.
5. write_boundary: tasks.md only.
6. output_and_evidence: sequential task groups with explicit entry prerequisites, RED/all production/GREEN task mapping and separate ownership, complete source coverage, returned hash/count/self-review.
7. completion_and_failure: finish after verifying every claimed GREEN is executable at its graph position; otherwise report specific upstream contradiction; no user question/implementation/Git/delegation.

## Definition re-review dispatch 3

1. role_and_objective: independent definition-reviewer Sol/medium, attempt 3 after two task-owned revisions.
2. authoritative_inputs: raw prior verdict and all raw artifacts in evidence/definition-review-boundary-03.json; only tasks changed.
3. workflow_boundary: same Story/feature/HEAD; diff SHA256 893ebdd6af1ac54d43881d83e63d4d14ae479b711cd2f7c9eb04c2d8f9b23826; task hash d7e9e3f0173716ca161275d1202af8ce6ea281665aba3e64f5ec6b1c408beba2; 40 sequential tasks.
4. constraints_and_skills: current read-only review/analyze, no delegation/runtime/Git/user question; check actual group prerequisites, retained failure/policy coverage, declaration vs validation, capture/open availability and truthful test-only evidence; distinguish harmless bootstrap from untested feature behavior.
5. write_boundary: none.
6. output_and_evidence: raw schema-valid verdict and concise refreshed analysis, precise correcting prerequisites for any remaining blocker.
7. completion_and_failure: independent ACCEPT/REVISE/BLOCKED; fourth consecutive REVISE is the pause threshold, not this attempt; no implementation authority.

## Definition correction 3

Attempt 3 returned REVISE with DR-13.1-001 (identity validation and early open ordering) and DR-13.1-003 (omitted explicit capture-policy/failure/prepublication-authorization/ref-mutation/PostgreSQL-constraint RED tests). Large integrated groups, child scaffolding and Infrastructure-only workspace boundary are now accepted aspects. Raw verdict/analysis: evidence/definition-review-03.json and definition-analysis-03.md. Counter is 3; a fourth consecutive REVISE requires user direction.

1. role_and_objective: task-author Luna/medium makes precise remaining corrections while preserving accepted group structure.
2. authoritative_inputs: unchanged raw attempt3 findings and accepted spec/plan/user authorization.
3. workflow_boundary: third correction; same Story/feature/HEAD; no upstream or implementation changes.
4. constraints_and_skills: current loop/TDD, no delegation; declarations before validation RED, then explicit validation implementation and GREEN; early manifest capture-only, open assertions after open exists; restore every omitted test before its production chain; compiled-contract initial runtime bootstrap is accepted by reviewer.
5. write_boundary: tasks.md only.
6. output_and_evidence: sequential IDs/references and case-by-case RED/production/GREEN coverage table for all raw omitted obligations, preserving child/test-only/trusted boundaries; exact hash/count/self-review returned.
7. completion_and_failure: return after all raw points/dependencies checked or specific blocker; no user request, scope change, implementation or Git action.

The correction3 handoff returned 43 tasks (hash ddb8c55cd380884a8d321194cfd707daa6c34bda271a53f27b8a853024ea3d13). Before pinning attempt4, root returned two exact output-consistency checks to the same task author: PostgreSQL table GREEN must point to checklist T019 after complete EF/migration/repository/composition prerequisites, and T009 must explicitly test each configured cap exhaustion separately from caller non-override and disk exhaustion. The seven-slot follow-up limited writes to tasks.md and required preserving the current structure. This is author handoff correction, not another independent verdict or scope change.

## Definition re-review dispatch 4

1. role_and_objective: independent definition-reviewer Sol/medium reviews corrected tasks and refreshed analyze.
2. authoritative_inputs: raw attempt3 findings and all raw paths in evidence/definition-review-boundary-04.json; accepted upstream/approval artifacts unchanged.
3. workflow_boundary: attempt4, same Story/feature/HEAD; task hash 261a5fd227ee134b1355541b6e644cd9049958b370b1127527c66a8a3536a1d0; boundary d7fe3e5dae8f16499680b600bf6d6bed6aeb9c2254cce162d68f9fbd9bf77265; 43 sequential tasks.
4. constraints_and_skills: current read-only loop/analyze, no delegation/runtime/Git/user question; verify identity/open ordering, restored exact failure/policy/ref/authority/PostgreSQL test cases and tables; preserve unchanged accepted bootstrap/workspace aspects; assess severity accurately independent of attempt count.
5. write_boundary: none.
6. output_and_evidence: raw schema-valid verdict with verified boundary and concise analysis.
7. completion_and_failure: independent ACCEPT/REVISE/BLOCKED; a fourth consecutive REVISE requires root to pause for user direction. No implementation authority.

## Checkpoint formatting/hash maintenance

After definition ACCEPT, root discovered nine trailing Markdown-space lines in the new spec/plan/checklist. Earlier unstaged diff checks do not inspect untracked files. Root removed only trailing horizontal whitespace and preserved exact original preimages under evidence/history; every content token and original line-ending sequence is unchanged. The proof is evidence/definition-whitespace-normalization-01.json.

Feature-local .gitattributes rules preserve exact reviewed bytes across checkouts and permit CR as a terminator while retaining active-artifact whitespace checks; historical preimages alone preserve original whitespace. This metadata does not change product/architecture/task scope. Prior task-only plan authorization remains recorded against its original bytes; equivalence is explicit, not a newly inferred implementation approval.

1. role_and_objective: definition-reviewer Sol/medium independently rebinds acceptance after metadata-only hash invalidation.
2. authoritative_inputs: prior ACCEPT, raw new boundary05, exact whitespace proof/preimages and .gitattributes; task bytes unchanged.
3. workflow_boundary: same Story/feature/HEAD; boundary a68d54ba9b28a4d1dd6058c3a010a273f8f8b5e8bf23ad9f6949a145ba00dd50. Last ACCEPT reset consecutive revisions to0; this is not another REVISE.
4. constraints_and_skills: bounded read-only equivalence/attribute/hash review, no delegation/runtime/Git/user question; preserve prior conclusions only after verification; flag any real scope change.
5. write_boundary: none.
6. output_and_evidence: raw schema-valid new-boundary verdict and brief equivalent-content analysis.
7. completion_and_failure: independent verdict before checkpoint and implementation-approval request; no writes/implementation or alteration of prior verdicts.

## Accepted definition and implementation-approval pause

Definition review attempt4 ACCEPTED all 43 tasks/32 obligations. A subsequent metadata-only rebind (attempt5) independently confirmed exact trailing-whitespace equivalence, unchanged task bytes, preserved active checks and no scope/authority change. Current raw verdict: evidence/definition-review-05.json; scope SHA256 a68d54ba9b28a4d1dd6058c3a010a273f8f8b5e8bf23ad9f6949a145ba00dd50. The prior plan authorization remains valid for identical content. Consecutive definition revisions reset to0.

Pending implementation decision: evidence/implementation-approval-01.md. No production/test/runtime work has begun. Convergence, quality, E2E and final application verification remain pending.

Accepted-definition checkpoint ID: bmad-13-1-definition-review-20260914T111049Z-a96ea6. Expected parent: cd82c8994063fa522253264f59c13b1cbc08a3f2. Root stages only the explicit manifest in evidence/definition-checkpoint-01.json after fresh ownership/hash/schema/whitespace checks. Historical rejected task files under evidence/history are audit preimages only and never executable feature selectors. No unrelated changes were observed.

## Implementation approval accepted

At 2026-09-15T01:39:28.276503Z, the user response "Approve implementation" was recorded against scope a68d54ba9b28a4d1dd6058c3a010a273f8f8b5e8bf23ad9f6949a145ba00dd50. Resume verified all accepted hashes, the selected feature, clean assigned dev checkout and uniquely bound definition checkpoint 68240bb86a93b946253ded0e48f9635a4047d8ec. The interrupted prior turn had issued no tool calls or mutations. Implementation now proceeds through the accepted TDD groups; only Story13.1 and owned verification resources are authorized.

## Implementation bootstrap dispatch and runtime preflight

1. role_and_objective: tdd-implementer Sol/medium performs approved T004 declarations only, no behavioral GREEN claim.
2. authoritative_inputs: accepted spec/plan/data-model/contracts/tasks, AGENTS, constitution/development and accepted implementation approval.
3. workflow_boundary: implement; T004 precedes runtime identity RED; HEAD68240bb86a93b946253ded0e48f9635a4047d8ec.
4. constraints_and_skills: no validation/EF/filesystem/Git/DI behavior; no delegation; preserve other writers; no Core workspace path seam.
5. write_boundary: Core SourceSnapshot.cs, RetainedSourceContracts.cs, IRetainedSourceRepository.cs only.
6. output_and_evidence: exact API shapes for test author, declaration-only build evidence, changed paths; no fabricated RED.
7. completion_and_failure: declarations compile or specific blocker; no stage/commit/state writes.

Runtime preflight found .NET10 available, Docker engine unavailable and no configured PostgreSQL test-admin connection. Pending async authorization asks whether to start Docker Desktop (which can resume preexisting auto-start containers) for the proposed isolated PostgreSQL17 fixture. Exact planned boundary is evidence/runtime-boundary-01.json. Core declaration/validation work continues; no database/container startup is authorized by elapsed time.

## Test scaffolding and identity RED dispatch

1. role_and_objective: tdd-test-author Luna/high prepares T001-T003 owned fixtures, then T005 public identity/value runtime RED.
2. authoritative_inputs: accepted source definition, actual completed Core declarations, current testing/TDD/constitution guidance and accepted implementation decision.
3. workflow_boundary: implement first identity group T004-T007; HEAD68240bb86a93b946253ded0e48f9635a4047d8ec; Core author finished its build before test build scheduling.
4. constraints_and_skills: no delegation or production writes; unique contained fixture directories; no fabricated compile/harness RED; already-correct shape checks reported test-only; no Web/DB/provider startup for Core-only T005. Later PG uses only the dedicated verified Story13.1 container and private connection file without logging credentials.
5. write_boundary: RetainedSourceFixture.cs, RetainedSourceTestFixtures.cs, RetainedSourceSeparateProcessTests.cs, RetainedSourceSeparateProcessChild.cs, and RetainedSourceApplicationContractTests.cs only.
6. output_and_evidence: exact public API tests, focused command/result/failures, hashes and scaffold checks returned; no implementation or qualification overclaim.
7. completion_and_failure: stop after honest runtime RED for producer dispatch, then same author returns for focused/surrounding GREEN; no stage/commit/state writes.

User reported starting Docker and database. Read-only inspection found existing vulgata-postgres; it was preserved. Root created separately owned vulgata-story131-34705378c4 using already-installed postgres:17-alpine, verified labels/container ID/loopback port5511 and PostgreSQL17.10 readiness. Runtime boundary evidence contains only non-secret facts and the private OS-temp connection-file path. No shared database credentials were reused.

## Identity TDD group verified

T001-T007 complete. T005 runtime RED was22 failing invalid-value cases plus3 existing-correct passes; minimal T006 validation changed only SourceSnapshot.cs; same test author verified25/25 focused and109/109 surrounding domain tests. Evidence: identity-red-01.json, identity-producer-green-01.json and identity-verified-green-01.json. Fixture-only BOM/reparse cleanup fixes preserve test ownership. No capture, persistence, source read or runtime qualification is claimed yet.

T006 and T007 dispatches used the same seven-slot contract: raw test/source hashes and accepted model as inputs; Sol/medium production owned only SourceSnapshot.cs (RetainedSourceContracts.cs only if required, untouched); Luna/high test author owned the five test/scaffold files; no delegation/state/Git writes; returned focused and surrounding evidence.

Pending coherent identity checkpoint: bmad-13-1-implement-20260915T020953Z-28b894. Next work is T008/T009/T014 integrated capture/policy/failure/PostgreSQL RED, then only the minimum accepted production chain required by those tests.

## Integrated capture RED dispatch

1. role_and_objective: same tdd-test-author Luna/high authors T008/T009/T014 against actual production composition and owned Git/PostgreSQL fixtures.
2. authoritative_inputs: accepted definition/current Core contracts, VulgataWebComposition, Identity/management authority, existing context factory/migrations, testing/TDD/constitution and runtime ownership evidence.
3. workflow_boundary: implement after bound identity checkpoint cbd1b498a975295ed1c474da140631d930a0d294. No source acquisition/store/EF mapping implemented yet.
4. constraints_and_skills: no delegation; compiled runtime missing new capability is accepted bootstrap, not compile/environment RED. Real application seams, no mocks of source internals, no premature open/read dependency or full Web/tool/provider runtime.
5. write_boundary: RetainedSourceCaptureTests.cs, RetainedSourcePostgreSqlTests.cs and retained-source test fixture helpers only; unique databases inside dedicated task container only; no secrets in repo/logs.
6. output_and_evidence: exact runtime RED for explicit success/policy/failure/auth/ref/mutation/cancellation/cleanup/PostgreSQL-constraint cases, commands/hashes/DB names/cleanup outcomes.
7. completion_and_failure: stop at meaningful RED for Sol/medium producer; no production/state/Git edits or stage/commit/delegation.

## Persistence RED and split work

The test author observed4 compiled runtime PostgreSQL failures for missing SourceSnapshot model/table/DbSet, with guarded unique-database setup/cleanup succeeding. Raw evidence is persistence-red-01.json. Capture tests still have setup gaps and claim no RED; root did not treat a missing POSTGRES_TARGET variable as a user blocker because the owned admin connection/helper can create it.

1. role_and_objective: Sol/medium producer implements independently ready T015/T016 EF mapping/migration only; Luna/high test author repairs capture-specific application fixture in parallel.
2. authoritative_inputs: frozen PostgreSQL test hash5c1c83c0f2decfb98101f8fe642c89301ced36090ae03773d0b63aa23908f483 and raw persistence RED; accepted source model/plan and current Web/Identity composition APIs.
3. workflow_boundary: same Story/feature at cbd1b498a975295ed1c474da140631d930a0d294; no capture behavior claimed. Production Web builds and static capture-test edits have disjoint source trees; test-project builds wait for coordination.
4. constraints_and_skills: current TDD/loop, no delegation; producer preserves SQLite harness and PostgreSQL semantics, uses design-time factory only to generate migrations, never its default connection for runtime updates. Test author creates/migrates/seeds a real unique target via existing helper, uses real user/Repository/role state, full symbolic refs and real source authorization; no dead callback mocks or unseeded placeholder data.
5. write_boundary: producer SourceSnapshotConfiguration.cs, VulgataDbContext.cs, new source migration/designer/model snapshot only; test author capture tests and capture-specific helper files only, frozen PostgreSQL tests unchanged.
6. output_and_evidence: producer returns source-owned migration/build readiness before tests; test author returns static fixture readiness before tests. Root schedules subsequent focused run serially.
7. completion_and_failure: no capture GREEN, no state/Git writes by workers, no unowned DB changes; raw blockers returned for bounded correction.

## Source configuration and capture production dispatch

Fresh current capture test run:27 total,10 invalid configuration GREEN,17 remaining missing application-service RED. The corrected fixture is real/migrated/seeded; its future mutation/time hooks are present but have not executed while the service is missing. Current raw evidence: capture-red-02.json.

1. role_and_objective: Sol/medium producer implements the minimum T011/T012/T013/T017/T018 capture chain needed by the observed runtime RED.
2. authoritative_inputs: raw capture-red-02/test+fixture hashes, accepted full definition, current validated policy/schema and actual Core interfaces.
3. workflow_boundary: same feature/HEAD with verified EF and config prerequisites; tests frozen and no competing builds.
4. constraints_and_skills: current TDD/loop, no delegation; raw Git objects, server subject/live fresh authority, exact ref/bytes, bounded/cancellable work, atomic seal then immutable row, safe owned cleanup and sanitized outcomes; no ordinary reads/workspace/other-story implementation yet.
5. write_boundary: GitCommittedObjectReader, DurableRetainedSourceStore, RetainedSourceCaptureService, minimum manifest codec/types, RetainedSourceRepository, SourceRepositoryAuthorizationVerifier, DI and Web composition; no tests/schema/intent/state/Git changes.
6. output_and_evidence: focused capture and frozen PG results with private owned runtime env; source hashes, cleanup/publication handling and raw blockers; no malformed-test accommodation.
7. completion_and_failure: minimal integrated GREEN or specific blocker, then same test-author independent verification; no stage/commit/delegation.

## Initial capture verification and boundary regression dispatch

Producer reached26/27 capture and4/4 PostgreSQL after root recreated the vanished auto-remove test container within the same authorized isolated scope. Updated exact resource identity is in runtime-boundary-01.json; no shared container/database was changed. The sole capture failure was an Administrator fixture incorrectly expecting denial after only SystemOwner-assignment removal. Same test author corrected it to a SystemOwner-only principal, then independently observed27/27 capture,4/4 PostgreSQL and16/16 surrounding Git/UserContext tests. Evidence: capture-producer-01.json and capture-verified-green-01.json. Final independent runs lacked persisted raw logs; their direct tool-observed counts are recorded without claiming an attachment.

T019 is not accepted yet. Root and producer identified remaining streaming, active deadline and final lock-time publication checks; implementation-observations-01.md records them with configuration edge cases. No additional implementation approval is needed: these enforce the unchanged accepted plan.

1. role_and_objective: Same Luna/high test author adds bounded public-seam capture regressions for the recorded gaps.
2. authoritative_inputs: Raw implementation-observations-01.md, accepted plan sections2/3/7/8, FR005/014/019/020, current frozen production and green tests.
3. workflow_boundary: HEAD cbd1b498a975295ed1c474da140631d930a0d294; initial capture tests green but T019 incomplete; owned replacement PostgreSQL fixture only.
4. constraints_and_skills: Current TDD; actual authorization and owned Git/DB fixtures; no implementation-mirroring assertions, fabricated RED, delegation or concurrent production edits. Resource streaming needs measurable behavior or an honest verification-method report.
5. write_boundary: RetainedSourceCaptureTests.cs, RetainedSourceCaptureApplicationFixture.cs, RetainedSourceFixture.cs and optional RetainedSourceCaptureBoundaryTests.cs; PostgreSQL helper portability only, retaining loopback/unique-DB safeguards.
6. output_and_evidence: Persist unique raw log/TRX files in private runtime results directory; exact expected/actual, RED versus characterization, commands/exits/counts/hashes and hook-execution evidence.
7. completion_and_failure: Return observed RED or precise seam obstacle for minimal Sol/medium production correction; no later read/workspace group, lifecycle/state/Git writes or commits.

## Capture boundary corrections

Policy boundary tests observed3RED: filesystem root normalization and unrepresentable capture/stale durations. Sol/medium changed only RetainedSourceCapturePolicy; same Luna/high author independently verified3/3 and27/27 capture. Next owned stalled-HTTP-Git test observed a real deadlineRED: request reached endpoint, but10s watchdog returned Cancelled instead of configured1s DeadlineExceeded. Producer added a monotonic chunked deadline monitor in capture service, retaining caller cancellation precedence and noncancellable row insertion. Same author independently verified4/4 boundary and27/27 capture. Evidence: capture-policy-red-01.json, capture-deadline-red-01.json and capture-deadline-producer-green-01.json; raw verification log/TRX paths are recorded in capture-lock-red-01.json.

The next public capture regression held an owned identity lock, observed real pre-lock authorization, revoked a SystemOwner-only subject's assignment, then released the lock. Actual Completed/visible row violated expected Denied/no row. Producer added fresh authority/binding/cancellation/deadline checks after sealing before row insertion and after replay verification before disclosure. Producer1/1 focused,5/5 boundary,27/27 capture GREEN. Current service SHA2B5193A88EC78E3CFF0B4D1C4D058AAA708F1BAE800A88426DB38AFB62A68D68. Independent verification and policy-root-overlap/cancellation characterization are dispatched. T019 remains pending, with streaming still unresolved.

All correction dispatches use the same seven slots: (1) named test/production role and single bounded objective; (2) accepted plan plus raw test/log/hash findings; (3) unchanged HEAD and approved feature boundary; (4) TDD/no delegation/no scope expansion; (5) disjoint exact test or production files; (6) raw counts/exits/log/TRX/hashes; (7) return at RED/GREEN for root routing, no state/stage/commit. No production worker edits tests and no test author edits production. Independent test runs and production builds are serialized.

## Initial capture group verified

T008-T019 complete after honest configuration, stalled-Git deadline, final-authority-lock and bounded-streaming RED/GREEN corrections. Independent current verification: resource1, boundary7, capture27, PostgreSQL4, surrounding16 all pass. Root parsed raw TRX counters and recorded full hashes in capture-group-verified-01.json.64MiB source payload allocated78,413,312 managed bytes, below134,217,728 ceiling. No ordinary reads/workspace/fresh-process qualification yet.

Pending checkpoint: bmad-13-1-implement-20260915T045638Z-d487d3. Next T020/T021 canonical manifest detail, then scoped Repository deletion and read-context groups. No other story is activated.

## Manifest characterization verified

T020/T021 complete as test-only existing-correct behavior:3manifest,27capture,16surrounding tests pass. Raw Git mktree binary-input fixtures preserve unsafe/nonUTF8 names without checkout. Canonical bytes/ordering/accounting, export-attribute immunity and explicit symlink/gitlink/path limitations verified. No production write or fabricated RED. Evidence manifest-verified-01.json; checkpoint bmad-13-1-implement-20260915T051458Z-02d72f. Next T022/T023 Repository deletion conflict.

## Retained-source deletion conflict TDD

T022 observed actual owned-PostgreSQL2RED/1GREEN: coordinator deletion fails before SaveChanges while severing a tracked required SourceSnapshot relationship; endpoint deletion reaches precise PostgreSQL23503 FK_SourceSnapshots_Repositories_RepositoryId; retained rows remain. Ordinary no-source deletion passes. Test fixture clears Windows EventLog providers so host logging permissions do not mask product failure. Raw evidence deletion-red-01.json.

1. role_and_objective: Sol/medium producer implements T023 sanitized retained-source conflict behavior.
2. authoritative_inputs: Accepted plan section1/FR017018 and raw deletion test/fixture/TRX hashes; current2f5dcd60 checkpoint.
3. workflow_boundary: Only Story13.1, current source/tests frozen; dedicated owned PostgreSQL fixture.
4. constraints_and_skills: Current TDD; preserve authorization/not-found, exact constraint race handling, immutable retained rows, Chinese409; no broad InvalidOperationException/FK swallowing, UI/DTO/new endpoint, delegation or tests/Git edits.
5. write_boundary: Web RepositoryApiEndpoints and RepositoryManagementCoordinator; root approved narrow HasForRepositoryAsync query addition to existing Core IRetainedSourceRepository and Infrastructure RetainedSourceRepository after producer explained tracked-relationship failure. This implements accepted persistence ownership, not a new plan/architecture decision. No nullable fallback to avoid test adaptation.
6. output_and_evidence: Focused deletion and surrounding Repository/PG/capture checks, logs/TRX/full hashes; any constructor/fake test adaptation returns to test author.
7. completion_and_failure: ReturnGREEN or precise blocker; root alone records state/tasks/checkpoints/commits.

## Scoped metadata reads verified; baseline gate remains pending

T024/T025 complete:24read,27capture,3manifest,4PostgreSQL tests pass. T023 focused conflict3/3 and isolated-key login12/12 also pass;5 unchanged pre-story baseline API/management tests still block its strict surrounding gate. User scope question is pending; no old tests are changed/skipped and no gate is waived. Evidence source-read-verified-01.json and baseline-verification-blocker-01.json. T023 remains unchecked.

Recovery checkpoint bmad-13-1-implement-20260915T065526Z-efd086 records verified source increments and their honest pending gate. Next dependency-ready work is T026/T027 byte/text reads. It is not a converged/reviewed/completed story.
