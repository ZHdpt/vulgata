using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests.Migrations;
using Xunit;
using Xunit.Sdk;

namespace Vulgata.Tests.PreparedPass2;

/// <summary>
/// T034 PostgreSQL publication qualification against the approved public
/// committer seam.  Each test owns a uniquely named disposable database and
/// observes durable publication, replay, and rollback behavior directly.
/// </summary>
public sealed class Pass2DocumentPostgreSqlAtomicTests
{
    [Fact]
    public void Pass2Publication_ExposesOnePublicAtomicReplayPort()
    {
        Type port = ResolvePort();

        MethodInfo commit = Assert.Single(
            port.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.Name == "CommitAsync"));
        Assert.Contains(
            commit.GetParameters(),
            parameter => parameter.ParameterType.Name.Contains(
                "Pass2",
                StringComparison.Ordinal));

        Assert.True(
            commit.ReturnType.IsGenericType &&
            commit.ReturnType.GetGenericTypeDefinition() == typeof(Task<>),
            "The atomic publication operation must return Task<T>.");

        Type result = commit.ReturnType.GetGenericArguments()[0];
        PropertyInfo disposition = Assert.Single(
            result.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.Name is "Status" or "Outcome" or "Disposition"));
        Assert.True(
            disposition.PropertyType.IsEnum,
            "The publication result must expose a closed replay disposition.");
        Assert.Contains("Conflict", Enum.GetNames(disposition.PropertyType), StringComparer.Ordinal);
        Assert.Contains("Replayed", Enum.GetNames(disposition.PropertyType), StringComparer.Ordinal);
    }

    [Fact]
    public void Pass2Execution_ExposesDurablePublicationAccountingFields()
    {
        Type execution = typeof(Pass2Execution);

        Assert.NotNull(execution.GetProperty("PublishedCount", BindingFlags.Public | BindingFlags.Instance));
        Assert.NotNull(execution.GetProperty("FailedCount", BindingFlags.Public | BindingFlags.Instance));
        PropertyInfo? stateVersion = execution.GetProperty(
            "StateVersion", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(stateVersion);
        Assert.Equal(typeof(long), stateVersion.PropertyType);
    }

    [PostgreSqlQualificationFact]
    public async Task PostgreSql17_ValidCandidatePublishesOneImmutableVersionAsync()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using QualificationFixture fixture = await QualificationFixture.CreateAsync(administrator);
        string traceBefore = await fixture.FlowTraceSnapshotAsync();

        Pass2BusinessLogicCommitResult result = await fixture.Committer.CommitAsync(
            fixture.CreateRequest(),
            CancellationToken.None);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, result.Status);
        Assert.NotNull(result.DocumentVersionId);

        Assert.Equal(1, await fixture.Context.LogicalDocuments.CountAsync(
            value => value.RepositoryId == fixture.ConsumerRepositoryId &&
                value.DocumentType == DocumentType.BusinessLogic));
        Assert.Equal(1, await fixture.Context.DocumentVersions.CountAsync(
            value => value.Id == result.DocumentVersionId));
        FlowGroup publishedGroup = await fixture.Context.FlowGroups.SingleAsync(
            value => value.Id == fixture.Group.Id);
        Assert.Equal(result.DocumentVersionId, publishedGroup.BusinessLogicDocumentVersionId);
        FlowTrace trace = await fixture.Context.FlowTraces.SingleAsync(
            value => value.FlowGroupId == publishedGroup.Id);
        FlowTraceStep[] traceSteps = await fixture.Context.FlowTraceSteps
            .Where(value => value.FlowTraceId == trace.Id)
            .OrderBy(value => value.StepOrder)
            .ToArrayAsync();
        Assert.NotEmpty(traceSteps);
        Assert.Equal(
            Enumerable.Range(1, traceSteps.Length),
            traceSteps.Select(value => value.StepOrder));
        Assert.Contains(traceSteps, value => value.DocumentVersionId == fixture.ConsumerDocumentVersionId);
        Assert.Contains(traceSteps, value => value.EdgeId == fixture.Edge.Id &&
            value.ExactResolutionDecisionId == fixture.DecisionId);
        Assert.Equal(traceBefore, await fixture.FlowTraceSnapshotAsync());
    }

    [PostgreSqlQualificationFact]
    public async Task PostgreSql17_ExactReplayAfterRestartReturnsSameVersionWithoutMutationAsync()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using QualificationFixture fixture = await QualificationFixture.CreateAsync(administrator);
        Pass2BusinessLogicCommitRequest request = fixture.CreateRequest();

        Pass2BusinessLogicCommitResult first = await fixture.Committer.CommitAsync(request);
        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, first.Status);
        string before = await fixture.PublicationSnapshotAsync();

        await using VulgataDbContext restarted = CreateContext(fixture.DatabaseConnectionString);
        Pass2BusinessLogicCommitResult replay = await new Pass2BusinessLogicDocumentCommitter(
            restarted, fixture.Scope).CommitAsync(request);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Replayed, replay.Status);
        Assert.Equal(first.DocumentVersionId, replay.DocumentVersionId);
        Assert.Equal(first.UsedDocumentVersionIds, replay.UsedDocumentVersionIds);
        Assert.Equal(before, await fixture.PublicationSnapshotAsync());
    }

    [PostgreSqlQualificationFact]
    public async Task PostgreSql17_ChangedReplayConflictsWithoutMutationAsync()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using QualificationFixture fixture = await QualificationFixture.CreateAsync(administrator);
        Pass2BusinessLogicCommitRequest request = fixture.CreateRequest();
        Pass2BusinessLogicCommitResult first = await fixture.Committer.CommitAsync(request);
        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, first.Status);
        string before = await fixture.PublicationSnapshotAsync();

        JsonObject changed = JsonNode.Parse(request.Candidate.GetRawText())!.AsObject();
        changed["title"] = "Changed replay candidate";
        Pass2BusinessLogicCommitRequest changedRequest = new(
            request.Pass2JobExecutionId,
            request.LeaseToken,
            request.AttemptOrdinal,
            JsonSerializer.SerializeToElement(changed),
            request.InitialEvidence,
            request.PromptIdentity);
        await using VulgataDbContext restarted = CreateContext(fixture.DatabaseConnectionString);
        Pass2BusinessLogicCommitResult conflict = await new Pass2BusinessLogicDocumentCommitter(
            restarted, fixture.Scope).CommitAsync(changedRequest);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Conflict, conflict.Status);
        Assert.Null(conflict.DocumentVersionId);
        Assert.Empty(conflict.UsedDocumentVersionIds);
        Assert.Equal(before, await fixture.PublicationSnapshotAsync());
    }

    [PostgreSqlQualificationFact]
    public async Task PostgreSql17_LostAcknowledgementRetryReturnsSameVersionWithoutMutationAsync()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using QualificationFixture fixture = await QualificationFixture.CreateAsync(administrator);
        Pass2BusinessLogicCommitRequest request = fixture.CreateRequest();

        Pass2BusinessLogicCommitResult first = await fixture.Committer.CommitAsync(request);
        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, first.Status);
        string before = await fixture.PublicationSnapshotAsync();

        // A caller may lose the acknowledgement after the transaction commits
        // and retry on the same live process. The retry must be a replay.
        Pass2BusinessLogicCommitResult retry = await fixture.Committer.CommitAsync(request);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Replayed, retry.Status);
        Assert.Equal(first.DocumentVersionId, retry.DocumentVersionId);
        Assert.Equal(first.UsedDocumentVersionIds, retry.UsedDocumentVersionIds);
        Assert.Equal(before, await fixture.PublicationSnapshotAsync());
    }

    [PostgreSqlQualificationFact]
    public async Task PostgreSql17_ConcurrentSameJobAdmissionHasOneOwnerAndOneReplayAsync()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using QualificationFixture fixture = await QualificationFixture.CreateAsync(administrator);
        Pass2BusinessLogicCommitRequest request = fixture.CreateRequest();
        int versionsBefore = await fixture.Context.DocumentVersions.CountAsync();
        await using VulgataDbContext leftContext = CreateContext(fixture.DatabaseConnectionString);
        await using VulgataDbContext rightContext = CreateContext(fixture.DatabaseConnectionString);

        Task<Pass2BusinessLogicCommitResult> left = new Pass2BusinessLogicDocumentCommitter(
            leftContext,
            fixture.Scope).CommitAsync(request);
        Task<Pass2BusinessLogicCommitResult> right = new Pass2BusinessLogicDocumentCommitter(
            rightContext,
            fixture.Scope).CommitAsync(request);
        Pass2BusinessLogicCommitResult[] results = await Task.WhenAll(left, right);

        Assert.Single(results, value => value.Status == Pass2BusinessLogicCommitStatus.Committed);
        Assert.Single(results, value => value.Status == Pass2BusinessLogicCommitStatus.Replayed);
        Assert.Equal(1, await fixture.Context.LogicalDocuments.CountAsync(value =>
            value.RepositoryId == fixture.ConsumerRepositoryId &&
            value.DocumentType == DocumentType.BusinessLogic));
        Assert.Equal(versionsBefore + 1, await fixture.Context.DocumentVersions.CountAsync());
    }

    [PostgreSqlQualificationFact]
    public async Task PostgreSql17_SharedResolutionEdgeWithAnotherDecisionIsRejectedWithoutMutationAsync()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using QualificationFixture fixture = await QualificationFixture.CreateAsync(administrator);
        (Guid sharedDecisionId, string sharedDecisionFingerprint) =
            await fixture.AddSecondDecisionReferencingSharedEdgeAsync();
        JsonObject substitutedCandidate = JsonNode.Parse(
            fixture.CreateRequest().Candidate.GetRawText())!.AsObject();
        JsonObject binding = substitutedCandidate["binding"]!.AsObject();
        binding["exactResolutionDecisionId"] = sharedDecisionId;
        binding["exactResolutionDecisionFingerprint"] = sharedDecisionFingerprint;
        Pass2BusinessLogicCommitRequest substitutedRequest = new(
            fixture.CreateRequest().Pass2JobExecutionId,
            fixture.CreateRequest().LeaseToken,
            fixture.CreateRequest().AttemptOrdinal,
            JsonSerializer.SerializeToElement(substitutedCandidate),
            fixture.CreateRequest().InitialEvidence,
            fixture.CreateRequest().PromptIdentity);
        string before = await fixture.PublicationSnapshotAsync();

        Pass2BusinessLogicCommitResult result = await fixture.Committer.CommitAsync(
            substitutedRequest,
            CancellationToken.None);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Rejected, result.Status);
        Assert.Null(result.DocumentVersionId);
        Assert.Empty(result.UsedDocumentVersionIds);
        Assert.Equal(before, await fixture.PublicationSnapshotAsync());
    }

    public static TheoryData<string> ReplayAuthorityMutations => new()
    {
        "decision-contribution",
        "references-edge",
        "source-anchor",
        "sections",
        "index",
        "flow-trace",
        "logical-current-pointer",
        "provenance",
        "job-execution-graph",
    };

    [PostgreSqlQualificationTheory]
    [MemberData(nameof(ReplayAuthorityMutations))]
    public async Task PostgreSql17_AuthorityDriftAfterCommitConflictsWithoutPublicationMutationAsync(
        string mutation)
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using QualificationFixture fixture = await QualificationFixture.CreateAsync(administrator);
        Pass2BusinessLogicCommitRequest request = fixture.CreateRequest();
        Pass2BusinessLogicCommitResult first = await fixture.Committer.CommitAsync(request);
        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, first.Status);
        await fixture.MutateReplayAuthorityAsync(mutation, first.DocumentVersionId!.Value);
        string before = await fixture.PublicationSnapshotAsync();

        Pass2BusinessLogicCommitResult replay = await fixture.Committer.CommitAsync(request);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Conflict, replay.Status);
        Assert.Null(replay.DocumentVersionId);
        Assert.Empty(replay.UsedDocumentVersionIds);
        Assert.Equal(before, await fixture.PublicationSnapshotAsync());
    }

    [PostgreSqlQualificationFact]
    public async Task PostgreSql17_LaterPairWithSameStableKeyAppendsAndSupersedesAsync()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using QualificationFixture fixture = await QualificationFixture.CreateAsync(
            administrator,
            additionalJob: true);
        Guid firstVersionId = (await fixture.Committer.CommitAsync(fixture.CreateRequest())).DocumentVersionId!.Value;
        Guid firstLogicalId = await fixture.BusinessLogicalDocumentIdAsync();
        Pass2BusinessLogicCommitRequest secondRequest = await fixture.PrepareSecondRequestWithStableKeyAsync(
            fixture.Group.LogicalDocumentStableKey);

        Pass2BusinessLogicCommitResult second = await fixture.CommitterForSecond.CommitAsync(secondRequest);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, second.Status);
        Assert.NotEqual(firstVersionId, second.DocumentVersionId);
        Assert.Equal(firstLogicalId, await fixture.BusinessLogicalDocumentIdAsync());
        DocumentVersion first = await fixture.Context.DocumentVersions.SingleAsync(value => value.Id == firstVersionId);
        Assert.Equal(second.DocumentVersionId, first.SupersededById);
        Assert.Equal(2, await fixture.BusinessVersionCountAsync(firstLogicalId));
    }

    [PostgreSqlQualificationFact]
    public async Task PostgreSql17_ReplayOfSupersededEarlierPairReturnsReplayedWithoutMutationAsync()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using QualificationFixture fixture = await QualificationFixture.CreateAsync(
            administrator,
            additionalJob: true);
        Pass2BusinessLogicCommitRequest firstRequest = fixture.CreateRequest();
        Pass2BusinessLogicCommitResult first = await fixture.Committer.CommitAsync(firstRequest);
        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, first.Status);

        Pass2BusinessLogicCommitRequest secondRequest =
            await fixture.PrepareSecondRequestWithStableKeyAsync(fixture.Group.LogicalDocumentStableKey);
        Pass2BusinessLogicCommitResult second =
            await fixture.CommitterForSecond!.CommitAsync(secondRequest);
        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, second.Status);
        Assert.NotEqual(first.DocumentVersionId, second.DocumentVersionId);
        Pass2JobExecution firstJob = await fixture.Context.Pass2JobExecutions
            .SingleAsync(value => value.Id == firstRequest.Pass2JobExecutionId);
        Pass2JobExecution secondJob = await fixture.Context.Pass2JobExecutions
            .SingleAsync(value => value.Id == secondRequest.Pass2JobExecutionId);
        Assert.Equal(Pass2JobExecutionState.Completed, firstJob.State);
        Assert.Equal(Pass2JobExecutionState.Completed, secondJob.State);
        Assert.Null(firstJob.LeaseToken);
        Assert.Null(secondJob.LeaseToken);

        string beforeReplay = await fixture.PublicationSnapshotAsync();
        Guid currentBeforeReplay = await fixture.Context.LogicalDocuments
            .Where(value => value.RepositoryId == fixture.ConsumerRepositoryId &&
                value.DocumentType == DocumentType.BusinessLogic)
            .Select(value => value.CurrentVersionId!.Value)
            .SingleAsync();
        DocumentVersion supersededBeforeReplay = await fixture.Context.DocumentVersions
            .SingleAsync(value => value.Id == first.DocumentVersionId);
        Assert.Equal(second.DocumentVersionId, supersededBeforeReplay.SupersededById);
        Assert.Equal(second.DocumentVersionId, currentBeforeReplay);

        Pass2BusinessLogicCommitResult replay =
            await fixture.Committer.CommitAsync(firstRequest);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Replayed, replay.Status);
        Assert.Equal(first.DocumentVersionId, replay.DocumentVersionId);
        Assert.Equal(first.UsedDocumentVersionIds, replay.UsedDocumentVersionIds);
        Assert.Equal(beforeReplay, await fixture.PublicationSnapshotAsync());
        Assert.Equal(currentBeforeReplay, await fixture.Context.LogicalDocuments
            .Where(value => value.RepositoryId == fixture.ConsumerRepositoryId &&
                value.DocumentType == DocumentType.BusinessLogic)
            .Select(value => value.CurrentVersionId!.Value)
            .SingleAsync());
        DocumentVersion supersededAfterReplay = await fixture.Context.DocumentVersions
            .SingleAsync(value => value.Id == first.DocumentVersionId);
        Assert.Equal(second.DocumentVersionId, supersededAfterReplay.SupersededById);
    }

    [PostgreSqlQualificationFact]
    public async Task PostgreSql17_ReplayOfSupersededPairWithCurrentPointerDriftReturnsConflictWithoutMutationAsync()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using QualificationFixture fixture = await QualificationFixture.CreateAsync(
            administrator,
            additionalJob: true);
        Pass2BusinessLogicCommitRequest firstRequest = fixture.CreateRequest();
        Pass2BusinessLogicCommitResult first = await fixture.Committer.CommitAsync(firstRequest);
        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, first.Status);

        Pass2BusinessLogicCommitRequest secondRequest =
            await fixture.PrepareSecondRequestWithStableKeyAsync(fixture.Group.LogicalDocumentStableKey);
        Pass2BusinessLogicCommitResult second =
            await fixture.CommitterForSecond!.CommitAsync(secondRequest);
        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, second.Status);
        Assert.NotEqual(first.DocumentVersionId, second.DocumentVersionId);

        await fixture.MoveCurrentPointerAsync(first.DocumentVersionId!.Value);
        string beforeReplay = await fixture.PublicationChainSnapshotAsync();

        Pass2BusinessLogicCommitResult replay =
            await fixture.Committer.CommitAsync(firstRequest);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Conflict, replay.Status);
        Assert.Null(replay.DocumentVersionId);
        Assert.Empty(replay.UsedDocumentVersionIds);
        Assert.Equal(beforeReplay, await fixture.PublicationChainSnapshotAsync());
    }

    [PostgreSqlQualificationFact]
    public async Task PostgreSql17_ReplayOfSupersededPairWithImmediateSuccessorBacklinkReturnsConflictWithoutMutationAsync()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using QualificationFixture fixture = await QualificationFixture.CreateAsync(
            administrator,
            additionalJob: true);
        Pass2BusinessLogicCommitRequest firstRequest = fixture.CreateRequest();
        Pass2BusinessLogicCommitResult first = await fixture.Committer.CommitAsync(firstRequest);
        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, first.Status);

        Pass2BusinessLogicCommitResult second =
            await fixture.CommitterForSecond!.CommitAsync(
                await fixture.PrepareSecondRequestWithStableKeyAsync(fixture.Group.LogicalDocumentStableKey));
        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, second.Status);
        Assert.NotEqual(first.DocumentVersionId, second.DocumentVersionId);

        await fixture.SetSupersededByAsync(second.DocumentVersionId!.Value, first.DocumentVersionId!.Value);
        string beforeReplay = await fixture.PublicationChainSnapshotAsync();

        Pass2BusinessLogicCommitResult replay =
            await fixture.Committer.CommitAsync(firstRequest);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Conflict, replay.Status);
        Assert.Null(replay.DocumentVersionId);
        Assert.Empty(replay.UsedDocumentVersionIds);
        Assert.Equal(beforeReplay, await fixture.PublicationChainSnapshotAsync());
    }

    [PostgreSqlQualificationFact]
    public async Task PostgreSql17_ReplayOfFirstPairWithSecondHopSupersededByDriftReturnsConflictWithoutMutationAsync()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using QualificationFixture fixture = await QualificationFixture.CreateAsync(
            administrator,
            additionalJob: true);
        Pass2BusinessLogicCommitRequest firstRequest = fixture.CreateRequest();
        Pass2BusinessLogicCommitResult first = await fixture.Committer.CommitAsync(firstRequest);
        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, first.Status);

        Pass2BusinessLogicCommitResult second =
            await fixture.CommitterForSecond!.CommitAsync(
                await fixture.PrepareSecondRequestWithStableKeyAsync(fixture.Group.LogicalDocumentStableKey));
        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, second.Status);

        (Pass2BusinessLogicCommitRequest thirdRequest, Pass2JobExecution thirdJob) =
            await fixture.PrepareThirdRequestWithStableKeyAsync();
        Pass2BusinessLogicCommitResult third = await new Pass2BusinessLogicDocumentCommitter(
            fixture.Context,
            fixture.Scope).CommitAsync(thirdRequest);
        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, third.Status);
        Assert.NotEqual(second.DocumentVersionId, third.DocumentVersionId);

        // The unique successor index prevents reusing B as another predecessor's
        // target, and the self-referencing FK requires a shared logical document.
        // Add a detached same-chain version so C can point at a valid but wrong
        // second-hop target without making the fixture setup itself invalid.
        Guid detachedVersionId = await fixture.CloneDetachedVersionAsync(
            third.DocumentVersionId!.Value);
        await fixture.SetSupersededByAsync(
            third.DocumentVersionId!.Value,
            detachedVersionId);
        string beforeReplay = await fixture.PublicationChainSnapshotAsync();

        Pass2BusinessLogicCommitResult replay =
            await fixture.Committer.CommitAsync(firstRequest);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Conflict, replay.Status);
        Assert.Null(replay.DocumentVersionId);
        Assert.Empty(replay.UsedDocumentVersionIds);
        Assert.Equal(beforeReplay, await fixture.PublicationChainSnapshotAsync());
        Assert.Equal(
            Pass2JobExecutionState.Completed,
            await fixture.Context.Pass2JobExecutions
                .Where(value => value.Id == thirdJob.Id)
                .Select(value => value.State)
                .SingleAsync());
    }

    [PostgreSqlQualificationFact]
    public async Task PostgreSql17_PublicationAdvancesExecutionPublishedCountAndStateVersionAtomicallyAsync()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using QualificationFixture fixture = await QualificationFixture.CreateAsync(
            administrator,
            additionalJob: true);
        QualificationFixture.ExecutionAccounting beforeFirst =
            await fixture.ExecutionAccountingAsync(fixture.PrimaryExecutionId);
        QualificationFixture.ExecutionAccounting beforeSecond =
            await fixture.ExecutionAccountingAsync(fixture.SecondExecutionId);

        Pass2BusinessLogicCommitResult first =
            await fixture.Committer.CommitAsync(fixture.CreateRequest());
        Pass2BusinessLogicCommitResult second =
            await fixture.CommitterForSecond!.CommitAsync(
                await fixture.PrepareSecondRequestWithStableKeyAsync(fixture.Group.LogicalDocumentStableKey));

        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, first.Status);
        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, second.Status);
        QualificationFixture.ExecutionAccounting afterFirst =
            await fixture.ExecutionAccountingAsync(fixture.PrimaryExecutionId);
        QualificationFixture.ExecutionAccounting afterSecond =
            await fixture.ExecutionAccountingAsync(fixture.SecondExecutionId);
        Assert.Equal(beforeFirst.PublishedCount + 1, afterFirst.PublishedCount);
        Assert.Equal(beforeFirst.FailedCount, afterFirst.FailedCount);
        Assert.Equal(beforeFirst.StateVersion + 1, afterFirst.StateVersion);
        Assert.Equal(beforeSecond.PublishedCount + 1, afterSecond.PublishedCount);
        Assert.Equal(beforeSecond.FailedCount, afterSecond.FailedCount);
        Assert.Equal(beforeSecond.StateVersion + 1, afterSecond.StateVersion);
    }

    [PostgreSqlQualificationFact]
    public async Task PostgreSql17_PublicationFailureRollsBackExecutionAccountingAsync()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using QualificationFixture fixture = await QualificationFixture.CreateAsync(administrator);
        QualificationFixture.ExecutionAccounting before =
            await fixture.ExecutionAccountingAsync(fixture.PrimaryExecutionId);
        await fixture.InstallFailureTriggerAsync("DocumentVersions", "INSERT");

        Pass2BusinessLogicCommitResult result =
            await fixture.Committer.CommitAsync(fixture.CreateRequest());

        Assert.Equal(Pass2BusinessLogicCommitStatus.Conflict, result.Status);
        QualificationFixture.ExecutionAccounting after =
            await fixture.ExecutionAccountingAsync(fixture.PrimaryExecutionId);
        Assert.Equal(before, after);
    }

    [PostgreSqlQualificationFact]
    public async Task PostgreSql17_ConcurrentPublicationAndReplayAdvanceExecutionAccountingOnceAsync()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using QualificationFixture fixture = await QualificationFixture.CreateAsync(administrator);
        Pass2BusinessLogicCommitRequest request = fixture.CreateRequest();
        QualificationFixture.ExecutionAccounting before =
            await fixture.ExecutionAccountingAsync(fixture.PrimaryExecutionId);
        await using VulgataDbContext leftContext = CreateContext(fixture.DatabaseConnectionString);
        await using VulgataDbContext rightContext = CreateContext(fixture.DatabaseConnectionString);

        Task<Pass2BusinessLogicCommitResult> left = new Pass2BusinessLogicDocumentCommitter(
            leftContext,
            fixture.Scope).CommitAsync(request);
        Task<Pass2BusinessLogicCommitResult> right = new Pass2BusinessLogicDocumentCommitter(
            rightContext,
            fixture.Scope).CommitAsync(request);
        Pass2BusinessLogicCommitResult[] results = await Task.WhenAll(left, right);

        Assert.Single(results, value => value.Status == Pass2BusinessLogicCommitStatus.Committed);
        Assert.Single(results, value => value.Status == Pass2BusinessLogicCommitStatus.Replayed);
        QualificationFixture.ExecutionAccounting after =
            await fixture.ExecutionAccountingAsync(fixture.PrimaryExecutionId);
        Assert.Equal(before.PublishedCount + 1, after.PublishedCount);
        Assert.Equal(before.FailedCount, after.FailedCount);
        Assert.Equal(before.StateVersion + 1, after.StateVersion);
    }

    [PostgreSqlQualificationFact]
    public async Task PostgreSql17_LaterPairChangedEndpointCreatesNewLogicalDocumentChainAsync()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using QualificationFixture fixture = await QualificationFixture.CreateAsync(
            administrator,
            additionalJob: true);
        Pass2BusinessLogicCommitResult first = await fixture.Committer.CommitAsync(fixture.CreateRequest());
        Pass2BusinessLogicCommitResult second = await fixture.CommitterForSecond.CommitAsync(
            await fixture.PrepareSecondRequestWithChangedEndpointAsync());

        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, first.Status);
        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, second.Status);
        Assert.NotEqual(first.DocumentVersionId, second.DocumentVersionId);
        Assert.Equal(2, await fixture.BusinessLogicalDocumentCountAsync());
        Assert.Equal(1, await fixture.BusinessVersionCountAsync(
            await fixture.BusinessLogicalDocumentIdForVersionAsync(first.DocumentVersionId!.Value)));
        Assert.Equal(1, await fixture.BusinessVersionCountAsync(
            await fixture.BusinessLogicalDocumentIdForVersionAsync(second.DocumentVersionId!.Value)));
    }

    [PostgreSqlQualificationFact]
    public async Task PostgreSql17_PersistedStableKeyDriftRejectsAndPreservesPriorCurrentVersionAsync()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using QualificationFixture fixture = await QualificationFixture.CreateAsync(
            administrator,
            additionalJob: true);
        Pass2BusinessLogicCommitResult first = await fixture.Committer.CommitAsync(fixture.CreateRequest());
        Pass2BusinessLogicCommitRequest staleSecondRequest = await fixture.PrepareSecondRequestAsync();
        await fixture.MutateSecondStableKeyAsync("business-flow/v1/" + new string('f', 64));
        string before = await fixture.PublicationSnapshotAsync();

        Pass2BusinessLogicCommitResult rejected = await fixture.CommitterForSecond.CommitAsync(staleSecondRequest);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, first.Status);
        Assert.Equal(Pass2BusinessLogicCommitStatus.Rejected, rejected.Status);
        Assert.Null(rejected.DocumentVersionId);
        Assert.Empty(rejected.UsedDocumentVersionIds);
        Assert.Equal(before, await fixture.PublicationSnapshotAsync());
        Assert.Equal(first.DocumentVersionId, await fixture.Context.LogicalDocuments
            .Where(value => value.DocumentType == DocumentType.BusinessLogic &&
                value.RepositoryId == fixture.ConsumerRepositoryId)
            .Select(value => value.CurrentVersionId)
            .SingleAsync());
    }

    [PostgreSqlQualificationFact]
    public async Task PostgreSql17_ReplayOfSupersededPairWithSuccessorVersionGapReturnsConflictWithoutMutationAsync()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using QualificationFixture fixture = await QualificationFixture.CreateAsync(
            administrator,
            additionalJob: true);
        Pass2BusinessLogicCommitRequest firstRequest = fixture.CreateRequest();
        Pass2BusinessLogicCommitResult first = await fixture.Committer.CommitAsync(firstRequest);
        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, first.Status);
        Pass2BusinessLogicCommitResult second = await fixture.CommitterForSecond!.CommitAsync(
            await fixture.PrepareSecondRequestWithStableKeyAsync(fixture.Group.LogicalDocumentStableKey));
        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, second.Status);

        await fixture.SetVersionNumberAsync(second.DocumentVersionId!.Value, 3);
        string beforeReplay = await fixture.PublicationChainSnapshotAsync();

        Pass2BusinessLogicCommitResult replay =
            await fixture.Committer.CommitAsync(firstRequest);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Conflict, replay.Status);
        Assert.Null(replay.DocumentVersionId);
        Assert.Empty(replay.UsedDocumentVersionIds);
        Assert.Equal(beforeReplay, await fixture.PublicationChainSnapshotAsync());
    }

    [PostgreSqlQualificationFact]
    public async Task PostgreSql17_ConcurrentLaterPairsSerializeCurrentPointerAndPreserveOldVersionAsync()
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using QualificationFixture fixture = await QualificationFixture.CreateAsync(
            administrator,
            additionalJob: true);
        Pass2BusinessLogicCommitRequest secondRequest = await fixture.PrepareSecondRequestWithStableKeyAsync(
            fixture.Group.LogicalDocumentStableKey);
        Pass2BusinessLogicCommitRequest firstRequest = fixture.CreateRequest();
        await using VulgataDbContext concurrentContext = CreateContext(fixture.DatabaseConnectionString);
        Task<Pass2BusinessLogicCommitResult> firstCommit = fixture.Committer.CommitAsync(firstRequest);
        Task<Pass2BusinessLogicCommitResult> successor = new Pass2BusinessLogicDocumentCommitter(
            concurrentContext,
            fixture.Scope).CommitAsync(secondRequest);
        Pass2BusinessLogicCommitResult first = await firstCommit;
        Pass2BusinessLogicCommitResult second = await successor;

        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, first.Status);
        Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, second.Status);
        Assert.Equal(1, await fixture.BusinessLogicalDocumentCountAsync());
        Guid[] publishedIds = [first.DocumentVersionId!.Value, second.DocumentVersionId!.Value];
        DocumentVersion[] versions = await fixture.Context.DocumentVersions
            .Where(value => publishedIds.Contains(value.Id)).ToArrayAsync();
        DocumentVersion oldVersion = Assert.Single(versions, value => value.SupersededById is not null);
        DocumentVersion currentVersion = Assert.Single(versions, value => value.SupersededById is null);
        Assert.Equal(currentVersion.Id, await fixture.Context.LogicalDocuments
            .Where(value => value.Id == oldVersion.LogicalDocumentId)
            .Select(value => value.CurrentVersionId).SingleAsync());
        Assert.Equal(currentVersion.Id, oldVersion.SupersededById);
    }

    public static TheoryData<string, string> PublicationFailureBoundaries => new()
    {
        { "LogicalDocuments", "INSERT" },
        { "DocumentVersions", "INSERT" },
        { "DocumentSections", "INSERT" },
        { "IndexEntries", "INSERT" },
        { "DocumentEdges", "INSERT" },
        { "FlowGroups", "UPDATE" },
        { "Pass2JobExecutions", "UPDATE" },
    };

    [PostgreSqlQualificationTheory]
    [MemberData(nameof(PublicationFailureBoundaries))]
    public async Task PostgreSql17_InjectedFailureAtPublicationBoundaryRollsBackEverythingAsync(
        string tableName,
        string operation)
    {
        string administrator = GetAdministratorConnectionStringOrSkip();
        await using QualificationFixture fixture = await QualificationFixture.CreateAsync(administrator);
        await fixture.InstallFailureTriggerAsync(tableName, operation);
        string before = await fixture.PublicationSnapshotAsync();

        Pass2BusinessLogicCommitResult result = await fixture.Committer.CommitAsync(
            fixture.CreateRequest(),
            CancellationToken.None);

        Assert.Equal(Pass2BusinessLogicCommitStatus.Conflict, result.Status);
        Assert.Null(result.DocumentVersionId);
        Assert.Empty(result.UsedDocumentVersionIds);
        Assert.Equal(before, await fixture.PublicationSnapshotAsync());
    }

    private static string GetAdministratorConnectionStringOrSkip()
    {
        string? value = Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN");
        if (string.IsNullOrWhiteSpace(value))
        {
            throw SkipException.ForSkip(
                "T034 PostgreSQL qualification skipped: set VULGATA_TEST_POSTGRES_ADMIN; no database was contacted.");
        }

        return value;
    }

    private static VulgataDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<VulgataDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                "__EFMigrationsHistory", "public"))
            .Options);

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static async Task CanonicalizeExactAuthorityAsync(VulgataDbContext context)
    {
        CrossRepositoryResolutionDecision seededDecision =
            await context.CrossRepositoryResolutionDecisions.SingleAsync();
        ExactCrossRepositoryEdgeEvidence edgeEvidence =
            await context.ExactCrossRepositoryEdgeEvidence.AsNoTracking().SingleAsync();
        DocumentVersion consumer = await context.DocumentVersions.AsNoTracking()
            .SingleAsync(value => value.Id == edgeEvidence.ConsumerDocumentVersionId);
        DocumentVersion provider = await context.DocumentVersions.AsNoTracking()
            .SingleAsync(value => value.Id == edgeEvidence.ProviderDocumentVersionId);
        string canonicalEvidenceFingerprint = ExactResolutionCanonicalAuthority.EvidenceFingerprint(
            edgeEvidence, consumer.ProfiledRevision, provider.ProfiledRevision);
        ExactResolutionProviderEvidenceContribution[] seededContributions = await context
            .ExactResolutionProviderEvidenceContributions.AsNoTracking()
            .Where(value => value.ResolutionDecisionId == seededDecision.Id)
            .OrderBy(value => value.ProviderOrdinal).ThenBy(value => value.EvidenceOrdinal)
            .ToArrayAsync();

        context.RemoveRange(await context.ExactResolutionProviderEvidenceContributions
            .Where(value => value.ResolutionDecisionId == seededDecision.Id).ToArrayAsync());
        context.Remove(seededDecision);
        await context.SaveChangesAsync();

        ExactCrossRepositoryEdgeEvidence trackedEvidence =
            await context.ExactCrossRepositoryEdgeEvidence
                .SingleAsync(value => value.EdgeId == edgeEvidence.EdgeId);
        SetProperty(trackedEvidence, nameof(ExactCrossRepositoryEdgeEvidence.EvidenceFingerprint),
            canonicalEvidenceFingerprint);
        CrossRepositoryResolutionDecision canonicalDecision =
            NewEntity<CrossRepositoryResolutionDecision>();
        SetProperty(canonicalDecision, nameof(CrossRepositoryResolutionDecision.Id),
            ExactResolutionCanonicalAuthority.DecisionId(canonicalEvidenceFingerprint));
        SetProperty(canonicalDecision, nameof(CrossRepositoryResolutionDecision.Pass1PairExecutionId),
            seededDecision.Pass1PairExecutionId);
        SetProperty(canonicalDecision, nameof(CrossRepositoryResolutionDecision.CoordinationId),
            seededDecision.CoordinationId);
        SetProperty(canonicalDecision, nameof(CrossRepositoryResolutionDecision.SystemId),
            seededDecision.SystemId);
        SetProperty(canonicalDecision, nameof(CrossRepositoryResolutionDecision.ConsumerCandidateId),
            seededDecision.ConsumerCandidateId);
        SetProperty(canonicalDecision, nameof(CrossRepositoryResolutionDecision.ConsumerEntryIdentity),
            edgeEvidence.OccurrenceId);
        SetProperty(canonicalDecision, nameof(CrossRepositoryResolutionDecision.ConsumerEntryStableKey),
            seededDecision.ConsumerEntryStableKey);
        SetProperty(canonicalDecision, nameof(CrossRepositoryResolutionDecision.ConsumerDocumentVersionId),
            seededDecision.ConsumerDocumentVersionId);
        SetProperty(canonicalDecision, nameof(CrossRepositoryResolutionDecision.Protocol), seededDecision.Protocol);
        SetProperty(canonicalDecision, nameof(CrossRepositoryResolutionDecision.NormalizedEndpointIdentifier),
            seededDecision.NormalizedEndpointIdentifier);
        SetProperty(canonicalDecision, nameof(CrossRepositoryResolutionDecision.Outcome), seededDecision.Outcome);
        SetProperty(canonicalDecision, nameof(CrossRepositoryResolutionDecision.ProviderDocumentVersionId),
            seededDecision.ProviderDocumentVersionId);
        SetProperty(canonicalDecision, nameof(CrossRepositoryResolutionDecision.DocumentEdgeId),
            seededDecision.DocumentEdgeId);
        SetProperty(canonicalDecision, nameof(CrossRepositoryResolutionDecision.UncertaintyId),
            seededDecision.UncertaintyId);
        SetProperty(canonicalDecision, nameof(CrossRepositoryResolutionDecision.EvidenceFingerprint),
            canonicalEvidenceFingerprint);
        SetProperty(canonicalDecision, nameof(CrossRepositoryResolutionDecision.CreatedAt), seededDecision.CreatedAt);
        context.CrossRepositoryResolutionDecisions.Add(canonicalDecision);

        foreach (ExactResolutionProviderEvidenceContribution seededContribution in seededContributions)
        {
            string fingerprint = ExactResolutionCanonicalAuthority.ProviderContributionFingerprint(
                canonicalEvidenceFingerprint,
                seededContribution.ProviderDocumentVersionId,
                seededContribution.SourceAnchorId);
            ExactResolutionProviderEvidenceContribution canonicalContribution =
                NewEntity<ExactResolutionProviderEvidenceContribution>();
            SetProperty(canonicalContribution, nameof(ExactResolutionProviderEvidenceContribution.Id),
                ExactResolutionCanonicalAuthority.ProviderContributionId(fingerprint));
            SetProperty(canonicalContribution,
                nameof(ExactResolutionProviderEvidenceContribution.ResolutionDecisionId), canonicalDecision.Id);
            SetProperty(canonicalContribution,
                nameof(ExactResolutionProviderEvidenceContribution.ProviderDocumentVersionId),
                seededContribution.ProviderDocumentVersionId);
            SetProperty(canonicalContribution, nameof(ExactResolutionProviderEvidenceContribution.SourceAnchorId),
                seededContribution.SourceAnchorId);
            SetProperty(canonicalContribution,
                nameof(ExactResolutionProviderEvidenceContribution.EvidenceFingerprint), fingerprint);
            SetProperty(canonicalContribution, nameof(ExactResolutionProviderEvidenceContribution.ProviderOrdinal),
                seededContribution.ProviderOrdinal);
            SetProperty(canonicalContribution, nameof(ExactResolutionProviderEvidenceContribution.EvidenceOrdinal),
                seededContribution.EvidenceOrdinal);
            SetProperty(canonicalContribution, nameof(ExactResolutionProviderEvidenceContribution.CreatedAt),
                seededContribution.CreatedAt);
            context.ExactResolutionProviderEvidenceContributions.Add(canonicalContribution);
        }

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static T NewEntity<T>() => (T)Activator.CreateInstance(
        typeof(T), BindingFlags.Instance | BindingFlags.NonPublic, null, null, null)!;

    private static void SetProperty<T>(object target, string propertyName, T value) =>
        target.GetType().GetProperty(propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(target, value);

    private sealed class QualificationFixture : IAsyncDisposable
    {
        private QualificationFixture(
            DisposablePostgreSqlDatabase database,
            ExactCrossRepositoryTestFixture sourceFixture,
            VulgataDbContext context,
            Pass2BusinessLogicDocumentCommitter committer,
            RepositoryAccessScope scope,
            Pass2JobExecution job,
            FlowGroup group,
            Pass2Execution execution,
            CrossRepositoryResolutionDecision decision,
            DocumentEdge edge,
            ExactResolutionProviderEvidenceContribution[] contributions,
            ExactPass1DocumentFixture consumer,
            Pass2JobExecution? secondJob,
            FlowGroup? secondGroup,
            Pass2Execution? secondExecution,
            CrossRepositoryResolutionDecision? secondDecision,
            DocumentEdge? secondEdge,
            ExactResolutionProviderEvidenceContribution[] secondContributions,
            string consumerAnchorHandle,
            string relationshipHandle,
            string? secondConsumerAnchorHandle,
            string? secondRelationshipHandle,
            string administrator)
        {
            Database = database;
            SourceFixture = sourceFixture;
            Context = context;
            Committer = committer;
            Scope = scope;
            Job = job;
            Group = group;
            Execution = execution;
            Decision = decision;
            Edge = edge;
            Contributions = contributions;
            Consumer = consumer;
            InitialLeaseToken = job.LeaseToken
                ?? throw new InvalidOperationException("The primary fixture job was not leased.");
            InitialAttemptCount = job.AttemptCount;
            SecondJob = secondJob;
            SecondGroup = secondGroup;
            SecondExecution = secondExecution;
            SecondDecision = secondDecision;
            SecondEdge = secondEdge;
            SecondContributions = secondContributions;
            ConsumerAnchorHandle = consumerAnchorHandle;
            RelationshipHandle = relationshipHandle;
            SecondConsumerAnchorHandle = secondConsumerAnchorHandle;
            SecondRelationshipHandle = secondRelationshipHandle;
            Administrator = administrator;
            CommitterForSecond = secondJob is null ? null : committer;
        }

        private DisposablePostgreSqlDatabase Database { get; }
        private ExactCrossRepositoryTestFixture SourceFixture { get; }
        internal VulgataDbContext Context { get; }
        internal Pass2BusinessLogicDocumentCommitter Committer { get; }
        internal RepositoryAccessScope Scope { get; }
        private Pass2JobExecution Job { get; }
        internal FlowGroup Group { get; }
        private Pass2Execution Execution { get; }
        private CrossRepositoryResolutionDecision Decision { get; }
        internal DocumentEdge Edge { get; }
        private ExactResolutionProviderEvidenceContribution[] Contributions { get; }
        private ExactPass1DocumentFixture Consumer { get; }
        private Guid InitialLeaseToken { get; }
        private int InitialAttemptCount { get; }
        private Pass2JobExecution? SecondJob { get; }
        private FlowGroup? SecondGroup { get; set; }
        private Pass2Execution? SecondExecution { get; }
        private CrossRepositoryResolutionDecision? SecondDecision { get; set; }
        private DocumentEdge? SecondEdge { get; }
        private ExactResolutionProviderEvidenceContribution[] SecondContributions { get; set; }
        private string ConsumerAnchorHandle { get; }
        private string RelationshipHandle { get; }
        private string? SecondConsumerAnchorHandle { get; }
        private string? SecondRelationshipHandle { get; }
        private string Administrator { get; }
        internal Pass2BusinessLogicDocumentCommitter? CommitterForSecond { get; }
        internal Guid ConsumerRepositoryId => Consumer.DocumentVersion.RepositoryId;
        internal Guid ConsumerDocumentVersionId => Consumer.DocumentVersion.Id;
        internal Guid DecisionId => Decision.Id;
        internal Guid PrimaryExecutionId => Execution.Id;
        internal Guid SecondExecutionId => SecondExecution?.Id
            ?? throw new InvalidOperationException("The second Pass 2 execution is not available.");
        internal string DatabaseConnectionString => Database.ConnectionString;

        internal static async Task<QualificationFixture> CreateAsync(
            string administrator,
            bool additionalJob = false)
        {
            DisposablePostgreSqlDatabase database =
                await DisposablePostgreSqlDatabase.CreateAsync(administrator);
            ExactCrossRepositoryTestFixture? sourceFixture = null;
            VulgataDbContext? context = null;
            try
            {
                sourceFixture = await ExactCrossRepositoryTestFixture.CreateAsync();
                context = CreateContext(database.ConnectionString);
                await context.Database.MigrateAsync();

                MethodInfo seedMethod = typeof(Pass2FlowPlanningPostgreSqlTests).GetMethod(
                    "SeedPass2Async", BindingFlags.Static | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("The accepted T020 seed is unavailable.");
                object invocation = seedMethod.Invoke(null, [context, sourceFixture])
                    ?? throw new InvalidOperationException("The accepted T020 seed returned null.");
                await Assert.IsAssignableFrom<Task>(invocation);
                await (Task)invocation;
                await CanonicalizeExactAuthorityAsync(context);
                object seed = (invocation.GetType().GetProperty("Result")?.GetValue(invocation))
                    ?? throw new InvalidOperationException("The accepted T020 seed result is unavailable.");

                ExactPass1DocumentFixture consumer = Get<ExactPass1DocumentFixture>(seed, "Consumer");
                Pass1PairExecution pair = Get<Pass1PairExecution>(seed, "Pair");
                RepositoryAccessScope scope = new(
                    [consumer.DocumentVersion.RepositoryId, Get<ExactPass1DocumentFixture>(seed, "ProviderXml")
                        .DocumentVersion.RepositoryId],
                    new AlwaysAllowAuthorizationVerifier());
                PreparedPass2StartResult start = await new PreparedPass2Store(context)
                    .StartAsync(pair.Id, scope);
                Assert.Equal(PreparedPass2StartOutcome.Created, start.Outcome);
                Pass2JobExecution[] jobs = await context.Pass2JobExecutions
                    .OrderBy(value => value.GroupOrder)
                    .ToArrayAsync();
                Assert.Equal(1, jobs.Length);
                for (int index = 0; index < jobs.Length; index++)
                {
                    DateTimeOffset stateChangedAt = jobs[index].StateChangedAt.AddSeconds(1);
                    jobs[index].AcquireLease(
                        ExactCrossRepositoryTestValues.Id(50_001 + index),
                        stateChangedAt.AddMinutes(5),
                        stateChangedAt);
                }
                await context.SaveChangesAsync();
                context.ChangeTracker.Clear();

                Pass2JobExecution job = await context.Pass2JobExecutions
                    .OrderBy(value => value.GroupOrder)
                    .FirstAsync();
                FlowGroup group = await context.FlowGroups.SingleAsync(value => value.Id == job.FlowGroupId);
                Pass2Execution execution = await context.Pass2Executions.SingleAsync(value => value.Id == job.Pass2ExecutionId);
                CrossRepositoryResolutionDecision decision = await context.CrossRepositoryResolutionDecisions
                    .SingleAsync(value => value.Id == group.ExactResolutionDecisionId);
                DocumentEdge edge = await context.DocumentEdges.SingleAsync(value => value.Id == group.ExactResolutionEdgeId);
                SourceAnchor consumerAnchor = await context.SourceAnchors.SingleAsync(
                    value => value.DocumentVersionId == consumer.DocumentVersion.Id);
                FlowTraceStep relationshipStep = await context.FlowTraceSteps.SingleAsync(
                    value => value.EdgeId == edge.Id);
                DocumentVersion relationshipDocument = await context.DocumentVersions.SingleAsync(
                    value => value.Id == relationshipStep.DocumentVersionId);
                ExactResolutionProviderEvidenceContribution[] contributions = await context
                    .ExactResolutionProviderEvidenceContributions
                    .Where(value => value.ResolutionDecisionId == decision.Id)
                    .OrderBy(value => value.ProviderOrdinal).ThenBy(value => value.EvidenceOrdinal)
                    .ToArrayAsync();
                if (additionalJob)
                {
                    await CloneIndependentLaterGraphAsync(
                        context,
                        pair,
                        job,
                        group,
                        execution,
                        decision,
                        contributions,
                        group.LogicalDocumentStableKey);
                }
                Pass2JobExecution? secondJob = null;
                FlowGroup? secondGroup = null;
                Pass2Execution? secondExecution = null;
                CrossRepositoryResolutionDecision? secondDecision = null;
                DocumentEdge? secondEdge = null;
                ExactResolutionProviderEvidenceContribution[] secondContributions = [];
                string? secondConsumerAnchorHandle = null;
                string? secondRelationshipHandle = null;
                if (additionalJob)
                {
                    secondJob = await context.Pass2JobExecutions
                        .OrderBy(value => value.GroupOrder).Skip(1).SingleAsync();
                    secondGroup = await context.FlowGroups.SingleAsync(value => value.Id == secondJob.FlowGroupId);
                    secondExecution = await context.Pass2Executions.SingleAsync(value => value.Id == secondJob.Pass2ExecutionId);
                    secondDecision = await context.CrossRepositoryResolutionDecisions
                        .SingleAsync(value => value.Id == secondGroup.ExactResolutionDecisionId);
                    secondEdge = await context.DocumentEdges.SingleAsync(value => value.Id == secondGroup.ExactResolutionEdgeId);
                    secondContributions = await context.ExactResolutionProviderEvidenceContributions
                        .Where(value => value.ResolutionDecisionId == secondDecision.Id)
                        .OrderBy(value => value.ProviderOrdinal).ThenBy(value => value.EvidenceOrdinal)
                        .ToArrayAsync();
                    secondConsumerAnchorHandle = Pass2InitialEvidenceHandle.Create(
                        secondJob.Id,
                        secondJob.AttemptCount,
                        Pass2CodeLogicRetrievalItemKind.SourceAnchor,
                        consumer.DocumentVersion.Id,
                        consumer.DocumentVersion.RepositoryId,
                        consumer.DocumentVersion.ScanRunId,
                        consumer.DocumentVersion.ProfiledRevision,
                        sourceAnchorId: consumerAnchor.Id,
                        byteStart: consumerAnchor.ByteStart,
                        byteEnd: consumerAnchor.ByteEnd,
                        sourceHash: consumerAnchor.SourceHash);
                    secondRelationshipHandle = Pass2InitialEvidenceHandle.Create(
                        secondJob.Id,
                        secondJob.AttemptCount,
                        Pass2CodeLogicRetrievalItemKind.Relationship,
                        relationshipDocument.Id,
                        relationshipDocument.RepositoryId,
                        relationshipDocument.ScanRunId,
                        relationshipDocument.ProfiledRevision,
                        edgeId: secondEdge.Id);
                }
                return new(
                    database,
                    sourceFixture,
                    context,
                    new Pass2BusinessLogicDocumentCommitter(context, scope),
                    scope,
                    job,
                    group,
                    execution,
                    decision,
                    edge,
                    contributions,
                    consumer,
                    secondJob,
                    secondGroup,
                    secondExecution,
                    secondDecision,
                    secondEdge,
                    secondContributions,
                    Pass2InitialEvidenceHandle.Create(
                        job.Id,
                        job.AttemptCount,
                        Pass2CodeLogicRetrievalItemKind.SourceAnchor,
                        consumer.DocumentVersion.Id,
                        consumer.DocumentVersion.RepositoryId,
                        consumer.DocumentVersion.ScanRunId,
                        consumer.DocumentVersion.ProfiledRevision,
                        sourceAnchorId: consumerAnchor.Id,
                        byteStart: consumerAnchor.ByteStart,
                        byteEnd: consumerAnchor.ByteEnd,
                        sourceHash: consumerAnchor.SourceHash),
                    Pass2InitialEvidenceHandle.Create(
                        job.Id,
                        job.AttemptCount,
                        Pass2CodeLogicRetrievalItemKind.Relationship,
                        relationshipDocument.Id,
                        relationshipDocument.RepositoryId,
                        relationshipDocument.ScanRunId,
                        relationshipDocument.ProfiledRevision,
                        edgeId: edge.Id),
                    secondConsumerAnchorHandle,
                    secondRelationshipHandle,
                    administrator);
            }
            catch
            {
                if (context is not null)
                {
                    await context.DisposeAsync();
                }

                if (sourceFixture is not null)
                {
                    await sourceFixture.DisposeAsync();
                }

                await database.DisposeAsync();
                throw;
            }
        }

        internal Pass2BusinessLogicCommitRequest CreateRequest()
        {
            string handle = ConsumerAnchorHandle;
            string relationshipHandle = RelationshipHandle;
            string schema = "business-logic/flow-v1";
            var candidate = new
            {
                schemaIdentity = schema,
                binding = new
                {
                    jobId = Job.Id,
                    pass2ExecutionId = Execution.Id,
                    flowGroupId = Group.Id,
                    systemId = Execution.SystemId,
                    consumerRepositoryId = Group.RepositoryId,
                    providerRepositoryId = Group.ProviderRepositoryId!.Value,
                    protocol = Group.Protocol!.Value.ToString(),
                    normalizedEndpointIdentifierFingerprint = Hash(Group.NormalizedEndpointIdentifier!),
                    consumerEntryStableKey = Group.ConsumerEntryStableKey!,
                    logicalKeySchemaIdentity = "business-flow-logical-key/1.0",
                    logicalDocumentStableKey = Group.LogicalDocumentStableKey,
                    exactResolutionDecisionId = Decision.Id,
                    exactResolutionDecisionFingerprint = Decision.EvidenceFingerprint,
                    exactResolutionEdgeId = Edge.Id,
                    providerEvidenceFingerprint = Hash(string.Join(
                        "\n",
                        new[] { "provider-evidence-set/1.0" }.Concat(
                            Contributions.Select(value => value.EvidenceFingerprint)))),
                    planFingerprint = Group.PlanFingerprint,
                    membershipFingerprint = Group.MembershipFingerprint!,
                    promptIdentity = "pass2-business-logic-worker.v1",
                    submissionSchemaIdentity = schema,
                },
                title = "Validated business flow",
                summary = new { description = "The bounded flow is supported.", evidenceHandleRefs = new[] { handle } },
                participants = new[]
                {
                    new { order = 1, role = "Consumer", name = "Consumer", responsibility = "Starts the flow.", evidenceHandleRefs = new[] { handle } },
                    new { order = 2, role = "Provider", name = "Provider", responsibility = "Handles the flow.", evidenceHandleRefs = new[] { handle } },
                },
                flowSteps = new[]
                {
                    new { order = 1, actor = "Consumer", action = "Sends the request.", outcome = "The provider handles it.", relationshipKind = "References", evidenceHandleRefs = new[] { relationshipHandle } },
                },
                businessRules = Array.Empty<object>(),
                dataMovements = Array.Empty<object>(),
                failurePaths = Array.Empty<object>(),
                unresolvedStatements = Array.Empty<object>(),
            };
            return new(
                Job.Id,
                InitialLeaseToken,
                InitialAttemptCount,
                JsonSerializer.SerializeToElement(candidate, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                }),
                [new Pass2BusinessLogicEvidenceBinding(
                    handle,
                    Consumer.DocumentVersion.Id,
                    Consumer.DocumentVersion.RepositoryId,
                    Consumer.DocumentVersion.ScanRunId,
                    Consumer.DocumentVersion.ProfiledRevision,
                    SourceAnchorId: null)],
                "pass2-business-logic-worker.v1");
        }

        internal async Task<Pass2BusinessLogicCommitRequest> PrepareSecondRequestAsync()
        {
            if (SecondJob is null || SecondGroup is null || SecondExecution is null ||
                SecondDecision is null || SecondEdge is null)
            {
                throw new InvalidOperationException("The second persisted Pass 2 job is not available.");
            }

            JsonObject candidate = JsonNode.Parse(CreateRequest().Candidate.GetRawText())!.AsObject();
            if (SecondConsumerAnchorHandle is null || SecondRelationshipHandle is null)
            {
                throw new InvalidOperationException("The second persisted evidence handles are unavailable.");
            }
            candidate["summary"]!["evidenceHandleRefs"]![0] = SecondConsumerAnchorHandle;
            candidate["participants"]![0]!["evidenceHandleRefs"]![0] = SecondConsumerAnchorHandle;
            candidate["participants"]![1]!["evidenceHandleRefs"]![0] = SecondConsumerAnchorHandle;
            candidate["flowSteps"]![0]!["evidenceHandleRefs"]![0] = SecondRelationshipHandle;
            JsonObject binding = candidate["binding"]!.AsObject();
            binding["jobId"] = SecondJob.Id;
            binding["pass2ExecutionId"] = SecondExecution.Id;
            binding["flowGroupId"] = SecondGroup.Id;
            binding["systemId"] = SecondExecution.SystemId;
            binding["consumerRepositoryId"] = SecondGroup.RepositoryId;
            binding["providerRepositoryId"] = SecondGroup.ProviderRepositoryId!.Value;
            binding["protocol"] = SecondGroup.Protocol!.Value.ToString();
            binding["normalizedEndpointIdentifierFingerprint"] = Hash(SecondGroup.NormalizedEndpointIdentifier!);
            binding["consumerEntryStableKey"] = SecondGroup.ConsumerEntryStableKey!;
            binding["logicalDocumentStableKey"] = SecondGroup.LogicalDocumentStableKey;
            binding["exactResolutionDecisionId"] = SecondDecision.Id;
            binding["exactResolutionDecisionFingerprint"] = SecondDecision.EvidenceFingerprint;
            binding["exactResolutionEdgeId"] = SecondEdge.Id;
            binding["providerEvidenceFingerprint"] = Hash(string.Join(
                "\n",
                new[] { "provider-evidence-set/1.0" }.Concat(
                    SecondContributions.Select(value => value.EvidenceFingerprint))));
            binding["planFingerprint"] = SecondGroup.PlanFingerprint;
            binding["membershipFingerprint"] = SecondGroup.MembershipFingerprint!;
            return new(
                SecondJob.Id,
                SecondJob.LeaseToken!.Value,
                SecondJob.AttemptCount,
                JsonSerializer.SerializeToElement(candidate),
                [new Pass2BusinessLogicEvidenceBinding(
                    SecondConsumerAnchorHandle,
                    Consumer.DocumentVersion.Id,
                    Consumer.DocumentVersion.RepositoryId,
                    Consumer.DocumentVersion.ScanRunId,
                    Consumer.DocumentVersion.ProfiledRevision)],
                "pass2-business-logic-worker.v1");
        }

        internal async Task<Pass2BusinessLogicCommitRequest> PrepareSecondRequestWithStableKeyAsync(
            string stableKey)
        {
            if (SecondGroup is null)
            {
                throw new InvalidOperationException("The second persisted Pass 2 group is not available.");
            }

            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"vulgata\".\"FlowGroups\" SET \"LogicalDocumentStableKey\" = {stableKey} WHERE \"Id\" = {SecondGroup.Id}");
            Context.ChangeTracker.Clear();
            SecondGroup = await Context.FlowGroups.SingleAsync(value => value.Id == SecondGroup.Id);
            return await PrepareSecondRequestAsync();
        }

        internal async Task<Pass2BusinessLogicCommitRequest> PrepareSecondRequestWithChangedEndpointAsync()
        {
            if (SecondGroup is null || SecondDecision is null)
            {
                throw new InvalidOperationException("The second persisted Pass 2 authority is not available.");
            }

            const string changedEndpoint = "cust/createSession";
            Guid previousDecisionId = SecondDecision.Id;
            ExactCrossRepositoryEdgeEvidence exactEvidence = await Context
                .ExactCrossRepositoryEdgeEvidence.SingleAsync(value => value.EdgeId == SecondEdge!.Id);
            DocumentVersion consumerDocument = await Context.DocumentVersions.AsNoTracking()
                .SingleAsync(value => value.Id == exactEvidence.ConsumerDocumentVersionId);
            DocumentVersion providerDocument = await Context.DocumentVersions.AsNoTracking()
                .SingleAsync(value => value.Id == exactEvidence.ProviderDocumentVersionId);
            SetProperty(exactEvidence, nameof(ExactCrossRepositoryEdgeEvidence.NormalizedEndpointIdentifier),
                changedEndpoint);
            string changedEvidenceFingerprint = ExactResolutionCanonicalAuthority.EvidenceFingerprint(
                exactEvidence, consumerDocument.ProfiledRevision, providerDocument.ProfiledRevision);
            SetProperty(exactEvidence, nameof(ExactCrossRepositoryEdgeEvidence.EvidenceFingerprint),
                changedEvidenceFingerprint);
            Guid changedDecisionId = ExactResolutionCanonicalAuthority.DecisionId(changedEvidenceFingerprint);
            CrossRepositoryResolutionDecision changedDecision = NewEntity<CrossRepositoryResolutionDecision>();
            SetProperty(changedDecision, nameof(CrossRepositoryResolutionDecision.Id), changedDecisionId);
            SetProperty(changedDecision, nameof(CrossRepositoryResolutionDecision.Pass1PairExecutionId),
                SecondExecution!.Pass1PairExecutionId);
            SetProperty(changedDecision, nameof(CrossRepositoryResolutionDecision.CoordinationId),
                SecondDecision.CoordinationId);
            SetProperty(changedDecision, nameof(CrossRepositoryResolutionDecision.SystemId), SecondDecision.SystemId);
            SetProperty(changedDecision, nameof(CrossRepositoryResolutionDecision.ConsumerCandidateId),
                SecondDecision.ConsumerCandidateId);
            SetProperty(changedDecision, nameof(CrossRepositoryResolutionDecision.ConsumerEntryIdentity),
                exactEvidence.OccurrenceId);
            SetProperty(changedDecision, nameof(CrossRepositoryResolutionDecision.ConsumerEntryStableKey),
                SecondDecision.ConsumerEntryStableKey);
            SetProperty(changedDecision, nameof(CrossRepositoryResolutionDecision.ConsumerDocumentVersionId),
                SecondDecision.ConsumerDocumentVersionId);
            SetProperty(changedDecision, nameof(CrossRepositoryResolutionDecision.Protocol), SecondDecision.Protocol);
            SetProperty(changedDecision, nameof(CrossRepositoryResolutionDecision.NormalizedEndpointIdentifier),
                changedEndpoint);
            SetProperty(changedDecision, nameof(CrossRepositoryResolutionDecision.Outcome), SecondDecision.Outcome);
            SetProperty(changedDecision, nameof(CrossRepositoryResolutionDecision.ProviderDocumentVersionId),
                SecondDecision.ProviderDocumentVersionId);
            SetProperty(changedDecision, nameof(CrossRepositoryResolutionDecision.DocumentEdgeId), SecondDecision.DocumentEdgeId);
            SetProperty(changedDecision, nameof(CrossRepositoryResolutionDecision.UncertaintyId), SecondDecision.UncertaintyId);
            SetProperty(changedDecision, nameof(CrossRepositoryResolutionDecision.EvidenceFingerprint),
                changedEvidenceFingerprint);
            SetProperty(changedDecision, nameof(CrossRepositoryResolutionDecision.CreatedAt), SecondDecision.CreatedAt);
            Context.CrossRepositoryResolutionDecisions.Add(changedDecision);
            List<ExactResolutionProviderEvidenceContribution> changedContributions = [];
            foreach (ExactResolutionProviderEvidenceContribution contribution in SecondContributions)
            {
                string fingerprint = ExactResolutionCanonicalAuthority.ProviderContributionFingerprint(
                    changedEvidenceFingerprint, contribution.ProviderDocumentVersionId, contribution.SourceAnchorId);
                ExactResolutionProviderEvidenceContribution changedContribution =
                    NewEntity<ExactResolutionProviderEvidenceContribution>();
                SetProperty(changedContribution, nameof(ExactResolutionProviderEvidenceContribution.Id),
                    ExactResolutionCanonicalAuthority.ProviderContributionId(fingerprint));
                SetProperty(changedContribution, nameof(ExactResolutionProviderEvidenceContribution.ResolutionDecisionId),
                    changedDecisionId);
                SetProperty(changedContribution, nameof(ExactResolutionProviderEvidenceContribution.ProviderDocumentVersionId),
                    contribution.ProviderDocumentVersionId);
                SetProperty(changedContribution, nameof(ExactResolutionProviderEvidenceContribution.SourceAnchorId),
                    contribution.SourceAnchorId);
                SetProperty(changedContribution, nameof(ExactResolutionProviderEvidenceContribution.EvidenceFingerprint), fingerprint);
                SetProperty(changedContribution, nameof(ExactResolutionProviderEvidenceContribution.ProviderOrdinal), contribution.ProviderOrdinal);
                SetProperty(changedContribution, nameof(ExactResolutionProviderEvidenceContribution.EvidenceOrdinal), contribution.EvidenceOrdinal);
                SetProperty(changedContribution, nameof(ExactResolutionProviderEvidenceContribution.CreatedAt), contribution.CreatedAt);
                Context.ExactResolutionProviderEvidenceContributions.Add(changedContribution);
                changedContributions.Add(changedContribution);
            }
            await Context.SaveChangesAsync();
            string changedStableKey = "business-flow/v1/" + Hash(string.Join(
                '\n',
                "business-flow-logical-key/1.0",
                SecondExecution.SystemId.ToString("D"),
                SecondGroup.RepositoryId.ToString("D"),
                SecondGroup.ProviderRepositoryId!.Value.ToString("D"),
                SecondGroup.Protocol!.Value.ToString(),
                changedEndpoint,
                SecondGroup.ConsumerEntryStableKey!));
            string changedPlanFingerprint = PreparedPass2FlowPlanner.ComputePlanFingerprint(
                changedDecisionId, changedEvidenceFingerprint, "Complete",
                SecondGroup.MembershipFingerprint!, changedContributions);
            await Context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "vulgata"."FlowGroups"
                SET "NormalizedEndpointIdentifier" = {changedEndpoint},
                    "LogicalDocumentStableKey" = {changedStableKey},
                    "ExactResolutionDecisionId" = {changedDecisionId},
                    "PlanFingerprint" = {changedPlanFingerprint}
                WHERE "Id" = {SecondGroup.Id};
                UPDATE "vulgata"."FlowTraceSteps"
                SET "ExactResolutionDecisionId" = {changedDecisionId}
                WHERE "ExactResolutionDecisionId" = {previousDecisionId}
                  AND "FlowTraceId" IN (
                      SELECT "Id" FROM "vulgata"."FlowTraces" WHERE "FlowGroupId" = {SecondGroup.Id});
                """);
            Context.ChangeTracker.Clear();
            SecondGroup = await Context.FlowGroups.SingleAsync(value => value.Id == SecondGroup.Id);
            SecondDecision = await Context.CrossRepositoryResolutionDecisions
                .SingleAsync(value => value.Id == changedDecisionId);
            SecondContributions = await Context.ExactResolutionProviderEvidenceContributions
                .Where(value => value.ResolutionDecisionId == changedDecisionId)
                .OrderBy(value => value.ProviderOrdinal).ThenBy(value => value.EvidenceOrdinal)
                .ToArrayAsync();
            return await PrepareSecondRequestAsync();
        }

        internal async Task<(Pass2BusinessLogicCommitRequest Request, Pass2JobExecution Job)>
            PrepareThirdRequestWithStableKeyAsync()
        {
            if (SecondJob is null || SecondGroup is null || SecondExecution is null ||
                SecondDecision is null || SecondEdge is null)
            {
                throw new InvalidOperationException("The second persisted Pass 2 authority is not available.");
            }

            Pass1PairExecution secondPair = await Context.Pass1PairExecutions.SingleAsync(
                value => value.Id == SecondExecution.Pass1PairExecutionId);
            await CloneIndependentLaterGraphAsync(
                Context,
                secondPair,
                SecondJob,
                SecondGroup,
                SecondExecution,
                SecondDecision,
                SecondContributions,
                Group.LogicalDocumentStableKey);

            Pass2JobExecution thirdJob = await Context.Pass2JobExecutions
                .OrderBy(value => value.GroupOrder).Skip(2).SingleAsync();
            FlowGroup thirdGroup = await Context.FlowGroups.SingleAsync(
                value => value.Id == thirdJob.FlowGroupId);
            Pass2Execution thirdExecution = await Context.Pass2Executions.SingleAsync(
                value => value.Id == thirdJob.Pass2ExecutionId);
            CrossRepositoryResolutionDecision thirdDecision =
                await Context.CrossRepositoryResolutionDecisions.SingleAsync(
                    value => value.Id == thirdGroup.ExactResolutionDecisionId);
            DocumentEdge thirdEdge = await Context.DocumentEdges.SingleAsync(
                value => value.Id == thirdGroup.ExactResolutionEdgeId);
            SourceAnchor consumerAnchor = await Context.SourceAnchors.SingleAsync(
                value => value.DocumentVersionId == Consumer.DocumentVersion.Id);
            FlowTraceStep relationshipStep = await Context.FlowTraceSteps.SingleAsync(
                value => value.FlowTrace.FlowGroupId == thirdGroup.Id && value.EdgeId == thirdEdge.Id);
            DocumentVersion relationshipDocument = await Context.DocumentVersions.SingleAsync(
                value => value.Id == relationshipStep.DocumentVersionId);
            ExactResolutionProviderEvidenceContribution[] thirdContributions = await Context
                .ExactResolutionProviderEvidenceContributions
                .Where(value => value.ResolutionDecisionId == thirdDecision.Id)
                .OrderBy(value => value.ProviderOrdinal).ThenBy(value => value.EvidenceOrdinal)
                .ToArrayAsync();
            string consumerHandle = Pass2InitialEvidenceHandle.Create(
                thirdJob.Id,
                thirdJob.AttemptCount,
                Pass2CodeLogicRetrievalItemKind.SourceAnchor,
                Consumer.DocumentVersion.Id,
                Consumer.DocumentVersion.RepositoryId,
                Consumer.DocumentVersion.ScanRunId,
                Consumer.DocumentVersion.ProfiledRevision,
                sourceAnchorId: consumerAnchor.Id,
                byteStart: consumerAnchor.ByteStart,
                byteEnd: consumerAnchor.ByteEnd,
                sourceHash: consumerAnchor.SourceHash);
            string relationshipHandle = Pass2InitialEvidenceHandle.Create(
                thirdJob.Id,
                thirdJob.AttemptCount,
                Pass2CodeLogicRetrievalItemKind.Relationship,
                relationshipDocument.Id,
                relationshipDocument.RepositoryId,
                relationshipDocument.ScanRunId,
                relationshipDocument.ProfiledRevision,
                edgeId: thirdEdge.Id);

            JsonObject candidate = JsonNode.Parse(CreateRequest().Candidate.GetRawText())!.AsObject();
            candidate["summary"]!["evidenceHandleRefs"]![0] = consumerHandle;
            candidate["participants"]![0]!["evidenceHandleRefs"]![0] = consumerHandle;
            candidate["participants"]![1]!["evidenceHandleRefs"]![0] = consumerHandle;
            candidate["flowSteps"]![0]!["evidenceHandleRefs"]![0] = relationshipHandle;
            JsonObject binding = candidate["binding"]!.AsObject();
            binding["jobId"] = thirdJob.Id;
            binding["pass2ExecutionId"] = thirdExecution.Id;
            binding["flowGroupId"] = thirdGroup.Id;
            binding["systemId"] = thirdExecution.SystemId;
            binding["consumerRepositoryId"] = thirdGroup.RepositoryId;
            binding["providerRepositoryId"] = thirdGroup.ProviderRepositoryId!.Value;
            binding["protocol"] = thirdGroup.Protocol!.Value.ToString();
            binding["normalizedEndpointIdentifierFingerprint"] = Hash(
                thirdGroup.NormalizedEndpointIdentifier!);
            binding["consumerEntryStableKey"] = thirdGroup.ConsumerEntryStableKey!;
            binding["logicalDocumentStableKey"] = thirdGroup.LogicalDocumentStableKey;
            binding["exactResolutionDecisionId"] = thirdDecision.Id;
            binding["exactResolutionDecisionFingerprint"] = thirdDecision.EvidenceFingerprint;
            binding["exactResolutionEdgeId"] = thirdEdge.Id;
            binding["providerEvidenceFingerprint"] = Hash(string.Join(
                "\n",
                new[] { "provider-evidence-set/1.0" }.Concat(
                    thirdContributions.Select(value => value.EvidenceFingerprint))));
            binding["planFingerprint"] = thirdGroup.PlanFingerprint;
            binding["membershipFingerprint"] = thirdGroup.MembershipFingerprint!;
            return (new(
                thirdJob.Id,
                thirdJob.LeaseToken!.Value,
                thirdJob.AttemptCount,
                JsonSerializer.SerializeToElement(candidate),
                [new Pass2BusinessLogicEvidenceBinding(
                    consumerHandle,
                    Consumer.DocumentVersion.Id,
                    Consumer.DocumentVersion.RepositoryId,
                    Consumer.DocumentVersion.ScanRunId,
                    Consumer.DocumentVersion.ProfiledRevision)],
                "pass2-business-logic-worker.v1"), thirdJob);
        }

        internal async Task MutateSecondStableKeyAsync(string stableKey)
        {
            if (SecondGroup is null)
            {
                throw new InvalidOperationException("The second persisted Pass 2 group is not available.");
            }

            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"vulgata\".\"FlowGroups\" SET \"LogicalDocumentStableKey\" = {stableKey} WHERE \"Id\" = {SecondGroup.Id}");
            Context.ChangeTracker.Clear();
        }

        internal async Task<Guid> BusinessLogicalDocumentIdAsync() =>
            await Context.LogicalDocuments.Where(value =>
                value.RepositoryId == ConsumerRepositoryId && value.DocumentType == DocumentType.BusinessLogic)
                .Select(value => value.Id).SingleAsync();

        internal Task<int> BusinessLogicalDocumentCountAsync() =>
            Context.LogicalDocuments.CountAsync(value =>
                value.RepositoryId == ConsumerRepositoryId && value.DocumentType == DocumentType.BusinessLogic);

        internal Task<int> BusinessVersionCountAsync(Guid logicalId) =>
            Context.DocumentVersions.CountAsync(value => value.LogicalDocumentId == logicalId);

        internal Task<Guid> BusinessLogicalDocumentIdForVersionAsync(Guid versionId) =>
            Context.DocumentVersions.Where(value => value.Id == versionId)
                .Select(value => value.LogicalDocumentId).SingleAsync();

        internal async Task MoveCurrentPointerAsync(Guid versionId)
        {
            await Context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "vulgata"."LogicalDocuments"
                SET "CurrentVersionId" = {versionId}
                WHERE "Id" = (
                    SELECT "LogicalDocumentId"
                    FROM "vulgata"."DocumentVersions"
                    WHERE "Id" = {versionId});
                """);
            Context.ChangeTracker.Clear();
        }

        internal async Task SetSupersededByAsync(Guid versionId, Guid successorId)
        {
            await Context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "vulgata"."DocumentVersions"
                SET "SupersededById" = {successorId}
                WHERE "Id" = {versionId};
                """);
            Context.ChangeTracker.Clear();
        }

        internal async Task SetVersionNumberAsync(Guid versionId, int versionNumber)
        {
            await Context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "vulgata"."DocumentVersions"
                SET "VersionNumber" = {versionNumber}
                WHERE "Id" = {versionId};
                """);
            Context.ChangeTracker.Clear();
        }

        internal async Task<Guid> CloneDetachedVersionAsync(Guid sourceVersionId)
        {
            Guid detachedVersionId = Guid.NewGuid();
            int nextVersionNumber = await Context.DocumentVersions
                .Where(value => value.LogicalDocumentId ==
                    Context.DocumentVersions
                        .Where(source => source.Id == sourceVersionId)
                        .Select(source => source.LogicalDocumentId)
                        .Single())
                .MaxAsync(value => value.VersionNumber) + 1;
            await Context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "vulgata"."DocumentVersions"
                SELECT (jsonb_populate_record(NULL::"vulgata"."DocumentVersions",
                    to_jsonb(source) || jsonb_build_object(
                        'Id', {detachedVersionId},
                        'VersionNumber', {nextVersionNumber},
                        'SupersededById', NULL))).*
                FROM "vulgata"."DocumentVersions" source
                WHERE source."Id" = {sourceVersionId};
                """);
            Context.ChangeTracker.Clear();
            return detachedVersionId;
        }

        internal async Task<string> PublicationChainSnapshotAsync()
        {
            Guid[] logicalIds = await Context.LogicalDocuments.AsNoTracking()
                .Where(value => value.RepositoryId == ConsumerRepositoryId &&
                    value.DocumentType == DocumentType.BusinessLogic)
                .Select(value => value.Id)
                .OrderBy(value => value)
                .ToArrayAsync();
            var logical = await Context.LogicalDocuments.AsNoTracking()
                .Where(value => logicalIds.Contains(value.Id))
                .OrderBy(value => value.Id)
                .Select(value => new
                {
                    value.Id,
                    value.RepositoryId,
                    value.DocumentType,
                    value.StableKey,
                    value.CurrentVersionId,
                })
                .ToArrayAsync();
            var versions = await Context.DocumentVersions.AsNoTracking()
                .Where(value => logicalIds.Contains(value.LogicalDocumentId))
                .OrderBy(value => value.LogicalDocumentId)
                .ThenBy(value => value.VersionNumber)
                .Select(value => new
                {
                    value.Id,
                    value.LogicalDocumentId,
                    value.RepositoryId,
                    value.SystemId,
                    value.ScanRunId,
                    value.ProfiledRevision,
                    value.VersionNumber,
                    value.SupersededById,
                    value.CommitFingerprint,
                    value.ContentFingerprint,
                    value.CreatedAt,
                })
                .ToArrayAsync();
            return JsonSerializer.Serialize(new { logical, versions });
        }

        internal async Task<string> PublicationSnapshotAsync()
        {
            int logical = await Context.LogicalDocuments.CountAsync(value =>
                value.RepositoryId == ConsumerRepositoryId && value.DocumentType == DocumentType.BusinessLogic);
            int versions = await Context.DocumentVersions.CountAsync();
            int provenance = await Context.DocumentEdges.CountAsync(value =>
                value.EdgeType == DocumentEdgeType.GeneratedFrom);
            int jobs = await Context.Pass2JobExecutions.CountAsync();
            int groups = await Context.FlowGroups.CountAsync();
            return $"{logical}:{versions}:{provenance}:{jobs}:{groups}";
        }

        internal async Task<ExecutionAccounting> ExecutionAccountingAsync(Guid executionId)
        {
            await using NpgsqlConnection connection = new(DatabaseConnectionString);
            await connection.OpenAsync();
            await using NpgsqlCommand columnsCommand = new("""
                SELECT "column_name"
                FROM information_schema.columns
                WHERE "table_schema" = 'vulgata'
                  AND "table_name" = 'Pass2Executions'
                  AND "column_name" IN ('PublishedCount', 'FailedCount', 'StateVersion', 'State')
                ORDER BY "column_name";
                """, connection);
            List<string> columns = [];
            await using (NpgsqlDataReader reader = await columnsCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    columns.Add(reader.GetString(0));
                }
            }

            Assert.Equal(
                ["FailedCount", "PublishedCount", "State", "StateVersion"],
                columns);

            await using NpgsqlCommand command = new("""
                SELECT "PublishedCount", "FailedCount", "StateVersion", "State"
                FROM "vulgata"."Pass2Executions"
                WHERE "Id" = @id;
                """, connection);
            command.Parameters.AddWithValue("id", executionId);
            await using NpgsqlDataReader values = await command.ExecuteReaderAsync();
            Assert.True(await values.ReadAsync(), $"Pass2Execution {executionId:D} was not found.");
            return new(
                values.GetInt32(0),
                values.GetInt32(1),
                values.GetInt64(2),
                values.GetString(3));
        }

        internal readonly record struct ExecutionAccounting(
            int PublishedCount,
            int FailedCount,
            long StateVersion,
            string State);

        internal async Task<string> FlowTraceSnapshotAsync()
        {
            var traces = await Context.FlowTraces.AsNoTracking()
                .Where(value => value.FlowGroupId == Group.Id)
                .OrderBy(value => value.TraceOrder)
                .Select(value => new { value.Id, value.TraceOrder, value.CreatedAt })
                .ToArrayAsync();
            var steps = await Context.FlowTraceSteps.AsNoTracking()
                .Where(value => value.FlowTrace.FlowGroupId == Group.Id)
                .OrderBy(value => value.FlowTraceId).ThenBy(value => value.StepOrder)
                .Select(value => new
                {
                    value.Id,
                    value.FlowTraceId,
                    value.StepOrder,
                    value.DocumentVersionId,
                    value.DocumentRepositoryId,
                    value.DocumentScanRunId,
                    value.EdgeId,
                    value.ExactResolutionDecisionId,
                    value.StepKind,
                    value.Description,
                    value.CreatedAt,
                })
                .ToArrayAsync();
            return JsonSerializer.Serialize(new { traces, steps });
        }

        internal async Task MutateReplayAuthorityAsync(string mutation, Guid publishedVersionId)
        {
            switch (mutation)
            {
                case "decision-contribution":
                    await Context.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE "vulgata"."ExactResolutionProviderEvidenceContributions"
                        SET "EvidenceFingerprint" = repeat('e', 64)
                        WHERE "ResolutionDecisionId" = {Decision.Id};
                        """);
                    break;
                case "references-edge":
                    await Context.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE "vulgata"."DocumentEdges"
                        SET "TargetDocumentVersionId" = {Consumer.DocumentVersion.Id},
                            "TargetRepositoryId" = {Consumer.DocumentVersion.RepositoryId},
                            "TargetScanRunId" = {Consumer.DocumentVersion.ScanRunId}
                        WHERE "Id" = {Edge.Id};
                        """);
                    break;
                case "source-anchor":
                    await Context.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE "vulgata"."SourceAnchors"
                        SET "SourceHash" = repeat('c', 64), "ByteStart" = 0, "ByteEnd" = 1
                        WHERE "DocumentVersionId" = {Consumer.DocumentVersion.Id};
                        """);
                    break;
                case "sections":
                    await Context.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE "vulgata"."DocumentSections"
                        SET "ContentMarkdown" = 'tampered section'
                        WHERE "DocumentVersionId" = {publishedVersionId};
                        """);
                    break;
                case "index":
                    await Context.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE "vulgata"."IndexEntries"
                        SET "Summary" = 'tampered index'
                        WHERE "DocumentVersionId" = {publishedVersionId};
                        """);
                    break;
                case "flow-trace":
                    await Context.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE "vulgata"."FlowTraceSteps"
                        SET "Description" = 'tampered trace step'
                        WHERE "FlowTraceId" IN (
                            SELECT "Id" FROM "vulgata"."FlowTraces" WHERE "FlowGroupId" = {Group.Id});
                        """);
                    break;
                case "logical-current-pointer":
                    await Context.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE "vulgata"."LogicalDocuments"
                        SET "CurrentVersionId" = NULL
                        WHERE "Id" = (
                            SELECT "LogicalDocumentId" FROM "vulgata"."DocumentVersions"
                            WHERE "Id" = {publishedVersionId});
                        """);
                    break;
                case "provenance":
                    await Context.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE "vulgata"."DocumentEdges"
                        SET "CreatedAt" = "CreatedAt" + INTERVAL '1 second'
                        WHERE "SourceDocumentVersionId" = {publishedVersionId}
                          AND "EdgeType" = 'GeneratedFrom';
                        """);
                    break;
                case "job-execution-graph":
                    await Context.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE "vulgata"."Pass2Executions"
                        SET "State" = 'Planning'
                        WHERE "Id" = {Execution.Id};
                        """);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation));
            }

            Context.ChangeTracker.Clear();
        }

        internal Task InstallFailureTriggerAsync(string tableName, string operation)
        {
            string functionName = $"t034_fail_{tableName.ToLowerInvariant()}";
            string triggerName = $"t034_fail_trigger_{tableName.ToLowerInvariant()}";
            string triggerEvent = operation == "UPDATE" ? "BEFORE UPDATE" : "BEFORE INSERT";
            string sql = $"""
                CREATE OR REPLACE FUNCTION vulgata."{functionName}"() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 't034 injected publication failure at {tableName}';
                END;
                $$;
                DROP TRIGGER IF EXISTS "{triggerName}" ON vulgata."{tableName}";
                CREATE TRIGGER "{triggerName}"
                    {triggerEvent} ON vulgata."{tableName}"
                    FOR EACH ROW EXECUTE FUNCTION vulgata."{functionName}"();
                """;
            return Context.Database.ExecuteSqlRawAsync(sql);
        }

        internal async Task<(Guid DecisionId, string Fingerprint)> AddSecondDecisionReferencingSharedEdgeAsync()
        {
            Guid duplicateId = Guid.NewGuid();
            string fingerprint = Hash($"shared-decision/{duplicateId:D}");
            await Context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "vulgata"."CrossRepositoryResolutionDecisions"
                    ("Id", "Pass1PairExecutionId", "CoordinationId", "SystemId",
                     "ConsumerCandidateId", "ConsumerEntryIdentity", "ConsumerEntryStableKey",
                     "ConsumerDocumentVersionId", "Protocol", "NormalizedEndpointIdentifier",
                     "Outcome", "ProviderDocumentVersionId", "DocumentEdgeId", "UncertaintyId",
                     "EvidenceFingerprint", "CreatedAt")
                SELECT {duplicateId}, "Pass1PairExecutionId", "CoordinationId", "SystemId",
                       "ConsumerCandidateId", "ConsumerEntryIdentity", "ConsumerEntryStableKey",
                       "ConsumerDocumentVersionId", "Protocol", "NormalizedEndpointIdentifier",
                       "Outcome", "ProviderDocumentVersionId", "DocumentEdgeId", "UncertaintyId",
                       {fingerprint}, "CreatedAt"
                FROM "vulgata"."CrossRepositoryResolutionDecisions"
                WHERE "Id" = {Decision.Id};
                """);
            return (duplicateId, fingerprint);
        }

        private static async Task CloneIndependentLaterGraphAsync(
            VulgataDbContext context,
            Pass1PairExecution pair,
            Pass2JobExecution job,
            FlowGroup group,
            Pass2Execution execution,
            CrossRepositoryResolutionDecision decision,
            IReadOnlyList<ExactResolutionProviderEvidenceContribution> contributions,
            string stableKey)
        {
            Guid coordinationId = Guid.NewGuid();
            Guid pairId = Guid.NewGuid();
            Guid executionId = Guid.NewGuid();
            Guid edgeId = Guid.NewGuid();
            Guid groupId = Guid.NewGuid();
            Guid jobId = Guid.NewGuid();
            Guid leaseToken = Guid.NewGuid();
            string pairPackageFingerprint = Hash($"later-pair-package/{pairId:D}");
            string pairSelectionFingerprint = Hash($"later-pair-selection/{pairId:D}");
            string pairFingerprint = Hash($"later-pair/{pairId:D}");
            ExactCrossRepositoryEdgeEvidence sourceEvidence = await context
                .ExactCrossRepositoryEdgeEvidence.AsNoTracking()
                .SingleAsync(value => value.EdgeId == group.ExactResolutionEdgeId);
            DocumentVersion sourceConsumer = await context.DocumentVersions.AsNoTracking()
                .SingleAsync(value => value.Id == sourceEvidence.ConsumerDocumentVersionId);
            DocumentVersion sourceProvider = await context.DocumentVersions.AsNoTracking()
                .SingleAsync(value => value.Id == sourceEvidence.ProviderDocumentVersionId);
            SourceAnchor sourceProviderAnchor = await context.SourceAnchors.AsNoTracking()
                .SingleAsync(value => value.Id == sourceEvidence.ProviderSourceAnchorId);
            LogicalDocument sourceProviderLogical = await context.LogicalDocuments.AsNoTracking()
                .SingleAsync(value => value.Id == sourceProvider.LogicalDocumentId);
            Guid laterProviderLogicalDocumentId = Guid.NewGuid();
            Guid laterProviderDocumentId = Guid.NewGuid();
            Guid laterProviderAnchorId = Guid.NewGuid();
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "vulgata"."LogicalDocuments"
                SELECT (jsonb_populate_record(NULL::"vulgata"."LogicalDocuments",
                    to_jsonb(source) || jsonb_build_object(
                        'Id', {laterProviderLogicalDocumentId},
                        'StableKey', source."StableKey" || '-later-{pairId:D}',
                        'CurrentVersionId', NULL))).*
                FROM "vulgata"."LogicalDocuments" source
                WHERE source."Id" = {sourceProviderLogical.Id};
                INSERT INTO "vulgata"."DocumentVersions"
                SELECT (jsonb_populate_record(NULL::"vulgata"."DocumentVersions",
                    to_jsonb(source) || jsonb_build_object(
                        'Id', {laterProviderDocumentId},
                        'LogicalDocumentId', {laterProviderLogicalDocumentId},
                        'VersionNumber', "VersionNumber" + 1000,
                        'SupersededById', NULL))).*
                FROM "vulgata"."DocumentVersions" source
                WHERE source."Id" = {sourceProvider.Id};
                INSERT INTO "vulgata"."SourceAnchors"
                SELECT (jsonb_populate_record(NULL::"vulgata"."SourceAnchors",
                    to_jsonb(source) || jsonb_build_object(
                        'Id', {laterProviderAnchorId},
                        'DocumentVersionId', {laterProviderDocumentId}))).*
                FROM "vulgata"."SourceAnchors" source
                WHERE source."Id" = {sourceProviderAnchor.Id};
                UPDATE "vulgata"."LogicalDocuments"
                SET "CurrentVersionId" = {laterProviderDocumentId}
                WHERE "Id" = {laterProviderLogicalDocumentId};
                """);
            ExactCrossRepositoryEdgeEvidence laterEvidence = new(
                edgeId,
                sourceEvidence.ConsumerSourceAnchorId,
                sourceEvidence.ConsumerDocumentVersionId,
                sourceEvidence.ConsumerRepositoryId,
                sourceEvidence.ConsumerScanRunId,
                laterProviderAnchorId,
                laterProviderDocumentId,
                sourceEvidence.ProviderRepositoryId,
                sourceEvidence.ProviderScanRunId,
                sourceEvidence.ConsumerRawIdentifier,
                sourceEvidence.ProviderRawIdentifier,
                sourceEvidence.NormalizedEndpointIdentifier,
                sourceEvidence.Protocol,
                sourceEvidence.ResolutionKind,
                sourceEvidence.Confidence,
                sourceEvidence.ProfileSchemaVersion,
                sourceEvidence.ProfileId,
                sourceEvidence.ProfileVersion,
                sourceEvidence.ProfileFingerprint,
                $"{sourceEvidence.OccurrenceId}/later/{pairId:D}",
                sourceEvidence.ConsumerEvidenceLocator,
                sourceEvidence.ProviderEvidenceLocator,
                string.Empty,
                sourceEvidence.CreatedAt);
            string decisionFingerprint = ExactResolutionCanonicalAuthority.EvidenceFingerprint(
                laterEvidence,
                sourceConsumer.ProfiledRevision,
                sourceProvider.ProfiledRevision);
            SetProperty(laterEvidence, nameof(ExactCrossRepositoryEdgeEvidence.EvidenceFingerprint), decisionFingerprint);
            Guid decisionId = ExactResolutionCanonicalAuthority.DecisionId(decisionFingerprint);
            FlowTrace[] sourceTraces = await context.FlowTraces.AsNoTracking()
                .Where(value => value.FlowGroupId == group.Id)
                .OrderBy(value => value.TraceOrder)
                .ToArrayAsync();
            Guid[] sourceTraceIds = sourceTraces.Select(value => value.Id).ToArray();
            FlowTraceStep[] sourceSteps = await context.FlowTraceSteps.AsNoTracking()
                .Where(value => sourceTraceIds.Contains(value.FlowTraceId))
                .OrderBy(value => value.FlowTraceId).ThenBy(value => value.StepOrder)
                .ToArrayAsync();
            FlowTraceStep[] orderedSourceSteps = sourceSteps
                .OrderBy(value => value.FlowTraceId)
                .ThenBy(value => value.StepOrder)
                .ToArray();
            if (orderedSourceSteps.Length < 5 ||
                orderedSourceSteps[3].EdgeId is not Guid implementationEdgeId ||
                (orderedSourceSteps.Length - 5) % 2 != 0)
            {
                throw new InvalidOperationException(
                    "The accepted Pass 2 planning trace cannot produce canonical fingerprints.");
            }
            DocumentEdge sourceImplementationEdge = await context.DocumentEdges.AsNoTracking()
                .SingleAsync(value => value.Id == implementationEdgeId);
            Guid laterImplementationEdgeId = Guid.NewGuid();
            (Guid EdgeId, Guid DocumentVersionId)[] optionalMembers = orderedSourceSteps
                .Skip(5)
                .Chunk(2)
                .Select(pair => pair[0].EdgeId is Guid edgeId
                    ? (edgeId, pair[1].DocumentVersionId)
                    : throw new InvalidOperationException(
                        "The accepted Pass 2 planning trace has an invalid optional edge."))
                .ToArray();
            string membershipFingerprint = PreparedPass2FlowPlanner.ComputeMembershipFingerprint(
                group.ConsumerDocumentVersionId!.Value,
                edgeId,
                laterProviderDocumentId,
                laterImplementationEdgeId,
                group.ProviderImplementationDocumentVersionId,
                optionalMembers);
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "vulgata"."TwoRepositoryScanCoordinations"
                SELECT (jsonb_populate_record(NULL::"vulgata"."TwoRepositoryScanCoordinations",
                    to_jsonb(source) || jsonb_build_object('Id', {coordinationId}))).*
                FROM "vulgata"."TwoRepositoryScanCoordinations" source
                WHERE source."Id" = {pair.CoordinationId};

                INSERT INTO "vulgata"."Pass1PairExecutions"
                SELECT (jsonb_populate_record(NULL::"vulgata"."Pass1PairExecutions",
                    to_jsonb(source) || jsonb_build_object(
                        'Id', {pairId}, 'CoordinationId', {coordinationId},
                        'PairPackageFingerprint', {pairPackageFingerprint},
                        'PairSelectionFingerprint', {pairSelectionFingerprint},
                        'Pass1PairFingerprint', {pairFingerprint}))).*
                FROM "vulgata"."Pass1PairExecutions" source
                WHERE source."Id" = {pair.Id};

                INSERT INTO "vulgata"."Pass2Executions"
                SELECT (jsonb_populate_record(NULL::"vulgata"."Pass2Executions",
                    to_jsonb(source) || jsonb_build_object(
                        'Id', {executionId}, 'Pass1PairExecutionId', {pairId},
                        'CoordinationId', {coordinationId}, 'State', 'Running'))).*
                FROM "vulgata"."Pass2Executions" source
                WHERE source."Id" = {execution.Id};

                """);

            context.DocumentEdges.Add(new DocumentEdge(
                edgeId,
                sourceEvidence.ConsumerDocumentVersionId,
                sourceEvidence.ConsumerRepositoryId,
                sourceEvidence.ConsumerScanRunId,
                laterProviderDocumentId,
                sourceEvidence.ProviderRepositoryId,
                sourceEvidence.ProviderScanRunId,
                DocumentEdgeType.References,
                sourceEvidence.CreatedAt));
            context.DocumentEdges.Add(new DocumentEdge(
                laterImplementationEdgeId,
                laterProviderDocumentId,
                sourceImplementationEdge.SourceRepositoryId,
                sourceImplementationEdge.SourceScanRunId,
                sourceImplementationEdge.TargetDocumentVersionId,
                sourceImplementationEdge.TargetRepositoryId,
                sourceImplementationEdge.TargetScanRunId,
                sourceImplementationEdge.EdgeType,
                sourceImplementationEdge.CreatedAt));
            context.ExactCrossRepositoryEdgeEvidence.Add(laterEvidence);
            CrossRepositoryResolutionDecision laterDecision = NewEntity<CrossRepositoryResolutionDecision>();
            SetProperty(laterDecision, nameof(CrossRepositoryResolutionDecision.Id), decisionId);
            SetProperty(laterDecision, nameof(CrossRepositoryResolutionDecision.Pass1PairExecutionId), pairId);
            SetProperty(laterDecision, nameof(CrossRepositoryResolutionDecision.CoordinationId), coordinationId);
            SetProperty(laterDecision, nameof(CrossRepositoryResolutionDecision.SystemId), decision.SystemId);
            SetProperty(laterDecision, nameof(CrossRepositoryResolutionDecision.ConsumerCandidateId), decision.ConsumerCandidateId);
            SetProperty(laterDecision, nameof(CrossRepositoryResolutionDecision.ConsumerEntryIdentity), laterEvidence.OccurrenceId);
            SetProperty(laterDecision, nameof(CrossRepositoryResolutionDecision.ConsumerEntryStableKey), decision.ConsumerEntryStableKey);
            SetProperty(laterDecision, nameof(CrossRepositoryResolutionDecision.ConsumerDocumentVersionId), decision.ConsumerDocumentVersionId);
            SetProperty(laterDecision, nameof(CrossRepositoryResolutionDecision.Protocol), decision.Protocol);
            SetProperty(laterDecision, nameof(CrossRepositoryResolutionDecision.NormalizedEndpointIdentifier), decision.NormalizedEndpointIdentifier);
            SetProperty(laterDecision, nameof(CrossRepositoryResolutionDecision.Outcome), decision.Outcome);
            SetProperty(laterDecision, nameof(CrossRepositoryResolutionDecision.ProviderDocumentVersionId), laterProviderDocumentId);
            SetProperty(laterDecision, nameof(CrossRepositoryResolutionDecision.DocumentEdgeId), edgeId);
            SetProperty(laterDecision, nameof(CrossRepositoryResolutionDecision.UncertaintyId), decision.UncertaintyId);
            SetProperty(laterDecision, nameof(CrossRepositoryResolutionDecision.EvidenceFingerprint), decisionFingerprint);
            SetProperty(laterDecision, nameof(CrossRepositoryResolutionDecision.CreatedAt), decision.CreatedAt);
            context.CrossRepositoryResolutionDecisions.Add(laterDecision);
            List<ExactResolutionProviderEvidenceContribution> laterContributions = [];
            foreach (ExactResolutionProviderEvidenceContribution contribution in contributions)
            {
                string fingerprint = ExactResolutionCanonicalAuthority.ProviderContributionFingerprint(
                    decisionFingerprint, laterProviderDocumentId, laterProviderAnchorId);
                ExactResolutionProviderEvidenceContribution laterContribution =
                    NewEntity<ExactResolutionProviderEvidenceContribution>();
                SetProperty(laterContribution, nameof(ExactResolutionProviderEvidenceContribution.Id),
                    ExactResolutionCanonicalAuthority.ProviderContributionId(fingerprint));
                SetProperty(laterContribution, nameof(ExactResolutionProviderEvidenceContribution.ResolutionDecisionId), decisionId);
                SetProperty(laterContribution, nameof(ExactResolutionProviderEvidenceContribution.ProviderDocumentVersionId), laterProviderDocumentId);
                SetProperty(laterContribution, nameof(ExactResolutionProviderEvidenceContribution.SourceAnchorId), laterProviderAnchorId);
                SetProperty(laterContribution, nameof(ExactResolutionProviderEvidenceContribution.EvidenceFingerprint), fingerprint);
                SetProperty(laterContribution, nameof(ExactResolutionProviderEvidenceContribution.ProviderOrdinal), contribution.ProviderOrdinal);
                SetProperty(laterContribution, nameof(ExactResolutionProviderEvidenceContribution.EvidenceOrdinal), contribution.EvidenceOrdinal);
                SetProperty(laterContribution, nameof(ExactResolutionProviderEvidenceContribution.CreatedAt), contribution.CreatedAt);
                context.ExactResolutionProviderEvidenceContributions.Add(laterContribution);
                laterContributions.Add(laterContribution);
            }
            await context.SaveChangesAsync();

            string planFingerprint = PreparedPass2FlowPlanner.ComputePlanFingerprint(
                decisionId,
                decisionFingerprint,
                "Complete",
                membershipFingerprint,
                laterContributions);

            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "vulgata"."FlowGroups"
                SELECT (jsonb_populate_record(NULL::"vulgata"."FlowGroups",
                    to_jsonb(source) || jsonb_build_object(
                        'Id', {groupId}, 'Pass2ExecutionId', {executionId},
                        'GroupOrder', {group.GroupOrder + 1},
                        'ExactResolutionDecisionId', {decisionId},
                        'ExactResolutionEdgeId', {edgeId},
                        'ProviderXmlDocumentVersionId', {laterProviderDocumentId},
                        'BusinessLogicDocumentVersionId', NULL,
                        'LogicalDocumentStableKey', {stableKey}, 'State', 'Complete',
                        'MembershipFingerprint', {membershipFingerprint},
                        'PlanFingerprint', {planFingerprint}))).*
                FROM "vulgata"."FlowGroups" source
                WHERE source."Id" = {group.Id};

                INSERT INTO "vulgata"."Pass2JobExecutions"
                SELECT (jsonb_populate_record(NULL::"vulgata"."Pass2JobExecutions",
                    to_jsonb(source) || jsonb_build_object(
                        'Id', {jobId}, 'Pass2ExecutionId', {executionId},
                        'FlowGroupId', {groupId}, 'State', 'Running',
                        'GroupOrder', {group.GroupOrder + 1},
                        'AttemptCount', 1, 'StateVersion', 1,
                        'LeaseToken', {leaseToken},
                        'LeaseExpiresAt', CURRENT_TIMESTAMP + INTERVAL '20 minutes',
                        'NextAttemptAt', NULL, 'FailureCategory', NULL,
                        'FailureCode', NULL, 'CommittedDocumentVersionId', NULL,
                        'CompletionFingerprint', NULL))).*
                FROM "vulgata"."Pass2JobExecutions" source
                WHERE source."Id" = {job.Id};
                """);

            Dictionary<Guid, Guid> traceIds = sourceTraces.ToDictionary(
                value => value.Id,
                _ => Guid.NewGuid());
            foreach (FlowTrace sourceTrace in sourceTraces)
            {
                context.FlowTraces.Add(new FlowTrace(
                    traceIds[sourceTrace.Id],
                    groupId,
                    sourceTrace.TraceOrder,
                    sourceTrace.CreatedAt));
            }
            foreach (FlowTraceStep sourceStep in sourceSteps)
            {
                context.FlowTraceSteps.Add(new FlowTraceStep(
                    Guid.NewGuid(),
                    traceIds[sourceStep.FlowTraceId],
                    sourceStep.StepOrder,
                    sourceStep.DocumentVersionId == group.ProviderXmlDocumentVersionId
                        ? laterProviderDocumentId
                        : sourceStep.DocumentVersionId,
                    sourceStep.DocumentRepositoryId,
                    sourceStep.DocumentScanRunId,
                    sourceStep.EdgeId == group.ExactResolutionEdgeId ? edgeId :
                    sourceStep.EdgeId == implementationEdgeId ? laterImplementationEdgeId :
                    sourceStep.EdgeId,
                    sourceStep.StepKind,
                    sourceStep.Description,
                    sourceStep.CreatedAt,
                    sourceStep.ExactResolutionDecisionId == decision.Id ? decisionId :
                        sourceStep.ExactResolutionDecisionId));
            }
            await context.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await SourceFixture.DisposeAsync();
            string databaseName = Database.DatabaseName;
            await Database.DisposeAsync();
            await using NpgsqlConnection connection = new(Administrator);
            await connection.OpenAsync();
            await using NpgsqlCommand command = new(
                "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);", connection);
            command.Parameters.AddWithValue("name", databaseName);
            Assert.False(Assert.IsType<bool>(await command.ExecuteScalarAsync()));
        }

        private static T Get<T>(object source, string name) =>
            (T)(source.GetType().GetProperty(name)?.GetValue(source)
                ?? throw new InvalidOperationException($"Seed property '{name}' is unavailable."));
    }

    private sealed class AlwaysAllowAuthorizationVerifier : IRepositoryAuthorizationVerifier
    {
        public ValueTask<bool> HasCurrentAccessAsync(
            IReadOnlyCollection<Guid> requiredRepositoryIds,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
    }

    private static Type ResolvePort() =>
        typeof(WorkerAgentJobPacket).Assembly.GetType(
            "Vulgata.Core.DomainServices.IPass2BusinessLogicDocumentCommitter")
        ?? throw new XunitException(
            "T034 RED: the public Pass 2 atomic publication/replay port is not implemented yet.");
}

/// <summary>
/// PostgreSQL qualification theories are opt-in at discovery time. This keeps
/// every generated boundary case visibly skipped when the approved admin
/// connection is absent, instead of relying on a runtime dynamic skip.
/// </summary>
internal sealed class PostgreSqlQualificationTheoryAttribute : TheoryAttribute
{
    public PostgreSqlQualificationTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN")))
        {
            Skip = "PostgreSQL qualification not run: set VULGATA_TEST_POSTGRES_ADMIN.";
        }
    }
}
