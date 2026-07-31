namespace Vulgata.Core.Entities;

public enum DocumentType
{
    CodeLogic,
    BusinessLogic,
}

public enum DocumentEdgeType
{
    References,
    GeneratedFrom,
}

public enum FlowTraceStepKind
{
    Document,
    Relationship,
    Termination,
}

public enum DocumentSectionKind
{
    Overview,
    BusinessRule,
    ProcessStep,
    TechnicalDetail,
    Evidence,
    Other,
}

public enum KnowledgeCommitStatus
{
    Committed,
    Conflict,
    Rejected,
}

public enum KnowledgeRejectionCode
{
    InvalidBound,
    InvalidContent,
    InvalidOwnership,
    MissingEndpoint,
    NonCurrentEndpoint,
    UnauthorizedEndpoint,
    InvalidEdgeSemantic,
    InvalidAnchor,
    InvalidFlow,
}

public sealed record CanonicalSection(
    Guid StableSectionId,
    int Order,
    DocumentSectionKind Kind,
    string Heading,
    string Markdown,
    string SearchText,
    IReadOnlyList<Guid> SourceAnchorIds,
    IReadOnlyList<Guid> DocumentVersionIds);

public sealed record CanonicalDocumentContent(
    string SchemaIdentity,
    DocumentType DocumentType,
    string StableKey,
    string Title,
    string RepositoryRelativePath,
    string Summary,
    IReadOnlyList<string> KeySymbols,
    IReadOnlyList<string> BusinessTerms,
    IReadOnlyList<string> TechnicalTerms,
    IReadOnlyList<CanonicalSection> Sections);

public sealed record KnowledgeDocumentMaterialization(
    CanonicalDocumentContent Content,
    string ContentJson,
    string ContentMarkdown,
    string ContentFingerprint,
    IndexEntry IndexEntry,
    IReadOnlyList<DocumentSection> Sections);

public sealed class KnowledgeStoreContractException : Exception
{
    public KnowledgeStoreContractException(KnowledgeRejectionCode rejectionCode)
        : base("The knowledge-store request was rejected.")
    {
        RejectionCode = rejectionCode;
    }

    public KnowledgeRejectionCode RejectionCode { get; }
}

public static class KnowledgeDocumentLimits
{
    public const string SchemaIdentity = "vulgata.knowledge-document/1.0";
    public const int MaximumContentBytes = 1_048_576;
    public const int MaximumMarkdownBytes = 1_048_576;
    public const int MaximumSections = 256;
    public const int MaximumSectionMarkdownBytes = 65_536;
    public const int MaximumSectionSearchTextBytes = 131_072;
    public const int MaximumAnchors = 512;
    public const int MaximumEdges = 1_024;
    public const int MaximumFlowGroups = 32;
    public const int MaximumTracesPerGroup = 64;
    public const int MaximumStepsPerTrace = 256;
    public const int MaximumShortTextBytes = 4_096;
    public const int MaximumRepositoryPathBytes = 2_048;
    public const int MaximumTerms = 256;
    public const int MaximumIndexSearchTextBytes = 4_194_304;
}
