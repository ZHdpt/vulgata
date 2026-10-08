using System.Collections.Immutable;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Web.Data;

namespace Vulgata.Tests.October;

public sealed class OctoberOperatorReaderBindingTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ControlledBinding_ConcurrentOpenSharesOriginalTicketAndOnlySuccessIsCachedAsync(bool success)
    {
        var subject = new KnowledgeSubject("controlled-subject", "controlled-original-stamp");
        var selection = new ReadSelectionRequest(new(new("controlled-db", "target", Batch, new string('a', 64)), Guid.NewGuid()),
            subject, KnowledgeMode.IT, KnowledgePurpose.Reader, [Guid.NewGuid()]);
        var reader = new ControlledReader();
        await using var binding = new OctoberOperatorReaderBinding(subject, selection, new ControlledResources(), _ => reader);
        var first = binding.Reader.OpenReadSelectionAsync(selection);
        var second = binding.Reader.OpenReadSelectionAsync(selection);
        Assert.Equal(1, reader.Calls);
        var result = success ? new ReadSelectionResult(KnowledgeReadStatus.Success, new("controlled-original-ticket"))
            : new ReadSelectionResult(KnowledgeReadStatus.Unavailable, null);
        reader.Completion.SetResult(result);
        Assert.Same(result, await first);
        Assert.Same(result, await second);
        Assert.Same(result, await binding.Reader.OpenReadSelectionAsync(selection));
        Assert.Equal(success ? 1 : 2, reader.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("same")]
    [InlineData("purpose")]
    [InlineData("scope")]
    public async Task ControlledBinding_ValidOtherIssuerTicketCannotBypassBoundReadsAsync(string variant)
    {
        var subject = new KnowledgeSubject("ordinary-user", "controlled-original-stamp");
        var selection = new ReadSelectionRequest(new(new("controlled-db", "target", Batch, new string('a', 64)), Guid.NewGuid()),
            subject, KnowledgeMode.IT, KnowledgePurpose.Reader, [Guid.NewGuid()]);
        var source = new B0bPublishedSource(new(selection.Publication, [], [], [], []));
        var protector = new OctoberKnowledgeDataProtectionTokenProtector(new EphemeralDataProtectionProvider());
        OctoberKnowledgeReader Reader() => new(source, selection.Publication, new B0bReaderDisclosure(),
            new OctoberKnowledgeCursorProtector(protector), new B0bReadLog());
        var inner = new CountingReader(Reader());
        await using var binding = new OctoberOperatorReaderBinding(subject, selection, new ControlledResources(), _ => inner);
        var own = await binding.Reader.OpenReadSelectionAsync(selection);
        Assert.Equal(KnowledgeReadStatus.Success, own.Status);
        var otherReader = Reader();
        var other = await otherReader.OpenReadSelectionAsync(variant switch
        {
            "purpose" => selection with { Purpose = KnowledgePurpose.Chat },
            "scope" => selection with { RepositoryScope = [] },
            _ => selection
        });
        Assert.Equal(KnowledgeReadStatus.Success, other.Status);
        // Real owner-issued, authenticated token under the SAME protector, not malformed input.
        Assert.Equal(KnowledgeReadStatus.Success, (await otherReader.SearchAsync(new(other.Selection!, ""))).Status);
        Assert.Equal(KnowledgeReadStatus.Success, (await binding.Reader.SearchAsync(new(own.Selection!, ""))).Status);
        int calls = inner.Calls;
        var handle = new DocumentHandle(Batch, selection.Publication.PublicationId, Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(KnowledgeReadStatus.Unavailable, (await binding.Reader.SearchAsync(new(other.Selection!, ""))).Status);
        Assert.Equal(KnowledgeReadStatus.Unavailable, (await binding.Reader.ReadExactAsync(new(other.Selection!, handle))).Status);
        Assert.Equal(KnowledgeReadStatus.Unavailable, (await binding.Reader.GetRelatedAsync(new(other.Selection!, handle))).Status);
        Assert.Equal(calls, inner.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ControlledBinding_DisposalWaitsActualReadAndRefusesLaterCallsAsync()
    {
        var subject = new KnowledgeSubject("controlled-subject", "controlled-original-stamp");
        var selection = new ReadSelectionRequest(new(new("controlled-db", "target", Batch, new string('a', 64)), Guid.NewGuid()),
            subject, KnowledgeMode.IT, KnowledgePurpose.Reader, [Guid.NewGuid()]);
        var resources = new ControlledResources();
        var reader = new ControlledReader();
        // This constructor probe qualifies ownership only, not genuine session issuance.
        var binding = new OctoberOperatorReaderBinding(subject, selection, resources, _ => reader);
        var read = binding.Reader.OpenReadSelectionAsync(selection);
        var disposing = binding.DisposeAsync().AsTask();
        try
        {
            Assert.False(disposing.IsCompleted);
            Assert.False(resources.Disposed);
            Assert.Equal(KnowledgeReadStatus.Unavailable, (await binding.Reader.OpenReadSelectionAsync(selection)).Status);
            Assert.Equal(1, reader.Calls);
            Assert.Equal("{}", System.Text.Json.JsonSerializer.Serialize(binding));
            Assert.DoesNotContain(subject.AuthenticationBinding, binding.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            reader.Completion.TrySetResult(new(KnowledgeReadStatus.Unavailable, null));
            await read;
            await disposing.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.True(resources.Disposed);
        await binding.DisposeAsync();
        Assert.Equal(1, resources.Disposals);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ControlledBinding_ForeignPurposeOrScopeCannotOpenReadAsync()
    {
        var subject = new KnowledgeSubject("controlled-subject", "controlled-original-stamp");
        var selection = new ReadSelectionRequest(new(new("controlled-db", "target", Batch, new string('a', 64)), Guid.NewGuid()),
            subject, KnowledgeMode.IT, KnowledgePurpose.Reader, [Guid.NewGuid()]);
        var reader = new ControlledReader();
        await using var binding = new OctoberOperatorReaderBinding(subject, selection, new ControlledResources(), _ => reader);
        Assert.Equal(KnowledgeReadStatus.Unavailable, (await binding.Reader.OpenReadSelectionAsync(selection with { Purpose = KnowledgePurpose.Chat })).Status);
        Assert.Equal(KnowledgeReadStatus.Unavailable, (await binding.Reader.OpenReadSelectionAsync(selection with { RepositoryScope = [] })).Status);
        Assert.Equal(0, reader.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Registration_ConcreteAndInterfaceUseTheSameScopedIssuer()
    {
        using var services = Services();
        using var first = services.CreateScope();
        using var second = services.CreateScope();
        var issuer = first.ServiceProvider.GetRequiredService<OctoberLocalAuthority>();
        Assert.Same(issuer, first.ServiceProvider.GetRequiredService<IOctoberLocalAuthority>());
        Assert.NotSame(issuer, second.ServiceProvider.GetRequiredService<OctoberLocalAuthority>());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BindReader_CallerConstructedSessionCannotBecomeAuthenticatedReaderAsync()
    {
        using var services = Services();
        using var scope = services.CreateScope();
        var authority = scope.ServiceProvider.GetRequiredService<IOctoberLocalAuthority>();
        var publication = new PublicationBinding(new("postgres://localhost:1/owned", "owned-target", Batch,
            new string('a', 64)), Guid.NewGuid());
        var binding = await ((OctoberLocalAuthority)authority).TryBindReaderAsync(
            new UntrustedSession(), publication, CancellationToken.None);
        Assert.Null(binding);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BindReader_PreCancelledCannotDiscoverCredentialsOrContactDatabaseAsync()
    {
        using var services = Services();
        using var scope = services.CreateScope();
        var authority = (OctoberLocalAuthority)scope.ServiceProvider.GetRequiredService<IOctoberLocalAuthority>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => authority.TryBindReaderAsync(
            new UntrustedSession(), new(new("postgres://localhost:1/owned", "owned-target", Batch,
                new string('a', 64)), Guid.NewGuid()), new CancellationToken(true)));
    }

    private static readonly Guid Batch = Guid.Parse("e88da2aa-63ef-454b-945f-fa10ed39b02d");

    private static ServiceProvider Services()
    {
        const string database = "postgres://localhost:1/owned";
        const string connection = "Host=localhost;Port=1;Database=owned;Username=unused;Password=unused";
        var configuration = new OctoberAuthorityConfiguration("owned-authority", "owned-target", Batch,
            database, connection, connection, database,
            ImmutableDictionary<string, string>.Empty.Add("owned-target", database),
            TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));
        var services = new ServiceCollection();
        OctoberHeadlessAuthorityRegistration.AddOctoberLocalAuthority(services, configuration,
            new ForbiddenCredentials(), TimeProvider.System);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class ForbiddenCredentials : IOctoberOperatorCredentialSource
    {
        public ValueTask<OctoberOperatorCredentials?> ReadAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("This self-contained refusal probe must not read credentials.");
    }

    private sealed class UntrustedSession : IOctoberOperatorSession
    {
        public string SubjectId => "not-authenticated";
        public string ManagementAuthorityId => "owned-authority";
        public DateTimeOffset ExpiresAtUtc => DateTimeOffset.MaxValue;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ControlledResources : IAsyncDisposable
    {
        public int Disposals { get; private set; }
        public bool Disposed => Disposals != 0;
        public ValueTask DisposeAsync() { Disposals++; return ValueTask.CompletedTask; }
    }

    private sealed class ControlledReader : IOctoberKnowledgeReader
    {
        public int Calls { get; private set; }
        public TaskCompletionSource<ReadSelectionResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<ReadSelectionResult> OpenReadSelectionAsync(ReadSelectionRequest request, CancellationToken cancellationToken = default)
        { Calls++; return Completion.Task; }
        public Task<SearchPage> SearchAsync(KnowledgeSearchRequest request, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<ContentPage> ReadExactAsync(ExactReadRequest request, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<RelatedPage> GetRelatedAsync(RelatedReadRequest request, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }

    private sealed class CountingReader(IOctoberKnowledgeReader inner) : IOctoberKnowledgeReader
    {
        public int Calls { get; private set; }
        public Task<ReadSelectionResult> OpenReadSelectionAsync(ReadSelectionRequest request, CancellationToken cancellationToken = default)
        { Calls++; return inner.OpenReadSelectionAsync(request, cancellationToken); }
        public Task<SearchPage> SearchAsync(KnowledgeSearchRequest request, CancellationToken cancellationToken = default)
        { Calls++; return inner.SearchAsync(request, cancellationToken); }
        public Task<ContentPage> ReadExactAsync(ExactReadRequest request, CancellationToken cancellationToken = default)
        { Calls++; return inner.ReadExactAsync(request, cancellationToken); }
        public Task<RelatedPage> GetRelatedAsync(RelatedReadRequest request, CancellationToken cancellationToken = default)
        { Calls++; return inner.GetRelatedAsync(request, cancellationToken); }
    }
}
