using Vulgata.Tests.TestInfrastructure;
using System.Reflection;
using Vulgata.Agents.ChatExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Web.Data;

namespace Vulgata.Tests;

/// <summary>
/// T066 public composition boundary for release-neutral Track B stream telemetry.
/// Signed candidate attestation remains exclusively absent until Track C.
/// </summary>
public sealed class ChatStreamObservationProvenanceTests
{
    private static readonly BindingFlags _instanceProperties =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void StreamRequest_TrackBMetadata_HasNoReleaseOrPostTransportValidationCoupling()
    {
        Type requestType = typeof(ChatProviderStreamRequest);
        string[] requiredProperties =
        [
            "RequestId",
            "AttemptKind",
            "ProviderIdentity",
            "ProviderConfigurationFingerprint",
            "Prompt",
            "EvidencePacketFingerprint",
            "StreamingRequestFingerprint",
            "AbsoluteDeadlineUnixTimeMilliseconds",
        ];
        string[] prohibitedProperties =
        [
            "CandidateCommit",
            "ReleaseRunId",
            "RunId",
            "CaseId",
            "ProcessObservationId",
            "ProcessPublicKeyFingerprint",
            "Signature",
            "AttestationSignature",
            "ArtifactPath",
            "ExportPath",
            "ValidationOutcome",
            "AcceptedAnswerDurationMilliseconds",
            "ReleaseEligible",
            "ReleaseStatus",
        ];

        AssertPropertiesPresent(requestType, requiredProperties);
        AssertPropertiesAbsent(requestType, prohibitedProperties);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void RuntimeObservation_TrackBTransportFacts_AreImmutableContentDarkAndReleaseNeutral()
    {
        Type observationType = GetRequiredType(
            typeof(IChatProviderStreamingTransport).Assembly,
            "Vulgata.Agents.ChatExecution.ChatStreamRuntimeObservation");
        string[] requiredProperties =
        [
            "RuntimeObservationId",
            "ObservationSequence",
            "RequestId",
            "AttemptKind",
            "ProviderIdentity",
            "ProviderApi",
            "ProviderConfigurationFingerprint",
            "CanonicalStreamingRequestFingerprint",
            "RequestStreamMode",
            "ResponseHeaderDisposition",
            "ResponseStatusCode",
            "ResponseMediaType",
            "NegotiatedProtocol",
            "ResponseHeadersAtUtc",
            "FirstPhysicalReadDisposition",
            "FirstPhysicalReadAtUtc",
            "PhysicalEofAtUtc",
            "PhysicalEofDisposition",
            "StreamingTransport",
            "StreamStartedAtUtc",
            "FirstDataAtUtc",
            "TerminalAtUtc",
            "TimeToFirstDataMilliseconds",
            "RawReadCount",
            "MaximumRawReadByteCount",
            "RawBodyByteCount",
            "MaximumSseLineByteCount",
            "MaximumSseEventByteCount",
            "ProviderChunkCount",
            "MaximumReceivedProviderChunkByteCount",
            "AssembledReceivedProviderResponseByteCount",
            "AnswerDeltaCount",
            "MaximumAnswerDeltaByteCount",
            "AssembledAnswerByteCount",
            "TerminalEventCount",
            "InactivityLimitMilliseconds",
            "OverallDeadlineAtUtc",
            "TransportOutcome",
            "RejectionCategory",
        ];
        string[] prohibitedProperties =
        [
            "ObservationAuthority",
            "ProcessObservationId",
            "AdapterImplementation",
            "AdapterVersion",
            "AdapterAssemblySha256",
            "ProcessPublicKeyFingerprint",
            "CandidateCommit",
            "ReleaseRunId",
            "RunId",
            "CaseId",
            "Signature",
            "AttestationSignature",
            "ArtifactPath",
            "ExportPath",
            "Prompt",
            "ResponseBody",
            "Answer",
            "PartialAnswer",
            "RawSelectionJson",
            "EvidencePacket",
            "EvidencePacketFingerprint",
            "Claim",
            "Citation",
            "Handle",
            "Relationship",
            "SourceAnchor",
            "Secret",
            "Connection",
            "HostPath",
            "ValidationOutcome",
            "AcceptedAnswerDurationMilliseconds",
            "ReleaseEligible",
            "ReleaseStatus",
        ];

        AssertPropertiesPresent(observationType, requiredProperties);
        AssertPropertiesAbsent(observationType, prohibitedProperties);
        Assert.All(
            observationType.GetProperties(_instanceProperties),
            property => Assert.True(
                HasNoPublicMutationAfterConstruction(property),
                $"{observationType.Name}.{property.Name} must be immutable after construction."));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void CompletionSummary_CoordinatorResult_IsImmutableContentDarkAndSeparateFromTransportFacts()
    {
        Type summaryType = GetRequiredType(
            typeof(ChatProviderStreamRequest).Assembly,
            "Vulgata.Core.DomainServices.ChatStreamCompletionSummary");
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
        string[] prohibitedProperties =
        [
            "CandidateCommit",
            "ReleaseRunId",
            "RunId",
            "CaseId",
            "ProcessObservationId",
            "ProcessPublicKeyFingerprint",
            "Signature",
            "AttestationSignature",
            "ArtifactPath",
            "ExportPath",
            "Prompt",
            "ResponseBody",
            "Answer",
            "PartialAnswer",
            "RawSelectionJson",
            "EvidencePacket",
            "EvidencePacketFingerprint",
            "Claim",
            "Citation",
            "Handle",
            "Relationship",
            "SourceAnchor",
            "ProviderIdentity",
            "ProviderConfigurationFingerprint",
            "RawReadCount",
            "RawBodyByteCount",
            "TransportOutcome",
            "RejectionCategory",
            "ReleaseEligible",
            "ReleaseStatus",
        ];

        AssertPropertiesPresent(summaryType, requiredProperties);
        AssertPropertiesAbsent(summaryType, prohibitedProperties);
        Assert.All(
            summaryType.GetProperties(_instanceProperties),
            property => Assert.True(
                HasNoPublicMutationAfterConstruction(property),
                $"{summaryType.Name}.{property.Name} must be immutable after construction."));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreparedChatCoordinator_ComposesPublicationOwnerForCompletionAuthority()
    {
        ConstructorInfo constructor = Assert.Single(typeof(PreparedChatCoordinator).GetConstructors());
        ParameterInfo ownerParameter = Assert.Single(
            constructor.GetParameters(),
            parameter => parameter.ParameterType == typeof(IPreparedChatPublicationOwner));
        ParameterInfo recorderParameter = Assert.Single(
            constructor.GetParameters(),
            parameter => parameter.ParameterType == typeof(IChatStreamCompletionSummaryRecorder));

        Assert.False(
            ownerParameter.IsOptional || ownerParameter.HasDefaultValue,
            "The publication owner must be one required typed dependency, not a default/fallback parameter.");
        Assert.False(
            recorderParameter.IsOptional || recorderParameter.HasDefaultValue,
            "The completion recorder must be one required typed dependency, not a default/fallback parameter.");
        Assert.Equal(
            NullabilityState.NotNull,
            new NullabilityInfoContext().Create(ownerParameter).ReadState);
        Assert.Equal(
            NullabilityState.NotNull,
            new NullabilityInfoContext().Create(recorderParameter).ReadState);
        Assert.DoesNotContain(constructor.GetParameters(), parameter => parameter.ParameterType == typeof(object));
        Assert.Equal(
            typeof(AcceptedPublicationCommit),
            typeof(PreparedChatResult).GetProperty(nameof(PreparedChatResult.PublicationCommit))!.PropertyType);
        Assert.Contains(
            typeof(AcceptedPublicationCommit).GetProperties(BindingFlags.Instance | BindingFlags.Public),
            property => property.Name == nameof(AcceptedPublicationCommit.CompletionSummary));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void TrackBComposition_DoesNotContainTrackCCandidateAttestation()
    {
        Assembly agentsAssembly = typeof(IChatProviderStreamingTransport).Assembly;

        Assert.Null(agentsAssembly.GetType(
            "Vulgata.Agents.ChatExecution.Story125StreamAttestationSink",
            throwOnError: false,
            ignoreCase: false));
        Assert.Null(agentsAssembly.GetType(
            "Vulgata.Agents.ChatExecution.Story125StreamAttemptAttestation",
            throwOnError: false,
            ignoreCase: false));
        Assert.Null(agentsAssembly.GetType(
            "Vulgata.Agents.ChatExecution.ChatStreamObservationReceipt",
            throwOnError: false,
            ignoreCase: false));
        Assert.Null(agentsAssembly.GetType(
            "Vulgata.Agents.ChatExecution.ChatStreamObservationAuthority",
            throwOnError: false,
            ignoreCase: false));
    }

    private static Type GetRequiredType(Assembly assembly, string fullName)
    {
        Type? type = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
        Assert.NotNull(type);
        return type;
    }

    private static void AssertPropertiesPresent(Type type, IReadOnlyCollection<string> names)
    {
        string[] missing = names
            .Where(name => type.GetProperty(name, _instanceProperties) is null)
            .ToArray();
        Assert.True(
            missing.Length == 0,
            $"{type.FullName} is missing required properties: {string.Join(", ", missing)}");
    }

    private static void AssertPropertiesAbsent(Type type, IReadOnlyCollection<string> names)
    {
        string[] leaked = names
            .Where(name => type.GetProperty(name, _instanceProperties) is not null)
            .ToArray();
        Assert.True(
            leaked.Length == 0,
            $"{type.FullName} leaks prohibited properties: {string.Join(", ", leaked)}");
    }

    private static bool HasNoPublicMutationAfterConstruction(PropertyInfo property)
    {
        MethodInfo? setter = property.SetMethod;
        return setter is null ||
            !setter.IsPublic ||
            setter.ReturnParameter.GetRequiredCustomModifiers().Any(
                modifier => modifier.FullName == "System.Runtime.CompilerServices.IsExternalInit");
    }
}
