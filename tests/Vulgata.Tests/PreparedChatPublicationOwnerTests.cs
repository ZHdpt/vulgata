using Vulgata.Tests.TestInfrastructure;
using System.Reflection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Web.Components.Pages;
using Vulgata.Web.Data;

namespace Vulgata.Tests;

/// <summary>
/// B4 RED coverage for the Web publication boundary.  The production seam is
/// intentionally discovered by its public contract name until T027 supplies
/// the implementation; no private implementation details are inspected.
/// </summary>
public sealed class PreparedChatPublicationOwnerTests
{
    private const string WebAssemblyName = "Vulgata.Web";
    private const string OwnerTypeName = "Vulgata.Web.Data.IPreparedChatPublicationOwner";
    private const string CandidateTypeName = "Vulgata.Web.Data.PreparedChatPublicationCandidate";
    private const string CommitTypeName = "Vulgata.Web.Data.AcceptedPublicationCommit";

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PublicationCandidateReady_HasNoLivePublicationEffectUntilAcceptedCommit()
    {
        Type ownerType = RequireType(OwnerTypeName);
        Type candidateType = RequireType(CandidateTypeName);
        Type commitType = RequireType(CommitTypeName);

        Assert.True(ownerType.IsInterface, "The publication boundary must be an interface.");
        Assert.NotEmpty(candidateType.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.NotEmpty(commitType.GetProperties(BindingFlags.Public | BindingFlags.Instance));
        Assert.Contains(
            commitType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name is "Answer" or "Conversation" or "CompletionSummary" or "Reveal");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("Pending")]
    [InlineData("AcceptedPublicationCommit")]
    [InlineData("TimedOutBeforePublication")]
    [InlineData("CancelledBeforePublication")]
    public void PublicationState_IsClosedAndHasExactlyOneTerminalCommit(string requiredState)
    {
        Type ownerType = RequireType(OwnerTypeName);
        Type? stateType = ownerType.Assembly.GetTypes()
            .SingleOrDefault(type => type.Name.Contains("PublicationState", StringComparison.Ordinal));

        Assert.NotNull(stateType);
        Assert.True(stateType!.IsEnum, "Publication state must be a closed enum.");
        string[] names = Enum.GetNames(stateType);
        Assert.Contains(requiredState, names);
        Assert.Equal(1, names.Count(name => name == "AcceptedPublicationCommit"));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void CommitBoundary_UsesThreeSynchronousNonThrowingOwnerOperations()
    {
        Type ownerType = RequireType(OwnerTypeName);
        MethodInfo[] methods = ownerType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

        Assert.Equal(3, methods.Length);
        Assert.Contains(methods, method =>
            method.Name == "CaptureSnapshot" &&
            method.GetParameters().Length == 0);
        Assert.Contains(methods, method =>
            method.Name == "Reset" &&
            method.GetParameters().Length == 0);
        MethodInfo commit = Assert.Single(methods.Where(method => method.GetParameters().Any(
            parameter => parameter.ParameterType.Name.Contains("PublicationCandidate", StringComparison.Ordinal))));
        Assert.All(methods, method =>
        {
            Assert.DoesNotContain("Async", method.Name, StringComparison.Ordinal);
            Assert.NotEqual(typeof(Task), method.ReturnType);
            Assert.NotEqual(typeof(ValueTask), method.ReturnType);
        });
        Assert.Contains(
            commit.GetParameters(),
            parameter => parameter.ParameterType.Name.Contains("PublicationCandidate", StringComparison.Ordinal));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PublicationOwner_ReceivesCancellationAndMonotonicClockAtTheCommitSeam()
    {
        Type ownerType = RequireType(OwnerTypeName);
        MethodInfo commit = Assert.Single(
            ownerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.GetParameters().Any(parameter =>
                    parameter.ParameterType.Name.Contains("PublicationCandidate", StringComparison.Ordinal))));
        Type implementationType = ownerType.Assembly.GetTypes()
            .Single(type => !type.IsInterface && ownerType.IsAssignableFrom(type));

        Assert.Contains(
            commit.GetParameters(),
            parameter => parameter.ParameterType == typeof(CancellationToken));
        Assert.Contains(
            implementationType.GetConstructors(BindingFlags.Public | BindingFlags.Instance),
            constructor => constructor.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(TimeProvider) ||
                parameter.ParameterType.Name.Contains("Monotonic", StringComparison.Ordinal)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PublicationCandidate_PreparesAllFallibleValuesBeforeOwnerCommit()
    {
        Type candidateType = RequireType(CandidateTypeName);
        string[] propertyNames = candidateType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToArray();

        Assert.Contains(propertyNames, name => name is "Answer" or "AcceptedAnswer");
        Assert.Contains(propertyNames, name => name.Contains("Conversation", StringComparison.Ordinal));
        Assert.Contains(propertyNames, name => name.Contains("Completion", StringComparison.Ordinal));
        Assert.Contains(propertyNames, name => name.Contains("Reveal", StringComparison.Ordinal));
        Assert.Contains(propertyNames, name => name.Contains("Deadline", StringComparison.Ordinal));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PublicationCandidate_BindsImmutableConversationGeneration()
    {
        PropertyInfo[] generationProperties = typeof(PreparedChatPublicationCandidate)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.Name.Contains("Generation", StringComparison.Ordinal))
            .ToArray();

        PropertyInfo generation = Assert.Single(generationProperties);
        Assert.False(generation.CanWrite);
        Assert.NotEqual(typeof(object), generation.PropertyType);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Coordinator_ComposesPublicationOwnerInsteadOfPublishingSuccessDirectly()
    {
        ConstructorInfo constructor = Assert.Single(typeof(PreparedChatCoordinator).GetConstructors());
        Assert.Contains(
            constructor.GetParameters(),
            parameter => parameter.ParameterType.FullName == OwnerTypeName);
        Assert.Single(
            constructor.GetParameters(),
            parameter => parameter.ParameterType == typeof(IChatStreamCompletionSummaryRecorder));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PublicationOwner_RejectsCancellationOrDeadlineBeforeCommitWithoutPartialPublication()
    {
        Type commitType = RequireType(CommitTypeName);
        string[] properties = commitType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToArray();

        Assert.Contains(properties, name => name.Contains("State", StringComparison.Ordinal));
        Assert.Contains(properties, name => name.Contains("Published", StringComparison.Ordinal));
        Assert.Contains(properties, name => name.Contains("Answer", StringComparison.Ordinal));
        Assert.Contains(properties, name => name.Contains("Conversation", StringComparison.Ordinal));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Commit_CancellationBeforeSoleSample_ReturnsClosedCancelledWithoutPublication()
    {
        var clock = new PublicationClock(150);
        var owner = new PreparedChatPublicationOwner(clock);
        var candidate = CreateCandidate(owner, startTimestamp: 100, deadlineTimestamp: 200);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        AcceptedPublicationCommit commit = owner
            .Commit(candidate, cancellation.Token);

        Assert.Equal(PreparedChatPublicationState.CancelledBeforePublication, commit.State);
        Assert.False(commit.Published);
        Assert.Null(commit.Answer);
        Assert.Null(commit.Conversation);
        Assert.Null(commit.CompletionSummary);
        Assert.Equal(0, clock.SampleCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Commit_CandidateCapturedBeforeReset_CannotPublishOrReactivate()
    {
        var clock = new PublicationClock(150);
        var owner = new PreparedChatPublicationOwner(clock);
        var candidate = CreateCandidate(
            owner,
            startTimestamp: 100,
            deadlineTimestamp: 200);

        owner.Reset();

        AcceptedPublicationCommit firstCommit = owner.Commit(candidate, CancellationToken.None);
        AcceptedPublicationCommit repeatedCommit = owner.Commit(candidate, CancellationToken.None);

        Assert.False(firstCommit.Published);
        Assert.Equal(PreparedChatPublicationState.CancelledBeforePublication, firstCommit.State);
        Assert.Null(firstCommit.Answer);
        Assert.Null(firstCommit.Conversation);
        Assert.Null(firstCommit.CompletionSummary);
        Assert.Same(firstCommit, repeatedCommit);
        Assert.Equal(PreparedChatPublicationState.CancelledBeforePublication, candidate.State);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Commit_PreResetCandidate_FirstRepeatedAndConcurrentCallsRemainClosedAndNonCurrentAsync()
    {
        var clock = new PublicationClock(150);
        var owner = new PreparedChatPublicationOwner(clock);
        var candidate = CreateCandidate(
            owner,
            startTimestamp: 100,
            deadlineTimestamp: 200);

        owner.Reset();

        AcceptedPublicationCommit[] commits = await Task.WhenAll(
            Enumerable.Range(0, 16)
                .Select(_ => Task.Run(() => owner.Commit(candidate, CancellationToken.None))));

        AcceptedPublicationCommit first = commits[0];
        Assert.False(first.Published);
        Assert.Equal(PreparedChatPublicationState.CancelledBeforePublication, first.State);
        Assert.All(commits, commit => Assert.Same(first, commit));
        Assert.Null(first.Answer);
        Assert.Null(first.Conversation);
        Assert.Null(first.CompletionSummary);
        Assert.Null(owner.CaptureSnapshot().CurrentCommit);
        Assert.Equal(PreparedChatPublicationState.CancelledBeforePublication, candidate.State);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(200)]
    [InlineData(201)]
    public void Commit_DeadlineAtOrAfterSoleSample_ReturnsClosedTimedOutWithoutPublication(
        long sampledTimestamp)
    {
        var clock = new PublicationClock(sampledTimestamp);
        var owner = new PreparedChatPublicationOwner(clock);
        var candidate = CreateCandidate(owner, startTimestamp: 100, deadlineTimestamp: 200);

        AcceptedPublicationCommit commit = owner
            .Commit(candidate, CancellationToken.None);

        Assert.Equal(PreparedChatPublicationState.TimedOutBeforePublication, commit.State);
        Assert.False(commit.Published);
        Assert.Null(commit.Answer);
        Assert.Null(commit.Conversation);
        Assert.Null(commit.CompletionSummary);
        Assert.Equal(1, clock.SampleCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Commit_StrictlyBeforeDeadline_RemainsAcceptedAfterSchedulerAdvance()
    {
        var clock = new PublicationClock(150);
        var owner = new PreparedChatPublicationOwner(clock);
        var candidate = CreateCandidate(owner, startTimestamp: 100, deadlineTimestamp: 200);

        AcceptedPublicationCommit commit = owner.Commit(candidate, CancellationToken.None);
        clock.Timestamp = 250;

        Assert.Equal(PreparedChatPublicationState.AcceptedPublicationCommit, commit.State);
        Assert.True(commit.Published);
        Assert.Same(candidate.AcceptedAnswer, commit.Answer);
        Assert.Same(candidate.ProposedConversation, commit.Conversation);
        Assert.NotNull(commit.CompletionSummary);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Commit_GetElapsedTimeFailure_DoesNotStrandCandidatePending()
    {
        var clock = new PublicationClock(150) { ThrowOnElapsedTime = true };
        var owner = new PreparedChatPublicationOwner(clock);
        var candidate = CreateCandidate(owner, startTimestamp: 100, deadlineTimestamp: 200);

        AcceptedPublicationCommit? commit = null;
        Exception? exception = Record.Exception(() =>
            commit = owner.Commit(candidate, CancellationToken.None));

        Assert.Null(exception);
        AcceptedPublicationCommit acceptedCommit = Assert.IsType<AcceptedPublicationCommit>(commit);
        Assert.NotEqual(PreparedChatPublicationState.Pending, acceptedCommit.State);
        Assert.Equal(PreparedChatPublicationState.AcceptedPublicationCommit, acceptedCommit.State);
        Assert.True(acceptedCommit.Published);
        Assert.NotNull(acceptedCommit.CompletionSummary);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Commit_ConcurrentRepeatedCalls_ReturnExactlyOneTerminalAcceptedCommitAsync()
    {
        var clock = new PublicationClock(150);
        var owner = new PreparedChatPublicationOwner(clock);
        var candidate = CreateCandidate(owner, startTimestamp: 100, deadlineTimestamp: 200);

        AcceptedPublicationCommit[] commits = await Task.WhenAll(
            Enumerable.Range(0, 16)
                .Select(_ => Task.Run(() => owner.Commit(candidate, CancellationToken.None))));

        AcceptedPublicationCommit first = commits[0];
        Assert.NotNull(first);
        Assert.True(first.Published);
        Assert.All(commits, commit => Assert.Same(first, commit));
        Assert.Equal(PreparedChatPublicationState.AcceptedPublicationCommit, candidate.State);
        Assert.Equal(1, clock.SampleCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Commit_AcceptedComposite_ContainsAnswerConversationSummaryAndRevealAtomically()
    {
        var clock = new PublicationClock(150);
        var owner = new PreparedChatPublicationOwner(clock);
        var candidate = CreateCandidate(owner, startTimestamp: 100, deadlineTimestamp: 200);

        AcceptedPublicationCommit commit = owner
            .Commit(candidate, CancellationToken.None);

        Assert.True(commit.Published);
        Assert.Same(candidate.AcceptedAnswer, commit.Answer);
        Assert.Same(candidate.ProposedConversation, commit.Conversation);
        Assert.Same(candidate.Reveal, commit.Reveal);
        Assert.NotNull(commit.CompletionSummary);
        Assert.Equal(
            candidate.CompletionSummary.CompletionSummaryId,
            commit.CompletionSummary!.CompletionSummaryId);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void CompletionData_IsExposedOnlyThroughPublishedComposite()
    {
        Type candidateType = RequireType(CandidateTypeName);
        Assert.Null(candidateType.GetProperty(
            "CommittedCompletionSummary",
            BindingFlags.Public | BindingFlags.Instance));

        var cancelledClock = new PublicationClock(150);
        var cancelledOwner = new PreparedChatPublicationOwner(cancelledClock);
        var cancelledCandidate = CreateCandidate(
            cancelledOwner,
            startTimestamp: 100,
            deadlineTimestamp: 200);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        AcceptedPublicationCommit cancelled = cancelledOwner
            .Commit(cancelledCandidate, cancellation.Token);
        Assert.False(cancelled.Published);
        Assert.Null(cancelled.CompletionSummary);

        var acceptedClock = new PublicationClock(150);
        var acceptedOwner = new PreparedChatPublicationOwner(acceptedClock);
        var acceptedCandidate = CreateCandidate(
            acceptedOwner,
            startTimestamp: 100,
            deadlineTimestamp: 200);
        AcceptedPublicationCommit accepted = acceptedOwner
            .Commit(acceptedCandidate, CancellationToken.None);

        Assert.True(accepted.Published);
        Assert.NotNull(accepted.CompletionSummary);
        Assert.Equal(
            acceptedCandidate.CompletionSummary.CompletionSummaryId,
            accepted.CompletionSummary!.CompletionSummaryId);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PublicationCandidate_RevealDirectiveBindsUnicodeScheduleCostAndSafetyMargin()
    {
        Type directiveType = typeof(PreparedChatRevealDirective);
        PropertyInfo? scheduleCost = directiveType.GetProperty("ScheduleCostMilliseconds");
        PropertyInfo? safetyMargin = directiveType.GetProperty("SafetyMarginMilliseconds");

        Assert.NotNull(scheduleCost);
        Assert.NotNull(safetyMargin);
        Assert.False(scheduleCost!.CanWrite);
        Assert.False(safetyMargin!.CanWrite);

        PreparedChatRevealDirective directive = PreparedChatRevealDirective.ProgressiveAccepted(29_000);
        object? cost = scheduleCost.GetValue(directive);
        object? margin = safetyMargin.GetValue(directive);
        Assert.True(Convert.ToInt64(cost) >= 0);
        Assert.True(Convert.ToInt64(margin) > 0);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Commit_AcceptedPublication_PreallocatesStableCompletionSummaryAuthority()
    {
        TimeProvider timeProvider = TimeProvider.System;
        var owner = new PreparedChatPublicationOwner(timeProvider);
        long startTimestamp = timeProvider.GetTimestamp();
        var candidate = new PreparedChatPublicationCandidate(
            GroundedAnswer.Create(
                ChatMode.Business,
                [MaterialClaim.Create("claim-1", 1, "已验证回答", ["citation-1"])],
                [DocumentCitation.Create(
                    "citation-1",
                    ["claim-1"],
                    "已验证文档",
                    DocumentType.BusinessLogic,
                    "mbs_account",
                    "src/PaymentFlow.cs",
                    20,
                    24)],
                DateTimeOffset.UtcNow),
            ChatConversationState.Empty(ChatConversationScope.Create(
                ChatMode.Business,
                Guid.NewGuid(),
                [Guid.NewGuid(), Guid.NewGuid()])),
            new PreparedChatCompletionSummaryTemplate(
                "summary-1",
                "request-1",
                [],
                false,
                ["accepted"],
                "accepted"),
            PreparedChatRevealDirective.ImmediateAccepted(
                DateTimeOffset.UtcNow.AddSeconds(29).ToUnixTimeMilliseconds()),
            startTimestamp,
            checked(startTimestamp + (29 * timeProvider.TimestampFrequency)),
            CancellationToken.None,
            new string('a', 64),
            new string('b', 64),
            owner.CaptureSnapshot());

        AcceptedPublicationCommit commit = owner
            .Commit(candidate, CancellationToken.None);

        object? first = commit.CompletionSummary;
        object? second = commit.CompletionSummary;

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Same(first, second);
        Type summaryType = first!.GetType();
        string[] requiredProperties =
        [
            "CompletionSummaryId",
            "RequestId",
            "RuntimeObservationIds",
            "SyntaxRepairUsed",
            "AttemptValidationOutcomes",
            "FinalValidationOutcome",
            "TotalAcceptedAnswerDurationMilliseconds",
        ];
        Assert.All(
            requiredProperties,
            propertyName => Assert.NotNull(summaryType.GetProperty(propertyName)));
        string[] prohibitedProperties =
        [
            "Prompt",
            "ResponseBody",
            "Answer",
            "EvidencePacket",
            "CandidateCommit",
            "Signature",
            "ExportPath",
            "RunId",
            "CaseId",
            "ReleaseEligible",
        ];
        Assert.DoesNotContain(
            summaryType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => prohibitedProperties.Contains(property.Name, StringComparer.Ordinal));
        object? duration = summaryType
            .GetProperty("TotalAcceptedAnswerDurationMilliseconds")!
            .GetValue(first);
        Assert.True(Convert.ToInt64(duration) >= 0);
    }

    private static PreparedChatPublicationCandidate CreateCandidate(
        IPreparedChatPublicationOwner owner,
        long startTimestamp,
        long deadlineTimestamp,
        CancellationToken deadlineCancellationToken = default)
    {
        GroundedAnswer answer = GroundedAnswer.Create(
            ChatMode.Business,
            [MaterialClaim.Create("claim-1", 1, "已验证回答", ["citation-1"])],
            [DocumentCitation.Create(
                "citation-1",
                ["claim-1"],
                "已验证文档",
                DocumentType.BusinessLogic,
                "mbs_account",
                "src/PaymentFlow.cs",
                20,
                24)],
            DateTimeOffset.UnixEpoch);
        ChatConversationState conversation = ChatConversationState.Empty(
            ChatConversationScope.Create(ChatMode.Business, Guid.NewGuid(), [Guid.NewGuid(), Guid.NewGuid()]));
        PreparedChatCompletionSummaryTemplate completionSummary = new(
            "summary-1",
            "request-1",
            [],
            false,
            ["accepted"],
            "accepted");
        return new PreparedChatPublicationCandidate(
            answer,
            conversation,
            completionSummary,
            PreparedChatRevealDirective.ProgressiveAccepted(29_000),
            startTimestamp,
            deadlineTimestamp,
            deadlineCancellationToken,
            new string('a', 64),
            new string('b', 64),
            owner.CaptureSnapshot());
    }

    private sealed class PublicationClock(long timestamp) : TimeProvider
    {
        public long Timestamp { get; set; } = timestamp;

        public int SampleCount { get; private set; }

        public bool ThrowOnElapsedTime { get; set; }

        public override long TimestampFrequency => ThrowOnElapsedTime
            ? throw new InvalidOperationException("elapsed-time seam must not be used after sampling")
            : TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            SampleCount++;
            return Timestamp;
        }

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;

    }

    private static Type RequireType(string fullName)
    {
        Type? type = Type.GetType($"{fullName}, {WebAssemblyName}");
        Assert.NotNull(type);
        return type!;
    }
}
