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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public void PreparedChatCoordinator_ComposesRequiredCompletionSummaryRecorder()
    {
        Type summaryType = GetRequiredType(
            typeof(ChatProviderStreamRequest).Assembly,
            "Vulgata.Core.DomainServices.ChatStreamCompletionSummary");
        ConstructorInfo constructor = Assert.Single(typeof(PreparedChatCoordinator).GetConstructors());
        ParameterInfo recorderParameter = Assert.Single(
            constructor.GetParameters(),
            parameter => parameter.ParameterType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Any(method => method.GetParameters() is [{ ParameterType: var parameterType }] &&
                    parameterType == summaryType));

        Assert.False(recorderParameter.HasDefaultValue);
        MethodInfo recordMethod = Assert.Single(
            recorderParameter.ParameterType.GetMethods(BindingFlags.Instance | BindingFlags.Public),
            method => method.GetParameters() is [{ ParameterType: var parameterType }] &&
                parameterType == summaryType);
        Assert.Contains("Completion", recordMethod.Name, StringComparison.Ordinal);
    }

    [Fact]
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
