using Microsoft.AspNetCore.Components.Authorization;
using FluentValidation;
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
    string? Message)
{
    public static PreparedChatResult Succeeded(GroundedAnswer answer) =>
        new(PreparedChatResultKind.Succeeded, answer, null);

    public static PreparedChatResult Failed(PreparedChatResultKind kind, string message) =>
        new(kind, null, message);
}

public sealed class PreparedChatCoordinator(
    AuthenticationStateProvider authenticationStateProvider,
    ISystemRepository systemRepository,
    IRepositoryRepository repositoryRepository,
    IRepositoryAuthorizationVerifier authorizationVerifier,
    IChatEvidenceRetriever evidenceRetriever,
    ChatCandidateExtractor candidateExtractor,
    IChatAgentDispatcher dispatcher,
    IChatEvidenceCurrentnessValidator currentnessValidator,
    TimeProvider timeProvider,
    ChatAgentResultValidator resultValidator,
    IPreparedChatScopeResolver preparedScopeResolver,
    IValidator<PreparedChatQuestionRequest> requestValidator)
{
    private const string _invalidRequestMessage = "请选择业务模式、系统和两个不同的仓库，并输入 1 至 2000 个字符的问题。";
    private const string _missingMessage = "未找到可用的知识范围，或您无权访问。";
    private const string _evidenceUnavailableMessage = "当前知识证据不可用或已更新，请稍后重试。";
    private const string _unsupportedMessage = "当前知识库中没有足够信息回答该问题。";
    private const string _providerUnavailableMessage = "暂时无法生成回答，请稍后重试。";
    private const string _timedOutMessage = "回答超时，请重试。";
    private const string _cancelledMessage = "已取消本次提问。";

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
            FluentValidation.Results.ValidationResult validationResult =
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
            UserContext? userContext = await GetCurrentUserContextAsync();
            if (userContext is null ||
                await systemRepository.GetVisibleByIdAsync(
                    request.SystemId,
                    userContext.ToSystemOwnerFilter(),
                    cancellationToken) is null)
            {
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
                    return Missing();
                }

                selectedRepositories.Add(repositoryId, repository);
            }

            var accessScope = new RepositoryAccessScope(
                request.RepositoryIds,
                authorizationVerifier);
            if (!await accessScope.HasCurrentAccessAsync(request.RepositoryIds, cancellationToken))
            {
                return Missing();
            }

            if (await preparedScopeResolver.ResolveAsync(
                    request.SystemId,
                    request.RepositoryIds,
                    accessScope,
                    cancellationToken) is null)
            {
                return Missing();
            }

            ChatEvidenceResult evidence = await evidenceRetriever.RetrieveAsync(
                request,
                accessScope,
                cancellationToken);
            if (evidence.Kind == ChatEvidenceResultKind.NotFoundOrUnauthorized)
            {
                return Missing();
            }

            if (evidence.Kind != ChatEvidenceResultKind.Found ||
                evidence.Packet is not EvidencePacket packet ||
                !PacketMatchesRequest(packet, request))
            {
                return EvidenceUnavailable();
            }

            IReadOnlyList<CandidateClaim> candidates = candidateExtractor.Extract(packet);
            if (candidates.Count == 0)
            {
                return EvidenceUnavailable();
            }

            ChatSynthesisRequest synthesisRequest = ChatSynthesisRequest.Create(
                request.RequestId,
                request.Mode,
                request.SystemId,
                request.Question,
                packet.Fingerprint,
                candidates);
            ChatSynthesisOutcome synthesis = await dispatcher.GenerateAsync(
                synthesisRequest,
                cancellationToken);
            if (synthesis.Result is null)
            {
                return MapSynthesisFailure(synthesis.FailureCode);
            }

            ChatAgentResultValidation validation = resultValidator.Validate(
                packet,
                synthesisRequest,
                synthesis);
            if (!validation.IsValid)
            {
                return EvidenceUnavailable();
            }

            if (!validation.IsAnswerable)
            {
                return await authorizationVerifier.HasCurrentAccessAsync(
                    request.RepositoryIds,
                    cancellationToken)
                        ? Unsupported()
                        : Missing();
            }

            IReadOnlyList<CandidateClaim> selectedCandidates = validation.SelectedCandidates;

            ChatCurrentnessValidationRequest currentnessRequest = BuildCurrentnessRequest(
                request,
                packet,
                selectedCandidates);
            ChatCurrentnessValidationOutcome currentness = await currentnessValidator.ValidateCurrentAsync(
                currentnessRequest,
                accessScope,
                cancellationToken);
            if (currentness != ChatCurrentnessValidationOutcome.Current)
            {
                return EvidenceUnavailable();
            }

            if (!await authorizationVerifier.HasCurrentAccessAsync(
                    request.RepositoryIds,
                    cancellationToken))
            {
                return Missing();
            }

            GroundedAnswer answer = BuildAnswer(
                request.Mode,
                packet,
                selectedCandidates,
                selectedRepositories,
                timeProvider.GetUtcNow());
            return PreparedChatResult.Succeeded(answer);
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
        catch (ChatContractException)
        {
            return EvidenceUnavailable();
        }
        catch (Exception)
        {
            return ProviderUnavailable();
        }
    }

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
        EvidencePacket packet,
        IReadOnlyCollection<CandidateClaim> candidates)
    {
        Dictionary<string, EvidenceDocument> documents = packet.Documents.ToDictionary(
            document => document.DocumentHandle,
            StringComparer.Ordinal);
        Dictionary<Guid, EvidenceDocument> documentsByVersion = packet.Documents.ToDictionary(
            document => document.DocumentVersionId);
        Dictionary<string, EvidenceAnchor> anchors = packet.SourceAnchors.ToDictionary(
            anchor => anchor.AnchorHandle,
            StringComparer.Ordinal);
        Dictionary<string, EvidenceRelationship> relationships = packet.Relationships.ToDictionary(
            relationship => relationship.RelationshipHandle,
            StringComparer.Ordinal);
        HashSet<Guid> documentVersionIds = candidates
            .Select(candidate => candidate.DocumentVersionId)
            .ToHashSet();
        EvidenceRelationship[] selectedRelationships =
        [
            .. candidates
            .SelectMany(candidate => candidate.RelationshipHandles)
            .Distinct(StringComparer.Ordinal)
            .Select(handle => relationships[handle]),
        ];
        foreach (EvidenceRelationship relationship in selectedRelationships)
        {
            _ = documentVersionIds.Add(documents[relationship.SourceDocumentHandle].DocumentVersionId);
            _ = documentVersionIds.Add(documents[relationship.TargetDocumentHandle].DocumentVersionId);
        }

        ChatCurrentDocument[] currentDocuments =
        [
            .. documentVersionIds
            .Select(versionId => documentsByVersion[versionId])
            .Select(document => new ChatCurrentDocument(
                document.LogicalDocumentId,
                document.DocumentVersionId)),
        ];
        ChatCurrentSection[] currentSections =
        [
            .. candidates
            .Select(candidate => new ChatCurrentSection(
                candidate.StableSectionId,
                candidate.DocumentVersionId,
                candidate.SpanStart,
                candidate.SpanLength,
                candidate.TextSha256)),
        ];
        ChatCurrentSourceAnchor[] currentAnchors =
        [
            .. candidates
            .SelectMany(candidate => candidate.AnchorHandles)
            .Distinct(StringComparer.Ordinal)
            .Select(handle => anchors[handle])
            .Select(anchor => new ChatCurrentSourceAnchor(
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
            .. selectedRelationships
            .Select(relationship =>
            {
                EvidenceDocument source = documents[relationship.SourceDocumentHandle];
                EvidenceDocument target = documents[relationship.TargetDocumentHandle];
                return new ChatCurrentRelationship(
                    relationship.RelationshipIdentity,
                    relationship.RelationshipType,
                    source.DocumentVersionId,
                    target.DocumentVersionId,
                    source.RepositoryId,
                    target.RepositoryId,
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
            currentRelationships);
    }

    private static GroundedAnswer BuildAnswer(
        ChatMode mode,
        EvidencePacket packet,
        IReadOnlyList<CandidateClaim> candidates,
        Dictionary<Guid, RepositoryEntity> repositories,
        DateTimeOffset generatedAt)
    {
        Dictionary<Guid, EvidenceDocument> documents = packet.Documents.ToDictionary(
            document => document.DocumentVersionId);
        Dictionary<string, EvidenceAnchor> anchors = packet.SourceAnchors.ToDictionary(
            anchor => anchor.AnchorHandle,
            StringComparer.Ordinal);
        Dictionary<Guid, EvidenceSection> sections = packet.Sections.ToDictionary(
            section => section.StableSectionId);
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
                candidate.Text,
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
