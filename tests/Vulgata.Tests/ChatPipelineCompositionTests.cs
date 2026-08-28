using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared.Chat;
using Vulgata.Web.Data;

namespace Vulgata.Tests;

[Collection(DataProtectionProductionCompositionCollection.Name)]
public sealed class ChatPipelineCompositionTests
{
    [Fact]
    public void ProductionComposition_Story125B1_ResolvesAdvisoryReaderAndRelationshipProjectorAsScopedPorts()
    {
        using var factory = new LoginLogoutTests.CustomWebApplicationFactory();
        using IServiceScope firstScope = factory.Services.CreateScope();
        using IServiceScope secondScope = factory.Services.CreateScope();

        IChatAdvisoryContextReader firstReader =
            firstScope.ServiceProvider.GetRequiredService<IChatAdvisoryContextReader>();
        IChatRelationshipProjector firstProjector =
            firstScope.ServiceProvider.GetRequiredService<IChatRelationshipProjector>();

        Assert.IsType<ChatAdvisoryContextReader>(firstReader);
        Assert.IsType<ChatRelationshipEvidenceProjector>(firstProjector);
        Assert.Same(
            firstReader,
            firstScope.ServiceProvider.GetRequiredService<IChatAdvisoryContextReader>());
        Assert.Same(
            firstProjector,
            firstScope.ServiceProvider.GetRequiredService<IChatRelationshipProjector>());
        Assert.NotSame(
            firstReader,
            secondScope.ServiceProvider.GetRequiredService<IChatAdvisoryContextReader>());
        Assert.NotSame(
            firstProjector,
            secondScope.ServiceProvider.GetRequiredService<IChatRelationshipProjector>());
    }

    [Fact]
    public void PreparedChatCoordinator_Story125B1_DeclaresServerOwnedAdvisoryBeforeEvidenceRetrieval()
    {
        ConstructorInfo constructor = Assert.Single(typeof(PreparedChatCoordinator).GetConstructors());
        ParameterInfo[] parameters = constructor.GetParameters();

        int advisoryIndex = Array.FindIndex(
            parameters,
            parameter => parameter.ParameterType == typeof(IChatAdvisoryContextReader));
        int retrievalIndex = Array.FindIndex(
            parameters,
            parameter => parameter.ParameterType == typeof(IChatEvidenceRetriever));

        Assert.True(
            advisoryIndex >= 0,
            "PreparedChatCoordinator must receive the server-owned advisory reader.");
        Assert.True(
            retrievalIndex >= 0,
            "PreparedChatCoordinator must receive the evidence retriever.");
        Assert.True(
            advisoryIndex < retrievalIndex,
            "Server-owned advisory Context must be acquired before retrieval and packet construction.");
    }

    [Fact]
    public void PreparedChatQuestionRequest_Story125B1_HasNoCallerAdvisoryTextBoundary()
    {
        Assert.DoesNotContain(
            typeof(PreparedChatQuestionRequest).GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name.Contains("Advisory", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            typeof(PreparedChatQuestionRequest).GetConstructors(BindingFlags.Public | BindingFlags.Instance),
            constructor => constructor.GetParameters().Any(parameter =>
                parameter.Name?.Contains("advisory", StringComparison.OrdinalIgnoreCase) == true));
    }
}

internal sealed class CanonicalEmptyAdvisoryContextReader : IChatAdvisoryContextReader
{
    public int CallCount { get; private set; }
    public List<string>? Trace { get; init; }
    public ChatAdvisoryContextReadResultKind? ResultKind { get; init; }

    public Task<ChatAdvisoryContextReadResult> ReadAsync(
        AuthorizedChatScope scope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        Trace?.Add("advisory");
        return Task.FromResult(ResultKind switch
        {
            ChatAdvisoryContextReadResultKind.NotFoundOrUnauthorized =>
                ChatAdvisoryContextReadResult.NotFoundOrUnauthorized(),
            ChatAdvisoryContextReadResultKind.InconsistentCurrentEvidence =>
                ChatAdvisoryContextReadResult.InconsistentCurrentEvidence(),
            _ => ChatAdvisoryContextReadResult.Succeeded(
                new ChatAdvisoryContextSnapshot([], string.Empty, [], 0, new string('0', 64))),
        });
    }
}
