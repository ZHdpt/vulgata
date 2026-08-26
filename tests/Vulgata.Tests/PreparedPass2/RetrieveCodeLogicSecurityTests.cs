using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests.PreparedPass2;

/// <summary>
/// T025 security/currentness REDs use the accepted T026 authoritative pair
/// fixture and mutate one persisted fact per case. Handle-dependent vectors
/// use handles issued by the valid persisted fixture before each mutation.
/// </summary>
public sealed class RetrieveCodeLogicSecurityTests
{
    [Theory]
    [InlineData("stale")]
    [InlineData("superseded")]
    [InlineData("cross-system")]
    [InlineData("profiled-revision")]
    public async Task RetrieveAsync_PersistedSecurityMutation_ReturnsClosedRefusalWithoutAuthorityMutationAsync(
        string mutation)
    {
        await using PersistedRetrievalFixture fixture =
            await PersistedRetrievalFixture.CreateAsync(mutation);

        string authorityBefore = await fixture.AuthoritySnapshotAsync();
        string persistedBefore = await fixture.MutationSnapshotAsync();
        await fixture.ApplyMutationAsync();
        string persistedAfter = await fixture.MutationSnapshotAsync();
        Assert.NotEqual(persistedBefore, persistedAfter);
        string mutatedFixture = await fixture.AuthoritySnapshotAsync();
        Assert.Equal(authorityBefore, mutatedFixture);
        Pass2CodeLogicRetrievalResult result = await fixture.Service.RetrieveAsync(
            fixture.JobExecutionId,
            new(
                Pass2CodeLogicRetrievalOperation.Search,
                queryText: "ledger"));

        string authorityAfter = await fixture.AuthoritySnapshotAsync();
        Assert.Equal(authorityBefore, authorityAfter);
        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, result.Outcome);
        Assert.Empty(result.Items);
        Assert.DoesNotContain(mutation, result.Code, StringComparison.Ordinal);
        Assert.Equal(0, fixture.SourceReader.Calls);
        Assert.Equal("0:0:0:0:0:0:0", await fixture.RetrievalAccountingSnapshotAsync());
    }

    [Fact]
    public async Task RetrieveAsync_RevokedServerScope_ReturnsClosedRefusalWithoutAuthorityMutationAsync()
    {
        await using PersistedRetrievalFixture fixture =
            await PersistedRetrievalFixture.CreateAsync("revoked-scope");

        string authorityBefore = await fixture.AuthoritySnapshotAsync();
        Pass2CodeLogicRetrievalResult result = await fixture.ServiceWithRevokedScope
            .RetrieveAsync(
                fixture.JobExecutionId,
                new(
                    Pass2CodeLogicRetrievalOperation.Search,
                    queryText: "ledger"));

        Assert.Equal(authorityBefore, await fixture.AuthoritySnapshotAsync());
        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, result.Outcome);
        Assert.Empty(result.Items);
        Assert.Equal(0, fixture.SourceReader.Calls);
    }

    [Fact]
    public async Task RetrieveAsync_UnverifiedServerScope_ReturnsClosedRefusalWithoutAuthorityMutationAsync()
    {
        await using PersistedRetrievalFixture fixture =
            await PersistedRetrievalFixture.CreateAsync("none");
        RepositoryAccessScope unverifiedScope =
            new(fixture.Scope.AuthorizedRepositoryIds);
        var service = new Pass2CodeLogicRetrievalService(
            fixture.Context,
            unverifiedScope,
            fixture.SourceReader);
        string authorityBefore = await fixture.AuthoritySnapshotAsync();
        string accountingBefore = await fixture.RetrievalAccountingSnapshotAsync();

        Pass2CodeLogicRetrievalResult result = await service.RetrieveAsync(
            fixture.JobExecutionId,
            new(
                Pass2CodeLogicRetrievalOperation.Search,
                queryText: "ledger"));

        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, result.Outcome);
        Assert.Empty(result.Items);
        Assert.DoesNotContain("ledger", result.Code, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, fixture.SourceReader.Calls);
        Assert.Equal(authorityBefore, await fixture.AuthoritySnapshotAsync());
        Assert.Equal(accountingBefore, await fixture.RetrievalAccountingSnapshotAsync());
    }

    [Fact]
    public async Task RetrieveAsync_HandleIssuedForDifferentJob_IsContentDarkRefusalAsync()
    {
        await using PersistedRetrievalFixture owner =
            await PersistedRetrievalFixture.CreateAsync("none");
        await using PersistedRetrievalFixture otherJob =
            await PersistedRetrievalFixture.CreateAsync("cross-job");

        Pass2CodeLogicRetrievalItem section = await owner.IssueSectionHandleAsync();
        Guid otherJobId = await otherJob.Context.Pass2JobExecutions
            .Where(value => value.Id != otherJob.JobExecutionId)
            .Select(value => value.Id)
            .SingleAsync();
        Pass2CodeLogicRetrievalResult targetProbe = await otherJob.Service.RetrieveAsync(
            otherJobId,
            new(Pass2CodeLogicRetrievalOperation.Search, queryText: "ledger"));
        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, targetProbe.Outcome);
        string accountingBefore = await otherJob.RetrievalAccountingSnapshotAsync(otherJobId);
        Pass2CodeLogicRetrievalResult result = await otherJob.Service.RetrieveAsync(
            otherJobId,
            new(
                Pass2CodeLogicRetrievalOperation.Sections,
                opaqueHandles: [section.OpaqueHandle]));

        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, result.Outcome);
        Assert.Empty(result.Items);
        Assert.DoesNotContain(section.OpaqueHandle, result.Code, StringComparison.Ordinal);
        Assert.Equal(0, otherJob.SourceReader.Calls);
        string accountingAfter = await otherJob.RetrievalAccountingSnapshotAsync(otherJobId);
        AssertReservedRefusalAccounting(accountingBefore, accountingAfter);
        Assert.Equal(
            Pass2RetrievalReceiptState.Refused,
            await otherJob.LatestReceiptStateAsync(otherJobId));
        AssertCurrentRemainingBudget(result, accountingAfter);
    }

    [Fact]
    public async Task RetrieveAsync_IssuedAnchorWithPersistedHashMismatch_IsContentDarkRefusalAsync()
    {
        await using PersistedRetrievalFixture fixture =
            await PersistedRetrievalFixture.CreateAsync("none");
        Pass2CodeLogicRetrievalItem anchor = await fixture.IssueAnchorHandleAsync();
        string originalHash = await fixture.Context.SourceAnchors
            .Where(value => value.Id == anchor.SourceAnchorId)
            .Select(value => value.SourceHash)
            .SingleAsync();
        fixture.SourceReader.FixedSourceHash = originalHash;
        string anchorBefore = await fixture.AnchorSnapshotAsync(anchor.SourceAnchorId!.Value);

        await fixture.Context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"SourceAnchors\" SET \"SourceHash\" = {new string('c', 64)} WHERE \"Id\" = {anchor.SourceAnchorId}");
        fixture.Context.ChangeTracker.Clear();

        string anchorAfter = await fixture.AnchorSnapshotAsync(anchor.SourceAnchorId.Value);
        Assert.NotEqual(anchorBefore, anchorAfter);
        string authorityBefore = await fixture.AuthoritySnapshotAsync();
        string accountingBefore = await fixture.RetrievalAccountingSnapshotAsync();
        Pass2CodeLogicRetrievalResult result = await fixture.Service.RetrieveAsync(
            fixture.JobExecutionId,
            new(
                Pass2CodeLogicRetrievalOperation.SourceExcerpt,
                opaqueHandles: [anchor.OpaqueHandle],
                containedByteOffset: 0,
                containedByteLength: 6));

        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, result.Outcome);
        Assert.Empty(result.Items);
        Assert.Equal(0, fixture.SourceReader.Calls);
        Assert.DoesNotContain("ledger", result.Code, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(authorityBefore, await fixture.AuthoritySnapshotAsync());
        string accountingAfter = await fixture.RetrievalAccountingSnapshotAsync();
        await AssertReservedRefusalAsync(fixture, accountingBefore, accountingAfter, result);
    }

    [Fact]
    public async Task RetrieveAsync_IssuedAnchorWithPersistedRangeMismatch_IsContentDarkRefusalAsync()
    {
        await using PersistedRetrievalFixture fixture =
            await PersistedRetrievalFixture.CreateAsync("none");
        Pass2CodeLogicRetrievalItem anchor = await fixture.IssueAnchorHandleAsync();
        string anchorBefore = await fixture.AnchorSnapshotAsync(anchor.SourceAnchorId!.Value);

        await fixture.Context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"SourceAnchors\" SET \"ByteStart\" = 10, \"ByteEnd\" = 16 WHERE \"Id\" = {anchor.SourceAnchorId}");
        fixture.Context.ChangeTracker.Clear();

        string anchorAfter = await fixture.AnchorSnapshotAsync(anchor.SourceAnchorId.Value);
        Assert.NotEqual(anchorBefore, anchorAfter);
        string authorityBefore = await fixture.AuthoritySnapshotAsync();
        string accountingBefore = await fixture.RetrievalAccountingSnapshotAsync();
        Pass2CodeLogicRetrievalResult result = await fixture.Service.RetrieveAsync(
            fixture.JobExecutionId,
            new(
                Pass2CodeLogicRetrievalOperation.SourceExcerpt,
                opaqueHandles: [anchor.OpaqueHandle],
                containedByteOffset: 0,
                containedByteLength: 6));

        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, result.Outcome);
        Assert.Empty(result.Items);
        Assert.Equal(0, fixture.SourceReader.Calls);
        Assert.Equal(authorityBefore, await fixture.AuthoritySnapshotAsync());
        string accountingAfter = await fixture.RetrievalAccountingSnapshotAsync();
        await AssertReservedRefusalAsync(fixture, accountingBefore, accountingAfter, result);
    }

    [Fact]
    public async Task RetrieveAsync_RelatedDocuments_StopsAtOneReferencesHopAndExcludesNonReferencesAsync()
    {
        await using PersistedRetrievalFixture fixture =
            await PersistedRetrievalFixture.CreateAsync("none");
        Pass2CodeLogicRetrievalItem document = await fixture.IssueDocumentHandleAsync();
        Guid implementationId = await fixture.Context.FlowGroups
            .Where(value => value.Pass2ExecutionId == fixture.ExecutionId)
            .Select(value => value.ProviderImplementationDocumentVersionId!.Value)
            .SingleAsync();
        DocumentVersion consumer = await fixture.Context.DocumentVersions
            .SingleAsync(value => value.Id == document.DocumentVersionId);
        DocumentVersion implementation = await fixture.Context.DocumentVersions
            .SingleAsync(value => value.Id == implementationId);
        DocumentEdge nonReference = new(
            ExactCrossRepositoryTestValues.Id(41_001),
            consumer.Id,
            consumer.RepositoryId,
            consumer.ScanRunId,
            implementation.Id,
            implementation.RepositoryId,
            implementation.ScanRunId,
            DocumentEdgeType.GeneratedFrom,
            ExactCrossRepositoryTestValues.Pass1CompletedAt.AddSeconds(1));
        fixture.Context.DocumentEdges.Add(nonReference);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        string authorityBefore = await fixture.AuthoritySnapshotAsync();

        Pass2CodeLogicRetrievalResult result = await fixture.Service.RetrieveAsync(
            fixture.JobExecutionId,
            new(
                Pass2CodeLogicRetrievalOperation.RelatedDocuments,
                opaqueHandles: [document.OpaqueHandle],
                direction: Pass2RelatedDocumentDirection.Outgoing));

        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, result.Outcome);
        Pass2CodeLogicRetrievalItem related = Assert.Single(result.Items);
        Assert.Equal(Pass2CodeLogicRetrievalItemKind.Relationship, related.Kind);
        Assert.NotEqual(implementationId, related.DocumentVersionId);
        Assert.DoesNotContain(result.Items, value => value.DocumentVersionId == implementationId);
        Assert.Equal(authorityBefore, await fixture.AuthoritySnapshotAsync());
    }

    [Fact]
    public async Task RetrieveAsync_UnknownUnauthorizedAndStaleRequestsHaveEquivalentContentDarkRefusalAsync()
    {
        await using PersistedRetrievalFixture owner =
            await PersistedRetrievalFixture.CreateAsync("none");
        await using PersistedRetrievalFixture stale =
            await PersistedRetrievalFixture.CreateAsync("stale");
        Pass2CodeLogicRetrievalItem issued = await owner.IssueSectionHandleAsync();
        await stale.ApplyMutationAsync();
        string ownerAccountingBefore = await owner.RetrievalAccountingSnapshotAsync();
        string staleAccountingBefore = await stale.RetrievalAccountingSnapshotAsync();

        Pass2CodeLogicRetrievalResult unknown = await owner.Service.RetrieveAsync(
            owner.JobExecutionId,
            new(
                Pass2CodeLogicRetrievalOperation.Sections,
                opaqueHandles: ["opaque-handle-never-issued"]));
        Pass2CodeLogicRetrievalResult unauthorized = await stale.Service.RetrieveAsync(
            stale.JobExecutionId,
            new(
                Pass2CodeLogicRetrievalOperation.Sections,
                opaqueHandles: [issued.OpaqueHandle]));

        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, unknown.Outcome);
        Assert.Equal(unknown.Outcome, unauthorized.Outcome);
        Assert.Equal(unknown.Code, unauthorized.Code);
        Assert.Empty(unknown.Items);
        Assert.Empty(unauthorized.Items);
        Assert.DoesNotContain("opaque-handle-never-issued", unknown.Code, StringComparison.Ordinal);
        Assert.DoesNotContain(issued.OpaqueHandle, unauthorized.Code, StringComparison.Ordinal);
        string ownerAccountingAfter = await owner.RetrievalAccountingSnapshotAsync();
        AssertReservedRefusalAccounting(ownerAccountingBefore, ownerAccountingAfter);
        Assert.Equal(Pass2RetrievalReceiptState.Refused, await owner.LatestReceiptStateAsync());
        Assert.Equal(staleAccountingBefore, await stale.RetrievalAccountingSnapshotAsync());
    }

    [Fact]
    public async Task RetrieveAsync_UnknownOpaqueSelector_ConsumesCallAndPersistsContentDarkRefusalAsync()
    {
        await using PersistedRetrievalFixture fixture =
            await PersistedRetrievalFixture.CreateAsync("none");
        string before = await fixture.RetrievalAccountingSnapshotAsync();

        Pass2CodeLogicRetrievalResult result = await fixture.Service.RetrieveAsync(
            fixture.JobExecutionId,
            new(
                Pass2CodeLogicRetrievalOperation.Sections,
                opaqueHandles: ["opaque-handle-never-issued"]));

        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, result.Outcome);
        Assert.Empty(result.Items);
        Assert.DoesNotContain("opaque-handle-never-issued", result.Code, StringComparison.Ordinal);
        Assert.Equal(0, fixture.SourceReader.Calls);

        string after = await fixture.RetrievalAccountingSnapshotAsync();
        AssertReservedRefusalAccounting(before, after);
        Assert.Equal(Pass2RetrievalReceiptState.Refused, await fixture.LatestReceiptStateAsync());
        AssertCurrentRemainingBudget(result, after);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("RetryScheduled")]
    [InlineData("Completed")]
    [InlineData("Failed")]
    public async Task RetrieveAsync_NonRunningJobState_IsContentDarkRefusalAsync(string state)
    {
        await using PersistedRetrievalFixture fixture =
            await PersistedRetrievalFixture.CreateAsync("none");

        await fixture.SetJobStateAsync(state);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(
            Enum.Parse<Pass2JobExecutionState>(state),
            await fixture.Context.Pass2JobExecutions
                .Where(value => value.Id == fixture.JobExecutionId)
                .Select(value => value.State)
                .SingleAsync());
        string accountingBefore = await fixture.RetrievalAccountingSnapshotAsync();

        Pass2CodeLogicRetrievalResult result = await fixture.Service.RetrieveAsync(
            fixture.JobExecutionId,
            new(Pass2CodeLogicRetrievalOperation.Search, queryText: "ledger"));

        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, result.Outcome);
        Assert.Empty(result.Items);
        Assert.DoesNotContain("ledger", result.Code, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, fixture.SourceReader.Calls);
        string accountingAfter = await fixture.RetrievalAccountingSnapshotAsync();
        Assert.Equal(accountingBefore, accountingAfter);
    }

    [Fact]
    public async Task RetrieveAsync_HandleIssuedBeforeAttemptAdvance_IsContentDarkRefusalAsync()
    {
        await using PersistedRetrievalFixture fixture =
            await PersistedRetrievalFixture.CreateAsync("none");
        Pass2CodeLogicRetrievalItem section = await fixture.IssueSectionHandleAsync();

        await fixture.Context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"Pass2JobExecutions\" SET \"AttemptCount\" = 2 WHERE \"Id\" = {fixture.JobExecutionId}");
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(
            2,
            await fixture.Context.Pass2JobExecutions
                .Where(value => value.Id == fixture.JobExecutionId)
                .Select(value => value.AttemptCount)
                .SingleAsync());
        string accountingBefore = await fixture.RetrievalAccountingSnapshotAsync();

        Pass2CodeLogicRetrievalResult result = await fixture.Service.RetrieveAsync(
            fixture.JobExecutionId,
            new(
                Pass2CodeLogicRetrievalOperation.Sections,
                opaqueHandles: [section.OpaqueHandle]));

        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, result.Outcome);
        Assert.Empty(result.Items);
        Assert.DoesNotContain(section.OpaqueHandle, result.Code, StringComparison.Ordinal);
        Assert.Equal(0, fixture.SourceReader.Calls);
        string accountingAfter = await fixture.RetrievalAccountingSnapshotAsync();
        await AssertReservedRefusalAsync(fixture, accountingBefore, accountingAfter, result);
    }

    [Fact]
    public void Pass2JobExecution_ExposesLeaseAndAttemptAdmissionIdentity()
    {
        Type jobType = typeof(Pass2JobExecution);
        Assert.NotNull(jobType.GetProperty(nameof(Pass2JobExecution.AttemptCount)));
        Assert.NotNull(jobType.GetProperty(nameof(Pass2JobExecution.StateVersion)));
        foreach (string propertyName in new[]
                 {
                     "LeaseToken",
                     "LeaseExpiresAt",
                     "NextAttemptAt",
                     "FailureCategory",
                     "FailureCode",
                     "StateChangedAt",
                 })
        {
            Assert.True(
                jobType.GetProperty(propertyName) is not null,
                $"Pass2JobExecution must expose {propertyName} for retrieval admission.");
        }
    }

    [Fact]
    public async Task RetrieveAsync_CurrentnessChangesBetweenSourceReadAndAdmission_IsRefusedWithConsumedReservationAsync()
    {
        await using PersistedRetrievalFixture fixture =
            await PersistedRetrievalFixture.CreateAsync("none");
        Pass2CodeLogicRetrievalItem anchor = await fixture.IssueAnchorHandleAsync();
        Guid logicalDocumentId = await fixture.Context.DocumentVersions
            .Where(value => value.Id == anchor.DocumentVersionId)
            .Select(value => value.LogicalDocumentId)
            .SingleAsync();
        fixture.SourceReader.BeforeReadAsync = async () =>
        {
            await fixture.Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = NULL WHERE \"Id\" = {logicalDocumentId}");
            fixture.Context.ChangeTracker.Clear();
        };
        string before = await fixture.RetrievalAccountingSnapshotAsync();

        Pass2CodeLogicRetrievalResult result = await fixture.Service.RetrieveAsync(
            fixture.JobExecutionId,
            new(
                Pass2CodeLogicRetrievalOperation.SourceExcerpt,
                opaqueHandles: [anchor.OpaqueHandle],
                containedByteOffset: 0,
                containedByteLength: 6));

        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, result.Outcome);
        Assert.Empty(result.Items);
        Assert.Equal(1, fixture.SourceReader.Calls);
        string after = await fixture.RetrievalAccountingSnapshotAsync();
        await AssertReservedRefusalAsync(fixture, before, after, result);
    }

    [Fact]
    public async Task PreparedPass2Store_AdmissionCurrentnessChangesAfterProjection_IsRefusedWithoutAdmissionMutationAsync()
    {
        await using PersistedRetrievalFixture fixture =
            await PersistedRetrievalFixture.CreateAsync("none");
        Pass2CodeLogicRetrievalRequest request = new(
            Pass2CodeLogicRetrievalOperation.Search,
            queryText: "ledger");
        Pass2CodeLogicRetrievalResult candidate = await fixture.Service.RetrieveAsync(
            fixture.JobExecutionId,
            request);
        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, candidate.Outcome);
        Assert.NotEmpty(candidate.Items);

        Pass2RetrievalReservation reservation = await new PreparedPass2Store(fixture.Context)
            .ReserveRetrievalAsync(fixture.JobExecutionId, request, CancellationToken.None);
        Assert.True(reservation.IsReserved);
        string reservedAccounting = await fixture.RetrievalAccountingSnapshotAsync();
        Pass2CodeLogicRetrievalItem projected = candidate.Items[0];
        Guid logicalDocumentId = await fixture.Context.DocumentVersions
            .Where(value => value.Id == projected.DocumentVersionId)
            .Select(value => value.LogicalDocumentId)
            .SingleAsync();
        Guid? currentBefore = await fixture.Context.LogicalDocuments
            .Where(value => value.Id == logicalDocumentId)
            .Select(value => value.CurrentVersionId)
            .SingleAsync();
        Assert.NotNull(currentBefore);

        await fixture.Context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = NULL WHERE \"Id\" = {logicalDocumentId}");
        fixture.Context.ChangeTracker.Clear();
        Assert.Null(await fixture.Context.LogicalDocuments
            .Where(value => value.Id == logicalDocumentId)
            .Select(value => value.CurrentVersionId)
            .SingleAsync());

        Guid[] initialDocumentVersionIds = await fixture.InitialDocumentVersionIdsAsync();
        Pass2CodeLogicRetrievalResult result = await new PreparedPass2Store(fixture.Context)
            .AdmitRetrievalAsync(
                fixture.JobExecutionId,
                reservation,
                request,
                candidate,
                initialDocumentVersionIds,
                CancellationToken.None);

        string admittedAccounting = await fixture.RetrievalAccountingSnapshotAsync();
        Assert.Equal(reservedAccounting, admittedAccounting);
        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, result.Outcome);
        Assert.Empty(result.Items);
    }

    [Theory]
    [InlineData("source-anchor-hash")]
    [InlineData("source-anchor-range")]
    public async Task RetrieveAsync_CandidateEvidenceChangesAfterProjectionBeforeAdmission_IsRefusedWithoutAcceptedMutationAsync(
        string mutation)
    {
        await using PersistedRetrievalFixture fixture =
            await PersistedRetrievalFixture.CreateAsync("none");
        Pass2CodeLogicRetrievalItem anchor = await fixture.IssueAnchorHandleAsync();
        string anchorBefore = await fixture.AnchorSnapshotAsync(anchor.SourceAnchorId!.Value);
        string authorityBefore = await fixture.AuthoritySnapshotAsync();
        string accountingBefore = await fixture.RetrievalAccountingSnapshotAsync();
        fixture.SourceReader.Reset();
        fixture.AccessVerifier.ArmAdmissionMutation(
            () => fixture.ApplyAdmissionMutationAsync(
                mutation,
                anchor.SourceAnchorId.Value,
                anchor.DocumentVersionId),
            expectedAuthorizationChecks: 3);

        Pass2CodeLogicRetrievalResult result = await fixture.Service.RetrieveAsync(
            fixture.JobExecutionId,
            new(
                Pass2CodeLogicRetrievalOperation.SourceExcerpt,
                opaqueHandles: [anchor.OpaqueHandle],
                containedByteOffset: 0,
                containedByteLength: 6));

        Assert.True(fixture.AccessVerifier.AdmissionMutationInvoked);
        Assert.NotEqual(anchorBefore, await fixture.AnchorSnapshotAsync(anchor.SourceAnchorId.Value));
        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, result.Outcome);
        Assert.Empty(result.Items);
        Assert.Equal(1, fixture.SourceReader.Calls);
        Assert.Equal(authorityBefore, await fixture.AuthoritySnapshotAsync());
        await AssertReservedRefusalAsync(
            fixture,
            accountingBefore,
            await fixture.RetrievalAccountingSnapshotAsync(),
            result);
    }

    [Theory]
    [InlineData("live-authorization")]
    [InlineData("pair")]
    [InlineData("system")]
    [InlineData("repository")]
    [InlineData("scan-run")]
    [InlineData("profiled-revision")]
    [InlineData("document-fingerprint")]
    [InlineData("source-anchor-hash")]
    [InlineData("source-anchor-range")]
    public async Task RetrieveAsync_AuthorityChangesBetweenSourceReadAndAdmission_IsRefusedWithoutAcceptedMutationAsync(
        string mutation)
    {
        await using PersistedRetrievalFixture fixture =
            await PersistedRetrievalFixture.CreateAsync("none");
        Pass2CodeLogicRetrievalItem anchor = await fixture.IssueAnchorHandleAsync();
        string before = await fixture.RetrievalAccountingSnapshotAsync();
        fixture.SourceReader.BeforeReadAsync = () => fixture.ApplyAdmissionMutationAsync(
            mutation,
            anchor.SourceAnchorId!.Value,
            anchor.DocumentVersionId);

        Pass2CodeLogicRetrievalResult result = await fixture.Service.RetrieveAsync(
            fixture.JobExecutionId,
            new(
                Pass2CodeLogicRetrievalOperation.SourceExcerpt,
                opaqueHandles: [anchor.OpaqueHandle],
                containedByteOffset: 0,
                containedByteLength: 6));

        Assert.Equal(Pass2CodeLogicRetrievalOutcome.Refused, result.Outcome);
        Assert.Empty(result.Items);
        Assert.DoesNotContain("ledger", result.Code, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, fixture.SourceReader.Calls);
        string after = await fixture.RetrievalAccountingSnapshotAsync();
        AssertReservedRefusalAccounting(before, after);
        Assert.Equal(Pass2RetrievalReceiptState.Refused, await fixture.LatestReceiptStateAsync());
        AssertCurrentRemainingBudget(result, after);
    }

    [Fact]
    public void RetrieveCodeLogic_RequestCannotSupplyAuthorityMutationFields()
    {
        string[] forbiddenFragments = ["Provider", "Group", "System", "Repository", "Revision"];
        Assert.DoesNotContain(
            typeof(Pass2CodeLogicRetrievalRequest).GetProperties(),
            property => forbiddenFragments.Any(fragment =>
                property.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase)));
    }

    private sealed class PersistedRetrievalFixture : IAsyncDisposable
    {
        private readonly string _mutation;

        private PersistedRetrievalFixture(
            ExactCrossRepositoryTestFixture fixture,
            VulgataDbContext context,
            Guid jobExecutionId,
            Guid executionId,
            RepositoryAccessScope scope,
            AllowAccessVerifier accessVerifier,
            string mutation)
        {
            Fixture = fixture;
            Context = context;
            JobExecutionId = jobExecutionId;
            ExecutionId = executionId;
            Scope = scope;
            AccessVerifier = accessVerifier;
            _mutation = mutation;
            SourceReader = new RecordingSourceReader();
            Service = new Pass2CodeLogicRetrievalService(Context, Scope, SourceReader);
            ServiceWithRevokedScope = new Pass2CodeLogicRetrievalService(
                Context,
                new RepositoryAccessScope(Scope.AuthorizedRepositoryIds, new DeniedAccessVerifier()),
                SourceReader);
        }

        internal ExactCrossRepositoryTestFixture Fixture { get; }
        internal VulgataDbContext Context { get; }
        internal Guid JobExecutionId { get; }
        internal Guid ExecutionId { get; }
        internal RepositoryAccessScope Scope { get; }
        internal AllowAccessVerifier AccessVerifier { get; }
        internal RecordingSourceReader SourceReader { get; }
        internal IPass2CodeLogicRetrievalService Service { get; }
        internal IPass2CodeLogicRetrievalService ServiceWithRevokedScope { get; }

        internal static async Task<PersistedRetrievalFixture> CreateAsync(string mutation)
        {
            ExactCrossRepositoryTestFixture fixture =
                await ExactCrossRepositoryTestFixture.CreateAsync();
            VulgataDbContext context = fixture.Database.CreateContext();
            try
            {
                MethodInfo seedMethod = typeof(Pass2FlowPlanningPostgreSqlTests)
                    .GetMethod("SeedPass2Async", BindingFlags.Static | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("The authoritative T026 seed is unavailable.");
                object invocation = seedMethod.Invoke(null, [context, fixture])
                    ?? throw new InvalidOperationException("The authoritative T026 seed returned null.");
                if (invocation is not Task seedTask)
                {
                    throw new InvalidOperationException("The authoritative T026 seed is not asynchronous.");
                }

                await seedTask;
                object seed = seedTask.GetType().GetProperty("Result")?.GetValue(seedTask)
                    ?? throw new InvalidOperationException("The authoritative T026 seed result is unavailable.");
                Pass1PairExecution pair = (Pass1PairExecution)GetRequiredProperty(seed, "Pair");
                RepositoryAccessScope scope = (RepositoryAccessScope)GetRequiredProperty(seed, "Scope");
                AllowAccessVerifier accessVerifier = new();
                RepositoryAccessScope liveScope = new(
                    scope.AuthorizedRepositoryIds,
                    accessVerifier);
                await AddSearchableSectionsAsync(context, seed);
                if (mutation == "cross-job")
                {
                    await AddAdditionalDecisionAsync(context, seed);
                }

                PreparedPass2StartResult start = await new PreparedPass2Store(context)
                    .StartAsync(pair.Id, liveScope);
                Assert.Equal(PreparedPass2StartOutcome.Created, start.Outcome);
                Pass2JobExecution[] jobs = await context.Pass2JobExecutions
                    .OrderBy(value => value.GroupOrder)
                    .ToArrayAsync();
                Assert.NotEmpty(jobs);
                for (int index = 0; index < jobs.Length; index++)
                {
                    DateTimeOffset stateChangedAt = jobs[index].StateChangedAt.AddSeconds(1);
                    jobs[index].AcquireLease(
                        ExactCrossRepositoryTestValues.Id(50_001 + index),
                        stateChangedAt.AddMinutes(5),
                        stateChangedAt);
                }
                await context.SaveChangesAsync();
                Pass2JobExecution job = jobs[0];
                Assert.All(
                    await context.Pass2JobExecutions.AsNoTracking().ToArrayAsync(),
                    value =>
                    {
                        Assert.Equal(Pass2JobExecutionState.Running, value.State);
                        Assert.Equal(1, value.AttemptCount);
                        Assert.NotNull(value.LeaseToken);
                        Assert.True(value.LeaseExpiresAt > value.StateChangedAt);
                    });
                Guid executionId = job.Pass2ExecutionId;
                Assert.Equal(1, await context.Pass2Executions.CountAsync());
                Assert.Equal(start.JobCount, await context.Pass2JobExecutions.CountAsync());
                Assert.Equal(start.RetrievalBudgetCount, await context.Pass2RetrievalBudgets.CountAsync());
                Assert.NotEmpty(await context.FlowGroups.ToArrayAsync());
                Assert.NotEmpty(await context.FlowTraces.ToArrayAsync());
                Assert.NotEmpty(await context.FlowTraceSteps.ToArrayAsync());
                Assert.NotEmpty(await context.DocumentVersions.ToArrayAsync());
                Assert.NotEmpty(await context.DocumentSections.ToArrayAsync());
                Assert.NotEmpty(await context.SourceAnchors.ToArrayAsync());
                Assert.NotEmpty(await context.DocumentEdges.ToArrayAsync());
                context.ChangeTracker.Clear();
                return new(fixture, context, job.Id, executionId, liveScope, accessVerifier, mutation);
            }
            catch
            {
                await context.DisposeAsync();
                await fixture.DisposeAsync();
                throw;
            }
        }

        internal async Task ApplyMutationAsync()
        {
            if (_mutation is "revoked-scope" or "none")
            {
                return;
            }

            Guid documentVersionId = await Context.FlowTraceSteps
                .Where(step => step.DocumentVersionId != Guid.Empty)
                .OrderBy(step => step.StepOrder)
                .Select(step => step.DocumentVersionId)
                .FirstAsync();

            switch (_mutation)
            {
                case "stale":
                {
                    Guid logicalDocumentId = await Context.DocumentVersions
                        .Where(version => version.Id == documentVersionId)
                        .Select(version => version.LogicalDocumentId)
                        .SingleAsync();
                    await Context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = NULL WHERE \"Id\" = {logicalDocumentId}");
                    break;
                }
                case "superseded":
                {
                    DocumentVersion current = await Context.DocumentVersions
                        .SingleAsync(version => version.Id == documentVersionId);
                    Guid successorId = ExactCrossRepositoryTestValues.Id(40_001);
                    DocumentVersion successor = DocumentVersion.Create(
                        successorId,
                        current.LogicalDocumentId,
                        current.RepositoryId,
                        current.SystemId,
                        current.ScanRunId,
                        current.ProfiledRevision,
                        current.VersionNumber + 1,
                        current.Title + " successor",
                        current.RepositoryRelativePath,
                        current.SchemaIdentity,
                        current.ContentJson,
                        current.ContentMarkdown,
                        new string('a', 64),
                        new string('b', 64),
                        current.CreatedAt.AddSeconds(1));
                    Context.DocumentVersions.Add(successor);
                    await Context.SaveChangesAsync();
                    await Context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE \"DocumentVersions\" SET \"SupersededById\" = {successorId} WHERE \"Id\" = {documentVersionId}");
                    break;
                }
                case "cross-system":
                {
                    Guid foreignSystemId = Guid.Parse("f0000000-0000-4000-8000-000000000009");
                    SystemEntity foreignSystem = new(
                        "T025 foreign system",
                        null,
                        null,
                        DateTimeOffset.UtcNow);
                    ExactCrossRepositoryReflection.SetId(foreignSystem, foreignSystemId);
                    Context.Systems.Add(foreignSystem);
                    await Context.SaveChangesAsync();
                    await Context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE \"Pass2Executions\" SET \"SystemId\" = {foreignSystemId} WHERE \"Id\" = (SELECT \"Pass2ExecutionId\" FROM \"Pass2JobExecutions\" WHERE \"Id\" = {JobExecutionId})");
                    break;
                }
                case "profiled-revision":
                    await Context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE \"DocumentVersions\" SET \"ProfiledRevision\" = {new string('f', 40)} WHERE \"Id\" = {documentVersionId}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(_mutation));
            }

            Context.ChangeTracker.Clear();
        }

        internal async Task<string> MutationSnapshotAsync()
        {
            if (_mutation is "revoked-scope" or "none")
            {
                return _mutation;
            }

            Guid documentVersionId = await Context.FlowTraceSteps
                .Where(step => step.DocumentVersionId != Guid.Empty)
                .OrderBy(step => step.StepOrder)
                .Select(step => step.DocumentVersionId)
                .FirstAsync();

            return _mutation switch
            {
                "stale" => (await Context.DocumentVersions
                    .Where(version => version.Id == documentVersionId)
                    .Select(version => version.LogicalDocumentId)
                    .Join(
                        Context.LogicalDocuments,
                        logicalDocumentId => logicalDocumentId,
                        logicalDocument => logicalDocument.Id,
                        (_, logicalDocument) => logicalDocument.CurrentVersionId)
                    .SingleAsync()).ToString() ?? string.Empty,
                "superseded" => (await Context.DocumentVersions
                    .Where(version => version.Id == documentVersionId)
                    .Select(version => version.SupersededById)
                    .SingleAsync()).ToString() ?? string.Empty,
                "profiled-revision" => await Context.DocumentVersions
                    .Where(version => version.Id == documentVersionId)
                    .Select(version => version.ProfiledRevision)
                    .SingleAsync(),
                "cross-system" => (await Context.Pass2JobExecutions
                    .Where(job => job.Id == JobExecutionId)
                    .Join(
                        Context.Pass2Executions,
                        job => job.Pass2ExecutionId,
                        execution => execution.Id,
                        (_, execution) => execution.SystemId)
                    .SingleAsync()).ToString(),
                _ => throw new ArgumentOutOfRangeException(nameof(_mutation)),
            };
        }

        internal async Task SetJobStateAsync(string state)
        {
            DateTimeOffset stateChangedAt = DateTimeOffset.UtcNow;
            string retryFailureCode = "t025-test-retry";
            string failedFailureCode = "t025-test-failed";
            switch (state)
            {
                case "Pending":
                    await Context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE \"Pass2JobExecutions\" SET \"State\" = {state}, \"LeaseToken\" = NULL, \"LeaseExpiresAt\" = NULL, \"NextAttemptAt\" = NULL, \"FailureCategory\" = NULL, \"FailureCode\" = NULL, \"CommittedDocumentVersionId\" = NULL, \"CompletionFingerprint\" = NULL, \"StateChangedAt\" = {stateChangedAt} WHERE \"Id\" = {JobExecutionId}");
                    break;
                case "RetryScheduled":
                    await Context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE \"Pass2JobExecutions\" SET \"State\" = {state}, \"LeaseToken\" = NULL, \"LeaseExpiresAt\" = NULL, \"NextAttemptAt\" = {stateChangedAt.AddMinutes(5)}, \"FailureCategory\" = {nameof(ScanJobFailureCategory.ProviderTransient)}, \"FailureCode\" = {retryFailureCode}, \"CommittedDocumentVersionId\" = NULL, \"CompletionFingerprint\" = NULL, \"StateChangedAt\" = {stateChangedAt} WHERE \"Id\" = {JobExecutionId}");
                    break;
                case "Completed":
                {
                    Guid committedDocumentVersionId = await Context.FlowTraceSteps
                        .Where(step => step.DocumentVersionId != Guid.Empty)
                        .Select(step => step.DocumentVersionId)
                        .FirstAsync();
                    await Context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE \"Pass2JobExecutions\" SET \"State\" = {state}, \"LeaseToken\" = NULL, \"LeaseExpiresAt\" = NULL, \"NextAttemptAt\" = NULL, \"FailureCategory\" = NULL, \"FailureCode\" = NULL, \"CommittedDocumentVersionId\" = {committedDocumentVersionId}, \"CompletionFingerprint\" = {new string('d', 64)}, \"StateChangedAt\" = {stateChangedAt} WHERE \"Id\" = {JobExecutionId}");
                    break;
                }
                case "Failed":
                    await Context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE \"Pass2JobExecutions\" SET \"State\" = {state}, \"LeaseToken\" = NULL, \"LeaseExpiresAt\" = NULL, \"NextAttemptAt\" = NULL, \"FailureCategory\" = {nameof(ScanJobFailureCategory.Security)}, \"FailureCode\" = {failedFailureCode}, \"CommittedDocumentVersionId\" = NULL, \"CompletionFingerprint\" = NULL, \"StateChangedAt\" = {stateChangedAt} WHERE \"Id\" = {JobExecutionId}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state));
            }
        }

        internal async Task ApplyAdmissionMutationAsync(
            string mutation,
            Guid sourceAnchorId,
            Guid documentVersionId)
        {
            switch (mutation)
            {
                case "live-authorization":
                    AccessVerifier.IsAllowed = false;
                    return;
                case "pair":
                    const string failedPairPhase = nameof(Pass1PairExecutionPhase.FailedSecurity);
                    await Context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE \"Pass1PairExecutions\" SET \"Phase\" = {failedPairPhase} WHERE \"Id\" = (SELECT \"Pass1PairExecutionId\" FROM \"Pass2Executions\" WHERE \"Id\" = (SELECT \"Pass2ExecutionId\" FROM \"Pass2JobExecutions\" WHERE \"Id\" = {JobExecutionId}))");
                    break;
                case "system":
                {
                    Guid foreignSystemId = Guid.Parse("f0000000-0000-4000-8000-000000000010");
                    SystemEntity foreignSystem = new(
                        "T025 admission drift system",
                        null,
                        null,
                        DateTimeOffset.UtcNow);
                    ExactCrossRepositoryReflection.SetId(foreignSystem, foreignSystemId);
                    Context.Systems.Add(foreignSystem);
                    await Context.SaveChangesAsync();
                    await Context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE \"Pass2Executions\" SET \"SystemId\" = {foreignSystemId} WHERE \"Id\" = (SELECT \"Pass2ExecutionId\" FROM \"Pass2JobExecutions\" WHERE \"Id\" = {JobExecutionId})");
                    break;
                }
                case "repository":
                {
                    Guid systemId = await Context.Pass2Executions
                        .Where(value => value.Id == ExecutionId)
                        .Select(value => value.SystemId)
                        .SingleAsync();
                    Guid foreignRepositoryId = Guid.Parse("a0000000-0000-4000-8000-000000000010");
                    Repository foreignRepository = Repository.Create(
                        systemId,
                        "T025 admission drift repository",
                        "https://example.test/t025-admission-drift-repository",
                        null,
                        null,
                        DateTimeOffset.UtcNow);
                    ExactCrossRepositoryReflection.SetId(foreignRepository, foreignRepositoryId);
                    Context.Repositories.Add(foreignRepository);
                    await Context.SaveChangesAsync();
                    await Context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE \"FlowGroups\" SET \"ProviderRepositoryId\" = {foreignRepositoryId} WHERE \"Id\" = (SELECT \"FlowGroupId\" FROM \"Pass2JobExecutions\" WHERE \"Id\" = {JobExecutionId})");
                    break;
                }
                case "scan-run":
                {
                    Guid systemId = await Context.Pass2Executions
                        .Where(value => value.Id == ExecutionId)
                        .Select(value => value.SystemId)
                        .SingleAsync();
                    Guid foreignRepositoryId = Guid.Parse("a0000000-0000-4000-8000-000000000011");
                    Repository foreignRepository = Repository.Create(
                        systemId,
                        "T025 admission scan-run drift repository",
                        "https://example.test/t025-admission-scan-run-drift-repository",
                        null,
                        null,
                        DateTimeOffset.UtcNow);
                    ExactCrossRepositoryReflection.SetId(foreignRepository, foreignRepositoryId);
                    Context.Repositories.Add(foreignRepository);
                    await Context.SaveChangesAsync();
                    await Context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE \"FlowGroups\" SET \"RepositoryId\" = (SELECT \"RepositoryTwoId\" FROM \"TwoRepositoryScanCoordinations\" WHERE \"Id\" = (SELECT \"CoordinationId\" FROM \"Pass2Executions\" WHERE \"Id\" = (SELECT \"Pass2ExecutionId\" FROM \"Pass2JobExecutions\" WHERE \"Id\" = {JobExecutionId}))), \"ScanRunId\" = (SELECT \"ScanRunTwoId\" FROM \"TwoRepositoryScanCoordinations\" WHERE \"Id\" = (SELECT \"CoordinationId\" FROM \"Pass2Executions\" WHERE \"Id\" = (SELECT \"Pass2ExecutionId\" FROM \"Pass2JobExecutions\" WHERE \"Id\" = {JobExecutionId}))), \"ConsumerDocumentVersionId\" = \"ProviderXmlDocumentVersionId\", \"ProviderRepositoryId\" = {foreignRepositoryId} WHERE \"Id\" = (SELECT \"FlowGroupId\" FROM \"Pass2JobExecutions\" WHERE \"Id\" = {JobExecutionId})");
                    break;
                }
                case "profiled-revision":
                    await Context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE \"TwoRepositoryScanCoordinations\" SET \"ProfiledRevisionOne\" = {new string('f', 40)} WHERE \"Id\" = (SELECT \"CoordinationId\" FROM \"Pass2Executions\" WHERE \"Id\" = (SELECT \"Pass2ExecutionId\" FROM \"Pass2JobExecutions\" WHERE \"Id\" = {JobExecutionId}))");
                    break;
                case "document-fingerprint":
                    await Context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE \"DocumentVersions\" SET \"ContentFingerprint\" = {new string('c', 64)} WHERE \"Id\" = {documentVersionId}");
                    break;
                case "source-anchor-hash":
                    await Context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE \"SourceAnchors\" SET \"SourceHash\" = {new string('c', 64)} WHERE \"Id\" = {sourceAnchorId}");
                    break;
                case "source-anchor-range":
                    await Context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE \"SourceAnchors\" SET \"ByteStart\" = 10, \"ByteEnd\" = 16 WHERE \"Id\" = {sourceAnchorId}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation));
            }

            Context.ChangeTracker.Clear();
        }

        internal async Task<Pass2CodeLogicRetrievalItem> IssueSectionHandleAsync()
        {
            Pass2CodeLogicRetrievalResult search = await Service.RetrieveAsync(
                JobExecutionId,
                new(Pass2CodeLogicRetrievalOperation.Search, queryText: "ledger"));
            Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, search.Outcome);
            Pass2CodeLogicRetrievalItem? section = search.Items.FirstOrDefault(value =>
                value.Kind == Pass2CodeLogicRetrievalItemKind.Section);
            Assert.NotNull(section);
            return section;
        }

        internal async Task<Pass2CodeLogicRetrievalItem> IssueAnchorHandleAsync()
        {
            Pass2CodeLogicRetrievalItem section = await IssueSectionHandleAsync();
            Pass2CodeLogicRetrievalResult anchors = await Service.RetrieveAsync(
                JobExecutionId,
                new(
                    Pass2CodeLogicRetrievalOperation.SourceAnchors,
                    opaqueHandles: [section.OpaqueHandle]));
            Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, anchors.Outcome);
            Pass2CodeLogicRetrievalItem? anchor = anchors.Items.FirstOrDefault(value =>
                value.Kind == Pass2CodeLogicRetrievalItemKind.SourceAnchor);
            Assert.NotNull(anchor);
            return anchor;
        }

        internal async Task<Pass2CodeLogicRetrievalItem> IssueDocumentHandleAsync()
        {
            Pass2CodeLogicRetrievalResult search = await Service.RetrieveAsync(
                JobExecutionId,
                new(Pass2CodeLogicRetrievalOperation.Search, queryText: "consumer"));
            Assert.Equal(Pass2CodeLogicRetrievalOutcome.Accepted, search.Outcome);
            return Assert.Single(search.Items, value =>
                value.Kind == Pass2CodeLogicRetrievalItemKind.Document);
        }

        internal async Task<string> AnchorSnapshotAsync(Guid anchorId)
        {
            return await Context.SourceAnchors
                .Where(value => value.Id == anchorId)
                .Select(value => value.SourceHash + ":" + value.ByteStart + ":" + value.ByteEnd)
                .SingleAsync();
        }

        internal async Task<string> RetrievalAccountingSnapshotAsync(Guid? jobExecutionId = null)
        {
            Guid selectedJobExecutionId = jobExecutionId ?? JobExecutionId;
            Pass2RetrievalBudget budget = await Context.Pass2RetrievalBudgets
                .SingleAsync(value => value.Pass2JobExecutionId == selectedJobExecutionId);
            int receipts = await Context.Pass2RetrievalReceipts
                .CountAsync(value => value.Pass2JobExecutionId == selectedJobExecutionId);
            Guid[] receiptIds = await Context.Pass2RetrievalReceipts
                .Where(value => value.Pass2JobExecutionId == selectedJobExecutionId)
                .Select(value => value.Id)
                .ToArrayAsync();
            int results = await Context.Pass2RetrievalResults
                .CountAsync(value => receiptIds.Contains(value.ReceiptId));
            return string.Join(
                ":",
                budget.CallsReserved,
                budget.ResultsReturned,
                budget.AdditionalDocumentCount,
                budget.ReturnedUtf8Bytes,
                budget.StateVersion,
                receipts,
                results);
        }

        internal async Task<Pass2RetrievalReceiptState> LatestReceiptStateAsync(
            Guid? jobExecutionId = null)
        {
            Guid selectedJobExecutionId = jobExecutionId ?? JobExecutionId;
            return await Context.Pass2RetrievalReceipts
                .Where(value => value.Pass2JobExecutionId == selectedJobExecutionId)
                .OrderByDescending(value => value.CallOrdinal)
                .Select(value => value.State)
                .FirstAsync();
        }

        internal async Task<Guid[]> InitialDocumentVersionIdsAsync()
        {
            Guid flowGroupId = await Context.Pass2JobExecutions
                .Where(value => value.Id == JobExecutionId)
                .Select(value => value.FlowGroupId)
                .SingleAsync();
            Guid[] traceIds = await Context.FlowTraces
                .Where(value => value.FlowGroupId == flowGroupId)
                .Select(value => value.Id)
                .ToArrayAsync();
            return await Context.FlowTraceSteps
                .Where(value => traceIds.Contains(value.FlowTraceId) &&
                    value.DocumentVersionId != Guid.Empty)
                .Select(value => value.DocumentVersionId)
                .Distinct()
                .ToArrayAsync();
        }

        internal async Task<string> AuthoritySnapshotAsync()
        {
            Guid executionId = await Context.Pass2JobExecutions
                .Where(job => job.Id == JobExecutionId)
                .Select(job => job.Pass2ExecutionId)
                .SingleAsync();
            var groupRows = await Context.FlowGroups.AsNoTracking()
                .Where(group => group.Pass2ExecutionId == executionId)
                .OrderBy(group => group.GroupOrder)
                .Select(group => new
                {
                    group.Id,
                    group.State,
                    group.PlanFingerprint,
                    group.MembershipFingerprint,
                    group.ExactResolutionDecisionId,
                    group.ExactResolutionEdgeId,
                })
                .ToArrayAsync();
            string groups = string.Join(
                "|",
                groupRows.Select(group => string.Join(
                    ":",
                    group.Id,
                    group.State,
                    group.PlanFingerprint,
                    group.MembershipFingerprint,
                    group.ExactResolutionDecisionId,
                    group.ExactResolutionEdgeId)));
            Guid[] decisionIds = groupRows
                .Where(group => group.ExactResolutionDecisionId is not null)
                .Select(group => group.ExactResolutionDecisionId!.Value)
                .ToArray();
            var decisionRows = await Context.CrossRepositoryResolutionDecisions.AsNoTracking()
                .Where(decision => decisionIds.Contains(decision.Id))
                .OrderBy(decision => decision.Id)
                .Select(decision => new
                {
                    decision.Id,
                    decision.ProviderDocumentVersionId,
                    decision.DocumentEdgeId,
                    decision.EvidenceFingerprint,
                })
                .ToArrayAsync();
            string decisions = string.Join(
                "|",
                decisionRows.Select(decision => string.Join(
                    ":",
                    decision.Id,
                    decision.ProviderDocumentVersionId,
                    decision.DocumentEdgeId,
                    decision.EvidenceFingerprint)));
            return groups + "\n" + decisions;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Fixture.DisposeAsync();
        }

        private static object GetRequiredProperty(object target, string name) =>
            target.GetType().GetProperty(name)?.GetValue(target)
            ?? throw new InvalidOperationException($"The authoritative seed property '{name}' is unavailable.");

        private static async Task AddSearchableSectionsAsync(VulgataDbContext context, object seed)
        {
            ExactPass1DocumentFixture[] documents =
            [
                (ExactPass1DocumentFixture)GetRequiredProperty(seed, "Consumer"),
                (ExactPass1DocumentFixture)GetRequiredProperty(seed, "ProviderXml"),
                (ExactPass1DocumentFixture)GetRequiredProperty(seed, "ProviderImplementation"),
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

        private static async Task AddAdditionalDecisionAsync(
            VulgataDbContext context,
            object seed)
        {
            MethodInfo method = typeof(Pass2FlowPlanningPostgreSqlTests)
                .GetMethod("AddExactDecisionsAsync", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("The accepted T020 decision fixture helper is unavailable.");
            object invocation = method.Invoke(null, [context, seed, 1])
                ?? throw new InvalidOperationException("The accepted T020 decision fixture invocation returned null.");
            if (invocation is not Task task)
            {
                throw new InvalidOperationException("The accepted T020 decision fixture helper is not asynchronous.");
            }

            await task;
        }
    }

    private static void AssertReservedRefusalAccounting(string beforeSnapshot, string afterSnapshot)
    {
        (int beforeCalls, int beforeResults, int beforeDocuments, int beforeBytes,
            long beforeStateVersion, int beforeReceipts, int beforeResultRows) =
            ParseAccountingSnapshot(beforeSnapshot);
        (int afterCalls, int afterResults, int afterDocuments, int afterBytes,
            long afterStateVersion, int afterReceipts, int afterResultRows) =
            ParseAccountingSnapshot(afterSnapshot);

        Assert.Equal(beforeCalls + 1, afterCalls);
        Assert.Equal(beforeResults, afterResults);
        Assert.Equal(beforeDocuments, afterDocuments);
        Assert.Equal(beforeBytes, afterBytes);
        Assert.Equal(beforeStateVersion + 1, afterStateVersion);
        Assert.Equal(beforeReceipts + 1, afterReceipts);
        Assert.Equal(beforeResultRows, afterResultRows);
    }

    private static async Task AssertReservedRefusalAsync(
        PersistedRetrievalFixture fixture,
        string beforeSnapshot,
        string afterSnapshot,
        Pass2CodeLogicRetrievalResult result)
    {
        AssertReservedRefusalAccounting(beforeSnapshot, afterSnapshot);
        AssertCurrentRemainingBudget(result, afterSnapshot);
        Assert.Equal(Pass2RetrievalReceiptState.Refused, await fixture.LatestReceiptStateAsync());
    }

    private static void AssertCurrentRemainingBudget(
        Pass2CodeLogicRetrievalResult result,
        string accountingSnapshot)
    {
        (int calls, int results, int documents, int bytes, _, _, _) =
            ParseAccountingSnapshot(accountingSnapshot);
        Assert.Equal(Math.Max(0, Pass2RetrievalContracts.MaxCalls - calls), result.RemainingBudget.Calls);
        Assert.Equal(Math.Max(0, Pass2RetrievalContracts.MaxResults - results), result.RemainingBudget.Results);
        Assert.Equal(
            Math.Max(0, Pass2RetrievalContracts.MaxAdditionalDocuments - documents),
            result.RemainingBudget.AdditionalDocuments);
        Assert.Equal(Pass2RetrievalContracts.MaxSectionsPerDocument, result.RemainingBudget.SectionsPerDocument);
        Assert.Equal(
            Math.Max(0, Pass2RetrievalContracts.MaxReturnedUtf8Bytes - bytes),
            result.RemainingBudget.ReturnedUtf8Bytes);
    }

    private static (int Calls, int Results, int Documents, int Bytes, long StateVersion, int Receipts, int ResultRows)
        ParseAccountingSnapshot(string snapshot)
    {
        string[] values = snapshot.Split(':');
        Assert.Equal(7, values.Length);
        return (
            int.Parse(values[0]),
            int.Parse(values[1]),
            int.Parse(values[2]),
            int.Parse(values[3]),
            long.Parse(values[4]),
            int.Parse(values[5]),
            int.Parse(values[6]));
    }

    private sealed class DeniedAccessVerifier : IRepositoryAuthorizationVerifier
    {
        public ValueTask<bool> HasCurrentAccessAsync(
            IReadOnlyCollection<Guid> requiredRepositoryIds,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }

    private sealed class AllowAccessVerifier : IRepositoryAuthorizationVerifier
    {
        private Func<Task>? _admissionMutation;
        private int _checksBeforeMutation;

        internal bool IsAllowed { get; set; } = true;

        internal bool AdmissionMutationInvoked { get; private set; }

        internal void ArmAdmissionMutation(
            Func<Task> mutation,
            int expectedAuthorizationChecks)
        {
            ArgumentNullException.ThrowIfNull(mutation);
            ArgumentOutOfRangeException.ThrowIfLessThan(expectedAuthorizationChecks, 1);
            _admissionMutation = mutation;
            _checksBeforeMutation = expectedAuthorizationChecks;
            AdmissionMutationInvoked = false;
        }

        public async ValueTask<bool> HasCurrentAccessAsync(
            IReadOnlyCollection<Guid> requiredRepositoryIds,
            CancellationToken cancellationToken = default)
        {
            if (_admissionMutation is not null &&
                Interlocked.Decrement(ref _checksBeforeMutation) == 0)
            {
                Func<Task>? mutation = Interlocked.Exchange(ref _admissionMutation, null);
                if (mutation is not null)
                {
                    AdmissionMutationInvoked = true;
                    await mutation();
                }
            }

            return IsAllowed;
        }
    }

    private sealed class RecordingSourceReader : IRevisionBoundSourceReader
    {
        public int Calls { get; private set; }
        public string? FixedSourceHash { get; set; }
        public Func<Task>? BeforeReadAsync { get; set; }

        public void Reset() => Calls = 0;

        public Task<SourceExcerptResult> ReadAsync(
            AnalysisScope scope,
            RevisionBoundSourceRequest request,
            CancellationToken cancellationToken)
        {
            Calls++;
            cancellationToken.ThrowIfCancellationRequested();
            return ReadAsyncCore(
                scope,
                request,
                cancellationToken);
        }

        private async Task<SourceExcerptResult> ReadAsyncCore(
            AnalysisScope scope,
            RevisionBoundSourceRequest request,
            CancellationToken cancellationToken)
        {
            if (BeforeReadAsync is not null)
            {
                await BeforeReadAsync();
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new SourceExcerptResult(
                AnalysisOperationStatus.Succeeded,
                scope.RepositoryId,
                scope.ProfiledRevision,
                request.RelativePath,
                request.Range,
                FixedSourceHash ?? request.ExpectedSourceSha256,
                "ledger",
                6,
                null);
        }
    }
}
