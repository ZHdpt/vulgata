# Quickstart: Validate Story 12.3

This guide is the accepted validation target for later implementation. It does not authorize task generation or production work.

## 1. Prerequisites

- Windows host checkout on a non-detached Story 12.3 branch.
- .NET 10 SDK.
- PostgreSQL 17 as the only required Docker service.
- CodeGraph `@colbymchenry/codegraph@1.5.0` and its accepted Windows x64 hashes.
- Java 21.
- `jdt-language-server-1.61.0-202608141332` with its accepted archive SHA-256.
- StreamJsonRpc 2.25.29 from central package management.
- two authorized neutral Repositories with an exact approved Story 12.2 pair of 50-150 File Code Units.
- a controllable OpenAI-compatible Worker provider for deterministic outage/tool-call qualification; real provider credentials remain configured/encrypted and are never printed.

Confirm topology without exposing paths or secrets:

```pwsh
docker ps --format "table {{.Names}}\t{{.Image}}\t{{.Status}}"
dotnet --info
java -version
codegraph --version
```

Expected: PostgreSQL 17 is the sole required Docker service; Vulgata, CodeGraph, Java, and JDT LS are host processes; CodeGraph reports 1.5.0 and Java reports major 21.

## 2. Static contract checks

Validate the strict schema parses and contains no open object shapes:

```pwsh
Get-Content -Raw specs/012-resumable-file-pass1-code-logic/contracts/code-logic-file-v2.schema.json |
  ConvertFrom-Json | Out-Null
rg -n '"additionalProperties": true|CodeGraphContext|retrieve_code_logic|Pass2BusinessLogic|Business Logic Document' specs/012-resumable-file-pass1-code-logic
```

Expected: JSON parsing succeeds; no `additionalProperties: true`; CodeGraphContext/Story 12.4 terms appear only in explicit exclusions/forbidden descriptions.

## 3. Canonical identity and projection

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass1CanonicalEnvelopeTests|FullyQualifiedName~CodeLogicFileV2SchemaTests|FullyQualifiedName~CodeLogicFileV2ProjectionTests"
```

Expected:

- literal envelope/digest/UUID vectors pass against production and independent codecs;
- smallest/multi-item/Unicode/asymmetric-UUID vectors pass;
- every unknown/duplicate/unsafe/over-byte/depth/order/range/reference mutation rejects;
- every changed communication kind/level/confidence/rule/provenance/classification fingerprint/Catalog Rule Identity/Evidence Locator and every mixed-kind binding rejects;
- canonical ContentJson, Markdown, at most nine Sections, Index Entry, anchor/Edge IDs and commit fingerprint are byte-identical across repeated runs;
- no projection contains a claim absent from JSON.

## 4. Pair start, shared concurrency, and order

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass1PairStartTests|FullyQualifiedName~Pass1ConcurrencyAndOrderingTests|FullyQualifiedName~Pass1ConcurrencyUpdateTests"
```

Expected:

- exactly two complete approved manifests start;
- all missing/partial/stale/mismatched/hidden packages create zero jobs;
- every File Code Unit has exactly one accepted Story 12.1 deterministic job;
- default capacity is two; 1/2/3/4 work; 0/5/noninteger reject before dispatch;
- authorized versioned 4->1 and 1->4 updates affect only later claims, preserve valid leases, and stop claims while active leases meet/exceed a decreased limit;
- current/equal update is a zero-write no-op; exact lost-ack update replay is zero-write; stale version, changed actor/target, hidden ownership, reconciling, and terminal updates reject;
- original Start replay compares the immutable initial limit and returns the current effective limit; it cannot mutate concurrency;
- reversed inputs and at least ten completion schedules keep job/Document/display identities identical;
- pair order uses Pair Selection Ordinal and Repository-local order uses Manifest Ordinal.

## 5. Navigation and bounded Agent loop

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~NavigateCodeContractTests|FullyQualifiedName~NavigateCodeSecurityTests|FullyQualifiedName~PreparedPass1WorkerDispatcherTests"
```

Expected:

- all eight operations return only authorized current Document or exact source evidence;
- current-Document and source-fallback evidence share stable target/observation identity;
- unavailable and unauthorized shapes are indistinguishable;
- 8 calls, 20 results, 65,536 cumulative bytes, one hop and depth 3 are hard limits with no partial evidence;
- first depth-four, over-results, over-bytes, and accepted-call/tool-attempt-eight exhaustion persist metadata-only receipts/marker that release no evidence but permit a schema-valid `BoundExceeded` deferred observation;
- BoundExceeded receipts cannot support a positive relationship or Communication Kind claim, and no ninth tool attempt occurs;
- retries use the remaining durable budget;
- repeated rejected calls cannot exceed eight tool attempts or ten provider turns;
- no scheduling, raw protocol/storage, CodeGraphContext, shell, network, unrestricted filesystem, host path, other Repository or stale-revision evidence is reachable.

## 6. Atomic v2 Document commit and replay

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass1DocumentCommitTests"
```

Expected:

- one valid job commits one immutable Code Logic Document Version and derived graph;
- every single identity/authorization/path/hash/range/schema/bound/reference/classification/receipt/anchor mutation writes zero graph rows and leaves the job non-Completed;
- exact lost-ack replay returns the same Document Version with zero mutation;
- changed/reordered/renormalized/partial replay conflicts;
- Version Chain owner remains Repository/path/segment and older immutable version remains preserved.

## 7. Failure circuit, retry, pause, and resume

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass1FailurePolicyTests|FullyQualifiedName~Pass1ProviderCircuitTests|FullyQualifiedName~Pass1RecoveryAndResumeApiTests"
```

Expected:

- provider transient permits exactly five ordered 10/30/60/120/300-second nominal windows with deterministic jitter and bounded Retry-After;
- no new job is claimed while probing;
- fifth failure persists pair/child provider-unavailable pause and `继续扫描`;
- success reopens only remaining valid work;
- isolated JobTransient attempts the same job at most twice across restart;
- provider configuration/unclassified systemic pause correctly;
- integrity/security stop new dispatch and fail closed;
- restart/manual resume revalidates both child Scans and exact pair, skips completed jobs, recovers only expired/due eligible work, and never duplicates Documents;
- unauthenticated/policy/ownership-filtered API behavior remains 401/403/indistinguishable 404.

## 8. Partial completion and local reconciliation

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass1ReconciliationTests|FullyQualifiedName~Pass1FailurePolicyTests"
```

Expected:

- denominator counts prepared File Code Units once, never attempts/probes/replays;
- zero, exact, and one-over `min(5,floor(total/10))` cases have exact outcomes;
- every accepted high-value seed is mandatory regardless of budget;
- clean child is Completed; child with bounded ordinary failures is CompletedWithWarnings;
- same authoritative inputs in ten completion orders yield the same canonically ordered warning inventory and Edge set;
- only exact current same-Repository/same-Scan-Run/revision/hash targets create REFERENCES Edges;
- ambiguous/stale/unsupported/cross-Repository/self/duplicate targets create no Edge;
- reconciliation mutates zero Document Versions and only advances the coordination to Story 12.4's `Resolving` handoff.

## 9. PostgreSQL 17 atomicity and migration

Use a disposable dedicated database; do not point these tests at user data.

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass1PairPostgreSqlAtomicTests|FullyQualifiedName~Pass1DocumentPostgreSqlAtomicTests"
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass1MigrationTests"
```

Expected:

- pair start and Document/job/reconciliation expose full commit or none at every injected boundary;
- competing claims never exceed capacity;
- stale writers, lost acknowledgements, cancellation and forced exceptions leave no partial rows;
- canonical receipts/outcomes/replay reconstruct from PostgreSQL scalar rows rather than trusting digests;
- concurrency-change audit records and exact update replay survive competing claims/restart without changing existing leases or deterministic scan/content identities;
- migration clean apply/rollback/reapply passes;
- historical v1 Documents/anchors/jobs remain readable;
- v1 and v2 schema identities coexist; v2 byte anchors are required by the new path;
- EF reports no pending model changes.

## 10. Real tool and host qualification

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~NavigateCodeRealToolQualificationTests"
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass1HostRuntimeQualificationTests"
```

Expected:

- real CodeGraph/JDT-LS/source operations are revision-bound and sanitized;
- all supported neutral Java operations succeed; XML/source and unsupported combinations return the closed bounded outcome;
- exact pinned runtime identities/hashes are recorded in the evidence artifact, not Documents;
- one approved two-Repository Pass 1 finishes at concurrency 1/2/4 with identical identities/order/Edges;
- PostgreSQL 17 is the only required Docker service;
- zero production dependency on CodeGraphContext.

This is Story 12.3 qualification only. Do not start Cross-Repository Resolution, Pass 2, Business Logic Documents, Chat, evaluation questions, or final release run.

## 11. Security/composition qualification

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~Pass1ProductionBoundaryQualificationTests"
```

Expected artifact: `artifacts/qualification/story-12.3/production-boundary-inspection.json`, recording inspected production assemblies/resources, denylist hash, tool-registration inventory, schema/prompt/runtime identities, and zero forbidden matches. Test assemblies/fixtures are not production scan targets.

Any match routes to a requirement-backed behavioral regression test; qualification does not edit production to hide it.

## 12. Final verification barrier

```pwsh
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~PreparedPass1"
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~DurableScanning|FullyQualifiedName~PreparedScanning|FullyQualifiedName~KnowledgeStore|FullyQualifiedName~AnalysisRuntime"
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj
dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj
git diff --check
```

Report focused and full results separately. Report a zero-error build separately from warnings. A known unrelated full-suite baseline failure must be named exactly and must not be presented as Story 12.3 success without the workflow's accepted verification treatment.

Completion evidence must bind the reviewed production/test boundary, migration and PostgreSQL results, real-tool/host evidence, convergence, quality review, E2E decision/result, and the root-owned checkpoint. Clean up only the disposable Story 12.3 database/container; leave existing PostgreSQL/user services untouched.
