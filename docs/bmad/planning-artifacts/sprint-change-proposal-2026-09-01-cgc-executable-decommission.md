# Sprint Change Proposal: CGC Executable Decommission

**Date:** 2026-09-01

**Mode:** Incremental

**Scope classification:** Major project correction; bounded first implementation phase

**Implementation authority:** Not granted by this proposal

## 1. Issue Summary

The post-competition project direction retires Epic 12 as a deadline overlay and returns Vulgata to an architecture- and PRD-led delivery sequence. The first correction phase removes CodeGraphContext (CGC) from executable code before reassessing the remaining system.

The trigger is a failed technical approach discovered through Epic 4, Epic 11, and Story 12.5 work. CGC indexing was operationally unsuitable, while the legacy profiling route became coupled to CGC-specific provider contracts. Epic 12 separately implemented a bounded replacement using CodeGraph 1.5, JDT-LS, deterministic Java/XML preparation, and a durable Prepared Scan Manifest. A broad deletion would therefore remove valid replacement work as well as the obsolete runtime.

Story 12.5 was terminated incomplete after real production JDT-LS qualification exposed unresolved deployment, Maven/build import, offline project/classpath, and source-navigation semantics. That blocker does not invalidate the already implemented Prepared Scan data flow, but it prevents treating Epic 12 as a complete production release.

## 2. Change-Navigation Checklist Record

### 2.1 Trigger and context

- [x] **1.1 Trigger:** Epic 4/11 CGC operational failure, confirmed again while Story 12.5 tried to enter the bounded production Scan path.
- [x] **1.2 Category:** failed technical approach requiring a different solution, followed by a strategic post-competition reset.
- [x] **1.3 Evidence:** legacy `CodeGraphContextProvider` and `IRepositoryProfilingCoordinator` remain in source; production composes CodeGraph 1.5/JDT-LS/Prepared Scan instead; Story 12.5 termination evidence records the unresolved JDT-LS boundary.

### 2.2 Epic impact

- [x] **2.1 Epic 12:** cannot be completed as originally planned and remains historical/terminated rather than current delivery authority.
- [x] **2.2 Required change:** remove executable CGC without rewriting historical Epic 4, Epic 11, or Epic 12 records.
- [!] **2.3 Remaining epics:** Epics 5–10 and later stories require a separate architecture/PRD assessment after cleanup.
- [!] **2.4 New work:** the assessment may replace, merge, reorder, or rewrite future epics; no new epic is authorized in this phase.
- [!] **2.5 Priority:** CGC decommission precedes architecture assessment, PRD discussion, and final-round planning.

### 2.3 Artifact impact

- [x] **3.1 PRD:** core goal—cross-Repository business understanding—remains valid. CGC-specific implementation assumptions are not reaffirmed. Requirement edits wait for the assessment phase.
- [x] **3.2 Architecture:** active runtime composition changes by removing CGC; CodeGraph 1.5/JDT-LS/Prepared Scan remains. Historical architecture documents remain provenance.
- [x] **3.3 UI/UX:** obsolete legacy Repository Scan actions may become explicitly unimplemented. Prepared Scan behavior is preserved. A later UX assessment decides the final Scan journey.
- [x] **3.4 Other artifacts:** remove CGC Docker/Python/runtime/qualification files; preserve EF schema/migrations and historical evidence. Testing acceptance is compilation only.

### 2.4 Path evaluation

| Option | Viability | Effort | Risk | Finding |
| --- | --- | --- | --- | --- |
| Directly repair existing CGC stories | Not viable | High | High | Reinvests in the failed runtime and recreates coupling |
| Roll back Epic 12 | Not viable | High | High | Discards valid CodeGraph 1.5, JDT-LS, durable Scan, Prepared Scan, Pass 1, and Pass 2 work |
| Redefine the complete PRD before cleanup | Viable later | High | Medium | Assessment would remain distorted by obsolete executable CGC code |
| Hybrid: decommission CGC, preserve Epic 12 replacements, then reassess | **Recommended** | Medium | Medium | Produces an honest baseline without pre-deciding the replacement architecture |

- [x] **4.4 Selected path:** Hybrid.

## 3. Impact Analysis

### Epic impact

- Epic 4 and Epic 11 remain completed historical records, including their CGC references.
- Epic 12 remains a dated historical overlay. Stories 12.1–12.4 preserve their accepted implementation artifacts; Story 12.5 remains terminated incomplete.
- The current phase adds no story and does not resume a story.
- Future epic disposition waits for the post-decommission assessment.

### PRD impact

The cleanup neither weakens nor redefines Vulgata's product goal. It removes one failed implementation dependency. General language/framework discovery, Code Unit granularity, structural graph authority, orchestration, and Scan UX become explicit assessment questions rather than silently inherited Epic 12 commitments.

### Architecture impact

The active executable architecture becomes:

```text
legacy Repository Scan entry -> NotImplementedException

Prepared Scan entry
  -> exact Profiled Revision acquisition
  -> contained Java/XML enumeration and exact hashes
  -> CodeGraph 1.5 completeness and bounded relationship expansion
  -> JDT-LS readiness and Java ranges
  -> safe XML + Communication Pattern Catalog
  -> File Code Units + Prepared Scan Manifest + prepared handoff
  -> Prepared Pass 1 -> exact Cross-Repository Resolution -> Prepared Pass 2
```

The preserved Prepared Scan path remains incomplete as a production product because JDT-LS deployment/classpath authority and generic Scan UX require reassessment.

### Data and migration impact

No schema or migration change is allowed. Existing Scan Runs, Pre-Scan Results, Prepared Scan Manifests, File Code Units, classifications, Documents, Edges, Uncertainties, and context/recovery rows remain intact.

### UI impact

Legacy single-Repository Scan initiation/read behavior may throw `NotImplementedException`. No HTTP 503 placeholder contract is introduced. Prepared Scan's current endpoint and failure mapping remain unchanged.

### Verification impact

The accepted technical gate is `dotnet build Vulgata.slnx`. Tests may be removed or minimally edited only to keep the solution compiling. No test, E2E, runtime, database, Docker, or real-tool qualification is required for this cleanup.

## 4. Detailed Change Proposals

### 4.1 Active architecture

**OLD**

CGC provider, legacy Repository profiling, and Epic 12 Prepared Scan coexist in executable source, obscuring which path is authoritative.

**NEW**

Remove executable CGC. Preserve CodeGraph 1.5/JDT-LS/Prepared Scan. Leave the legacy Repository profiling boundary explicitly unimplemented.

**Rationale**

This produces an honest baseline without discarding modern replacement work or pretending the remaining architecture is complete.

### 4.2 Historical specifications and evidence

**OLD**

Historical documents describe the architecture accepted when their stories ran.

**NEW**

No text change. Those documents remain historical evidence and may continue to mention CGC.

**Rationale**

Rewriting history would make past decisions and evidence misleading.

### 4.3 Legacy Scan behavior

**OLD**

Legacy API/UI surfaces depend on `IRepositoryProfilingCoordinator`, whose real implementation depends on `ICodeGraphProvider`/CGC and is not the Epic 12 production path.

**NEW**

Retain only the minimum compiling boundary and throw `NotImplementedException` for obsolete operations.

**Rationale**

The post-cleanup assessment must decide the future Scan entrance. An HTTP 503 simulation would falsely appear to be a supported temporary service contract.

### 4.4 Data model

**OLD**

Historical and prepared persistence coexist.

**NEW**

No model, migration, or stored-data change.

**Rationale**

The assessment needs the current durable model and provenance intact.

### 4.5 Acceptance policy

**OLD**

Story workflows normally require focused tests, regression tests, E2E decisions, and quality gates.

**NEW**

For this intentionally incomplete cleanup only, successful solution compilation is the sole technical acceptance gate. `git diff --check` and a changed-file inventory are reporting hygiene, not additional product acceptance.

**Rationale**

Tests enforcing intentionally retired CGC behavior would obstruct removal and encode obsolete architecture.

## 5. Recommended Implementation Handoff

### Scope

The overall correction is **Major** because it precedes a PRD and architecture replan. The first execution package is **bounded removal work** and should be handled by a fresh Developer session.

### Developer session responsibilities

- pin and preserve the current branch/worktree state;
- inventory all executable CGC references and classify ambiguous CodeGraph names;
- remove CGC runtime/provider/Docker/qualification code;
- add the narrow legacy `NotImplementedException` placeholder;
- retain Epic 12 CodeGraph 1.5/JDT-LS/Prepared Scan and all persistence;
- achieve `dotnet build Vulgata.slnx` success;
- report changed files and preserved gaps without expanding scope.

### Current design session responsibilities

- retain ownership of architectural interpretation and future assessment;
- review any execution-time ambiguity that would remove a preserved Epic 12 component or change persistence;
- after cleanup, reassess architecture and PRD before redrafting epics.

## 6. Success Criteria

- Executable CGC dependencies are absent.
- The solution compiles.
- Legacy single-Repository profiling is explicitly unimplemented.
- Epic 12's CodeGraph 1.5/JDT-LS/Prepared Scan implementation remains present.
- No persistence migration or historical artifact is removed or rewritten.
- Known gaps are preserved for the next assessment rather than repaired opportunistically.

## 7. Approval Record

The user approved:

- preservation of historical Epic 4/specification/evidence references;
- Approach A, allowing an intentionally incomplete codebase during the correction;
- `NotImplementedException` rather than HTTP 503 placeholders;
- no anti-restoration or placeholder-behavior test;
- successful compilation as the sole technical acceptance gate;
- preservation of Epic 12's CodeGraph 1.5/JDT-LS/Prepared Scan replacement path after an explicit Epic 12 audit.

Implementation remains pending a written-design review and execution handoff.
