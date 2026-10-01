using System.Collections.Immutable;
using Vulgata.Core.DomainServices;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October;

public sealed class OctoberHostCompositionTests
{
    private const string TargetId = "october-host-target-a";
    private const string CanonicalDatabaseIdentity = "sqlite://owned/october-host-target-a";
    private const string ExpectedManagementAuthorityId = "ordinary-management-a";
    private const string OtherManagementAuthorityId = "ordinary-management-b";
    private const string SubjectId = "october-host-subject";
    private const string OperatorLabel = "october-host-operator";
    private static readonly Guid BatchId = Guid.Parse("e3576576-f6ca-49e0-9ec6-aa784c5bab64");
    private static readonly Guid RepositoryId = Guid.Parse("a579c66a-2b3d-476f-9f2d-abdb90066289");
    private static readonly Guid SystemId = Guid.Parse("b268fa5c-d81b-4a88-a4c3-94e347348d4e");
    private static readonly Guid GlobalId = Guid.Parse("72e41d20-e11f-4df8-b46a-cdfc6288a7ae");
    private static readonly DateTimeOffset FirstCommitUtc =
        new(2026, 9, 30, 11, 20, 0, TimeSpan.Zero);

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_PrepareRunsLocalAuthorityThenIdentityMigrationAndPolicy()
    {
        SelectionBundle selection = CreateSelection();
        OctoberSetupRequest callerRequest = selection.Request with { SelectionDigest = string.Empty };
        GeneratorRig rig = CreateRig(selection);
        OctoberGeneratorCommand command = new(
            OctoberGeneratorMode.Prepare,
            TargetId,
            BatchId,
            callerRequest);

        OctoberGeneratorExecutionResult result = await rig.Module.ExecuteAsync(command);

        Assert.Equal(OctoberGeneratorExecutionStatus.Prepared, result.Status);
        Assert.Equal(
            new[] { "authenticate", "authorize-selection", "target-identity", "migrate", "write" },
            rig.Events.ToArray());
        Assert.Equal(TargetId, rig.Identity.LastTargetId);
        Assert.Equal(CanonicalDatabaseIdentity, rig.Identity.LastCanonicalIdentity);
        Assert.NotEqual(callerRequest.SelectionDigest, selection.Request.SelectionDigest);
        Assert.Equal(selection.Request.TargetId, rig.Writer.LastRequest!.TargetId);
        Assert.Equal(selection.Request.BatchId, rig.Writer.LastRequest.BatchId);
        Assert.Equal(selection.Request.SelectionDigest, rig.Writer.LastRequest.SelectionDigest);
        Assert.Equal(selection.Authority.SelectionDigest, rig.Writer.LastAuthority!.SelectionDigest);
        Assert.Equal(OctoberPrepareStatus.Created, rig.Writer.LastResult.Status);
        Assert.Equal(FirstCommitUtc, result.CommittedBatch!.SelectedAtUtc);
        Assert.Null(result.BoundSetupData);
        Assert.Equal(0, rig.Selector.CallCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_CancellationAfterAuthorizedSelection_DoesNotReachTargetOrWrite()
    {
        SelectionBundle selection = CreateSelection();
        using CancellationTokenSource cancellation = new();
        GeneratorRig rig = CreateRig(selection, cancelAfterAuthorization: cancellation);
        OctoberGeneratorCommand command = new(
            OctoberGeneratorMode.Prepare,
            TargetId,
            BatchId,
            selection.Request);

        OctoberGeneratorExecutionResult result = await rig.Module.ExecuteAsync(
            command, cancellation.Token);

        Assert.Equal(OctoberGeneratorExecutionStatus.Cancelled, result.Status);
        Assert.Equal(new[] { "authenticate", "authorize-selection" }, rig.Events.ToArray());
        Assert.Null(result.CommittedBatch);
        Assert.Null(result.BoundSetupData);
        Assert.Equal(0, rig.Identity.CallCount);
        Assert.Equal(0, rig.Migrator.CallCount);
        Assert.Equal(0, rig.Writer.CallCount);
        Assert.Equal(0, rig.Selector.CallCount);
        Assert.True(rig.Authority.LastSession.IsDisposed,
            "The authenticated session must be disposed when cancellation ends the command.");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_SelectUsesExactSelectorWithoutMigrationOrWriter()
    {
        SelectionBundle selection = CreateSelection();
        OctoberBoundSetupSnapshot history = CreateSnapshot(selection);
        OctoberHistoricalBoundData bound = new(TargetId, BatchId, history);
        GeneratorRig rig = CreateRig(selection, selectorResult:
            new OctoberSelectionResult(OctoberSelectionStatus.Selected, bound));
        OctoberGeneratorCommand command = new(
            OctoberGeneratorMode.Select,
            TargetId,
            BatchId,
            SetupRequest: null);

        OctoberGeneratorExecutionResult result = await rig.Module.ExecuteAsync(command);

        Assert.Equal(OctoberGeneratorExecutionStatus.Selected, result.Status);
        Assert.Equal(new[] { "authenticate", "authorize-selection", "target-identity", "selector" },
            rig.Events.ToArray());
        Assert.Equal(TargetId, rig.Selector.LastSelection!.TargetId);
        Assert.Equal(BatchId, rig.Selector.LastSelection.BatchId);
        Assert.Equal(selection.Authority.SelectionDigest,
            rig.Selector.LastSelection.Authority.SelectionDigest);
        Assert.Same(bound, result.BoundSetupData);
        Assert.Null(result.CommittedBatch);
        Assert.Equal(0, rig.Migrator.CallCount);
        Assert.Equal(0, rig.Writer.CallCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_SelectWithFreshSameAuthorityContext_PreservesHistoricalSnapshot()
    {
        SelectionBundle stored = CreateSelection();
        SelectionBundle fresh = ChangeAdvisoryValues(stored);
        OctoberHistoricalBoundData historical = new(
            TargetId, BatchId, CreateSnapshot(stored));
        GeneratorRig rig = CreateRig(fresh, selectorResult:
            new OctoberSelectionResult(OctoberSelectionStatus.Selected, historical));
        OctoberGeneratorCommand command = new(
            OctoberGeneratorMode.Select,
            TargetId,
            BatchId,
            SetupRequest: null);

        OctoberGeneratorExecutionResult result = await rig.Module.ExecuteAsync(command);

        Assert.Equal(OctoberGeneratorExecutionStatus.Selected, result.Status);
        Assert.Same(historical, result.BoundSetupData);
        Assert.Equal(stored.Request.SelectionDigest,
            Assert.IsAssignableFrom<IOctoberHistoricalBoundSetupData>(result.BoundSetupData)
                .Snapshot.Request.SelectionDigest);
        Assert.Equal(FirstCommitUtc,
            Assert.IsAssignableFrom<IOctoberHistoricalBoundSetupData>(result.BoundSetupData)
                .Snapshot.SelectedAtUtc);
        Assert.NotEqual(fresh.Authority.SelectionDigest,
            Assert.IsAssignableFrom<IOctoberHistoricalBoundSetupData>(result.BoundSetupData)
                .Snapshot.Request.SelectionDigest);
        Assert.DoesNotContain("migrate", rig.Events);
        Assert.DoesNotContain("write", rig.Events);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_PrepareAuthorityBWithCollidingSelection_DeniesBeforeTargetAccess()
    {
        SelectionBundle authorityB = CreateSelection(OtherManagementAuthorityId);
        GeneratorRig rig = CreateRig(authorityB);
        OctoberGeneratorCommand command = new(
            OctoberGeneratorMode.Prepare,
            TargetId,
            BatchId,
            authorityB.Request);

        OctoberGeneratorExecutionResult result = await rig.Module.ExecuteAsync(command);

        Assert.Equal(OctoberGeneratorExecutionStatus.Denied, result.Status);
        Assert.Equal(new[] { "authenticate", "authorize-selection" }, rig.Events.ToArray());
        Assert.Null(result.CommittedBatch);
        Assert.Null(result.BoundSetupData);
        Assert.Equal(0, rig.Identity.CallCount);
        Assert.Equal(0, rig.Migrator.CallCount);
        Assert.Equal(0, rig.Writer.CallCount);
        Assert.Equal(0, rig.Selector.CallCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_AuthorityDenialStopsBeforeTargetAndReturnsClosedFailure()
    {
        SelectionBundle selection = CreateSelection();
        GeneratorRig rig = CreateRig(selection, OctoberAuthorityDecisionStatus.Denied);
        OctoberGeneratorCommand command = new(
            OctoberGeneratorMode.Prepare,
            TargetId,
            BatchId,
            selection.Request);

        OctoberGeneratorExecutionResult result = await rig.Module.ExecuteAsync(command);

        Assert.Equal(OctoberGeneratorExecutionStatus.Denied, result.Status);
        Assert.Equal(new[] { "authenticate", "authorize-selection" }, rig.Events.ToArray());
        Assert.Null(result.CommittedBatch);
        Assert.Null(result.BoundSetupData);
        Assert.Equal(0, rig.Identity.CallCount);
        Assert.Equal(0, rig.Migrator.CallCount);
        Assert.Equal(0, rig.Writer.CallCount);
        Assert.Equal(0, rig.Selector.CallCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_ConnectedTargetMismatchStopsBeforeMigrationOrSetupWrite()
    {
        SelectionBundle selection = CreateSelection();
        GeneratorRig rig = CreateRig(selection,
            connectedTargetStatus: OctoberConnectedTargetStatus.Mismatched);
        OctoberGeneratorCommand command = new(
            OctoberGeneratorMode.Prepare,
            TargetId,
            BatchId,
            selection.Request);

        OctoberGeneratorExecutionResult result = await rig.Module.ExecuteAsync(command);

        Assert.Equal(OctoberGeneratorExecutionStatus.TargetUnavailable, result.Status);
        Assert.Equal(new[] { "authenticate", "authorize-selection", "target-identity" }, rig.Events.ToArray());
        Assert.Null(result.CommittedBatch);
        Assert.Equal(0, rig.Migrator.CallCount);
        Assert.Equal(0, rig.Writer.CallCount);
        Assert.Equal(0, rig.Selector.CallCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_PrepareConflictReturnsNoCommittedCapability()
    {
        SelectionBundle selection = CreateSelection();
        GeneratorRig rig = CreateRig(selection,
            prepareResult: new OctoberPrepareResult(OctoberPrepareStatus.Conflict, null));
        OctoberGeneratorCommand command = new(
            OctoberGeneratorMode.Prepare,
            TargetId,
            BatchId,
            selection.Request);

        OctoberGeneratorExecutionResult result = await rig.Module.ExecuteAsync(command);

        Assert.Equal(OctoberGeneratorExecutionStatus.Denied, result.Status);
        Assert.Null(result.CommittedBatch);
        Assert.Null(result.BoundSetupData);
        Assert.Equal(new[] { "authenticate", "authorize-selection", "target-identity", "migrate", "write" },
            rig.Events.ToArray());
    }

    private static GeneratorRig CreateRig(
        SelectionBundle selection,
        OctoberAuthorityDecisionStatus authorityStatus = OctoberAuthorityDecisionStatus.Authorized,
        OctoberConnectedTargetStatus connectedTargetStatus = OctoberConnectedTargetStatus.Matched,
        OctoberPrepareResult? prepareResult = null,
        OctoberSelectionResult? selectorResult = null,
        CancellationTokenSource? cancelAfterAuthorization = null)
    {
        List<string> events = [];
        var authority = new RecordingLocalAuthority(
            events, selection, authorityStatus, cancelAfterAuthorization);
        var identity = new RecordingIdentityVerifier(events, connectedTargetStatus);
        var migrator = new RecordingMigrator(events);
        var writer = new RecordingSetupWriter(events, prepareResult);
        var selector = new RecordingSelector(events, selectorResult ??
            new OctoberSelectionResult(OctoberSelectionStatus.MissingBatch, null));
        var configuredTarget = new OctoberConfiguredTarget(
            TargetId, CanonicalDatabaseIdentity, identity, migrator, writer, selector);
        var module = new OctoberGeneratorModule(authority, configuredTarget,
            ExpectedManagementAuthorityId);
        return new GeneratorRig(module, events, authority, identity, migrator, writer, selector);
    }

    private static SelectionBundle CreateSelection(string managementAuthorityId = ExpectedManagementAuthorityId)
    {
        OctoberRepositorySelection repository = new(
            RepositoryId, SystemId, "root-binding-v1", OperatorLabel);
        OctoberSetupRequest request = new(
            TargetId,
            BatchId,
            [repository],
            [
                new OctoberContextSelection(OctoberContextScope.Global, GlobalId),
                new OctoberContextSelection(OctoberContextScope.System, SystemId),
                new OctoberContextSelection(OctoberContextScope.Repository, RepositoryId),
            ],
            string.Empty);
        OctoberSelectedAdvisoryContext context = new(
            managementAuthorityId,
            ImmutableArray.Create(repository),
            ImmutableArray.Create(
                Entry(OctoberContextScope.Global, GlobalId, "global advisory v1"),
                Entry(OctoberContextScope.System, SystemId, "system advisory v1"),
                Entry(OctoberContextScope.Repository, RepositoryId, "repository advisory v1")),
            ImmutableArray.Create(
                Difference(OctoberContextScope.Global, OctoberContextScope.System),
                Difference(OctoberContextScope.Global, OctoberContextScope.Repository),
                Difference(OctoberContextScope.System, OctoberContextScope.Repository)));
        string digest = OctoberSelectionDigest.Compute(request, context);
        request = request with { SelectionDigest = digest };
        OctoberAuthorizedSelection authority = new(
            TargetId,
            BatchId,
            SubjectId,
            CanonicalDatabaseIdentity,
            [RepositoryId],
            digest,
            context);
        return new SelectionBundle(request, authority);
    }

    private static SelectionBundle ChangeAdvisoryValues(SelectionBundle original)
    {
        OctoberSelectedAdvisoryContext context = original.Authority.SelectedContext!;
        OctoberAdvisoryContextEntry[] entries = context.Entries.ToArray();
        entries[0] = entries[0] with { Value = "global advisory revised" };
        entries[1] = entries[1] with { Value = "system advisory revised" };
        entries[2] = entries[2] with { Value = "repository advisory revised" };
        OctoberSelectedAdvisoryContext freshContext = context with
        {
            Entries = entries.ToImmutableArray(),
        };
        string digest = OctoberSelectionDigest.Compute(original.Request, freshContext);
        OctoberSetupRequest freshRequest = original.Request with { SelectionDigest = digest };
        return new SelectionBundle(freshRequest, original.Authority with
        {
            SelectionDigest = digest,
            SelectedContext = freshContext,
        });
    }

    private static OctoberBoundSetupSnapshot CreateSnapshot(SelectionBundle selection) =>
        new(selection.Request, selection.Authority, FirstCommitUtc);

    private static OctoberAdvisoryContextEntry Entry(
        OctoberContextScope scope,
        Guid origin,
        string value) => new(
            RepositoryId,
            scope,
            OctoberContextApplicability.Selected,
            origin,
            value,
            OctoberContextEvidenceKind.AdvisoryNotSourceProof);

    private static OctoberContextDifference Difference(
        OctoberContextScope left,
        OctoberContextScope right) => new(RepositoryId, left, right);

    private sealed record SelectionBundle(
        OctoberSetupRequest Request,
        OctoberAuthorizedSelection Authority);

    private sealed record OctoberHistoricalBoundData(
        string TargetId,
        Guid BatchId,
        OctoberBoundSetupSnapshot Snapshot) : IOctoberHistoricalBoundSetupData;

    private sealed record GeneratorRig(
        IOctoberGeneratorModule Module,
        List<string> Events,
        RecordingLocalAuthority Authority,
        RecordingIdentityVerifier Identity,
        RecordingMigrator Migrator,
        RecordingSetupWriter Writer,
        RecordingSelector Selector);

    private sealed class RecordingLocalAuthority(
        List<string> events,
        SelectionBundle selection,
        OctoberAuthorityDecisionStatus selectionStatus,
        CancellationTokenSource? cancelAfterAuthorization) : IOctoberLocalAuthority
    {
        private readonly RecordingOperatorSession _session = new(
            SubjectId,
            selection.Authority.SelectedContext?.ManagementAuthorityId ?? ExpectedManagementAuthorityId,
            FirstCommitUtc.AddHours(1));

        public RecordingOperatorSession LastSession => _session;

        public Task<OctoberAuthenticationResult> AuthenticateAsync(CancellationToken cancellationToken)
        {
            events.Add("authenticate");
            return Task.FromResult(new OctoberAuthenticationResult(
                OctoberAuthenticationStatus.Authenticated, _session));
        }

        public Task<OctoberAuthoritySelectionResult> AuthorizeSelectionAsync(
            IOctoberOperatorSession session,
            OctoberAuthoritySelectionRequest request,
            CancellationToken cancellationToken)
        {
            events.Add("authorize-selection");
            OctoberAuthoritySelectionResult result = new(
                selectionStatus,
                selectionStatus == OctoberAuthorityDecisionStatus.Authorized
                    ? selection.Request
                    : null,
                selectionStatus == OctoberAuthorityDecisionStatus.Authorized
                    ? selection.Authority
                    : null);
            if (selectionStatus == OctoberAuthorityDecisionStatus.Authorized)
                cancelAfterAuthorization?.Cancel();
            return Task.FromResult(result);
        }

        public Task<OctoberSourceApprovalResult> IssueSourceApprovalAsync(
            IOctoberOperatorSession session,
            IOctoberHistoricalBoundSetupData boundSetup,
            Guid repositoryId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Generator prepare/select tests do not issue source approvals.");

        public Task<PreparedSourceAuthorityResult> VerifySourceApprovalAsync(
            IOctoberOperatorSession session,
            string targetId,
            PreparedSourceApproval approval,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Generator prepare/select tests do not verify source approvals.");

        public Task<OctoberRevocationStatus> RevokeSourceApprovalAsync(
            IOctoberOperatorSession session,
            string targetId,
            string approvalIdentity,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Generator prepare/select tests do not revoke source approvals.");
    }

    private sealed class RecordingOperatorSession(
        string subjectId,
        string authorityId,
        DateTimeOffset expiresAtUtc) : IOctoberOperatorSession
    {
        public string SubjectId { get; } = subjectId;
        public string ManagementAuthorityId { get; } = authorityId;
        public DateTimeOffset ExpiresAtUtc { get; } = expiresAtUtc;
        public bool IsDisposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingIdentityVerifier(
        List<string> events,
        OctoberConnectedTargetStatus status) : IOctoberConnectedTargetIdentityVerifier
    {
        public int CallCount { get; private set; }
        public string? LastTargetId { get; private set; }
        public string? LastCanonicalIdentity { get; private set; }

        public Task<OctoberConnectedTargetStatus> VerifyAsync(
            string targetId,
            string canonicalDatabaseIdentity,
            CancellationToken cancellationToken = default)
        {
            events.Add("target-identity");
            CallCount++;
            LastTargetId = targetId;
            LastCanonicalIdentity = canonicalDatabaseIdentity;
            return Task.FromResult(status);
        }
    }

    private sealed class RecordingMigrator(List<string> events) : IOctoberTargetMigrator
    {
        public int CallCount { get; private set; }

        public Task MigrateAsync(CancellationToken cancellationToken = default)
        {
            events.Add("migrate");
            CallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSetupWriter(
        List<string> events,
        OctoberPrepareResult? configuredResult) : IOctoberSetupWriter
    {
        public int CallCount { get; private set; }
        public OctoberSetupRequest? LastRequest { get; private set; }
        public OctoberAuthorizedSelection? LastAuthority { get; private set; }
        public OctoberPrepareResult LastResult { get; private set; } = new(OctoberPrepareStatus.Denied, null);

        public Task<OctoberPrepareResult> CommitAsync(
            OctoberSetupRequest request,
            OctoberAuthorizedSelection authority,
            CancellationToken cancellationToken = default)
        {
            events.Add("write");
            CallCount++;
            LastRequest = request;
            LastAuthority = authority;
            LastResult = configuredResult ?? new OctoberPrepareResult(
                OctoberPrepareStatus.Created,
                new OctoberCommittedBatch(
                    TargetId, BatchId, CanonicalDatabaseIdentity,
                    request.SelectionDigest, FirstCommitUtc));
            return Task.FromResult(LastResult);
        }
    }

    private sealed class RecordingSelector(
        List<string> events,
        OctoberSelectionResult configuredResult) : IOctoberBatchSelector
    {
        public int CallCount { get; private set; }
        public OctoberStartupSelection? LastSelection { get; private set; }

        public Task<OctoberSelectionResult> BindAsync(
            OctoberStartupSelection selection,
            CancellationToken cancellationToken = default)
        {
            events.Add("selector");
            CallCount++;
            LastSelection = selection;
            return Task.FromResult(configuredResult);
        }
    }
}
