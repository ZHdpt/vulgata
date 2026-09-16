using System.Reflection;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests.Migrations;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Vulgata.Tests.PreparedPass2;

/// <summary>
/// T026 PostgreSQL REDs for the public retrieve_code_logic seam. The fixture
/// is the same authoritative terminal Pass 1 pair used by T020; only the
/// uniquely named database is created by this test.
/// </summary>
public sealed class RetrieveCodeLogicPostgreSqlBudgetTests(ITestOutputHelper output)
{
    [PostgreSqlQualificationFact]
    public void RetrieveAsync_ServerOwnedCallIdentityOverloadIsAvailable()
    {
        Type? identityType = typeof(Pass2CodeLogicRetrievalService).Assembly.GetType(
            "Vulgata.Infrastructure.Data.Pass2RetrievalCallIdentity",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(identityType);
        Assert.True(identityType!.IsNotPublic);
        Assert.Equal(typeof(int), identityType.GetProperty(
            "AttemptOrdinal",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.PropertyType);
        Assert.Equal(typeof(int), identityType.GetProperty(
            "CallOrdinal",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.PropertyType);
        Assert.Equal(typeof(string), identityType.GetProperty(
            "ToolCallIdFingerprint",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.PropertyType);

        MethodInfo? invocation = typeof(Pass2CodeLogicRetrievalService)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SingleOrDefault(method =>
            {
                if (method.Name != nameof(IPass2CodeLogicRetrievalService.RetrieveAsync))
                {
                    return false;
                }

                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 4 &&
                    parameters[0].ParameterType == typeof(Guid) &&
                    parameters[1].ParameterType == identityType &&
                    parameters[2].ParameterType == typeof(Pass2CodeLogicRetrievalRequest) &&
                    parameters[3].ParameterType == typeof(CancellationToken) &&
                    method.ReturnType == typeof(Task<Pass2CodeLogicRetrievalResult>);
            });

        Assert.NotNull(invocation);
        Assert.False(invocation!.IsPublic);
    }

    [PostgreSqlQualificationFact]
    public async Task RetrieveAsync_ConcurrentReservationsKeepOrdinalsAndCountersConsistentAsync()
    {
        await RunOnSeededPostgreSqlAsync(async (context, jobId, scope) =>
        {
            string connectionString = context.Database.GetConnectionString()
                ?? throw new InvalidOperationException("The disposable PostgreSQL connection is missing.");
            await using VulgataDbContext firstContext = CreateContext(connectionString);
            await using VulgataDbContext secondContext = CreateContext(connectionString);
            Pass2CodeLogicRetrievalService firstService = CreateConcreteService(firstContext, scope);
            Pass2CodeLogicRetrievalService secondService = CreateConcreteService(secondContext, scope);

            Task<Pass2CodeLogicRetrievalResult> first = firstService.RetrieveAsync(
                jobId,
                new Pass2CodeLogicRetrievalRequest(
                    Pass2CodeLogicRetrievalOperation.Search,
                    queryText: "t026-concurrent-reservation-no-match-a"));
            Task<Pass2CodeLogicRetrievalResult> second = secondService.RetrieveAsync(
                jobId,
                new Pass2CodeLogicRetrievalRequest(
                    Pass2CodeLogicRetrievalOperation.Search,
                    queryText: "t026-concurrent-reservation-no-match-b"));

            Pass2CodeLogicRetrievalResult[] outcomes = await Task.WhenAll(first, second);
            Assert.All(
                outcomes,
                outcome => Assert.Equal(Pass2CodeLogicRetrievalOutcome.NoResults, outcome.Outcome));

            await using VulgataDbContext verificationContext = CreateContext(connectionString);
            Pass2RetrievalBudget budget = await verificationContext.Pass2RetrievalBudgets.SingleAsync();
            Pass2RetrievalReceipt[] receipts = await verificationContext.Pass2RetrievalReceipts
                .OrderBy(receipt => receipt.CallOrdinal)
                .ToArrayAsync();
            Assert.Equal(2, budget.CallsReserved);
            Assert.Equal(0, budget.ResultsReturned);
            Assert.Equal(2, receipts.Length);
            Assert.Equal([1, 2], receipts.Select(receipt => receipt.CallOrdinal));
            Assert.All(
                receipts,
                receipt => Assert.Equal(Pass2RetrievalReceiptState.NoResults, receipt.State));
        });
    }

    [PostgreSqlQualificationFact]
    public async Task RetrieveAsync_FourAdditionalDocumentsAreInclusiveAndFifthIsBoundedAsync()
    {
        await RunOnSeededPostgreSqlAsync(async (context, jobId, scope) =>
        {
            string[] queries = await AddExternalAdditionalDocumentsAsync(context);
            Assert.Equal(5, queries.Length);
            Pass2CodeLogicRetrievalService service = CreateConcreteService(context, scope);

            for (int ordinal = 0; ordinal < Pass2RetrievalContracts.MaxAdditionalDocuments; ordinal++)
            {
                Pass2CodeLogicRetrievalResult result = await service.RetrieveAsync(
                    jobId,
                    new Pass2CodeLogicRetrievalRequest(
                        Pass2CodeLogicRetrievalOperation.Search,
                        queryText: queries[ordinal]));
                Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, result.Outcome);
                Assert.Single(result.Items);
                Assert.Equal(Pass2CodeLogicRetrievalItemKind.Document, result.Items[0].Kind);
            }

            context.ChangeTracker.Clear();
            Pass2RetrievalBudget beforeFifth = await context.Pass2RetrievalBudgets.SingleAsync();
            Assert.Equal(4, beforeFifth.CallsReserved);
            Assert.Equal(4, beforeFifth.ResultsReturned);
            Assert.Equal(Pass2RetrievalContracts.MaxAdditionalDocuments,
                beforeFifth.AdditionalDocumentCount);
            int receiptCountBeforeFifth = await context.Pass2RetrievalReceipts.CountAsync();
            int resultCountBeforeFifth = await context.Pass2RetrievalResults.CountAsync();

            Pass2CodeLogicRetrievalResult fifth = await service.RetrieveAsync(
                jobId,
                new Pass2CodeLogicRetrievalRequest(
                    Pass2CodeLogicRetrievalOperation.Search,
                    queryText: queries[4]));
            Assert.Equal(Pass2CodeLogicRetrievalOutcome.BoundExceeded, fifth.Outcome);
            Assert.Empty(fifth.Items);

            context.ChangeTracker.Clear();
            Pass2RetrievalBudget afterFifth = await context.Pass2RetrievalBudgets.SingleAsync();
            Assert.Equal(5, afterFifth.CallsReserved);
            Assert.Equal(4, afterFifth.ResultsReturned);
            Assert.Equal(Pass2RetrievalContracts.MaxAdditionalDocuments,
                afterFifth.AdditionalDocumentCount);
            Assert.Equal(receiptCountBeforeFifth + 1,
                await context.Pass2RetrievalReceipts.CountAsync());
            Assert.Equal(resultCountBeforeFifth, await context.Pass2RetrievalResults.CountAsync());
            Pass2RetrievalReceipt refusal = await context.Pass2RetrievalReceipts
                .OrderByDescending(receipt => receipt.CallOrdinal)
                .FirstAsync();
            Assert.Equal(Pass2RetrievalReceiptState.BoundExceeded, refusal.State);
        });
    }

    [PostgreSqlQualificationFact]
    public async Task RetrieveAsync_FifthSectionForDocumentIsRefusedWithoutMutationAsync()
    {
        await RunOnSeededPostgreSqlAsync(async (context, jobId, scope) =>
        {
            string fifthSectionQuery = await AddFifthSectionAsync(context);
            Pass2CodeLogicRetrievalService service = CreateConcreteService(context, scope);
            string uniqueDocumentQuery = await SelectUniqueDocumentQueryAsync(context);
            Pass2CodeLogicRetrievalResult document = await service.RetrieveAsync(
                jobId,
                new Pass2CodeLogicRetrievalRequest(
                    Pass2CodeLogicRetrievalOperation.Search,
                    queryText: uniqueDocumentQuery));
            Assert.Single(document.Items);
            Pass2CodeLogicRetrievalResult sections = await service.RetrieveAsync(
                jobId,
                new Pass2CodeLogicRetrievalRequest(
                    Pass2CodeLogicRetrievalOperation.Sections,
                    opaqueHandles: [document.Items.Single().OpaqueHandle]));
            Assert.Equal(Pass2RetrievalContracts.MaxSectionsPerDocument, sections.Items.Count);

            context.ChangeTracker.Clear();
            Pass2RetrievalBudget beforeFifth = await context.Pass2RetrievalBudgets.SingleAsync();
            Assert.Equal(5, beforeFifth.ResultsReturned);
            int receiptCountBeforeFifth = await context.Pass2RetrievalReceipts.CountAsync();
            int resultCountBeforeFifth = await context.Pass2RetrievalResults.CountAsync();
            Pass2CodeLogicRetrievalResult fifth = await service.RetrieveAsync(
                jobId,
                new Pass2CodeLogicRetrievalRequest(
                    Pass2CodeLogicRetrievalOperation.Search,
                    queryText: fifthSectionQuery));
            Assert.Equal(Pass2CodeLogicRetrievalOutcome.BoundExceeded, fifth.Outcome);
            Assert.Empty(fifth.Items);

            context.ChangeTracker.Clear();
            Pass2RetrievalBudget afterFifth = await context.Pass2RetrievalBudgets.SingleAsync();
            Assert.Equal(beforeFifth.CallsReserved + 1, afterFifth.CallsReserved);
            Assert.Equal(beforeFifth.ResultsReturned, afterFifth.ResultsReturned);
            Assert.Equal(receiptCountBeforeFifth + 1,
                await context.Pass2RetrievalReceipts.CountAsync());
            Assert.Equal(resultCountBeforeFifth, await context.Pass2RetrievalResults.CountAsync());
        });
    }

    [PostgreSqlQualificationFact]
    public async Task RetrieveAsync_DuplicateHandleProjectionProducesOneCanonicalResultAndChargesOnceAsync()
    {
        await RunOnSeededPostgreSqlAsync(async (context, jobId, scope) =>
        {
            DocumentVersion document = await context.DocumentVersions.AsNoTracking()
                .Where(value => context.FlowTraceSteps
                    .Select(step => step.DocumentVersionId)
                    .Contains(value.Id))
                .OrderBy(value => value.Id)
                .FirstAsync();
            string uniqueDocumentQuery = await SelectUniqueDocumentQueryAsync(context);
            Pass2CodeLogicRetrievalService service = CreateConcreteService(context, scope);
            Pass2CodeLogicRetrievalResult documentSearch = await service
                .RetrieveAsync(
                    jobId,
                    new Pass2CodeLogicRetrievalRequest(
                        Pass2CodeLogicRetrievalOperation.Search,
                        queryText: uniqueDocumentQuery));
            Assert.Single(documentSearch.Items);
            Assert.Equal(document.Id, documentSearch.Items.Single().DocumentVersionId);

            Pass2CodeLogicRetrievalResult result = await service.RetrieveAsync(
                jobId,
                new Pass2CodeLogicRetrievalRequest(
                    Pass2CodeLogicRetrievalOperation.Sections,
                    opaqueHandles:
                    [
                        documentSearch.Items.Single().OpaqueHandle,
                        documentSearch.Items.Single().OpaqueHandle,
                    ]));
            Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, result.Outcome);
            Assert.Equal(Pass2RetrievalContracts.MaxSectionsPerDocument, result.Items.Count);
            Assert.Equal(result.Items.Count, result.Items.Select(item => item.OpaqueHandle).Distinct().Count());

            context.ChangeTracker.Clear();
            Pass2RetrievalBudget budget = await context.Pass2RetrievalBudgets.SingleAsync();
            Assert.Equal(2, budget.CallsReserved);
            Assert.Equal(5, budget.ResultsReturned);
            Assert.Equal(2, await context.Pass2RetrievalReceipts.CountAsync());
            Assert.Equal(5, await context.Pass2RetrievalResults.CountAsync());
        });
    }

    [PostgreSqlQualificationFact]
    public async Task RetrieveAsync_ExactOrdinalReplayReturnsSameDescriptorsWithoutMutationAsync()
    {
        await RunOnSeededPostgreSqlAsync(async (context, jobId, scope) =>
        {
            string uniqueDocumentQuery = await SelectUniqueDocumentQueryAsync(context);
            Pass2CodeLogicRetrievalRequest request = new(
                Pass2CodeLogicRetrievalOperation.Search,
                queryText: uniqueDocumentQuery);
            Pass2RetrievalCallIdentity identity = new(
                attemptOrdinal: 1,
                callOrdinal: 1,
                toolCallIdFingerprint: new string('a', 64));

            Pass2CodeLogicRetrievalResult first = await new Pass2CodeLogicRetrievalService(
                context,
                scope,
                new SuccessfulSourceReader()).RetrieveAsync(jobId, identity, request, CancellationToken.None);
            Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, first.Outcome);

            context.ChangeTracker.Clear();
            Pass2RetrievalBudget beforeReplay = await context.Pass2RetrievalBudgets.SingleAsync();
            int receiptCountBeforeReplay = await context.Pass2RetrievalReceipts.CountAsync();
            int resultCountBeforeReplay = await context.Pass2RetrievalResults.CountAsync();
            int callsBeforeReplay = beforeReplay.CallsReserved;
            int resultsBeforeReplay = beforeReplay.ResultsReturned;
            int bytesBeforeReplay = beforeReplay.ReturnedUtf8Bytes;
            int documentsBeforeReplay = beforeReplay.AdditionalDocumentCount;

            Pass2CodeLogicRetrievalResult replay = await new Pass2CodeLogicRetrievalService(
                context,
                scope,
                new SuccessfulSourceReader()).RetrieveAsync(jobId, identity, request, CancellationToken.None);

            Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, replay.Outcome);
            Assert.Equal(
                first.Items.Select(item => item.OpaqueHandle),
                replay.Items.Select(item => item.OpaqueHandle));
            context.ChangeTracker.Clear();
            Pass2RetrievalBudget afterReplay = await context.Pass2RetrievalBudgets.SingleAsync();
            Assert.Equal(callsBeforeReplay, afterReplay.CallsReserved);
            Assert.Equal(resultsBeforeReplay, afterReplay.ResultsReturned);
            Assert.Equal(bytesBeforeReplay, afterReplay.ReturnedUtf8Bytes);
            Assert.Equal(documentsBeforeReplay, afterReplay.AdditionalDocumentCount);
            Assert.Equal(receiptCountBeforeReplay, await context.Pass2RetrievalReceipts.CountAsync());
            Assert.Equal(resultCountBeforeReplay, await context.Pass2RetrievalResults.CountAsync());
        });
    }

    [PostgreSqlQualificationFact]
    public async Task RetrieveAsync_AcknowledgementLossReplaySectionsRehydratesContentWithoutMutationAsync()
    {
        await RunOnSeededPostgreSqlAsync(async (context, jobId, scope) =>
        {
            string connectionString = context.Database.GetConnectionString()
                ?? throw new InvalidOperationException("The disposable PostgreSQL connection is missing.");
            string uniqueDocumentQuery = await SelectUniqueDocumentQueryAsync(context);
            Pass2CodeLogicRetrievalRequest searchRequest = new(
                Pass2CodeLogicRetrievalOperation.Search,
                queryText: uniqueDocumentQuery);
            Pass2CodeLogicRetrievalResult search = await new Pass2CodeLogicRetrievalService(
                context,
                scope,
                new SuccessfulSourceReader()).RetrieveAsync(
                    jobId,
                    new Pass2RetrievalCallIdentity(1, 1, new string('1', 64)),
                    searchRequest,
                    CancellationToken.None);
            Pass2CodeLogicRetrievalItem document = Assert.Single(search.Items.Where(
                item => item.Kind == Pass2CodeLogicRetrievalItemKind.Document));
            Pass2CodeLogicRetrievalRequest sectionsRequest = new(
                Pass2CodeLogicRetrievalOperation.Sections,
                opaqueHandles: [document.OpaqueHandle]);
            Pass2RetrievalCallIdentity identity = new(1, 2, new string('2', 64));
            Pass2CodeLogicRetrievalResult first = await new Pass2CodeLogicRetrievalService(
                context,
                scope,
                new SuccessfulSourceReader()).RetrieveAsync(
                    jobId,
                    identity,
                    sectionsRequest,
                    CancellationToken.None);
            Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, first.Outcome);
            Assert.NotEmpty(first.Items);
            Assert.All(first.Items, item => Assert.False(string.IsNullOrEmpty(item.ContentMarkdown)));

            context.ChangeTracker.Clear();
            Pass2RetrievalBudget beforeReplay = await context.Pass2RetrievalBudgets.SingleAsync();
            int receiptCount = await context.Pass2RetrievalReceipts.CountAsync();
            int resultCount = await context.Pass2RetrievalResults.CountAsync();
            await using VulgataDbContext replayContext = CreateContext(connectionString);
            Pass2CodeLogicRetrievalResult replay = await new Pass2CodeLogicRetrievalService(
                replayContext,
                scope,
                new SuccessfulSourceReader()).RetrieveAsync(
                    jobId,
                    identity,
                    sectionsRequest,
                    CancellationToken.None);

            Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, replay.Outcome);
            Assert.Equal(first.Items.Count, replay.Items.Count);
            for (int index = 0; index < first.Items.Count; index++)
            {
                AssertEquivalent(first.Items[index], replay.Items[index]);
                Assert.Equal(first.Items[index].ContentMarkdown, replay.Items[index].ContentMarkdown);
            }

            Pass2RetrievalBudget afterReplay = await replayContext.Pass2RetrievalBudgets.SingleAsync();
            Assert.Equal(beforeReplay.CallsReserved, afterReplay.CallsReserved);
            Assert.Equal(beforeReplay.ResultsReturned, afterReplay.ResultsReturned);
            Assert.Equal(beforeReplay.ReturnedUtf8Bytes, afterReplay.ReturnedUtf8Bytes);
            Assert.Equal(beforeReplay.AdditionalDocumentCount, afterReplay.AdditionalDocumentCount);
            Assert.Equal(receiptCount, await replayContext.Pass2RetrievalReceipts.CountAsync());
            Assert.Equal(resultCount, await replayContext.Pass2RetrievalResults.CountAsync());
        });
    }

    [PostgreSqlQualificationFact]
    public async Task RetrieveAsync_AcknowledgementLossReplaySourceExcerptRehydratesExcerptWithoutMutationAsync()
    {
        await RunOnSeededPostgreSqlAsync(async (context, jobId, scope) =>
        {
            string connectionString = context.Database.GetConnectionString()
                ?? throw new InvalidOperationException("The disposable PostgreSQL connection is missing.");
            string uniqueDocumentQuery = await SelectUniqueDocumentQueryAsync(context);
            Pass2CodeLogicRetrievalResult search = await new Pass2CodeLogicRetrievalService(
                context,
                scope,
                new SuccessfulSourceReader()).RetrieveAsync(
                    jobId,
                    new Pass2RetrievalCallIdentity(1, 1, new string('3', 64)),
                    new Pass2CodeLogicRetrievalRequest(
                        Pass2CodeLogicRetrievalOperation.Search,
                        queryText: uniqueDocumentQuery),
                    CancellationToken.None);
            Pass2CodeLogicRetrievalItem document = Assert.Single(search.Items.Where(
                item => item.Kind == Pass2CodeLogicRetrievalItemKind.Document));
            Pass2CodeLogicRetrievalResult anchors = await new Pass2CodeLogicRetrievalService(
                context,
                scope,
                new SuccessfulSourceReader()).RetrieveAsync(
                    jobId,
                    new Pass2RetrievalCallIdentity(1, 2, new string('4', 64)),
                    new Pass2CodeLogicRetrievalRequest(
                        Pass2CodeLogicRetrievalOperation.SourceAnchors,
                        opaqueHandles: [document.OpaqueHandle]),
                    CancellationToken.None);
            Pass2CodeLogicRetrievalItem anchor = Assert.Single(anchors.Items);
            Pass2CodeLogicRetrievalRequest excerptRequest = new(
                Pass2CodeLogicRetrievalOperation.SourceExcerpt,
                opaqueHandles: [anchor.OpaqueHandle],
                containedByteOffset: 0,
                containedByteLength: 7);
            Pass2RetrievalCallIdentity identity = new(1, 3, new string('5', 64));
            Pass2CodeLogicRetrievalResult first = await new Pass2CodeLogicRetrievalService(
                context,
                scope,
                new SuccessfulSourceReader()).RetrieveAsync(
                    jobId,
                    identity,
                    excerptRequest,
                    CancellationToken.None);
            Pass2CodeLogicRetrievalItem firstItem = Assert.Single(first.Items);
            Assert.Equal(Pass2CodeLogicRetrievalItemKind.SourceExcerpt, firstItem.Kind);
            Assert.False(string.IsNullOrEmpty(firstItem.Excerpt));

            context.ChangeTracker.Clear();
            Pass2RetrievalBudget beforeReplay = await context.Pass2RetrievalBudgets.SingleAsync();
            int receiptCount = await context.Pass2RetrievalReceipts.CountAsync();
            int resultCount = await context.Pass2RetrievalResults.CountAsync();
            await using VulgataDbContext replayContext = CreateContext(connectionString);
            Pass2CodeLogicRetrievalResult replay = await new Pass2CodeLogicRetrievalService(
                replayContext,
                scope,
                new SuccessfulSourceReader()).RetrieveAsync(
                    jobId,
                    identity,
                    excerptRequest,
                    CancellationToken.None);
            Pass2CodeLogicRetrievalItem replayItem = Assert.Single(replay.Items);

            Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, replay.Outcome);
            AssertEquivalent(firstItem, replayItem);
            Assert.Equal(firstItem.Excerpt, replayItem.Excerpt);
            Pass2RetrievalBudget afterReplay = await replayContext.Pass2RetrievalBudgets.SingleAsync();
            Assert.Equal(beforeReplay.CallsReserved, afterReplay.CallsReserved);
            Assert.Equal(beforeReplay.ResultsReturned, afterReplay.ResultsReturned);
            Assert.Equal(beforeReplay.ReturnedUtf8Bytes, afterReplay.ReturnedUtf8Bytes);
            Assert.Equal(beforeReplay.AdditionalDocumentCount, afterReplay.AdditionalDocumentCount);
            Assert.Equal(receiptCount, await replayContext.Pass2RetrievalReceipts.CountAsync());
            Assert.Equal(resultCount, await replayContext.Pass2RetrievalResults.CountAsync());
        });
    }

    [PostgreSqlQualificationFact]
    public async Task RetrieveAsync_AcknowledgementLossReplayAfterCurrentnessChangesIsRefusedWithoutMutationAsync()
    {
        await RunOnSeededPostgreSqlAsync(async (context, jobId, scope) =>
        {
            string connectionString = context.Database.GetConnectionString()
                ?? throw new InvalidOperationException("The disposable PostgreSQL connection is missing.");
            string uniqueDocumentQuery = await SelectUniqueDocumentQueryAsync(context);
            Pass2CodeLogicRetrievalRequest request = new(
                Pass2CodeLogicRetrievalOperation.Search,
                queryText: uniqueDocumentQuery);
            Pass2RetrievalCallIdentity identity = new(1, 1, new string('6', 64));
            Pass2CodeLogicRetrievalResult first = await new Pass2CodeLogicRetrievalService(
                context,
                scope,
                new SuccessfulSourceReader()).RetrieveAsync(
                    jobId,
                    identity,
                    request,
                    CancellationToken.None);
            Pass2CodeLogicRetrievalItem document = Assert.Single(first.Items.Where(
                item => item.Kind == Pass2CodeLogicRetrievalItemKind.Document));
            Guid logicalDocumentId = await context.DocumentVersions.AsNoTracking()
                .Where(value => value.Id == document.DocumentVersionId)
                .Select(value => value.LogicalDocumentId)
                .SingleAsync();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"vulgata\".\"LogicalDocuments\" SET \"CurrentVersionId\" = NULL WHERE \"Id\" = {logicalDocumentId}");
            context.ChangeTracker.Clear();
            Pass2RetrievalBudget beforeReplay = await context.Pass2RetrievalBudgets.SingleAsync();
            int receiptCount = await context.Pass2RetrievalReceipts.CountAsync();
            int resultCount = await context.Pass2RetrievalResults.CountAsync();

            await using VulgataDbContext replayContext = CreateContext(connectionString);
            Pass2CodeLogicRetrievalResult replay = await new Pass2CodeLogicRetrievalService(
                replayContext,
                scope,
                new SuccessfulSourceReader()).RetrieveAsync(
                    jobId,
                    identity,
                    request,
                    CancellationToken.None);

            Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, replay.Outcome);
            Assert.Empty(replay.Items);
            Pass2RetrievalBudget afterReplay = await replayContext.Pass2RetrievalBudgets.SingleAsync();
            Assert.Equal(beforeReplay.CallsReserved, afterReplay.CallsReserved);
            Assert.Equal(beforeReplay.ResultsReturned, afterReplay.ResultsReturned);
            Assert.Equal(beforeReplay.ReturnedUtf8Bytes, afterReplay.ReturnedUtf8Bytes);
            Assert.Equal(beforeReplay.AdditionalDocumentCount, afterReplay.AdditionalDocumentCount);
            Assert.Equal(receiptCount, await replayContext.Pass2RetrievalReceipts.CountAsync());
            Assert.Equal(resultCount, await replayContext.Pass2RetrievalResults.CountAsync());
        });
    }

    [PostgreSqlQualificationFact]
    public async Task RetrieveAsync_ChangedRequestForExistingOrdinalIsRefusedWithoutMutationAsync()
    {
        await RunOnSeededPostgreSqlAsync(async (context, jobId, scope) =>
        {
            string uniqueDocumentQuery = await SelectUniqueDocumentQueryAsync(context);
            Pass2RetrievalCallIdentity identity = new(
                attemptOrdinal: 1,
                callOrdinal: 1,
                toolCallIdFingerprint: new string('b', 64));
            Pass2CodeLogicRetrievalResult first = await new Pass2CodeLogicRetrievalService(
                context,
                scope,
                new SuccessfulSourceReader()).RetrieveAsync(
                    jobId,
                    identity,
                    new Pass2CodeLogicRetrievalRequest(
                        Pass2CodeLogicRetrievalOperation.Search,
                        queryText: uniqueDocumentQuery),
                    CancellationToken.None);
            Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, first.Outcome);

            context.ChangeTracker.Clear();
            Pass2RetrievalBudget beforeConflict = await context.Pass2RetrievalBudgets.SingleAsync();
            int receiptCountBeforeConflict = await context.Pass2RetrievalReceipts.CountAsync();
            int resultCountBeforeConflict = await context.Pass2RetrievalResults.CountAsync();
            Pass2CodeLogicRetrievalResult conflict = await new Pass2CodeLogicRetrievalService(
                context,
                scope,
                new SuccessfulSourceReader()).RetrieveAsync(
                    jobId,
                    identity,
                    new Pass2CodeLogicRetrievalRequest(
                        Pass2CodeLogicRetrievalOperation.Search,
                        queryText: "t026-changed-request-no-match"),
                    CancellationToken.None);

            Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, conflict.Outcome);
            Assert.Empty(conflict.Items);
            context.ChangeTracker.Clear();
            Pass2RetrievalBudget afterConflict = await context.Pass2RetrievalBudgets.SingleAsync();
            Assert.Equal(beforeConflict.CallsReserved, afterConflict.CallsReserved);
            Assert.Equal(beforeConflict.ResultsReturned, afterConflict.ResultsReturned);
            Assert.Equal(beforeConflict.ReturnedUtf8Bytes, afterConflict.ReturnedUtf8Bytes);
            Assert.Equal(beforeConflict.AdditionalDocumentCount, afterConflict.AdditionalDocumentCount);
            Assert.Equal(receiptCountBeforeConflict, await context.Pass2RetrievalReceipts.CountAsync());
            Assert.Equal(resultCountBeforeConflict, await context.Pass2RetrievalResults.CountAsync());
        });
    }

    [PostgreSqlQualificationFact]
    public async Task RetrieveAsync_LostReservationRemainsConsumedAndCannotBeRetriedAsync()
    {
        await RunOnSeededPostgreSqlAsync(async (context, jobId, scope) =>
        {
            string uniqueDocumentQuery = await SelectUniqueDocumentQueryAsync(context);
            Pass2CodeLogicRetrievalResult document = await new Pass2CodeLogicRetrievalService(
                context,
                scope,
                new SuccessfulSourceReader()).RetrieveAsync(
                    jobId,
                    new Pass2RetrievalCallIdentity(1, 1, new string('c', 64)),
                    new Pass2CodeLogicRetrievalRequest(
                        Pass2CodeLogicRetrievalOperation.Search,
                        queryText: uniqueDocumentQuery),
                    CancellationToken.None);
            Assert.Single(document.Items);
            Pass2CodeLogicRetrievalResult anchors = await new Pass2CodeLogicRetrievalService(
                context,
                scope,
                new SuccessfulSourceReader()).RetrieveAsync(
                    jobId,
                    new Pass2RetrievalCallIdentity(1, 2, new string('d', 64)),
                    new Pass2CodeLogicRetrievalRequest(
                        Pass2CodeLogicRetrievalOperation.SourceAnchors,
                        opaqueHandles: [document.Items.Single().OpaqueHandle]),
                    CancellationToken.None);
            Pass2CodeLogicRetrievalItem anchor = anchors.Items
                .Single(item => item.Kind == Pass2CodeLogicRetrievalItemKind.SourceAnchor);
            Pass2CodeLogicRetrievalRequest excerptRequest = new(
                Pass2CodeLogicRetrievalOperation.SourceExcerpt,
                opaqueHandles: [anchor.OpaqueHandle],
                containedByteOffset: 0,
                containedByteLength: 1);
            ThrowingSourceReader throwingReader = new();

            await Assert.ThrowsAsync<InvalidOperationException>(() => new Pass2CodeLogicRetrievalService(
                context,
                scope,
                throwingReader).RetrieveAsync(
                    jobId,
                    new Pass2RetrievalCallIdentity(1, 3, new string('e', 64)),
                    excerptRequest,
                    CancellationToken.None));
            Assert.Equal(1, throwingReader.ReadCalls);

            context.ChangeTracker.Clear();
            Pass2RetrievalBudget afterLoss = await context.Pass2RetrievalBudgets.SingleAsync();
            Assert.Equal(3, afterLoss.CallsReserved);
            Assert.Equal(2, await context.Pass2RetrievalReceipts.CountAsync());
            int resultCountAfterLoss = await context.Pass2RetrievalResults.CountAsync();

            CountingSuccessfulSourceReader retryReader = new();
            Pass2CodeLogicRetrievalResult retry = await new Pass2CodeLogicRetrievalService(
                context,
                scope,
                retryReader).RetrieveAsync(
                    jobId,
                    new Pass2RetrievalCallIdentity(1, 3, new string('e', 64)),
                    excerptRequest,
                    CancellationToken.None);
            Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, retry.Outcome);
            Assert.Empty(retry.Items);
            Assert.Equal(0, retryReader.ReadCalls);
            context.ChangeTracker.Clear();
            Pass2RetrievalBudget afterRetry = await context.Pass2RetrievalBudgets.SingleAsync();
            Assert.Equal(3, afterRetry.CallsReserved);
            Assert.Equal(2, await context.Pass2RetrievalReceipts.CountAsync());
            Assert.Equal(resultCountAfterLoss, await context.Pass2RetrievalResults.CountAsync());

            Pass2CodeLogicRetrievalResult next = await new Pass2CodeLogicRetrievalService(
                context,
                scope,
                retryReader).RetrieveAsync(
                    jobId,
                    new Pass2RetrievalCallIdentity(1, 4, new string('f', 64)),
                    excerptRequest,
                    CancellationToken.None);
            Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, next.Outcome);
            Assert.Single(next.Items);
            Assert.Equal(1, retryReader.ReadCalls);
        });
    }

    [PostgreSqlQualificationFact]
    public async Task RetrieveAsync_EightCallsAreInclusiveAndNinthIsPersistedBoundRefusalAsync()
    {
        await RunOnSeededPostgreSqlAsync(async (context, jobId, scope) =>
        {
            IPass2CodeLogicRetrievalService service = CreateService(context, scope);
            Pass2CodeLogicRetrievalRequest request = new(
                Pass2CodeLogicRetrievalOperation.Search,
                queryText: "t026-call-cap-no-match");

            for (int ordinal = 1; ordinal <= Pass2RetrievalContracts.MaxCalls; ordinal++)
            {
                Pass2CodeLogicRetrievalResult result = await service.RetrieveAsync(jobId, request);
                Assert.True(
                    result.Outcome is Pass2CodeLogicRetrievalOutcome.Accepted or
                    Pass2CodeLogicRetrievalOutcome.NoResults,
                    $"call {ordinal} must complete without consuming an unavailable/refused path");
            }

            context.ChangeTracker.Clear();
            Pass2RetrievalBudget budget = await context.Pass2RetrievalBudgets.SingleAsync();
            Assert.Equal(Pass2RetrievalContracts.MaxCalls, budget.CallsReserved);
            Assert.Equal(Pass2RetrievalContracts.MaxCalls,
                await context.Pass2RetrievalReceipts.CountAsync());
            int resultCountBeforeNinth = await context.Pass2RetrievalResults.CountAsync();

            Pass2CodeLogicRetrievalResult ninth = await service.RetrieveAsync(jobId, request);
            Assert.Equal(Pass2CodeLogicRetrievalOutcome.BoundExceeded, ninth.Outcome);

            context.ChangeTracker.Clear();
            budget = await context.Pass2RetrievalBudgets.SingleAsync();
            Assert.Equal(Pass2RetrievalContracts.MaxCalls, budget.CallsReserved);
            Pass2RetrievalReceipt[] receipts = await context.Pass2RetrievalReceipts
                .OrderBy(receipt => receipt.CallOrdinal)
                .ToArrayAsync();
            Assert.Equal(Pass2RetrievalContracts.MaxCalls, receipts.Length);
            Assert.Equal(
                Enumerable.Range(1, Pass2RetrievalContracts.MaxCalls),
                receipts.Select(receipt => receipt.CallOrdinal));
            Assert.DoesNotContain(receipts, receipt => receipt.CallOrdinal == 9);
            Assert.Equal(resultCountBeforeNinth, await context.Pass2RetrievalResults.CountAsync());
        });
    }

    [PostgreSqlQualificationFact]
    public async Task RetrieveAsync_TenReturnedResultsAreInclusiveAndEleventhIsPersistedBoundRefusalAsync()
    {
        await RunOnSeededPostgreSqlAsync(async (context, jobId, scope) =>
        {
            IPass2CodeLogicRetrievalService service = CreateService(context, scope);
            Pass2CodeLogicRetrievalRequest request = new(
                Pass2CodeLogicRetrievalOperation.Search,
                queryText: "ledger");

            Pass2CodeLogicRetrievalResult first = await service.RetrieveAsync(jobId, request);
            Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, first.Outcome);
            Assert.Equal(Pass2RetrievalContracts.MaxResults, first.Items.Count);
            Assert.Equal(first.Items.Count, first.Items.Select(item => item.OpaqueHandle).Distinct().Count());

            context.ChangeTracker.Clear();
            Pass2RetrievalBudget budget = await context.Pass2RetrievalBudgets.SingleAsync();
            Assert.Equal(Pass2RetrievalContracts.MaxResults, budget.ResultsReturned);
            int receiptCountBeforePlusOne = await context.Pass2RetrievalReceipts.CountAsync();

            Pass2CodeLogicRetrievalResult plusOne = await service.RetrieveAsync(jobId, request);
            Assert.Equal(Pass2CodeLogicRetrievalOutcome.BoundExceeded, plusOne.Outcome);

            context.ChangeTracker.Clear();
            budget = await context.Pass2RetrievalBudgets.SingleAsync();
            Assert.Equal(Pass2RetrievalContracts.MaxResults, budget.ResultsReturned);
            Assert.Equal(receiptCountBeforePlusOne + 1,
                await context.Pass2RetrievalReceipts.CountAsync());
            Pass2RetrievalReceipt refusal = await context.Pass2RetrievalReceipts
                .OrderByDescending(receipt => receipt.CallOrdinal)
                .FirstAsync();
            Assert.Equal(Pass2RetrievalReceiptState.BoundExceeded, refusal.State);
            Assert.Empty(await context.Pass2RetrievalResults
                .Where(result => result.ReceiptId == refusal.Id)
                .ToArrayAsync());
        });
    }

    [PostgreSqlQualificationFact]
    public async Task RetrieveAsync_ExactReturnedUtf8ByteBudgetIsInclusiveAndPlusOneIsBoundedAsync()
    {
        await RunOnSeededPostgreSqlAsync(async (context, jobId, scope) =>
        {
            IPass2CodeLogicRetrievalService service = CreateService(context, scope);
            string uniqueDocumentQuery = await SelectUniqueDocumentQueryAsync(context);
            Pass2CodeLogicRetrievalResult documents = await service.RetrieveAsync(
                jobId,
                new Pass2CodeLogicRetrievalRequest(
                    Pass2CodeLogicRetrievalOperation.Search,
                    queryText: uniqueDocumentQuery));
            Assert.Single(documents.Items);
            Pass2CodeLogicRetrievalItem document = documents.Items
                .First(item => item.Kind == Pass2CodeLogicRetrievalItemKind.Document);
            Pass2CodeLogicRetrievalResult anchors = await service.RetrieveAsync(
                jobId,
                new Pass2CodeLogicRetrievalRequest(
                    Pass2CodeLogicRetrievalOperation.SourceAnchors,
                    opaqueHandles: [document.OpaqueHandle]));
            Pass2CodeLogicRetrievalItem anchor = anchors.Items
                .First(item => item.Kind == Pass2CodeLogicRetrievalItemKind.SourceAnchor);
            int bytesBeforeExcerpt = documents.Items
                .Concat(anchors.Items)
                .Sum(item => item.ReturnedUtf8Bytes);
            int remainingProviderBytes = Pass2RetrievalContracts.MaxReturnedUtf8Bytes - bytesBeforeExcerpt;
            int exactExcerptPayloadBytes = SolveExcerptPayloadForProviderBudget(
                remainingProviderBytes,
                document.DocumentVersionId,
                anchor.SourceAnchorId!.Value);
            Assert.True(exactExcerptPayloadBytes > 0);
            int expectedProviderBytes = ProviderVisibleExcerptBytes(
                document.DocumentVersionId,
                anchor.SourceAnchorId.Value,
                exactExcerptPayloadBytes);
            Assert.Equal(remainingProviderBytes, expectedProviderBytes);

            Pass2CodeLogicRetrievalResult exact = await service.RetrieveAsync(
                jobId,
                new Pass2CodeLogicRetrievalRequest(
                    Pass2CodeLogicRetrievalOperation.SourceExcerpt,
                    opaqueHandles: [anchor.OpaqueHandle],
                    containedByteOffset: 0,
                    containedByteLength: exactExcerptPayloadBytes));
            Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, exact.Outcome);
            Assert.Equal(expectedProviderBytes,
                exact.Items.Single().ReturnedUtf8Bytes);

            context.ChangeTracker.Clear();
            Pass2RetrievalBudget budget = await context.Pass2RetrievalBudgets.SingleAsync();
            Assert.Equal(Pass2RetrievalContracts.MaxReturnedUtf8Bytes, budget.ReturnedUtf8Bytes);

            Pass2CodeLogicRetrievalResult plusOne = await service.RetrieveAsync(
                jobId,
                new Pass2CodeLogicRetrievalRequest(
                    Pass2CodeLogicRetrievalOperation.SourceExcerpt,
                    opaqueHandles: [anchor.OpaqueHandle],
                    containedByteOffset: 0,
                    containedByteLength: exactExcerptPayloadBytes + 1));
            Assert.Equal(Pass2CodeLogicRetrievalOutcome.BoundExceeded, plusOne.Outcome);
        });
    }

    [PostgreSqlQualificationFact]
    public async Task RetrieveAsync_FourSectionsPerDocumentAreInclusiveAsync()
    {
        await RunOnSeededPostgreSqlAsync(async (context, jobId, scope) =>
        {
            IPass2CodeLogicRetrievalService service = CreateService(context, scope);
            string uniqueDocumentQuery = await SelectUniqueDocumentQueryAsync(context);
            Pass2CodeLogicRetrievalResult documents = await service.RetrieveAsync(
                jobId,
                new Pass2CodeLogicRetrievalRequest(
                    Pass2CodeLogicRetrievalOperation.Search,
                    queryText: uniqueDocumentQuery));
            Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, documents.Outcome);
            Assert.Single(documents.Items);
            Pass2CodeLogicRetrievalItem document = documents.Items
                .First(item => item.Kind == Pass2CodeLogicRetrievalItemKind.Document);
            Assert.Equal(Pass2CodeLogicRetrievalItemKind.Document, document.Kind);

            Pass2CodeLogicRetrievalResult sections = await service.RetrieveAsync(
                jobId,
                new Pass2CodeLogicRetrievalRequest(
                    Pass2CodeLogicRetrievalOperation.Sections,
                    opaqueHandles: [document.OpaqueHandle]));
            Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, sections.Outcome);
            Assert.Equal(Pass2RetrievalContracts.MaxSectionsPerDocument, sections.Items.Count);
            Assert.All(sections.Items, item =>
                Assert.Equal(Pass2CodeLogicRetrievalItemKind.Section, item.Kind));

            context.ChangeTracker.Clear();
            Pass2RetrievalBudget budget = await context.Pass2RetrievalBudgets.SingleAsync();
            Assert.Equal(5, budget.ResultsReturned);
        });
    }

    [PostgreSqlQualificationFact]
    public async Task RetrieveAsync_RepeatedRequestChargesCallsButKeepsDocumentAccountingCanonicalAsync()
    {
        await RunOnSeededPostgreSqlAsync(async (context, jobId, scope) =>
        {
            IPass2CodeLogicRetrievalService service = CreateService(context, scope);
            string uniqueDocumentQuery = await SelectUniqueDocumentQueryAsync(context);
            Pass2CodeLogicRetrievalResult documentSearch = await service.RetrieveAsync(
                jobId,
                new Pass2CodeLogicRetrievalRequest(
                    Pass2CodeLogicRetrievalOperation.Search,
                    queryText: uniqueDocumentQuery));
            Assert.Single(documentSearch.Items);
            Pass2CodeLogicRetrievalItem document = documentSearch.Items
                .First(item => item.Kind == Pass2CodeLogicRetrievalItemKind.Document);
            Pass2CodeLogicRetrievalRequest request = new(
                Pass2CodeLogicRetrievalOperation.Sections,
                opaqueHandles: [document.OpaqueHandle]);

            Pass2CodeLogicRetrievalResult first = await service.RetrieveAsync(jobId, request);
            Pass2CodeLogicRetrievalResult second = await service.RetrieveAsync(jobId, request);
            Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, first.Outcome);
            Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, second.Outcome);
            Assert.Equal(
                first.Items.Select(item => item.OpaqueHandle),
                second.Items.Select(item => item.OpaqueHandle));

            context.ChangeTracker.Clear();
            Pass2RetrievalBudget budget = await context.Pass2RetrievalBudgets.SingleAsync();
            Assert.Equal(3, budget.CallsReserved);
            Assert.Equal(9, budget.ResultsReturned);
            Assert.InRange(budget.AdditionalDocumentCount, 0,
                Pass2RetrievalContracts.MaxAdditionalDocuments);
            Assert.InRange(budget.ReturnedUtf8Bytes, 0, Pass2RetrievalContracts.MaxReturnedUtf8Bytes);
            Assert.Equal(3, await context.Pass2RetrievalReceipts.CountAsync());
        });
    }

    [PostgreSqlQualificationFact]
    public async Task RetrieveAsync_NewServiceInstanceDoesNotResetPersistedBudgetAsync()
    {
        await RunOnSeededPostgreSqlAsync(async (context, jobId, scope) =>
        {
            Pass2CodeLogicRetrievalRequest request = new(
                Pass2CodeLogicRetrievalOperation.Search,
                queryText: "t026-restart-no-match");
            Pass2CodeLogicRetrievalResult first = await CreateService(context, scope)
                .RetrieveAsync(jobId, request);
            Pass2CodeLogicRetrievalResult second = await CreateService(context, scope)
                .RetrieveAsync(jobId, request);

            Assert.True(first.Outcome is Pass2CodeLogicRetrievalOutcome.Accepted or
                Pass2CodeLogicRetrievalOutcome.NoResults);
            Assert.True(second.Outcome is Pass2CodeLogicRetrievalOutcome.Accepted or
                Pass2CodeLogicRetrievalOutcome.NoResults);

            context.ChangeTracker.Clear();
            Pass2RetrievalBudget budget = await context.Pass2RetrievalBudgets.SingleAsync();
            Assert.Equal(2, budget.CallsReserved);
            Assert.Equal(2, await context.Pass2RetrievalReceipts.CountAsync());
        });
    }

    private async Task RunOnSeededPostgreSqlAsync(
        Func<VulgataDbContext, Guid, RepositoryAccessScope, Task> action)
    {
        string? administratorConnectionString =
            Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN");
        if (string.IsNullOrWhiteSpace(administratorConnectionString))
        {
            throw SkipException.ForSkip(
                "Qualification skipped visibly: VULGATA_TEST_POSTGRES_ADMIN is unset; no database was contacted.");
        }

        await using DisposablePostgreSqlDatabase database =
            await DisposablePostgreSqlDatabase.CreateAsync(administratorConnectionString);
        string databaseName = database.DatabaseName;
        output.WriteLine("Owned disposable PostgreSQL database: {0}", databaseName);
        try
        {
            await using ExactCrossRepositoryTestFixture fixture =
                await ExactCrossRepositoryTestFixture.CreateAsync();
            await using VulgataDbContext context = CreateContext(database.ConnectionString);
            await context.Database.MigrateAsync();

            (Pass1PairExecution pair, RepositoryAccessScope scope) =
                await SeedUsingT020Async(context, fixture);
            PreparedPass2StartResult start = await Pass2FlowPlanningPostgreSqlTests.CreateStore(context)
                .StartAsync(pair.Id, scope);
            Assert.Equal(PreparedPass2StartOutcome.Created, start.Outcome);
            await AddRetrievalArtifactsAsync(context);
            Pass2JobExecution job = await context.Pass2JobExecutions.SingleAsync();
            DateTimeOffset stateChangedAt = DateTimeOffset.UtcNow;
            job.AcquireLease(Guid.NewGuid(), stateChangedAt.AddMinutes(5), stateChangedAt);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            RepositoryAccessScope liveScope = new(
                scope.AuthorizedRepositoryIds,
                new AlwaysAllowAuthorizationVerifier());
            await action(context, job.Id, liveScope);
        }
        finally
        {
            await database.DisposeAsync();
            await AssertDatabaseDroppedAsync(administratorConnectionString, databaseName);
        }
    }

    private static async Task<(Pass1PairExecution Pair, RepositoryAccessScope Scope)> SeedUsingT020Async(
        VulgataDbContext context,
        ExactCrossRepositoryTestFixture fixture)
    {
        // T020 owns the accepted authoritative pair fixture. Invoke only its
        // private setup helper; the behavior under test remains this class's
        // public IPass2CodeLogicRetrievalService call.
        MethodInfo seedMethod = typeof(Pass2FlowPlanningPostgreSqlTests).GetMethod(
            "SeedPass2Async",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("T020 authoritative seed helper is missing.");
        object invocation = seedMethod.Invoke(null, [context, fixture])
            ?? throw new InvalidOperationException("T020 seed invocation returned null.");
        if (invocation is not Task seedTask)
        {
            throw new InvalidOperationException("T020 seed helper is not asynchronous.");
        }

        await seedTask;
        object seed = seedTask.GetType().GetProperty("Result")?.GetValue(seedTask)
            ?? throw new InvalidOperationException("T020 seed result is unavailable.");
        MethodInfo addOptionalMethod = typeof(Pass2FlowPlanningPostgreSqlTests).GetMethod(
            "AddOptionalDocumentsAsync",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("T020 optional-document seed helper is missing.");
        object addInvocation = addOptionalMethod.Invoke(null, [context, seed, 5])
            ?? throw new InvalidOperationException("T020 optional-document seed invocation returned null.");
        if (addInvocation is not Task addTask)
        {
            throw new InvalidOperationException("T020 optional-document seed helper is not asynchronous.");
        }

        await addTask;
        Pass1PairExecution pair = (Pass1PairExecution)(seed.GetType()
            .GetProperty("Pair")?.GetValue(seed)
            ?? throw new InvalidOperationException("T020 seed pair is unavailable."));
        RepositoryAccessScope scope = (RepositoryAccessScope)(seed.GetType()
            .GetProperty("Scope")?.GetValue(seed)
            ?? throw new InvalidOperationException("T020 seed scope is unavailable."));
        return (pair, scope);
    }

    private static async Task AddRetrievalArtifactsAsync(VulgataDbContext context)
    {
        Guid[] documentIds = await context.FlowTraceSteps.AsNoTracking()
            .Where(step => step.DocumentVersionId != Guid.Empty)
            .Select(step => step.DocumentVersionId)
            .Distinct()
            .ToArrayAsync();
        DocumentVersion[] documents = await context.DocumentVersions.AsNoTracking()
            .Where(document => documentIds.Contains(document.Id))
            .ToArrayAsync();
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        foreach (DocumentVersion document in documents)
        {
            if (!await context.IndexEntries.AnyAsync(entry => entry.DocumentVersionId == document.Id))
            {
                context.IndexEntries.Add(IndexEntry.Create(
                    Guid.NewGuid(),
                    document.LogicalDocumentId,
                    document.Id,
                    document.RepositoryId,
                    document.SystemId,
                    DocumentType.CodeLogic,
                    $"Ledger document {document.Id:N}",
                    "Ledger retrieval fixture",
                    document.RepositoryRelativePath,
                    "[]",
                    "[]",
                    "[]",
                    "ledger ledger-doc",
                    createdAt));
            }

            bool hasSections = await context.DocumentSections
                .AnyAsync(section => section.DocumentVersionId == document.Id);
            if (!hasSections)
            {
                for (int sectionOrder = 1; sectionOrder <= Pass2RetrievalContracts.MaxSectionsPerDocument; sectionOrder++)
                {
                    context.DocumentSections.Add(DocumentSection.Create(
                        Guid.NewGuid(),
                        document.LogicalDocumentId,
                        document.Id,
                        document.RepositoryId,
                        Guid.NewGuid(),
                        sectionOrder,
                        DocumentSectionKind.TechnicalDetail,
                        $"Ledger section {sectionOrder}",
                        $"Ledger section content {sectionOrder} for {document.Id:N}",
                        "ledger",
                        JsonSerializer.Serialize(Array.Empty<Guid>()),
                        JsonSerializer.Serialize(Array.Empty<Guid>()),
                        createdAt));
                }
            }
        }

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static async Task<string[]> AddExternalAdditionalDocumentsAsync(VulgataDbContext context)
    {
        DocumentVersion template = await context.DocumentVersions.AsNoTracking()
            .Where(value => value.RepositoryRelativePath.Contains("/related/"))
            .OrderBy(value => value.Id)
            .FirstAsync();
        SourceAnchor templateAnchor = await context.SourceAnchors.AsNoTracking()
            .SingleAsync(value => value.DocumentVersionId == template.Id);
        string[] queries = Enumerable.Range(1, Pass2RetrievalContracts.MaxAdditionalDocuments + 1)
            .Select(index => $"t026-external-additional-document-{index:D2}")
            .ToArray();
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        for (int index = 0; index < queries.Length; index++)
        {
            Guid logicalDocumentId = Guid.NewGuid();
            Guid documentVersionId = Guid.NewGuid();
            Guid sourceAnchorId = Guid.NewGuid();
            string path = $"src/main/java/retrieval-external/{documentVersionId:N}.java";
            LogicalDocument logicalDocument = LogicalDocument.Create(
                logicalDocumentId,
                template.RepositoryId,
                DocumentType.CodeLogic,
                $"t026-external-logical-{index + 1:D2}",
                createdAt);
            DocumentVersion document = DocumentVersion.Create(
                documentVersionId,
                logicalDocumentId,
                template.RepositoryId,
                template.SystemId,
                template.ScanRunId,
                template.ProfiledRevision,
                1,
                $"T026 external additional document {index + 1:D2}",
                path,
                KnowledgeDocumentLimits.SchemaIdentity,
                "{}",
                $"# T026 external additional document {index + 1:D2}",
                template.ContentFingerprint,
                template.CommitFingerprint,
                createdAt);
            document.SourceAnchors.Add(new SourceAnchor(
                sourceAnchorId,
                documentVersionId,
                template.RepositoryId,
                template.ScanRunId,
                templateAnchor.CodeUnitId,
                template.ProfiledRevision,
                path,
                $"{path}#t026",
                templateAnchor.LineStart,
                templateAnchor.LineEnd,
                templateAnchor.SourceHash,
                createdAt));
            context.LogicalDocuments.Add(logicalDocument);
            await context.SaveChangesAsync();
            context.DocumentVersions.Add(document);
            await context.SaveChangesAsync();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"vulgata\".\"LogicalDocuments\" SET \"CurrentVersionId\" = {documentVersionId} WHERE \"Id\" = {logicalDocumentId}");
            context.IndexEntries.Add(IndexEntry.Create(
                Guid.NewGuid(),
                logicalDocumentId,
                documentVersionId,
                template.RepositoryId,
                template.SystemId,
                DocumentType.CodeLogic,
                $"T026 external additional document {index + 1:D2}",
                "T026 additional-document bound fixture",
                path,
                "[]",
                "[]",
                "[]",
                queries[index],
                createdAt));
        }

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return queries;
    }

    private static async Task<string> AddFifthSectionAsync(VulgataDbContext context)
    {
        FlowTraceStep step = await context.FlowTraceSteps.AsNoTracking()
            .Where(value => value.DocumentVersionId != Guid.Empty)
            .OrderBy(value => value.DocumentVersionId)
            .ThenBy(value => value.StepOrder)
            .FirstAsync();
        string query = $"t026-fifth-section-{step.DocumentVersionId:N}";
        if (!await context.DocumentSections.AnyAsync(value =>
                value.DocumentVersionId == step.DocumentVersionId && value.SectionOrder == 5))
        {
            context.DocumentSections.Add(DocumentSection.Create(
                Guid.NewGuid(),
                (await context.DocumentVersions.AsNoTracking()
                    .SingleAsync(value => value.Id == step.DocumentVersionId)).LogicalDocumentId,
                step.DocumentVersionId,
                step.DocumentRepositoryId,
                Guid.NewGuid(),
                5,
                DocumentSectionKind.TechnicalDetail,
                "T026 fifth section",
                "T026 fifth section must be bounded",
                query,
                JsonSerializer.Serialize(Array.Empty<Guid>()),
                JsonSerializer.Serialize(Array.Empty<Guid>()),
                DateTimeOffset.UtcNow));
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }

        return query;
    }

    private static async Task<string> SelectUniqueDocumentQueryAsync(VulgataDbContext context)
    {
        IndexEntry entry = await context.IndexEntries.AsNoTracking()
            .Where(value => value.DocumentType == DocumentType.CodeLogic)
            .OrderBy(value => value.DocumentVersionId)
            .ThenBy(value => value.Id)
            .FirstAsync();
        return entry.Title;
    }

    private static IPass2CodeLogicRetrievalService CreateService(
        VulgataDbContext context,
        RepositoryAccessScope scope)
    {
        return CreateConcreteService(context, scope);
    }

    private static Pass2CodeLogicRetrievalService CreateConcreteService(
        VulgataDbContext context,
        RepositoryAccessScope scope)
    {
        return new Pass2CodeLogicRetrievalService(context, scope, new SuccessfulSourceReader());
    }

    private static void AssertEquivalent(
        Pass2CodeLogicRetrievalItem expected,
        Pass2CodeLogicRetrievalItem actual)
    {
        Assert.Equal(expected.OpaqueHandle, actual.OpaqueHandle);
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.DocumentVersionId, actual.DocumentVersionId);
        Assert.Equal(expected.ReturnedUtf8Bytes, actual.ReturnedUtf8Bytes);
        Assert.Equal(expected.SectionId, actual.SectionId);
        Assert.Equal(expected.SourceAnchorId, actual.SourceAnchorId);
        Assert.Equal(expected.EdgeId, actual.EdgeId);
        Assert.Equal(expected.ByteStart, actual.ByteStart);
        Assert.Equal(expected.ByteEnd, actual.ByteEnd);
    }

    private static int SolveExcerptPayloadForProviderBudget(
        int providerBudget,
        Guid documentVersionId,
        Guid sourceAnchorId)
    {
        int payloadBytes = Math.Max(1, providerBudget);
        for (int iteration = 0; iteration < 32; iteration++)
        {
            int serializedBytes = ProviderVisibleExcerptBytes(
                documentVersionId,
                sourceAnchorId,
                payloadBytes);
            int correction = providerBudget - serializedBytes;
            if (correction == 0)
            {
                return payloadBytes;
            }

            payloadBytes = checked(payloadBytes + correction);
            if (payloadBytes < 1)
            {
                break;
            }
        }

        throw new InvalidOperationException(
            "The independent provider-visible excerpt serialization did not converge.");
    }

    private static int ProviderVisibleExcerptBytes(
        Guid documentVersionId,
        Guid sourceAnchorId,
        int excerptPayloadBytes) =>
        Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(new
        {
            opaqueHandle = Guid.Empty.ToString("D"),
            kind = Pass2CodeLogicRetrievalItemKind.SourceExcerpt.ToString(),
            documentVersionId,
            sectionId = (Guid?)null,
            sourceAnchorId,
            edgeId = (Guid?)null,
            byteStart = 0L,
            byteEnd = (long)excerptPayloadBytes,
            contentMarkdown = (string?)null,
            excerpt = new string('x', excerptPayloadBytes),
        }));

    private static VulgataDbContext CreateContext(string connectionString)
    {
        DbContextOptions<VulgataDbContext> options = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                "__EFMigrationsHistory",
                "public"))
            .Options;
        return new VulgataDbContext(options);
    }

    private static async Task AssertDatabaseDroppedAsync(
        string administratorConnectionString,
        string databaseName)
    {
        NpgsqlConnectionStringBuilder administrator = new(administratorConnectionString)
        {
            Pooling = false,
            Timeout = 5,
            CommandTimeout = 15,
        };
        await using NpgsqlConnection connection = new(administrator.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);",
            connection);
        command.Parameters.AddWithValue("name", databaseName);
        bool exists = (bool)(await command.ExecuteScalarAsync() ?? false);
        Assert.False(exists, $"Owned disposable database '{databaseName}' was not dropped.");
    }

    private sealed class AlwaysAllowAuthorizationVerifier : IRepositoryAuthorizationVerifier
    {
        public ValueTask<bool> HasCurrentAccessAsync(
            IReadOnlyCollection<Guid> requiredRepositoryIds,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(true);
        }
    }

    private sealed class SuccessfulSourceReader : IRevisionBoundSourceReader
    {
        public Task<SourceExcerptResult> ReadAsync(
            AnalysisScope scope,
            RevisionBoundSourceRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string excerpt = new('x', request.ByteLimit);
            return Task.FromResult(new SourceExcerptResult(
                AnalysisOperationStatus.Succeeded,
                scope.RepositoryId,
                scope.ProfiledRevision,
                request.RelativePath,
                request.Range,
                request.ExpectedSourceSha256,
                excerpt,
                excerpt.Length,
                null));
        }
    }

    private sealed class CountingSuccessfulSourceReader : IRevisionBoundSourceReader
    {
        internal int ReadCalls { get; private set; }

        public Task<SourceExcerptResult> ReadAsync(
            AnalysisScope scope,
            RevisionBoundSourceRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadCalls++;
            string excerpt = new('x', request.ByteLimit);
            return Task.FromResult(new SourceExcerptResult(
                AnalysisOperationStatus.Succeeded,
                scope.RepositoryId,
                scope.ProfiledRevision,
                request.RelativePath,
                request.Range,
                request.ExpectedSourceSha256,
                excerpt,
                excerpt.Length,
                null));
        }
    }

    private sealed class ThrowingSourceReader : IRevisionBoundSourceReader
    {
        internal int ReadCalls { get; private set; }

        public Task<SourceExcerptResult> ReadAsync(
            AnalysisScope scope,
            RevisionBoundSourceRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadCalls++;
            throw new InvalidOperationException("intentional T026 post-reservation source failure");
        }
    }
}
