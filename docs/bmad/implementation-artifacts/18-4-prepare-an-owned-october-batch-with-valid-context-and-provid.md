# Story 18.4 development record

Status: in-progress (definition and planning only)
Feature directory: `specs/017-october-batch-setup`
Workflow state: `specs/017-october-batch-setup/workflow-state.json`

## Lifecycle story

### Story 18.4: Prepare an Owned October Batch with Valid Context and Provider Bindings

As a batch operator,
I want one valid isolated batch setup shared by the generator and October Web,
So that generation and later readers address the intended data without polluting normal or other batch data.

**Requirements:** O-FR01, O-FR02, O-FR17, O-FR18, O-FR19; O-AR01-O-AR04, O-NFR03.
**Completion dependencies:** 18.1.
**Parallel start:** source identity and wiki/setup F0; no JDT/CodeGraph or model analysis dependency.

**Acceptance Criteria:**

**Given** an explicitly authorized owned database target and real operator identity,
**When** programmatic October setup is requested,
**Then** valid System/Repository/context bindings and October batch metadata are created through shared rules,
**And** only this story's setup records/mappings are introduced; no fake administrator, dummy pair/FlowGroup, forced ScanRun or new CRUD UI is used.

**Given** two distinct owned batch databases with identical fixed schema names,
**When** generator and October Web start with an exact database and batch ID,
**Then** both bind the selected setup and reject missing/mismatched selection without previous/latest-batch fallback,
**And** a switch requires restart; no read/write crosses into the other target.

**Given** selected Global/System/Repository context and explicit provider configuration,
**When** setup is bound,
**Then** actual context values/origins and conflicts are retained, and API type/Worker/Chat role profiles and configuration precedence are explicit,
**And** no unspecified `.env` discovery, borrowed credentials, silent field mixing or automatic failover occurs; protected-row copying requires compatibility only if that route is used.

**Given** October enabled versus ordinary startup,
**When** mappings/migrations and registration are evaluated,
**Then** the October persistence context, migration history and model snapshot are isolated; ordinary startup and the normal model remain unchanged,
**And** setup emits sanitized diagnostics without claiming tool readiness, paid-provider readiness or completed Knowledge generation.

## Authorized boundary

- The user authorized Story 18.4 definition and planning only. Task generation, implementation, production/test changes, runtime/provider use and migration execution are prohibited in this run.
- Independently decidable planning covers owned batch setup, exact database and batch selection, context origin/conflict handling, explicit provider-role binding/configuration precedence, and isolation from ordinary startup and other batch databases.
- Story 18.1 is actively implemented in a different checkout. This worktree must not inspect or modify that checkout, duplicate its source-access contracts, invent source identity/tool contracts, or claim the dependency is satisfied.
- The specification and plan must identify every seam that consumes Story 18.1's accepted source identity/tool contracts. Those seams remain unresolved dependencies until an integrated, verified 18.1 baseline is present in this worktree and the plan is reconciled against its actual accepted contracts.
- Delivery must stop after an independently reviewed plan. No planned-change authorization is requested merely to document the dependency, and no tasks may be generated. Before any later continuation, root must verify the integrated 18.1 baseline, reconcile specification/plan assumptions and hashes, repeat affected review, and satisfy any then-applicable planned-change authorization.

## Activation and ownership

- Observed source commit: `5e5553374b342fb658776fbfcb86c6e06513832a`.
- Assigned checkout: `C:/Users/13728/.codex/worktrees/d166/vulgata`.
- The app/user supplied this separate managed worktree as a native handoff. It is detached at the supplied commit; no branch mutation was performed.
- Initial index/worktree was clean. The root owns lifecycle state, evidence, exact-path staging and checkpoints. Definition workers may write only the current feature's stage-scoped definition artifacts and may not stage or commit.
- Lifecycle advanced from backlog through accepted story readiness to in-progress for Story 18.4 only; Epic 18 is in-progress. Story 18.1's tracker entry and separate implementation remain untouched.

## Dependency hold

Definition and plan authoring may proceed using only accepted repository authorities and the explicit dependency boundary above. Task generation and every later delivery stage are held pending an integrated, verified Story 18.1 baseline and plan reconciliation against its actual accepted source identity/tool contracts.

## Specification result

- The persistent definition author completed `specs/017-october-batch-setup/spec.md` and its requirements checklist. Root verified the returned hashes and the checklist's complete result.
- The specification makes owned setup, exact database/batch selection, context provenance/conflicts, provider-role binding and October/ordinary isolation independently plannable.
- FR-011, SC-006 and the Prepared Source Binding entity keep Repository/root/source identity, source grant and contained read-only tool binding explicitly dependent on Story 18.1. They do not define or claim those contracts.
- Specification self-review: `specs/017-october-batch-setup/evidence/spec-self-review-01.json`.
- No application/test/runtime/provider/database work occurred. The optional agent-context hook was not run because the stage write boundary excluded it.

## Architecture plan result

- The ordinary definition author classified the plan as `architecture` before writing because the accepted story requires a dedicated October persistence boundary and a shared generator–Web exact-selection contract. Plan authorship therefore moved to the required retained `plan-architect-high` profile; the ordinary author remains retained for later non-architecture phases.
- The plan selects one explicitly owned PostgreSQL database per batch, a fixed `october` schema and separate EF context/migration history/model snapshot, one shared setup/selection policy, exact immutable process-lifetime database/batch selection, retained context origins/conflicts, and complete explicitly sourced Worker/Chat provider profiles.
- Ordinary Web Identity/domain composition and migrations remain separate and unchanged. No new package, generator executable, CRUD UI, provider/database execution, source/test/migration edit or task is introduced by planning.
- Plan IDs `ID-I04`, `ID-L03` and `ID-T04` reserve the prepared-source adapter, reuse and verification seams for the actual integrated 18.1 contracts. They deliberately leave contract names, storage shape, cardinality and grant semantics unspecified.
- Deterministic plan-content validation passed the architecture front matter, required headings and stable ID shapes. All specification requirements and success criteria map to design IDs or the explicit 18.1 hold.
- Plan self-review: `specs/017-october-batch-setup/evidence/plan-self-review-01.json`.
- The architecture classification requires independent plan-only review. Current plan content does not itself establish an AGENTS.md Worker-tool, Scan-strategy or PRD-change authorization trigger; root and reviewer must reassess after 18.1 reconciliation.
- The optional provider-source preference remains nonblocking: the draft uses one explicitly supplied, source-neutral configuration bundle per process, with no unspecified `.env` discovery or protected-row copy.

## Independent plan review and correction

- Plan review 01 returned `REVISE` with one HIGH plan-owned finding: the first draft named an authenticated operator and owned target but did not define non-forgeable authority sources for October Web and the future headless generator. The recovery episode retains one failed result; no production started.
- The retained architecture plan owner corrected the seam. Existing Web Identity and live `ManagementAccess` issue a short-lived, purpose-bound activation grant; October Web verifies it locally and the generator validates it through Web. Deployment-owned allowlists map opaque target IDs to canonical database descriptors, both hosts verify connected database identity before October migration/setup, and protected use repeats live authorization. Standalone Repository setup requires current management authority and a real `SystemId == null` Repository. This setup grant remains separate from Story 18.1 source grants.
- Plan review 02 independently returned `ACCEPT` with no findings and confirmed the architecture classification, design IDs, corrected trust boundary, testability, absence of a current Worker-tool/Scan-strategy/PRD planned-change trigger, and continued 18.1 hold.
- The raw accepted plan verdict is preserved at `specs/017-october-batch-setup/evidence/plan-review-02.json`. The complete-definition recovery episode cannot close until tasks and the later full definition review exist. Because the user prohibited task generation in this run, workflow state remains before tasks and no planned-change authorization or implementation approval is requested.

## Ready boundary and required resume

- Ready now: reviewed specification and architecture plan for owned setup, exact database/batch selection, context provenance/conflicts, explicit provider profiles, separate October persistence/migrations, ordinary-startup isolation, non-forgeable activation authority, and controlled/PostgreSQL verification seams.
- Still dependent on 18.1: `FR-011`, `SC-006`, `ID-I04`, `ID-L03`, `ID-T04`, the prepared-source relationship/cardinality, source grant lifetime, contained read-only tool association, source failure/cancellation mapping and their tests.
- Before task generation or any later delivery, integrate and verify Story 18.1 in this checkout, inspect its accepted spec/plan/review, production contracts, tests and verification evidence, replace the deferred source-binding assumptions, reconcile the affected spec/plan through the earliest owner, rerun deterministic validation and independent plan review, and reassess the repository planned-change triggers. Only then may the retained definition author generate tasks under a newly permitted continuation.
- No implementation tasks, source/test/migration/configuration changes, database/provider/runtime activity or implementation approval occurred.

## Planning checkpoint

- Pending correction checkpoint: `bmad-18-4-plan-correction-20260928T092111Z-d166`.
- Planned manifest: this development record, current feature definition/plan set, workflow state and current raw self-review/review/transition evidence. The ignored local `.specify/feature.json` selector is not part of the commit.

## Workflow bookkeeping

- Activation transition: `specs/017-october-batch-setup/evidence/transitions/transition-0001-activation.json`.
- Preflight: `specs/017-october-batch-setup/evidence/preflight-01.json`.
- Activation checkpoint will include only lifecycle/definition setup records; specification and plan follow under their own governed stage evidence.

## Feature directory relocation

- On 2026-09-28 the user reported that another runner had occupied sequential feature number `016` and directed this feature to move to `017`.
- The live feature mapping is now `specs/017-october-batch-setup`. Specification and plan semantics, recovery count, author ownership, raw review outcomes and the Story 18.1 hold are unchanged.
- Repository-relative evidence paths and their dependent hashes were mechanically rebound. The original exact bytes remain recoverable in commits `f693f2c5`, `acc74c51` and `30936b94`; relocation evidence is `specs/017-october-batch-setup/evidence/feature-directory-relocation-01.json`.
- Existing Git history was not rewritten. A normal rename commit is safer for later merge/rebase because it preserves stable commit identities and lets Git apply rename detection against the other runner's current tree. History rewriting would not remove ordinary overlapping-content conflicts and would make shared checkpoint references stale.
- Pending relocation checkpoint: `bmad-18-4-feature-relocation-20260928T153541Z-d166`.

## Delegated authority registration

- Verified user sources register this runner as stable chat `01a0e728-b7cc-78f1-8a3d-d0d49c7dfde2` and the October approver as stable chat `01a0e708-c489-7871-9f73-31413f20bf2c`; later title matching is not authority.
- The designation covers the two existing delegated gates and permits consideration of a fresh finite recovery-extension decision at a reached pause limit, with cumulative `max_pause_at` no greater than 7. It is not an automatic extension and does not add seven attempts.
- Story 18.4 remains explicitly PLAN-ONLY. Current recovery remains `failed_results: 1`, `pause_at: 4`; no gate, extension, task generation, implementation or resume is approved.
- Recovery-extension delegation is not usable in this checkout yet: governed-delivery 0.7.3 and the current Vulgata consumer policy explicitly exclude it. Compatible 0.7.4 assets and policy must be integrated and validated before a future request can rely on that purpose.
- The exact provenance, conditions and non-effects are recorded in `specs/017-october-batch-setup/evidence/delegated-authority-registration-01.json`. Any later use requires current raw evidence and a fresh delegate response; cumulative count/limit 7 returns to the human.
