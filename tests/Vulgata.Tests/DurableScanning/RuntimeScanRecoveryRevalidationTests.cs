using Vulgata.Tests.TestInfrastructure;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Web;

namespace Vulgata.Tests.DurableScanning;

/// <summary>
/// T057 RED: recovery binding observations must be obtained from independent,
/// current authorities.  The persisted recovery snapshot below deliberately
/// contains the old revision, source hash, prompt, and schema.  The production
/// probe receives no IDurableScanStore; it must query the Repository, the
/// completed ScanRun and its Code Units, the current Worker dispatch identity,
/// and the current Worker provider assignment.
/// </summary>
public sealed class RuntimeScanRecoveryRevalidationTests
{
    private static readonly Guid _systemId =
        Guid.Parse("57000000-0000-4000-8000-000000000001");
    private static readonly Guid _repositoryId =
        Guid.Parse("57000000-0000-4000-8000-000000000002");
    private static readonly Guid _executionId =
        Guid.Parse("56000000-0000-4000-8000-000000000003");
    private static readonly Guid _t056ScanRunId =
        Guid.Parse("56000000-0000-4000-8000-000000000006");
    private const string _revision =
        "0123456789abcdef0123456789abcdef01234567";
    private const string _sourceHash =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string _t056RuntimeHash =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string _oldRevision =
        "1111111111111111111111111111111111111111";
    private const string _currentRevision =
        "2222222222222222222222222222222222222222";
    private const string _oldSourceHash =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string _currentSourceHash =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string _runtimeHash =
        "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RevalidateAsync_ExactCurrentBindings_AcceptsWithoutInventingTransition()
    {
        ScanRecoverySnapshot snapshot = SnapshotT056();
        FakeBindingProbe probe = new(ObservedT056(snapshot));
        RuntimeScanRecoveryRevalidator revalidator = new(ReadyRuntimeT056(), probe);

        ScanRecoveryRevalidationResult result = await revalidator.RevalidateAsync(
            snapshot,
            AuthorizedScopeT056(),
            CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal(DurableScanReason.None, result.Reason);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("repository")]
    [InlineData("revision")]
    [InlineData("file-hash")]
    [InlineData("prompt")]
    [InlineData("schema")]
    [InlineData("provider")]
    [InlineData("runtime")]
    public async Task RevalidateAsync_EachCurrentBindingMismatch_FailsClosed(string mismatch)
    {
        ScanRecoverySnapshot snapshot = SnapshotT056();
        ScanRecoveryObservedBindings observed = ObservedT056(snapshot) with
        {
            RepositoryAccessible = mismatch == "repository" ? false : true,
            ProfiledRevision = mismatch == "revision" ? new string('c', 40) : snapshot.ProfiledRevision,
            AffectedFiles = mismatch == "file-hash"
                ? [new ScanRecoveryFileBinding(snapshot.AffectedFiles[0].FileCodeUnitId, new string('d', 64))]
                : snapshot.AffectedFiles,
            PromptIdentity = mismatch == "prompt" ? "prompt/other" : snapshot.PromptIdentity,
            DocumentSchemaIdentity = mismatch == "schema" ? "document-schema/other" : snapshot.DocumentSchemaIdentity,
            ProviderAvailable = mismatch == "provider" ? false : true,
            AnalysisRuntimeIdentityHash = mismatch == "runtime" ? new string('e', 64) : snapshot.AnalysisRuntimeIdentityHash,
        };
        RuntimeScanRecoveryRevalidator revalidator =
            new(ReadyRuntimeT056(), new FakeBindingProbe(observed));

        ScanRecoveryRevalidationResult result = await revalidator.RevalidateAsync(
            snapshot,
            AuthorizedScopeT056(),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Reason, new[]
        {
            DurableScanReason.Unauthorized,
            DurableScanReason.InvalidIdentity,
            DurableScanReason.ProviderUnavailable,
            DurableScanReason.IntegrityViolation,
        });
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RevalidateAsync_RepositoryNotInCurrentOwnedRepositoryScope_FailsClosed()
    {
        ScanRecoverySnapshot snapshot = SnapshotT056();
        RuntimeScanRecoveryRevalidator revalidator = new(
            ReadyRuntimeT056(),
            new FakeBindingProbe(ObservedT056(snapshot)));

        ScanRecoveryRevalidationResult result = await revalidator.RevalidateAsync(
            snapshot,
            new DurableScanAuthorizationScope(
                "t056-actor",
                [_systemId],
                [Guid.Parse("56000000-0000-0000-8000-000000000099")]),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(DurableScanReason.Unauthorized, result.Reason);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RevalidateAsync_NullCurrentBindings_FailsClosed()
    {
        ScanRecoverySnapshot snapshot = SnapshotT056();
        RuntimeScanRecoveryRevalidator revalidator = new(
            ReadyRuntimeT056(),
            new FakeBindingProbe(null));

        ScanRecoveryRevalidationResult result = await revalidator.RevalidateAsync(
            snapshot,
            AuthorizedScopeT056(),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(DurableScanReason.InvalidIdentity, result.Reason);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RevalidateAsync_ConfiguredRuntimeIdentityDiffersFromPersistedSnapshot_FailsBeforeTransition()
    {
        ScanRecoverySnapshot snapshot = SnapshotT056();
        FakeBindingProbe bindingProbe = new(ObservedT056(snapshot));
        FixedHealthProbe configuredHealthProbe = new(new AnalysisHealthProbeResult(
            AnalysisRuntimeHealthStatus.Ready,
            "none",
            string.Empty,
            2,
            [
                new AnalysisHealthCheck("codegraph", AnalysisRuntimeHealthStatus.Ready),
                new AnalysisHealthCheck("jdt-ls", AnalysisRuntimeHealthStatus.Ready),
            ],
            runtimeIdentityHash: new string('c', 64)));
        RuntimeScanRecoveryRevalidator revalidator = new(
            ReadyRuntimeT056(),
            configuredHealthProbe,
            bindingProbe);

        ScanRecoveryRevalidationResult result = await revalidator.RevalidateAsync(
            snapshot,
            AuthorizedScopeT056(),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(DurableScanReason.InvalidIdentity, result.Reason);
        Assert.Equal(1, bindingProbe.CallCount);
        Assert.Equal(snapshot.RepositoryId, configuredHealthProbe.Scope?.RepositoryId);
        Assert.Equal(snapshot.ProfiledRevision, configuredHealthProbe.Scope?.ProfiledRevision);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RevalidateAsync_ConfiguredRuntimeHealthFailure_FailsClosedWithBoundedReason()
    {
        ScanRecoverySnapshot snapshot = SnapshotT056();
        FakeBindingProbe bindingProbe = new(ObservedT056(snapshot));
        ThrowingHealthProbe configuredHealthProbe = new(
            new IOException("sensitive configured-runtime failure"));
        RuntimeScanRecoveryRevalidator revalidator = new(
            ReadyRuntimeT056(),
            configuredHealthProbe,
            bindingProbe);

        ScanRecoveryRevalidationResult result = await revalidator.RevalidateAsync(
            snapshot,
            AuthorizedScopeT056(),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Reason,
            new[]
            {
                DurableScanReason.InvalidIdentity,
                DurableScanReason.IntegrityViolation,
                DurableScanReason.ProviderUnavailable,
            });
        Assert.Equal(1, bindingProbe.CallCount);
        Assert.Equal(1, configuredHealthProbe.CallCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RevalidateAsync_ConfiguredRuntimeHealthCancellation_PropagatesWithoutFailClosedMapping()
    {
        ScanRecoverySnapshot snapshot = SnapshotT056();
        FakeBindingProbe bindingProbe = new(ObservedT056(snapshot));
        ThrowingHealthProbe configuredHealthProbe = new(
            new OperationCanceledException("configured-runtime cancellation"));
        RuntimeScanRecoveryRevalidator revalidator = new(
            ReadyRuntimeT056(),
            configuredHealthProbe,
            bindingProbe);

        await Assert.ThrowsAsync<OperationCanceledException>(() => revalidator.RevalidateAsync(
            snapshot,
            AuthorizedScopeT056(),
            CancellationToken.None));

        Assert.Equal(1, bindingProbe.CallCount);
        Assert.Equal(1, configuredHealthProbe.CallCount);
    }

    private static ScanRecoverySnapshot SnapshotT056() =>
        new(
            _executionId,
            _systemId,
            _repositoryId,
            _t056ScanRunId,
            4,
            [],
            ScanExecutionPhase.PausedProviderUnavailable,
            _revision,
            [new ScanRecoveryFileBinding(
                Guid.Parse("56000000-0000-4000-8000-000000000004"),
                _sourceHash)],
            "prompt/1.0",
            "document-schema/1.0",
            _t056RuntimeHash);

    private static DurableScanAuthorizationScope AuthorizedScopeT056() =>
        new("t056-actor", [_systemId], [_repositoryId]);

    private static AnalysisRuntimeReadiness ReadyRuntimeT056()
    {
        AnalysisRuntimeReadiness readiness = new();
        readiness.Update(new AnalysisHealthProbeResult(
            AnalysisRuntimeHealthStatus.Ready,
            "none",
            string.Empty,
            1,
            [
                new AnalysisHealthCheck("codegraph", AnalysisRuntimeHealthStatus.Ready),
                new AnalysisHealthCheck("jdt-ls", AnalysisRuntimeHealthStatus.Ready),
            ],
            runtimeIdentityHash: _t056RuntimeHash));
        return readiness;
    }

    private static ScanRecoveryObservedBindings ObservedT056(ScanRecoverySnapshot snapshot) =>
        new(
            true,
            snapshot.ProfiledRevision,
            snapshot.AffectedFiles,
            snapshot.PromptIdentity,
            snapshot.DocumentSchemaIdentity,
            true,
            snapshot.AnalysisRuntimeIdentityHash);

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ObserveAsync_UsesIndependentCurrentAuthorities_NotPersistedRecoverySnapshot()
    {
        CurrentAuthorities authorities = CreateCurrentAuthorities();
        ScanRecoverySnapshot persistedSnapshot = new(
            Guid.Parse("57000000-0000-4000-8000-000000000005"),
            _systemId,
            _repositoryId,
            authorities.CurrentScanRun.Id,
            9,
            [],
            ScanExecutionPhase.PausedProviderUnavailable,
            _oldRevision,
            [new ScanRecoveryFileBinding(
                authorities.CurrentCodeUnit.Id,
                _oldSourceHash)],
            "pass1/old",
            "document/old",
            _runtimeHash);

        RuntimeScanRecoveryBindingProbe probe = new(
            authorities.Repositories,
            authorities.ScanRuns,
            authorities.DispatchIdentity,
            authorities.ProviderResolver);

        ScanRecoveryObservedBindings? observed = await probe.ObserveAsync(
            persistedSnapshot,
            new DurableScanAuthorizationScope(
                "t057-actor",
                [_systemId],
                [_repositoryId]),
            CancellationToken.None);

        Assert.NotNull(observed);
        Assert.True(observed.RepositoryAccessible);
        Assert.Equal(_currentRevision, observed.ProfiledRevision);
        Assert.Equal(
            [new ScanRecoveryFileBinding(
                authorities.CurrentCodeUnit.Id,
                _currentSourceHash)],
            observed.AffectedFiles);
        Assert.Equal("pass1/current", observed.PromptIdentity);
        Assert.Equal(KnowledgeDocumentLimits.SchemaIdentity, observed.DocumentSchemaIdentity);
        Assert.True(observed.ProviderAvailable);

        Assert.Equal(1, authorities.Repositories.CallCount);
        Assert.Equal(1, authorities.ScanRuns.CallCount);
        Assert.Equal(persistedSnapshot.RepositoryId, authorities.ScanRuns.LastRepositoryId);
        Assert.Equal(persistedSnapshot.ScanRunId, authorities.ScanRuns.LastScanRunId);
        Assert.Equal(1, authorities.DispatchIdentity.CallCount);
        Assert.Equal(1, authorities.ProviderOverrideRepository.CallCount);
        Assert.Equal(1, authorities.ProviderRepository.CallCount);
    }

    private static CurrentAuthorities CreateCurrentAuthorities()
    {
        Repository repository = Repository.Create(
            _systemId,
            "current-repository",
            "https://example.invalid/current.git",
            null,
            null,
            DateTimeOffset.UnixEpoch);
        var repositories = new FakeRepositoryRepository(repository);

        ScanRun scanRun = ScanRun.Start(_repositoryId, 30, DateTimeOffset.UnixEpoch);
        scanRun.BindProfiledRevision(_currentRevision);
        CodeUnit codeUnit = new(
            scanRun.Id,
            _repositoryId,
            "src/current.cs",
            "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
            _currentSourceHash,
            1,
            12,
            "CurrentType",
            CodeUnitType.File,
            RangeConfidence.Exact,
            "csharp");
        scanRun.CodeUnits.Add(codeUnit);
        var scanRuns = new FakeScanRunRepository(scanRun, codeUnit);

        var dispatchIdentity = new MutableDispatchIdentitySource(
            new ScanRecoveryDispatchIdentity(
                "pass1/current",
                KnowledgeDocumentLimits.SchemaIdentity));
        var overrideRepository = new FakeSystemLlmProviderOverrideRepository();
        var providerRepository = new FakeLlmProviderRepository();
        var resolver = new WorkerAgentProviderResolver(
            overrideRepository,
            providerRepository);

        return new(
            repositories,
            scanRuns,
            dispatchIdentity,
            resolver,
            overrideRepository,
            providerRepository,
            scanRun,
            codeUnit);
    }

    private sealed record CurrentAuthorities(
        FakeRepositoryRepository Repositories,
        FakeScanRunRepository ScanRuns,
        MutableDispatchIdentitySource DispatchIdentity,
        WorkerAgentProviderResolver ProviderResolver,
        FakeSystemLlmProviderOverrideRepository ProviderOverrideRepository,
        FakeLlmProviderRepository ProviderRepository,
        ScanRun CurrentScanRun,
        CodeUnit CurrentCodeUnit);

    private sealed class MutableDispatchIdentitySource(
        ScanRecoveryDispatchIdentity? current) : IScanRecoveryDispatchIdentitySource
    {
        public ScanRecoveryDispatchIdentity? Current { get; set; } = current;
        public int CallCount { get; private set; }

        public Task<ScanRecoveryDispatchIdentity?> GetAsync(
            Guid systemId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(_systemId, systemId);
            CallCount++;
            return Task.FromResult(Current);
        }
    }

    private sealed class FakeRepositoryRepository(Repository current)
        : IRepositoryRepository
    {
        public int CallCount { get; private set; }

        public Task<Repository?> GetBySystemAndIdAsync(
            Guid systemId,
            Guid repositoryId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult<Repository?>(
                systemId == _systemId && repositoryId == _repositoryId ? current : null);
        }

        public Task<IReadOnlyList<Repository>> ListBySystemAsync(
            Guid systemId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Repository>> ListStandaloneAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Repository?> GetByIdAsync(
            Guid repositoryId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Repository?> GetStandaloneByIdAsync(
            Guid repositoryId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> NameExistsAsync(
            Guid systemId,
            string name,
            Guid? excludeRepositoryId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> StandaloneNameExistsAsync(
            string name,
            Guid? excludeRepositoryId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Repository> AddAsync(
            Repository repository,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Remove(Repository repository) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeScanRunRepository(ScanRun current, CodeUnit currentCodeUnit)
        : IScanRunRepository
    {
        public int CallCount { get; private set; }
        public Guid LastRepositoryId { get; private set; }
        public Guid LastScanRunId { get; private set; }

        public Task<ScanRun?> GetAsync(
            Guid repositoryId,
            Guid scanRunId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastRepositoryId = repositoryId;
            LastScanRunId = scanRunId;
            return Task.FromResult<ScanRun?>(
                repositoryId == _repositoryId && scanRunId == current.Id ? current : null);
        }

        public Task<ApprovedPreScanHandoff?> GetApprovedPreScanHandoffAsync(
            Guid repositoryId,
            Guid scanRunId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddAsync(ScanRun scanRun, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void AddCandidateOutcomes(IEnumerable<ScanCandidateOutcome> outcomes) =>
            throw new NotSupportedException();

        public void AddStructuralRecords(
            IEnumerable<CodeUnit> codeUnits,
            IEnumerable<CodeUnitEdge> edges,
            IEnumerable<UnresolvedStructuralRelationshipCandidate> unresolvedRelationships) =>
            throw new NotSupportedException();

        public Task<PreScanResult> PersistCompletedProfileAsync(
            RepositoryProfileCommitDraft draft,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RecoverFailedFinalCommitAsync(
            Guid repositoryId,
            Guid scanRunId,
            string sanitizedMessage,
            DateTimeOffset failedAt,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RecoverAbortedFinalCommitAsync(
            Guid repositoryId,
            Guid scanRunId,
            string sanitizedMessage,
            DateTimeOffset abortedAt,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeSystemLlmProviderOverrideRepository
        : ISystemLlmProviderOverrideRepository
    {
        public int CallCount { get; private set; }

        public Task<SystemLlmProviderOverride?> GetBySystemAndAgentTypeAsync(
            Guid systemId,
            AgentType agentType,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult<SystemLlmProviderOverride?>(null);
        }

        public Task<IReadOnlyList<SystemLlmProviderOverride>> ListBySystemAsync(
            Guid systemId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SystemLlmProviderOverride?> GetBySystemAndIdAsync(
            Guid systemId,
            Guid overrideId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SystemLlmProviderOverride> AddAsync(
            SystemLlmProviderOverride overrideEntry,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> RemoveAsync(
            Guid systemId,
            Guid overrideId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeLlmProviderRepository : ILlmProviderRepository
    {
        private readonly LlmProvider _provider = new(
            "current-worker",
            "https://provider.invalid/v1",
            "encrypted-key",
            ApiType.ChatCompletions,
            AgentType.Worker,
            DateTimeOffset.UnixEpoch);

        public int CallCount { get; private set; }

        public Task<IReadOnlyList<LlmProvider>> ListAllAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult<IReadOnlyList<LlmProvider>>([_provider]);
        }

        public Task<LlmProvider?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> NameExistsAsync(
            string name,
            Guid? excludeProviderId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LlmProvider> AddAsync(
            LlmProvider provider,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Remove(LlmProvider provider) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeBindingProbe(ScanRecoveryObservedBindings? observed)
        : IScanRecoveryBindingProbe
    {
        public int CallCount { get; private set; }

        public Task<ScanRecoveryObservedBindings?> ObserveAsync(
            ScanRecoverySnapshot snapshot,
            DurableScanAuthorizationScope authorizationScope,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(observed);
        }
    }

    private sealed class FixedHealthProbe(AnalysisHealthProbeResult result)
        : IAnalysisRuntimeHealthProbe
    {
        public AnalysisScope? Scope { get; private set; }

        public Task<AnalysisHealthProbeResult> CheckConfiguredIntegrityAsync(
            AnalysisScope scope,
            CancellationToken cancellationToken)
        {
            Scope = scope;
            return Task.FromResult(result);
        }
    }

    private sealed class ThrowingHealthProbe(Exception exception)
        : IAnalysisRuntimeHealthProbe
    {
        public int CallCount { get; private set; }

        public Task<AnalysisHealthProbeResult> CheckConfiguredIntegrityAsync(
            AnalysisScope scope,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromException<AnalysisHealthProbeResult>(exception);
        }
    }
}
