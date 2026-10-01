using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32.SafeHandles;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Source;
using Vulgata.Tests.TestInfrastructure;
using Xunit;

namespace Vulgata.Tests.PreparedSource;

[Collection(PreparedSourceTestCollection.Name)]
public sealed class PreparedCodeGraphPersistenceQualificationTests
{
    private const string ChildOperationVariable = "VULGATA_T029_STORE_CHILD_OPERATION";
    private const string PreparedRootVariable = "VULGATA_T029_STORE_PREPARED_ROOT";
    private const string SourceStorageRootVariable = "VULGATA_T029_STORE_STORAGE_ROOT";
    private const string SandboxRootVariable = "VULGATA_T029_STORE_SANDBOX_ROOT";
    private const string OwnerIdentityVariable = "VULGATA_T029_STORE_OWNER_IDENTITY";
    private const string OwnershipIdVariable = "VULGATA_T029_STORE_OWNERSHIP_ID";
    private const string ApprovalVariable = "VULGATA_T029_STORE_APPROVAL";
    private const string ReadyRecordVariable = "VULGATA_T029_STORE_READY_RECORD";
    private const string ResultVariable = "VULGATA_T029_STORE_RESULT";
    private const string LifecycleFixturesVariable = "VULGATA_T029_STORE_LIFECYCLE_FIXTURES";
    private const string SeedOperation = "seed-ready";
    private const string ReopenOperation = "reopen-ready";
    private const string SeedLifecycleOperation = "seed-lifecycle";
    private const string ReopenLifecycleOperation = "reopen-lifecycle";
    private const string ChildTestName =
        "Vulgata.Tests.PreparedSource.PreparedCodeGraphPersistenceQualificationTests.T029_ReadyOwnershipRecordSurvivesFreshTestHostProcess";
    private const string LifecycleChildTestName =
        "Vulgata.Tests.PreparedSource.PreparedCodeGraphPersistenceQualificationTests.T029_LifecycleRecordsStayUnavailableAfterFreshTestHostRestart";
    private const int MaximumChildOutputCharacters = 8_192;

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task T029_ReadyOwnershipRecordSurvivesFreshTestHostProcess()
    {
        if (Environment.GetEnvironmentVariable(ChildOperationVariable) is { Length: > 0 })
        {
            await ExecuteChildOperationAsync();
            return;
        }

        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);

        byte[] originalSource = await File.ReadAllBytesAsync(fixture.UnfamiliarTextPath);
        byte[] originalSibling = await File.ReadAllBytesAsync(fixture.SentinelPath);
        Guid ownershipId = Guid.NewGuid();
        string ownerIdentity = $"owner-{fixture.SandboxId:N}";
        string readyRecordPath = Path.Combine(fixture.SandboxPath, "ready-record.json");
        string seedResultPath = Path.Combine(fixture.SandboxPath, "seed-result.json");
        string reopenResultPath = Path.Combine(fixture.SandboxPath, "reopen-result.json");
        var childInputs = new ChildInputs(
            fixture.SandboxPath,
            fixture.PreparedRootPath,
            sourceStorageRoot,
            ownerIdentity,
            ownershipId,
            JsonSerializer.Serialize(fixture.Approval),
            readyRecordPath);

        ChildProcessResult seed = await RunChildAsync(
            fixture.SandboxPath,
            childInputs with { Operation = SeedOperation, ResultPath = seedResultPath });
        AssertChildSucceeded(seed, fixture.SandboxPath, "seed Ready ownership record");
        using JsonDocument seedEvidence = ReadBoundedJson(seedResultPath);
        JsonElement seeded = seedEvidence.RootElement;
        Assert.Equal(SeedOperation, seeded.GetProperty("operation").GetString());
        Assert.Equal("Found", seeded.GetProperty("loadStatus").GetString());
        Assert.Equal("VerifiedOwned", seeded.GetProperty("viewStatus").GetString());
        int seedProcessId = seeded.GetProperty("processId").GetInt32();
        Assert.NotEqual(Environment.ProcessId, seedProcessId);

        ChildProcessResult reopen = await RunChildAsync(
            fixture.SandboxPath,
            childInputs with { Operation = ReopenOperation, ResultPath = reopenResultPath });
        AssertChildSucceeded(reopen, fixture.SandboxPath, "reopen Ready ownership record");
        using JsonDocument reopenedEvidence = ReadBoundedJson(reopenResultPath);
        JsonElement reopened = reopenedEvidence.RootElement;

        Assert.Equal(ReopenOperation, reopened.GetProperty("operation").GetString());
        Assert.Equal("Found", reopened.GetProperty("loadStatus").GetString());
        Assert.Equal("VerifiedOwned", reopened.GetProperty("viewStatus").GetString());
        int reopenProcessId = reopened.GetProperty("processId").GetInt32();
        Assert.NotEqual(Environment.ProcessId, reopenProcessId);
        Assert.NotEqual(seedProcessId, reopenProcessId);

        JsonElement seededRecord = seeded.GetProperty("record");
        JsonElement reopenedRecord = reopened.GetProperty("record");
        AssertRecordSnapshotsEqual(seededRecord, reopenedRecord);
        Assert.Equal(ownershipId, reopenedRecord.GetProperty("ownershipId").GetGuid());
        Assert.Equal("Ready", reopenedRecord.GetProperty("phase").GetString());
        Assert.True(reopenedRecord.GetProperty("version").GetInt64() > 0);
        Assert.Equal(fixture.Approval.ApprovalIdentity,
            reopenedRecord.GetProperty("approval").GetProperty("approvalIdentity").GetString());
        Assert.Equal(Path.GetFullPath(fixture.PreparedRootPath),
            reopenedRecord.GetProperty("physicalRootPath").GetString());
        Assert.Equal(ownerIdentity, reopenedRecord.GetProperty("workspaceOwnerIdentity").GetString());

        PreparedToolDirectoryObjectIdentity expectedEntry = ObserveDirectoryIdentity(
            Path.Combine(fixture.PreparedRootPath, ".codegraph"));
        AssertIdentityEqual(
            expectedEntry,
            reopenedRecord.GetProperty("stateEntryObjectIdentity"));
        Assert.Equal(originalSource, await File.ReadAllBytesAsync(fixture.UnfamiliarTextPath));
        Assert.Equal(originalSibling, await File.ReadAllBytesAsync(fixture.SentinelPath));
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task T029_LifecycleRecordsStayUnavailableAfterFreshTestHostRestart()
    {
        if (Environment.GetEnvironmentVariable(ChildOperationVariable) is { Length: > 0 })
        {
            await ExecuteChildOperationAsync();
            return;
        }

        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string sourceStorageRoot = Path.Combine(fixture.SandboxPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);
        string[] phases = ["Creating", "Disposing", "Removed", "Quarantined"];
        var phaseFixtures = new List<PhaseFixture>(phases.Length);
        var originalSources = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (string phase in phases)
        {
            string root = Path.Combine(fixture.SandboxPath, $"workspace-{phase.ToLowerInvariant()}");
            Directory.CreateDirectory(root);
            byte[] content = Encoding.UTF8.GetBytes($"source fixture for {phase}\n");
            string sourcePath = Path.Combine(root, "source.txt");
            await File.WriteAllBytesAsync(sourcePath, content);
            originalSources.Add(sourcePath, content);
            phaseFixtures.Add(new PhaseFixture(
                phase,
                root,
                $"owner-{phase}-{fixture.SandboxId:N}",
                Guid.NewGuid()));
        }

        byte[] originalSibling = await File.ReadAllBytesAsync(fixture.SentinelPath);
        string recordsPath = Path.Combine(fixture.SandboxPath, "lifecycle-records.json");
        string seedResultPath = Path.Combine(fixture.SandboxPath, "lifecycle-seed-result.json");
        string reopenResultPath = Path.Combine(fixture.SandboxPath, "lifecycle-reopen-result.json");
        string fixturesJson = JsonSerializer.Serialize(phaseFixtures.Select(item => new
        {
            phase = item.Phase,
            rootPath = item.RootPath,
            ownerIdentity = item.OwnerIdentity,
            ownershipId = item.OwnershipId,
        }));
        var childInputs = new ChildInputs(
            fixture.SandboxPath,
            fixture.PreparedRootPath,
            sourceStorageRoot,
            $"owner-lifecycle-{fixture.SandboxId:N}",
            Guid.NewGuid(),
            JsonSerializer.Serialize(fixture.Approval),
            recordsPath,
            PhaseFixturesJson: fixturesJson);

        ChildProcessResult seed = await RunChildAsync(
            fixture.SandboxPath,
            childInputs with { Operation = SeedLifecycleOperation, ResultPath = seedResultPath },
            LifecycleChildTestName);
        AssertChildSucceeded(seed, fixture.SandboxPath, "seed lifecycle records");
        using JsonDocument seedEvidence = ReadBoundedJson(seedResultPath);
        JsonElement seeded = seedEvidence.RootElement;
        Assert.Equal(SeedLifecycleOperation, seeded.GetProperty("operation").GetString());
        int seedProcessId = seeded.GetProperty("processId").GetInt32();
        Assert.NotEqual(Environment.ProcessId, seedProcessId);

        ChildProcessResult reopen = await RunChildAsync(
            fixture.SandboxPath,
            childInputs with { Operation = ReopenLifecycleOperation, ResultPath = reopenResultPath },
            LifecycleChildTestName);
        AssertChildSucceeded(reopen, fixture.SandboxPath, "reopen lifecycle records");
        using JsonDocument reopenEvidence = ReadBoundedJson(reopenResultPath);
        JsonElement reopened = reopenEvidence.RootElement;
        Assert.Equal(ReopenLifecycleOperation, reopened.GetProperty("operation").GetString());
        int reopenProcessId = reopened.GetProperty("processId").GetInt32();
        Assert.NotEqual(Environment.ProcessId, reopenProcessId);
        Assert.NotEqual(seedProcessId, reopenProcessId);

        foreach (PhaseFixture phaseFixture in phaseFixtures)
        {
            JsonElement seededCase = FindLifecycleCase(seeded, phaseFixture.Phase);
            JsonElement reopenedCase = FindLifecycleCase(reopened, phaseFixture.Phase);
            Assert.Equal("Found", seededCase.GetProperty("loadStatus").GetString());
            Assert.Equal("Unavailable", seededCase.GetProperty("viewStatus").GetString());
            Assert.Equal("Found", reopenedCase.GetProperty("loadStatus").GetString());
            Assert.Equal("Unavailable", reopenedCase.GetProperty("viewStatus").GetString());

            JsonElement seededRecord = seededCase.GetProperty("record");
            JsonElement reopenedRecord = reopenedCase.GetProperty("record");
            AssertRecordSnapshotsEqual(seededRecord, reopenedRecord);
            Assert.Equal(phaseFixture.OwnershipId, reopenedRecord.GetProperty("ownershipId").GetGuid());
            Assert.Equal(phaseFixture.Phase, reopenedRecord.GetProperty("phase").GetString());
            Assert.True(reopenedRecord.GetProperty("version").GetInt64() > 0);
            Assert.Equal(phaseFixture.RootPath,
                reopenedRecord.GetProperty("physicalRootPath").GetString());
            Assert.Equal(phaseFixture.OwnerIdentity,
                reopenedRecord.GetProperty("workspaceOwnerIdentity").GetString());

            string entryPath = Path.Combine(phaseFixture.RootPath, ".codegraph");
            if (phaseFixture.Phase is "Disposing" or "Quarantined")
            {
                Assert.True(Directory.Exists(entryPath));
                AssertIdentityEqual(
                    ObserveDirectoryIdentity(entryPath),
                    reopenedRecord.GetProperty("stateEntryObjectIdentity"));
            }
            else
            {
                Assert.False(Directory.Exists(entryPath));
                Assert.Equal(JsonValueKind.Null,
                    reopenedRecord.GetProperty("stateEntryObjectIdentity").ValueKind);
            }
        }

        foreach ((string path, byte[] expected) in originalSources)
        {
            Assert.Equal(expected, await File.ReadAllBytesAsync(path));
        }
        Assert.Equal(originalSibling, await File.ReadAllBytesAsync(fixture.SentinelPath));
    }

    private static async Task ExecuteChildOperationAsync()
    {
        string operation = RequiredEnvironment(ChildOperationVariable);
        string sandboxRoot = RequiredEnvironment(SandboxRootVariable);
        string preparedRoot = RequiredEnvironment(PreparedRootVariable);
        string sourceStorageRoot = RequiredEnvironment(SourceStorageRootVariable);
        string ownerIdentity = RequiredEnvironment(OwnerIdentityVariable);
        Guid ownershipId = Guid.Parse(RequiredEnvironment(OwnershipIdVariable));
        PreparedSourceApproval approval = JsonSerializer.Deserialize<PreparedSourceApproval>(
            RequiredEnvironment(ApprovalVariable)) ??
            throw new InvalidOperationException("The owned child approval fixture was invalid.");
        string readyRecordPath = RequiredEnvironment(ReadyRecordVariable);
        string resultPath = RequiredEnvironment(ResultVariable);

        AssertOwnedChildPath(sandboxRoot, preparedRoot);
        AssertOwnedChildPath(sandboxRoot, sourceStorageRoot);
        AssertOwnedChildPath(sandboxRoot, readyRecordPath);
        AssertOwnedChildPath(sandboxRoot, resultPath);
        Assert.True(Directory.Exists(preparedRoot));
        Assert.True(Directory.Exists(sourceStorageRoot));

        RetainedSourceCapturePolicy policy = CreatePolicy(
            sourceStorageRoot,
            sandboxRoot,
            preparedRoot);
        if (operation is SeedLifecycleOperation or ReopenLifecycleOperation)
        {
            string phaseFixturesJson = RequiredEnvironment(LifecycleFixturesVariable);
            await ExecuteLifecycleChildOperationAsync(
                operation,
                policy,
                sandboxRoot,
                approval,
                phaseFixturesJson,
                readyRecordPath,
                resultPath);
            return;
        }

        var absent = new PreparedCodeGraphWorkspaceOwnership(
            ownershipId,
            Version: 0,
            PreparedCodeGraphOwnershipPhase.Absent,
            approval,
            Path.GetFullPath(preparedRoot),
            ObserveDirectoryIdentity(preparedRoot),
            ownerIdentity,
            StateEntryObjectIdentity: null);

        if (operation == SeedOperation)
        {
            await SeedReadyRecordAsync(policy, absent, resultPath, readyRecordPath);
            return;
        }

        Assert.Equal(ReopenOperation, operation);
        PreparedCodeGraphWorkspaceOwnership expectedReady = ReadExpectedRecord(readyRecordPath);
        Assert.Equal(absent.OwnershipId, expectedReady.OwnershipId);
        Assert.Equal(absent.Approval, expectedReady.Approval);
        Assert.Equal(absent.PhysicalRootPath, expectedReady.PhysicalRootPath);
        Assert.Equal(absent.RootObjectIdentity, expectedReady.RootObjectIdentity);
        Assert.Equal(absent.WorkspaceOwnerIdentity, expectedReady.WorkspaceOwnerIdentity);
        Assert.Equal(PreparedCodeGraphOwnershipPhase.Ready, expectedReady.Phase);
        Assert.True(expectedReady.Version > absent.Version);

        var store = new FilePreparedCodeGraphOwnershipStore(policy);
        PreparedCodeGraphOwnershipLoadResult freshLoad = await store.LoadAsync(absent);
        Assert.Equal(PreparedCodeGraphOwnershipLoadStatus.Found, freshLoad.Status);
        PreparedCodeGraphWorkspaceOwnership currentReady = Assert.IsType<PreparedCodeGraphWorkspaceOwnership>(
            freshLoad.Record);
        Assert.Equal(expectedReady, currentReady);

        var authority = new ControlledPreparedSourceAuthority();
        authority.Approve(approval);
        var coordinator = new PreparedCodeGraphStateCoordinator(
            authority,
            new FixtureToolUseAuthorizer(approval),
            new FixtureWorkspaceOwnershipVerifier(
                absent.PhysicalRootPath,
                absent.RootObjectIdentity,
                ownerIdentity),
            store);
        PreparedCodeGraphOwnershipViewResult view = await coordinator.GetOwnershipViewAsync(
            currentReady,
            approval);

        Assert.Equal(PreparedCodeGraphOwnershipViewStatus.VerifiedOwned, view.Status);
        Assert.Equal(currentReady, view.Record);
        Assert.Equal(
            ObserveDirectoryIdentity(Path.Combine(preparedRoot, ".codegraph")),
            currentReady.StateEntryObjectIdentity);
        WriteEvidence(resultPath, operation, freshLoad.Status, view.Status, currentReady);
    }

    private static async Task ExecuteLifecycleChildOperationAsync(
        string operation,
        RetainedSourceCapturePolicy policy,
        string sandboxRoot,
        PreparedSourceApproval approval,
        string phaseFixturesJson,
        string recordsPath,
        string resultPath)
    {
        IReadOnlyList<PhaseFixture> fixtures = ReadPhaseFixtures(phaseFixturesJson);
        Assert.Equal(new[] { "Creating", "Disposing", "Removed", "Quarantined" },
            fixtures.Select(fixture => fixture.Phase).ToArray());
        foreach (PhaseFixture fixture in fixtures)
        {
            AssertOwnedChildPath(sandboxRoot, fixture.RootPath);
            Assert.True(Directory.Exists(fixture.RootPath));
        }

        var store = new FilePreparedCodeGraphOwnershipStore(policy);
        var authority = new ControlledPreparedSourceAuthority();
        authority.Approve(approval);
        var toolAuthorizer = new FixtureToolUseAuthorizer(approval);

        if (operation == SeedLifecycleOperation)
        {
            PreparedCodeGraphStoreProvisionStatus provision =
                await FilePreparedCodeGraphOwnershipStore.ProvisionNewAsync(policy);
            Assert.Equal(PreparedCodeGraphStoreProvisionStatus.Provisioned, provision);

            var seeded = new List<LifecycleRecord>(fixtures.Count);
            foreach (PhaseFixture fixture in fixtures)
            {
                PreparedCodeGraphWorkspaceOwnership absent = CreateAbsentBinding(fixture, approval);
                PreparedCodeGraphWorkspaceOwnership phaseRecord;
                PreparedCodeGraphStateCoordinator coordinator;
                if (fixture.Phase == "Creating")
                {
                    coordinator = CreateCoordinator(absent, authority, toolAuthorizer, store);
                    PreparedCodeGraphOwnershipTransitionResult transition =
                        await store.BeginCreateAsync(absent);
                    Assert.Equal(PreparedCodeGraphOwnershipTransitionStatus.Committed, transition.Status);
                    phaseRecord = Assert.IsType<PreparedCodeGraphWorkspaceOwnership>(transition.Record);
                    AssertTransition(absent, phaseRecord, PreparedCodeGraphOwnershipPhase.Creating);
                }
                else
                {
                    var readyResult = await CreateReadyRecordAsync(
                        absent,
                        authority,
                        toolAuthorizer,
                        store);
                    coordinator = readyResult.Coordinator;
                    PreparedCodeGraphWorkspaceOwnership ready = readyResult.Ready;
                    if (fixture.Phase == "Disposing")
                    {
                        PreparedCodeGraphOwnershipTransitionResult transition =
                            await store.BeginDispositionAsync(ready);
                        Assert.Equal(PreparedCodeGraphOwnershipTransitionStatus.Committed, transition.Status);
                        phaseRecord = Assert.IsType<PreparedCodeGraphWorkspaceOwnership>(transition.Record);
                        AssertTransition(ready, phaseRecord, PreparedCodeGraphOwnershipPhase.Disposing);
                    }
                    else if (fixture.Phase == "Removed")
                    {
                        PreparedToolWorkspaceCheckStatus disposed =
                            await coordinator.DisposeOwnedStateAsync(approval, ready);
                        Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, disposed);
                        PreparedCodeGraphOwnershipLoadResult removedLoad = await store.LoadAsync(ready);
                        Assert.Equal(PreparedCodeGraphOwnershipLoadStatus.Found, removedLoad.Status);
                        phaseRecord = Assert.IsType<PreparedCodeGraphWorkspaceOwnership>(removedLoad.Record);
                        AssertTransition(ready, phaseRecord, PreparedCodeGraphOwnershipPhase.Removed);
                    }
                    else
                    {
                        Assert.Equal("Quarantined", fixture.Phase);
                        PreparedCodeGraphOwnershipTransitionResult transition =
                            await store.QuarantineAsync(ready);
                        Assert.Equal(PreparedCodeGraphOwnershipTransitionStatus.Committed, transition.Status);
                        phaseRecord = Assert.IsType<PreparedCodeGraphWorkspaceOwnership>(transition.Record);
                        AssertTransition(ready, phaseRecord, PreparedCodeGraphOwnershipPhase.Quarantined);
                    }
                }

                PreparedCodeGraphOwnershipLoadResult loaded = await store.LoadAsync(phaseRecord);
                Assert.Equal(PreparedCodeGraphOwnershipLoadStatus.Found, loaded.Status);
                Assert.Equal(phaseRecord, loaded.Record);
                AssertLifecyclePhysicalState(fixture, phaseRecord);
                PreparedCodeGraphOwnershipViewResult view = await coordinator.GetOwnershipViewAsync(
                    phaseRecord,
                    approval);
                Assert.Equal(PreparedCodeGraphOwnershipViewStatus.Unavailable, view.Status);
                Assert.Null(view.Record);
                seeded.Add(new LifecycleRecord(fixture.Phase, loaded.Status, view.Status, phaseRecord));
            }

            WriteLifecycleEvidence(recordsPath, operation, seeded);
            WriteLifecycleEvidence(resultPath, operation, seeded);
            return;
        }

        Assert.Equal(ReopenLifecycleOperation, operation);
        IReadOnlyList<LifecycleRecord> expectedRecords = ReadLifecycleEvidence(recordsPath);
        Assert.Equal(fixtures.Count, expectedRecords.Count);
        var reopened = new List<LifecycleRecord>(fixtures.Count);
        foreach (PhaseFixture fixture in fixtures)
        {
            LifecycleRecord expected = Assert.Single(
                expectedRecords.Where(record => record.Phase == fixture.Phase));
            Assert.Equal(PreparedCodeGraphOwnershipLoadStatus.Found, expected.LoadStatus);
            Assert.Equal(PreparedCodeGraphOwnershipViewStatus.Unavailable, expected.ViewStatus);
            PreparedCodeGraphWorkspaceOwnership expectedRecord = expected.Record;
            PreparedCodeGraphWorkspaceOwnership binding = CreateAbsentBinding(fixture, approval);
            Assert.Equal(binding.OwnershipId, expectedRecord.OwnershipId);
            Assert.Equal(binding.Approval, expectedRecord.Approval);
            Assert.Equal(binding.PhysicalRootPath, expectedRecord.PhysicalRootPath);
            Assert.Equal(binding.RootObjectIdentity, expectedRecord.RootObjectIdentity);
            Assert.Equal(binding.WorkspaceOwnerIdentity, expectedRecord.WorkspaceOwnerIdentity);

            PreparedCodeGraphOwnershipLoadResult load = await store.LoadAsync(expectedRecord);
            Assert.Equal(PreparedCodeGraphOwnershipLoadStatus.Found, load.Status);
            PreparedCodeGraphWorkspaceOwnership current = Assert.IsType<PreparedCodeGraphWorkspaceOwnership>(
                load.Record);
            Assert.Equal(expectedRecord, current);
            AssertLifecyclePhysicalState(fixture, current);

            PreparedCodeGraphStateCoordinator coordinator = CreateCoordinator(
                binding,
                authority,
                toolAuthorizer,
                store);
            PreparedCodeGraphOwnershipViewResult view = await coordinator.GetOwnershipViewAsync(
                current,
                approval);
            Assert.Equal(PreparedCodeGraphOwnershipViewStatus.Unavailable, view.Status);
            Assert.Null(view.Record);
            reopened.Add(new LifecycleRecord(fixture.Phase, load.Status, view.Status, current));
        }

        WriteLifecycleEvidence(resultPath, operation, reopened);
    }

    private static async Task<(
        PreparedCodeGraphStateCoordinator Coordinator,
        PreparedCodeGraphWorkspaceOwnership Ready)> CreateReadyRecordAsync(
        PreparedCodeGraphWorkspaceOwnership absent,
        ControlledPreparedSourceAuthority authority,
        FixtureToolUseAuthorizer toolAuthorizer,
        FilePreparedCodeGraphOwnershipStore store)
    {
        var issuer = new PreparedToolWorkspaceIssuer(authority, toolAuthorizer);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                absent.Approval,
                absent.PhysicalRootPath,
                PreparedToolPurpose.CodeGraphPreparation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);
        PreparedCodeGraphStateCoordinator coordinator = CreateCoordinator(
            absent,
            authority,
            toolAuthorizer,
            store);
        try
        {
            PreparedToolWorkspaceCheckStatus use = await coordinator.UseOwnedStateAsync(
                lease,
                absent,
                (_, _) => ValueTask.CompletedTask);
            Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, use);
            PreparedCodeGraphOwnershipLoadResult load = await store.LoadAsync(absent);
            Assert.Equal(PreparedCodeGraphOwnershipLoadStatus.Found, load.Status);
            PreparedCodeGraphWorkspaceOwnership ready = Assert.IsType<PreparedCodeGraphWorkspaceOwnership>(
                load.Record);
            AssertTransition(absent, ready, PreparedCodeGraphOwnershipPhase.Ready);
            Assert.NotNull(ready.StateEntryObjectIdentity);
            Assert.Equal(
                ObserveDirectoryIdentity(Path.Combine(absent.PhysicalRootPath, ".codegraph")),
                ready.StateEntryObjectIdentity);
            PreparedCodeGraphOwnershipViewResult view = await coordinator.GetOwnershipViewAsync(
                ready,
                absent.Approval);
            Assert.Equal(PreparedCodeGraphOwnershipViewStatus.VerifiedOwned, view.Status);
            Assert.Equal(ready, view.Record);
            return (coordinator, ready);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    private static PreparedCodeGraphStateCoordinator CreateCoordinator(
        PreparedCodeGraphWorkspaceOwnership binding,
        ControlledPreparedSourceAuthority authority,
        FixtureToolUseAuthorizer toolAuthorizer,
        FilePreparedCodeGraphOwnershipStore store) =>
        new(
            authority,
            toolAuthorizer,
            new FixtureWorkspaceOwnershipVerifier(
                binding.PhysicalRootPath,
                binding.RootObjectIdentity,
                binding.WorkspaceOwnerIdentity),
            store);

    private static PreparedCodeGraphWorkspaceOwnership CreateAbsentBinding(
        PhaseFixture fixture,
        PreparedSourceApproval approval) =>
        new(
            fixture.OwnershipId,
            Version: 0,
            PreparedCodeGraphOwnershipPhase.Absent,
            approval,
            Path.GetFullPath(fixture.RootPath),
            ObserveDirectoryIdentity(fixture.RootPath),
            fixture.OwnerIdentity,
            StateEntryObjectIdentity: null);

    private static void AssertTransition(
        PreparedCodeGraphWorkspaceOwnership expected,
        PreparedCodeGraphWorkspaceOwnership actual,
        PreparedCodeGraphOwnershipPhase phase)
    {
        Assert.Equal(phase, actual.Phase);
        Assert.True(actual.Version > expected.Version);
        Assert.Equal(expected.OwnershipId, actual.OwnershipId);
        Assert.Equal(expected.Approval, actual.Approval);
        Assert.Equal(expected.PhysicalRootPath, actual.PhysicalRootPath);
        Assert.Equal(expected.RootObjectIdentity, actual.RootObjectIdentity);
        Assert.Equal(expected.WorkspaceOwnerIdentity, actual.WorkspaceOwnerIdentity);
        if (phase is PreparedCodeGraphOwnershipPhase.Creating or PreparedCodeGraphOwnershipPhase.Removed)
        {
            Assert.Null(actual.StateEntryObjectIdentity);
        }
        else if (phase is PreparedCodeGraphOwnershipPhase.Disposing or PreparedCodeGraphOwnershipPhase.Quarantined)
        {
            Assert.Equal(expected.StateEntryObjectIdentity, actual.StateEntryObjectIdentity);
        }
    }

    private static void AssertLifecyclePhysicalState(
        PhaseFixture fixture,
        PreparedCodeGraphWorkspaceOwnership record)
    {
        Assert.Equal(Enum.Parse<PreparedCodeGraphOwnershipPhase>(fixture.Phase), record.Phase);
        string entryPath = Path.Combine(fixture.RootPath, ".codegraph");
        if (fixture.Phase is "Disposing" or "Quarantined")
        {
            Assert.True(Directory.Exists(entryPath));
            Assert.NotNull(record.StateEntryObjectIdentity);
            Assert.Equal(record.StateEntryObjectIdentity, ObserveDirectoryIdentity(entryPath));
        }
        else
        {
            Assert.False(Directory.Exists(entryPath));
            Assert.Null(record.StateEntryObjectIdentity);
        }
    }

    private static IReadOnlyList<PhaseFixture> ReadPhaseFixtures(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray()
            .Select(element => new PhaseFixture(
                element.GetProperty("phase").GetString()!,
                element.GetProperty("rootPath").GetString()!,
                element.GetProperty("ownerIdentity").GetString()!,
                element.GetProperty("ownershipId").GetGuid()))
            .ToArray();
    }

    private static void WriteLifecycleEvidence(
        string path,
        string operation,
        IReadOnlyList<LifecycleRecord> records)
    {
        var evidence = new
        {
            operation,
            processId = Environment.ProcessId,
            cases = records.Select(item => new
            {
                phase = item.Phase,
                loadStatus = item.LoadStatus.ToString(),
                viewStatus = item.ViewStatus.ToString(),
                record = OwnershipSnapshot(item.Record),
            }),
        };
        File.WriteAllText(path, JsonSerializer.Serialize(evidence));
    }

    private static IReadOnlyList<LifecycleRecord> ReadLifecycleEvidence(string path)
    {
        using JsonDocument document = ReadBoundedJson(path);
        return document.RootElement.GetProperty("cases").EnumerateArray()
            .Select(item => new LifecycleRecord(
                item.GetProperty("phase").GetString()!,
                Enum.Parse<PreparedCodeGraphOwnershipLoadStatus>(item.GetProperty("loadStatus").GetString()!),
                Enum.Parse<PreparedCodeGraphOwnershipViewStatus>(item.GetProperty("viewStatus").GetString()!),
                ReadOwnershipRecord(item.GetProperty("record"))))
            .ToArray();
    }

    private static JsonElement FindLifecycleCase(JsonElement evidence, string phase) =>
        Assert.Single(evidence.GetProperty("cases").EnumerateArray()
            .Where(item => item.GetProperty("phase").GetString() == phase));

    private static async Task SeedReadyRecordAsync(
        RetainedSourceCapturePolicy policy,
        PreparedCodeGraphWorkspaceOwnership absent,
        string resultPath,
        string readyRecordPath)
    {
        PreparedCodeGraphStoreProvisionStatus provision =
            await FilePreparedCodeGraphOwnershipStore.ProvisionNewAsync(policy);
        Assert.Equal(PreparedCodeGraphStoreProvisionStatus.Provisioned, provision);

        var authority = new ControlledPreparedSourceAuthority();
        authority.Approve(absent.Approval);
        var toolAuthorizer = new FixtureToolUseAuthorizer(absent.Approval);
        var issuer = new PreparedToolWorkspaceIssuer(authority, toolAuthorizer);
        PreparedToolWorkspaceIssueResult issue = await issuer.IssueAsync(
            new PreparedToolWorkspaceGrantRequest(
                absent.Approval,
                absent.PhysicalRootPath,
                PreparedToolPurpose.CodeGraphPreparation,
                TimeSpan.FromSeconds(10)));
        Assert.Equal(PreparedToolWorkspaceIssueStatus.Issued, issue.Status);
        PreparedToolWorkspaceLease lease = Assert.IsType<PreparedToolWorkspaceLease>(issue.Lease);

        var store = new FilePreparedCodeGraphOwnershipStore(policy);
        var coordinator = new PreparedCodeGraphStateCoordinator(
            authority,
            toolAuthorizer,
            new FixtureWorkspaceOwnershipVerifier(
                absent.PhysicalRootPath,
                absent.RootObjectIdentity,
                absent.WorkspaceOwnerIdentity),
            store);
        try
        {
            PreparedToolWorkspaceCheckStatus use = await coordinator.UseOwnedStateAsync(
                lease,
                absent,
                (_, _) => ValueTask.CompletedTask);
            Assert.Equal(PreparedToolWorkspaceCheckStatus.Current, use);

            PreparedCodeGraphOwnershipLoadResult initialReadyLoad = await store.LoadAsync(absent);
            Assert.Equal(PreparedCodeGraphOwnershipLoadStatus.Found, initialReadyLoad.Status);
            PreparedCodeGraphWorkspaceOwnership ready = Assert.IsType<PreparedCodeGraphWorkspaceOwnership>(
                initialReadyLoad.Record);
            Assert.Equal(PreparedCodeGraphOwnershipPhase.Ready, ready.Phase);
            Assert.True(ready.Version > absent.Version);
            Assert.Equal(absent.OwnershipId, ready.OwnershipId);
            Assert.Equal(absent.Approval, ready.Approval);
            Assert.Equal(absent.PhysicalRootPath, ready.PhysicalRootPath);
            Assert.Equal(absent.RootObjectIdentity, ready.RootObjectIdentity);
            Assert.Equal(absent.WorkspaceOwnerIdentity, ready.WorkspaceOwnerIdentity);
            Assert.Equal(
                ObserveDirectoryIdentity(Path.Combine(absent.PhysicalRootPath, ".codegraph")),
                ready.StateEntryObjectIdentity);

            PreparedCodeGraphOwnershipViewResult view = await coordinator.GetOwnershipViewAsync(
                ready,
                absent.Approval);
            Assert.Equal(PreparedCodeGraphOwnershipViewStatus.VerifiedOwned, view.Status);
            Assert.Equal(ready, view.Record);

            PreparedCodeGraphOwnershipLoadResult persisted = await store.LoadAsync(ready);
            Assert.Equal(PreparedCodeGraphOwnershipLoadStatus.Found, persisted.Status);
            Assert.Equal(ready, persisted.Record);
            WriteEvidence(readyRecordPath, SeedOperation, persisted.Status, view.Status, ready);
            WriteEvidence(resultPath, SeedOperation, persisted.Status, view.Status, ready);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    private static RetainedSourceCapturePolicy CreatePolicy(
        string sourceStorageRoot,
        string sandboxRoot,
        string preparedRoot)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SourceStorage:RootPath"] = sourceStorageRoot,
                ["SourceStorage:MaximumEntries"] = "10000",
                ["SourceStorage:MaximumBlobBytes"] = "8388608",
                ["SourceStorage:MaximumAggregateBlobBytes"] = "67108864",
                ["SourceStorage:CaptureDeadlineSeconds"] = "600",
                ["SourceStorage:OperationStaleAfterMinutes"] = "1440",
                ["SourceStorage:VerificationBufferBytes"] = "65536",
                ["SourceStorage:Read:DeadlineSeconds"] = "60",
            })
            .Build();
        return RetainedSourceCapturePolicy.Create(configuration, sandboxRoot, preparedRoot);
    }

    private static async Task<ChildProcessResult> RunChildAsync(
        string sandboxRoot,
        ChildInputs inputs,
        string childTestName = ChildTestName)
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectPath = Path.Combine(repositoryRoot, "tests", "Vulgata.Tests", "Vulgata.Tests.csproj");
        string settingsPath = Path.Combine(repositoryRoot, "tests", "Vulgata.Tests", "explicit-suites.runsettings");
        string childResults = Path.Combine(sandboxRoot, $"child-results-{Guid.NewGuid():N}");
        Directory.CreateDirectory(childResults);

        var startInfo = new ProcessStartInfo(ResolveDotnetHost())
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("test");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--settings");
        startInfo.ArgumentList.Add(settingsPath);
        startInfo.ArgumentList.Add("--filter");
        startInfo.ArgumentList.Add($"Suite=LocalTools&FullyQualifiedName~{childTestName}");
        startInfo.ArgumentList.Add("--results-directory");
        startInfo.ArgumentList.Add(childResults);
        startInfo.ArgumentList.Add("--logger");
        startInfo.ArgumentList.Add("trx;LogFileName=child.trx");
        startInfo.ArgumentList.Add("--verbosity");
        startInfo.ArgumentList.Add("quiet");

        foreach (string name in startInfo.Environment.Keys.ToArray())
        {
            if (name.StartsWith("VULGATA_", StringComparison.Ordinal) ||
                name.StartsWith("ConnectionStrings__", StringComparison.Ordinal) ||
                name.StartsWith("OPENAI_", StringComparison.Ordinal) ||
                name.StartsWith("DEEPSEEK_", StringComparison.Ordinal) ||
                name.StartsWith("AZURE_OPENAI_", StringComparison.Ordinal))
            {
                startInfo.Environment.Remove(name);
            }
        }

        startInfo.Environment["VULGATA_TEST_SUITE"] = "LocalTools";
        startInfo.Environment[ChildOperationVariable] = inputs.Operation!;
        startInfo.Environment[PreparedRootVariable] = inputs.PreparedRoot;
        startInfo.Environment[SourceStorageRootVariable] = inputs.SourceStorageRoot;
        startInfo.Environment[SandboxRootVariable] = inputs.SandboxRoot;
        startInfo.Environment[OwnerIdentityVariable] = inputs.OwnerIdentity;
        startInfo.Environment[OwnershipIdVariable] = inputs.OwnershipId.ToString("D");
        startInfo.Environment[ApprovalVariable] = inputs.ApprovalJson;
        startInfo.Environment[ReadyRecordVariable] = inputs.ReadyRecordPath;
        startInfo.Environment[ResultVariable] = inputs.ResultPath!;
        if (inputs.PhaseFixturesJson is not null)
        {
            startInfo.Environment[LifecycleFixturesVariable] = inputs.PhaseFixturesJson;
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("The owned LocalTools child testhost did not start.");
        }

        Task<string> outputTask = ReadBoundedAsync(process.StandardOutput);
        Task<string> errorTask = ReadBoundedAsync(process.StandardError);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // The owned child already exited while cancellation was observed.
            }

            await process.WaitForExitAsync();
            string timedOutOutput = SanitizeOutput(
                $"{await outputTask}\n{await errorTask}", sandboxRoot);
            throw new TimeoutException(
                $"The owned LocalTools child testhost exceeded 90 seconds. {timedOutOutput}");
        }

        return new ChildProcessResult(
            process.ExitCode,
            await outputTask,
            await errorTask,
            ReadChildFailureDetails(Path.Combine(childResults, "child.trx"), sandboxRoot));
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader)
    {
        var captured = new StringBuilder();
        char[] buffer = new char[1024];
        bool truncated = false;
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory())) != 0)
        {
            int available = MaximumChildOutputCharacters - captured.Length;
            if (available > 0)
            {
                int take = Math.Min(available, count);
                captured.Append(buffer, 0, take);
                truncated |= take < count;
            }
            else
            {
                truncated = true;
            }
        }

        if (truncated)
        {
            captured.Append("\n<child output truncated>");
        }

        return captured.ToString();
    }

    private static void AssertChildSucceeded(
        ChildProcessResult child,
        string sandboxRoot,
        string operation)
    {
        string output = SanitizeOutput(
            $"{child.StandardOutput}\n{child.StandardError}\n{child.FailureDetails}",
            sandboxRoot);
        Assert.True(child.ExitCode == 0, $"The child could not {operation} (exit {child.ExitCode}). {output}");
    }

    private static string ReadChildFailureDetails(string trxPath, string sandboxRoot)
    {
        if (!File.Exists(trxPath))
        {
            return "Child TRX was not produced.";
        }

        var info = new FileInfo(trxPath);
        if (info.Length is <= 0 or > 1_048_576)
        {
            return "Child TRX exceeded its diagnostic size bound.";
        }

        XDocument document = XDocument.Load(trxPath);
        string[] failures = document.Descendants()
            .Where(element => element.Name.LocalName == "UnitTestResult" &&
                string.Equals((string?)element.Attribute("outcome"), "Failed", StringComparison.Ordinal))
            .Select(result =>
            {
                XElement? error = result.Descendants()
                    .FirstOrDefault(element => element.Name.LocalName == "ErrorInfo");
                string testName = (string?)result.Attribute("testName") ?? "unknown child test";
                string message = error?.Elements()
                    .FirstOrDefault(element => element.Name.LocalName == "Message")?.Value ??
                    "No child test error message was recorded.";
                string stack = error?.Elements()
                    .FirstOrDefault(element => element.Name.LocalName == "StackTrace")?.Value ??
                    string.Empty;
                return $"{testName}: {message}\n{stack}";
            })
            .ToArray();

        return SanitizeOutput(string.Join("\n", failures), sandboxRoot);
    }

    private static string SanitizeOutput(string output, string sandboxRoot)
    {
        string sanitized = output.Replace(sandboxRoot, "<owned-fixture>", StringComparison.OrdinalIgnoreCase);
        return sanitized.Length <= MaximumChildOutputCharacters
            ? sanitized
            : sanitized[..MaximumChildOutputCharacters] + "\n<child output truncated>";
    }

    private static string ResolveDotnetHost()
    {
        string? configuredHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(configuredHost) && File.Exists(configuredHost))
        {
            return Path.GetFullPath(configuredHost);
        }

        string? currentExecutable = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(currentExecutable) && File.Exists(currentExecutable) &&
            Path.GetFileNameWithoutExtension(currentExecutable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFullPath(currentExecutable);
        }

        string? dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(dotnetRoot))
        {
            string rootedHost = Path.Combine(dotnetRoot, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
            if (File.Exists(rootedHost))
            {
                return Path.GetFullPath(rootedHost);
            }
        }

        throw new InvalidOperationException(
            "The current testhost did not expose an absolute dotnet host path; refusing PATH-based child launch.");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "tests", "Vulgata.Tests", "Vulgata.Tests.csproj")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("The Vulgata test project root could not be located.");
    }

    private static PreparedCodeGraphWorkspaceOwnership ReadExpectedRecord(string path)
    {
        using JsonDocument document = ReadBoundedJson(path);
        return ReadOwnershipRecord(document.RootElement.GetProperty("record"));
    }

    private static PreparedCodeGraphWorkspaceOwnership ReadOwnershipRecord(JsonElement record)
    {
        JsonElement approvalElement = record.GetProperty("approval");
        JsonElement rootIdentityElement = record.GetProperty("rootObjectIdentity");
        JsonElement entryIdentityElement = record.GetProperty("stateEntryObjectIdentity");
        return new PreparedCodeGraphWorkspaceOwnership(
            record.GetProperty("ownershipId").GetGuid(),
            record.GetProperty("version").GetInt64(),
            Enum.Parse<PreparedCodeGraphOwnershipPhase>(record.GetProperty("phase").GetString()!),
            new PreparedSourceApproval(
                approvalElement.GetProperty("batchId").GetGuid(),
                approvalElement.GetProperty("repositoryId").GetGuid(),
                approvalElement.GetProperty("subjectId").GetString()!,
                Enum.Parse<PreparedSourcePurpose>(approvalElement.GetProperty("purpose").GetString()!),
                approvalElement.GetProperty("approvalIdentity").GetString()!,
                approvalElement.GetProperty("rootBindingIdentity").GetString()!,
                approvalElement.GetProperty("operatorLabel").GetString()!),
            record.GetProperty("physicalRootPath").GetString()!,
            ReadIdentity(rootIdentityElement),
            record.GetProperty("workspaceOwnerIdentity").GetString()!,
            entryIdentityElement.ValueKind == JsonValueKind.Null
                ? null
                : ReadIdentity(entryIdentityElement));
    }

    private static JsonDocument ReadBoundedJson(string path)
    {
        var info = new FileInfo(path);
        Assert.True(info.Exists, "The expected child result marker was not created.");
        Assert.True(info.Length is > 0 and <= 16_384, "The child result marker exceeded its owned fixture bound.");
        return JsonDocument.Parse(File.ReadAllBytes(path));
    }

    private static void AssertRecordSnapshotsEqual(JsonElement expected, JsonElement actual)
    {
        Assert.Equal(expected.GetProperty("ownershipId").GetGuid(), actual.GetProperty("ownershipId").GetGuid());
        Assert.Equal(expected.GetProperty("version").GetInt64(), actual.GetProperty("version").GetInt64());
        Assert.Equal(expected.GetProperty("phase").GetString(), actual.GetProperty("phase").GetString());
        JsonElement expectedApproval = expected.GetProperty("approval");
        JsonElement actualApproval = actual.GetProperty("approval");
        Assert.Equal(expectedApproval.GetProperty("batchId").GetGuid(), actualApproval.GetProperty("batchId").GetGuid());
        Assert.Equal(expectedApproval.GetProperty("repositoryId").GetGuid(), actualApproval.GetProperty("repositoryId").GetGuid());
        Assert.Equal(expectedApproval.GetProperty("subjectId").GetString(), actualApproval.GetProperty("subjectId").GetString());
        Assert.Equal(expectedApproval.GetProperty("purpose").GetString(), actualApproval.GetProperty("purpose").GetString());
        Assert.Equal(expectedApproval.GetProperty("approvalIdentity").GetString(), actualApproval.GetProperty("approvalIdentity").GetString());
        Assert.Equal(expectedApproval.GetProperty("rootBindingIdentity").GetString(), actualApproval.GetProperty("rootBindingIdentity").GetString());
        Assert.Equal(expectedApproval.GetProperty("operatorLabel").GetString(), actualApproval.GetProperty("operatorLabel").GetString());
        Assert.Equal(expected.GetProperty("physicalRootPath").GetString(), actual.GetProperty("physicalRootPath").GetString());
        AssertIdentityEqual(
            ReadIdentity(expected.GetProperty("rootObjectIdentity")),
            actual.GetProperty("rootObjectIdentity"));
        Assert.Equal(expected.GetProperty("workspaceOwnerIdentity").GetString(), actual.GetProperty("workspaceOwnerIdentity").GetString());
        JsonElement expectedEntry = expected.GetProperty("stateEntryObjectIdentity");
        JsonElement actualEntry = actual.GetProperty("stateEntryObjectIdentity");
        Assert.Equal(expectedEntry.ValueKind, actualEntry.ValueKind);
        if (expectedEntry.ValueKind != JsonValueKind.Null)
        {
            AssertIdentityEqual(ReadIdentity(expectedEntry), actualEntry);
        }
    }

    private static void WriteEvidence(
        string path,
        string operation,
        PreparedCodeGraphOwnershipLoadStatus loadStatus,
        PreparedCodeGraphOwnershipViewStatus viewStatus,
        PreparedCodeGraphWorkspaceOwnership record)
    {
        var evidence = new
        {
            operation,
            processId = Environment.ProcessId,
            loadStatus = loadStatus.ToString(),
            viewStatus = viewStatus.ToString(),
            record = OwnershipSnapshot(record),
        };
        File.WriteAllText(path, JsonSerializer.Serialize(evidence));
    }

    private static object OwnershipSnapshot(PreparedCodeGraphWorkspaceOwnership record) => new
    {
        ownershipId = record.OwnershipId,
        version = record.Version,
        phase = record.Phase.ToString(),
        approval = new
        {
            batchId = record.Approval.BatchId,
            repositoryId = record.Approval.RepositoryId,
            subjectId = record.Approval.SubjectId,
            purpose = record.Approval.Purpose.ToString(),
            approvalIdentity = record.Approval.ApprovalIdentity,
            rootBindingIdentity = record.Approval.RootBindingIdentity,
            operatorLabel = record.Approval.OperatorLabel,
        },
        physicalRootPath = record.PhysicalRootPath,
        rootObjectIdentity = IdentityJson(record.RootObjectIdentity),
        workspaceOwnerIdentity = record.WorkspaceOwnerIdentity,
        stateEntryObjectIdentity = record.StateEntryObjectIdentity is null
            ? null
            : IdentityJson(record.StateEntryObjectIdentity),
    };

    private static object IdentityJson(PreparedToolDirectoryObjectIdentity identity) => new
    {
        volumeSerialNumber = identity.VolumeSerialNumber,
        fileIndexHigh = identity.FileIndexHigh,
        fileIndexLow = identity.FileIndexLow,
    };

    private static PreparedToolDirectoryObjectIdentity ReadIdentity(JsonElement element) => new(
        element.GetProperty("volumeSerialNumber").GetUInt32(),
        element.GetProperty("fileIndexHigh").GetUInt32(),
        element.GetProperty("fileIndexLow").GetUInt32());

    private static void AssertIdentityEqual(
        PreparedToolDirectoryObjectIdentity expected,
        JsonElement actual)
    {
        Assert.Equal(expected.VolumeSerialNumber, actual.GetProperty("volumeSerialNumber").GetUInt32());
        Assert.Equal(expected.FileIndexHigh, actual.GetProperty("fileIndexHigh").GetUInt32());
        Assert.Equal(expected.FileIndexLow, actual.GetProperty("fileIndexLow").GetUInt32());
    }

    private static PreparedToolDirectoryObjectIdentity ObserveDirectoryIdentity(string path)
    {
        (PreparedSourceTraversal.OpenStatus status, SafeFileHandle? handle) =
            PreparedSourceTraversal.OpenRoot(path);
        using (handle)
        {
            Assert.Equal(PreparedSourceTraversal.OpenStatus.Opened, status);
            PreparedSourceTraversal.DirectoryIdentity observed = Assert.IsType<PreparedSourceTraversal.DirectoryIdentity>(
                handle is null ? null : PreparedSourceTraversal.ObserveDirectory(handle));
            return new PreparedToolDirectoryObjectIdentity(
                observed.VolumeSerialNumber,
                observed.FileIndexHigh,
                observed.FileIndexLow);
        }
    }

    private static void AssertOwnedChildPath(string sandboxRoot, string path)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sandboxRoot));
        string candidate = Path.GetFullPath(path);
        Assert.True(candidate.StartsWith(
                root + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal),
            "The child path must remain inside the parent-owned fixture.");
    }

    private static string RequiredEnvironment(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"The child testhost requires {name}.");

    private sealed record ChildInputs(
        string SandboxRoot,
        string PreparedRoot,
        string SourceStorageRoot,
        string OwnerIdentity,
        Guid OwnershipId,
        string ApprovalJson,
        string ReadyRecordPath,
        string? Operation = null,
        string? ResultPath = null,
        string? PhaseFixturesJson = null);

    private sealed record PhaseFixture(
        string Phase,
        string RootPath,
        string OwnerIdentity,
        Guid OwnershipId);

    private sealed record LifecycleRecord(
        string Phase,
        PreparedCodeGraphOwnershipLoadStatus LoadStatus,
        PreparedCodeGraphOwnershipViewStatus ViewStatus,
        PreparedCodeGraphWorkspaceOwnership Record);

    private sealed record ChildProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError,
        string FailureDetails);

    private sealed class FixtureToolUseAuthorizer(PreparedSourceApproval currentApproval)
        : IPreparedToolUseAuthorizer
    {
        public Task<PreparedToolUseAuthorityResult> VerifyCurrentAsync(
            PreparedSourceApproval approval,
            PreparedToolPurpose toolPurpose,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PreparedToolUseAuthorityStatus status = approval == currentApproval &&
                toolPurpose == PreparedToolPurpose.CodeGraphPreparation
                    ? PreparedToolUseAuthorityStatus.Current
                    : PreparedToolUseAuthorityStatus.Denied;
            return Task.FromResult(new PreparedToolUseAuthorityResult(status));
        }
    }

    private sealed class FixtureWorkspaceOwnershipVerifier(
        string physicalRootPath,
        PreparedToolDirectoryObjectIdentity rootIdentity,
        string ownerIdentity) : IPreparedCodeGraphWorkspaceOwnershipVerifier
    {
        public ValueTask<bool> VerifyCurrentAsync(
            PreparedCodeGraphWorkspaceOwnership ownership,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool current = ownership.PhysicalRootPath.Equals(physicalRootPath, StringComparison.OrdinalIgnoreCase) &&
                ownership.RootObjectIdentity == rootIdentity &&
                ownership.WorkspaceOwnerIdentity == ownerIdentity;
            if (current && ownership.Phase == PreparedCodeGraphOwnershipPhase.Absent)
            {
                current = ownership.StateEntryObjectIdentity is null &&
                    !Directory.Exists(Path.Combine(physicalRootPath, ".codegraph"));
            }
            else if (current && ownership.Phase == PreparedCodeGraphOwnershipPhase.Ready)
            {
                current = ownership.StateEntryObjectIdentity is not null &&
                    TryObserveDirectoryIdentity(
                        Path.Combine(physicalRootPath, ".codegraph"),
                        out PreparedToolDirectoryObjectIdentity? actual) &&
                    actual == ownership.StateEntryObjectIdentity;
            }

            return ValueTask.FromResult(current);
        }

        public ValueTask<bool> VerifyDispositionReadyAsync(
            PreparedCodeGraphWorkspaceOwnership ownership,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool bindingCurrent = (ownership.Phase is PreparedCodeGraphOwnershipPhase.Ready or
                    PreparedCodeGraphOwnershipPhase.Disposing) &&
                ownership.PhysicalRootPath.Equals(physicalRootPath, StringComparison.OrdinalIgnoreCase) &&
                ownership.RootObjectIdentity == rootIdentity &&
                ownership.WorkspaceOwnerIdentity == ownerIdentity &&
                ownership.StateEntryObjectIdentity is not null;
            string statePath = Path.Combine(physicalRootPath, ".codegraph");
            bool entryCurrent = ownership.Phase == PreparedCodeGraphOwnershipPhase.Ready
                ? TryObserveDirectoryIdentity(
                    statePath,
                    out PreparedToolDirectoryObjectIdentity? readyEntry) &&
                    readyEntry == ownership.StateEntryObjectIdentity
                : ownership.Phase == PreparedCodeGraphOwnershipPhase.Disposing &&
                    (!Directory.Exists(statePath) ||
                        TryObserveDirectoryIdentity(
                            statePath,
                            out PreparedToolDirectoryObjectIdentity? disposingEntry) &&
                        disposingEntry == ownership.StateEntryObjectIdentity);
            bool current = bindingCurrent && entryCurrent;
            return ValueTask.FromResult(current);
        }
    }

    private static bool TryObserveDirectoryIdentity(
        string path,
        out PreparedToolDirectoryObjectIdentity? identity)
    {
        (PreparedSourceTraversal.OpenStatus status, SafeFileHandle? handle) =
            PreparedSourceTraversal.OpenRoot(path);
        using (handle)
        {
            if (status != PreparedSourceTraversal.OpenStatus.Opened || handle is null)
            {
                identity = null;
                return false;
            }

            PreparedSourceTraversal.DirectoryIdentity? observed =
                PreparedSourceTraversal.ObserveDirectory(handle);
            identity = observed is null
                ? null
                : new PreparedToolDirectoryObjectIdentity(
                    observed.VolumeSerialNumber,
                    observed.FileIndexHigh,
                    observed.FileIndexLow);
            return identity is not null;
        }
    }
}
