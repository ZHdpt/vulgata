# CGC Executable Decommission Design

**Date:** 2026-09-01

**Status:** Design approved in conversation; implementation not started

**Scope:** Remove executable CodeGraphContext dependencies while preserving historical records and Epic 12's bounded Prepared Scan path

## 1. Decision

Vulgata will remove CodeGraphContext (CGC) from executable production and test code. Historical Epic 4, Epic 11, Epic 12, specification, decision, review, and evidence artifacts remain unchanged as records of the decisions and results that existed at the time.

The decommission deliberately permits the legacy single-Repository profiling path to become incomplete. Its remaining callable boundary will throw `NotImplementedException` until the post-decommission architecture assessment decides whether to remove, redesign, or replace that user flow.

Epic 12's CodeGraph 1.5/JDT-LS/Prepared Scan path is not a CGC dependency and must remain executable source. The decommission must not replace, disable, or reinterpret that path.

## 2. Why This Boundary Exists

CGC proved unsuitable as Vulgata's release-critical pre-analysis runtime: its indexing was too slow and unreliable for the project schedule, and its provider-specific query and qualification surface became coupled to the legacy Repository profiling coordinator. Epic 12 replaced the bounded release path with closed CodeGraph 1.5 and JDT-LS ports, deterministic Java/XML preparation, and a durable Prepared Scan Manifest.

The current repository therefore contains two different Scan entrances:

1. a legacy single-Repository route whose coordinator depends on `ICodeGraphProvider` and `CodeGraphContextProvider`; and
2. an Epic 12 two-Repository Prepared Scan route using CodeGraph 1.5, JDT-LS, safe XML analysis, File Code Units, and prepared handoff persistence.

Removing both would destroy working replacement code and misclassify the remaining architecture gap. The correct boundary removes only the first route's CGC implementation and preserves the second route for later reassessment and repair.

## 3. Goals

- Remove executable CGC provider, Python runtime, HTTP protocol, Docker installation, qualification harness, and legacy integration code.
- Leave no production code that starts, calls, resolves, or qualifies CGC.
- Preserve Epic 12's closed CodeGraph 1.5, JDT-LS, revision-bound source, and Prepared Scan implementation.
- Preserve existing persistence schema, migrations, and stored historical/prepared data.
- Make obsolete legacy Scan behavior visibly incomplete through `NotImplementedException`.
- Produce a codebase that successfully compiles and is ready for a separate architecture and PRD assessment.

## 4. Non-Goals

- Restore or redesign legacy Repository profiling.
- Generalize Prepared Scan beyond exactly two Java/XML Repositories.
- Repair JDT-LS build import, offline classpath, or extracted-installation deployment behavior.
- Replace framework detection, symbol-level Code Units, or the complete legacy structural graph.
- Rewrite historical specifications, evidence, retrospectives, checkpoints, or decision records.
- Delete migrations, tables, columns, or historical rows associated with PreScan, Code Units, classifications, or Scan Runs.
- Add an anti-restoration test, placeholder-behavior test, runtime qualification, E2E test, or regression requirement.
- Resume or complete Story 12.5.

## 5. Responsibility Map

| Legacy CGC responsibility | Preserved Epic 12 authority | Disposition |
| --- | --- | --- |
| Python/HTTP analysis service and arbitrary provider query protocol | `ICodeGraphAnalysisPort` and `IPreparedCodeGraphAnalysisPort` implemented by CodeGraph 1.5 adapters | Remove CGC implementation; preserve closed ports and adapters |
| Repository structural indexing | Revision-bound CodeGraph 1.5 `init`, `index`, `status`, and closed queries | Preserve |
| Java symbols and navigation | JDT-LS through `IJavaAnalysisSessionFactory` | Preserve; record deployment/classpath gap |
| General CGC parser language attribution | Deterministic `.java`/`.xml` extension classification | Remove legacy attribution; record bounded-language gap |
| Symbol-level Code Unit extraction | Exact-hash `CodeUnitType.File` units with bounded safe splitting | Preserve File Code Units; record missing symbol-unit replacement |
| Framework attribution | No general Epic 12 equivalent | Remove CGC-derived behavior; preserve schema; record gap |
| Structural relationship projection | Bounded CodeGraph Calls/Imports/Inherits expansion for deterministic selection | Preserve expansion; record absence of a general persisted structural graph |
| XML communication clues | Safe XML pipeline plus Communication Pattern Catalog and Communication Kind Classifications | Preserve |
| Legacy approved Pre-Scan Handoff v1 | Prepared Scan Manifest plus prepared v2 Pre-Scan Result/Handoff | Preserve both persisted shapes; new work uses only prepared handoff |
| Legacy single-Repository Scan entry | No equivalent single-Repository Epic 12 entry | Retain only a compiling `NotImplementedException` boundary |
| Two-Repository release entry | `/api/systems/{systemId}/prepared-scans` and Prepared Scan initiation/coordinator/builder | Preserve |

## 6. Preserved Executable Boundary

The implementation session must preserve these responsibilities and their dependencies:

- `Vulgata.Infrastructure/Analysis/CodeGraph/**`;
- `Vulgata.Infrastructure/Analysis/Java/**`;
- revision-bound and prepared source readers;
- `PreparedScanManifest`, prepared entries/audits, Communication Kind Classifications, and Scan Context Snapshots;
- `IPreparedScanStore`, `IPreparedScanHandoffSource`, and `IPreparedScanInitiationStore`;
- `PreparedScanInitiationCoordinator`, `PreparedScanCoordinator`, and `PreparedScanPairBuilder`;
- the production Prepared Scan endpoint;
- Prepared Pass 1 and Prepared Pass 2 code;
- all EF configurations, migrations, and model-snapshot content;
- historical v1 read compatibility where it compiles without a CGC runtime.

The implementation session must treat a reference to the word `CodeGraph` as ambiguous until it identifies whether the reference means CodeGraph 1.5 or CodeGraphContext. Names, namespaces, port types, package identities, and call sites—not substring matching—decide the disposition.

## 7. Removed or Stubbed Boundary

The executable removal set is expected to include:

- `CodeGraphContextProvider` and its provider-specific query/result translation;
- legacy `ICodeGraphProvider` contracts and projections when no preserved caller requires them;
- `RepositoryProfilingCoordinator` behavior that obtains Code Units and relationships from CGC;
- CGC HTTP client/options/health/qualification code;
- CGC Docker/Python installation and locked dependencies;
- current tests whose sole purpose is CGC provider, Docker, protocol, or qualification behavior;
- legacy Repository Scan UI/API code that cannot compile without the removed coordinator.

The preferred placeholder is at the narrow legacy application boundary. Methods that previously initiated or read legacy Repository profiling may remain with compatible signatures and immediately throw `NotImplementedException`. The placeholder must not fabricate a Scan Run, return HTTP 503 as a designed replacement contract, or delegate to Prepared Scan with invented pair/revision inputs.

Exact file deletion and minimal compatibility edits are implementation decisions constrained by this boundary and the compilation gate. The implementation agent must re-observe all callers before removing a contract.

## 8. Persistence and Historical Records

The decommission performs no EF migration and no model change. Existing data remains readable to the extent supported by preserved entities and repositories. Historical rows are not promoted to current Prepared Scan authority, and no conversion or backfill is introduced.

Historical Markdown, JSON, evidence, specifications, reviews, checkpoints, and retrospectives may continue to mention CGC. Those references are provenance, not executable dependencies, and must remain unchanged.

## 9. Expected Incomplete State

After decommission, the codebase intentionally contains these known gaps:

- legacy single-Repository Scan initiation is not implemented;
- Prepared Scan remains bounded to exactly two Java/XML Repositories;
- general language and framework discovery is absent;
- symbol-level Code Unit generation is absent from PreScan;
- CodeGraph relationships guide selection but do not constitute a general persisted source graph;
- JDT-LS deployment, offline project/classpath authority, and build-import behavior require reassessment;
- Story 12.5's reviewed exact-profile seed authority remains competition-specific and requires later disposition;
- Story 12.5 remains terminated and no complete production Scan/Chat release claim is restored by this cleanup.

These gaps are inputs to the next architecture and PRD assessment. They are not defects to fix during CGC decommission.

## 10. Error Behavior

`NotImplementedException` is the sole placeholder mechanism. The decommission does not define a stable HTTP error contract for obsolete legacy Scan endpoints. Middleware may translate an uncaught exception according to the host's existing behavior; that runtime result is outside this phase's acceptance boundary.

Prepared Scan retains its existing typed failures and HTTP behavior. No exception placeholder may be inserted into the Prepared Scan, Prepared Pass 1, or Prepared Pass 2 flow solely because Story 12.5 qualification is incomplete.

## 11. Verification and Acceptance

The only required technical acceptance command is:

```pwsh
dotnet build Vulgata.slnx
```

Acceptance requires exit code zero. Existing test sources may be deleted or minimally edited when necessary for compilation after the production removal. The following are explicitly not acceptance requirements:

- `dotnet test`;
- runtime startup;
- Docker build or Compose startup;
- real CodeGraph/JDT-LS qualification;
- database migration validation;
- API/UI behavior checks;
- anti-restoration or architecture tests;
- proof that obsolete legacy endpoints throw a particular exception at runtime.

The implementation agent must also inspect `git diff --check` and report the complete changed-file inventory, but those checks do not broaden the technical acceptance gate beyond successful compilation.

## 12. Implementation Sequence

1. Pin the starting commit and record the clean/dirty worktree state.
2. Inventory executable CGC references, classifying each as legacy runtime, preserved history, preserved CodeGraph 1.5, or ambiguous.
3. Remove CGC Docker/Python/runtime/qualification artifacts.
4. Remove CGC provider implementation and legacy-only contracts/projections.
5. Replace the narrow legacy Repository profiling boundary with `NotImplementedException`, minimizing signature changes.
6. Remove or minimally repair CGC-only tests and callers until the solution compiles.
7. Confirm Prepared Scan, CodeGraph 1.5, JDT-LS, persistence, Prepared Pass 1, and Prepared Pass 2 source remains present and composed.
8. Run `dotnet build Vulgata.slnx`, repair compilation only, then run `git diff --check`.
9. Report removed files, compatibility edits, preserved modern components, known incomplete behavior, and the build result.

## 13. Success Criteria

The decommission is complete when:

- the solution builds successfully;
- no executable production or test code requires CGC, its Python runtime, HTTP API, private protocol/storage, Docker service, or qualification harness;
- Epic 12 CodeGraph 1.5/JDT-LS/Prepared Scan code and persistence remain present;
- obsolete legacy Scan entry behavior is explicitly unimplemented rather than simulated;
- no migration or historical artifact is removed or rewritten;
- all known functional gaps are reported for the subsequent architecture assessment.
