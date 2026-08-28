using System.Reflection;
using Vulgata.Agents.ChatExecution;
using Vulgata.Core.DomainServices;

namespace Vulgata.Tests;

/// <summary>
/// T032 RED boundary for the production, content-dark observation authority.
/// ReleaseGate is deliberately not involved: a test-only semantic harness
/// cannot prove that production rejects an SDK aggregate as stream evidence.
/// </summary>
public sealed class ChatStreamObservationProvenanceTests
{
    [Fact]
    public void StreamRequest_BindsCandidateCaseAttemptAndProviderConfiguration()
    {
        Type requestType = typeof(ChatProviderStreamRequest).Assembly.GetType(
            "Vulgata.Core.DomainServices.ChatProviderStreamRequest",
            throwOnError: true,
            ignoreCase: false)!;

        string[] requiredFields =
        [
            "CandidateCommit",
            "CaseId",
            "RequestId",
            "AttemptKind",
            "ProviderIdentity",
            "ProviderConfigurationFingerprint",
            "Prompt",
            "EvidencePacketFingerprint",
            "StreamingRequestFingerprint",
            "AbsoluteDeadlineUnixTimeMilliseconds",
        ];

        string[] missingFields = requiredFields
            .Where(field => requestType.GetProperty(field) is null)
            .ToArray();

        Assert.Empty(missingFields);
    }

    [Fact]
    public void ObservationReceipt_BindsCandidateCaseProviderAndBoundedStreamLifecycle()
    {
        Type receiptType = typeof(IChatProviderStreamingTransport).Assembly.GetType(
            "Vulgata.Agents.ChatExecution.ChatStreamObservationReceipt",
            throwOnError: true,
            ignoreCase: false)!;

        string[] requiredFields =
        [
            "ObservationAuthority",
            "ProcessObservationId",
            "AdapterImplementation",
            "AdapterVersion",
            "AdapterAssemblySha256",
            "ProcessPublicKeyFingerprint",
            "ObservationSequence",
            "CandidateCommit",
            "CaseId",
            "ProviderIdentity",
            "ProviderApi",
            "ProviderConfigurationFingerprint",
            "RequestId",
            "AttemptKind",
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
            "AnswerDeltaCount",
            "MaximumAnswerDeltaByteCount",
            "AssembledAnswerByteCount",
            "TerminalEventCount",
            "InactivityLimitMilliseconds",
            "OverallDeadlineAtUtc",
            "TransportOutcome",
            "RejectionCategory",
            "ValidationOutcome",
            "AcceptedAnswerDurationMilliseconds",
        ];

        string[] missingFields = requiredFields
            .Where(field => receiptType.GetProperty(
                field,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) is null)
            .ToArray();

        Assert.Empty(missingFields);
    }

    [Fact]
    public void AggregateConsistentSdkChunkReceipt_WithoutRawAdapterProvenance_IsRejectedByProductionAuthority()
    {
        Assembly agentsAssembly = typeof(IChatProviderStreamingTransport).Assembly;
        Type? authorityType = agentsAssembly.GetType(
            "Vulgata.Agents.ChatExecution.ChatStreamObservationAuthority",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(authorityType);

        ConstructorInfo constructor = Assert.Single(
            authorityType!.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            candidate => candidate.GetParameters().Length == 0);
        object authority = constructor.Invoke(null);

        MethodInfo verifier = Assert.Single(
            authorityType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            candidate =>
                (candidate.Name is "Verify" or "VerifyReceipt" or "Validate" or "ValidateReceipt") &&
                candidate.GetParameters() is [{ ParameterType: var parameterType }] &&
                parameterType == typeof(string));

        object? result = verifier.Invoke(
            authority,
            ["{\"source\":\"sdk-aggregate\",\"chunks\":[\"answer\"]}"]);

        Assert.False(
            result switch
            {
                bool accepted => accepted,
                IReadOnlyCollection<string> errors => errors.Count == 0,
                IEnumerable<string> errors => !errors.Any(),
                _ => throw new Xunit.Sdk.XunitException(
                    $"{authorityType.FullName}.{verifier.Name} must return bool or validation errors."),
            });
    }
}
