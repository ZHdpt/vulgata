using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Web;

namespace Vulgata.Tests.DurableScanning;

/// <summary>
/// T056 RED: production recovery revalidation must compare the immutable
/// recovery snapshot with current Repository access, revision/file bindings,
/// prompt/schema identity, provider availability, and canonical runtime
/// identity before the coordinator is allowed to transition durable state.
///
/// This test intentionally targets the accepted Core seam. At the current
/// boundary the new binding records, probe, and snapshot fields are absent;
/// the focused command is therefore expected to be a compile RED until the
/// implementer supplies those exact production contracts.
/// </summary>
public sealed class RuntimeScanRecoveryRevalidationTests
{
    private static readonly Guid _systemId =
        Guid.Parse("56000000-0000-4000-8000-000000000001");
    private static readonly Guid _repositoryId =
        Guid.Parse("56000000-0000-4000-8000-000000000002");
    private static readonly Guid _executionId =
        Guid.Parse("56000000-0000-4000-8000-000000000003");
    private const string _revision =
        "0123456789abcdef0123456789abcdef01234567";
    private const string _sourceHash =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string _runtimeHash =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public async Task RevalidateAsync_ExactCurrentBindings_AcceptsWithoutInventingTransition()
    {
        ScanRecoverySnapshot snapshot = Snapshot();
        FakeBindingProbe probe = new(Observed(snapshot));
        RuntimeScanRecoveryRevalidator revalidator = new(ReadyRuntime(), probe);

        ScanRecoveryRevalidationResult result = await revalidator.RevalidateAsync(
            snapshot,
            AuthorizedScope(),
            CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal(DurableScanReason.None, result.Reason);
    }

    [Theory]
    [InlineData("repository")]
    [InlineData("revision")]
    [InlineData("file-hash")]
    [InlineData("prompt")]
    [InlineData("schema")]
    [InlineData("provider")]
    [InlineData("runtime")]
    public async Task RevalidateAsync_EachCurrentBindingMismatch_FailsClosed(string mismatch)
    {
        ScanRecoverySnapshot snapshot = Snapshot();
        ScanRecoveryObservedBindings observed = Observed(snapshot) with
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
            new(ReadyRuntime(), new FakeBindingProbe(observed));

        ScanRecoveryRevalidationResult result = await revalidator.RevalidateAsync(
            snapshot,
            AuthorizedScope(),
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

    [Fact]
    public async Task RevalidateAsync_RepositoryNotInCurrentOwnedRepositoryScope_FailsClosed()
    {
        ScanRecoverySnapshot snapshot = Snapshot();
        RuntimeScanRecoveryRevalidator revalidator = new(
            ReadyRuntime(),
            new FakeBindingProbe(Observed(snapshot)));

        DurableScanAuthorizationScope scope = new(
            "t056-actor",
            [_systemId],
            [Guid.Parse("56000000-0000-4000-8000-000000000099")]);

        ScanRecoveryRevalidationResult result = await revalidator.RevalidateAsync(
            snapshot,
            scope,
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(DurableScanReason.Unauthorized, result.Reason);
    }

    [Fact]
    public async Task RevalidateAsync_NullCurrentBindings_FailsClosed()
    {
        ScanRecoverySnapshot snapshot = Snapshot();
        RuntimeScanRecoveryRevalidator revalidator = new(
            ReadyRuntime(),
            new FakeBindingProbe(null));

        ScanRecoveryRevalidationResult result = await revalidator.RevalidateAsync(
            snapshot,
            AuthorizedScope(),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(DurableScanReason.InvalidIdentity, result.Reason);
    }

    [Fact]
    public async Task RevalidateAsync_ConfiguredRuntimeIdentityDiffersFromPersistedSnapshot_FailsBeforeTransition()
    {
        ScanRecoverySnapshot snapshot = Snapshot();
        FakeBindingProbe bindingProbe = new(Observed(snapshot));
        FixedHealthProbe configuredHealthProbe = new(
            new AnalysisHealthProbeResult(
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
            ReadyRuntime(),
            configuredHealthProbe,
            bindingProbe);

        ScanRecoveryRevalidationResult result = await revalidator.RevalidateAsync(
            snapshot,
            AuthorizedScope(),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(DurableScanReason.InvalidIdentity, result.Reason);
        Assert.Equal(1, bindingProbe.CallCount);
        Assert.Equal(snapshot.RepositoryId, configuredHealthProbe.Scope?.RepositoryId);
        Assert.Equal(snapshot.ProfiledRevision, configuredHealthProbe.Scope?.ProfiledRevision);
    }

    private static ScanRecoverySnapshot Snapshot() =>
        new(
            _executionId,
            _systemId,
            _repositoryId,
            4,
            [],
            ScanExecutionPhase.PausedProviderUnavailable,
            _revision,
            [new ScanRecoveryFileBinding(
                Guid.Parse("56000000-0000-4000-8000-000000000004"),
                _sourceHash)],
            "prompt/1.0",
            "document-schema/1.0",
            _runtimeHash);

    private static DurableScanAuthorizationScope AuthorizedScope() =>
        new("t056-actor", [_systemId], [_repositoryId]);

    private static AnalysisRuntimeReadiness ReadyRuntime()
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
            runtimeIdentityHash: _runtimeHash));
        return readiness;
    }

    private static ScanRecoveryObservedBindings Observed(ScanRecoverySnapshot snapshot) =>
        new(
            true,
            snapshot.ProfiledRevision,
            snapshot.AffectedFiles,
            snapshot.PromptIdentity,
            snapshot.DocumentSchemaIdentity,
            true,
            snapshot.AnalysisRuntimeIdentityHash);

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
}
