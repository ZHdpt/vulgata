using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests;

namespace Vulgata.Tests.PreparedPass2;

/// <summary>
/// T024 contract tests for the closed <c>retrieve_code_logic</c> Worker Agent
/// function.  These tests deliberately stop at the public Core seam: operation
/// behavior and content-dark refusal paths belong to the service/security
/// slices once that seam exists.
/// </summary>
public sealed class RetrieveCodeLogicContractTests
{
    private const string ServiceTypeName =
        "Vulgata.Core.DomainServices.IPass2CodeLogicRetrievalService";

    private static readonly string[] ExpectedOperations =
    [
        "Search",
        "Sections",
        "SourceAnchors",
        "SourceExcerpt",
        "RelatedDocuments",
    ];

    [Fact]
    public void RetrieveCodeLogic_PublicService_DeclaresTypedServerOwnedAsyncSeam()
    {
        Type? service = FindService();
        Assert.NotNull(service);
        if (service is null)
        {
            return;
        }

        Assert.True(service.IsInterface);

        MethodInfo retrieve = Assert.Single(
            service.GetMethods(), method => method.Name == "RetrieveAsync");
        Assert.True(
            retrieve.ReturnType.IsGenericType &&
            retrieve.ReturnType.GetGenericTypeDefinition() == typeof(Task<>),
            "The retrieval seam must return a typed asynchronous result.");

        ParameterInfo[] parameters = retrieve.GetParameters();
        Assert.Contains(parameters, parameter => parameter.ParameterType == typeof(CancellationToken));
        Assert.DoesNotContain(
            parameters,
            parameter => parameter.ParameterType == typeof(RepositoryAccessScope));

        ParameterInfo[] requestParameters = parameters
            .Where(parameter => parameter.ParameterType != typeof(CancellationToken))
            .ToArray();
        Assert.InRange(requestParameters.Length, 1, 2);
        Assert.Contains(
            requestParameters,
            parameter => parameter.ParameterType != typeof(Guid) &&
                         parameter.ParameterType != typeof(string));
    }

    [Fact]
    public void RetrieveCodeLogic_Request_ClosesTheFiveTypedOperations()
    {
        Type? service = FindService();
        Assert.NotNull(service);
        if (service is null)
        {
            return;
        }

        MethodInfo retrieve = Assert.Single(
            service.GetMethods(), method => method.Name == "RetrieveAsync");
        Type requestType = Assert.Single(
            retrieve.GetParameters()
                .Where(parameter => parameter.ParameterType != typeof(CancellationToken) &&
                                    parameter.ParameterType != typeof(Guid))
                .Select(parameter => parameter.ParameterType));

        PropertyInfo? operation = requestType.GetProperty("Operation");
        Assert.NotNull(operation);
        if (operation is null)
        {
            return;
        }

        Assert.True(operation.PropertyType.IsEnum);
        Assert.Equal(
            ExpectedOperations.OrderBy(name => name),
            Enum.GetNames(operation.PropertyType).OrderBy(name => name));
    }

    [Fact]
    public void RetrieveCodeLogic_Result_IsTypedAndNotAnUnboundedRawPayload()
    {
        Type? service = FindService();
        Assert.NotNull(service);
        if (service is null)
        {
            return;
        }

        MethodInfo retrieve = Assert.Single(
            service.GetMethods(), method => method.Name == "RetrieveAsync");
        Type resultType = Assert.Single(retrieve.ReturnType.GetGenericArguments());

        Assert.NotEqual(typeof(object), resultType);
        Assert.NotEqual(typeof(string), resultType);
        Assert.NotEmpty(resultType.GetProperties(BindingFlags.Instance | BindingFlags.Public));
    }

    [Fact]
    public void RetrieveCodeLogic_Item_ExposesClosedKindAndTypedBoundedDescriptor()
    {
        Type itemType = typeof(Pass2CodeLogicRetrievalItem);
        PropertyInfo? kind = itemType.GetProperty("Kind");
        Assert.NotNull(kind);
        if (kind is null)
        {
            return;
        }

        Assert.True(kind.PropertyType.IsEnum);
        Assert.Equal(
            ["Document", "Section", "SourceAnchor", "SourceExcerpt", "Relationship"],
            Enum.GetNames(kind.PropertyType));

        PropertyInfo? documentVersionId = itemType.GetProperty("DocumentVersionId");
        Assert.NotNull(documentVersionId);
        if (documentVersionId is null)
        {
            return;
        }

        Assert.Equal(typeof(Guid), documentVersionId.PropertyType);
        PropertyInfo? returnedUtf8Bytes = itemType.GetProperty("ReturnedUtf8Bytes");
        Assert.NotNull(returnedUtf8Bytes);
        if (returnedUtf8Bytes is null)
        {
            return;
        }

        Assert.Equal(typeof(int), returnedUtf8Bytes.PropertyType);

        PropertyInfo[] publicProperties = itemType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        Assert.DoesNotContain(publicProperties, property =>
            property.PropertyType == typeof(object) ||
            property.PropertyType.FullName is "System.Text.Json.JsonDocument" or "System.Text.Json.JsonElement");
        Assert.DoesNotContain(publicProperties, property =>
            property.Name.Contains("AbsolutePath", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("RepositoryAccessScope", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RetrieveCodeLogic_Item_ExposesTypedTransientDescriptorCoordinates()
    {
        Type itemType = typeof(Pass2CodeLogicRetrievalItem);
        (string Name, Type Type)[] expected =
        [
            ("SectionId", typeof(Guid?)),
            ("SourceAnchorId", typeof(Guid?)),
            ("EdgeId", typeof(Guid?)),
            ("ByteStart", typeof(long?)),
            ("ByteEnd", typeof(long?)),
        ];

        foreach ((string name, Type type) in expected)
        {
            PropertyInfo? property = itemType.GetProperty(name);
            Assert.NotNull(property);
            if (property is not null)
            {
                Assert.Equal(type, property.PropertyType);
            }
        }
    }

    [Fact]
    public void RetrieveCodeLogic_Bounds_AreInclusiveAndExact()
    {
        Assert.Equal(8, Pass2RetrievalContracts.MaxCalls);
        Assert.Equal(10, Pass2RetrievalContracts.MaxResults);
        Assert.Equal(4, Pass2RetrievalContracts.MaxAdditionalDocuments);
        Assert.Equal(4, Pass2RetrievalContracts.MaxSectionsPerDocument);
        Assert.Equal(65_536, Pass2RetrievalContracts.MaxReturnedUtf8Bytes);
    }

    [Fact]
    public void RetrieveCodeLogic_DurableRows_AreContentDarkAndUseClosedOutcomes()
    {
        Assert.Equal(
            ["Accepted", "NoResults", "BoundExceeded", "Refused", "Unavailable"],
            Enum.GetNames<Pass2RetrievalReceiptState>());
        Assert.Equal(["Admitted"], Enum.GetNames<Pass2RetrievalResultState>());

        string[] forbiddenFragments =
        [
            "Query", "Section", "Source", "Path", "Endpoint", "Provider",
            "Content", "Prompt", "Response", "Secret", "Reasoning", "Raw",
        ];

        foreach (Type entityType in new[]
                 {
                     typeof(Pass2RetrievalReceipt),
                     typeof(Pass2RetrievalResult),
                 })
        {
            foreach (PropertyInfo property in entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                Assert.DoesNotContain(
                    forbiddenFragments,
                    fragment => property.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Fact]
    public async Task RetrieveCodeLogic_Search_UsesCurrentPersistedSectionsAndOpaqueResultsAsync()
    {
        await using RetrievalHarness harness = await RetrievalHarness.CreateAsync();

        Pass2CodeLogicRetrievalResult result = await harness.Service.RetrieveAsync(
            harness.JobId,
            new Pass2CodeLogicRetrievalRequest(
                Pass2CodeLogicRetrievalOperation.Search,
                queryText: "ledger"));

        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, result.Outcome);
        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item => Assert.False(string.IsNullOrWhiteSpace(item.OpaqueHandle)));
        Assert.DoesNotContain("ledger", result.Code, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RetrieveCodeLogic_Search_RepeatedRequestIsCanonicallyOrderedAndDeduplicatedAsync()
    {
        await using RetrievalHarness harness = await RetrievalHarness.CreateAsync();
        Pass2CodeLogicRetrievalRequest request = new(
            Pass2CodeLogicRetrievalOperation.Search,
            queryText: "ledger");

        Pass2CodeLogicRetrievalResult first = await harness.Service.RetrieveAsync(
            harness.JobId,
            request);
        Pass2CodeLogicRetrievalResult second = await harness.Service.RetrieveAsync(
            harness.JobId,
            request);

        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, first.Outcome);
        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, second.Outcome);
        string[] firstHandles = first.Items.Select(item => item.OpaqueHandle).ToArray();
        string[] secondHandles = second.Items.Select(item => item.OpaqueHandle).ToArray();
        Assert.Equal(firstHandles, secondHandles);
        Assert.Equal(firstHandles.Length, firstHandles.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task RetrieveCodeLogic_SearchIssuedSectionHandle_SectionsReturnsCurrentBoundedSectionAsync()
    {
        await using RetrievalHarness harness = await RetrievalHarness.CreateAsync();

        Pass2CodeLogicRetrievalResult search = await harness.Service.RetrieveAsync(
            harness.JobId,
            new Pass2CodeLogicRetrievalRequest(
                Pass2CodeLogicRetrievalOperation.Search,
                queryText: "ledger"));
        Assert.NotEmpty(search.Items);
        Pass2CodeLogicRetrievalItem sectionHandle = search.Items.First(
            item => item.Kind == Pass2CodeLogicRetrievalItemKind.Section);

        Pass2CodeLogicRetrievalResult result = await harness.Service.RetrieveAsync(
            harness.JobId,
            new Pass2CodeLogicRetrievalRequest(
                Pass2CodeLogicRetrievalOperation.Sections,
                opaqueHandles: [sectionHandle.OpaqueHandle]));

        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, result.Outcome);
        Pass2CodeLogicRetrievalItem returned = Assert.Single(result.Items);
        Assert.Equal(Pass2CodeLogicRetrievalItemKind.Section, returned.Kind);
        Assert.Equal(sectionHandle.DocumentVersionId, returned.DocumentVersionId);
        Assert.True(returned.ReturnedUtf8Bytes > 0);
    }

    [Fact]
    public async Task RetrieveCodeLogic_SearchIssuedSectionHandle_SourceAnchorsReturnsSafeCurrentDescriptorAsync()
    {
        await using RetrievalHarness harness = await RetrievalHarness.CreateAsync();

        Pass2CodeLogicRetrievalResult search = await harness.Service.RetrieveAsync(
            harness.JobId,
            new Pass2CodeLogicRetrievalRequest(
                Pass2CodeLogicRetrievalOperation.Search,
                queryText: "ledger"));
        Assert.NotEmpty(search.Items);
        Pass2CodeLogicRetrievalItem sectionHandle = search.Items.First(
            item => item.Kind == Pass2CodeLogicRetrievalItemKind.Section);

        Pass2CodeLogicRetrievalResult result = await harness.Service.RetrieveAsync(
            harness.JobId,
            new Pass2CodeLogicRetrievalRequest(
                Pass2CodeLogicRetrievalOperation.SourceAnchors,
                opaqueHandles: [sectionHandle.OpaqueHandle]));

        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, result.Outcome);
        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item =>
        {
            Assert.Equal(Pass2CodeLogicRetrievalItemKind.SourceAnchor, item.Kind);
            Assert.Equal(sectionHandle.DocumentVersionId, item.DocumentVersionId);
            Assert.True(item.ReturnedUtf8Bytes > 0);
        });
    }

    [Fact]
    public async Task RetrieveCodeLogic_SearchIssuedAnchorHandle_SourceExcerptReadsExactRevisionBoundSourceAsync()
    {
        await using RetrievalHarness harness = await RetrievalHarness.CreateAsync();

        Pass2CodeLogicRetrievalResult search = await harness.Service.RetrieveAsync(
            harness.JobId,
            new Pass2CodeLogicRetrievalRequest(
                Pass2CodeLogicRetrievalOperation.Search,
                queryText: "ledger"));
        Assert.NotEmpty(search.Items);
        Pass2CodeLogicRetrievalItem sectionHandle = search.Items.First(
            item => item.Kind == Pass2CodeLogicRetrievalItemKind.Section);
        Pass2CodeLogicRetrievalResult anchors = await harness.Service.RetrieveAsync(
            harness.JobId,
            new Pass2CodeLogicRetrievalRequest(
                Pass2CodeLogicRetrievalOperation.SourceAnchors,
                opaqueHandles: [sectionHandle.OpaqueHandle]));
        Assert.NotEmpty(anchors.Items);
        Pass2CodeLogicRetrievalItem anchorHandle = anchors.Items.First(
            item => item.Kind == Pass2CodeLogicRetrievalItemKind.SourceAnchor);
        SourceAnchor expectedAnchor = await harness.Context.SourceAnchors
            .AsNoTracking()
            .SingleAsync(anchor => anchor.DocumentVersionId == anchorHandle.DocumentVersionId);

        Pass2CodeLogicRetrievalResult result = await harness.Service.RetrieveAsync(
            harness.JobId,
            new Pass2CodeLogicRetrievalRequest(
                Pass2CodeLogicRetrievalOperation.SourceExcerpt,
                opaqueHandles: [anchorHandle.OpaqueHandle],
                containedByteOffset: 0,
                containedByteLength: 6));

        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, result.Outcome);
        Pass2CodeLogicRetrievalItem excerpt = Assert.Single(result.Items);
        Assert.Equal(Pass2CodeLogicRetrievalItemKind.SourceExcerpt, excerpt.Kind);
        Assert.Equal(anchorHandle.DocumentVersionId, excerpt.DocumentVersionId);
        Assert.Equal(1, harness.SourceReader.ReadCalls);
        Assert.NotNull(harness.SourceReader.LastRequest);
        Assert.Equal(expectedAnchor.RepositoryRelativePath, harness.SourceReader.LastRequest!.RelativePath);
        Assert.Equal(expectedAnchor.ProfiledRevision, harness.SourceReader.LastScope!.ProfiledRevision);
        Assert.Equal(expectedAnchor.SourceHash, harness.SourceReader.LastRequest.ExpectedSourceSha256);
        Assert.Equal(0, harness.SourceReader.LastRequest.ByteStart);
        Assert.Equal(6, harness.SourceReader.LastRequest.ByteEndExclusive);
    }

    [Fact]
    public async Task RetrieveCodeLogic_SearchIssuedDocumentHandle_RelatedDocumentsFollowsOneReferencesHopAsync()
    {
        await using RetrievalHarness harness = await RetrievalHarness.CreateAsync();

        Pass2CodeLogicRetrievalResult search = await harness.Service.RetrieveAsync(
            harness.JobId,
            new Pass2CodeLogicRetrievalRequest(
                Pass2CodeLogicRetrievalOperation.Search,
                queryText: "consumer"));
        Pass2CodeLogicRetrievalItem documentHandle = Assert.Single(
            search.Items,
            item => item.Kind == Pass2CodeLogicRetrievalItemKind.Document);

        Pass2CodeLogicRetrievalResult result = await harness.Service.RetrieveAsync(
            harness.JobId,
            new Pass2CodeLogicRetrievalRequest(
                Pass2CodeLogicRetrievalOperation.RelatedDocuments,
                opaqueHandles: [documentHandle.OpaqueHandle],
                direction: Pass2RelatedDocumentDirection.Outgoing));

        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, result.Outcome);
        Pass2CodeLogicRetrievalItem related = Assert.Single(result.Items);
        Assert.Equal(Pass2CodeLogicRetrievalItemKind.Relationship, related.Kind);
        Assert.NotEqual(documentHandle.DocumentVersionId, related.DocumentVersionId);
        Assert.True(related.ReturnedUtf8Bytes > 0);
    }

    [Fact]
    public async Task RetrieveCodeLogic_Sections_UnknownOpaqueHandleIsClosedRefusalAsync()
    {
        await using RetrievalHarness harness = await RetrievalHarness.CreateAsync();

        Pass2CodeLogicRetrievalResult result = await harness.Service.RetrieveAsync(
            harness.JobId,
            new Pass2CodeLogicRetrievalRequest(
                Pass2CodeLogicRetrievalOperation.Sections,
                opaqueHandles: ["opaque-handle-not-issued-for-this-job"]));

        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, result.Outcome);
        Assert.Empty(result.Items);
        Assert.DoesNotContain("opaque-handle-not-issued-for-this-job", result.Code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RetrieveCodeLogic_SourceAnchors_UnknownOpaqueHandleIsClosedRefusalAsync()
    {
        await using RetrievalHarness harness = await RetrievalHarness.CreateAsync();

        Pass2CodeLogicRetrievalResult result = await harness.Service.RetrieveAsync(
            harness.JobId,
            new Pass2CodeLogicRetrievalRequest(
                Pass2CodeLogicRetrievalOperation.SourceAnchors,
                opaqueHandles: ["opaque-anchor-not-issued-for-this-job"]));

        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, result.Outcome);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task RetrieveCodeLogic_SourceExcerpt_UnknownOpaqueHandleIsRefusedBeforeSourceReadAsync()
    {
        await using RetrievalHarness harness = await RetrievalHarness.CreateAsync();

        Pass2CodeLogicRetrievalResult result = await harness.Service.RetrieveAsync(
            harness.JobId,
            new Pass2CodeLogicRetrievalRequest(
                Pass2CodeLogicRetrievalOperation.SourceExcerpt,
                opaqueHandles: ["opaque-source-anchor-not-issued-for-this-job"],
                containedByteOffset: 0,
                containedByteLength: 6));

        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, result.Outcome);
        Assert.Empty(result.Items);
        Assert.Equal(0, harness.SourceReader.ReadCalls);
    }

    [Fact]
    public async Task RetrieveCodeLogic_RelatedDocuments_UnknownOpaqueHandleIsClosedRefusalAsync()
    {
        await using RetrievalHarness harness = await RetrievalHarness.CreateAsync();

        Pass2CodeLogicRetrievalResult result = await harness.Service.RetrieveAsync(
            harness.JobId,
            new Pass2CodeLogicRetrievalRequest(
                Pass2CodeLogicRetrievalOperation.RelatedDocuments,
                opaqueHandles: ["opaque-document-not-issued-for-this-job"],
                direction: Pass2RelatedDocumentDirection.Outgoing));

        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, result.Outcome);
        Assert.Empty(result.Items);
    }

    private sealed class RetrievalHarness : IAsyncDisposable
    {
        private RetrievalHarness(
            ExactCrossRepositoryTestFixture fixture,
            VulgataDbContext context,
            Guid jobId,
            RepositoryAccessScope scope,
            RecordingSourceReader sourceReader)
        {
            Fixture = fixture;
            Context = context;
            JobId = jobId;
            SourceReader = sourceReader;
            Service = new Pass2CodeLogicRetrievalService(context, scope, sourceReader);
        }

        private ExactCrossRepositoryTestFixture Fixture { get; }

        internal VulgataDbContext Context { get; }

        internal Guid JobId { get; }

        internal RecordingSourceReader SourceReader { get; }

        internal IPass2CodeLogicRetrievalService Service { get; }

        internal static async Task<RetrievalHarness> CreateAsync()
        {
            ExactCrossRepositoryTestFixture fixture =
                await ExactCrossRepositoryTestFixture.CreateAsync();
            VulgataDbContext context = fixture.Database.CreateContext();
            try
            {
                object seed = await InvokeAuthoritativeSeedAsync(context, fixture);
                Pass1PairExecution pair = GetProperty<Pass1PairExecution>(seed, "Pair");
                RepositoryAccessScope scope = GetProperty<RepositoryAccessScope>(seed, "Scope");
                await AddSearchableSectionsAsync(context, seed);

                PreparedPass2StartResult start = await new PreparedPass2Store(context)
                    .StartAsync(pair.Id, scope);
                Assert.Equal(PreparedPass2StartOutcome.Created, start.Outcome);
                Pass2JobExecution job = await context.Pass2JobExecutions.SingleAsync();
                Assert.Equal(1, await context.Pass2RetrievalBudgets.CountAsync());
                Assert.Equal(3, await context.DocumentSections.CountAsync());

                RecordingSourceReader sourceReader = new();
                return new(fixture, context, job.Id, scope, sourceReader);
            }
            catch
            {
                await context.DisposeAsync();
                await fixture.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Fixture.DisposeAsync();
        }

        private static async Task<object> InvokeAuthoritativeSeedAsync(
            VulgataDbContext context,
            ExactCrossRepositoryTestFixture fixture)
        {
            MethodInfo seedMethod = typeof(Pass2FlowPlanningPostgreSqlTests).GetMethod(
                "SeedPass2Async",
                BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("The accepted T020 seed helper is missing.");
            object invocation = seedMethod.Invoke(null, [context, fixture])
                ?? throw new InvalidOperationException("The accepted T020 seed invocation returned null.");
            Task seedTask = Assert.IsAssignableFrom<Task>(invocation);
            await seedTask;
            return seedTask.GetType().GetProperty("Result")?.GetValue(seedTask)
                ?? throw new InvalidOperationException("The accepted T020 seed result is unavailable.");
        }

        private static async Task AddSearchableSectionsAsync(
            VulgataDbContext context,
            object seed)
        {
            ExactPass1DocumentFixture[] documents =
            [
                GetProperty<ExactPass1DocumentFixture>(seed, "Consumer"),
                GetProperty<ExactPass1DocumentFixture>(seed, "ProviderXml"),
                GetProperty<ExactPass1DocumentFixture>(seed, "ProviderImplementation"),
            ];

            for (int index = 0; index < documents.Length; index++)
            {
                ExactPass1DocumentFixture document = documents[index];
                context.DocumentSections.Add(DocumentSection.Create(
                    ExactCrossRepositoryTestValues.Id(30_001 + index),
                    document.LogicalDocument.Id,
                    document.DocumentVersion.Id,
                    document.DocumentVersion.RepositoryId,
                    ExactCrossRepositoryTestValues.Id(30_101 + index),
                    1,
                    DocumentSectionKind.Evidence,
                    $"Ledger evidence {index + 1}",
                    $"ledger updateSession evidence {index + 1}",
                    "ledger updateSession",
                    $"[\"{document.SourceAnchor.Id}\"]",
                    $"[\"{document.DocumentVersion.Id}\"]",
                    ExactCrossRepositoryTestValues.Pass1CompletedAt));

                context.IndexEntries.Add(IndexEntry.Create(
                    ExactCrossRepositoryTestValues.Id(30_201 + index),
                    document.LogicalDocument.Id,
                    document.DocumentVersion.Id,
                    document.DocumentVersion.RepositoryId,
                    ExactCrossRepositoryTestValues.SystemId,
                    DocumentType.CodeLogic,
                    $"{document.DocumentVersion.Title} ledger",
                    $"Current {document.DocumentVersion.Title} document",
                    document.DocumentVersion.RepositoryRelativePath,
                    "[]",
                    "[]",
                    "[]",
                    index == 0 ? "consumer ledger" : $"provider ledger {index}",
                    ExactCrossRepositoryTestValues.Pass1CompletedAt));
            }

            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }

        private static T GetProperty<T>(object source, string name) =>
            (T)(source.GetType().GetProperty(name)?.GetValue(source)
                ?? throw new InvalidOperationException($"Seed property is unavailable: {name}."));
    }

    private sealed class RecordingSourceReader : IRevisionBoundSourceReader
    {
        internal int ReadCalls { get; private set; }

        internal AnalysisScope? LastScope { get; private set; }

        internal RevisionBoundSourceRequest? LastRequest { get; private set; }

        public Task<SourceExcerptResult> ReadAsync(
            AnalysisScope scope,
            RevisionBoundSourceRequest request,
            CancellationToken cancellationToken)
        {
            ReadCalls++;
            LastScope = scope;
            LastRequest = request;
            return Task.FromResult(new SourceExcerptResult(
                AnalysisOperationStatus.Succeeded,
                scope.RepositoryId,
                scope.ProfiledRevision,
                request.RelativePath,
                request.Range,
                request.ExpectedSourceSha256,
                "ledger",
                6,
                null));
        }
    }

    private static Type? FindService() =>
        typeof(Pass2RetrievalContracts).Assembly.GetType(ServiceTypeName, throwOnError: false);
}
