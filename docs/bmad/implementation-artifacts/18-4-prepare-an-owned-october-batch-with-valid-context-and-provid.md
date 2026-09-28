# Story 18.4 development record

Status: in-progress (definition and planning only)
Feature directory: `specs/016-october-batch-setup`
Workflow state: `specs/016-october-batch-setup/workflow-state.json`

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

## Workflow bookkeeping

- Activation transition: `specs/016-october-batch-setup/evidence/transitions/transition-0001-activation.json`.
- Preflight: `specs/016-october-batch-setup/evidence/preflight-01.json`.
- Activation checkpoint will include only lifecycle/definition setup records; specification and plan follow under their own governed stage evidence.
