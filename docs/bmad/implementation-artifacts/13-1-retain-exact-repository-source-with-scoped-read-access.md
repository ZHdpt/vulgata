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
