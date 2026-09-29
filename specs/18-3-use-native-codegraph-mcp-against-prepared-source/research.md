# Research: Story 18.3 Native CodeGraph MCP

This is source-grounded planning research, not a native capability test. The selected host, package API and Story 18.1 integration still need verification.

## Decision 1: Consume the F0 prepared-source authority

**Decision**: Consume Story 18.1's application-approved Repository/root binding and contained read-tool authority; keep its identity, grant and list/read/search contract under 18.1 ownership. **Rationale**: The [October proposal's F0 source/tool slice](../../docs/bmad/planning-artifacts/sprint-change-proposal-2026-09-26-october-batch-release.md) and [Story 18.1](../../docs/bmad/planning-artifacts/october-release-epics.md#story-181-access-prepared-source-through-contained-read-only-tools) put source authority there. [AD-3/6](../../docs/bmad/planning-artifacts/architecture/ARCHITECTURE-SPINE.md) require contained source and application-selected tool roots. **Alternative**: a new 18.3 root registry would duplicate ownership and risk divergent grants; reject. **Open verification**: integrated 18.1 interface/lifetime and how a trusted CodeGraph adapter obtains contained physical access are not yet verified; 18.1 is not presumed to supply a workspace lease.

## Decision 2: Native tool path

**Decision**: Use CLI only for CodeGraph preparation where supported, then native MCP transport for discovery and useful invocation. **Rationale**: [O-FR05](../../docs/bmad/planning-artifacts/october-release-epics.md) and [PRD FR-13.1](../../docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md) require genuine native consumption; the current [CodeGraphAnalysisAdapter](../../src/dotnet/Vulgata.Infrastructure/Analysis/CodeGraph/CodeGraphAnalysisAdapter.cs) is CLI/JSON and [project state](../../docs/agents/project-state.md) explicitly does not claim native MCP. **Alternatives**: CLI response wrapper, CodeGraphContext or simulated discovery do not satisfy the acceptance boundary. **Open verification**: native client package/API, CodeGraph launch mode, version and real tool catalogue in the selected host.

## Decision 3: Scope, lifetime and failure

**Decision**: Grant a reviewed purpose-scoped read-only subset after discovery; bind each finite session to one approved Repository; validate tool arguments and output against the current grant. Propagate cancellation and return distinct preparation/connection/discovery/invocation outcomes. **Rationale**: [AD-6/15](../../docs/bmad/planning-artifacts/architecture/ARCHITECTURE-SPINE.md), [O-NFR01/O-AR09](../../docs/bmad/planning-artifacts/october-release-epics.md) and [spec FR-002–FR-007](spec.md). **Alternatives**: unfiltered native surface, model-selected `projectPath`, shared mutable current-project session or silent source-only fallback break containment/truthful readiness. **Open verification**: precise response budgets and any safe same-binding sharing need qualification under existing host policy.

## Authority and compatibility

The [Constitution](../../.specify/memory/constitution.md) places tool/process adaptation in Infrastructure and bounded agent execution behind injected contracts. [October proposal item 11](../../docs/bmad/planning-artifacts/sprint-change-proposal-2026-09-26-october-batch-release.md) selects direct host execution; Docker application/tool packaging and qualification are outside this release. No database migration, public product API, general MCP registry, JDT-LS work or provider call is planned. Historical revision-bound CLI/JSON contracts remain their own scope. A mismatch with integrated 18.1 or actual native capabilities returns to definition and independent review before implementation.
