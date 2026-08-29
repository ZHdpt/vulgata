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
    public void ProductionComposition_Story125T067_UsesOneConstructorComposedTypedRetrievalPath()
    {
        using var factory = new LoginLogoutTests.CustomWebApplicationFactory();
        using IServiceScope scope = factory.Services.CreateScope();

        IChatEvidenceRetriever retriever =
            scope.ServiceProvider.GetRequiredService<IChatEvidenceRetriever>();
        ConstructorInfo constructor = Assert.Single(typeof(ChatEvidenceRetriever).GetConstructors());
        Type[] dependencyTypes = constructor.GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.IsType<ChatEvidenceRetriever>(retriever);
        Assert.Contains(typeof(IChatRelationshipProjector), dependencyTypes);
        Assert.Contains(typeof(ChatEvidencePacketBuilder), dependencyTypes);
        Assert.IsType<ChatRelationshipEvidenceProjector>(
            scope.ServiceProvider.GetRequiredService<IChatRelationshipProjector>());
        Assert.IsType<ChatEvidencePacketBuilder>(
            scope.ServiceProvider.GetRequiredService<ChatEvidencePacketBuilder>());
    }

    [Fact]
    public void RetrievalArchitecture_Story125T073_LexicalStageConsumesAuthorizedSnapshot()
    {
        ConstructorInfo constructor = Assert.Single(typeof(ChatEvidenceRetriever).GetConstructors());
        MethodInfo[] typedLexicalMethods = constructor.GetParameters()
            .Select(parameter => parameter.ParameterType)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Where(method => method.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(AuthorizedChatSnapshot)))
            .Where(method => ReturnsClosedStageValue(method.ReturnType, typeof(ChatLexicalPrimaryProjection)))
            .ToArray();

        Assert.True(
            typedLexicalMethods.Length == 1,
            "The runtime retriever must constructor-compose one lexical stage that consumes AuthorizedChatSnapshot and returns ChatLexicalPrimaryProjection.");
        MethodInfo lexicalMethod = typedLexicalMethods[0];
        Assert.DoesNotContain(
            lexicalMethod.GetParameters(),
            parameter => parameter.ParameterType == typeof(ChatRetrievalRequest));
        Assert.DoesNotContain(
            lexicalMethod.GetParameters(),
            parameter => IsGuidCollection(parameter.ParameterType));
    }

    [Fact]
    public void RetrievalArchitecture_Story125T073_RelationshipStageConsumesLexicalProjection()
    {
        MethodInfo projectMethod = Assert.Single(
            typeof(IChatRelationshipProjector).GetMethods(BindingFlags.Public | BindingFlags.Instance));
        ParameterInfo[] parameters = projectMethod.GetParameters();

        Assert.Contains(
            parameters,
            parameter => parameter.ParameterType == typeof(ChatLexicalPrimaryProjection));
        Assert.DoesNotContain(
            parameters,
            parameter => parameter.ParameterType == typeof(ChatRetrievalRequest));
        Assert.DoesNotContain(parameters, parameter => IsGuidCollection(parameter.ParameterType));
        Assert.True(
            ReturnsClosedStageValue(
                projectMethod.ReturnType,
                typeof(ChatRelationshipExpansionProjection)),
            "The relationship stage must return the closed ChatRelationshipExpansionProjection stage result.");
    }

    [Fact]
    public void RetrievalArchitecture_Story125T073_PacketBuilderConsumesOnlyTypedAssembly()
    {
        MethodInfo buildMethod = Assert.Single(
            typeof(ChatEvidencePacketBuilder).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.Name == nameof(ChatEvidencePacketBuilder.Build)));
        ParameterInfo parameter = Assert.Single(buildMethod.GetParameters());

        Assert.Equal(typeof(ChatEvidenceAssembly), parameter.ParameterType);
        Assert.Equal(typeof(EvidencePacket), buildMethod.ReturnType);
        Assert.DoesNotContain(
            typeof(ChatEvidencePacketBuilder).GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.GetParameters().Any(candidate =>
                candidate.ParameterType == typeof(ChatEvidencePacketBuildRequest)));
    }

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

    private static bool ReturnsClosedStageValue(Type returnType, Type valueType)
    {
        Type candidate = returnType.IsGenericType &&
            returnType.GetGenericTypeDefinition() == typeof(Task<>)
                ? returnType.GetGenericArguments()[0]
                : returnType;
        return candidate.IsGenericType &&
            candidate.GetGenericTypeDefinition() == typeof(ChatRetrievalStageResult<>) &&
            candidate.GetGenericArguments()[0] == valueType;
    }

    private static bool IsGuidCollection(Type type) =>
        type == typeof(Guid[]) ||
        type.GetInterfaces()
            .Concat([type])
            .Any(candidate => candidate.IsGenericType &&
                candidate.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>) &&
                candidate.GetGenericArguments()[0] == typeof(Guid));
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
