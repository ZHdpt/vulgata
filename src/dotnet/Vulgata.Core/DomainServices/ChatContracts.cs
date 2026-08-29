using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

public enum ChatMode
{
    Business,
    It,
}

public enum ChatContractFailureCode
{
    InvalidRequest,
    InvalidEvidence,
    InvalidSelection,
    InvalidCurrentness,
    InvalidCitation,
    ProviderFailure,
    TimedOut,
    Cancelled,
}

public sealed class ChatContractException : Exception
{
    public ChatContractException(ChatContractFailureCode failureCode)
        : base(failureCode.ToString())
    {
        FailureCode = failureCode;
    }

    public ChatContractFailureCode FailureCode { get; }
}

public sealed record ChatQuestionRequest
{
    private ChatQuestionRequest(
        string requestId,
        ChatMode mode,
        Guid systemId,
        IReadOnlyList<Guid> repositoryIds,
        string question)
    {
        RequestId = requestId;
        Mode = mode;
        SystemId = systemId;
        RepositoryIds = repositoryIds;
        Question = question;
    }

    public string RequestId { get; }

    public ChatMode Mode { get; }

    public Guid SystemId { get; }

    public IReadOnlyList<Guid> RepositoryIds { get; }

    public string Question { get; }

    public static ChatQuestionRequest Create(
        string requestId,
        ChatMode mode,
        Guid systemId,
        IReadOnlyCollection<Guid> repositoryIds,
        string question)
    {
        if (string.IsNullOrWhiteSpace(requestId) ||
            repositoryIds is null ||
            !Enum.IsDefined(mode) || systemId == Guid.Empty)
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidRequest);
        }

        Guid[] canonicalRepositoryIds = repositoryIds.Order().ToArray();
        if (canonicalRepositoryIds.Length != 2 ||
            canonicalRepositoryIds.Any(id => id == Guid.Empty) ||
            canonicalRepositoryIds[0] == canonicalRepositoryIds[1])
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidRequest);
        }

        string normalizedQuestion = NormalizeQuestion(question);
        int questionLength = normalizedQuestion.EnumerateRunes().Count();
        if (questionLength is < 1 or > 2_000)
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidRequest);
        }

        return new ChatQuestionRequest(
            requestId.Trim(),
            mode,
            systemId,
            Array.AsReadOnly(canonicalRepositoryIds),
            normalizedQuestion);
    }

    private static string NormalizeQuestion(string question)
    {
        if (question is null)
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidRequest);
        }

        string normalized = question.Normalize(NormalizationForm.FormC);
        var builder = new StringBuilder(normalized.Length);
        bool whitespacePending = false;
        foreach (char character in normalized)
        {
            if (char.IsWhiteSpace(character))
            {
                whitespacePending = builder.Length > 0;
                continue;
            }

            if (char.IsControl(character))
            {
                throw new ChatContractException(ChatContractFailureCode.InvalidRequest);
            }

            if (whitespacePending)
            {
                _ = builder.Append(' ');
                whitespacePending = false;
            }

            _ = builder.Append(character);
        }

        return builder.ToString();
    }
}

public enum ChatEvidenceResultKind
{
    Found,
    NotFoundOrUnauthorized,
    InvalidRequest,
    IncompleteOrStale,
}

public sealed record ChatEvidenceResult
{
    private ChatEvidenceResult(ChatEvidenceResultKind kind, EvidencePacket? packet)
    {
        Kind = kind;
        Packet = packet;
    }

    public ChatEvidenceResultKind Kind { get; }

    public EvidencePacket? Packet { get; }

    public static ChatEvidenceResult Found(EvidencePacket packet) =>
        new(ChatEvidenceResultKind.Found, packet ?? throw new ArgumentNullException(nameof(packet)));

    public static ChatEvidenceResult NotFoundOrUnauthorized() =>
        new(ChatEvidenceResultKind.NotFoundOrUnauthorized, null);

    public static ChatEvidenceResult InvalidRequest() =>
        new(ChatEvidenceResultKind.InvalidRequest, null);

    public static ChatEvidenceResult IncompleteOrStale() =>
        new(ChatEvidenceResultKind.IncompleteOrStale, null);
}

public enum ChatRelationshipType
{
    References,
    GeneratedFrom,
}

public sealed record EvidenceDocument(
    string DocumentHandle,
    Guid LogicalDocumentId,
    Guid DocumentVersionId,
    Guid RepositoryId,
    DocumentType DocumentType,
    string Title,
    string ProfiledRevision);

public sealed record EvidenceSection(
    string SectionHandle,
    string DocumentHandle,
    Guid StableSectionId,
    int SectionOrder,
    DocumentSectionKind SectionKind,
    string Heading,
    string ContentMarkdown,
    IReadOnlyList<string> AnchorHandles,
    IReadOnlyList<string> SupportingDocumentHandles);

public sealed record EvidenceAnchor(
    string AnchorHandle,
    Guid AnchorId,
    string DocumentHandle,
    Guid DocumentVersionId,
    Guid RepositoryId,
    string RepositoryRelativePath,
    int LineStart,
    int LineEnd,
    string SourceHash);

public sealed record EvidenceRelationship(
    string RelationshipHandle,
    Guid EdgeId,
    ChatRelationshipType RelationshipType,
    string SourceDocumentHandle,
    string TargetDocumentHandle,
    int Depth,
    Guid? ExactDecisionId,
    Guid? FlowGroupId,
    string? ProfileFingerprint)
{
    public string RelationshipIdentity { get; init; } = EdgeId.ToString("D", CultureInfo.InvariantCulture);

    public int ExactDecisionCount { get; init; }

    public string? ExactDecisionSetFingerprint { get; init; }

    public EvidenceRelationship(
        string relationshipHandle,
        Guid edgeId,
        ChatRelationshipType relationshipType,
        string sourceDocumentHandle,
        string targetDocumentHandle,
        int depth,
        Guid? exactDecisionId,
        int exactDecisionCount,
        string? exactDecisionSetFingerprint,
        Guid? flowGroupId,
        string? profileFingerprint)
        : this(
            relationshipHandle,
            edgeId,
            relationshipType,
            sourceDocumentHandle,
            targetDocumentHandle,
            depth,
            exactDecisionId,
            flowGroupId,
            profileFingerprint)
    {
        ExactDecisionCount = exactDecisionCount;
        ExactDecisionSetFingerprint = exactDecisionSetFingerprint;
    }

    public EvidenceRelationship(
        string relationshipHandle,
        string relationshipIdentity,
        ChatRelationshipType relationshipType,
        string sourceDocumentHandle,
        string targetDocumentHandle,
        string? profileFingerprint)
        : this(
            relationshipHandle,
            DeriveLegacyEdgeId(relationshipIdentity),
            relationshipType,
            sourceDocumentHandle,
            targetDocumentHandle,
            1,
            null,
            null,
            profileFingerprint)
    {
        RelationshipIdentity = relationshipIdentity;
    }

    private static Guid DeriveLegacyEdgeId(string relationshipIdentity)
    {
        string fingerprint = ChatContractGuard.Sha256(relationshipIdentity ?? string.Empty);
        return Guid.ParseExact(fingerprint[..32], "N");
    }
}

public sealed record EvidenceFlowStep(
    int StepOrder,
    string DocumentHandle,
    string? RelationshipHandle,
    string Description);

public sealed record EvidenceFlowStepAuthority(
    Guid FlowTraceStepId,
    int StepOrder,
    FlowTraceStepKind StepKind,
    string Description,
    Guid DocumentVersionId,
    Guid DocumentRepositoryId,
    Guid DocumentScanRunId,
    Guid? EdgeId,
    Guid? ExactResolutionDecisionId);

public sealed record EvidenceFlowGroupAuthority(
    Guid FlowGroupId,
    string SchemaIdentity,
    string State,
    Guid BusinessLogicDocumentVersionId,
    Guid? ExactResolutionDecisionId,
    Guid ExactResolutionEdgeId,
    string PlanFingerprint,
    string MembershipFingerprint,
    Guid FlowTraceId,
    int FlowTraceOrder,
    IReadOnlyList<EvidenceFlowStepAuthority> Steps);

public sealed record EvidencePacketSelection(
    string Policy,
    int CandidateWindow,
    int PrimarySectionTarget,
    IReadOnlyList<string> PrimaryDocumentHandles,
    IReadOnlyList<string> PrimarySectionHandles,
    IReadOnlyList<string> ExpansionSeedDocumentHandles);

public sealed record EvidencePacketFlowGroup(
    string Handle,
    Guid FlowGroupId,
    string State,
    string BusinessLogicDocumentHandle,
    string RelationshipHandle,
    Guid ExactDecisionId,
    Guid ExactEdgeId,
    int DecisionRank,
    string PlanFingerprint,
    string MembershipFingerprint,
    Guid FlowTraceId,
    int FlowTraceOrder);

public sealed record EvidencePacketFlowStep(
    string FlowGroupHandle,
    int Order,
    string DocumentHandle,
    string? RelationshipHandle,
    string Description);

public sealed record AuthorizedChatDocument(
    Guid LogicalDocumentId,
    Guid DocumentVersionId,
    Guid RepositoryId,
    Guid ScanRunId,
    DocumentType DocumentType,
    string Title,
    string ProfiledRevision);

public sealed record AuthorizedChatSection(
    Guid DocumentVersionId,
    Guid StableSectionId,
    int SectionOrder,
    DocumentSectionKind SectionKind,
    string Heading,
    string ContentMarkdown,
    IReadOnlyList<Guid> SourceAnchorIds,
    IReadOnlyList<Guid> SupportedDocumentVersionIds,
    string RepositoryRelativePath,
    string KeySymbolsJson,
    string BusinessTermsJson,
    string TechnicalTermsJson,
    string SearchText,
    string RepositoryName);

public sealed record AuthorizedChatSourceAnchor(
    Guid AnchorId,
    Guid DocumentVersionId,
    Guid RepositoryId,
    Guid ScanRunId,
    string ProfiledRevision,
    string RepositoryRelativePath,
    int LineStart,
    int LineEnd,
    string SourceHash);

public sealed record AuthorizedChatSnapshot
{
    private AuthorizedChatSnapshot(
        AuthorizedChatScope scope,
        ChatAdvisoryContextSnapshot advisoryContext,
        IReadOnlyList<AuthorizedChatDocument> documents,
        IReadOnlyList<AuthorizedChatSection> sections,
        IReadOnlyList<AuthorizedChatSourceAnchor> sourceAnchors,
        ChatConversationState conversation)
    {
        Scope = scope;
        AdvisoryContext = advisoryContext;
        Documents = documents;
        Sections = sections;
        SourceAnchors = sourceAnchors;
        Conversation = conversation;
    }

    public AuthorizedChatScope Scope { get; }
    public ChatAdvisoryContextSnapshot AdvisoryContext { get; }
    public IReadOnlyList<AuthorizedChatDocument> Documents { get; }
    public IReadOnlyList<AuthorizedChatSection> Sections { get; }
    public IReadOnlyList<AuthorizedChatSourceAnchor> SourceAnchors { get; }
    public ChatConversationState Conversation { get; }

    public static AuthorizedChatSnapshot Create(
        AuthorizedChatScope scope,
        ChatAdvisoryContextSnapshot advisoryContext,
        IReadOnlyCollection<AuthorizedChatDocument> documents,
        IReadOnlyCollection<AuthorizedChatSection> sections,
        IReadOnlyCollection<AuthorizedChatSourceAnchor> sourceAnchors,
        ChatConversationState conversation)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(advisoryContext);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(sections);
        ArgumentNullException.ThrowIfNull(sourceAnchors);
        ArgumentNullException.ThrowIfNull(conversation);

        Guid[] repositoryIds = scope.RepositoryIds.Order().ToArray();
        AuthorizedChatDocument[] documentSnapshot = documents.ToArray();
        AuthorizedChatSection[] sectionSnapshot = sections.Select(section => section with
        {
            SourceAnchorIds = Array.AsReadOnly(section.SourceAnchorIds.ToArray()),
            SupportedDocumentVersionIds =
                Array.AsReadOnly(section.SupportedDocumentVersionIds.ToArray()),
        }).ToArray();
        AuthorizedChatSourceAnchor[] anchorSnapshot = sourceAnchors.ToArray();
        Dictionary<Guid, AuthorizedChatDocument> documentsById = documentSnapshot
            .GroupBy(document => document.DocumentVersionId)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single());
        if (scope.SystemId == Guid.Empty ||
            repositoryIds.Length != 2 ||
            repositoryIds.Any(id => id == Guid.Empty || !scope.AccessScope.Contains(id)) ||
            repositoryIds[0] == repositoryIds[1] ||
            !conversation.Scope.Equals(ChatConversationScope.Create(
                conversation.Scope.Mode,
                scope.SystemId,
                repositoryIds)) ||
            advisoryContext.EncodedUtf8Bytes is < 0 or > 8_192 ||
            !ChatContractGuard.IsSha256(advisoryContext.AuthorityFingerprint) ||
            documentSnapshot.Length < 1 ||
            documentsById.Count != documentSnapshot.Length ||
            documentSnapshot.Any(document =>
                document.LogicalDocumentId == Guid.Empty ||
                document.DocumentVersionId == Guid.Empty ||
                !repositoryIds.Contains(document.RepositoryId)) ||
            sectionSnapshot.Length < 1 ||
            sectionSnapshot.Select(section => section.StableSectionId).Distinct().Count() !=
                sectionSnapshot.Length ||
            sectionSnapshot.Any(section =>
                section.StableSectionId == Guid.Empty ||
                !documentsById.ContainsKey(section.DocumentVersionId) ||
                section.SectionOrder < 0 ||
                section.SectionKind is not (DocumentSectionKind.Overview or
                    DocumentSectionKind.BusinessRule or
                    DocumentSectionKind.ProcessStep or
                    DocumentSectionKind.TechnicalDetail or
                    DocumentSectionKind.Evidence)) ||
            anchorSnapshot.Select(anchor => anchor.AnchorId).Distinct().Count() !=
                anchorSnapshot.Length ||
            anchorSnapshot.Any(anchor =>
                anchor.AnchorId == Guid.Empty ||
                !documentsById.ContainsKey(anchor.DocumentVersionId)))
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidCurrentness);
        }

        return new AuthorizedChatSnapshot(
            new AuthorizedChatScope(
                scope.SystemId,
                Array.AsReadOnly(repositoryIds),
                scope.AccessScope),
            advisoryContext,
            Array.AsReadOnly(documentSnapshot),
            Array.AsReadOnly(sectionSnapshot),
            Array.AsReadOnly(anchorSnapshot),
            conversation);
    }
}

public sealed record ChatLexicalPrimaryProjection
{
    private ChatLexicalPrimaryProjection(
        IReadOnlyList<EvidenceDocument> primaryDocuments,
        IReadOnlyList<EvidenceSection> primarySections,
        IReadOnlyList<EvidenceAnchor> primarySourceAnchors,
        IReadOnlyList<Guid> expansionSeedDocumentVersionIds,
        string primaryBindingFingerprint)
    {
        PrimaryDocuments = primaryDocuments;
        PrimarySections = primarySections;
        PrimarySourceAnchors = primarySourceAnchors;
        ExpansionSeedDocumentVersionIds = expansionSeedDocumentVersionIds;
        PrimaryBindingFingerprint = primaryBindingFingerprint;
    }

    public IReadOnlyList<EvidenceDocument> PrimaryDocuments { get; }
    public IReadOnlyList<EvidenceSection> PrimarySections { get; }
    public IReadOnlyList<EvidenceAnchor> PrimarySourceAnchors { get; }
    public IReadOnlyList<Guid> ExpansionSeedDocumentVersionIds { get; }
    public string PrimaryBindingFingerprint { get; }

    public static ChatLexicalPrimaryProjection Create(
        IReadOnlyCollection<EvidenceDocument> primaryDocuments,
        IReadOnlyCollection<EvidenceSection> primarySections,
        IReadOnlyCollection<EvidenceAnchor> primarySourceAnchors,
        string primaryBindingFingerprint)
    {
        ArgumentNullException.ThrowIfNull(primaryDocuments);
        ArgumentNullException.ThrowIfNull(primarySections);
        ArgumentNullException.ThrowIfNull(primarySourceAnchors);

        EvidenceDocument[] documents = primaryDocuments.ToArray();
        EvidenceSection[] sections = primarySections.ToArray();
        EvidenceAnchor[] anchors = primarySourceAnchors.ToArray();
        HashSet<string> documentHandles = documents
            .Select(document => document.DocumentHandle)
            .ToHashSet(StringComparer.Ordinal);
        if (documents.Length is < 1 or > 5 ||
            sections.Length is < 1 or > 12 ||
            sections.Any(section => !documentHandles.Contains(section.DocumentHandle)) ||
            sections.GroupBy(section => section.DocumentHandle, StringComparer.Ordinal)
                .Any(group => group.Count() > 4) ||
            !ChatContractGuard.IsSha256(primaryBindingFingerprint))
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidSelection);
        }

        Guid[] seeds = documents
            .Take(Math.Min(3, documents.Length))
            .Select(document => document.DocumentVersionId)
            .ToArray();
        return new ChatLexicalPrimaryProjection(
            Array.AsReadOnly(documents),
            Array.AsReadOnly(sections),
            Array.AsReadOnly(anchors),
            Array.AsReadOnly(seeds),
            primaryBindingFingerprint);
    }
}

public sealed record ChatRelationshipExpansionProjection
{
    private ChatRelationshipExpansionProjection(
        string primaryBindingFingerprint,
        IReadOnlyList<EvidenceDocument> addedDocuments,
        IReadOnlyList<EvidenceSection> supportSections,
        IReadOnlyList<EvidenceAnchor> supportSourceAnchors,
        IReadOnlyList<EvidenceRelationship> relationships,
        IReadOnlyList<EvidenceFlowStep> flowEvidence,
        IReadOnlyList<EvidenceFlowGroupAuthority> flowGroups)
    {
        PrimaryBindingFingerprint = primaryBindingFingerprint;
        AddedDocuments = addedDocuments;
        SupportSections = supportSections;
        SupportSourceAnchors = supportSourceAnchors;
        Relationships = relationships;
        FlowEvidence = flowEvidence;
        FlowGroups = flowGroups;
    }

    public string PrimaryBindingFingerprint { get; }
    public IReadOnlyList<EvidenceDocument> AddedDocuments { get; }
    public IReadOnlyList<EvidenceSection> SupportSections { get; }
    public IReadOnlyList<EvidenceAnchor> SupportSourceAnchors { get; }
    public IReadOnlyList<EvidenceRelationship> Relationships { get; }
    public IReadOnlyList<EvidenceFlowStep> FlowEvidence { get; }
    public IReadOnlyList<EvidenceFlowGroupAuthority> FlowGroups { get; }

    public static ChatRelationshipExpansionProjection Create(
        ChatLexicalPrimaryProjection primary,
        IReadOnlyCollection<EvidenceDocument> addedDocuments,
        IReadOnlyCollection<EvidenceSection> supportSections,
        IReadOnlyCollection<EvidenceAnchor> supportSourceAnchors,
        IReadOnlyCollection<EvidenceRelationship> relationships,
        IReadOnlyCollection<EvidenceFlowStep> flowEvidence,
        IReadOnlyCollection<EvidenceFlowGroupAuthority> flowGroups)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(addedDocuments);
        ArgumentNullException.ThrowIfNull(supportSections);
        ArgumentNullException.ThrowIfNull(supportSourceAnchors);
        ArgumentNullException.ThrowIfNull(relationships);
        ArgumentNullException.ThrowIfNull(flowEvidence);
        ArgumentNullException.ThrowIfNull(flowGroups);

        EvidenceDocument[] documents = addedDocuments.ToArray();
        EvidenceRelationship[] relationshipSnapshot = relationships.ToArray();
        EvidenceFlowStep[] flowSnapshot = flowEvidence.ToArray();
        EvidenceFlowGroupAuthority[] groupSnapshot = flowGroups.ToArray();
        if (primary.PrimaryDocuments.Count + documents.Length > 5 ||
            relationshipSnapshot.Length > 32 ||
            flowSnapshot.Length > 12 ||
            groupSnapshot.Length > 10)
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidSelection);
        }

        return new ChatRelationshipExpansionProjection(
            primary.PrimaryBindingFingerprint,
            Array.AsReadOnly(documents),
            Array.AsReadOnly(supportSections.ToArray()),
            Array.AsReadOnly(supportSourceAnchors.ToArray()),
            Array.AsReadOnly(relationshipSnapshot),
            Array.AsReadOnly(flowSnapshot),
            Array.AsReadOnly(groupSnapshot));
    }
}

public sealed record ChatEvidenceAssembly(ChatEvidencePacketBuildRequest BuildRequest);

public sealed record ChatEvidencePacketBuildRequest
{
    private ChatEvidencePacketBuildRequest(
        string requestId,
        ChatMode mode,
        Guid systemId,
        IReadOnlyList<Guid> repositoryIds,
        IReadOnlyList<EvidenceDocument> documents,
        IReadOnlyList<EvidenceSection> sections,
        IReadOnlyList<EvidenceAnchor> sourceAnchors,
        IReadOnlyList<EvidenceRelationship> relationships,
        IReadOnlyList<EvidenceFlowStep> flowEvidence,
        IReadOnlyList<EvidenceFlowGroupAuthority> flowGroups,
        string advisoryContext,
        string advisoryContextAuthorityFingerprint,
        ChatConversationState conversation,
        IReadOnlyList<string> optionalSectionHandles,
        IReadOnlyList<string> primaryDocumentHandles,
        IReadOnlyList<string> primarySectionHandles,
        IReadOnlyList<string> expansionSeedDocumentHandles,
        IReadOnlyDictionary<string, int> optionalRelationshipUnitRanks,
        IReadOnlyDictionary<string, int> lexicalExtraSectionRanks)
    {
        RequestId = requestId;
        Mode = mode;
        SystemId = systemId;
        RepositoryIds = repositoryIds;
        Documents = documents;
        Sections = sections;
        SourceAnchors = sourceAnchors;
        Relationships = relationships;
        FlowEvidence = flowEvidence;
        FlowGroups = flowGroups;
        AdvisoryContext = advisoryContext;
        AdvisoryContextAuthorityFingerprint = advisoryContextAuthorityFingerprint;
        Conversation = conversation;
        OptionalSectionHandles = optionalSectionHandles;
        PrimaryDocumentHandles = primaryDocumentHandles;
        PrimarySectionHandles = primarySectionHandles;
        ExpansionSeedDocumentHandles = expansionSeedDocumentHandles;
        OptionalRelationshipUnitRanks = optionalRelationshipUnitRanks;
        LexicalExtraSectionRanks = lexicalExtraSectionRanks;
    }

    public string RequestId { get; }
    public ChatMode Mode { get; }
    public Guid SystemId { get; }
    public IReadOnlyList<Guid> RepositoryIds { get; }
    public IReadOnlyList<EvidenceDocument> Documents { get; }
    public IReadOnlyList<EvidenceSection> Sections { get; }
    public IReadOnlyList<EvidenceAnchor> SourceAnchors { get; }
    public IReadOnlyList<EvidenceRelationship> Relationships { get; }
    public IReadOnlyList<EvidenceFlowStep> FlowEvidence { get; }
    public IReadOnlyList<EvidenceFlowGroupAuthority> FlowGroups { get; }
    public string AdvisoryContext { get; }
    public string AdvisoryContextAuthorityFingerprint { get; }
    public ChatConversationState Conversation { get; }
    public IReadOnlyList<string> OptionalSectionHandles { get; }
    public IReadOnlyList<string> PrimaryDocumentHandles { get; }
    public IReadOnlyList<string> PrimarySectionHandles { get; }
    public IReadOnlyList<string> ExpansionSeedDocumentHandles { get; }
    public IReadOnlyDictionary<string, int> OptionalRelationshipUnitRanks { get; }
    public IReadOnlyDictionary<string, int> LexicalExtraSectionRanks { get; }

    public static ChatEvidencePacketBuildRequest Create(
        string requestId,
        ChatMode mode,
        Guid systemId,
        IReadOnlyCollection<Guid> repositoryIds,
        IReadOnlyCollection<EvidenceDocument> documents,
        IReadOnlyCollection<EvidenceSection> sections,
        IReadOnlyCollection<EvidenceAnchor> sourceAnchors,
        IReadOnlyCollection<EvidenceRelationship> relationships,
        IReadOnlyCollection<EvidenceFlowStep> flowEvidence,
        IReadOnlyCollection<EvidenceFlowGroupAuthority> flowGroups,
        string advisoryContext,
        ChatConversationState conversation,
        IReadOnlyCollection<string> optionalSectionHandles,
        string? advisoryContextAuthorityFingerprint = null,
        IReadOnlyCollection<string>? primaryDocumentHandles = null,
        IReadOnlyCollection<string>? primarySectionHandles = null,
        IReadOnlyCollection<string>? expansionSeedDocumentHandles = null,
        IReadOnlyDictionary<string, int>? optionalRelationshipUnitRanks = null,
        IReadOnlyDictionary<string, int>? lexicalExtraSectionRanks = null)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        string normalizedAdvisory = advisoryContext?.Normalize(NormalizationForm.FormC) ?? string.Empty;
        Guid[] canonicalRepositories = ChatContractGuard.CanonicalRepositoryIds(repositoryIds);
        ChatConversationScope expectedScope = ChatConversationScope.Create(
            mode,
            systemId,
            canonicalRepositories);
        EvidenceSection[] sectionSnapshot = sections?.ToArray() ?? [];
        EvidenceDocument[] documentSnapshot = documents?.ToArray() ?? [];
        string[] canonicalOptionalHandles = optionalSectionHandles?
            .Select(handle => handle?.Trim() ?? string.Empty)
            .Order(StringComparer.Ordinal)
            .ToArray() ?? [];
        string[] canonicalPrimarySections = (primarySectionHandles ?? sectionSnapshot
                .Take(12)
                .Select(section => section.SectionHandle))
            .Select(handle => handle?.Trim() ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        HashSet<string> primarySectionSet = canonicalPrimarySections.ToHashSet(StringComparer.Ordinal);
        var strongestPrimarySectionHandles = new HashSet<string>(StringComparer.Ordinal);
        var primaryDocumentSections = new HashSet<string>(StringComparer.Ordinal);
        foreach (string handle in canonicalPrimarySections)
        {
            EvidenceSection? section = sectionSnapshot.FirstOrDefault(candidate => string.Equals(
                candidate.SectionHandle,
                handle,
                StringComparison.Ordinal));
            if (section is not null && primaryDocumentSections.Add(section.DocumentHandle))
            {
                strongestPrimarySectionHandles.Add(handle);
            }
        }
        string[] inferredPrimaryDocuments = sectionSnapshot
            .Where(section => primarySectionSet.Contains(section.SectionHandle))
            .Select(section => section.DocumentHandle)
            .Distinct(StringComparer.Ordinal)
            .Take(5)
            .ToArray();
        string[] canonicalPrimaryDocuments = (primaryDocumentHandles ?? inferredPrimaryDocuments)
            .Select(handle => handle?.Trim() ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] canonicalSeeds = (expansionSeedDocumentHandles ?? canonicalPrimaryDocuments.Take(3))
            .Select(handle => handle?.Trim() ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string authorityFingerprint = advisoryContextAuthorityFingerprint ?? new string('0', 64);
        HashSet<string> documentHandleSet = documentSnapshot
            .Select(document => document.DocumentHandle)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> sectionHandleSet = sectionSnapshot
            .Select(section => section.SectionHandle)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> relationshipHandleSet = relationships?
            .Select(relationship => relationship.RelationshipHandle)
            .ToHashSet(StringComparer.Ordinal) ?? [];
        Dictionary<string, int> relationshipRankSnapshot = (optionalRelationshipUnitRanks ??
                new Dictionary<string, int>(StringComparer.Ordinal))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        Dictionary<string, int> lexicalRankSnapshot = (lexicalExtraSectionRanks ??
                new Dictionary<string, int>(StringComparer.Ordinal))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(requestId) ||
            !Enum.IsDefined(mode) ||
            systemId == Guid.Empty ||
            Encoding.UTF8.GetByteCount(normalizedAdvisory) > 8_192 ||
            conversation.EncodedUtf8Bytes > 4_096 ||
            !conversation.Scope.Equals(expectedScope) ||
            !ChatContractGuard.IsSha256(authorityFingerprint) ||
            canonicalPrimaryDocuments.Length is < 1 or > 5 ||
            canonicalPrimarySections.Length is < 1 or > 12 ||
            canonicalSeeds.Length is < 1 or > 3 ||
            canonicalPrimaryDocuments.Any(handle => !documentHandleSet.Contains(handle)) ||
            canonicalPrimarySections.Any(handle => !sectionHandleSet.Contains(handle)) ||
            canonicalSeeds.Any(handle => !canonicalPrimaryDocuments.Contains(handle, StringComparer.Ordinal)) ||
            canonicalOptionalHandles.Any(handle => !ChatContractGuard.IsHandle(handle)) ||
            canonicalOptionalHandles.Distinct(StringComparer.Ordinal).Count() != canonicalOptionalHandles.Length ||
            canonicalOptionalHandles.Any(handle =>
                sectionSnapshot.All(section => !string.Equals(
                    section.SectionHandle,
                    handle,
                    StringComparison.Ordinal))) ||
            relationshipRankSnapshot.Any(item =>
                item.Value < 1 ||
                !ChatContractGuard.IsHandle(item.Key) ||
                !relationshipHandleSet.Contains(item.Key)) ||
            lexicalRankSnapshot.Any(item =>
                item.Value < 1 ||
                !ChatContractGuard.IsHandle(item.Key) ||
                !canonicalOptionalHandles.Contains(item.Key, StringComparer.Ordinal) ||
                !primarySectionSet.Contains(item.Key) ||
                strongestPrimarySectionHandles.Contains(item.Key)))
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidEvidence);
        }

        return new ChatEvidencePacketBuildRequest(
            requestId.Trim(),
            mode,
            systemId,
            Array.AsReadOnly(canonicalRepositories),
            Array.AsReadOnly(documentSnapshot),
            Array.AsReadOnly(sectionSnapshot),
            Array.AsReadOnly(sourceAnchors?.ToArray() ?? []),
            Array.AsReadOnly(relationships?.ToArray() ?? []),
            Array.AsReadOnly(flowEvidence?.ToArray() ?? []),
            Array.AsReadOnly(flowGroups?.ToArray() ?? []),
            normalizedAdvisory,
            authorityFingerprint,
            conversation,
            Array.AsReadOnly(canonicalOptionalHandles),
            Array.AsReadOnly(canonicalPrimaryDocuments),
            Array.AsReadOnly(canonicalPrimarySections),
            Array.AsReadOnly(canonicalSeeds),
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(relationshipRankSnapshot),
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(lexicalRankSnapshot));
    }
}

[JsonConverter(typeof(EvidencePacketJsonConverter))]
public sealed record EvidencePacket
{
    private const int _maxEvidenceUtf8Bytes = 65_536;

    public const string SupportedSchemaVersion = "grounded-chat-evidence/2.2";

    private EvidencePacket(
        string requestId,
        ChatMode mode,
        Guid systemId,
        IReadOnlyList<Guid> repositoryIds,
        IReadOnlyList<EvidenceDocument> documents,
        IReadOnlyList<EvidenceSection> sections,
        IReadOnlyList<EvidenceAnchor> sourceAnchors,
        IReadOnlyList<EvidenceRelationship> relationships,
        IReadOnlyList<EvidenceFlowStep> flowEvidence,
        IReadOnlyList<EvidenceFlowGroupAuthority> flowGroups,
        string advisoryContext,
        string advisoryContextAuthorityFingerprint,
        ChatConversationState conversation,
        EvidencePacketSelection selection,
        IReadOnlyList<EvidencePacketFlowGroup> packetFlowGroups,
        IReadOnlyList<EvidencePacketFlowStep> packetFlowSteps,
        string fingerprint)
    {
        SchemaVersion = SupportedSchemaVersion;
        RequestId = requestId;
        Mode = mode;
        SystemId = systemId;
        RepositoryIds = repositoryIds;
        Documents = documents;
        Sections = sections;
        SourceAnchors = sourceAnchors;
        Relationships = relationships;
        FlowEvidence = flowEvidence;
        FlowGroups = flowGroups;
        AdvisoryContext = advisoryContext;
        AdvisoryContextAuthorityFingerprint = advisoryContextAuthorityFingerprint;
        Conversation = conversation;
        Selection = selection;
        PacketFlowGroups = packetFlowGroups;
        PacketFlowSteps = packetFlowSteps;
        Fingerprint = fingerprint;
    }

    public string SchemaVersion { get; }
    public string RequestId { get; }
    public ChatMode Mode { get; }
    public Guid SystemId { get; }
    public IReadOnlyList<Guid> RepositoryIds { get; }
    public IReadOnlyList<EvidenceDocument> Documents { get; }
    public IReadOnlyList<EvidenceSection> Sections { get; }
    public IReadOnlyList<EvidenceAnchor> SourceAnchors { get; }
    public IReadOnlyList<EvidenceRelationship> Relationships { get; }
    public IReadOnlyList<EvidenceFlowStep> FlowEvidence { get; }
    public IReadOnlyList<EvidenceFlowGroupAuthority> FlowGroups { get; }
    public string AdvisoryContext { get; }
    public string AdvisoryContextAuthorityFingerprint { get; }
    public ChatConversationState Conversation { get; }
    public EvidencePacketSelection Selection { get; }
    public IReadOnlyList<EvidencePacketFlowGroup> PacketFlowGroups { get; }
    public IReadOnlyList<EvidencePacketFlowStep> PacketFlowSteps { get; }
    public string Fingerprint { get; }

    public static EvidencePacket Create(
        string requestId,
        ChatMode mode,
        Guid systemId,
        IReadOnlyCollection<Guid> repositoryIds,
        IReadOnlyCollection<EvidenceDocument> documents,
        IReadOnlyCollection<EvidenceSection> sections,
        IReadOnlyCollection<EvidenceAnchor> sourceAnchors,
        IReadOnlyCollection<EvidenceRelationship> relationships,
        IReadOnlyCollection<EvidenceFlowStep> flowEvidence,
        IReadOnlyCollection<EvidenceFlowGroupAuthority>? flowGroups = null,
        string advisoryContext = "",
        ChatConversationState? conversation = null,
        string? advisoryContextAuthorityFingerprint = null,
        IReadOnlyCollection<string>? primaryDocumentHandles = null,
        IReadOnlyCollection<string>? primarySectionHandles = null,
        IReadOnlyCollection<string>? expansionSeedDocumentHandles = null)
    {
        Guid[] canonicalRepositories = ChatContractGuard.CanonicalRepositoryIds(repositoryIds);
        EvidenceDocument[] canonicalDocuments = documents?.OrderBy(
            document => document.DocumentHandle,
            StringComparer.Ordinal).ToArray() ?? [];
        EvidenceSection[] canonicalSections = sections?.OrderBy(
            section => section.SectionHandle,
            StringComparer.Ordinal).ToArray() ?? [];
        EvidenceAnchor[] canonicalAnchors = sourceAnchors?.OrderBy(
            anchor => anchor.AnchorHandle,
            StringComparer.Ordinal).ToArray() ?? [];
        EvidenceRelationship[] canonicalRelationships = relationships?.OrderBy(
            relationship => relationship.RelationshipHandle,
            StringComparer.Ordinal).ToArray() ?? [];
        EvidenceFlowStep[] canonicalFlow = flowEvidence?.OrderBy(step => step.StepOrder).ToArray() ?? [];
        EvidenceFlowGroupAuthority[] canonicalFlowGroups = flowGroups?
            .OrderBy(group => group.ExactResolutionEdgeId)
            .ThenBy(group => group.ExactResolutionDecisionId)
            .ThenBy(group => group.FlowTraceOrder)
            .ThenBy(group => group.FlowGroupId)
            .Select(group => group with
            {
                Steps = Array.AsReadOnly((group.Steps ?? [])
                    .OrderBy(step => step.StepOrder)
                    .ThenBy(step => step.FlowTraceStepId)
                    .ToArray()),
            })
            .ToArray() ?? [];
        string normalizedAdvisory = advisoryContext?.Normalize(NormalizationForm.FormC) ?? string.Empty;
        string authorityFingerprint = advisoryContextAuthorityFingerprint ?? new string('0', 64);
        ChatConversationState canonicalConversation = conversation ?? ChatConversationState.Empty(
            ChatConversationScope.Create(mode, systemId, canonicalRepositories));
        ChatConversationScope expectedConversationScope = ChatConversationScope.Create(
            mode,
            systemId,
            canonicalRepositories);
        if (string.IsNullOrWhiteSpace(requestId) ||
            !Enum.IsDefined(mode) ||
            systemId == Guid.Empty ||
            canonicalDocuments.Length is < 1 or > 5 ||
            canonicalSections.Length is < 1 or > 20 ||
            canonicalAnchors.Length is < 1 or > 32 ||
            canonicalRelationships.Length > 32 ||
            canonicalFlow.Length > 12 ||
            canonicalFlowGroups.Length > 10 ||
            Encoding.UTF8.GetByteCount(normalizedAdvisory) > 8_192 ||
            canonicalConversation.EncodedUtf8Bytes > 4_096 ||
            !ChatContractGuard.IsSha256(authorityFingerprint) ||
            !canonicalConversation.Scope.Equals(expectedConversationScope))
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidEvidence);
        }

        ValidatePacketItems(
            canonicalRepositories,
            canonicalDocuments,
            canonicalSections,
            canonicalAnchors,
            canonicalRelationships,
            canonicalFlow);
        ValidateFlowGroups(canonicalRepositories, canonicalDocuments, canonicalFlowGroups);
        EvidencePacketSelection selection = CreateSelection(
            canonicalDocuments,
            canonicalSections,
            primaryDocumentHandles,
            primarySectionHandles,
            expansionSeedDocumentHandles);
        (EvidencePacketFlowGroup[] packetFlowGroups, EvidencePacketFlowStep[] packetFlowSteps) =
            ProjectFlowBindings(
                canonicalDocuments,
                canonicalRelationships,
                canonicalFlow,
                canonicalFlowGroups);
        Dictionary<Guid, string[]> flowGroupHandlesByEdge = packetFlowGroups
            .GroupBy(group => group.ExactEdgeId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Handle).ToArray());
        string canonicalJson = JsonSerializer.Serialize(new
        {
            schemaVersion = SupportedSchemaVersion,
            requestId,
            mode = mode.ToString(),
            systemId,
            repositoryIds = canonicalRepositories,
            selection = new
            {
                policy = selection.Policy,
                candidateWindow = selection.CandidateWindow,
                primarySectionTarget = selection.PrimarySectionTarget,
                primaryDocumentHandles = selection.PrimaryDocumentHandles,
                primarySectionHandles = selection.PrimarySectionHandles,
                expansionSeedDocumentHandles = selection.ExpansionSeedDocumentHandles,
            },
            documents = canonicalDocuments.Select(document => new
            {
                document.DocumentHandle,
                document.LogicalDocumentId,
                document.DocumentVersionId,
                document.RepositoryId,
                document.DocumentType,
                document.Title,
                document.ProfiledRevision,
            }),
            sections = canonicalSections.Select(section => new
            {
                section.SectionHandle,
                section.DocumentHandle,
                section.StableSectionId,
                section.SectionOrder,
                section.SectionKind,
                section.Heading,
                section.ContentMarkdown,
                anchorHandles = section.AnchorHandles.Order(StringComparer.Ordinal),
                supportingDocumentHandles = section.SupportingDocumentHandles.Order(StringComparer.Ordinal),
            }),
            sourceAnchors = canonicalAnchors,
            relationships = canonicalRelationships.Select(relationship => new
            {
                handle = relationship.RelationshipHandle,
                relationship.EdgeId,
                type = relationship.RelationshipType == ChatRelationshipType.References
                    ? "references"
                    : "generatedFrom",
                relationship.SourceDocumentHandle,
                relationship.TargetDocumentHandle,
                relationship.Depth,
                selectedExactDecisionId = relationship.ExactDecisionId,
                relationship.ExactDecisionCount,
                relationship.ExactDecisionSetFingerprint,
                flowGroupHandles = flowGroupHandlesByEdge.GetValueOrDefault(relationship.EdgeId, []),
            }),
            flowGroups = packetFlowGroups,
            flowSteps = packetFlowSteps.Select(step => new
            {
                step.FlowGroupHandle,
                step.Order,
                step.DocumentHandle,
                step.RelationshipHandle,
                step.Description,
            }),
            advisoryContext = normalizedAdvisory,
            advisoryContextAuthorityFingerprint = authorityFingerprint,
            conversation = EvidencePacketJsonConverter.ProjectConversation(canonicalConversation),
        });
        if (Encoding.UTF8.GetByteCount(canonicalJson) > _maxEvidenceUtf8Bytes)
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidEvidence);
        }

        return new EvidencePacket(
            requestId.Trim(),
            mode,
            systemId,
            Array.AsReadOnly(canonicalRepositories),
            Array.AsReadOnly(canonicalDocuments),
            Array.AsReadOnly(canonicalSections),
            Array.AsReadOnly(canonicalAnchors),
            Array.AsReadOnly(canonicalRelationships),
            Array.AsReadOnly(canonicalFlow),
            Array.AsReadOnly(canonicalFlowGroups),
            normalizedAdvisory,
            authorityFingerprint,
            canonicalConversation,
            selection,
            Array.AsReadOnly(packetFlowGroups),
            Array.AsReadOnly(packetFlowSteps),
            ChatContractGuard.Sha256(canonicalJson));
    }

    private static EvidencePacketSelection CreateSelection(
        IReadOnlyList<EvidenceDocument> documents,
        IReadOnlyList<EvidenceSection> sections,
        IReadOnlyCollection<string>? primaryDocumentHandles,
        IReadOnlyCollection<string>? primarySectionHandles,
        IReadOnlyCollection<string>? expansionSeedDocumentHandles)
    {
        string[] primarySections = (primarySectionHandles ?? sections.Take(12)
                .Select(section => section.SectionHandle))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        HashSet<string> primarySectionSet = primarySections.ToHashSet(StringComparer.Ordinal);
        string[] inferredDocuments = sections
            .Where(section => primarySectionSet.Contains(section.SectionHandle))
            .Select(section => section.DocumentHandle)
            .Distinct(StringComparer.Ordinal)
            .Take(5)
            .ToArray();
        string[] primaryDocuments = (primaryDocumentHandles ?? inferredDocuments)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] seeds = (expansionSeedDocumentHandles ?? primaryDocuments.Take(3))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        HashSet<string> documentHandles = documents.Select(item => item.DocumentHandle)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> sectionHandles = sections.Select(item => item.SectionHandle)
            .ToHashSet(StringComparer.Ordinal);
        if (primaryDocuments.Length is < 1 or > 5 ||
            primarySections.Length is < 1 or > 12 ||
            seeds.Length is < 1 or > 3 ||
            primaryDocuments.Any(handle => !documentHandles.Contains(handle)) ||
            primarySections.Any(handle => !sectionHandles.Contains(handle)) ||
            seeds.Any(handle => !primaryDocuments.Contains(handle, StringComparer.Ordinal)))
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidEvidence);
        }

        return new EvidencePacketSelection(
            "lexical-current-v2/1.0",
            256,
            12,
            Array.AsReadOnly(primaryDocuments),
            Array.AsReadOnly(primarySections),
            Array.AsReadOnly(seeds));
    }

    private static (EvidencePacketFlowGroup[] Groups, EvidencePacketFlowStep[] Steps)
        ProjectFlowBindings(
            IReadOnlyList<EvidenceDocument> documents,
            IReadOnlyList<EvidenceRelationship> relationships,
            IReadOnlyList<EvidenceFlowStep> flowSteps,
            IReadOnlyList<EvidenceFlowGroupAuthority> flowGroups)
    {
        Dictionary<Guid, string> documentHandles = documents.ToDictionary(
            document => document.DocumentVersionId,
            document => document.DocumentHandle);
        Dictionary<Guid, EvidenceRelationship> relationshipsByEdge = relationships.ToDictionary(
            relationship => relationship.EdgeId);
        var decisionRanks = new Dictionary<(Guid EdgeId, Guid DecisionId), int>();
        var nextRanks = new Dictionary<Guid, int>();
        EvidencePacketFlowGroup[] groups = flowGroups.Select((group, index) =>
        {
            if (!relationshipsByEdge.TryGetValue(group.ExactResolutionEdgeId, out EvidenceRelationship? relationship) ||
                group.ExactResolutionDecisionId is not Guid decisionId ||
                !documentHandles.TryGetValue(group.BusinessLogicDocumentVersionId, out string? documentHandle))
            {
                throw new ChatContractException(ChatContractFailureCode.InvalidEvidence);
            }

            var key = (group.ExactResolutionEdgeId, decisionId);
            if (!decisionRanks.TryGetValue(key, out int decisionRank))
            {
                decisionRank = nextRanks.GetValueOrDefault(group.ExactResolutionEdgeId) + 1;
                nextRanks[group.ExactResolutionEdgeId] = decisionRank;
                decisionRanks[key] = decisionRank;
            }

            return new EvidencePacketFlowGroup(
                $"fg-{index + 1}",
                group.FlowGroupId,
                group.State,
                documentHandle,
                relationship.RelationshipHandle,
                decisionId,
                group.ExactResolutionEdgeId,
                decisionRank,
                group.PlanFingerprint,
                group.MembershipFingerprint,
                group.FlowTraceId,
                group.FlowTraceOrder);
        }).ToArray();

        var unused = flowGroups.SelectMany((group, groupIndex) => group.Steps.Select(step =>
            (GroupIndex: groupIndex, Step: step))).ToList();
        EvidencePacketFlowStep[] steps = flowSteps.Select(step =>
        {
            int matchIndex = unused.FindIndex(item =>
                documentHandles.GetValueOrDefault(item.Step.DocumentVersionId) == step.DocumentHandle &&
                string.Equals(item.Step.Description, step.Description, StringComparison.Ordinal) &&
                (step.RelationshipHandle is null ||
                    groups[item.GroupIndex].RelationshipHandle == step.RelationshipHandle));
            if (matchIndex < 0)
            {
                throw new ChatContractException(ChatContractFailureCode.InvalidEvidence);
            }

            int groupIndex = unused[matchIndex].GroupIndex;
            unused.RemoveAt(matchIndex);
            return new EvidencePacketFlowStep(
                groups[groupIndex].Handle,
                step.StepOrder,
                step.DocumentHandle,
                step.RelationshipHandle,
                step.Description);
        }).ToArray();
        return (groups, steps);
    }

    private static void ValidateFlowGroups(
        IReadOnlyCollection<Guid> repositoryIds,
        IReadOnlyCollection<EvidenceDocument> documents,
        IReadOnlyCollection<EvidenceFlowGroupAuthority> flowGroups)
    {
        HashSet<Guid> documentVersionIds = documents
            .Select(document => document.DocumentVersionId)
            .ToHashSet();
        if (flowGroups.Select(group => group.FlowGroupId).Distinct().Count() != flowGroups.Count ||
            flowGroups.Sum(group => group.Steps.Count) > 12 ||
            flowGroups.Any(group =>
                group.FlowGroupId == Guid.Empty ||
                !string.Equals(group.SchemaIdentity, "endpoint-flow-group/1.0", StringComparison.Ordinal) ||
                !string.Equals(group.State, "Published", StringComparison.Ordinal) ||
                group.BusinessLogicDocumentVersionId == Guid.Empty ||
                !documentVersionIds.Contains(group.BusinessLogicDocumentVersionId) ||
                group.ExactResolutionDecisionId == Guid.Empty ||
                group.ExactResolutionEdgeId == Guid.Empty ||
                !ChatContractGuard.IsSha256(group.PlanFingerprint) ||
                !ChatContractGuard.IsSha256(group.MembershipFingerprint) ||
                group.FlowTraceId == Guid.Empty ||
                group.FlowTraceOrder < 1 ||
                group.Steps.Count < 1 ||
                group.Steps.Select(step => step.FlowTraceStepId).Distinct().Count() != group.Steps.Count ||
                !group.Steps.Select(step => step.StepOrder).SequenceEqual(
                    Enumerable.Range(1, group.Steps.Count)) ||
                group.Steps.Any(step =>
                    step.FlowTraceStepId == Guid.Empty ||
                    !Enum.IsDefined(step.StepKind) ||
                    string.IsNullOrWhiteSpace(step.Description) ||
                    step.DocumentVersionId == Guid.Empty ||
                    !documentVersionIds.Contains(step.DocumentVersionId) ||
                    !repositoryIds.Contains(step.DocumentRepositoryId) ||
                    step.DocumentScanRunId == Guid.Empty)))
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidEvidence);
        }
    }

    private static void ValidatePacketItems(
        IReadOnlyCollection<Guid> repositoryIds,
        IReadOnlyList<EvidenceDocument> documents,
        IReadOnlyList<EvidenceSection> sections,
        IReadOnlyList<EvidenceAnchor> anchors,
        IReadOnlyList<EvidenceRelationship> relationships,
        IReadOnlyList<EvidenceFlowStep> flow)
    {
        if (documents.Select(document => document.DocumentHandle).Distinct(StringComparer.Ordinal).Count() != documents.Count ||
            sections.Select(section => section.SectionHandle).Distinct(StringComparer.Ordinal).Count() != sections.Count ||
            anchors.Select(anchor => anchor.AnchorHandle).Distinct(StringComparer.Ordinal).Count() != anchors.Count ||
            relationships.Select(relationship => relationship.RelationshipHandle).Distinct(StringComparer.Ordinal).Count() != relationships.Count ||
            relationships.Select(relationship => relationship.EdgeId).Distinct().Count() != relationships.Count ||
            relationships.Select(relationship => relationship.TargetDocumentHandle).Distinct(StringComparer.Ordinal).Count() != relationships.Count)
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidEvidence);
        }

        Dictionary<string, EvidenceDocument> documentMap = documents.ToDictionary(
            document => document.DocumentHandle,
            StringComparer.Ordinal);
        HashSet<string> anchorHandles = anchors.Select(anchor => anchor.AnchorHandle).ToHashSet(StringComparer.Ordinal);
        HashSet<string> relationshipHandles = relationships.Select(relationship => relationship.RelationshipHandle).ToHashSet(StringComparer.Ordinal);
        if (documents.Any(document =>
                !ChatContractGuard.IsHandle(document.DocumentHandle) ||
                document.LogicalDocumentId == Guid.Empty ||
                document.DocumentVersionId == Guid.Empty ||
                !repositoryIds.Contains(document.RepositoryId) ||
                !Enum.IsDefined(document.DocumentType) ||
                string.IsNullOrWhiteSpace(document.Title) ||
                string.IsNullOrWhiteSpace(document.ProfiledRevision)) ||
            sections.Any(section =>
                !ChatContractGuard.IsHandle(section.SectionHandle) ||
                !documentMap.ContainsKey(section.DocumentHandle) ||
                section.StableSectionId == Guid.Empty ||
                section.SectionOrder < 1 ||
                !Enum.IsDefined(section.SectionKind) ||
                string.IsNullOrWhiteSpace(section.ContentMarkdown) ||
                section.AnchorHandles.Count < 1 ||
                section.AnchorHandles.Any(handle => !anchorHandles.Contains(handle)) ||
                section.SupportingDocumentHandles.Any(handle => !documentMap.ContainsKey(handle))) ||
            anchors.Any(anchor =>
                !ChatContractGuard.IsHandle(anchor.AnchorHandle) ||
                anchor.AnchorId == Guid.Empty ||
                !documentMap.TryGetValue(anchor.DocumentHandle, out EvidenceDocument? document) ||
                anchor.DocumentVersionId != document.DocumentVersionId ||
                anchor.RepositoryId != document.RepositoryId ||
                !ChatContractGuard.IsSafeRepositoryRelativePath(anchor.RepositoryRelativePath) ||
                anchor.LineStart < 1 ||
                anchor.LineEnd < anchor.LineStart ||
                !ChatContractGuard.IsSha256(anchor.SourceHash)) ||
            relationships.Any(relationship =>
                !ChatContractGuard.IsHandle(relationship.RelationshipHandle) ||
                relationship.EdgeId == Guid.Empty ||
                string.IsNullOrWhiteSpace(relationship.RelationshipIdentity) ||
                !Enum.IsDefined(relationship.RelationshipType) ||
                !documentMap.ContainsKey(relationship.SourceDocumentHandle) ||
                !documentMap.ContainsKey(relationship.TargetDocumentHandle) ||
                relationship.SourceDocumentHandle == relationship.TargetDocumentHandle ||
                relationship.Depth is < 0 or > 1 ||
                relationship.ExactDecisionId == Guid.Empty ||
                relationship.ExactDecisionCount < 0 ||
                relationship.ExactDecisionSetFingerprint is not null &&
                !ChatContractGuard.IsSha256(relationship.ExactDecisionSetFingerprint) ||
                relationship.FlowGroupId == Guid.Empty ||
                relationship.ProfileFingerprint is not null &&
                !ChatContractGuard.IsSha256(relationship.ProfileFingerprint)) ||
            flow.Select((step, index) => (step, index)).Any(item =>
                item.step.StepOrder != item.index + 1 ||
                !documentMap.ContainsKey(item.step.DocumentHandle) ||
                item.step.RelationshipHandle is not null &&
                !relationshipHandles.Contains(item.step.RelationshipHandle) ||
                string.IsNullOrWhiteSpace(item.step.Description)))
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidEvidence);
        }
    }
}

public sealed class EvidencePacketJsonConverter : JsonConverter<EvidencePacket>
{
    public override EvidencePacket? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        throw new NotSupportedException();

    public override void Write(
        Utf8JsonWriter writer,
        EvidencePacket value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("schemaVersion", value.SchemaVersion);
        writer.WriteString("requestId", value.RequestId);
        writer.WriteString("mode", value.Mode == ChatMode.Business ? "business" : "it");
        writer.WriteString("systemId", value.SystemId);
        WriteValue(writer, "repositoryIds", value.RepositoryIds, options);
        WriteValue(writer, "selection", new
        {
            policy = value.Selection.Policy,
            candidateWindow = value.Selection.CandidateWindow,
            primarySectionTarget = value.Selection.PrimarySectionTarget,
            primaryDocumentHandles = value.Selection.PrimaryDocumentHandles,
            primarySectionHandles = value.Selection.PrimarySectionHandles,
            expansionSeedDocumentHandles = value.Selection.ExpansionSeedDocumentHandles,
        }, options);
        WriteValue(writer, "documents", value.Documents.Select(document => new
        {
            handle = document.DocumentHandle,
            logicalDocumentId = document.LogicalDocumentId,
            documentVersionId = document.DocumentVersionId,
            repositoryId = document.RepositoryId,
            documentType = document.DocumentType == DocumentType.CodeLogic
                ? "codeLogic"
                : "businessLogic",
            profiledRevision = document.ProfiledRevision,
            title = document.Title,
        }), options);
        WriteValue(writer, "sections", value.Sections.Select(section => new
        {
            handle = section.SectionHandle,
            documentHandle = section.DocumentHandle,
            stableSectionId = section.StableSectionId,
            order = section.SectionOrder,
            kind = section.SectionKind.ToString(),
            heading = section.Heading,
            contentMarkdown = section.ContentMarkdown,
            anchorHandles = section.AnchorHandles,
            supportingDocumentHandles = section.SupportingDocumentHandles,
        }), options);
        WriteValue(writer, "sourceAnchors", value.SourceAnchors.Select(anchor => new
        {
            handle = anchor.AnchorHandle,
            anchorId = anchor.AnchorId,
            documentHandle = anchor.DocumentHandle,
            repositoryId = anchor.RepositoryId,
            repositoryRelativePath = anchor.RepositoryRelativePath,
            lineStart = anchor.LineStart,
            lineEnd = anchor.LineEnd,
            sourceHash = anchor.SourceHash,
        }), options);
        Dictionary<Guid, string[]> flowGroupHandlesByEdge = value.PacketFlowGroups
            .GroupBy(group => group.ExactEdgeId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Handle).ToArray());
        WriteValue(writer, "relationships", value.Relationships.Select(relationship => new
        {
            handle = relationship.RelationshipHandle,
            edgeId = relationship.EdgeId,
            type = relationship.RelationshipType == ChatRelationshipType.References
                ? "references"
                : "generatedFrom",
            sourceDocumentHandle = relationship.SourceDocumentHandle,
            targetDocumentHandle = relationship.TargetDocumentHandle,
            depth = relationship.Depth,
            selectedExactDecisionId = relationship.ExactDecisionId,
            exactDecisionCount = relationship.ExactDecisionCount,
            exactDecisionSetFingerprint = relationship.ExactDecisionSetFingerprint,
            flowGroupHandles = flowGroupHandlesByEdge.GetValueOrDefault(relationship.EdgeId, []),
        }), options);
        WriteValue(writer, "flowGroups", value.PacketFlowGroups.Select(group => new
        {
            handle = group.Handle,
            flowGroupId = group.FlowGroupId,
            state = group.State,
            businessLogicDocumentHandle = group.BusinessLogicDocumentHandle,
            relationshipHandle = group.RelationshipHandle,
            exactDecisionId = group.ExactDecisionId,
            exactEdgeId = group.ExactEdgeId,
            decisionRank = group.DecisionRank,
            planFingerprint = group.PlanFingerprint,
            membershipFingerprint = group.MembershipFingerprint,
            flowTraceId = group.FlowTraceId,
            flowTraceOrder = group.FlowTraceOrder,
        }), options);
        WriteValue(writer, "flowSteps", value.PacketFlowSteps.Select(step => new
        {
            flowGroupHandle = step.FlowGroupHandle,
            order = step.Order,
            documentHandle = step.DocumentHandle,
            relationshipHandle = step.RelationshipHandle,
            description = step.Description,
        }), options);
        writer.WriteString("advisoryContext", value.AdvisoryContext);
        writer.WriteString(
            "advisoryContextAuthorityFingerprint",
            value.AdvisoryContextAuthorityFingerprint);
        WriteValue(writer, "conversation", ProjectConversation(value.Conversation), options);
        writer.WriteString("fingerprint", value.Fingerprint);
        writer.WriteEndObject();
    }

    internal static object ProjectConversation(ChatConversationState conversation) => new
    {
        turns = conversation.Turns.Select(turn => new
        {
            question = turn.Question,
            validatedAnswer = turn.AnswerText,
            packetFingerprint = turn.PacketFingerprint,
            priorCitationHandles = turn.CitationHandles,
            priorLogicalDocumentIds = turn.LogicalDocumentIds,
        }),
    };

    private static void WriteValue<T>(
        Utf8JsonWriter writer,
        string propertyName,
        T value,
        JsonSerializerOptions options)
    {
        writer.WritePropertyName(propertyName);
        JsonSerializer.Serialize(writer, value, options);
    }
}

public sealed record CandidateClaim
{
    private CandidateClaim(
        string candidateHandle,
        string evidenceFingerprint,
        ChatMode mode,
        Guid documentVersionId,
        Guid stableSectionId,
        int spanStart,
        string text,
        string textSha256,
        IReadOnlyList<string> anchorHandles,
        IReadOnlyList<string> relationshipHandles,
        bool allowsBusinessNarrative,
        string supportSignature)
    {
        CandidateHandle = candidateHandle;
        EvidenceFingerprint = evidenceFingerprint;
        Mode = mode;
        DocumentVersionId = documentVersionId;
        StableSectionId = stableSectionId;
        SpanStart = spanStart;
        SpanLength = text.Length;
        Text = text;
        TextSha256 = textSha256;
        AnchorHandles = anchorHandles;
        RelationshipHandles = relationshipHandles;
        AllowsBusinessNarrative = allowsBusinessNarrative;
        SupportSignature = supportSignature;
    }

    public string CandidateHandle { get; }
    public string EvidenceFingerprint { get; }
    public ChatMode Mode { get; }
    public Guid DocumentVersionId { get; }
    public Guid StableSectionId { get; }
    public int SpanStart { get; }
    public int SpanLength { get; }
    public string Text { get; }
    public string TextSha256 { get; }
    public IReadOnlyList<string> AnchorHandles { get; }
    public IReadOnlyList<string> RelationshipHandles { get; }
    public bool AllowsBusinessNarrative { get; }
    public string SupportSignature { get; }

    public static CandidateClaim Create(
        string candidateHandle,
        string evidenceFingerprint,
        ChatMode mode,
        Guid documentVersionId,
        Guid stableSectionId,
        int spanStart,
        string text,
        IReadOnlyCollection<string> anchorHandles,
        IReadOnlyCollection<string> relationshipHandles,
        bool allowsBusinessNarrative = false)
    {
        string[] canonicalAnchors = ChatContractGuard.CanonicalHandles(
            anchorHandles,
            1,
            4,
            ChatContractFailureCode.InvalidEvidence);
        string[] canonicalRelationships = ChatContractGuard.CanonicalHandles(
            relationshipHandles,
            0,
            12,
            ChatContractFailureCode.InvalidEvidence);
        if (!ChatContractGuard.IsHandle(candidateHandle) ||
            !ChatContractGuard.IsSha256(evidenceFingerprint) ||
            !Enum.IsDefined(mode) ||
            documentVersionId == Guid.Empty ||
            stableSectionId == Guid.Empty ||
            spanStart < 0 ||
            string.IsNullOrWhiteSpace(text) ||
            text.Length > 2_000)
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidEvidence);
        }

        if (allowsBusinessNarrative && mode != ChatMode.Business)
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidEvidence);
        }

        string textSha256 = ChatContractGuard.Sha256(text);
        string supportMaterial = string.Join(
            '\u001f',
            mode.ToString(),
            evidenceFingerprint,
            documentVersionId.ToString("D", CultureInfo.InvariantCulture),
            stableSectionId.ToString("D", CultureInfo.InvariantCulture),
            spanStart.ToString(CultureInfo.InvariantCulture),
            text.Length.ToString(CultureInfo.InvariantCulture),
            textSha256,
            string.Join(',', canonicalAnchors),
            string.Join(',', canonicalRelationships),
            allowsBusinessNarrative.ToString(CultureInfo.InvariantCulture));
        return new CandidateClaim(
            candidateHandle,
            evidenceFingerprint,
            mode,
            documentVersionId,
            stableSectionId,
            spanStart,
            text,
            textSha256,
            Array.AsReadOnly(canonicalAnchors),
            Array.AsReadOnly(canonicalRelationships),
            allowsBusinessNarrative,
            ChatContractGuard.Sha256(supportMaterial));
    }
}

public sealed record ChatSynthesisSelection(
    string CandidateHandle,
    int Order,
    string? NarrativeText = null);

public sealed record ChatSynthesisRequest
{
    private ChatSynthesisRequest(
        string requestId,
        ChatMode mode,
        Guid systemId,
        string question,
        string evidenceFingerprint,
        IReadOnlyList<CandidateClaim> candidateClaims,
        long absoluteDeadlineUnixTimeMilliseconds)
    {
        RequestId = requestId;
        Mode = mode;
        SystemId = systemId;
        Question = question;
        EvidenceFingerprint = evidenceFingerprint;
        CandidateClaims = candidateClaims;
        AbsoluteDeadlineUnixTimeMilliseconds = absoluteDeadlineUnixTimeMilliseconds;
    }

    public string RequestId { get; }
    public ChatMode Mode { get; }
    public Guid SystemId { get; }
    public string Question { get; }
    public string EvidenceFingerprint { get; }
    public IReadOnlyList<CandidateClaim> CandidateClaims { get; }
    public long AbsoluteDeadlineUnixTimeMilliseconds { get; }

    public static ChatSynthesisRequest Create(
        string requestId,
        ChatMode mode,
        Guid systemId,
        string question,
        string evidenceFingerprint,
        IReadOnlyCollection<CandidateClaim> candidateClaims,
        long absoluteDeadlineUnixTimeMilliseconds = long.MaxValue)
    {
        string normalizedQuestion = question?.Normalize(NormalizationForm.FormC).Trim() ?? string.Empty;
        CandidateClaim[] canonicalClaims = candidateClaims?.OrderBy(
            claim => claim.CandidateHandle,
            StringComparer.Ordinal).ToArray() ?? [];
        if (string.IsNullOrWhiteSpace(requestId) ||
            !Enum.IsDefined(mode) ||
            systemId == Guid.Empty ||
            normalizedQuestion.EnumerateRunes().Count() is < 1 or > 2_000 ||
            !ChatContractGuard.IsSha256(evidenceFingerprint) ||
            absoluteDeadlineUnixTimeMilliseconds <= 0 ||
            canonicalClaims.Length < 1 ||
            canonicalClaims.Select(claim => claim.CandidateHandle).Distinct(StringComparer.Ordinal).Count() != canonicalClaims.Length ||
            canonicalClaims.Any(claim =>
                claim.Mode != mode ||
                !string.Equals(claim.EvidenceFingerprint, evidenceFingerprint, StringComparison.Ordinal)))
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidSelection);
        }

        return new ChatSynthesisRequest(
            requestId.Trim(),
            mode,
            systemId,
            normalizedQuestion,
            evidenceFingerprint,
            Array.AsReadOnly(canonicalClaims),
            absoluteDeadlineUnixTimeMilliseconds);
    }
}

public enum ChatAnswerability
{
    Supported,
    Unsupported,
}

public sealed record ChatSynthesisResult
{
    public const string SupportedSchemaVersion = "prepared-chat-answer-v1";

    public const string CompatibleProviderEchoSchemaVersion =
        "prepared-chat-selection-request-v1";

    private ChatSynthesisResult(
        string requestId,
        ChatMode mode,
        string evidenceFingerprint,
        ChatAnswerability answerability,
        IReadOnlyList<ChatSynthesisSelection> selections)
    {
        SchemaVersion = SupportedSchemaVersion;
        RequestId = requestId;
        Mode = mode;
        EvidenceFingerprint = evidenceFingerprint;
        Answerability = answerability;
        Selections = selections;
    }

    public string SchemaVersion { get; }
    public string RequestId { get; }
    public ChatMode Mode { get; }
    public string EvidenceFingerprint { get; }
    public ChatAnswerability Answerability { get; }
    public IReadOnlyList<ChatSynthesisSelection> Selections { get; }

    public static ChatSynthesisResult Create(
        string requestId,
        ChatMode mode,
        string evidenceFingerprint,
        IReadOnlyCollection<ChatSynthesisSelection> selections)
        => Create(
            requestId,
            mode,
            evidenceFingerprint,
            ChatAnswerability.Supported,
            selections);

    public static ChatSynthesisResult Create(
        string requestId,
        ChatMode mode,
        string evidenceFingerprint,
        ChatAnswerability answerability,
        IReadOnlyCollection<ChatSynthesisSelection> selections)
    {
        ChatSynthesisSelection[] values = selections?.ToArray() ?? [];
        if (string.IsNullOrWhiteSpace(requestId) ||
            !Enum.IsDefined(mode) ||
            !Enum.IsDefined(answerability) ||
            !ChatContractGuard.IsSha256(evidenceFingerprint) ||
            (answerability == ChatAnswerability.Supported && values.Length is < 1 or > 12) ||
            (answerability == ChatAnswerability.Unsupported && values.Length != 0) ||
            values.Select(selection => selection.CandidateHandle).Distinct(StringComparer.Ordinal).Count() != values.Length ||
            values.Select((selection, index) => (selection, index)).Any(item =>
                !ChatContractGuard.IsHandle(item.selection.CandidateHandle) ||
                item.selection.Order != item.index + 1))
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidSelection);
        }

        return new ChatSynthesisResult(
            requestId.Trim(),
            mode,
            evidenceFingerprint,
            answerability,
            Array.AsReadOnly(values));
    }
}

public sealed record ChatSynthesisOutcome
{
    private const int _maxRawSelectionJsonLength = 65_536;

    private ChatSynthesisOutcome(
        ChatSynthesisResult? result,
        ChatContractFailureCode? failureCode,
        string? rawSelectionJson)
    {
        Result = result;
        FailureCode = failureCode;
        RawSelectionJson = rawSelectionJson;
    }

    public ChatSynthesisResult? Result { get; }

    public ChatContractFailureCode? FailureCode { get; }

    public string? RawSelectionJson { get; }

    public static ChatSynthesisOutcome Generated(
        ChatSynthesisResult result,
        string rawSelectionJson)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (string.IsNullOrWhiteSpace(rawSelectionJson) ||
            rawSelectionJson.Length > _maxRawSelectionJsonLength)
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidSelection);
        }

        return new ChatSynthesisOutcome(result, null, rawSelectionJson);
    }

    public static ChatSynthesisOutcome Failed(ChatContractFailureCode failureCode)
    {
        if (failureCode is not (
            ChatContractFailureCode.InvalidSelection or
            ChatContractFailureCode.ProviderFailure or
            ChatContractFailureCode.TimedOut or
            ChatContractFailureCode.Cancelled))
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidSelection);
        }

        return new ChatSynthesisOutcome(null, failureCode, null);
    }
}

public sealed record MaterialClaim
{
    private MaterialClaim(
        string claimId,
        int order,
        string text,
        IReadOnlyList<string> citationIds)
    {
        ClaimId = claimId;
        Order = order;
        Text = text;
        CitationIds = citationIds;
    }

    public string ClaimId { get; }
    public int Order { get; }
    public string Text { get; }
    public IReadOnlyList<string> CitationIds { get; }

    public static MaterialClaim Create(
        string claimId,
        int order,
        string text,
        IReadOnlyCollection<string> citationIds)
    {
        string[] canonicalCitations = ChatContractGuard.CanonicalHandles(
            citationIds,
            1,
            4,
            ChatContractFailureCode.InvalidCitation);
        if (!ChatContractGuard.IsHandle(claimId) ||
            order < 1 ||
            string.IsNullOrWhiteSpace(text) ||
            text.Length > 2_000)
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidCitation);
        }

        return new MaterialClaim(
            claimId,
            order,
            text,
            Array.AsReadOnly(canonicalCitations));
    }
}

public sealed record DocumentCitation
{
    private DocumentCitation(
        string citationId,
        IReadOnlyList<string> claimIds,
        string documentTitle,
        DocumentType documentType,
        string repositoryDisplayName,
        string repositoryRelativePath,
        int lineStart,
        int lineEnd,
        string? sectionHeading)
    {
        CitationId = citationId;
        ClaimIds = claimIds;
        DocumentTitle = documentTitle;
        DocumentType = documentType;
        RepositoryDisplayName = repositoryDisplayName;
        RepositoryRelativePath = repositoryRelativePath;
        LineStart = lineStart;
        LineEnd = lineEnd;
        SectionHeading = sectionHeading;
    }

    public string CitationId { get; }
    public IReadOnlyList<string> ClaimIds { get; }
    public string DocumentTitle { get; }
    public DocumentType DocumentType { get; }
    public string RepositoryDisplayName { get; }
    public string RepositoryRelativePath { get; }
    public int LineStart { get; }
    public int LineEnd { get; }
    public string? SectionHeading { get; }

    public static DocumentCitation Create(
        string citationId,
        IReadOnlyCollection<string> claimIds,
        string documentTitle,
        DocumentType documentType,
        string repositoryDisplayName,
        string repositoryRelativePath,
        int lineStart,
        int lineEnd,
        string? sectionHeading = null)
    {
        string[] canonicalClaims = ChatContractGuard.CanonicalHandles(
            claimIds,
            1,
            12,
            ChatContractFailureCode.InvalidCitation);
        if (!ChatContractGuard.IsHandle(citationId) ||
            string.IsNullOrWhiteSpace(documentTitle) ||
            !Enum.IsDefined(documentType) ||
            string.IsNullOrWhiteSpace(repositoryDisplayName) ||
            !ChatContractGuard.IsSafeRepositoryRelativePath(repositoryRelativePath) ||
            lineStart < 1 ||
            lineEnd < lineStart ||
            sectionHeading is { Length: > 512 })
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidCitation);
        }

        return new DocumentCitation(
            citationId,
            Array.AsReadOnly(canonicalClaims),
            documentTitle,
            documentType,
            repositoryDisplayName,
            repositoryRelativePath.Replace('\\', '/'),
            lineStart,
            lineEnd,
            sectionHeading);
    }
}

public sealed record GroundedAnswer
{
    private GroundedAnswer(
        ChatMode mode,
        IReadOnlyList<MaterialClaim> materialClaims,
        IReadOnlyList<DocumentCitation> documentCitations,
        DateTimeOffset generatedAt)
    {
        Mode = mode;
        MaterialClaims = materialClaims;
        DocumentCitations = documentCitations;
        GeneratedAt = generatedAt;
    }

    public ChatMode Mode { get; }
    public IReadOnlyList<MaterialClaim> MaterialClaims { get; }
    public IReadOnlyList<DocumentCitation> DocumentCitations { get; }
    public DateTimeOffset GeneratedAt { get; }

    public static GroundedAnswer Create(
        ChatMode mode,
        IReadOnlyCollection<MaterialClaim> materialClaims,
        IReadOnlyCollection<DocumentCitation> documentCitations,
        DateTimeOffset generatedAt)
    {
        MaterialClaim[] claims = materialClaims?.OrderBy(claim => claim.Order).ToArray() ?? [];
        DocumentCitation[] citations = documentCitations?.OrderBy(
            citation => citation.CitationId,
            StringComparer.Ordinal).ToArray() ?? [];
        Dictionary<string, MaterialClaim> claimMap = claims.ToDictionary(claim => claim.ClaimId, StringComparer.Ordinal);
        Dictionary<string, DocumentCitation> citationMap = citations.ToDictionary(citation => citation.CitationId, StringComparer.Ordinal);
        if (!Enum.IsDefined(mode) ||
            generatedAt == default ||
            claims.Length is < 1 or > 12 ||
            citations.Length is < 1 or > 48 ||
            claims.Select((claim, index) => (claim, index)).Any(item => item.claim.Order != item.index + 1) ||
            claims.Any(claim => claim.CitationIds.Any(citationId =>
                !citationMap.TryGetValue(citationId, out DocumentCitation? citation) ||
                !citation.ClaimIds.Contains(claim.ClaimId, StringComparer.Ordinal))) ||
            citations.Any(citation => citation.ClaimIds.Any(claimId =>
                !claimMap.TryGetValue(claimId, out MaterialClaim? claim) ||
                !claim.CitationIds.Contains(citation.CitationId, StringComparer.Ordinal))))
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidCitation);
        }

        return new GroundedAnswer(
            mode,
            Array.AsReadOnly(claims),
            Array.AsReadOnly(citations),
            generatedAt);
    }
}

public enum ChatCurrentnessValidationOutcome
{
    Current,
    MissingOrStale,
}

public sealed record ChatCurrentDocument(
    Guid LogicalDocumentId,
    Guid ExpectedCurrentDocumentVersionId,
    string ExpectedTitle);

public sealed record ChatCurrentSection(
    Guid StableSectionId,
    Guid DocumentVersionId,
    int SpanStart,
    int SpanLength,
    string TextSha256);

public sealed record ChatCurrentSourceAnchor(
    Guid AnchorId,
    Guid DocumentVersionId,
    Guid RepositoryId,
    string RepositoryRelativePath,
    int LineStart,
    int LineEnd,
    string SourceHash);

public sealed record ChatCurrentRelationship(
    Guid EdgeId,
    ChatRelationshipType RelationshipType,
    Guid SourceDocumentVersionId,
    Guid TargetDocumentVersionId,
    Guid SourceRepositoryId,
    Guid TargetRepositoryId,
    int Depth,
    Guid? ExactDecisionId,
    Guid? FlowGroupId,
    string? ProfileFingerprint)
{
    public int ExactDecisionCount { get; init; }

    public string? ExactDecisionSetFingerprint { get; init; }

    public ChatCurrentRelationship(
        Guid edgeId,
        ChatRelationshipType relationshipType,
        Guid sourceDocumentVersionId,
        Guid targetDocumentVersionId,
        Guid sourceRepositoryId,
        Guid targetRepositoryId,
        int depth,
        Guid? exactDecisionId,
        int exactDecisionCount,
        string? exactDecisionSetFingerprint,
        Guid? flowGroupId,
        string? profileFingerprint)
        : this(
            edgeId,
            relationshipType,
            sourceDocumentVersionId,
            targetDocumentVersionId,
            sourceRepositoryId,
            targetRepositoryId,
            depth,
            exactDecisionId,
            flowGroupId,
            profileFingerprint)
    {
        ExactDecisionCount = exactDecisionCount;
        ExactDecisionSetFingerprint = exactDecisionSetFingerprint;
    }
}

public sealed record ChatCurrentnessValidationRequest
{
    private ChatCurrentnessValidationRequest(
        string requestId,
        string evidenceFingerprint,
        Guid systemId,
        IReadOnlyList<Guid> repositoryIds,
        IReadOnlyList<ChatCurrentDocument> documents,
        IReadOnlyList<ChatCurrentSection> sections,
        IReadOnlyList<ChatCurrentSourceAnchor> sourceAnchors,
        IReadOnlyList<ChatCurrentRelationship> relationships,
        IReadOnlyList<EvidenceFlowGroupAuthority> flowGroups)
    {
        RequestId = requestId;
        EvidenceFingerprint = evidenceFingerprint;
        SystemId = systemId;
        RepositoryIds = repositoryIds;
        Documents = documents;
        Sections = sections;
        SourceAnchors = sourceAnchors;
        Relationships = relationships;
        FlowGroups = flowGroups;
    }

    public string RequestId { get; }
    public string EvidenceFingerprint { get; }
    public Guid SystemId { get; }
    public IReadOnlyList<Guid> RepositoryIds { get; }
    public IReadOnlyList<ChatCurrentDocument> Documents { get; }
    public IReadOnlyList<ChatCurrentSection> Sections { get; }
    public IReadOnlyList<ChatCurrentSourceAnchor> SourceAnchors { get; }
    public IReadOnlyList<ChatCurrentRelationship> Relationships { get; }
    public IReadOnlyList<EvidenceFlowGroupAuthority> FlowGroups { get; }

    public static ChatCurrentnessValidationRequest Create(
        string requestId,
        string evidenceFingerprint,
        Guid systemId,
        IReadOnlyCollection<Guid> repositoryIds,
        IReadOnlyCollection<ChatCurrentDocument> documents,
        IReadOnlyCollection<ChatCurrentSection> sections,
        IReadOnlyCollection<ChatCurrentSourceAnchor> sourceAnchors,
        IReadOnlyCollection<ChatCurrentRelationship> relationships,
        IReadOnlyCollection<EvidenceFlowGroupAuthority>? flowGroups = null)
    {
        Guid[] canonicalRepositories;
        try
        {
            canonicalRepositories = ChatContractGuard.CanonicalRepositoryIds(repositoryIds);
        }
        catch (ChatContractException)
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidCurrentness);
        }

        ChatCurrentDocument[] canonicalDocuments = documents?.OrderBy(document => document.LogicalDocumentId).ToArray() ?? [];
        ChatCurrentSection[] canonicalSections = sections?
            .OrderBy(section => section.StableSectionId)
            .ThenBy(section => section.DocumentVersionId)
            .ThenBy(section => section.SpanStart)
            .ThenBy(section => section.SpanLength)
            .ThenBy(section => section.TextSha256, StringComparer.Ordinal)
            .ToArray() ?? [];
        ChatCurrentSourceAnchor[] canonicalAnchors = sourceAnchors?.OrderBy(anchor => anchor.AnchorId).ToArray() ?? [];
        ChatCurrentRelationship[] canonicalRelationships = relationships?
            .OrderBy(relationship => relationship.EdgeId)
            .ToArray() ?? [];
        EvidenceFlowGroupAuthority[] canonicalFlowGroups = flowGroups?
            .OrderBy(group => group.FlowGroupId)
            .Select(group => group with
            {
                Steps = Array.AsReadOnly((group.Steps ?? [])
                    .OrderBy(step => step.StepOrder)
                    .ThenBy(step => step.FlowTraceStepId)
                    .ToArray()),
            })
            .ToArray() ?? [];

        if (string.IsNullOrWhiteSpace(requestId) ||
            !ChatContractGuard.IsSha256(evidenceFingerprint) ||
            systemId == Guid.Empty ||
            canonicalDocuments.Length is < 1 or > 5 ||
            canonicalSections.Length is < 1 or > 20 ||
            canonicalAnchors.Length is < 1 or > 32 ||
            canonicalRelationships.Length > 32 ||
            canonicalFlowGroups.Length > 10 ||
            canonicalDocuments.Select(document => document.LogicalDocumentId).Distinct().Count() != canonicalDocuments.Length ||
            canonicalSections
                .Select(section => (
                    section.StableSectionId,
                    section.DocumentVersionId,
                    section.SpanStart,
                    section.SpanLength,
                    section.TextSha256))
                .Distinct()
                .Count() != canonicalSections.Length ||
            canonicalSections
                .GroupBy(section => section.StableSectionId)
                .Any(group => group.Select(section => section.DocumentVersionId).Distinct().Count() != 1) ||
            canonicalAnchors.Select(anchor => anchor.AnchorId).Distinct().Count() != canonicalAnchors.Length ||
            canonicalRelationships.Select(relationship => relationship.EdgeId).Distinct().Count() != canonicalRelationships.Length ||
            canonicalDocuments.Any(document =>
                document.LogicalDocumentId == Guid.Empty ||
                document.ExpectedCurrentDocumentVersionId == Guid.Empty ||
                string.IsNullOrWhiteSpace(document.ExpectedTitle)) ||
            canonicalSections.Any(section =>
                section.StableSectionId == Guid.Empty ||
                section.DocumentVersionId == Guid.Empty ||
                section.SpanStart < 0 ||
                section.SpanLength < 1 ||
                !ChatContractGuard.IsSha256(section.TextSha256)) ||
            canonicalAnchors.Any(anchor =>
                anchor.AnchorId == Guid.Empty ||
                anchor.DocumentVersionId == Guid.Empty ||
                !canonicalRepositories.Contains(anchor.RepositoryId) ||
                !ChatContractGuard.IsSafeRepositoryRelativePath(anchor.RepositoryRelativePath) ||
                anchor.LineStart < 1 ||
                anchor.LineEnd < anchor.LineStart ||
                !ChatContractGuard.IsSha256(anchor.SourceHash)) ||
            canonicalRelationships.Any(relationship =>
                relationship.EdgeId == Guid.Empty ||
                !Enum.IsDefined(relationship.RelationshipType) ||
                relationship.SourceDocumentVersionId == Guid.Empty ||
                relationship.TargetDocumentVersionId == Guid.Empty ||
                relationship.SourceDocumentVersionId == relationship.TargetDocumentVersionId ||
                !canonicalRepositories.Contains(relationship.SourceRepositoryId) ||
                !canonicalRepositories.Contains(relationship.TargetRepositoryId) ||
                relationship.Depth is < 0 or > 1 ||
                relationship.RelationshipType == ChatRelationshipType.References &&
                    relationship.SourceRepositoryId != relationship.TargetRepositoryId &&
                    (relationship.ExactDecisionId is null ||
                     relationship.ExactDecisionCount < 0 ||
                     relationship.ExactDecisionCount == 0 &&
                         relationship.ExactDecisionSetFingerprint is not null ||
                     relationship.ExactDecisionCount > 0 &&
                         !ChatContractGuard.IsSha256(relationship.ExactDecisionSetFingerprint!)) ||
                relationship.SourceRepositoryId == relationship.TargetRepositoryId &&
                    (relationship.ExactDecisionId is not null ||
                     relationship.ExactDecisionCount != 0 ||
                     relationship.ExactDecisionSetFingerprint is not null) ||
                relationship.RelationshipType == ChatRelationshipType.GeneratedFrom &&
                    (relationship.ExactDecisionId is not null ||
                     relationship.ExactDecisionCount != 0 ||
                     relationship.ExactDecisionSetFingerprint is not null) ||
                relationship.FlowGroupId is Guid flowGroupId &&
                    !canonicalFlowGroups.Any(group =>
                        group.FlowGroupId == flowGroupId &&
                        group.ExactResolutionEdgeId == relationship.EdgeId &&
                        group.ExactResolutionDecisionId == relationship.ExactDecisionId) ||
                relationship.ProfileFingerprint is not null &&
                !ChatContractGuard.IsSha256(relationship.ProfileFingerprint)) ||
            canonicalFlowGroups.Select(group => group.FlowGroupId).Distinct().Count() != canonicalFlowGroups.Length ||
            canonicalFlowGroups.Sum(group => group.Steps.Count) > 12 ||
            canonicalFlowGroups.Any(group =>
                group.FlowGroupId == Guid.Empty ||
                !string.Equals(group.SchemaIdentity, "endpoint-flow-group/1.0", StringComparison.Ordinal) ||
                !string.Equals(group.State, "Published", StringComparison.Ordinal) ||
                group.BusinessLogicDocumentVersionId == Guid.Empty ||
                !canonicalDocuments.Any(document =>
                    document.ExpectedCurrentDocumentVersionId == group.BusinessLogicDocumentVersionId) ||
                group.ExactResolutionDecisionId == Guid.Empty ||
                group.ExactResolutionEdgeId == Guid.Empty ||
                !ChatContractGuard.IsSha256(group.PlanFingerprint) ||
                !ChatContractGuard.IsSha256(group.MembershipFingerprint) ||
                group.FlowTraceId == Guid.Empty ||
                group.FlowTraceOrder < 1 ||
                group.Steps.Count < 1 ||
                group.Steps.Select(step => step.FlowTraceStepId).Distinct().Count() != group.Steps.Count ||
                !group.Steps.Select(step => step.StepOrder).SequenceEqual(
                    Enumerable.Range(1, group.Steps.Count)) ||
                group.Steps.Any(step =>
                    step.FlowTraceStepId == Guid.Empty ||
                    !Enum.IsDefined(step.StepKind) ||
                    string.IsNullOrWhiteSpace(step.Description) ||
                    step.DocumentVersionId == Guid.Empty ||
                    !canonicalDocuments.Any(document =>
                        document.ExpectedCurrentDocumentVersionId == step.DocumentVersionId) ||
                    !canonicalRepositories.Contains(step.DocumentRepositoryId) ||
                    step.DocumentScanRunId == Guid.Empty)))
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidCurrentness);
        }

        return new ChatCurrentnessValidationRequest(
            requestId.Trim(),
            evidenceFingerprint,
            systemId,
            Array.AsReadOnly(canonicalRepositories),
            Array.AsReadOnly(canonicalDocuments),
            Array.AsReadOnly(canonicalSections),
            Array.AsReadOnly(canonicalAnchors),
            Array.AsReadOnly(canonicalRelationships),
            Array.AsReadOnly(canonicalFlowGroups));
    }

}

internal static class ChatContractGuard
{
    internal static bool IsSha256(string value) =>
        value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    internal static bool IsHandle(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 64 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    internal static string Sha256(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    internal static string[] CanonicalHandles(
        IReadOnlyCollection<string> values,
        int minimum,
        int maximum,
        ChatContractFailureCode failureCode)
    {
        if (values is null)
        {
            throw new ChatContractException(failureCode);
        }

        string[] canonical = values.Order(StringComparer.Ordinal).ToArray();
        if (canonical.Length < minimum ||
            canonical.Length > maximum ||
            canonical.Any(value => !IsHandle(value)) ||
            canonical.Distinct(StringComparer.Ordinal).Count() != canonical.Length)
        {
            throw new ChatContractException(failureCode);
        }

        return canonical;
    }

    internal static Guid[] CanonicalRepositoryIds(IReadOnlyCollection<Guid> repositoryIds)
    {
        Guid[] canonical = repositoryIds?.Order().ToArray() ?? [];
        if (canonical.Length != 2 ||
            canonical.Any(id => id == Guid.Empty) ||
            canonical[0] == canonical[1])
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidRequest);
        }

        return canonical;
    }

    internal static bool IsSafeRepositoryRelativePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Replace('\\', '/');
        string[] segments = normalized.Split('/');
        return !normalized.StartsWith('/') &&
               !(normalized.Length >= 2 && char.IsAsciiLetter(normalized[0]) && normalized[1] == ':') &&
               !Uri.TryCreate(normalized, UriKind.Absolute, out _) &&
               segments.All(segment => segment.Length > 0 && segment is not "." and not "..");
    }
}
