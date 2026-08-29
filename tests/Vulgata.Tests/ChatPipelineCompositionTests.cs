using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Agents.ChatExecution;
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
    public void RetrievalArchitecture_Story125T074_RetrieverConstructorComposesServerOwnedAdvisoryReader()
    {
        ConstructorInfo constructor = Assert.Single(typeof(ChatEvidenceRetriever).GetConstructors());
        ParameterInfo[] parameters = constructor.GetParameters();

        Assert.Single(
            parameters,
            parameter => parameter.ParameterType == typeof(IChatAdvisoryContextReader));
        Assert.True(
            Array.FindIndex(parameters, parameter =>
                parameter.ParameterType == typeof(IChatAdvisoryContextReader)) <
            Array.FindIndex(parameters, parameter =>
                parameter.ParameterType == typeof(IChatLexicalEvidenceProjector)),
            "The retriever must acquire server-owned advisory Context before the typed lexical stage.");
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
    public void PublicationBoundary_Story125B4_DeclaresOneOwnerAndNoAlternateSuccessOwner()
    {
        Type? ownerType = Type.GetType(
            "Vulgata.Web.Data.IPreparedChatPublicationOwner, Vulgata.Web");

        Assert.NotNull(ownerType);
        Assert.True(ownerType!.IsInterface);
        Type[] implementations = ownerType.Assembly.GetTypes()
            .Where(type => !type.IsInterface && ownerType.IsAssignableFrom(type))
            .ToArray();
        Assert.Single(implementations);
    }

    [Fact]
    public void ProductionComposition_Story125BInt_ResolvesOneScopedPublicationOwnerForBusinessFlow()
    {
        using var factory = new LoginLogoutTests.CustomWebApplicationFactory();
        using IServiceScope scope = factory.Services.CreateScope();

        PreparedChatCoordinator coordinator = scope.ServiceProvider
            .GetRequiredService<PreparedChatCoordinator>();
        IPreparedChatPublicationOwner owner = scope.ServiceProvider
            .GetRequiredService<IPreparedChatPublicationOwner>();
        IChatStreamCompletionSummaryRecorder recorder = scope.ServiceProvider
            .GetRequiredService<IChatStreamCompletionSummaryRecorder>();
        IPreparedChatPublicationOwner[] owners = scope.ServiceProvider
            .GetServices<IPreparedChatPublicationOwner>()
            .ToArray();
        IChatStreamCompletionSummaryRecorder[] recorders = scope.ServiceProvider
            .GetServices<IChatStreamCompletionSummaryRecorder>()
            .ToArray();

        Assert.IsType<PreparedChatPublicationOwner>(owner);
        Assert.Single(owners);
        Assert.Single(recorders);
        Assert.Same(
            owner,
            scope.ServiceProvider.GetRequiredService<IPreparedChatPublicationOwner>());

        FieldInfo? publicationOwnerField = typeof(PreparedChatCoordinator).GetField(
            "_publicationOwner",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(publicationOwnerField);
        Assert.Same(owner, publicationOwnerField!.GetValue(coordinator));

        FieldInfo? completionSummaryRecorderField = typeof(PreparedChatCoordinator).GetField(
            "_completionSummaryRecorder",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(completionSummaryRecorderField);
        Assert.Same(recorder, completionSummaryRecorderField!.GetValue(coordinator));
    }

    [Fact]
    public void ProductionComposition_Story125T071_UsesOneOwnerGenerationResetBoundary()
    {
        Type ownerType = typeof(IPreparedChatPublicationOwner);
        MemberInfo snapshotMember = Assert.Single(
            ownerType.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Where(member => member.Name.Contains("Snapshot", StringComparison.Ordinal)));
        Type snapshotType = snapshotMember switch
        {
            MethodInfo method => method.ReturnType,
            PropertyInfo property => property.PropertyType,
            _ => throw new Xunit.Sdk.XunitException(
                "The publication owner snapshot must be a public method or property."),
        };

        Assert.Contains(
            snapshotType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name.Contains("Generation", StringComparison.Ordinal));
        Assert.Contains(
            snapshotType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name.Contains("Commit", StringComparison.Ordinal) ||
                property.Name.Contains("Current", StringComparison.Ordinal));

        MethodInfo reset = Assert.Single(
            ownerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.Name.Contains("Reset", StringComparison.Ordinal)));
        Assert.Empty(reset.GetParameters());
        Assert.Equal(typeof(void), reset.ReturnType);

        FieldInfo[] coordinatorShadowAuthorities = typeof(PreparedChatCoordinator)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.FieldType.Name == "PreparedChatPublicationAuthority")
            .ToArray();
        Assert.Empty(coordinatorShadowAuthorities);
    }

    [Fact]
    public void ProductionComposition_Story125BInt_DispatcherUsesTypedStreamingTransport()
    {
        using var factory = new LoginLogoutTests.CustomWebApplicationFactory();
        using IServiceScope scope = factory.Services.CreateScope();

        IChatAgentDispatcher dispatcher =
            scope.ServiceProvider.GetRequiredService<IChatAgentDispatcher>();
        IChatProviderStreamingTransport transport =
            scope.ServiceProvider.GetRequiredService<IChatProviderStreamingTransport>();

        Assert.IsType<ChatAgentDispatcher>(dispatcher);
        Assert.IsType<OpenAiCompatibleChatProviderStreamingTransport>(transport);

        FieldInfo? streamingTransportField = typeof(ChatAgentDispatcher).GetField(
            "_streamingTransport",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(streamingTransportField);
        Assert.Same(transport, streamingTransportField!.GetValue(dispatcher));
    }

    [Fact]
    public void ProductionComposition_Story125BInt_ItPathHasNoTrackCAttestationOrAlternateLiveStateOwner()
    {
        Type? attestationSink = typeof(ChatAgentDispatcher).Assembly.GetType(
            "Vulgata.Agents.ChatExecution.Story125StreamAttestationSink");
        Assert.Null(attestationSink);

        FieldInfo[] liveStateFields = typeof(PreparedChatCoordinator)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.FieldType == typeof(ChatConversationState))
            .ToArray();
        Assert.Empty(liveStateFields);

        MethodInfo[] nonStreamingEntryPoints = typeof(ChatAgentDispatcher)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name.Contains("NonStreaming", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.Empty(nonStreamingEntryPoints);
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
