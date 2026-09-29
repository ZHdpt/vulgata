---
governed_delivery_classification: architecture
governed_delivery_reason: Adds a Worker-visible native CodeGraph MCP tool and changes required Scan preparation/use under application-owned source authority.
---

# Implementation Plan: Use Native CodeGraph MCP Against Prepared Source

**Branch**: `codex/story-18-3-plan` | **Date**: 2026-09-29 | **Spec**: [spec.md](spec.md)

**Input**: Story 18.3 and the agreed F0 source/tool slice. Definition and planning only; Story 18.1 is active elsewhere and remains a completion dependency. [October epics](../../docs/bmad/planning-artifacts/october-release-epics.md#story-183-use-native-codegraph-mcp-against-prepared-source); [proposal](../../docs/bmad/planning-artifacts/sprint-change-proposal-2026-09-26-october-batch-release.md).

## Summary

Use the application-approved prepared Repository binding from Story 18.1 to prepare CodeGraph through the declared installation. Connect to CodeGraph's native MCP endpoint, discover a reviewed read-only tool subset, and make genuine native invocation available to the Worker through injected Agent Execution tooling. Bind project/root and grant outside model arguments, contain every result, and report preparation, connection and invocation distinctly. Existing CLI/JSON, CodeGraphContext and file tools cannot satisfy native MCP readiness. [O-FR05/O-NFR01](../../docs/bmad/planning-artifacts/october-release-epics.md); [PRD FR-4.9/13.1](../../docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md); [AD-3/4/6](../../docs/bmad/planning-artifacts/architecture/ARCHITECTURE-SPINE.md).

## Technical Context

**Language/Version**: C# / `net10.0` in current projects. The actual CodeGraph version must be recorded in later selected-host qualification. [Infrastructure project](../../src/dotnet/Vulgata.Infrastructure/Vulgata.Infrastructure.csproj).

**Primary Dependencies**: Current `Microsoft.Agents.AI` packages are centrally versioned; current Infrastructure CodeGraph integration is CLI/JSON. A suitable native MCP client API/package and CodeGraph launch mode remain unverified integration inputs; no documentation target is an installed-runtime claim. [Package manifest](../../Directory.Packages.props); [adapter](../../src/dotnet/Vulgata.Infrastructure/Analysis/CodeGraph/CodeGraphAnalysisAdapter.cs); [project state](../../docs/agents/project-state.md).

**Storage**: No new database or canonical Document store. Prepared-source authority remains with 18.1; CodeGraph index/process state is disposable and separate. [AD-3](../../docs/bmad/planning-artifacts/architecture/ARCHITECTURE-SPINE.md).

**Testing**: Controlled boundary tests in `tests/Vulgata.Tests`, plus a later real native MCP discovery/invocation/root-binding smoke on a small owned prepared fixture in the selected host. Mock/static/CLI evidence remains separate. [Testing guidance](../../docs/agents/testing.md).

**Target Platform**: October generator and Web execute directly on the host with a declared local CodeGraph installation. Qualify native MCP in that selected host composition. Docker application/tool packaging and qualification are outside this release story; the generic [AD-15/30](../../docs/bmad/planning-artifacts/architecture/ARCHITECTURE-SPINE.md) target remains separate. [October proposal item 11](../../docs/bmad/planning-artifacts/sprint-change-proposal-2026-09-26-october-batch-release.md).

**Project Type**: Modular .NET web application with Infrastructure source/tool adapters, injected Agent Execution contracts, and Web composition. No outward MCP endpoint.

**Performance Goals**: Bounded work, time and response size under existing host policy; no invented throughput goal or semantic-completeness claim.

**Constraints**: The model cannot choose project, physical root, executable, grant or arbitrary command. Cancellation settles owned resources; required CodeGraph failure remains explicit without silent source-only fallback. Separately authorized 18.1 file tools remain usable. [Spec FR-002–FR-006](spec.md); [AD-6](../../docs/bmad/planning-artifacts/architecture/ARCHITECTURE-SPINE.md).

**Scale/Scope**: One approved prepared Repository binding per request/connection, purpose-granted native read tools only. General registered MCP, JDT-LS, Knowledge, provider/model calls and the 18.1 source service stay with their owners.

## Constitution Check

*Pre-research and post-design disposition: compatible, conditional on 18.1 reconciliation and native qualification; no exception requested.*

| Principle | Disposition |
|---|---|
| I. Layered boundaries | Infrastructure owns process/MCP adaptation, Agents receives an injected bounded tool, Web composes; Core/Agents gain no arbitrary host authority. |
| II. Domain/language | Repository-relative provenance and disposable index state are not accepted Knowledge or permanent Code Unit identity. |
| III. Validation/API | No new product HTTP/UI contract; tool grants, arguments and outputs are validated before disclosure. |
| IV. Verification | Later behavior work requires honest RED/GREEN; real native smoke is separate from controlled tests. No tests/runtime run during planning. |
| V. Security | Recheck live binding and purpose before discovery, invocation and result disclosure; bound responses and sanitize diagnostics. |

The [Constitution](../../.specify/memory/constitution.md), [architecture spine](../../docs/bmad/planning-artifacts/architecture/ARCHITECTURE-SPINE.md) and [glossary](../../docs/UBIQUITOUS_LANGUAGE.md) align. October prepared origin is distinct from historical Git-bound Source Snapshot; this plan does not impose a Git revision field on 18.1.

## Project Structure

### Documentation (this feature)

```text
specs/18-3-use-native-codegraph-mcp-against-prepared-source/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── contracts/native-codegraph-boundary.md
```

### Source Code (repository root; prospective ownership)

```text
src/dotnet/Vulgata.Infrastructure/Analysis/CodeGraph/  # current CLI/JSON locality; native adapter
src/dotnet/Vulgata.Agents/WorkerExecution/            # injected Worker tool grant
src/dotnet/Vulgata.Web/Program.cs                     # declared host composition
tests/Vulgata.Tests/                                  # existing single test project
```

**Structure Decision**: Reuse CodeGraph locality and Agent Execution injection; add only the native capability the host supports. Exact 18.1 integration types are deferred to its verified baseline. Existing CLI/JSON code and Web registration are implementation context, not proof of native MCP. [Adapter](../../src/dotnet/Vulgata.Infrastructure/Analysis/CodeGraph/CodeGraphAnalysisAdapter.cs); [Web composition](../../src/dotnet/Vulgata.Web/Program.cs); [layout](../../docs/agents/development.md).

## Implementation Design

### Responsibilities

- **ID-R01**: Story 18.1 alone owns approved prepared-root identity, grants and contained list/read/search. Story 18.3 consumes that F0 authority through a trusted adapter and rejects absent, stale or foreign bindings; it creates no second root registry or source service.
- **ID-R02**: Infrastructure owns declared CodeGraph preparation, native MCP process/client lifecycle and result containment; Agents presents the granted surface to the Worker; Web composes. Scan Control retains wider scheduling and retry authority.
- **ID-R03**: Application policy grants an explicit, reviewed purpose-scoped subset of discovered native read tools. Unknown, write, shell, command and arbitrary-path tools receive no grant; a newly useful tool needs qualification and grant review.

### Interfaces

- **ID-I01**: Consume 18.1's approved prepared Repository/root binding and current purpose/grant under F0. Exact type/signature/lifetime, including how a trusted CodeGraph adapter obtains contained physical access, require reconciliation with integrated, verified 18.1; this plan does not require 18.1 to supply a workspace lease/type. Model arguments contain no physical path or root selector. [F0 proposal](../../docs/bmad/planning-artifacts/sprint-change-proposal-2026-09-26-october-batch-release.md).
- **ID-I02**: Worker-facing discovery/invocation carries only current binding, grant and bounded native tool arguments/results. Adapter fixes or checks native `projectPath` outside model arguments and rejects conflicting, nested or concealed project/path overrides and uncontainable returned locations. [AD-6](../../docs/bmad/planning-artifacts/architecture/ARCHITECTURE-SPINE.md).
- **ID-I03**: Use actual native MCP transport for discovery and invocation. CLI preparation may establish the index, but CLI queries, CodeGraphContext and simulated responses are never advertised as native MCP. Later host qualification pins real tool names/schemas/launch mode without a general MCP registry. [O-FR05/O-AR09](../../docs/bmad/planning-artifacts/october-release-epics.md).

### State and Lifetime

- **ID-S01**: A finite MCP session/handle binds one prepared Repository, purpose and grant epoch. No process-global mutable current project is shared across roots. Changed/revoked bindings invalidate stale handles before use and result disclosure; concurrent roots cannot inherit each other's index, connection or output.
- **ID-S02**: Index/session/process state stays disposable, separate from 18.1 source authority and canonical Knowledge. Record preparation, connection and discovery readiness separately; successful index or empty result proves no completeness or permanent identity.

### Async and Cancellation

- **ID-A01**: Propagate caller cancellation and finite deadlines through preparation, connection, discovery, invocation and output handling. On cancellation, timeout or lease loss, close only owned MCP/process resources and release permits; never stop unrelated services or requests.
- **ID-A02**: Isolate or serialize operations until client/server multiplexing is qualified. Start with binding-scoped finite sessions and independent request ownership; share/coalesce only with an explicit same-binding concurrency proof.

### Failure and Retry

- **ID-F01**: Return distinct sanitized outcomes for missing/unprepared source, unavailable installation, preparation, connection, discovery/grant, invocation, timeout, cancellation and bounded/incomplete result. No-match is distinct from incomplete index. Ready requires required native discovery; successful invocation is separate qualification evidence.
- **ID-F02**: Required CodeGraph failure never silently selects source-only analysis. Authorized 18.1 file tools may be used independently. The adapter performs no hidden retry; Scan Control applies its existing bounded retry/pause policy. [AD-15](../../docs/bmad/planning-artifacts/architecture/ARCHITECTURE-SPINE.md).
- **ID-F03**: Deny a native result that cannot be confined to the current grant, or return explicitly redacted/limited data where containment is proven. Do not disclose absolute paths, outside-root source, raw arguments, secrets or unbounded stderr.

### Reuse and Locality

- **ID-L01**: Reuse existing CodeGraph configuration/process patterns only where they satisfy prepared-origin native behavior. Preserve historical revision-bound CLI/JSON invariants; do not broaden them by relabelling a CLI reply as native MCP.
- **ID-L02**: Keep native adaptation by Infrastructure CodeGraph code, Worker grant wrapper in Agents and composition in Web. No generic MCP framework, extra service, database schema, JDT-LS coupling or provider call is required.

### Test Seams

- **ID-T01**: Inject a controlled client/process seam for grant filtering, fixed root, nested override, stale/revoked handle, cross-root concurrency, output limits and cancellation cleanup; these tests do not claim native qualification.
- **ID-T02**: An owned small fixture in the selected host must record actual CodeGraph version, native discovery, one useful result with correct relative source, foreign-root denial, truthful failures and owned cleanup. Independently authorized 18.1 file-tool use remains possible; no provider/model call is needed. [Story acceptance](../../docs/bmad/planning-artifacts/october-release-epics.md).
- **ID-T03**: Before later delivery, reconcile 18.1's approved binding and the trusted adapter's contained physical-access mechanism against the integrated, verified baseline; rerun affected plan-content, independent review, behavior and runtime gates. A labelled F0 test adapter cannot close the dependency.

### Alternatives

- **ID-X01**: CLI-query-to-tool wrapping or CodeGraphContext reuse would be shorter but cannot prove native MCP discovery/invocation and fails O-FR05/FR-13.1; reject.
- **ID-X02**: Model-selected physical roots or exposing all discovered native tools would simplify wrapping but violate application authority and O-NFR01; reject.
- **ID-X03**: One mutable current-project MCP session may save startup cost but makes cross-root and revocation isolation hard to prove; start with binding-scoped sessions and revisit only with evidence.

### Consequences

- **ID-C01**: This is a Worker-visible agent-tool and material Scan preparation/use change. AGENTS.md requires independent plan review and planned-change authorization before tasks. The current user instruction stops after independent plan review.
- **ID-C02**: The allowlist may exclude new useful native tools until reviewed, and binding-scoped sessions may add startup cost. Those limitations are preferable to false coverage or cross-root reuse claims.
- **ID-C03**: Completion depends on integrated, verified Story 18.1 and real selected-host CodeGraph native smoke; F0 fixtures and historical CLI tests cannot satisfy either. Changed facts return to affected definition/review gates.

## Unresolved Dependencies and Reconciliation

1. **Story 18.1** is active elsewhere. Its integrated, verified approved-root binding and contained read-tool behavior are not available as a completed baseline here. Compare its actual contract and any trusted-adapter physical-access mechanism to ID-I01/S01/T03 before later tasks or implementation; a mismatch returns to the earliest affected definition owner and independent review. Do not change or duplicate 18.1.
2. **Native capability**: The manifest confirms MAF packages and current CLI/JSON behavior, not a native MCP client package/API, CodeGraph launch mode or installed runtime version. Verify these in the selected host. If unavailable, report a blocker rather than substituting CLI behavior.
3. **Tool catalogue and limits**: Qualify real names, schemas, path fields and result shapes, then fix the explicit grant and bounds under existing application policy. Unknown capabilities remain denied pending review.
4. **Gate order**: Reconcile 18.1 and rerun affected deterministic plan checks and independent reviews before tasks. A later accepted plan still needs the repository-mandated planned-change decision. This planning run creates no tasks, implementation, runtime/provider evidence or approval.

## Self-Review

The plan has one owner per boundary: 18.1 source authority, Infrastructure native tool adaptation, Agents filtered exposure, Web composition and Scan Control scheduling. The invariant is current application-selected binding/purpose at discovery, invocation and disclosure. Finite binding-scoped lifetime and distinct outcomes make cancellation, stale handles and concurrency testable. Migration effect is additive at the tool boundary, with no schema change or rewrite of historical Git-bound source/CLI behavior. October's direct-host composition is the qualification target; generic Docker packaging is outside this story. Feasibility depends on the named 18.1 and native host capabilities; neither is claimed complete, and this plan does not impose a workspace lease on 18.1. AD-3/4/6/15, Constitution I/IV/V and O-FR05/O-NFR01 align under that scoped release choice. `docs/project-lessons.md` is absent in this checkout, so no recorded lesson was inferred. [Constitution](../../.specify/memory/constitution.md); [architecture](../../docs/bmad/planning-artifacts/architecture/ARCHITECTURE-SPINE.md); [October proposal](../../docs/bmad/planning-artifacts/sprint-change-proposal-2026-09-26-october-batch-release.md); [testing](../../docs/agents/testing.md).
