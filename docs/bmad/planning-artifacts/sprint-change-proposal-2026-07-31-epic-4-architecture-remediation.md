# Sprint Change Proposal: Epic 4 Architecture Remediation

**Date:** 2026-07-31
**Trigger:** Epic 4 close architecture audit at source commit
`1b9c98bb4999e254ab19704643477732f92c842c`
**Mode:** Batch
**Scope classification:** Moderate direct adjustment within Story 4.3
**Execution level:** `expedited-mvp`
**User decision:** Approved the narrow remediation plan on 2026-07-30 and
directed execution on 2026-07-31.

## 1. Issue Summary

Epic 4's exact-source architecture audit returned `REVISE`. Three findings are
reserved because each is `HIGH`, violates a Story 4.3 MUST, and carries a
Strong recommendation:

1. `epic4-framework-snapshot-representation-mismatch`
2. `epic4-empty-prescan-compatibility-path`
3. `epic4-profile-only-persistence-path`

The implementation otherwise passed the clean detached Web build, the complete
615-test suite, and Docker verification. The findings are integration-seam
defects rather than new product requirements:

- the profiling coordinator supplies the materialized committed snapshot
  directory to framework detection, while the detector treats that directory
  as a Git working tree and invokes Git inventory/blob operations;
- two public coordinator constructors install an empty Pre-Scan detector that
  creates `pre_scan_approved` without performing Communication Kind
  Classification; and
- `IScanRunRepository` still exposes a profile-only persistence operation in
  addition to the required atomic profile-plus-Pre-Scan Result operation.

## 2. Change Navigation Checklist

### Trigger and evidence

- [x] 1.1 Triggering story: Story
  `4-3-conservative-pre-scan-gate-and-communication-type-detection`.
- [x] 1.2 Category: technical integration limitations discovered during the
  Epic 4 architecture gate.
- [x] 1.3 Evidence: the canonical audit envelope at
  `docs/bmad/implementation-artifacts/epic-gates/4/audit-raw/dispatch-2.json`,
  independently verified against its pinned Git blobs and excerpts.

### Epic impact

- [x] 2.1 Epic 4 remains viable after a narrow correction.
- [N/A] 2.2 No Epic scope, acceptance-criterion, or story inventory change.
- [x] 2.3 Epic 11 depends on the corrected Pre-Scan Handoff and benefits from
  one production-equivalent profiling path.
- [N/A] 2.4 No future epic becomes obsolete and no new epic is required.
- [N/A] 2.5 Epic ordering remains unchanged. Story 11.1 continues to hold
  migration/model-snapshot mutation until Story 4.3's completed checkpoint.

### Artifact conflicts

- [x] 3.1 PRD: no requirement change. The correction restores the current
  deterministic, Profiled Revision-bound, atomic Pre-Scan contract.
- [x] 3.2 Architecture: update the Story 4.3 plan set so framework detection
  consumes the already-verified committed snapshot representation and the
  public module exposes no bypass seams.
- [N/A] 3.3 UX: no UI or user-flow change.
- [x] 3.4 Tests: replace compatibility-path construction with the required
  detector/snapshot seams and add one production-composition regression.
  Docker/runtime configuration and the existing additive migration are
  unchanged.

### Path evaluation

- [x] 4.1 **Direct Adjustment — selected.** Moderate implementation effort,
  low product-scope risk, and approximately 3–6 hours plus final verification
  and re-audit.
- [N/A] 4.2 Rollback is not justified; Stories 4.1–4.3 contain accepted
  behavior that the correction preserves.
- [N/A] 4.3 PRD/MVP reduction is not justified; waiving the framework mismatch
  would leave the demonstration profiling path fail-closed before framework
  facts are produced.
- [x] 4.4 Keep all three corrections in the existing Story 4.3 SpecKit feature
  so the dated `expedited-mvp` authority remains valid.

## 3. Impact Analysis

### Epic and story impact

Epic 4 remains `in-progress` until the corrected source passes Story 4.3's
review and verification gates and a fresh Epic 4 architecture audit. Story
4.3's functional requirements and success criteria do not change.

Epic 11 receives no additional scope. Its consumers continue to validate the
Pre-Scan Handoff's exact Profiled Revision. Story 11.1 retains its existing
hold on `VulgataDbContext`, migration, and model-snapshot edits until the
Story 4.3 completed checkpoint is available.

### PRD, architecture, and UX impact

- **PRD:** no edit.
- **Canonical architecture:** no product-level decision changes. The
  implementation is brought into conformance with the already-approved shared
  committed-snapshot and atomic-persistence design.
- **Story 4.3 SpecKit plan set:** revise the detector input seam, constructor
  policy, persistence interface, and verification coverage.
- **UX:** no edit.

### Technical impact

The correction changes internal Scan profiling behavior and therefore triggers
the repository's planned-change authorization after independent plan
acceptance and before task generation. It adds no Worker Agent tool, dispatch,
scheduling, retry, provider-failover, PRD weakening, migration, or external
dependency.

## 4. Recommended Approach

Use one Story 4.3 correction with four bounded outcomes:

1. Make `IFrameworkEvidenceDetector` consume the verified committed Repository
   snapshot contract rather than rediscovering Git identity from the
   materialized archive directory. Framework evidence readers must operate
   only on the same Profiled Revision-bound snapshot already used by
   CodeGraphContext and Communication Kind Classification.
2. Remove the public coordinator construction paths that install
   `EmptyPreScanCommunicationDetector`, and remove that empty implementation.
   Every coordinator instance must receive the real
   `IPreScanCommunicationDetector` and committed snapshot service.
3. Remove the profile-only `PersistCompletedProfileAsync` contract and
   implementation. The only final profile persistence operation accepts
   `RepositoryProfileCommitDraft` and atomically commits profiling facts with
   the Pre-Scan Result.
4. Add a production-composition regression that exercises committed snapshot
   acquisition, framework detection, communication detection, and atomic
   persistence through the public interfaces. Convert affected tests away
   from bypass constructors. Existing focused detector tests remain.

The medium catalog-locality recommendation is deferred because it does not
violate a runtime MUST and is not necessary to make the demonstration path
correct.

## 5. Detailed Change Proposals

### Story 4.3 plan: committed snapshot seam

**OLD**

> The coordinator gives one immutable snapshot root to both CodeGraphContext
> and all detector readers.

The interface nevertheless accepts a path string, allowing framework
detection to reinterpret the extracted directory as a Git checkout.

**NEW**

> The coordinator gives the verified `ICommittedRepositorySnapshotLease` to
> framework and communication detection. Framework detection inventories and
> reads bounded supported artifacts through the materialized snapshot
> representation and never runs Git against `RootPath`. The coordinator
> verifies snapshot identity before profiling and again before atomic
> persistence.

### Story 4.3 plan: required detector construction

**OLD**

> Production DI supplies the real detector, while compatibility constructors
> may substitute an empty detector.

**NEW**

> Every public `RepositoryProfilingCoordinator` construction path requires
> `IPreScanCommunicationDetector` and
> `ICommittedRepositorySnapshotService`. No empty approval adapter or
> missing-snapshot compatibility constructor exists.

### Story 4.3 plan and data model: atomic persistence

**OLD**

> `IScanRunRepository` exposes both profile-only and atomic final profile
> persistence operations.

**NEW**

> `IScanRunRepository` exposes one final persistence operation taking
> `RepositoryProfileCommitDraft`. Final persistence always commits the
> complete profile and Pre-Scan Result in one transaction. No public
> profile-only final commit exists.

### Story 4.3 verification

**OLD**

> Framework detector tests may construct the implementation directly and
> Story 4.3 integration tests may manually skip production composition seams.

**NEW**

> At least one regression crosses the production interfaces from verified
> committed snapshot acquisition through framework and communication
> detection to atomic persistence. Tests prove an archive-only snapshot does
> not require `.git`, no coordinator can auto-approve without detection, and
> no profile-only final persistence seam remains.

## 6. Implementation Handoff

**Plan owner:** a fresh `plan-architect` revises the existing
`specs/003-conservative-pre-scan-gate/` plan set without changing accepted
Story 4.3 product scope.

**Plan judge:** a fresh `plan-judge` evaluates feasibility, architecture,
security, migration impact, and testability against the canonical audit and
current PRD.

**Human gate:** after `ACCEPT`, the root pauses for explicit Scan-strategy
authorization before tasks.

**Implementation owner:** fresh `tdd-implementer` instances execute bounded
task groups with observed RED then GREEN. The root remains sole committer.

**Success criteria:**

- all three reserved architecture findings are removed at their public seams;
- no PRD, migration, authorization, or Worker Agent behavior changes;
- focused tests, full tests, Web build, and Docker verification pass;
- a fresh exact-source Epic 4 architecture audit returns no unresolved
  reserved finding.
