# Sprint Change Proposal: Story 12.5 Exact-Profile Seed Selection

**Date:** 2026-08-31
**Story:** 12.5 — Grounded LLM-Wiki Chat & August 30 Release Evidence
**Mode:** expedited batch
**Classification:** moderate, release-blocking Scan-strategy correction
**Status:** approved under `story-12-5-run-03-all-remaining-tasks-approval-02`

## 1. Issue summary

The real run-03 Prepared Scan and Pass 1 completed honestly, but the accepted
exact Cross-Repository Resolution coordinator returned `Refused`. This is not a
CodeGraph 1.5, JDT-LS, provider, or Story 12.4 resolver defect. The generic
Prepared Scan selection admitted 150 File Code Units, all from `mbs_channel`,
and retained zero high-value seeds. The `mbs_account` Manifest therefore
contained no selected File Code Unit.

The reviewed exact-resolution profile requires four source files. Immutable
Prepared Scan audit rows show that only the provider implementation file was
selected; the consumer invocation, provider XML declaration, and unsupported
observation owner were eligible but excluded by the 150-unit cap. Their
per-Repository normalized fallback ranks were 429/1212, 763/2292, and 204/1212.
The coordinator correctly failed closed because the required current
source-anchored Code Logic Documents did not exist.

## 2. Impact analysis and checklist

| Checklist area | Result |
| --- | --- |
| Trigger/context | Done — Story 12.5 T036 real run exposed a cross-stage selection prerequisite absent from tests that pre-seeded exact Documents. |
| Epic impact | Action needed only in Epic 12 / Story 12.5. Stories 12.2-12.4 remain accepted and are not rewritten. |
| PRD impact | The August overlay promises exact selected Cross-Repository Resolution but does not require its reviewed source files to survive the selection cap. |
| Architecture impact | Section 4.3 currently forbids a sample profile from selecting seeds. That conflicts with the fixed reviewed exact profile and bounded cap. |
| UX impact | None. Existing Chinese Scan and Chat UI behavior remains unchanged. |
| Data/schema impact | None. Existing File Code Unit, Manifest entry/audit, Pass 1, Document, Edge, and Uncertainty tables are sufficient. |
| Security impact | The correction is server-owned, revision-bound, closed-profile-only, authorization-preserving, and fail-closed. No request may supply paths or a profile. |
| Option 1 — direct adjustment | Recommended; medium effort, medium risk. Add a narrow reviewed-profile seed authority before the existing pure planner. |
| Option 2 — rollback | Rejected. Track A/B, Prepared Scan initiation, Pass 1 fixes, and Story 12.4 remain valid evidence. |
| Option 3 — reduce MVP | Rejected. Exact Cross-Repository Edge, honest Uncertainty, and Cross-Repository Business Logic Document are mandatory retained release value. |

## 3. Recommended approach

Add one server-owned, read-only exact-profile seed authority that derives the
mandatory source paths from the existing embedded reviewed
`ExactResolutionProfile`. It activates only when the complete Prepared Scan
pair matches the profile's exact two Repository stable identities, full Git
refs, and Profiled Revisions. No caller-supplied profile, alias, path, score, or
selector is accepted.

Before `PreparedScanPlanner.Plan`, the pair builder marks every eligible bounded
File Code Unit whose repository/path is required by that matched profile as a
new closed `ReviewedExactResolutionProfile` business-evidence category. These
units participate in the existing high-value-seed count and existing maximum
20 seeds per Repository. The normal deterministic score/path ordering, 150-unit
pair bound, CodeGraph expansion, audit creation, Manifest publication, Pass 1
dispatch, exact resolver, and Pass 2 remain unchanged.

Activation is all-or-none. A profile mismatch has no selection effect. A full
profile match with a required path missing, filtered, unsafe, ambiguous across
Repositories, or exceeding existing seed/split bounds rejects preparation
before Manifest publication. Exact replay remains deterministic because the
same reviewed profile and exact revisions produce the same candidate scores,
selection, audit rows, package fingerprint, and Manifests.

## 4. Exact authority edits

### PRD August overlay

**Old:** 50-150 automatically selected File Code Units and later exact selected
Cross-Repository Resolution are independent bullets.

**New:** for the fixed reviewed release profile and only its exact two
Repository revisions, every safe eligible source file required to prove the
consumer, provider declaration, provider implementation, and honest unsupported
observation is a mandatory high-value seed inside the existing bounds.

### Epic 12 / Story 12.5

**Old:** the real run requires 50-150 automatically selected File Code Units,
every seed successful, then exact evidence.

**New:** the automatic selection must include all safe eligible source files
required by the matched reviewed exact profile; absence or ambiguity blocks
Manifest publication rather than allowing a later impossible exact-resolution
run.

### August architecture section 4.3

**Old:** `No LLM, PageRank, clustering, or sample profile chooses seeds.`

**New:** no caller-supplied or arbitrary sample profile chooses seeds. One
embedded reviewed release profile may contribute mandatory high-value seed
paths only after exact pair/ref/revision identity match. Production code reads
the profile contract and contains no sample Repository name, path, endpoint, or
line number.

### Story 12.5 SpecKit definition

Add a closed selection-precondition slice and invalidate only the uncompleted
run-03 release lane. Preserve run-03 as historical diagnostic evidence. A fresh
guarded lowercase database and complete real run are required after the
correction passes definition, TDD, convergence, quality, and E2E gates.

## 5. Implementation handoff

### Proposed production ownership

- `Vulgata.Core`: one closed evidence enum value and bounded seed-selection
  contract if required; no persistence/model change.
- `Vulgata.Infrastructure`: one read-only adapter over the existing
  `ExactResolutionProfileCatalog` that returns an immutable revision-bound
  required-path set; no Story 12.4 contract mutation.
- `Vulgata.Web`: compose the authority into `PreparedScanPairBuilder` before
  the existing planner and fail closed on a matched-but-incomplete profile;
  coordinate any `Program.cs` registration as a shared-file edit.
- `Vulgata.Tests`: pure planner/authority tests, pair-builder tests, production
  composition tests, and one vertical PostgreSQL test proving both Repositories
  contribute required selected seeds without pre-seeded lifecycle/content rows.

### Mandatory tests

1. exact pair/ref/revision match promotes all four reviewed required paths;
2. normal input order changes do not alter selection or fingerprints;
3. a nonmatching pair/revision receives no profile promotion;
4. missing, unsafe, filtered, duplicate, or ambiguous required path publishes no
   Manifest;
5. all required units count as high-value seeds and must succeed in Pass 1;
6. the existing 20-per-Repository and 150-pair bounds remain enforced;
7. default DI has exactly one server-owned authority and no caller path/profile
   input;
8. the production vertical begins from ordinary System/Repository rows and
   reaches exact resolution without pre-seeded Documents or Edges;
9. neutral fixtures and non-release Repositories retain the existing generic
   selection behavior;
10. no CGC, legacy profiling coordinator, migration, `VulgataDbContext` model
    edit, Story 12.4 contract edit, Worker/Pass 2/Chat rewrite, or fake evidence.

### Estimate and risk

Definition/review: 1-2 hours. TDD implementation and review: 3-6 hours. A fresh
real Scan/Pass 1/Pass 2 run: 4-8 hours depending on provider latency. Chat,
restart, screenshots, E2E, and final verification: 2-4 hours. Primary risks are
profile-to-Repository identity binding and another expensive real run; both are
controlled by exact fail-closed matching and focused qualification before the
fresh database is created.

## 6. Approval and boundaries

The user's instruction to execute all remaining Story 12.5 tasks without
further approval, including remote LLM egress and temporary database creation,
auto-accepts this recommended in-scope correction. It does not waive technical
gates or permit an EF migration, `VulgataDbContext`/snapshot change, Story 12.4
contract mutation, CGC restoration, arbitrary profile/path input, unrestricted
Agent tools, fake Backup B evidence, credential disclosure, direct database
repair, or reuse of run-03 as passed release evidence.
