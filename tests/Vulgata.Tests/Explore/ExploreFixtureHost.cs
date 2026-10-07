using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Web.Explore;
using Vulgata.Web.ViewModels.Explore;

namespace Vulgata.Tests.Explore;

/// <summary>In-process presentation composition only. No Web host, store, listener or provider is started.</summary>
internal sealed class ExploreFixtureHost
{
    public OctoberKnowledgeFixture Fixture { get; } = new();
    public ControlledDisclosurePolicy Policy { get; } = new();
    public ControlledKnowledgeReader OwnerReader { get; }
    public ExploreReaderProbe Reader { get; }
    public ExploreTestContext Context { get; }
    public ExploreTestAuthentication Authentication { get; } = new();
    public ExplorePresentationService Service { get; }

    public ExploreFixtureHost(KnowledgeMode mode = KnowledgeMode.IT)
    {
        OwnerReader = new(Fixture, Policy);
        Reader = new(OwnerReader);
        Context = new(new(Fixture.Selection(mode), true));
        Service = new(Reader, Context, Authentication);
    }

    public DocumentReaderViewModel CreateReader() => new(Service);
    public DocumentBrowseViewModel CreateBrowse() => new(Service);

    public ContentPage Content(DocumentHandle handle, string content, string? next = null) =>
        new(KnowledgeReadStatus.Success,
            new(handle, DocumentKind.CodeLogic, "ControlledFixture 文档", Fixture.Claims),
            content, Fixture.Claims, next is null ? null : new(next), new(true, ["ControlledFixture 部分覆盖"]));

    public static ContentPage Failure(KnowledgeReadStatus status) => new(status, null, null, [], null, null);
}

internal sealed class ExploreTestAuthentication : AuthenticationStateProvider
{
    public ClaimsPrincipal User { get; set; } = new(new ClaimsIdentity(
        [new(ClaimTypes.NameIdentifier, "ordinary-user"), new(ClaimTypes.Role, "User")], "ControlledFixture"));

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(User));
    public void NotifyChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}

internal sealed class ExploreTestContext(ExploreContext? current) : IExploreContextProvider
{
    public event Action? ContextChanged;
    public ExploreContext? Current { get; set; } = current;
    public Task<ExploreContext?> GetCurrentAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);
    public void NotifyChanged() => ContextChanged?.Invoke();
}

/// <summary>
/// Records calls to the imported reader. Optional scripted responses exercise presentation transport limits/races only;
/// they do not implement Knowledge authorization, persistence, splitting or protected cursor semantics.
/// </summary>
internal sealed class ExploreReaderProbe(IOctoberKnowledgeReader inner) : IOctoberKnowledgeReader
{
    public List<ReadSelectionRequest> Selections { get; } = [];
    public List<ExactReadRequest> Reads { get; } = [];
    public List<KnowledgeSearchRequest> Searches { get; } = [];
    public List<CancellationToken> ReadTokens { get; } = [];
    public Func<ExactReadRequest, CancellationToken, Task<ContentPage>>? ReadResponse { get; set; }

    public Task<ReadSelectionResult> OpenReadSelectionAsync(ReadSelectionRequest request, CancellationToken cancellationToken = default)
    {
        Selections.Add(request);
        return inner.OpenReadSelectionAsync(request, cancellationToken);
    }

    public Task<ContentPage> ReadExactAsync(ExactReadRequest request, CancellationToken cancellationToken = default)
    {
        Reads.Add(request);
        ReadTokens.Add(cancellationToken);
        return ReadResponse is null ? inner.ReadExactAsync(request, cancellationToken) : ReadResponse(request, cancellationToken);
    }

    public Task<SearchPage> SearchAsync(KnowledgeSearchRequest request, CancellationToken cancellationToken = default)
    {
        Searches.Add(request);
        return inner.SearchAsync(request, cancellationToken);
    }

    public Task<RelatedPage> GetRelatedAsync(RelatedReadRequest request, CancellationToken cancellationToken = default) =>
        inner.GetRelatedAsync(request, cancellationToken);
}
