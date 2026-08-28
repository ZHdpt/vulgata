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
        ChatConversationState conversation,
        IReadOnlyList<string> optionalSectionHandles)
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
        Conversation = conversation;
        OptionalSectionHandles = optionalSectionHandles;
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
    public ChatConversationState Conversation { get; }
    public IReadOnlyList<string> OptionalSectionHandles { get; }

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
        IReadOnlyCollection<string> optionalSectionHandles)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        string normalizedAdvisory = advisoryContext?.Normalize(NormalizationForm.FormC) ?? string.Empty;
        Guid[] canonicalRepositories = ChatContractGuard.CanonicalRepositoryIds(repositoryIds);
        ChatConversationScope expectedScope = ChatConversationScope.Create(
            mode,
            systemId,
            canonicalRepositories);
        EvidenceSection[] sectionSnapshot = sections?.ToArray() ?? [];
        string[] canonicalOptionalHandles = optionalSectionHandles?
            .Select(handle => handle?.Trim() ?? string.Empty)
            .Order(StringComparer.Ordinal)
            .ToArray() ?? [];
        if (string.IsNullOrWhiteSpace(requestId) ||
            !Enum.IsDefined(mode) ||
            systemId == Guid.Empty ||
            Encoding.UTF8.GetByteCount(normalizedAdvisory) > 8_192 ||
            conversation.EncodedUtf8Bytes > 4_096 ||
            !conversation.Scope.Equals(expectedScope) ||
            canonicalOptionalHandles.Any(handle => !ChatContractGuard.IsHandle(handle)) ||
            canonicalOptionalHandles.Distinct(StringComparer.Ordinal).Count() != canonicalOptionalHandles.Length ||
            canonicalOptionalHandles.Any(handle =>
                sectionSnapshot.All(section => !string.Equals(
                    section.SectionHandle,
                    handle,
                    StringComparison.Ordinal))))
        {
            throw new ChatContractException(ChatContractFailureCode.InvalidEvidence);
        }

        return new ChatEvidencePacketBuildRequest(
            requestId.Trim(),
            mode,
            systemId,
            Array.AsReadOnly(canonicalRepositories),
            Array.AsReadOnly(documents?.ToArray() ?? []),
            Array.AsReadOnly(sectionSnapshot),
            Array.AsReadOnly(sourceAnchors?.ToArray() ?? []),
            Array.AsReadOnly(relationships?.ToArray() ?? []),
            Array.AsReadOnly(flowEvidence?.ToArray() ?? []),
            Array.AsReadOnly(flowGroups?.ToArray() ?? []),
            normalizedAdvisory,
            conversation,
            Array.AsReadOnly(canonicalOptionalHandles));
    }
}

[JsonConverter(typeof(EvidencePacketJsonConverter))]
public sealed record EvidencePacket
{
    private const int _maxEvidenceUtf8Bytes = 65_536;

    public const string SupportedSchemaVersion = "grounded-chat-evidence/2.0";

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
        ChatConversationState conversation,
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
        Conversation = conversation;
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
    public ChatConversationState Conversation { get; }
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
        ChatConversationState? conversation = null)
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
            .OrderBy(group => group.FlowGroupId)
            .Select(group => group with
            {
                Steps = Array.AsReadOnly((group.Steps ?? [])
                    .OrderBy(step => step.StepOrder)
                    .ThenBy(step => step.FlowTraceStepId)
                    .ToArray()),
            })
            .ToArray() ?? [];
        string normalizedAdvisory = advisoryContext?.Normalize(NormalizationForm.FormC) ?? string.Empty;
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
        string canonicalJson = JsonSerializer.Serialize(new
        {
            schemaVersion = SupportedSchemaVersion,
            requestId,
            mode = mode.ToString(),
            systemId,
            repositoryIds = canonicalRepositories,
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
                relationship.ExactDecisionId,
                relationship.FlowGroupId,
            }),
            flowEvidence = canonicalFlow.Select(step => new
            {
                step.StepOrder,
                step.DocumentHandle,
                step.RelationshipHandle,
                step.Description,
            }),
            flowGroups = canonicalFlowGroups,
            advisoryContext = normalizedAdvisory,
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
            canonicalConversation,
            ChatContractGuard.Sha256(canonicalJson));
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
            exactDecisionId = relationship.ExactDecisionId,
            flowGroupId = relationship.FlowGroupId,
        }), options);
        WriteValue(writer, "flowSteps", value.FlowEvidence.Select(step => new
        {
            order = step.StepOrder,
            documentHandle = step.DocumentHandle,
            relationshipHandle = step.RelationshipHandle,
            description = step.Description,
        }), options);
        writer.WriteString("advisoryContext", value.AdvisoryContext);
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
    string? ProfileFingerprint);

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
                    relationship.ExactDecisionId is null ||
                relationship.RelationshipType == ChatRelationshipType.GeneratedFrom &&
                    relationship.ExactDecisionId is not null ||
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
