# Native CodeGraph Worker Tool Boundary

This is Story 18.3's native consumer and callable Agent seam, not a redefinition of Story 18.1's F0 reads or Story 18.2's shared source/root lease. The independent Core/Agent/native-client seam is implemented against controlled fixtures; physical preparation and app-bound invocation are held for a verified 18.2 lease and a separate user decision on CodeGraph-specific placement. [Plan ID-I01–I05/S03](../plan.md); [spec FR-010](../spec.md); [implementation evidence](../evidence/implementation-group-03.json).

## Callable public seam

The narrow pure tool-session contract is owned by Story 18.3 in `Vulgata.Core.DomainServices`; the Agent wrapper is in `Vulgata.Agents.WorkerExecution`, and the fixture native client is in Infrastructure. The following accepted callable shape preserves completed independent work. It defines no Story 18.2 lease or production placement constructor:

```csharp
public interface ICodeGraphWorkerToolSession : IAsyncDisposable
{
    Task<CodeGraphToolReadiness> DiscoverAsync(CancellationToken cancellationToken = default);
    Task<CodeGraphToolInvocationResult> ExploreAsync(
        string query, int maxFiles, CancellationToken cancellationToken = default);
}

public sealed class CodeGraphFunctions
{
    public CodeGraphFunctions(ICodeGraphWorkerToolSession session);
    public Task<CodeGraphToolRegistration> CreateAsync(CancellationToken cancellationToken = default);
}

public sealed record CodeGraphToolRegistration(
    CodeGraphToolReadiness Readiness,
    IReadOnlyList<Microsoft.Extensions.AI.AIFunction> Functions);
```

`CodeGraphToolReadiness` carries the bound Repository ID, preparation/connection/discovery stage, explicit outcome and sanitized limitation/failure code. `CodeGraphToolInvocationResult` carries the bound Repository ID, outcome, bounded verified Repository-relative source excerpts plus optional bounded structural summary, and sanitized limitation/failure code; a denied/unavailable/expired/cancelled result has no source/location. A source excerpt has relative path, bounded text and any safely verified line/range. No result exposes physical root, approval identity, native raw arguments, server instructions or process diagnostics. Exact encoding/private parser details remain implementation choices.

The trusted caller will construct the production session from application-selected exact `PreparedSourceApproval`, mandatory current verifier, declared CodeGraph executable/policy, an 18.2-issued source/root lease and a separately approved Story 18.3-owned staging/index project. Its producer signature remains pending the actual 18.2 checkpoint and user placement choice; it returns to affected plan/tasks and independent review before physical work. The independent wrapper/client tests are already complete and retain their evidence. `CodeGraphFunctions` borrows the caller-owned session: `CreateAsync` forwards cancellation to `DiscoverAsync`; success returns one fixed-schema `codegraph_explore` function, failure none. The trusted caller owns `DisposeAsync` after invocations settle. Saved functions cannot reopen a disposed/expired session. Valid calls forward cancellation unchanged to `ExploreAsync`; malformed fields return sanitized denial before session dispatch. [Callable-seam lesson](../../../docs/project-lessons.md); [18.1 public seam](../../016-contained-source-tools/contracts/prepared-source-tools.md); [implementation evidence](../evidence/implementation-group-01.json).

## Trusted input

- The integrated Story 18.1 `PreparedSourceApproval` fixes batch, Repository, subject, `WorkerSourceRead` purpose, approval identity and root-binding identity. `IPreparedSourceCurrentAuthorityVerifier.VerifyCurrentAsync` checks the exact tuple at session creation, discovery, native invocation and immediately before output disclosure. Revocation/change returns denied; unavailable verification returns authority-unavailable, both without buffered source/location. Its public Agent seam remains exactly list/read/search. [18.1 Core contract](../../../src/dotnet/Vulgata.Core/DomainServices/PreparedSourceContracts.cs).
- Story 18.2's identifiable verified checkpoint must supply shared prepared-source/root authority, contained reads and lease lifetime. Story 18.3 cannot infer a copy grant or writable CodeGraph index from it, create a parallel lease or extend Story 18.1's read context. [Current spec](../spec.md); [invalidation](../evidence/index-placement-invalidation-01.json).
- Pinned CodeGraph 1.5.0 stores `.codegraph` (or another single-segment `CODEGRAPH_DIR`) **inside** its project root; no installed external-index path exists. The recommended Story 18.3 staging project uses only regular source files copied through the verified 18.2 lease, conditional on explicit user authority. Proposed initial ceilings are one Repository, 10,000 regular files, 512 MiB and 120 seconds, with no ready partial index on cap overflow. Native `codegraph init` and `serve --mcp -p` may target only that approved disposable project; no live-root write, unapproved copy or symlink-farm substitution. If the decision/lease/placement proof is absent, return `Unavailable` before preparation. [Pinned-package evidence](../evidence/index-placement-invalidation-01.json); [placement analysis](../research.md#decision-4-pinned-index-placement-and-authority); [upstream external-storage request](https://github.com/colbymchenry/codegraph/issues/986).

## Discovery and invocation

- The native `initialize`/`tools/list` flow must find the reviewed `codegraph_explore` name/schema. The raw discovered `AITool` and server instructions are not passed through to the Worker; schema drift, an unknown native tool or empty grant reports explicit nonready status. [Raw discovery](../evidence/native-mcp-standalone-01.raw.json); [Microsoft .NET MCP guidance](https://learn.microsoft.com/en-us/agent-framework/agents/tools/local-mcp-tools).
- The model-visible wrapper schema is a closed JSON object with required nonempty UTF-8 `query` <=512 bytes and optional integral `maxFiles` 1–12 (default 4); `additionalProperties: false`. Unknown/nested authority selectors, `projectPath`, malformed values, write/command requests and unreviewed tool names are denied before native dispatch. Trusted configuration may lower caps but model input cannot raise them.
- The native schema's optional `projectPath` is supplied/fixed by Infrastructure to the approved disposable CodeGraph project bound to the exact 18.2 source lease, outside model arguments. Invocation rechecks exact live authority, staging/session lifetime and budget. Native source locations must map from staged regular files to the approved Repository-relative paths; unverified paths, symlink/reparse entries or observed changed input fail closed. Authority is rechecked before buffered bytes/locations are released. Server-authored advisory instructions and freshness/completeness claims remain untrusted content, not Worker policy.
- Preparation, connection, discovery and useful invocation are separately observable. A CLI query, CodeGraphContext value, simulated result or source-only fallback never carries a native MCP success label. The standalone probe is native transport evidence only.

## Failure and lifecycle

Typed outcomes distinguish unavailable placement/authority, unavailable installation, unprepared source, preparation, connection, discovery/schema/grant and invocation failures, no match, limited/incomplete output, timeout, cancellation and expired/foreign binding. Diagnostics are sanitized/bounded. Initial policy permits at most 10 native invocations and 2 minutes per binding-scoped session, 30 seconds per operation and 256 KiB result; counters are distinct from Story 18.1 file-tool budgets. Caller cancellation/deadlines flow through conditional staging, CLI preparation, stdio client, discovery, invocation and current-authority checks. On failure, authority loss, expiry or disposal, owned MCP/process activity and any authorized staged regular files plus index are settled; unrelated services and operator live source are untouched. Scan Control owns higher-level retry/pause; independently authorized 18.1 file tools remain usable but do not satisfy native CodeGraph readiness. [AD-6/15](../../../docs/bmad/planning-artifacts/architecture/ARCHITECTURE-SPINE.md); [Story 18.3](../../../docs/bmad/planning-artifacts/october-release-epics.md#story-183-use-native-codegraph-mcp-against-prepared-source).

## Representative cases

| Case | Expected outcome |
|---|---|
| Approved binding, qualified read tool, fixture symbol | Native result with correct relative location and binding. |
| Tool argument supplies another project or physical path | Denied before native invocation; no outside-root disclosure. |
| Saved handle after grant revocation, binding change or verifier unavailability | Denied or authority-unavailable; buffered output discarded and session closed. |
| Successful CLI preparation, MCP connection fails | Not ready; connection failure reported explicitly. |
| Native `codegraph_explore` schema changes or raw server instructions urge broader access | Nonready/limited; no raw tool or instructions become Worker policy. |
| Native tool returns no match or incomplete index | Distinct no-match/limited evidence; no source-absence or coverage claim. |
| No explicit temporary-copy decision or verified 18.2 lease | `Unavailable` before staging/init; no live-root write or source copy. |
| Approved staged project includes a symlink/reparse entry or source changes during staging | Fail closed, remove owned staging/index, preserve operator root. |
