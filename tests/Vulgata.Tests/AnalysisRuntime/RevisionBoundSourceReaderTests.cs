using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Git;

namespace Vulgata.Tests.AnalysisRuntime;

/// <summary>
/// T034 RED coverage for the revision-contained source-reading port.
/// The adapter is deliberately obtained through its public Core port.  The
/// small reflection-only factory keeps this RED test compilable before the
/// T035 Infrastructure type exists; it does not expose or test a private
/// implementation member.
/// </summary>
public sealed class RevisionBoundSourceReaderTests
{
    private static readonly Guid _repositoryId =
        Guid.Parse("12000000-0000-4000-8000-000000000002");

    private static readonly Guid _scanRunId =
        Guid.Parse("12000000-0000-4000-8000-000000000102");

    private const string ProfiledRevision =
        "0123456789abcdef0123456789abcdef01234567";

    private const string SourceRelativePath =
        "src/main/java/example/analysis/GreetingService.java";

    private const string SourceSha256 =
        "50b7491a6ad4a65e4f794127c0b90608b520b4874b6a2006a28073b3d9fa62ee";

    private const string ExpectedExcerpt =
        "public final class GreetingService {\n" +
        "    private final GreetingFormatter formatter;\n" +
        "\n" +
        "    public GreetingService(GreetingFormatter formatter) {\n" +
        "        this.formatter = Objects.requireNonNull(formatter);";

    [Fact]
    public async Task ReadAsync_UsesOnlyVerifiedRevisionLeaseAndReturnsExactBoundedEvidence()
    {
        string fixtureRoot = FindFixtureRepositoryRoot();
        const string bindingToken = "fixture-binding-1200000000000000000000000000000000000002";
        TestSnapshotLease lease = new(_repositoryId, _scanRunId, ProfiledRevision, fixtureRoot);
        IRevisionBoundSourceReader reader = CreateReader(lease, bindingToken);
        AnalysisScope scope = CreateScope(_repositoryId, _scanRunId, ProfiledRevision, bindingToken);

        SourceExcerptResult success = await reader.ReadAsync(
            scope,
            new RevisionBoundSourceRequest(
                SourceRelativePath,
                new AnalysisLineRange(5, 9),
                SourceSha256,
                byteLimit: 4_096),
            CancellationToken.None);

        Assert.Equal(AnalysisOperationStatus.Succeeded, success.Status);
        Assert.Null(success.Failure);
        Assert.Equal(_repositoryId, success.RepositoryId);
        Assert.Equal(ProfiledRevision, success.ProfiledRevision);
        Assert.Equal(SourceRelativePath, success.RelativePath);
        Assert.Equal(5, success.Range.StartLine);
        Assert.Equal(9, success.Range.EndLine);
        Assert.Equal(SourceSha256, success.SourceSha256);
        Assert.Equal(ExpectedExcerpt, success.Excerpt);
        Assert.Equal(Encoding.UTF8.GetByteCount(ExpectedExcerpt), success.ByteCount);
        Assert.DoesNotContain(Path.GetFullPath(fixtureRoot), success.Excerpt, StringComparison.OrdinalIgnoreCase);

        SourceExcerptResult tooSmall = await reader.ReadAsync(
            scope,
            new RevisionBoundSourceRequest(
                SourceRelativePath,
                new AnalysisLineRange(5, 9),
                SourceSha256,
                byteLimit: Encoding.UTF8.GetByteCount(ExpectedExcerpt) - 1),
            CancellationToken.None);

        AssertRejected(tooSmall, AnalysisPortFailureReason.Rejected);

        SourceExcerptResult staleRevision = await reader.ReadAsync(
            CreateScope(_repositoryId, _scanRunId, "fedcba9876543210fedcba9876543210fedcba98", bindingToken),
            new RevisionBoundSourceRequest(
                SourceRelativePath,
                new AnalysisLineRange(5, 9),
                SourceSha256,
                byteLimit: 4_096),
            CancellationToken.None);

        AssertRejected(staleRevision, AnalysisPortFailureReason.IntegrityMismatch);

        SourceExcerptResult otherRepository = await reader.ReadAsync(
            CreateScope(
                Guid.Parse("12000000-0000-4000-8000-000000000099"),
                _scanRunId,
                ProfiledRevision,
                bindingToken),
            new RevisionBoundSourceRequest(
                SourceRelativePath,
                new AnalysisLineRange(5, 9),
                SourceSha256,
                byteLimit: 4_096),
            CancellationToken.None);

        AssertRejected(otherRepository, AnalysisPortFailureReason.Rejected);

        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        SourceExcerptResult cancelled = await reader.ReadAsync(
            scope,
            new RevisionBoundSourceRequest(
                SourceRelativePath,
                new AnalysisLineRange(5, 9),
                SourceSha256,
                byteLimit: 4_096),
            cancellation.Token);

        AssertRejected(cancelled, AnalysisPortFailureReason.Cancelled);

        SourceExcerptResult missing = await reader.ReadAsync(
            scope,
            new RevisionBoundSourceRequest(
                "src/main/java/example/analysis/NotPresent.java",
                new AnalysisLineRange(1, 1),
                SourceSha256,
                byteLimit: 4_096),
            CancellationToken.None);

        AssertRejected(missing, AnalysisPortFailureReason.Unavailable);
        Assert.NotNull(missing.Failure);
        Assert.InRange(missing.Failure!.Diagnostic.Length, 1, 256);
        Assert.DoesNotContain(fixtureRoot, missing.Failure.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NotPresent.java", missing.Failure.Diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("stderr", missing.Failure.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/", missing.Failure.Diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("\\", missing.Failure.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void RevisionBoundSourceRequest_RejectsUnboundedOrEscapingRangesBeforeThePort()
    {
        Assert.Throws<ArgumentException>(() => new RevisionBoundSourceRequest(
            "../outside.java",
            new AnalysisLineRange(1, 1),
            SourceSha256,
            byteLimit: 4_096));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RevisionBoundSourceRequest(
            SourceRelativePath,
            new AnalysisLineRange(1, 1),
            SourceSha256,
            byteLimit: 1_048_577));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AnalysisLineRange(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AnalysisLineRange(3, 2));
    }

    private static void AssertRejected(
        SourceExcerptResult result,
        AnalysisPortFailureReason reason)
    {
        Assert.Equal(AnalysisOperationStatus.Failed, result.Status);
        Assert.NotNull(result.Failure);
        Assert.Equal(reason, result.Failure!.Reason);
        Assert.InRange(result.Failure.Diagnostic.Length, 1, 256);
        Assert.DoesNotContain(Path.DirectorySeparatorChar, result.Failure.Diagnostic);
        Assert.DoesNotContain(Path.AltDirectorySeparatorChar, result.Failure.Diagnostic);
        Assert.DoesNotContain("stderr", result.Failure.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    private static AnalysisScope CreateScope(
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision,
        string bindingToken) =>
        new(
            repositoryId,
            scanRunId,
            profiledRevision,
            bindingToken,
            new string('a', 64),
            new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds()));

    private static IRevisionBoundSourceReader CreateReader(
        TestSnapshotLease lease,
        string bindingToken)
    {
        Type adapterType = typeof(CommittedRepositorySnapshotService).Assembly
            .GetType("Vulgata.Infrastructure.Analysis.Source.RevisionBoundSourceReader")
            ?? throw new InvalidOperationException(
                "T035 RevisionBoundSourceReader is not present at the accepted Infrastructure seam.");

        object?[] arguments = [lease, bindingToken];
        object instance = Activator.CreateInstance(adapterType, arguments)
            ?? throw new InvalidOperationException(
                "T035 RevisionBoundSourceReader does not expose the accepted lease-bound constructor.");
        return Assert.IsAssignableFrom<IRevisionBoundSourceReader>(instance);
    }

    private static string FindFixtureRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not locate the Vulgata repository root.");
        }

        return Path.GetFullPath(Path.Combine(
            directory.FullName,
            "tests",
            "Vulgata.Tests",
            "Fixtures",
            "AnalysisRuntime",
            "repository"));
    }

    private sealed class TestSnapshotLease(
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision,
        string rootPath) : ICommittedRepositorySnapshotLease
    {
        public Guid RepositoryId { get; } = repositoryId;
        public Guid ScanRunId { get; } = scanRunId;
        public string ProfiledRevision { get; } = profiledRevision;
        public string RootPath { get; } = rootPath;

        public Task VerifyIdentityAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
