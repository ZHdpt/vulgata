using System.Runtime.CompilerServices;
using System.Text;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulgata.Agents.ChatExecution.October;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Explore;
using Vulgata.Web.Services.OctoberChat;
using Vulgata.Web.ViewModels.OctoberChat;

namespace Vulgata.Tests.October.Integration;

public sealed class OctoberChatHostBindingTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BusinessSynthesisPrompt_ExactEmbeddedResourceOccursOnceAndMatchesSourceAsync()
    {
        const string logicalName = "Vulgata.Agents.Prompts.OctoberBusiness.Synthesis.md";
        var assembly = typeof(OctoberChatPromptCatalog).Assembly;
        Assert.Single(assembly.GetManifestResourceNames().Where(name => name == logicalName));
        using var resource = assembly.GetManifestResourceStream(logicalName);
        Assert.NotNull(resource);
        using var reader = new StreamReader(resource, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false);
        string actual = await reader.ReadToEndAsync();
        string expected = await File.ReadAllTextAsync(PromptPath(Path.Combine("OctoberBusiness", "Synthesis.md")), new UTF8Encoding(false, true));
        Assert.False(string.IsNullOrWhiteSpace(expected));
        Assert.Equal(expected, actual);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(KnowledgeMode.Business, "Vulgata.Agents.Prompts.OctoberChatBusiness.txt", "OctoberChatBusiness.txt")]
    [InlineData(KnowledgeMode.IT, "Vulgata.Agents.Prompts.OctoberChatIT.txt", "OctoberChatIT.txt")]
    public async Task DefaultPromptCatalog_ExactEmbeddedModeResourceOccursOnceAndMatchesSourceAsync(
        KnowledgeMode mode, string logicalName, string fileName)
    {
        var assembly = typeof(OctoberChatPromptCatalog).Assembly;
        Assert.Single(assembly.GetManifestResourceNames().Where(name => name == logicalName));
        var catalog = new OctoberChatPromptCatalog(); // Default assembly path, not an injected resource fixture.
        string expected = await File.ReadAllTextAsync(PromptPath(fileName), new UTF8Encoding(false, true));
        string? actual = await catalog.LoadAsync(mode);
        Assert.False(string.IsNullOrWhiteSpace(expected));
        Assert.Equal(expected, actual);
        KnowledgeMode otherMode = mode == KnowledgeMode.Business ? KnowledgeMode.IT : KnowledgeMode.Business;
        Assert.NotEqual(actual, await catalog.LoadAsync(otherMode));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DefaultPromptCatalog_UnknownMode_DoesNotFallBackAsync() =>
        Assert.Null(await new OctoberChatPromptCatalog().LoadAsync((KnowledgeMode)int.MaxValue));

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChatContext_AbsentOrInvalidSettings_IsUnavailableWithoutConnectionAsync(bool invalid)
    {
        var config = invalid ? Configuration() : new ConfigurationBuilder().Build();
        if (invalid) config["October:Wiki:PublicationId"] = "invalid";
        var services = OctoberWikiCompositionTests.Services(new IntegrationAuthentication());
        int connections = 0;
        services.AddOctoberWikiReader(config, _ => { connections++; throw new InvalidOperationException(); });
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        var scoped = scope.ServiceProvider;
        Assert.Null(await scoped.GetRequiredService<IOctoberChatContextProvider>().GetCurrentAsync());
        var selectionService = scoped.GetRequiredService<IOctoberChatSelectionService>();
        Assert.NotNull(scoped.GetRequiredService<OctoberChatEntryViewModel>());
        var supplied = new ReadSelectionRequest(new(new("unavailable", "unavailable", Guid.NewGuid(), new string('a', 64)), Guid.NewGuid()),
            new("ordinary-user", "controlled-stamp"), KnowledgeMode.IT, KnowledgePurpose.Chat, []);
        foreach (var request in new ReadSelectionRequest?[] { null, supplied })
        {
            var result = await selectionService.OpenAsync(request);
            Assert.Equal(OctoberChatEntryStatus.Unavailable, result.Status);
            Assert.Null(result.Conversation);
            Assert.Null(result.Catalog);
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => selectionService.OpenAsync(supplied, new CancellationToken(true)));
        Assert.Equal(0, connections);
        OctoberWikiCompositionTests.AssertNoProducer(scoped);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ChatContext_FrozenSelectionUsesChatPurposeWhileExploreKeepsReaderAndRechecksPolicyAsync()
    {
        var config = Configuration();
        var services = OctoberWikiCompositionTests.Services(new IntegrationAuthentication());
        var policy = new RecordingChatPolicy();
        // Constructing the real selection service resolves the reader's EF options, not a connection.
        // This controlled test never calls the reader or opens this inert connection.
        services.AddOctoberWikiReader(config, _ => "Host=unused.invalid;Database=not_opened;Username=fixture;Password=fixture-only");
        services.Replace(ServiceDescriptor.Singleton<IOctoberKnowledgeDisclosurePolicy>(policy));
        await using var provider = services.BuildServiceProvider();
        var chat = provider.GetRequiredService<IOctoberChatContextProvider>();
        var first = Assert.IsType<ReadSelectionRequest>(await chat.GetCurrentAsync());
        Assert.Equal(KnowledgePurpose.Chat, first.Purpose);
        config["October:Wiki:PublicationId"] = Guid.NewGuid().ToString();
        config["October:Wiki:Mode"] = "Business";
        config["October:Wiki:RepositoryIds:0"] = Guid.NewGuid().ToString();
        Assert.Equal(first, await chat.GetCurrentAsync());
        Assert.Equal(2, policy.Requests.Count);
        Assert.All(policy.Requests, request => { Assert.Equal(KnowledgePurpose.Chat, request.Selection.Purpose); Assert.Empty(request.AllContributors); Assert.Null(request.Kind); });
        var reader = Assert.IsType<ExploreContext>(await provider.GetRequiredService<IExploreContextProvider>().GetCurrentAsync());
        Assert.Equal(KnowledgePurpose.Reader, reader.Selection.Purpose);
        Assert.Equal(first.Publication, reader.Selection.Publication);
        Assert.Equal(first.Mode, reader.Selection.Mode);
        Assert.Equal(first.RepositoryScope, reader.Selection.RepositoryScope);
        policy.Status = DisclosureStatus.Denied;
        Assert.Null(await chat.GetCurrentAsync());
        policy.Throw = true;
        Assert.Null(await chat.GetCurrentAsync());
        Assert.NotNull(provider.GetRequiredService<IOctoberChatSelectionService>());
        OctoberWikiCompositionTests.AssertNoProducer(provider);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("anonymous")]
    [InlineData("missing-stamp")]
    [InlineData("multiple-identities")]
    public async Task ChatContext_InvalidIdentity_CannotObtainSelectionAsync(string variant)
    {
        var identity = new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "ordinary-user")], "Controlled");
        if (variant != "missing-stamp") identity.AddClaim(new("AspNet.Identity.SecurityStamp", "controlled-stamp"));
        var principal = variant == "anonymous" ? new ClaimsPrincipal(new ClaimsIdentity()) : new ClaimsPrincipal(identity);
        if (variant == "multiple-identities") principal.AddIdentity(new ClaimsIdentity(identity.Claims, "Another"));
        var services = OctoberWikiCompositionTests.Services(new IntegrationAuthentication(principal));
        var policy = new RecordingChatPolicy();
        services.AddOctoberWikiReader(Configuration(), _ => throw new InvalidOperationException());
        services.Replace(ServiceDescriptor.Singleton<IOctoberKnowledgeDisclosurePolicy>(policy));
        await using var provider = services.BuildServiceProvider();
        Assert.Null(await provider.GetRequiredService<IOctoberChatContextProvider>().GetCurrentAsync());
        Assert.Empty(policy.Requests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ChatContext_AuthenticationChangesDuringPendingCheck_DiscardsGrantAndDisposalUnsubscribesAsync()
    {
        var auth = new IntegrationAuthentication();
        var services = OctoberWikiCompositionTests.Services(auth);
        var policy = new PendingIntegrationPolicy();
        services.AddOctoberWikiReader(Configuration(), _ => throw new InvalidOperationException());
        services.Replace(ServiceDescriptor.Singleton<IOctoberKnowledgeDisclosurePolicy>(policy));
        await using var provider = services.BuildServiceProvider();
        var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IOctoberChatContextProvider>();
        int changed = 0;
        context.ContextChanged += () => changed++;
        var pending = context.GetCurrentAsync();
        await policy.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        auth.Set(new ClaimsPrincipal(new ClaimsIdentity()));
        Assert.Equal(1, changed);
        policy.Complete.SetResult(DisclosureStatus.Allowed);
        Assert.Null(await pending.WaitAsync(TimeSpan.FromSeconds(10)));
        await scope.DisposeAsync();
        int afterDisposal = changed;
        auth.Set(new ClaimsPrincipal(new ClaimsIdentity()));
        Assert.Equal(afterDisposal, changed);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ChatContext_CallerCancellationDuringPolicyWait_PropagatesAsync()
    {
        var services = OctoberWikiCompositionTests.Services(new IntegrationAuthentication());
        var policy = new PendingIntegrationPolicy();
        services.AddOctoberWikiReader(Configuration(), _ => throw new InvalidOperationException());
        services.Replace(ServiceDescriptor.Singleton<IOctoberKnowledgeDisclosurePolicy>(policy));
        await using var provider = services.BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        var pending = provider.GetRequiredService<IOctoberChatContextProvider>().GetCurrentAsync(cancellation.Token);
        await policy.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    private static IConfigurationRoot Configuration() => OctoberWikiCompositionTests.Configuration(
        new(new("controlled-db", "controlled-target", Guid.NewGuid(), new string('a', 64)), Guid.NewGuid()), Guid.NewGuid(), Path.GetTempPath());

    private sealed class RecordingChatPolicy : IOctoberKnowledgeDisclosurePolicy
    {
        public List<DisclosureRequest> Requests { get; } = [];
        public DisclosureStatus Status { get; set; } = DisclosureStatus.Allowed;
        public bool Throw { get; set; }
        public Task<DisclosureStatus> CheckAsync(DisclosureRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (Throw) throw new IOException("Controlled authority outage.");
            return Task.FromResult(Status);
        }
    }

    private static string PromptPath(string fileName, [CallerFilePath] string testFile = "")
    {
        DirectoryInfo directory = new(Path.GetDirectoryName(testFile)!);
        string repository = directory.Parent!.Parent!.Parent!.Parent!.FullName;
        return Path.Combine(repository, "src", "dotnet", "Vulgata.Agents", "Prompts", fileName);
    }
}
