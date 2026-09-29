---
governed_delivery_classification: architecture
governed_delivery_reason: Binds a material Worker navigation tool and application authority for restricted JDT-LS over an October prepared Repository; AGENTS.md planned-change authorization applies.
---

# Implementation Plan: Navigate Prepared Java Source with Restricted JDT-LS

**Branch**: `codex/18-2-plan` | **Date**: 2026-09-29 | **Spec**: [spec.md](spec.md)

**Input**: Story 18.2 of `docs/bmad/planning-artifacts/october-release-epics.md`, the September 26 October proposal, and this feature's specification. Story 18.1 is active elsewhere, not yet an integrated verified baseline.

## Summary

Offer bounded definition and reference navigation through an application-bound JDT-LS tool over one whole operator-prepared Java Repository. Consume the agreed F0 prepared-source grant without defining a second source identity, root grant or Story 18.1 list/read/search contract. Keep JDT-LS state separate from source; disable automatic Maven/Gradle import, build and dependency restoration. Return only contained Repository-relative observations with truthful limits. Qualify real navigation and source no-write behavior in the declared host profile.

This is a material agent-tool change under `AGENTS.md`: independent plan review and planned-change authorization must precede future tasks. The present user instruction stops after independent plan review. Before later delivery, reconcile this design against integrated, verified 18.1 and rerun affected gates.

## Technical Context

**Language/Version**: .NET 10/C#; Java 21 and JDT-LS from explicitly configured local installation, subject to later host qualification.

**Primary Dependencies**: Existing Core analysis ports and Infrastructure StreamJsonRpc/JDT-LS process mechanisms; pending 18.1 F0 prepared-source grant. Build configuration owns installed versions.

**Storage**: Disposable JDT-LS data/configuration only, separate from prepared source. No database, Knowledge schema, source copy, Source Snapshot, Git revision identity or repository-wide hash.

**Testing**: Later honest behavioral RED/GREEN in `tests/Vulgata.Tests`, controlled seam tests, then real JDT-LS definition/reference qualification. No tests or runtime work in this plan turn.

**Target Platform**: October host-run profile; generic Docker deployment target remains outside this October delivery slice.

**Project Type**: Internal Worker tool and Infrastructure adapter in the existing modular monolith.

**Performance Goals**: Bounded readiness/request/queue/shutdown, one owned session per selected Repository, serialized unsafe protocol work. No completeness or throughput promise.

**Constraints**: Prepared directories remain operator-maintained and unchanged; no arbitrary path, source write, command tool, dependency restoration or silent result truncation; required native CodeGraph remains separate.

**Scale/Scope**: One whole Java Repository per grant, with concurrent Workers sharing at most one owned session. No other LSP or multi-session policy.

## Constitution Check

**Pre-research**: Constitution I/V require Core to own contracts, Infrastructure to own filesystem/process adapters and Agents to use injected capabilities, never arbitrary host access. IV requires honest RED/GREEN and actual tool qualification later. AD-15 and PRD FR-15.1–15.3 permit the temporary restricted profile. The October proposal specifically narrows source origin and deployment to operator-prepared host directories. No Constitution exception is proposed.

**Post-design**: These boundaries remain intact. The unresolved 18.1 integration and direct-root JDT-LS no-write feasibility are explicit qualification dependencies. If either changes authority or source behavior, return to definition/architecture and affected gates.

## Project Structure

### Documentation (this feature)

```text
specs/18-2-navigate-prepared-java-source-with-restricted-jdt-ls/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── contracts/navigation-tool.md
└── quickstart.md
```

### Source Code (existing seams; future changes are not authorized here)

```text
src/dotnet/Vulgata.Core/DomainServices/AnalysisRuntimeContracts.cs
src/dotnet/Vulgata.Infrastructure/Analysis/Java/JdtLsAnalysisAdapter.cs
src/dotnet/Vulgata.Agents/
src/dotnet/Vulgata.Web/Program.cs
tests/Vulgata.Tests/AnalysisRuntime/
```

**Structure Decision**: Keep 18.1/F0 source authority with its owner. Place navigation-specific contract, adapter and composition in existing projects after integration. Do not create a parallel prepared-root registry, source mirror or new project. Exact type/DI names await integrated 18.1 reconciliation.

## Implementation Design

### Responsibilities

- **ID-R01**: Application Tool Runtime selects the prepared Repository grant and restricted profile; Scan Control decides availability for bounded Worker work. Infrastructure alone owns JDT-LS filesystem/process authority; Worker arguments cannot grant it. This follows O-AR02/O-NFR01 and Constitution I/V.
- **ID-R02**: The navigation module translates requests and JDT-LS replies to bounded Repository-relative observations and limitations. It does not create source identity, accept Knowledge, infer a missing relationship from no result, own 18.1 file tools or replace CodeGraph (O-FR04–O-FR06; PRD FR-4.9/15.2).

### Interfaces

- **ID-I01**: One Worker-facing definition/reference interface accepts only an application-granted Repository binding, Repository-relative Java path, position and bounded result controls. The application supplies root, identity, profile and session authority outside model arguments. Adapt to 18.1's actual F0 handle/location types after integration; do not define a second root-grant or list/read/search schema. See `contracts/navigation-tool.md`.
- **ID-I02**: Return observed relative path/range locations, request kind, explicit result bound/continuation-or-limit status and capability/classpath/failure classification. Zero locations mean `no result observed`, never absence proof. Withhold outside-grant URIs, physical paths and source content.
- **ID-I03**: Keep JDT-LS stdio, configured Java/JDT-LS installation and fixed restricted profile behind the Infrastructure interface. Models cannot select adapter, executable, workspace, import/build mode or host path (PRD FR-15.1/15.3; AD-15).

### State and Lifetime

- **ID-S01**: Key session ownership by the selected Repository binding from F0; permit at most one live JDT-LS process per selected Repository in the host. Root/profile conflict cannot reuse it. Acquisition and release include protocol, queue, process and disposable data/configuration; waits or busy results are bounded.
- **ID-S02**: Use the prepared Repository as source project root with separate disposable JDT-LS `-data` and configuration. Do not mirror/copy source, invent Git identity or use `PreparedRevisionJdtLsWorkspaceLeaseProvider` as an October grant. Qualification must establish no writes to prepared source; a failure returns for reviewed design correction.

### Async and Cancellation

- **ID-A01**: Bound readiness, queue wait, navigation call and shutdown. Serialize shared protocol operations while concurrent safety is unproven. Cancellation removes a waiter or settles the active operation without delivering late results to another request. Retire an unhealthy/timed-out session before reuse.

### Failure and Retry

- **ID-F01**: Distinguish invalid/changed/unavailable grant, missing installation, readiness failure, protocol failure, cancellation, timeout, limit and classpath/capability limitation. None becomes successful empty evidence. Sanitize diagnostics without source text, secrets or absolute paths; known changed/absent input invalidates source-inspection claims.
- **ID-F02**: Retry only a new bounded attempt after the failed owned session is fully settled and the same grant remains valid. Never auto-restore dependencies, switch root/adapter or replay uncertain results. Separately granted 18.1 file tools remain usable when navigation is limited; CodeGraph remains required (O-FR04–O-FR06).

### Reuse and Locality

- **ID-L01**: Reuse current StreamJsonRpc stdio, bounded request, process cleanup and relative mapping only after direct-root assessment. Current `AnalysisScope`/`ProfiledRevision`, revision lease, project mirror/copy, Windows configuration and empty-as-success parsing are not accepted October semantics. Concentrate binding/containment/normalization in the navigation module, not Worker callers.
- **ID-L02**: Keep 18.1 root identity, grant and list/read/search with their owner; adapt navigation to accepted F0 types. Keep CodeGraph native MCP and Scan Control scheduling with their respective owners (O-AR06/O-AR07).

### Test Seams

- **ID-T01**: Exercise the public navigation interface with a representative Java fixture, real declarations/callers, actual definition/reference locations and classpath limitations under the no-import/no-build/no-restore profile. Do not count initialization alone as navigation (O-AR09).
- **ID-T02**: Use injected process/protocol and owned roots to exercise foreign roots, traversal/symlink escape, unmappable returned URI, overlap, cancellation, timeout, source unavailability and cleanup. Verify no source writes or unrelated process termination. Fakes prove boundaries, not real JDT-LS qualification.

### Alternatives

- **ID-X01**: Reuse of the existing prepared-revision lease/project mirror unchanged was rejected: it verifies Git-style revision identity and copies source into a JDT-LS project, contrary to the October prepared-input/no-copy contract.
- **ID-X02**: Worker-chosen workspace/executable and per-Worker sessions were rejected because they defeat application authority and one-session ownership (O-FR06/O-NFR01). Dependency-aware import/restore or another LSP requires later scope under AD-15.

### Consequences

- **ID-C01**: No-import navigation may miss definitions/references across unresolved classpaths; every result must retain its observed limits, while 18.1 file tools permit continued inspection and native CodeGraph remains separately required.
- **ID-C02**: Direct prepared-root use avoids duplicated source but exposes a real JDT-LS no-write risk. Failure requires architecture correction and affected review/authorization, not silent copying or operator-source changes.
- **ID-C03**: Parallel plan work against F0 does not establish implementation readiness. Integrated, verified 18.1 may invalidate grant or interface assumptions; reconcile and rerun affected gates before future delivery.

## Dependencies and Verification Limits

1. **18.1 integration**: Its prepared identity, grant lifetime, authorization, relative locations and file-tool failure/continuation semantics are pending. Reconcile actual integrated types and behavior; never alter or duplicate them here.
2. **Restricted JDT-LS feasibility**: The present adapter's `initialize` shows no evidenced Maven/Gradle disabling settings, and its prepared-revision lease copies source. Future qualification must establish fixed restricted settings, useful navigation and no source writes. This plan does not claim those results.
3. **Qualification sequence**: The user chose a small owned representative fixture first, then the actual prepared Repository when supplied. Evidence from the fixture cannot be promoted to the later Repository or universal Java completeness. Access to the latter is not supplied by this preference.
4. **Evidence**: This turn inspected repository authorities/source only. No JDT-LS process, provider, build, test, database or application runtime was run.

## Self-Review

- **Feasibility**: Existing protocol/process code is a mechanism candidate. Restricted settings, direct-root no-write behavior and real definition/reference quality remain unqualified and are explicit risks.
- **Invariants**: One application-bound Repository and owned session, bounded serialized calls, contained relative observations, independent file-tool fallback and no outside-root disclosure/source write are defined across the interface.
- **Migration/compatibility**: Preserve historical Git/revision adapter and validators; add October composition only after 18.1 integration. No migration, package upgrade, source copy, Docker change or 18.1 contract rewrite is proposed.
- **Constitution/ADR alignment**: Core/Infrastructure/Agents ownership follows Constitution I/V and O-AR02; restricted profile follows AD-15/PRD FR-15.1–15.3; truthful limitations follow FR-15.2/O-AR09. October host/source scope does not claim the generic Docker/Source Snapshot design is implemented.
- **Lessons**: `docs/project-lessons.md` is absent here; historical Story 12.1 JDT-LS notes inform mechanism assessment but do not qualify October runtime.

No Constitution violation is proposed, so Complexity Tracking is not needed.
