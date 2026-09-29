# Native CodeGraph Worker Tool Boundary

This is the Story 18.3 consumer and callable Agent seam, not a redefinition of Story 18.1's F0 read contract or Story 18.2's pending shared trusted physical-workspace contract. Owned standalone evidence observed CodeGraph 1.5.0 native `codegraph_explore`; it does not establish Vulgata Agent authorization. [Plan ID-I01–I05](../plan.md); [probe](../evidence/native-mcp-standalone-01.json).

## Callable public seam

The planned narrow pure tool-session contract is owned by Story 18.3 in `Vulgata.Core.DomainServices`; Infrastructure implements it after receiving the verified Story 18.2 shared workspace through trusted composition. The Agent wrapper is a separate Story 18.3 type in `Vulgata.Agents.WorkerExecution`. These signatures specify what tests and Worker composition may call; they do not define or invent Story 18.2's physical-access interface:

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

The trusted caller constructs the production session from application-selected exact `PreparedSourceApproval`, mandatory current verifier, declared CodeGraph executable/policy and Story 18.2's verified physical workspace. That producer's public constructor/factory signature is **pending the Story 18.2 checkpoint** and returns to affected plan/tasks and independent review before physical integration; Story 18.3 cannot provide a replacement lease or use Story 18.1's private handles. Tasks for independent client/wrapper/controlled-test work may be generated only after accepted plan review and planned-change authorization; implementation against controlled sessions additionally needs accepted complete-definition review and implementation approval. None of this permits physical-root preparation, application-bound native invocation or Vulgata qualification before the checkpoint. `CodeGraphFunctions` constructor rejects null, borrows an already issued session, and `CreateAsync` forwards cancellation to `DiscoverAsync`. Success returns exactly one fixed-schema `codegraph_explore` `AIFunction`; any nonready outcome returns an empty function list with its readiness. The trusted caller owns session `DisposeAsync` after all invocations settle. A function saved past disposal/expiry returns sanitized closed status and never reconnects. Valid function calls forward cancellation unchanged to `ExploreAsync`; malformed/unknown fields return sanitized denied without session dispatch. [Callable-seam lesson](../../../docs/project-lessons.md); [18.1 public seam](../../016-contained-source-tools/contracts/prepared-source-tools.md).

## Trusted input

- The integrated Story 18.1 `PreparedSourceApproval` fixes batch, Repository, subject, `WorkerSourceRead` purpose, approval identity and root-binding identity. `IPreparedSourceCurrentAuthorityVerifier.VerifyCurrentAsync` checks the exact tuple at session creation, discovery, native invocation and immediately before output disclosure. Revocation/change returns denied; unavailable verification returns authority-unavailable, both without buffered source/location. Its public Agent seam remains exactly list/read/search. [18.1 Core contract](../../../src/dotnet/Vulgata.Core/DomainServices/PreparedSourceContracts.cs).
- Story 18.2's verified shared trusted physical workspace supplies contained physical access to Infrastructure only. Until its identifiable checkpoint exists, no Story 18.3 construction can claim authorized root access. It is not a model argument or an extension to Story 18.1's read context. [Current spec](../spec.md).
- Declared CodeGraph executable/configuration and finite policy budgets come from trusted host composition, never tool arguments. The standalone probe ran `codegraph init <owned fixture>` and `codegraph serve --mcp -p <owned fixture> --no-watch`. `codegraph init` writes `.codegraph` in its target project tree. Production commands may target only a Story 18.2-verified contained tool workspace with safe writable index state tied to approved source; the fixture is not permission to write the operator-prepared live root or duplicate source. If the workspace cannot provide that placement, preparation is unavailable pending plan/owner correction. [Probe](../evidence/native-mcp-standalone-01.json); [plan ID-S03/F04](../plan.md).

## Discovery and invocation

- The native `initialize`/`tools/list` flow must find the reviewed `codegraph_explore` name/schema. The raw discovered `AITool` and server instructions are not passed through to the Worker; schema drift, an unknown native tool or empty grant reports explicit nonready status. [Raw discovery](../evidence/native-mcp-standalone-01.raw.json); [Microsoft .NET MCP guidance](https://learn.microsoft.com/en-us/agent-framework/agents/tools/local-mcp-tools).
- The model-visible wrapper schema is a closed JSON object with required nonempty UTF-8 `query` <=512 bytes and optional integral `maxFiles` 1–12 (default 4); `additionalProperties: false`. Unknown/nested authority selectors, `projectPath`, malformed values, write/command requests and unreviewed tool names are denied before native dispatch. Trusted configuration may lower caps but model input cannot raise them.
- The native schema's optional `projectPath` is supplied/fixed by Infrastructure to the verified contained workspace bound to the approved source, outside model arguments. Invocation rechecks exact live authority, session lifetime and budget. The native result is parsed/validated as bounded source evidence, locations become Repository-relative, and authority is rechecked before any buffered byte/location is released. Outside-root or uncontainable data is denied; a provably contained but capped result reports `limited`. Server-authored advisory instructions and claims of verbatim freshness/completeness are untrusted content, not Worker policy.
- Preparation, connection, discovery and useful invocation are separately observable. A CLI query, CodeGraphContext value, simulated result or source-only fallback never carries a native MCP success label. The standalone probe is native transport evidence only.

## Failure and lifecycle

Typed outcomes distinguish unavailable installation, unprepared source, preparation failure, connection failure, discovery/schema/grant failure, invocation failure, no match, limited/incomplete output, timeout, cancellation, authority denied/unavailable and expired/foreign binding. Diagnostics are sanitized/bounded. Initial policy permits at most 10 native invocations and 2 minutes per binding-scoped session, 30 seconds per operation and 256 KiB result; these counters are distinct from Story 18.1 file-tool budgets and server advisory text. Caller cancellation/deadlines flow through CLI preparation, stdio client, discovery, invocation and current-authority checks. On failure, authority loss, expiry or disposal, owned MCP client/process activity is settled and buffered output discarded; unrelated services and other roots are untouched. Scan Control owns higher-level retry/pause; independently authorized 18.1 file tools can still operate. [AD-6/15](../../../docs/bmad/planning-artifacts/architecture/ARCHITECTURE-SPINE.md); [Story 18.3](../../../docs/bmad/planning-artifacts/october-release-epics.md#story-183-use-native-codegraph-mcp-against-prepared-source).

## Representative cases

| Case | Expected outcome |
|---|---|
| Approved binding, qualified read tool, fixture symbol | Native result with correct relative location and binding. |
| Tool argument supplies another project or physical path | Denied before native invocation; no outside-root disclosure. |
| Saved handle after grant revocation, binding change or verifier unavailability | Denied or authority-unavailable; buffered output discarded and session closed. |
| Successful CLI preparation, MCP connection fails | Not ready; connection failure reported explicitly. |
| Native `codegraph_explore` schema changes or raw server instructions urge broader access | Nonready/limited; no raw tool or instructions become Worker policy. |
| Native tool returns no match or incomplete index | Distinct no-match/limited evidence; no source-absence or coverage claim. |
