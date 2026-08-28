using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests;

/// <summary>
/// T031 public-seam RED vectors for the server-owned applied advisory Context
/// reader.  These tests deliberately do not call ContextManagement internals,
/// inspect private members, or provide caller-owned advisory text.
/// </summary>
public sealed class ChatAdvisoryContextReaderTests
{
    [Fact]
    public async Task ReadAsync_AuthorizedScope_ComposesAppliedGlobalSystemAndExactlyTwoRepositoriesAsync()
    {
        await using AdvisoryContextFixture fixture = await AdvisoryContextFixture.CreateAsync();

        ChatAdvisoryContextSnapshot snapshot = await fixture.ReadSnapshotAsync();

        Assert.Contains("全局术语", snapshot.ComposedText, StringComparison.Ordinal);
        Assert.Contains("系统术语", snapshot.ComposedText, StringComparison.Ordinal);
        Assert.Contains("甲仓库术语", snapshot.ComposedText, StringComparison.Ordinal);
        Assert.Contains("乙仓库术语", snapshot.ComposedText, StringComparison.Ordinal);
        Assert.True(snapshot.ComposedText.IndexOf("全局术语", StringComparison.Ordinal) <
            snapshot.ComposedText.IndexOf("系统术语", StringComparison.Ordinal));
        Assert.Equal(4, snapshot.SourceUnits.Count);
    }

    [Fact]
    public async Task ReadAsync_AuthorizedScope_OrdersRepositoryUnitsByRfc4122IdRegardlessOfRequestOrderAsync()
    {
        await using AdvisoryContextFixture fixture = await AdvisoryContextFixture.CreateAsync();

        ChatAdvisoryContextSnapshot first = await fixture.ReadSnapshotAsync(
            repositoryOrder: [fixture.RepositoryA.Id, fixture.RepositoryB.Id]);
        ChatAdvisoryContextSnapshot second = await fixture.ReadSnapshotAsync(
            repositoryOrder: [fixture.RepositoryB.Id, fixture.RepositoryA.Id]);

        Assert.Equal(first.ComposedText, second.ComposedText);
        Assert.Equal(first.AuthorityFingerprint, second.AuthorityFingerprint);
        Assert.Equal(first.Terms, second.Terms);
    }

    [Fact]
    public async Task ReadAsync_PendingContextAndCallerText_AreExcludedFromAppliedSnapshotAsync()
    {
        await using AdvisoryContextFixture fixture = await AdvisoryContextFixture.CreateAsync();
        await fixture.AddPendingChangesAsync();

        ChatAdvisoryContextSnapshot snapshot = await fixture.ReadSnapshotAsync();

        Assert.DoesNotContain("待生效术语", snapshot.ComposedText, StringComparison.Ordinal);
        Assert.DoesNotContain("caller-controlled", snapshot.ComposedText, StringComparison.Ordinal);
        Assert.DoesNotContain("调用方注入", snapshot.ComposedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAsync_AuthorizationDenied_ReturnsMissingEquivalentWithoutIdentityAsync()
    {
        await using AdvisoryContextFixture fixture = await AdvisoryContextFixture.CreateAsync();

        ChatAdvisoryContextReadResult result = await fixture.ReadAsync(
            new RecordingAuthorizationVerifier(false));

        Assert.Equal(ChatAdvisoryContextReadResultKind.NotFoundOrUnauthorized, result.Kind);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task ReadAsync_ScopeMembershipMismatch_ReturnsMissingEquivalentWithoutIdentityAsync()
    {
        await using AdvisoryContextFixture fixture = await AdvisoryContextFixture.CreateAsync();
        Guid unrelatedRepositoryId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        ChatAdvisoryContextReadResult result = await fixture.ReadAsync(
            new RecordingAuthorizationVerifier(true),
            repositoryOrder: [fixture.RepositoryA.Id, unrelatedRepositoryId]);

        Assert.Equal(ChatAdvisoryContextReadResultKind.NotFoundOrUnauthorized, result.Kind);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task ReadAsync_AllAppliedValuesBlank_ReturnsCanonicalEmptySnapshotAsync()
    {
        await using AdvisoryContextFixture fixture = await AdvisoryContextFixture.CreateAsync(
            globalContext: " ",
            systemContext: "\r\n",
            repositoryAContext: "\t",
            repositoryBContext: null);

        ChatAdvisoryContextSnapshot snapshot = await fixture.ReadSnapshotAsync();

        Assert.Equal(string.Empty, snapshot.ComposedText);
        Assert.Empty(snapshot.Terms);
        Assert.Equal(0, snapshot.EncodedUtf8Bytes);
        Assert.Matches("^[0-9a-f]{64}$", snapshot.AuthorityFingerprint);
    }

    [Fact]
    public async Task ReadAsync_NormalizesNfcLineEndingsAndOuterWhitespaceBeforeFingerprintingAsync()
    {
        await using AdvisoryContextFixture fixture = await AdvisoryContextFixture.CreateAsync(
            globalContext: "  cafe\u0301\r\n",
            systemContext: " 系统术语 \r",
            repositoryAContext: "甲仓库术语",
            repositoryBContext: "乙仓库术语");

        ChatAdvisoryContextSnapshot snapshot = await fixture.ReadSnapshotAsync();

        Assert.Contains("café", snapshot.ComposedText, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", snapshot.ComposedText, StringComparison.Ordinal);
        Assert.DoesNotContain("  ", snapshot.ComposedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAsync_NonmatchingAppliedContext_RemainsValidButContributesNoCallerTermsAsync()
    {
        await using AdvisoryContextFixture fixture = await AdvisoryContextFixture.CreateAsync(
            globalContext: "与问题无关的全局说明",
            systemContext: "与问题无关的系统说明",
            repositoryAContext: "与问题无关的甲仓库说明",
            repositoryBContext: "与问题无关的乙仓库说明");

        ChatAdvisoryContextSnapshot snapshot = await fixture.ReadSnapshotAsync();

        Assert.NotEmpty(snapshot.ComposedText);
        Assert.DoesNotContain("caller", snapshot.Terms, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("question", snapshot.Terms, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadAsync_UsedContextMutation_ChangesAuthorityFingerprintAfterRefetchAsync()
    {
        await using AdvisoryContextFixture fixture = await AdvisoryContextFixture.CreateAsync();
        ChatAdvisoryContextSnapshot before = await fixture.ReadSnapshotAsync();

        await fixture.UpdateRepositoryContextAsync("改写后的甲仓库术语");
        ChatAdvisoryContextSnapshot after = await fixture.ReadSnapshotAsync();

        Assert.NotEqual(before.AuthorityFingerprint, after.AuthorityFingerprint);
        Assert.NotEqual(before.ComposedText, after.ComposedText);
    }

    [Fact]
    public async Task ReadAsync_Exactly8192Utf8Bytes_SucceedsWithoutTruncationAsync()
    {
        await using AdvisoryContextFixture fixture = await AdvisoryContextFixture.CreateAsync(
            globalContext: new string('a', 8_192),
            systemContext: null,
            repositoryAContext: null,
            repositoryBContext: null);

        GlobalContext storedGlobal = await fixture.Context.GlobalContexts.SingleAsync();
        Assert.Equal(8_192, storedGlobal.Context.Length);

        ChatAdvisoryContextSnapshot snapshot = await fixture.ReadSnapshotAsync(
            repositoryOrder: [fixture.RepositoryA.Id, fixture.RepositoryB.Id]);

        Assert.Equal(8_192, snapshot.EncodedUtf8Bytes);
        Assert.Equal(new string('a', 8_192), snapshot.ComposedText);
    }

    [Fact]
    public async Task ReadAsync_8193Utf8Bytes_ReturnsInconsistentCurrentEvidenceWithoutTruncationAsync()
    {
        await using AdvisoryContextFixture fixture = await AdvisoryContextFixture.CreateAsync(
            globalContext: new string('a', 8_193),
            systemContext: null,
            repositoryAContext: null,
            repositoryBContext: null);

        ChatAdvisoryContextReadResult result = await fixture.ReadAsync(
            new RecordingAuthorizationVerifier(true),
            repositoryOrder: [fixture.RepositoryA.Id, fixture.RepositoryB.Id]);

        Assert.Equal(ChatAdvisoryContextReadResultKind.InconsistentCurrentEvidence, result.Kind);
        Assert.Null(result.Snapshot);
    }

    private sealed class RecordingAuthorizationVerifier(params bool[] decisions) :
        IRepositoryAuthorizationVerifier
    {
        private int _call;

        public ValueTask<bool> HasCurrentAccessAsync(
            IReadOnlyCollection<Guid> requiredRepositoryIds,
            CancellationToken cancellationToken = default)
        {
            int index = Math.Min(_call++, decisions.Length - 1);
            return ValueTask.FromResult(decisions.Length > 0 && decisions[index]);
        }
    }

    private sealed class AdvisoryContextFixture(
        SqliteKnowledgeStoreSession session,
        Vulgata.Core.Entities.System system,
        Repository repositoryA,
        Repository repositoryB) : IAsyncDisposable
    {
        private static readonly DateTimeOffset _now =
            new(2026, 8, 28, 0, 0, 0, TimeSpan.Zero);

        internal VulgataDbContext Context { get; } = session.Context;
        internal Vulgata.Core.Entities.System System { get; } = system;
        internal Repository RepositoryA { get; } = repositoryA;
        internal Repository RepositoryB { get; } = repositoryB;

        internal static async Task<AdvisoryContextFixture> CreateAsync(
            string? globalContext = "全局术语",
            string? systemContext = "系统术语",
            string? repositoryAContext = "甲仓库术语",
            string? repositoryBContext = "乙仓库术语")
        {
            SqliteKnowledgeStoreSession session =
                await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
            var system = new Vulgata.Core.Entities.System(
                "测试系统", null, systemContext, _now);
            Repository repositoryA = system.AddRepository(
                "甲仓库", "https://example.test/a.git", null, repositoryAContext, _now);
            Repository repositoryB = system.AddRepository(
                "乙仓库", "https://example.test/b.git", null, repositoryBContext, _now);
            session.Context.Systems.Add(system);
            session.Context.GlobalContexts.Add(new GlobalContext(globalContext ?? string.Empty, _now));
            await session.Context.SaveChangesAsync();
            session.Context.ChangeTracker.Clear();
            return new AdvisoryContextFixture(session, system, repositoryA, repositoryB);
        }

        internal async Task AddPendingChangesAsync()
        {
            Context.PendingContextChanges.AddRange(
                new PendingContextChange(ContextScopeType.Global, "global", "待生效术语", _now),
                new PendingContextChange(ContextScopeType.System, System.Id.ToString(), "调用方注入", _now),
                new PendingContextChange(ContextScopeType.Repository, RepositoryA.Id.ToString(), "caller-controlled", _now));
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        internal async Task UpdateRepositoryContextAsync(string value)
        {
            Repository repository = await Context.Repositories.SingleAsync(
                candidate => candidate.Id == RepositoryA.Id);
            repository.UpdateDetails(repository.Name, repository.GitUrl, repository.Description, value, _now.AddMinutes(1));
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        internal async Task<ChatAdvisoryContextReadResult> ReadAsync(
            IRepositoryAuthorizationVerifier verifier,
            IReadOnlyCollection<Guid>? repositoryOrder = null)
        {
            var scope = new AuthorizedChatScope(
                System.Id,
                repositoryOrder ?? [RepositoryA.Id, RepositoryB.Id],
                new RepositoryAccessScope(
                    repositoryOrder ?? [RepositoryA.Id, RepositoryB.Id],
                    verifier));
            return await new ChatAdvisoryContextReader(Context).ReadAsync(scope);
        }

        internal async Task<ChatAdvisoryContextSnapshot> ReadSnapshotAsync(
            IReadOnlyCollection<Guid>? repositoryOrder = null)
        {
            ChatAdvisoryContextReadResult result = await ReadAsync(
                new RecordingAuthorizationVerifier(true),
                repositoryOrder);
            Assert.Equal(ChatAdvisoryContextReadResultKind.Succeeded, result.Kind);
            return Assert.IsType<ChatAdvisoryContextSnapshot>(result.Snapshot);
        }

        public async ValueTask DisposeAsync() => await session.DisposeAsync();
    }
}
