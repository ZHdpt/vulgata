using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vulgata.Core.DomainServices;

namespace Vulgata.Agents.WorkerExecution;

/// <summary>
/// Concrete bounded dispatcher for one server-prepared Pass 2 Flow Group.
/// </summary>
public sealed class PreparedPass2WorkerDispatcher : IPreparedPass2WorkerDispatcher
{
    private const string PromptResourceName =
        "Vulgata.Agents.Prompts.pass2-business-logic-worker.v1.md";
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);
    private readonly PreparedPass2JobExecutor _executor;
    private readonly IPass2BusinessLogicDocumentCommitter _committer;
    private readonly PreparedPass2WorkerContractAuthority? _workerContract;

    public PreparedPass2WorkerDispatcher(
        PreparedPass2JobExecutor executor,
        IPass2BusinessLogicDocumentCommitter committer)
        : this(executor, committer, null, true)
    {
    }

    public PreparedPass2WorkerDispatcher(
        PreparedPass2JobExecutor executor,
        IPass2BusinessLogicDocumentCommitter committer,
        IPreparedPass2WorkerContractAuthority workerContractAuthority)
        : this(executor, committer, workerContractAuthority, false)
    {
    }

    private PreparedPass2WorkerDispatcher(
        PreparedPass2JobExecutor executor,
        IPass2BusinessLogicDocumentCommitter committer,
        IPreparedPass2WorkerContractAuthority? workerContractAuthority,
        bool compatibilityAdapter)
    {
        _ = compatibilityAdapter;
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _committer = committer ?? throw new ArgumentNullException(nameof(committer));
        _workerContract = workerContractAuthority?.GetCurrent();
    }

    public async Task<PreparedPass2DispatchResult> DispatchAsync(
        PreparedPass2DispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (_workerContract is null || !IsAuthoritative(request) ||
            request.Flow is null || request.RuntimeBinding is null ||
            !request.RuntimeBinding.IsCanonical() ||
            !request.Flow.IsExactMatch(request.RuntimeBinding.Flow))
        {
            return new(false, "invalid-binding");
        }

        PreparedPass2WorkerPacket packet;
        try
        {
            packet = Project(request);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidDataException or OverflowException)
        {
            return new(false, "invalid-binding");
        }

        PreparedPass2RawExecutionResult result = await _executor.ExecuteAsync(
            packet,
            LoadPrompt(),
            (candidate, token) => CommitCandidateAsync(request, candidate, token),
            cancellationToken);
        return new(
            result.Accepted,
            result.Accepted ? null : Sanitize(result.ReasonCode));
    }

    private async Task<PreparedPass2CandidateDecision> CommitCandidateAsync(
        PreparedPass2DispatchRequest dispatch,
        ReadOnlyMemory<byte> candidateUtf8,
        CancellationToken cancellationToken)
    {
        try
        {
            using JsonDocument candidate = JsonDocument.Parse(candidateUtf8, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64,
            });
            Pass2BusinessLogicCommitResult result = await _committer.CommitAsync(
                new Pass2BusinessLogicCommitRequest(
                    dispatch.Pass2JobExecutionId,
                    dispatch.LeaseToken,
                    dispatch.AttemptOrdinal,
                    candidate.RootElement,
                    dispatch.InitialEvidence,
                    dispatch.PromptIdentity)
                {
                    PromptFingerprint = dispatch.PromptFingerprint,
                    SubmissionSchemaIdentity = dispatch.SubmissionSchemaIdentity,
                    SubmissionSchemaFingerprint = dispatch.SubmissionSchemaFingerprint,
                    ToolDeclarationFingerprint = dispatch.ToolDeclarationFingerprint,
                },
                cancellationToken);
            return result.Status is Pass2BusinessLogicCommitStatus.Committed or
                Pass2BusinessLogicCommitStatus.Replayed
                ? new PreparedPass2CandidateDecision(
                    PreparedPass2SubmissionDisposition.Accepted,
                    JsonSerializer.SerializeToUtf8Bytes(candidate.RootElement))
                : PreparedPass2CandidateDecision.Reject(
                    result.Status == Pass2BusinessLogicCommitStatus.Conflict
                        ? PreparedPass2SubmissionDisposition.InvalidBinding
                        : PreparedPass2SubmissionDisposition.InvalidSemantics);
        }
        catch (JsonException)
        {
            return PreparedPass2CandidateDecision.Reject(
                PreparedPass2SubmissionDisposition.InvalidShape);
        }
    }

    private static PreparedPass2WorkerPacket Project(
        PreparedPass2DispatchRequest request)
    {
        PreparedPass2Flow flow = request.Flow;
        PreparedPass2RuntimeBinding binding = request.RuntimeBinding;
        var handles = new Dictionary<Guid, string>();
        foreach (Pass2BusinessLogicEvidenceBinding evidence in request.InitialEvidence)
        {
            if (evidence.SectionId is null && evidence.SourceAnchorId is null &&
                evidence.EdgeId is null)
            {
                handles.TryAdd(evidence.DocumentVersionId, evidence.OpaqueHandle);
            }

            if (evidence.SectionId is { } sectionId)
            {
                handles.TryAdd(sectionId, evidence.OpaqueHandle);
            }

            if (evidence.SourceAnchorId is { } anchorId)
            {
                handles.TryAdd(anchorId, evidence.OpaqueHandle);
            }

            if (evidence.EdgeId is { } edgeId)
            {
                handles.TryAdd(edgeId, evidence.OpaqueHandle);
            }
        }

        if (binding.Members.Any(member =>
                !handles.ContainsKey(member.DocumentVersionId) ||
                member.SourceAnchorIds.Any(anchorId => !handles.ContainsKey(anchorId))) ||
            !handles.ContainsKey(binding.RelationshipId))
        {
            throw new InvalidDataException(
                "The server-issued initial evidence handles are incomplete.");
        }

        byte[] providerUtf8 = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaIdentity = "vulgata.pass2-worker-packet/2.0",
            jobId = request.Pass2JobExecutionId,
            leaseExpiresAt = request.LeaseExpiresAt,
            attemptOrdinal = request.AttemptOrdinal,
            pass2ExecutionId = request.Pass2ExecutionId,
            flowGroupId = request.FlowGroupId,
            exactResolutionDecisionId = request.ExactResolutionDecisionId,
            exactResolutionEdgeId = request.ExactResolutionEdgeId,
            exactResolutionDecisionFingerprint = request.ExactResolutionDecisionFingerprint,
            providerEvidenceFingerprint = request.ProviderEvidenceFingerprint,
            protocol = request.Protocol,
            normalizedEndpointIdentifierFingerprint =
                request.NormalizedEndpointIdentifierFingerprint,
            consumerEntryStableKey = request.ConsumerEntryStableKey,
            logicalDocumentStableKey = request.LogicalDocumentStableKey,
            planFingerprint = request.PlanFingerprint,
            membershipFingerprint = request.MembershipFingerprint,
            promptIdentity = request.PromptIdentity,
            promptFingerprint = request.PromptFingerprint,
            submissionSchemaIdentity = request.SubmissionSchemaIdentity,
            submissionSchemaFingerprint = request.SubmissionSchemaFingerprint,
            toolDeclarationIdentity = request.ToolDeclarationIdentity,
            toolDeclarationFingerprint = request.ToolDeclarationFingerprint,
            flow = new
            {
                flow.SchemaIdentity,
                flow.ManifestId,
                flow.FlowId,
                flow.SystemId,
                flow.RepositoryId,
                members = binding.Members.Select(member => new
                {
                    member.Order,
                    member.RepositoryId,
                    documentHandle = handles[member.DocumentVersionId],
                    sourceAnchorHandles = member.SourceAnchorIds.Select(id => handles[id]),
                }),
                relationshipHandle = handles[binding.RelationshipId],
                traceEvidence = flow.TraceEvidence.Select(evidence => new
                {
                    evidence.Order,
                    evidence.Label,
                    evidence.Locator,
                }),
            },
            remainingRetrievalBudget = new
            {
                calls = Pass2RetrievalContracts.MaxCalls,
                results = Pass2RetrievalContracts.MaxResults,
                additionalDocuments = Pass2RetrievalContracts.MaxAdditionalDocuments,
                sectionsPerDocument = Pass2RetrievalContracts.MaxSectionsPerDocument,
                returnedUtf8Bytes = Pass2RetrievalContracts.MaxReturnedUtf8Bytes,
            },
        });
        if (providerUtf8.Length > PreparedPass2WireBudget.MaximumInitialRequestUtf8Bytes)
        {
            throw new InvalidDataException("The prepared Pass 2 packet exceeds its bound.");
        }

        return new(
            request.Pass2JobExecutionId,
            flow,
            binding,
            handles,
            providerUtf8)
        {
            LeaseToken = request.LeaseToken,
            LeaseExpiresAt = request.LeaseExpiresAt,
            AttemptOrdinal = request.AttemptOrdinal,
            Pass2ExecutionId = request.Pass2ExecutionId,
            FlowGroupId = request.FlowGroupId,
            ExactResolutionDecisionId = request.ExactResolutionDecisionId,
            LogicalDocumentStableKey = request.LogicalDocumentStableKey,
            PlanFingerprint = request.PlanFingerprint,
            MembershipFingerprint = request.MembershipFingerprint,
            PromptIdentity = request.PromptIdentity,
            PromptFingerprint = request.PromptFingerprint,
            SubmissionSchemaIdentity = request.SubmissionSchemaIdentity,
            SubmissionSchemaFingerprint = request.SubmissionSchemaFingerprint,
            ToolDeclarationIdentity = request.ToolDeclarationIdentity,
            ToolDeclarationFingerprint = request.ToolDeclarationFingerprint,
        };
    }

    private bool IsAuthoritative(PreparedPass2DispatchRequest request) =>
        request.Pass2JobExecutionId != Guid.Empty &&
        request.LeaseToken != Guid.Empty &&
        request.LeaseExpiresAt > DateTimeOffset.UtcNow &&
        request.AttemptOrdinal is >= 1 and <= 2 &&
        request.Pass2ExecutionId != Guid.Empty &&
        request.FlowGroupId != Guid.Empty &&
        request.ExactResolutionDecisionId != Guid.Empty &&
        request.ExactResolutionEdgeId != Guid.Empty &&
        IsLowerFingerprint(request.ExactResolutionDecisionFingerprint) &&
        IsLowerFingerprint(request.ProviderEvidenceFingerprint) &&
        request.Protocol is "CustomRpc" or "Http" &&
        IsLowerFingerprint(request.NormalizedEndpointIdentifierFingerprint) &&
        IsLowerFingerprint(request.ConsumerEntryStableKey) &&
        request.LogicalDocumentStableKey.StartsWith(
            "business-flow/v1/",
            StringComparison.Ordinal) &&
        IsLowerFingerprint(request.LogicalDocumentStableKey["business-flow/v1/".Length..]) &&
        IsLowerFingerprint(request.PlanFingerprint) &&
        IsLowerFingerprint(request.MembershipFingerprint) &&
        StringComparer.Ordinal.Equals(
            request.PromptIdentity, _workerContract!.PromptIdentity) &&
        StringComparer.Ordinal.Equals(
            request.PromptFingerprint, _workerContract.PromptFingerprint) &&
        StringComparer.Ordinal.Equals(
            request.SubmissionSchemaIdentity, _workerContract.SubmissionSchemaIdentity) &&
        StringComparer.Ordinal.Equals(
            request.SubmissionSchemaFingerprint, _workerContract.SubmissionSchemaFingerprint) &&
        string.Equals(
            request.ToolDeclarationIdentity,
            Pass2StrictToolDeclarations.Identity,
            StringComparison.Ordinal) &&
        StringComparer.Ordinal.Equals(
            request.ToolDeclarationFingerprint, _workerContract.ToolDeclarationFingerprint) &&
        request.InitialEvidence is { Count: > 0 } &&
        request.InitialEvidence.All(IsValidEvidenceBinding);

    private static bool IsValidEvidenceBinding(Pass2BusinessLogicEvidenceBinding binding) =>
        Guid.TryParseExact(binding.OpaqueHandle, "D", out Guid handle) &&
        handle != Guid.Empty &&
        string.Equals(binding.OpaqueHandle, handle.ToString("D"), StringComparison.Ordinal) &&
        binding.DocumentVersionId != Guid.Empty &&
        binding.RepositoryId != Guid.Empty &&
        binding.ScanRunId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(binding.ProfiledRevision);

    private static bool IsLowerFingerprint(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string LoadPrompt()
    {
        Assembly assembly = typeof(PreparedPass2WorkerDispatcher).Assembly;
        string[] matches = assembly.GetManifestResourceNames()
            .Where(name => string.Equals(name, PromptResourceName, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException("The fixed Pass 2 prompt is unavailable.");
        }

        using Stream stream = assembly.GetManifestResourceStream(matches[0]) ??
            throw new InvalidOperationException("The fixed Pass 2 prompt is unavailable.");
        using var reader = new StreamReader(stream, _strictUtf8, true);
        string prompt = reader.ReadToEnd();
        if (string.IsNullOrWhiteSpace(prompt) ||
            !prompt.Contains("retrieve_code_logic", StringComparison.Ordinal) ||
            !prompt.Contains("submit_business_logic", StringComparison.Ordinal) ||
            !prompt.Contains("business-logic/flow-v1", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The fixed Pass 2 prompt is invalid.");
        }

        return prompt;
    }

    private static string Sanitize(string? reasonCode) => reasonCode switch
    {
        "invalid-binding" or "request-bound" or "provider-timeout" or
        "provider-unavailable" or "invalid-tool-call" or
        "submission-rejected" or "submission-missing" or
        "retrieval-bound-exceeded" => reasonCode,
        _ => "worker-failed",
    };
}
