using System.Text;
using Vulgata.Core.DomainServices;

using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.PreparedSource;

/// <summary>
/// In-memory authority control for direct prepared-source tests. It deliberately
/// contains no filesystem root or persistence access.
/// </summary>
internal sealed class ControlledPreparedSourceAuthority : IPreparedSourceCurrentAuthorityVerifier
{
    private readonly object _sync = new();
    private PreparedSourceAuthorityStatus _state = PreparedSourceAuthorityStatus.Unavailable;
    private PreparedSourceApproval? _currentApproval;
    private PreparedSourceApproval? _lastRequestedApproval;

    internal void Approve(PreparedSourceApproval approval)
    {
        lock (_sync)
        {
            _currentApproval = approval;
            _state = PreparedSourceAuthorityStatus.Current;
        }
    }

    internal void Revoke()
    {
        lock (_sync)
        {
            _state = PreparedSourceAuthorityStatus.Revoked;
        }
    }

    internal void ApproveMissing()
    {
        lock (_sync)
        {
            _currentApproval = null;
            _state = PreparedSourceAuthorityStatus.Current;
        }
    }

    internal void MakeUnavailable()
    {
        lock (_sync)
        {
            _state = PreparedSourceAuthorityStatus.Unavailable;
        }
    }

    public Task<PreparedSourceAuthorityResult> VerifyCurrentAsync(
        PreparedSourceApproval requestedApproval,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            _lastRequestedApproval = requestedApproval;
            if (_state != PreparedSourceAuthorityStatus.Current)
            {
                return Task.FromResult(new PreparedSourceAuthorityResult(_state, null));
            }

            if (_currentApproval is null)
            {
                return Task.FromResult(new PreparedSourceAuthorityResult(
                    PreparedSourceAuthorityStatus.Current,
                    null));
            }

            if (_currentApproval != requestedApproval)
            {
                return Task.FromResult(new PreparedSourceAuthorityResult(
                    PreparedSourceAuthorityStatus.Revoked,
                    null));
            }

            return Task.FromResult(new PreparedSourceAuthorityResult(
                PreparedSourceAuthorityStatus.Current,
                _currentApproval));
        }
    }

    internal PreparedSourceApproval? LastRequestedApproval
    {
        get
        {
            lock (_sync)
            {
                return _lastRequestedApproval;
            }
        }
    }
}

/// <summary>
/// Owns a small prepared directory and an adjacent outside-root sentinel for
/// source-tool tests. Disposal removes only this fixture's uniquely named tree.
/// </summary>
internal sealed class PreparedSourceFixture : IAsyncDisposable
{
    private const string DirectoryPrefix = "vulgata-prepared-source-";
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _sandboxPath;
    private bool _disposed;

    private PreparedSourceFixture(string sandboxPath, Guid sandboxId)
    {
        _sandboxPath = sandboxPath;
        SandboxId = sandboxId;
        PreparedRootPath = Path.Combine(sandboxPath, "prepared");
        SiblingPath = Path.Combine(sandboxPath, "outside");
        SentinelPath = Path.Combine(SiblingPath, "sentinel.txt");

        Authority = new ControlledPreparedSourceAuthority();
        Approval = new PreparedSourceApproval(
            BatchId: Guid.NewGuid(),
            RepositoryId: Guid.NewGuid(),
            SubjectId: "prepared-source-test-subject",
            Purpose: PreparedSourcePurpose.WorkerSourceRead,
            ApprovalIdentity: $"approval-{sandboxId:N}",
            RootBindingIdentity: $"root-binding-{sandboxId:N}",
            OperatorLabel: "operator supplied fixture label");
    }

    internal Guid SandboxId { get; }

    internal string SandboxPath => _sandboxPath;

    internal string PreparedRootPath { get; }

    internal string SiblingPath { get; }

    internal string SentinelPath { get; }

    internal ControlledPreparedSourceAuthority Authority { get; }

    internal PreparedSourceApproval Approval { get; }

    internal string UnfamiliarTextPath => Path.Combine(PreparedRootPath, "notes.oddlangx");

    internal string LongTextPath => Path.Combine(PreparedRootPath, "long.payloadx");

    internal string EmptyTextPath => Path.Combine(PreparedRootPath, "empty.datax");

    internal string InvalidUtf8Path => Path.Combine(PreparedRootPath, "invalid.bytesx");

    internal static async Task<PreparedSourceFixture> CreateAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Guid sandboxId = Guid.NewGuid();
        string sandboxPath = Path.Combine(
            Path.GetFullPath(Path.GetTempPath()),
            DirectoryPrefix + sandboxId.ToString("N"));
        var fixture = new PreparedSourceFixture(sandboxPath, sandboxId);

        try
        {
            Directory.CreateDirectory(fixture.PreparedRootPath);
            Directory.CreateDirectory(fixture.SiblingPath);

            await File.WriteAllTextAsync(
                fixture.UnfamiliarTextPath,
                "café\nsource fixture\n",
                StrictUtf8,
                cancellationToken);
            await File.WriteAllBytesAsync(
                fixture.LongTextPath,
                StrictUtf8.GetBytes(new string('L', 70_000)),
                cancellationToken);
            await File.WriteAllBytesAsync(
                fixture.EmptyTextPath,
                [],
                cancellationToken);
            await File.WriteAllBytesAsync(
                fixture.InvalidUtf8Path,
                [0x66, 0x80, 0x6F],
                cancellationToken);
            await File.WriteAllTextAsync(
                fixture.SentinelPath,
                "outside the approved prepared root",
                StrictUtf8,
                cancellationToken);

            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    internal bool OwnsPath(string path)
    {
        string candidate = Path.GetFullPath(path);
        string sandbox = Path.GetFullPath(_sandboxPath);
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(candidate, sandbox, comparison) ||
            candidate.StartsWith(
                sandbox.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar,
                comparison);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        string expectedPath = Path.Combine(
            Path.GetFullPath(Path.GetTempPath()),
            DirectoryPrefix + SandboxId.ToString("N"));
        if (!string.Equals(
                Path.GetFullPath(_sandboxPath),
                expectedPath,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Prepared-source fixture cleanup target is not owned.");
        }

        if (Directory.Exists(_sandboxPath))
        {
            DeleteOwnedTreeWithoutFollowingLinks(_sandboxPath);
        }

        _disposed = true;
        return ValueTask.CompletedTask;
    }

    private static void DeleteOwnedTreeWithoutFollowingLinks(string directoryPath)
    {
        if ((File.GetAttributes(directoryPath) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("Prepared-source fixture cleanup encountered a reparse root.");
        }

        foreach (string entryPath in Directory.EnumerateFileSystemEntries(directoryPath))
        {
            FileAttributes attributes = File.GetAttributes(entryPath);
            bool isDirectory = (attributes & FileAttributes.Directory) != 0;
            bool isReparsePoint = (attributes & FileAttributes.ReparsePoint) != 0;

            if (isDirectory && !isReparsePoint)
            {
                DeleteOwnedTreeWithoutFollowingLinks(entryPath);
            }
            else if (isDirectory)
            {
                Directory.Delete(entryPath);
            }
            else
            {
                File.Delete(entryPath);
            }
        }

        Directory.Delete(directoryPath);
    }
}

public sealed class PreparedSourceFixtureTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CreateAsync_OwnsPreparedAndSiblingTrees_DisposalRemovesOnlyOwnedTree()
    {
        PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        string sandboxPath = fixture.SandboxPath;
        string preparedRootPath = fixture.PreparedRootPath;
        string siblingPath = fixture.SiblingPath;
        string sentinelPath = fixture.SentinelPath;
        string unrelatedPath = Path.GetFullPath(Path.GetTempPath());

        try
        {
            Assert.True(fixture.OwnsPath(preparedRootPath));
            Assert.True(fixture.OwnsPath(siblingPath));
            Assert.False(fixture.OwnsPath(unrelatedPath));
            Assert.Equal("café\nsource fixture\n", await File.ReadAllTextAsync(fixture.UnfamiliarTextPath));
            Assert.Equal(70_000, (await File.ReadAllBytesAsync(fixture.LongTextPath)).Length);
            Assert.Empty(await File.ReadAllBytesAsync(fixture.EmptyTextPath));
            byte[] invalidUtf8Bytes = await File.ReadAllBytesAsync(fixture.InvalidUtf8Path);
            Assert.Equal<byte>([0x66, 0x80, 0x6F], invalidUtf8Bytes);
            Assert.Equal("outside the approved prepared root", await File.ReadAllTextAsync(sentinelPath));

            fixture.Authority.Approve(fixture.Approval);
            PreparedSourceAuthorityResult approved = await fixture.Authority.VerifyCurrentAsync(fixture.Approval);
            PreparedSourceAuthorityResult foreign = await fixture.Authority.VerifyCurrentAsync(
                fixture.Approval with { RootBindingIdentity = "another-root-binding" });
            fixture.Authority.Revoke();
            PreparedSourceAuthorityResult revoked = await fixture.Authority.VerifyCurrentAsync(fixture.Approval);
            fixture.Authority.MakeUnavailable();
            PreparedSourceAuthorityResult unavailable = await fixture.Authority.VerifyCurrentAsync(fixture.Approval);

            Assert.Equal(PreparedSourceAuthorityStatus.Current, approved.Status);
            Assert.Equal(PreparedSourceAuthorityStatus.Revoked, foreign.Status);
            Assert.Equal(PreparedSourceAuthorityStatus.Revoked, revoked.Status);
            Assert.Equal(PreparedSourceAuthorityStatus.Unavailable, unavailable.Status);
        }
        finally
        {
            await fixture.DisposeAsync();
        }

        Assert.False(Directory.Exists(sandboxPath));
        Assert.False(File.Exists(sentinelPath));
    }
}
