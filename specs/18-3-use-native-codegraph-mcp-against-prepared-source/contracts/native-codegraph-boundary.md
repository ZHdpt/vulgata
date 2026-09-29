# Native CodeGraph Worker Tool Boundary

This describes the Story 18.3 consumer contract, not a redefinition of Story 18.1's F0 source contract or a claim about installed native tool names. [Plan ID-I01–I03](../plan.md).

## Trusted input

- Application-supplied approved prepared Repository/root binding and current Worker purpose/grant, as provided by integrated Story 18.1. A trusted adapter's contained physical-access mechanism is reconciled later; this contract does not require an 18.1 workspace lease. The Worker/model cannot supply or replace these values.
- Declared CodeGraph installation/configuration and finite policy budgets supplied by host composition, never by tool arguments.
- Native tool name and bounded tool-specific arguments only after a reviewed purpose-scoped grant. `projectPath` and equivalent root selectors are fixed by the adapter outside model arguments; conflicting or nested selectors are rejected.

## Discovery and invocation

- Discovery returns only reviewed native read tools qualified for the current purpose/binding; an empty granted set reports an explicit limitation, not readiness.
- Invocation rechecks live binding, grant, path fields and budget. Returned source locations are verified and expressed relative to the approved Repository; outside-root or uncontainable results are denied, or explicitly limited/redacted where containment is proven.
- Preparation, connection, discovery and useful invocation are separately observable. A CLI query, CodeGraphContext value, simulated result or source-only fallback never carries a native MCP success label.

## Failure and lifecycle

Typed outcomes distinguish unavailable installation, unprepared source, preparation failure, connection failure, discovery/grant failure, invocation failure, no match, limited/incomplete output, timeout, cancellation and revoked/foreign binding. Diagnostic text is sanitized and bounded. Finite sessions release only owned client/process resources. Scan Control owns higher-level retry/pause; independently authorized 18.1 file tools can still operate. [AD-6/15](../../../docs/bmad/planning-artifacts/architecture/ARCHITECTURE-SPINE.md); [Story 18.3](../../../docs/bmad/planning-artifacts/october-release-epics.md#story-183-use-native-codegraph-mcp-against-prepared-source).

## Representative cases

| Case | Expected outcome |
|---|---|
| Approved binding, qualified read tool, fixture symbol | Native result with correct relative location and binding. |
| Tool argument supplies another project or physical path | Denied before native invocation; no outside-root disclosure. |
| Saved handle after grant revocation or binding change | Denied; session disposed. |
| Successful CLI preparation, MCP connection fails | Not ready; connection failure reported explicitly. |
| Native tool returns no match or incomplete index | Distinct no-match/limited evidence; no source-absence or coverage claim. |
