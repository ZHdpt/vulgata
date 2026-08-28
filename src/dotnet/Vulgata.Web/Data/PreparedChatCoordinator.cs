using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Components.Authorization;
using Vulgata.Agents.ChatExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Shared;
using Vulgata.Shared.Chat;
using Vulgata.Web.RequestPipeline;
using RepositoryEntity = Vulgata.Core.Entities.Repository;

namespace Vulgata.Web.Data;

public sealed record PreparedChatRepositoryOption(Guid RepositoryId, string DisplayName);

public sealed record PreparedChatScopeOption(
    Guid SystemId,
    string SystemDisplayName,
    IReadOnlyList<PreparedChatRepositoryOption> Repositories);

public enum PreparedChatResultKind
{
    Succeeded,
    Unsupported,
    InvalidRequest,
    MissingOrUnauthorized,
    EvidenceUnavailable,
    ProviderUnavailable,
    TimedOut,
    Cancelled,
}

public sealed record PreparedChatResult(
    PreparedChatResultKind Kind,
    GroundedAnswer? Answer,
    string? Message,
    long RemainingDeadlineMilliseconds)
{
    public static PreparedChatResult Succeeded(
        GroundedAnswer answer,
        long remainingDeadlineMilliseconds) =>
        new(
            PreparedChatResultKind.Succeeded,
            answer,
            null,
            Math.Max(0, remainingDeadlineMilliseconds));

    public static PreparedChatResult Failed(PreparedChatResultKind kind, string message) =>
        new(kind, null, message, 0);
}

public sealed class PreparedChatCoordinator(
    AuthenticationStateProvider authenticationStateProvider,
    ISystemRepository systemRepository,
    IRepositoryRepository repositoryRepository,
    IRepositoryAuthorizationVerifier authorizationVerifier,
    IChatAdvisoryContextReader advisoryContextReader,
    IChatEvidenceRetriever evidenceRetriever,
    ChatCandidateExtractor candidateExtractor,
    IChatAgentDispatcher dispatcher,
    IChatEvidenceCurrentnessValidator currentnessValidator,
    TimeProvider timeProvider,
    ChatAgentResultValidator resultValidator,
    IPreparedChatScopeResolver preparedScopeResolver,
    IValidator<PreparedChatQuestionRequest> requestValidator,
    ILogger<PreparedChatCoordinator>? logger = null) : IDisposable
{
    private const string _invalidRequestMessage = "请选择业务模式、系统和两个不同的仓库，并输入 1 至 2000 个字符的问题。";
    private const string _missingMessage = "未找到可用的知识范围，或您无权访问。";
    private const string _evidenceUnavailableMessage = "当前知识证据不可用或已更新，请稍后重试。";
    private const string _unsupportedMessage = "当前知识库中没有足够信息回答该问题。";
    private const string _providerUnavailableMessage = "暂时无法生成回答，请稍后重试。";
    private const string _timedOutMessage = "回答超时，请重试。";
    private const string _cancelledMessage = "已取消本次提问。";
    private ChatConversationState? _conversationState;

    public async Task<IReadOnlyList<PreparedChatScopeOption>> ListScopesAsync(
        CancellationToken cancellationToken = default)
    {
        var authenticationState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var principal = authenticationState.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            return [];
        }

        var userContext = new UserContext(
            principal,
            principal.IsInRole(RoleNames.Administrator));
        if (string.IsNullOrWhiteSpace(userContext.UserId))
        {
            return [];
        }

        var systems = await systemRepository.ListVisibleAsync(
            userContext.ToSystemOwnerFilter(),
            cancellationToken);
        var scopes = new List<PreparedChatScopeOption>(systems.Count);
        foreach (var system in systems.OrderBy(system => system.Name, StringComparer.Ordinal))
        {
            var repositories = await repositoryRepository.ListBySystemAsync(
                system.Id,
                cancellationToken);
            if (repositories.Count < 2)
            {
                continue;
            }

            Guid[] candidateIds = repositories.Select(repository => repository.Id).ToArray();
            if (candidateIds.Length > 256)
            {
                continue;
            }

            var accessScope = new RepositoryAccessScope(candidateIds, authorizationVerifier);
            PreparedChatScopeBinding? binding = await preparedScopeResolver.ResolveAsync(
                system.Id,
                candidateIds,
                accessScope,
                cancellationToken);
            if (binding is null ||
                !await accessScope.HasCurrentAccessAsync(
                    binding.RepositoryIds,
                    cancellationToken))
            {
                continue;
            }

            PreparedChatRepositoryOption[] options = repositories
                .Where(repository => binding.RepositoryIds.Contains(repository.Id))
                .OrderBy(repository => repository.Name, StringComparer.Ordinal)
                .Select(repository => new PreparedChatRepositoryOption(
                    repository.Id,
                    repository.Name))
                .ToArray();
            scopes.Add(new PreparedChatScopeOption(
                system.Id,
                system.Name,
                Array.AsReadOnly(options)));
        }

        return scopes.AsReadOnly();
    }

    public async Task<PreparedChatResult> AskAsync(
        ChatMode mode,
        Guid systemId,
        IReadOnlyCollection<Guid> repositoryIds,
        string question,
        CancellationToken cancellationToken = default)
    {
        var boundaryRequest = new PreparedChatQuestionRequest
        {
            Mode = mode switch
            {
                ChatMode.Business => PreparedChatMode.Business,
                ChatMode.It => PreparedChatMode.It,
                _ => null,
            },
            SystemId = systemId,
            RepositoryIds = repositoryIds,
            Question = question,
        };
        return await AskAsync(boundaryRequest, cancellationToken);
    }

    public async Task<PreparedChatResult> AskAsync(
        PreparedChatQuestionRequest boundaryRequest,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Cancelled();
        }

        try
        {
            ValidationResult validationResult =
                await requestValidator.ValidateAsync(boundaryRequest, cancellationToken);
            if (!validationResult.IsValid)
            {
                return InvalidRequest();
            }

            ChatMode mode = boundaryRequest.Mode == PreparedChatMode.Business
                ? ChatMode.Business
                : ChatMode.It;
            ChatQuestionRequest request = ChatQuestionRequest.Create(
                Guid.NewGuid().ToString("N"),
                mode,
                boundaryRequest.SystemId,
                boundaryRequest.RepositoryIds,
                boundaryRequest.NormalizedQuestion);
            ChatConversationScope conversationScope = ChatConversationScope.Create(
                request.Mode,
                request.SystemId,
                request.RepositoryIds);
            _conversationState = (_conversationState ?? ChatConversationState.Empty(conversationScope))
                .ResetForScopeChange(conversationScope);
            DateTimeOffset deadlineUtc = timeProvider.GetUtcNow().AddSeconds(29);
            long absoluteDeadlineUnixTimeMilliseconds = deadlineUtc.ToUnixTimeMilliseconds();
            UserContext? userContext = await GetCurrentUserContextAsync();
            if (userContext is null ||
                await systemRepository.GetVisibleByIdAsync(
                    request.SystemId,
                    userContext.ToSystemOwnerFilter(),
                    cancellationToken) is null)
            {
                ClearConversation();
                return Missing();
            }

            Dictionary<Guid, RepositoryEntity> selectedRepositories = [];
            foreach (Guid repositoryId in request.RepositoryIds)
            {
                RepositoryEntity? repository = await repositoryRepository.GetBySystemAndIdAsync(
                    request.SystemId,
                    repositoryId,
                    cancellationToken);
                if (repository is null)
                {
                    ClearConversation();
                    return Missing();
                }

                selectedRepositories.Add(repositoryId, repository);
            }

            var accessScope = new RepositoryAccessScope(
                request.RepositoryIds,
                authorizationVerifier);
            if (!await accessScope.HasCurrentAccessAsync(request.RepositoryIds, cancellationToken))
            {
                ClearConversation();
                return Missing();
            }

            if (await preparedScopeResolver.ResolveAsync(
                    request.SystemId,
                    request.RepositoryIds,
                    accessScope,
                    cancellationToken) is null)
            {
                ClearConversation();
                return Missing();
            }

            ChatAdvisoryContextReadResult advisoryResult =
                await advisoryContextReader.ReadAsync(
                    new AuthorizedChatScope(
                        request.SystemId,
                        request.RepositoryIds,
                        accessScope),
                    cancellationToken);
            if (advisoryResult.Kind == ChatAdvisoryContextReadResultKind.NotFoundOrUnauthorized)
            {
                ClearConversation();
                return Missing();
            }

            if (advisoryResult.Kind != ChatAdvisoryContextReadResultKind.Succeeded ||
                advisoryResult.Snapshot is not ChatAdvisoryContextSnapshot advisoryContext)
            {
                return EvidenceUnavailable();
            }

            ChatRetrievalRequest retrievalRequest = ChatRetrievalRequest.Create(
                request,
                _conversationState,
                deadlineUtc,
                advisoryContext);
            ChatEvidenceResult evidence = await evidenceRetriever.RetrieveAsync(
                retrievalRequest,
                accessScope,
                cancellationToken);
            if (evidence.Kind == ChatEvidenceResultKind.NotFoundOrUnauthorized)
            {
                ClearConversation();
                return Missing();
            }

            if (evidence.Kind != ChatEvidenceResultKind.Found ||
                evidence.Packet is not EvidencePacket packet ||
                !PacketMatchesRequest(packet, request))
            {
                return EvidenceUnavailable();
            }

            IReadOnlyList<CandidateClaim> extractedCandidates = candidateExtractor.Extract(packet);
            if (extractedCandidates.Count == 0)
            {
                return EvidenceUnavailable();
            }

            IReadOnlyList<CandidateClaim> candidates = ChatCandidateRanker.Rank(
                request.Question,
                request.Mode,
                extractedCandidates);
            if (ChatQuestionRelevanceGate.IsDefinitelyUnrelated(request.Question))
            {
                return await authorizationVerifier.HasCurrentAccessAsync(
                    request.RepositoryIds,
                    cancellationToken)
                    ? Unsupported()
                    : Missing();
            }

            ChatSynthesisRequest synthesisRequest = ChatSynthesisRequest.Create(
                request.RequestId,
                request.Mode,
                request.SystemId,
                request.Question,
                packet.Fingerprint,
                candidates,
                absoluteDeadlineUnixTimeMilliseconds);
            ChatSynthesisOutcome synthesis = await dispatcher.GenerateAsync(
                synthesisRequest,
                cancellationToken);
            if (synthesis.Result is null)
            {
                IReadOnlyList<CandidateClaim> fallbackCandidates =
                    synthesis.FailureCode is ChatContractFailureCode.ProviderFailure or
                        ChatContractFailureCode.TimedOut
                        ? ChatCandidateRanker.SelectStrongGroundedMatches(
                            request.Question,
                            candidates)
                        : [];
                if (fallbackCandidates.Count > 0)
                {
                    logger?.LogInformation(
                        "Prepared Chat used grounded rescue after synthesis failure {FailureCode} in {Mode} mode.",
                        synthesis.FailureCode,
                        request.Mode);
                    return await CompleteGroundedAnswerAsync(
                        request,
                        packet,
                        fallbackCandidates,
                        CreateGroundedFallbackClaims(fallbackCandidates),
                        selectedRepositories,
                        accessScope,
                        absoluteDeadlineUnixTimeMilliseconds,
                        cancellationToken);
                }

                logger?.LogWarning(
                    "Prepared Chat synthesis failed with {FailureCode}; no strong grounded rescue was available in {Mode} mode.",
                    synthesis.FailureCode,
                    request.Mode);
                return MapSynthesisFailure(synthesis.FailureCode);
            }

            ChatAgentResultValidation validation = resultValidator.Validate(
                packet,
                synthesisRequest,
                synthesis);
            if (!validation.IsValid)
            {
                if (dispatcher is IChatStreamValidationDispositionRecorder recorder)
                {
                    _ = recorder.TryRecordFinalValidationDisposition(
                        synthesisRequest.RequestId,
                        ChatStreamValidationDisposition.RejectedSemantic);
                }

                logger?.LogWarning(
                    "Prepared Chat provider selection failed validation in {Mode} mode.",
                    request.Mode);
                return EvidenceUnavailable();
            }

            if (!validation.IsAnswerable)
            {
                if (dispatcher is IChatStreamValidationDispositionRecorder recorder)
                {
                    _ = recorder.TryRecordFinalValidationDisposition(
                        synthesisRequest.RequestId,
                        ChatStreamValidationDisposition.RejectedSemantic);
                }

                IReadOnlyList<CandidateClaim> fallbackCandidates =
                    ChatCandidateRanker.SelectStrongGroundedMatches(
                        request.Question,
                        candidates);
                if (fallbackCandidates.Count > 0)
                {
                    logger?.LogInformation(
                        "Prepared Chat used grounded rescue after an unsupported provider decision in {Mode} mode.",
                        request.Mode);
                    return await CompleteGroundedAnswerAsync(
                        request,
                        packet,
                        fallbackCandidates,
                        CreateGroundedFallbackClaims(fallbackCandidates),
                        selectedRepositories,
                        accessScope,
                        absoluteDeadlineUnixTimeMilliseconds,
                        cancellationToken);
                }

                logger?.LogInformation(
                    "Prepared Chat provider returned unsupported with no strong grounded rescue in {Mode} mode.",
                    request.Mode);
                return await authorizationVerifier.HasCurrentAccessAsync(
                    request.RepositoryIds,
                    cancellationToken)
                    ? Unsupported()
                    : Missing();
            }

            return await CompleteGroundedAnswerAsync(
                request,
                packet,
                validation.SelectedCandidates,
                validation.MaterialClaims,
                selectedRepositories,
                accessScope,
                absoluteDeadlineUnixTimeMilliseconds,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Cancelled();
        }
        catch (ChatContractException exception) when (
            exception.FailureCode == ChatContractFailureCode.InvalidRequest)
        {
            return InvalidRequest();
        }
        catch (ChatContractException exception)
        {
            logger?.LogWarning(
                "Prepared Chat rejected contract {FailureCode}.",
                exception.FailureCode);
            return EvidenceUnavailable();
        }
        catch (Exception exception)
        {
            logger?.LogWarning(
                "Prepared Chat failed at an unclassified stage with {ErrorType}.",
                exception.GetType().Name);
            return ProviderUnavailable();
        }
    }

    private async Task<PreparedChatResult> CompleteGroundedAnswerAsync(
        ChatQuestionRequest request,
        EvidencePacket packet,
        IReadOnlyList<CandidateClaim> selectedCandidates,
        IReadOnlyList<MaterialClaim> materialClaims,
        Dictionary<Guid, RepositoryEntity> selectedRepositories,
        RepositoryAccessScope accessScope,
        long absoluteDeadlineUnixTimeMilliseconds,
        CancellationToken cancellationToken)
    {
        ChatCurrentnessValidationRequest currentnessRequest = BuildCurrentnessRequest(
            request,
            packet);
        ChatCurrentnessValidationOutcome currentness = await currentnessValidator.ValidateCurrentAsync(
            currentnessRequest,
            accessScope,
            cancellationToken);
        if (currentness != ChatCurrentnessValidationOutcome.Current)
        {
            logger?.LogWarning(
                "Prepared Chat currentness validation returned {Currentness} in {Mode} mode.",
                currentness,
                request.Mode);
            return EvidenceUnavailable();
        }

        if (!await authorizationVerifier.HasCurrentAccessAsync(
                request.RepositoryIds,
                cancellationToken))
        {
            ClearConversation();
            return Missing();
        }

        GroundedAnswer answer = BuildAnswer(
            request.Mode,
            packet,
            selectedCandidates,
            materialClaims,
            selectedRepositories,
            timeProvider.GetUtcNow());
        AppendValidatedConversationTurn(request, packet, selectedCandidates, answer);
        long remainingDeadlineMilliseconds = absoluteDeadlineUnixTimeMilliseconds -
            timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        return PreparedChatResult.Succeeded(answer, remainingDeadlineMilliseconds);
    }

    private void AppendValidatedConversationTurn(
        ChatQuestionRequest request,
        EvidencePacket packet,
        IReadOnlyCollection<CandidateClaim> selectedCandidates,
        GroundedAnswer answer)
    {
        if (_conversationState is null)
        {
            return;
        }

        HashSet<Guid> selectedDocumentVersionIds = selectedCandidates
            .Select(candidate => candidate.DocumentVersionId)
            .ToHashSet();
        Guid[] logicalDocumentIds = packet.Documents
            .Where(document => selectedDocumentVersionIds.Contains(document.DocumentVersionId))
            .Select(document => document.LogicalDocumentId)
            .Distinct()
            .ToArray();
        string[] citationHandles = selectedCandidates
            .SelectMany(candidate => candidate.AnchorHandles)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string answerText = string.Join(
            '\n',
            answer.MaterialClaims
                .OrderBy(claim => claim.Order)
                .Select(claim => claim.Text));
        ValidatedConversationTurn turn = ValidatedConversationTurn.Create(
            request.Question,
            answerText,
            packet.Fingerprint,
            citationHandles,
            logicalDocumentIds);
        _conversationState = _conversationState.AppendValidatedTurn(turn);
    }

    private void ClearConversation()
    {
        if (_conversationState is not null)
        {
            _conversationState = _conversationState.Reset();
        }
    }

    public void Dispose()
    {
        ClearConversation();
        GC.SuppressFinalize(this);
    }

    private static IReadOnlyList<MaterialClaim> CreateGroundedFallbackClaims(
        IReadOnlyList<CandidateClaim> candidates) =>
        Array.AsReadOnly(candidates
            .Select((candidate, index) => MaterialClaim.Create(
                $"claim{index + 1}",
                index + 1,
                candidate.Text,
                candidate.AnchorHandles))
            .ToArray());

    private async Task<UserContext?> GetCurrentUserContextAsync()
    {
        var authenticationState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var principal = authenticationState.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userContext = new UserContext(
            principal,
            principal.IsInRole(RoleNames.Administrator));
        return string.IsNullOrWhiteSpace(userContext.UserId) ? null : userContext;
    }

    private static bool PacketMatchesRequest(
        EvidencePacket packet,
        ChatQuestionRequest request) =>
        StringComparer.Ordinal.Equals(packet.RequestId, request.RequestId) &&
        packet.Mode == request.Mode &&
        packet.SystemId == request.SystemId &&
        packet.RepositoryIds.SequenceEqual(request.RepositoryIds);

    private static ChatCurrentnessValidationRequest BuildCurrentnessRequest(
        ChatQuestionRequest request,
        EvidencePacket packet)
    {
        Dictionary<string, EvidenceDocument> documents = packet.Documents.ToDictionary(
            document => document.DocumentHandle,
            StringComparer.Ordinal);

        ChatCurrentDocument[] currentDocuments =
        [
            .. packet.Documents.Select(document => new ChatCurrentDocument(
                    document.LogicalDocumentId,
                    document.DocumentVersionId,
                    document.Title)),
        ];
        ChatCurrentSection[] currentSections =
        [
            .. packet.Sections.Select(section => new ChatCurrentSection(
                section.StableSectionId,
                documents[section.DocumentHandle].DocumentVersionId,
                0,
                section.ContentMarkdown.Length,
                Sha256(section.ContentMarkdown))),
        ];
        ChatCurrentSourceAnchor[] currentAnchors =
        [
            .. packet.SourceAnchors.Select(anchor => new ChatCurrentSourceAnchor(
                    anchor.AnchorId,
                    anchor.DocumentVersionId,
                    anchor.RepositoryId,
                    anchor.RepositoryRelativePath,
                    anchor.LineStart,
                    anchor.LineEnd,
                    anchor.SourceHash)),
        ];
        ChatCurrentRelationship[] currentRelationships =
        [
            .. packet.Relationships.Select(relationship =>
                {
                    EvidenceDocument source = documents[relationship.SourceDocumentHandle];
                    EvidenceDocument target = documents[relationship.TargetDocumentHandle];
                    return new ChatCurrentRelationship(
                        relationship.EdgeId,
                        relationship.RelationshipType,
                        source.DocumentVersionId,
                        target.DocumentVersionId,
                        source.RepositoryId,
                        target.RepositoryId,
                        relationship.Depth,
                        relationship.ExactDecisionId,
                        relationship.ExactDecisionCount,
                        relationship.ExactDecisionSetFingerprint,
                        relationship.FlowGroupId,
                        relationship.ProfileFingerprint);
                }),
        ];
        return ChatCurrentnessValidationRequest.Create(
            request.RequestId,
            packet.Fingerprint,
            request.SystemId,
            request.RepositoryIds,
            currentDocuments,
            currentSections,
            currentAnchors,
            currentRelationships,
            packet.FlowGroups);
    }

    private static string Sha256(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static GroundedAnswer BuildAnswer(
        ChatMode mode,
        EvidencePacket packet,
        IReadOnlyList<CandidateClaim> candidates,
        IReadOnlyList<MaterialClaim> validatedClaims,
        Dictionary<Guid, RepositoryEntity> repositories,
        DateTimeOffset generatedAt)
    {
        Dictionary<Guid, EvidenceDocument> documents = packet.Documents.ToDictionary(document => document.DocumentVersionId);
        Dictionary<string, EvidenceAnchor> anchors = packet.SourceAnchors.ToDictionary(
            anchor => anchor.AnchorHandle,
            StringComparer.Ordinal);
        Dictionary<Guid, EvidenceSection> sections = packet.Sections.ToDictionary(section => section.StableSectionId);
        if (validatedClaims.Count != candidates.Count)
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidSelection);
        }

        var claims = new List<MaterialClaim>(candidates.Count);
        var citations = new List<DocumentCitation>();
        for (int claimIndex = 0; claimIndex < candidates.Count; claimIndex++)
        {
            CandidateClaim candidate = candidates[claimIndex];
            string claimId = $"claim-{claimIndex + 1}";
            var citationIds = new List<string>(candidate.AnchorHandles.Count);
            for (int anchorIndex = 0; anchorIndex < candidate.AnchorHandles.Count; anchorIndex++)
            {
                EvidenceAnchor anchor = anchors[candidate.AnchorHandles[anchorIndex]];
                EvidenceDocument document = documents[anchor.DocumentVersionId];
                string citationId = $"citation-{claimIndex + 1}-{anchorIndex + 1}";
                citationIds.Add(citationId);
                citations.Add(DocumentCitation.Create(
                    citationId,
                    [claimId],
                    document.Title,
                    document.DocumentType,
                    repositories[document.RepositoryId].Name,
                    anchor.RepositoryRelativePath,
                    anchor.LineStart,
                    anchor.LineEnd,
                    sections[candidate.StableSectionId].Heading));
            }

            claims.Add(MaterialClaim.Create(
                claimId,
                claimIndex + 1,
                validatedClaims[claimIndex].Text,
                citationIds));
        }

        return GroundedAnswer.Create(mode, claims, citations, generatedAt);
    }

    private static PreparedChatResult MapSynthesisFailure(ChatContractFailureCode? failureCode) =>
        failureCode switch
        {
            ChatContractFailureCode.Cancelled => Cancelled(),
            ChatContractFailureCode.TimedOut => TimedOut(),
            ChatContractFailureCode.InvalidSelection => EvidenceUnavailable(),
            _ => ProviderUnavailable(),
        };

    private static PreparedChatResult InvalidRequest() =>
        PreparedChatResult.Failed(PreparedChatResultKind.InvalidRequest, _invalidRequestMessage);

    private static PreparedChatResult Missing() =>
        PreparedChatResult.Failed(PreparedChatResultKind.MissingOrUnauthorized, _missingMessage);

    private static PreparedChatResult EvidenceUnavailable() =>
        PreparedChatResult.Failed(PreparedChatResultKind.EvidenceUnavailable, _evidenceUnavailableMessage);

    private static PreparedChatResult Unsupported() =>
        PreparedChatResult.Failed(PreparedChatResultKind.Unsupported, _unsupportedMessage);

    private static PreparedChatResult ProviderUnavailable() =>
        PreparedChatResult.Failed(PreparedChatResultKind.ProviderUnavailable, _providerUnavailableMessage);

    private static PreparedChatResult TimedOut() =>
        PreparedChatResult.Failed(PreparedChatResultKind.TimedOut, _timedOutMessage);

    private static PreparedChatResult Cancelled() =>
        PreparedChatResult.Failed(PreparedChatResultKind.Cancelled, _cancelledMessage);
}
