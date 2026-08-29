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

    [Fact]
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

    [Theory]
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

    [Fact]
    public void CommitBoundary_UsesOneSynchronousNonThrowingOwnerOperation()
    {
        Type ownerType = RequireType(OwnerTypeName);
        MethodInfo[] methods = ownerType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

        Assert.Single(methods);
        MethodInfo commit = methods[0];
        Assert.DoesNotContain("Async", commit.Name, StringComparison.Ordinal);
        Assert.NotEqual(typeof(Task), commit.ReturnType);
        Assert.NotEqual(typeof(ValueTask), commit.ReturnType);
        Assert.Contains(
            commit.GetParameters(),
            parameter => parameter.ParameterType.Name.Contains("PublicationCandidate", StringComparison.Ordinal));
    }

    [Fact]
    public void PublicationOwner_ReceivesCancellationAndMonotonicClockAtTheCommitSeam()
    {
        Type ownerType = RequireType(OwnerTypeName);
        MethodInfo commit = Assert.Single(ownerType.GetMethods(BindingFlags.Public | BindingFlags.Instance));
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

    [Fact]
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

    [Fact]
    public void Coordinator_ComposesPublicationOwnerInsteadOfPublishingSuccessDirectly()
    {
        ConstructorInfo constructor = Assert.Single(typeof(PreparedChatCoordinator).GetConstructors());
        Assert.Contains(
            constructor.GetParameters(),
            parameter => parameter.ParameterType.FullName == OwnerTypeName);
        Assert.DoesNotContain(
            constructor.GetParameters(),
            parameter => parameter.ParameterType == typeof(IChatStreamCompletionSummaryRecorder));
    }

    [Fact]
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

    [Fact]
    public void Commit_AcceptedPublication_PreallocatesStableCompletionSummaryAuthority()
    {
        TimeProvider timeProvider = TimeProvider.System;
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
            new string('b', 64));

        AcceptedPublicationCommit commit = new PreparedChatPublicationOwner(timeProvider)
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

    private static Type RequireType(string fullName)
    {
        Type? type = Type.GetType($"{fullName}, {WebAssemblyName}");
        Assert.NotNull(type);
        return type!;
    }
}
