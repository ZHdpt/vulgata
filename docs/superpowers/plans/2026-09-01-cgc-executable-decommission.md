# CGC Executable Decommission Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove executable CodeGraphContext dependencies while preserving Epic 12's CodeGraph 1.5/JDT-LS/Prepared Scan path and leaving legacy Repository profiling explicitly unimplemented.

**Architecture:** Delete the CGC provider/runtime/qualification branch at its source, collapse the legacy `RepositoryProfilingCoordinator` to a registered `NotImplementedException` boundary, and retain all modern analysis ports, Prepared Scan persistence, Prepared Pass 1, and Prepared Pass 2. Historical planning/evidence and the EF model remain untouched.

**Tech Stack:** .NET 10, ASP.NET Core/Blazor, EF Core/PostgreSQL persistence retained unchanged, CodeGraph 1.5 and JDT-LS retained, Dockerfile cleanup only.

**Spec:** `docs/superpowers/specs/2026-09-01-cgc-executable-decommission-design.md`

## Global Constraints

- Work in the existing local workspace and current branch; do not create a worktree, reset, rebase, merge, pull, or push.
- Re-observe `git status --short --branch` and `git rev-parse HEAD` before editing; preserve unrelated user changes.
- Historical Epic 4, Epic 11, Epic 12, SpecKit, BMad, review, decision, checkpoint, and evidence artifacts are read-only.
- Preserve `src/dotnet/Vulgata.Infrastructure/Analysis/CodeGraph/**`, `Analysis/Java/**`, revision-bound source readers, Prepared Scan, Prepared Pass 1, Prepared Pass 2, and their current production registrations.
- Preserve every EF entity/configuration/migration/model-snapshot file and all durable data shapes.
- Use `NotImplementedException` at the legacy Repository profiling boundary; do not create an HTTP 503 placeholder or adapt that route to Prepared Scan.
- Do not add anti-restoration tests or placeholder-behavior tests.
- Tests are not an acceptance gate. Delete or minimally edit obsolete test source only as needed for the solution to compile.
- The sole technical acceptance command is `dotnet build Vulgata.slnx` with exit code zero. `git diff --check` and changed-file inventory are mandatory handoff hygiene, not additional product acceptance gates.
- Use `apply_patch` for source edits and explicit named-path staging. Commit after each completed task.

## File Structure and Ownership

| Path | Responsibility | Planned disposition |
| --- | --- | --- |
| `docker/Dockerfile` | Application image | Remove Python/CGC stage and copied runtime; preserve .NET build/runtime and generic Git workspace setup |
| `docker/codegraph/requirements.lock` | CGC Python lock | Delete |
| `src/dotnet/Vulgata.Infrastructure/CodeGraph/CodeGraphContextProvider.cs` | CGC HTTP/provider integration | Delete |
| `src/dotnet/Vulgata.Core/DomainServices/RepositoryProfilingContracts.cs` | Mixed generic profiling and CGC projection contracts | Remove only `ICodeGraphProvider` and CGC-only provider projection records |
| `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs` | Legacy Repository Scan application boundary | Replace implementation with dependency-free `NotImplementedException` stub; retain result/interface signatures |
| `src/dotnet/Vulgata.Web/Program.cs` | Production composition | Remove `CodeGraphOptions` and dead unavailable coordinator; register the legacy stub; preserve modern adapters/Prepared Scan |
| `src/dotnet/Vulgata.Infrastructure/Git/BackupB/BackupBDirectProvisioner.cs` | Historical Backup B code | Remove obsolete CGC-specific comment wording only |
| `src/dotnet/Vulgata.Agents/Prompts/pass1-code-logic-worker.v2.md` | Current Prepared Pass 1 prompt | Replace the obsolete product name in its prohibition with generic `legacy analysis-provider access`; preserve tool authority |
| CGC-only and Story 11.5 release test files named in Task 3 | Obsolete executable qualification | Delete |
| Mixed modern test files named in Task 3 | Compilation and retained modern coverage | Remove CGC-only assertions/helpers or replace deleted type anchors |

---

### Task 1: Remove the containerized CGC runtime

**Files:**

- Modify: `docker/Dockerfile`
- Delete: `docker/codegraph/requirements.lock`

**Interfaces:**

- Consumes: existing .NET publish stage and runtime image
- Produces: an application Dockerfile with no Python/CGC build stage or runtime copy

- [ ] **Step 1: Re-observe the exact Docker dependency**

Run:

```pwsh
rg -n "codegraphcontext|CodeGraphContext|cgc version|COPY --from=codegraph|PYTHON" docker docker-compose.yml
```

Expected: matches only in `docker/Dockerfile` and `docker/codegraph/requirements.lock`; stop and report before editing if another executable Docker/Compose dependency appears.

- [ ] **Step 2: Remove the CGC image stage and runtime copy**

Edit `docker/Dockerfile` so it begins with the existing source-label arguments followed directly by the .NET build stage:

```dockerfile
ARG VULGATA_SOURCE_COMMIT
ARG VULGATA_SOURCE_TREE
ARG VULGATA_PRODUCTION_SOURCE_MANIFEST_SHA256

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
```

Remove:

- the complete `FROM python:3.12-bookworm ... AS codegraph` stage;
- its `requirements.lock`, pip, and `cgc version` commands;
- both `COPY --from=codegraph` instructions;
- `PYTHONDONTWRITEBYTECODE` and `PYTHONNOUSERSITE`.

Retain the Git/CA/curl installation and `/var/lib/vulgata-codegraph` directory because the current `GitWorkingCopyOptions` still derives its generic Repository workspace beneath that configured data root. Rewrite the adjacent comment to describe application runtime prerequisites, not CGC.

- [ ] **Step 3: Delete the Python lock**

Delete exactly:

```text
docker/codegraph/requirements.lock
```

Remove the now-empty `docker/codegraph` directory if Git has no other tracked file there.

- [ ] **Step 4: Verify the Docker source boundary**

Run:

```pwsh
rg -n "codegraphcontext|CodeGraphContext|cgc version|COPY --from=codegraph|PYTHON" docker docker-compose.yml
```

Expected: no matches.

- [ ] **Step 5: Commit Task 1**

```pwsh
git add -- docker/Dockerfile docker/codegraph/requirements.lock
git diff --cached --check
git commit -m "chore: remove CGC container runtime"
```

### Task 2: Collapse legacy Repository profiling to the explicit placeholder

**Files:**

- Delete: `src/dotnet/Vulgata.Infrastructure/CodeGraph/CodeGraphContextProvider.cs`
- Modify: `src/dotnet/Vulgata.Core/DomainServices/RepositoryProfilingContracts.cs`
- Modify: `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`
- Modify: `src/dotnet/Vulgata.Web/Program.cs`
- Modify: `src/dotnet/Vulgata.Infrastructure/Git/BackupB/BackupBDirectProvisioner.cs`
- Modify: `src/dotnet/Vulgata.Agents/Prompts/pass1-code-logic-worker.v2.md`

**Interfaces:**

- Consumes: existing `IRepositoryProfilingCoordinator` API/UI signatures
- Produces: `RepositoryProfilingCoordinator : IRepositoryProfilingCoordinator` with no runtime dependencies; every operation throws `NotImplementedException`
- Preserves: `ICodeGraphAnalysisPort`, `IPreparedCodeGraphAnalysisPort`, `IJavaAnalysisSessionFactory`, `PreparedScanInitiationCoordinator`, `PreparedScanCoordinator`, and `PreparedScanPairBuilder`

- [ ] **Step 1: Delete the CGC provider implementation**

Delete exactly:

```text
src/dotnet/Vulgata.Infrastructure/CodeGraph/CodeGraphContextProvider.cs
```

- [ ] **Step 2: Remove CGC-only Core contracts**

In `RepositoryProfilingContracts.cs`, preserve the Git/snapshot/candidate/framework contracts. Remove:

```csharp
public interface ICodeGraphProvider
{
    Task<CodeGraphProviderResult> ProfileAsync(
        string workingCopyPath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
```

Also remove the contiguous CGC-only model family beginning with `CodeGraphProviderResult` and ending with `ProviderCandidateOutcome`. Do not remove `ScanSourceCandidate`, framework-evidence types, `StructuralRelationshipKind`, or any Prepared Scan/analysis-port type.

- [ ] **Step 3: Replace the legacy coordinator implementation**

In `RepositoryProfilingCoordinator.cs`, retain `RepositoryProfilingOutcome`, `RepositoryProfilingResult`, and `IRepositoryProfilingCoordinator`. Replace the dependency-heavy implementation with this shape:

```csharp
public sealed class RepositoryProfilingCoordinator : IRepositoryProfilingCoordinator
{
    private const string _message =
        "Legacy Repository profiling is not implemented after CGC decommission.";

    public Task<RepositoryProfilingResult> StartSystemRepositoryAsync(
        Guid systemId,
        Guid repositoryId,
        IUserContext user,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(_message);

    public Task<RepositoryProfilingResult> StartStandaloneRepositoryAsync(
        Guid repositoryId,
        IUserContext user,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(_message);

    public Task<ScanRunSummaryDto?> GetSystemRepositoryRunAsync(
        Guid systemId,
        Guid repositoryId,
        Guid scanRunId,
        IUserContext user,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(_message);

    public Task<ScanRunSummaryDto?> GetStandaloneRepositoryRunAsync(
        Guid repositoryId,
        Guid scanRunId,
        IUserContext user,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(_message);
}
```

Remove obsolete usings. Do not catch or translate this exception in the legacy endpoints or UI.

- [ ] **Step 4: Correct production composition**

In `Program.cs`:

1. Add the following registration beside the Repository/Scan application services:

```csharp
builder.Services.AddScoped<IRepositoryProfilingCoordinator, RepositoryProfilingCoordinator>();
```

2. Remove only the `CodeGraphOptions` singleton construction. Retain `profilingDataRoot` and construct `GitWorkingCopyOptions` from `Path.Combine(profilingDataRoot, "repositories")`.
3. Delete the complete internal `RuntimeUnavailableRepositoryProfilingCoordinator` class.
4. Preserve the registrations that map `CodeGraphAnalysisAdapter` to `ICodeGraphAnalysisPort` and `IPreparedCodeGraphAnalysisPort`, the JDT-LS factory, and every Prepared Scan/Pass 1/Pass 2 registration.

- [ ] **Step 5: Remove obsolete executable wording**

In `BackupBDirectProvisioner.cs`, replace the comment reference to `CodeGraphContext or ICodeGraphProvider` with `the retired legacy analysis provider`.

In `pass1-code-logic-worker.v2.md`, replace only the `CodeGraphContext` noun in the raw-tool prohibition with `legacy analysis-provider access`. Preserve every other tool, source, authorization, and result constraint.

- [ ] **Step 6: Prove production projects compile independently**

Run:

```pwsh
dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj
```

Expected: exit code zero. Repair only compiler errors caused by this task. Do not change EF files or modern Prepared Scan/analysis contracts.

- [ ] **Step 7: Verify deleted production symbols**

Run:

```pwsh
rg -n "CodeGraphContextProvider|ICodeGraphProvider|CodeGraphOptions|RuntimeUnavailableRepositoryProfilingCoordinator|codegraphcontext" src/dotnet -g "*.cs" -g "*.md"
```

Expected: no matches. `IRepositoryProfilingCoordinator` and `RepositoryProfilingCoordinator` remain expected placeholder symbols.

- [ ] **Step 8: Commit Task 2**

Stage only the six named production paths plus the deleted provider, then run:

```pwsh
git diff --cached --check
git commit -m "refactor: retire legacy CGC profiling"
```

### Task 3: Remove obsolete CGC test and release-harness source

**Files:**

- Delete: `tests/Vulgata.Tests/CodeGraphContextProviderTests.cs`
- Delete: `tests/Vulgata.Tests/CodeGraphProviderContractTests.cs`
- Delete: `tests/Vulgata.Tests/CodeGraphProviderContract/runner.py`
- Delete: `tests/Vulgata.Tests/CodeGraphQualification/**`
- Delete: `tests/Vulgata.Tests/RepositoryProfilingTests.cs`
- Delete: `tests/Vulgata.Tests/Story43EndToEndTests.cs`
- Delete: `tests/Vulgata.Tests/ArchitectureRemediationContractTests.cs`
- Delete: `tests/Vulgata.Tests/DemoChatReleaseGatePostgreSqlTests.cs`
- Delete: `tests/Vulgata.Tests/ReleaseGate/Story115ReleaseHarness.cs`
- Delete: `tests/Vulgata.Tests/ReleaseGate/Provisioner/**`
- Delete: `tests/Vulgata.Tests/ReleaseGate/loopback-openai-provider.py`
- Modify: `tests/Vulgata.Tests/Vulgata.Tests.csproj`
- Modify: `tests/Vulgata.Tests/AnalysisRuntime/AnalysisRuntimeHealthTests.cs`
- Modify: `tests/Vulgata.Tests/AnalysisRuntime/CodeGraphAdapterTests.cs`
- Modify: `tests/Vulgata.Tests/BackupBDirectProvisionerTests.cs`
- Modify: `tests/Vulgata.Tests/RepositoryApiTests.cs`
- Modify: `tests/Vulgata.Tests/PreparedScanning/PreparedScanRuntimeIdentityResolverTests.cs`
- Modify: `tests/Vulgata.Tests/PreparedScanning/PreparedCodeGraphReadinessTests.cs`
- Modify: `tests/Vulgata.Tests/PreparedScanning/PreparedScanBoundaryTests.cs`
- Modify: `tests/Vulgata.Tests/PreparedScanning/PreparedScanInitiationApiTests.cs`
- Modify: `tests/Vulgata.Tests/PreparedPass1/NavigateCodeSecurityTests.cs`
- Modify: `tests/Vulgata.Tests/PreparedPass1/Pass1ProductionBoundaryQualificationTests.cs`

**Interfaces:**

- Consumes: Task 2's deleted provider types and retained legacy placeholder interface
- Produces: a compiling test project with no CGC provider, qualification, release-harness, or anti-restoration source
- Preserves: Story 12.5 test files under `ReleaseGate/Story125/**` and modern CodeGraph 1.5/Prepared Scan tests

- [ ] **Step 1: Delete the CGC-only suites and Story 11.5 executable harness**

Delete exactly the files/directories listed as `Delete` above. Preserve all Markdown/JSON evidence under `docs/**` and `specs/**`. Preserve `tests/Vulgata.Tests/ReleaseGate/Story125/**`.

- [ ] **Step 2: Remove the obsolete provisioner exclusion**

In `Vulgata.Tests.csproj`, remove this now-empty item group:

```xml
<ItemGroup>
  <Compile Remove="ReleaseGate/Provisioner/**/*.cs" />
</ItemGroup>
```

- [ ] **Step 3: Retain modern CodeGraph tests without a CGC type anchor**

In `AnalysisRuntime/CodeGraphAdapterTests.cs`, replace:

```csharp
Assembly infrastructureAssembly = typeof(CodeGraphContextProvider).Assembly;
```

with:

```csharp
Assembly infrastructureAssembly = typeof(CodeGraphAnalysisAdapter).Assembly;
```

Remove the now-unused `using Vulgata.Infrastructure.CodeGraph;` directive. Keep all CodeGraph 1.5 identity, command, parser, and bounded-port assertions.

- [ ] **Step 4: Remove anti-restoration assertions without removing positive modern coverage**

Make these exact edits:

- `AnalysisRuntimeHealthTests.cs`: remove the two CGC assertions from `OrdinaryStartup_DoesNotCreateIndexOrImportRepository`; delete `ProductionComposition_DoesNotRequireHistoricalCodeGraphContextOrPython` completely.
- `BackupBDirectProvisionerTests.cs`: delete the `CGC Non-Invocation Tests` region containing `Provisioner_Constructor_DoesNotInject_CodeGraphContext` and `Provisioner_Assembly_HasNoCodeGraphContextReferences`; preserve remaining Backup B tests.
- `PreparedScanRuntimeIdentityResolverTests.cs`: rename `DefaultComposition_UsesCodeGraph15AndJdtLsPortsWithoutLegacyProfiling` to `DefaultComposition_UsesCodeGraph15AndJdtLsPorts`; retain positive modern-port assertions and remove legacy profiling reflection/string-negative assertions.
- `PreparedCodeGraphReadinessTests.cs`: remove only the assertion that the adapter type name does not contain `CodeGraphContext`.
- `PreparedScanBoundaryTests.cs`, `NavigateCodeSecurityTests.cs`, and `Pass1ProductionBoundaryQualificationTests.cs`: remove only the `CodeGraphContext` literal from forbidden-name arrays; preserve all generic raw CodeGraph/LSP/storage/shell/network restrictions.
- `PreparedScanInitiationApiTests.cs`: remove the `Assert.False(scenario.CgcResolved)` assertion and the complete `CgcResolved` property.

- [ ] **Step 5: Remove the one mixed test that constructs the deleted coordinator shape**

In `RepositoryApiTests.cs`, delete the complete test method:

```text
ManagementSystemScanRunRead_WhenVisibilityIsRevokedDuringRead_ReturnsNotFoundWithoutProfilingDisclosureAsync
```

Preserve the endpoint, authorization, Repository CRUD, and interface-stub tests that still compile against `IRepositoryProfilingCoordinator`.

- [ ] **Step 6: Build the complete solution**

Run:

```pwsh
dotnet build Vulgata.slnx
```

Expected: exit code zero. If compilation identifies another test source that directly constructs or implements a deleted CGC type, remove the smallest CGC-specific method/helper. Do not delete a modern Prepared Scan, CodeGraph 1.5, JDT-LS, Prepared Pass 1, Prepared Pass 2, chat, management, or persistence file merely to silence an unrelated compiler error.

- [ ] **Step 7: Verify executable CGC source is absent**

Run:

```pwsh
rg -n "CodeGraphContextProvider|ICodeGraphProvider|CodeGraphOptions|codegraphcontext|cgc version" src tests docker -g "*.cs" -g "*.py" -g "*.csproj" -g "Dockerfile" -g "*.lock"
```

Expected: no matches. Markdown specifications, evidence, prompts, and historical JSON are outside this command and remain untouched.

- [ ] **Step 8: Commit Task 3**

Stage only the Task 3 paths. Review the deleted-file list explicitly, then run:

```pwsh
git diff --cached --check
git commit -m "test: remove obsolete CGC qualification source"
```

### Task 4: Final preserved-boundary audit and handoff

**Files:**

- Modify: none expected
- Read: `docs/superpowers/specs/2026-09-01-cgc-executable-decommission-design.md`
- Read: `src/dotnet/Vulgata.Web/Program.cs`
- Read: `src/dotnet/Vulgata.Web/Data/PreparedScanPairBuilder.cs`
- Read: `src/dotnet/Vulgata.Web/Data/PreparedScanCoordinator.cs`

**Interfaces:**

- Consumes: committed Tasks 1–3
- Produces: final build evidence, exact changed-file inventory, and explicit preserved-gap report

- [ ] **Step 1: Verify the preserved modern composition**

Run:

```pwsh
rg -n "ICodeGraphAnalysisPort|IPreparedCodeGraphAnalysisPort|CodeGraphAnalysisAdapter|IJavaAnalysisSessionFactory|PreparedScanInitiationCoordinator|IPreparedScanPairBuilder|PreparedScanCoordinator" src/dotnet/Vulgata.Web/Program.cs
```

Expected: all seven modern symbols remain registered.

- [ ] **Step 2: Verify the Prepared Scan endpoint remains mapped**

Run:

```pwsh
rg -n "MapPreparedScanApiEndpoints|/api/systems/\{systemId:guid\}/prepared-scans" src/dotnet/Vulgata.Web
```

Expected: the endpoint registration and route remain.

- [ ] **Step 3: Run the sole technical acceptance gate**

Run:

```pwsh
dotnet build Vulgata.slnx
```

Expected: exit code zero. Do not run `dotnet test`, Docker, runtime, database, CodeGraph, JDT-LS, API, browser, or E2E qualification as acceptance work.

- [ ] **Step 4: Inspect repository hygiene**

Run:

```pwsh
git diff --check
git status --short
git diff --name-status HEAD~3..HEAD
```

Expected: no whitespace error; no uncommitted implementation file; inventory contains only the planned Docker, legacy profiling, prompt/comment, and obsolete test-source changes.

- [ ] **Step 5: Report the intentionally incomplete state**

The final response must state all of the following without claiming runtime success:

```text
- Legacy single-Repository profiling now throws NotImplementedException.
- Prepared Scan CodeGraph 1.5/JDT-LS source and persistence remain present.
- General language/framework discovery and symbol-level PreScan Code Units remain gaps.
- JDT-LS deployment/offline classpath authority remains unresolved.
- Story 12.5 remains terminated incomplete.
- No migration or historical specification/evidence was changed.
- dotnet build Vulgata.slnx exited successfully.
```

Do not create another implementation commit unless Step 4 finds an actual uncommitted planned correction.
