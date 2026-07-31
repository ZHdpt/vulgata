using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public sealed class KnowledgeDocumentMaterializer
{
    private static readonly JsonDocumentOptions _jsonOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 4,
    };
    public KnowledgeDocumentMaterialization Materialize(
        Guid logicalDocumentId,
        Guid documentVersionId,
        Guid repositoryId,
        Guid? systemId,
        string contentJson,
        DateTimeOffset createdAt)
    {
        EnsureIdentifier(logicalDocumentId);
        EnsureIdentifier(documentVersionId);
        EnsureIdentifier(repositoryId);
        if (systemId == Guid.Empty)
        {
            throw new KnowledgeStoreContractException(KnowledgeRejectionCode.InvalidOwnership);
        }

        CanonicalDocumentContent content = Parse(contentJson);
        string canonicalJson = Serialize(content);
        EnsureUtf8Bound(canonicalJson, KnowledgeDocumentLimits.MaximumContentBytes);
        string markdown = RenderMarkdown(content);
        EnsureUtf8Bound(markdown, KnowledgeDocumentLimits.MaximumMarkdownBytes);
        string fingerprint = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{canonicalJson}\n{markdown}")));

        string keySymbolsJson = JsonSerializer.Serialize(content.KeySymbols);
        string businessTermsJson = JsonSerializer.Serialize(content.BusinessTerms);
        string technicalTermsJson = JsonSerializer.Serialize(content.TechnicalTerms);
        string searchText = string.Join(
            '\n',
            new[]
            {
                content.Title,
                content.Summary,
                string.Join(' ', content.KeySymbols),
                string.Join(' ', content.BusinessTerms),
                string.Join(' ', content.TechnicalTerms),
            }.Concat(content.Sections.Select(section => section.SearchText)));
        EnsureUtf8Bound(searchText, KnowledgeDocumentLimits.MaximumIndexSearchTextBytes);

        IndexEntry indexEntry = IndexEntry.Create(
            CreateDeterministicId(documentVersionId, "index"),
            logicalDocumentId,
            documentVersionId,
            repositoryId,
            systemId,
            content.DocumentType,
            content.Title,
            content.Summary,
            content.RepositoryRelativePath,
            keySymbolsJson,
            businessTermsJson,
            technicalTermsJson,
            searchText,
            createdAt);
        DocumentSection[] sections = content.Sections
            .Select(section => DocumentSection.Create(
                CreateDeterministicId(documentVersionId, section.StableSectionId.ToString("D")),
                logicalDocumentId,
                documentVersionId,
                repositoryId,
                section.StableSectionId,
                section.Order,
                section.Kind,
                section.Heading,
                section.Markdown,
                section.SearchText,
                JsonSerializer.Serialize(section.SourceAnchorIds),
                JsonSerializer.Serialize(section.DocumentVersionIds),
                createdAt))
            .ToArray();

        return new(content, canonicalJson, markdown, fingerprint, indexEntry, sections);
    }

    public KnowledgeDocumentMaterialization Materialize(
        Guid logicalDocumentId,
        Guid documentVersionId,
        Guid repositoryId,
        Guid? systemId,
        CanonicalDocumentContent content,
        DateTimeOffset createdAt) =>
        Materialize(
            logicalDocumentId,
            documentVersionId,
            repositoryId,
            systemId,
            Serialize(content),
            createdAt);

    public string CreateCommitFingerprint(
        KnowledgeCommit request,
        KnowledgeDocumentMaterialization projection)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("operationId", request.OperationId);
            writer.WriteString("logicalDocumentId", request.LogicalDocumentId);
            if (request.ExpectedCurrentVersionId is Guid expectedCurrentVersionId)
            {
                writer.WriteString("expectedCurrentVersionId", expectedCurrentVersionId);
            }
            else
            {
                writer.WriteNull("expectedCurrentVersionId");
            }

            writer.WriteString("newDocumentVersionId", request.NewDocumentVersionId);
            writer.WriteString("repositoryId", request.RepositoryId);
            writer.WriteString("scanRunId", request.ScanRunId);
            writer.WriteString("profiledRevision", request.ProfiledRevision);
            writer.WriteString("committedAt", request.CommittedAt);
            writer.WriteString("contentJson", projection.ContentJson);
            writer.WriteString("contentMarkdown", projection.ContentMarkdown);
            writer.WriteString("contentFingerprint", projection.ContentFingerprint);

            writer.WriteStartArray("sourceAnchors");
            foreach (SourceAnchorDraft anchor in request.SourceAnchors.OrderBy(anchor => anchor.Id))
            {
                writer.WriteStartObject();
                writer.WriteString("id", anchor.Id);
                writer.WriteString("documentVersionId", anchor.DocumentVersionId);
                writer.WriteString("repositoryId", anchor.RepositoryId);
                writer.WriteString("scanRunId", anchor.ScanRunId);
                writer.WriteString("codeUnitId", anchor.CodeUnitId);
                writer.WriteString("profiledRevision", anchor.ProfiledRevision);
                writer.WriteString("repositoryRelativePath", anchor.RepositoryRelativePath);
                writer.WriteString("evidenceLocator", anchor.EvidenceLocator);
                writer.WriteNumber("lineStart", anchor.LineStart);
                writer.WriteNumber("lineEnd", anchor.LineEnd);
                writer.WriteString("sourceHash", anchor.SourceHash);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("edges");
            foreach (EdgeDraft edge in request.Edges.OrderBy(edge => edge.Id))
            {
                writer.WriteStartObject();
                writer.WriteString("id", edge.Id);
                writer.WriteString("sourceDocumentVersionId", edge.SourceDocumentVersionId);
                writer.WriteString("sourceRepositoryId", edge.SourceRepositoryId);
                writer.WriteString("sourceScanRunId", edge.SourceScanRunId);
                writer.WriteString("targetDocumentVersionId", edge.TargetDocumentVersionId);
                writer.WriteString("targetRepositoryId", edge.TargetRepositoryId);
                writer.WriteString("targetScanRunId", edge.TargetScanRunId);
                writer.WriteString("edgeType", edge.EdgeType.ToString());
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("flowGroups");
            foreach (FlowGroupDraft group in request.FlowGroups
                .OrderBy(group => group.GroupOrder)
                .ThenBy(group => group.Id))
            {
                writer.WriteStartObject();
                writer.WriteString("id", group.Id);
                writer.WriteNumber("groupOrder", group.GroupOrder);
                writer.WriteString("title", group.Title);
                writer.WriteStartArray("traces");
                foreach (FlowTraceDraft trace in group.Traces
                    .OrderBy(trace => trace.TraceOrder)
                    .ThenBy(trace => trace.Id))
                {
                    writer.WriteStartObject();
                    writer.WriteString("id", trace.Id);
                    writer.WriteNumber("traceOrder", trace.TraceOrder);
                    writer.WriteStartArray("steps");
                    foreach (FlowTraceStepDraft step in trace.Steps
                        .OrderBy(step => step.StepOrder)
                        .ThenBy(step => step.Id))
                    {
                        writer.WriteStartObject();
                        writer.WriteString("id", step.Id);
                        writer.WriteNumber("stepOrder", step.StepOrder);
                        writer.WriteString("documentVersionId", step.DocumentVersionId);
                        writer.WriteString("documentRepositoryId", step.DocumentRepositoryId);
                        writer.WriteString("documentScanRunId", step.DocumentScanRunId);
                        if (step.EdgeId is Guid edgeId)
                        {
                            writer.WriteString("edgeId", edgeId);
                        }
                        else
                        {
                            writer.WriteNull("edgeId");
                        }

                        writer.WriteString("stepKind", step.StepKind.ToString());
                        writer.WriteString("description", step.Description);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            WriteIndexEntry(writer, projection.IndexEntry);
            writer.WriteStartArray("sections");
            foreach (DocumentSection section in projection.Sections
                .OrderBy(section => section.SectionOrder)
                .ThenBy(section => section.Id))
            {
                writer.WriteStartObject();
                writer.WriteString("id", section.Id);
                writer.WriteString("logicalDocumentId", section.LogicalDocumentId);
                writer.WriteString("documentVersionId", section.DocumentVersionId);
                writer.WriteString("repositoryId", section.RepositoryId);
                writer.WriteString("stableSectionId", section.StableSectionId);
                writer.WriteNumber("sectionOrder", section.SectionOrder);
                writer.WriteString("sectionKind", section.SectionKind.ToString());
                writer.WriteString("heading", section.Heading);
                writer.WriteString("contentMarkdown", section.ContentMarkdown);
                writer.WriteString("searchText", section.SearchText);
                writer.WriteString("sourceAnchorIdsJson", section.SourceAnchorIdsJson);
                writer.WriteString("documentVersionIdsJson", section.DocumentVersionIdsJson);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteIndexEntry(Utf8JsonWriter writer, IndexEntry entry)
    {
        writer.WriteStartObject("indexEntry");
        writer.WriteString("id", entry.Id);
        writer.WriteString("logicalDocumentId", entry.LogicalDocumentId);
        writer.WriteString("documentVersionId", entry.DocumentVersionId);
        writer.WriteString("repositoryId", entry.RepositoryId);
        if (entry.SystemId is Guid systemId)
        {
            writer.WriteString("systemId", systemId);
        }
        else
        {
            writer.WriteNull("systemId");
        }

        writer.WriteString("documentType", entry.DocumentType.ToString());
        writer.WriteString("title", entry.Title);
        writer.WriteString("summary", entry.Summary);
        writer.WriteString("repositoryRelativePath", entry.RepositoryRelativePath);
        writer.WriteString("keySymbolsJson", entry.KeySymbolsJson);
        writer.WriteString("businessTermsJson", entry.BusinessTermsJson);
        writer.WriteString("technicalTermsJson", entry.TechnicalTermsJson);
        writer.WriteString("searchText", entry.SearchText);
        writer.WriteEndObject();
    }

    private static CanonicalDocumentContent Parse(string contentJson)
    {
        try
        {
            EnsureUtf8Bound(contentJson, KnowledgeDocumentLimits.MaximumContentBytes);
            using JsonDocument document = JsonDocument.Parse(contentJson, _jsonOptions);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                Reject(KnowledgeRejectionCode.InvalidContent);
            }

            RequireExactProperties(
                root,
                "schemaIdentity",
                "documentType",
                "stableKey",
                "title",
                "repositoryRelativePath",
                "summary",
                "keySymbols",
                "businessTerms",
                "technicalTerms",
                "sections");

            string schemaIdentity = RequireString(root, "schemaIdentity");
            if (!StringComparer.Ordinal.Equals(
                    schemaIdentity,
                    KnowledgeDocumentLimits.SchemaIdentity))
            {
                Reject(KnowledgeRejectionCode.InvalidContent);
            }

            DocumentType documentType = ParseClosedEnum<DocumentType>(
                RequireString(root, "documentType"));
            string stableKey = NormalizeText(
                RequireString(root, "stableKey"),
                KnowledgeDocumentLimits.MaximumShortTextBytes,
                false);
            string title = NormalizeText(
                RequireString(root, "title"),
                KnowledgeDocumentLimits.MaximumShortTextBytes,
                false);
            string repositoryRelativePath = NormalizeRepositoryPath(
                RequireString(root, "repositoryRelativePath"));
            string summary = NormalizeText(
                RequireString(root, "summary"),
                KnowledgeDocumentLimits.MaximumShortTextBytes,
                false);
            IReadOnlyList<string> keySymbols = ParseOrderedStrings(
                root.GetProperty("keySymbols"));
            IReadOnlyList<string> businessTerms = ParseOrderedStrings(
                root.GetProperty("businessTerms"));
            IReadOnlyList<string> technicalTerms = ParseOrderedStrings(
                root.GetProperty("technicalTerms"));
            IReadOnlyList<CanonicalSection> sections = ParseSections(root.GetProperty("sections"));

            return new(
                schemaIdentity,
                documentType,
                stableKey,
                title,
                repositoryRelativePath,
                summary,
                keySymbols,
                businessTerms,
                technicalTerms,
                sections);
        }
        catch (KnowledgeStoreContractException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or FormatException)
        {
            throw new KnowledgeStoreContractException(KnowledgeRejectionCode.InvalidContent);
        }
    }

    private static IReadOnlyList<CanonicalSection> ParseSections(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            Reject(KnowledgeRejectionCode.InvalidContent);
        }

        JsonElement[] values = element.EnumerateArray().ToArray();
        if (values.Length is < 1 or > KnowledgeDocumentLimits.MaximumSections)
        {
            Reject(KnowledgeRejectionCode.InvalidBound);
        }

        var sections = new List<CanonicalSection>(values.Length);
        for (int index = 0; index < values.Length; index++)
        {
            JsonElement section = values[index];
            if (section.ValueKind != JsonValueKind.Object)
            {
                Reject(KnowledgeRejectionCode.InvalidContent);
            }

            RequireExactProperties(
                section,
                "stableSectionId",
                "order",
                "kind",
                "heading",
                "markdown",
                "searchText",
                "sourceAnchorIds",
                "documentVersionIds");
            int order = section.GetProperty("order").GetInt32();
            if (order != index + 1)
            {
                Reject(KnowledgeRejectionCode.InvalidContent);
            }

            Guid stableSectionId = section.GetProperty("stableSectionId").GetGuid();
            EnsureIdentifier(stableSectionId);
            string heading = NormalizeText(
                RequireString(section, "heading"),
                KnowledgeDocumentLimits.MaximumShortTextBytes,
                false);
            string markdown = NormalizeText(
                RequireString(section, "markdown"),
                KnowledgeDocumentLimits.MaximumSectionMarkdownBytes,
                true);
            string searchText = NormalizeText(
                RequireString(section, "searchText"),
                KnowledgeDocumentLimits.MaximumSectionSearchTextBytes,
                true);
            sections.Add(new(
                stableSectionId,
                order,
                ParseClosedEnum<DocumentSectionKind>(RequireString(section, "kind")),
                heading,
                markdown,
                searchText,
                ParseOrderedGuids(
                    section.GetProperty("sourceAnchorIds"),
                    KnowledgeDocumentLimits.MaximumAnchors),
                ParseOrderedGuids(
                    section.GetProperty("documentVersionIds"),
                    KnowledgeDocumentLimits.MaximumEdges)));
        }

        if (sections.Select(section => section.StableSectionId).Distinct().Count() != sections.Count)
        {
            Reject(KnowledgeRejectionCode.InvalidContent);
        }

        return sections;
    }

    private static IReadOnlyList<string> ParseOrderedStrings(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            Reject(KnowledgeRejectionCode.InvalidContent);
        }

        string[] values = element.EnumerateArray()
            .Select(value =>
                NormalizeText(
                    value.GetString() ?? string.Empty,
                    KnowledgeDocumentLimits.MaximumShortTextBytes,
                    false))
            .ToArray();
        if (values.Length > KnowledgeDocumentLimits.MaximumTerms)
        {
            Reject(KnowledgeRejectionCode.InvalidBound);
        }

        EnsureCanonicalOrder(values, StringComparer.Ordinal);
        return values;
    }

    private static IReadOnlyList<Guid> ParseOrderedGuids(JsonElement element, int maximumCount)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            Reject(KnowledgeRejectionCode.InvalidContent);
        }

        Guid[] values = element.EnumerateArray().Select(value => value.GetGuid()).ToArray();
        if (values.Length > maximumCount)
        {
            Reject(KnowledgeRejectionCode.InvalidBound);
        }

        if (values.Any(value => value == Guid.Empty) ||
            values.Distinct().Count() != values.Length ||
            !values.SequenceEqual(values.OrderBy(value => value)))
        {
            Reject(KnowledgeRejectionCode.InvalidContent);
        }

        return values;
    }

    private static void EnsureCanonicalOrder<T>(
        IReadOnlyList<T> values,
        IComparer<T> comparer)
    {
        if (values.Distinct().Count() != values.Count ||
            !values.SequenceEqual(values.OrderBy(value => value, comparer)))
        {
            Reject(KnowledgeRejectionCode.InvalidContent);
        }
    }

    private static string Serialize(CanonicalDocumentContent content)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaIdentity", content.SchemaIdentity);
            writer.WriteString("documentType", content.DocumentType.ToString());
            writer.WriteString("stableKey", content.StableKey);
            writer.WriteString("title", content.Title);
            writer.WriteString("repositoryRelativePath", content.RepositoryRelativePath);
            writer.WriteString("summary", content.Summary);
            WriteStrings(writer, "keySymbols", content.KeySymbols);
            WriteStrings(writer, "businessTerms", content.BusinessTerms);
            WriteStrings(writer, "technicalTerms", content.TechnicalTerms);
            writer.WriteStartArray("sections");
            foreach (CanonicalSection section in content.Sections)
            {
                writer.WriteStartObject();
                writer.WriteString("stableSectionId", section.StableSectionId);
                writer.WriteNumber("order", section.Order);
                writer.WriteString("kind", section.Kind.ToString());
                writer.WriteString("heading", section.Heading);
                writer.WriteString("markdown", section.Markdown);
                writer.WriteString("searchText", section.SearchText);
                WriteGuids(writer, "sourceAnchorIds", section.SourceAnchorIds);
                WriteGuids(writer, "documentVersionIds", section.DocumentVersionIds);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string RenderMarkdown(CanonicalDocumentContent content)
    {
        var builder = new StringBuilder()
            .Append("# ")
            .Append(content.Title);
        foreach (CanonicalSection section in content.Sections)
        {
            builder.Append("\n\n## ")
                .Append(section.Heading)
                .Append("\n\n")
                .Append(section.Markdown);
        }

        return builder.ToString();
    }

    private static string NormalizeText(string value, int maximumBytes, bool allowLayoutControls)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Reject(KnowledgeRejectionCode.InvalidContent);
        }

        string normalized = value.Normalize(NormalizationForm.FormC);
        foreach (char character in normalized)
        {
            bool control = character <= '\u001f' || character is >= '\u007f' and <= '\u009f';
            if (control && !(allowLayoutControls && character is '\n' or '\t'))
            {
                Reject(KnowledgeRejectionCode.InvalidContent);
            }
        }

        KnowledgeTextSafety.EnsureSafe(normalized);
        EnsureUtf8Bound(normalized, maximumBytes);
        return normalized;
    }

    private static string NormalizeRepositoryPath(string value)
    {
        string path = NormalizeText(
                value,
                KnowledgeDocumentLimits.MaximumRepositoryPathBytes,
                false)
            .Replace('\\', '/');
        string[] segments = path.Split('/');
        if (path.StartsWith('/') ||
            segments.Any(segment =>
                segment.Length == 0 ||
                segment is "." or "..") ||
            (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':'))
        {
            Reject(KnowledgeRejectionCode.InvalidContent);
        }

        return path;
    }

    private static void RequireExactProperties(JsonElement element, params string[] names)
    {
        JsonProperty[] properties = element.EnumerateObject().ToArray();
        if (properties.Length != names.Length ||
            properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() !=
            properties.Length ||
            properties.Any(property => !names.Contains(property.Name, StringComparer.Ordinal)) ||
            names.Any(name => !properties.Any(property =>
                StringComparer.Ordinal.Equals(property.Name, name))))
        {
            Reject(KnowledgeRejectionCode.InvalidContent);
        }
    }

    private static string RequireString(JsonElement element, string propertyName)
    {
        JsonElement property = element.GetProperty(propertyName);
        if (property.ValueKind != JsonValueKind.String)
        {
            Reject(KnowledgeRejectionCode.InvalidContent);
        }

        return property.GetString() ?? string.Empty;
    }

    private static T ParseClosedEnum<T>(string value)
        where T : struct, Enum
    {
        if (!Enum.TryParse(value, false, out T result) ||
            !StringComparer.Ordinal.Equals(value, result.ToString()))
        {
            Reject(KnowledgeRejectionCode.InvalidContent);
        }

        return result;
    }

    private static void WriteStrings(
        Utf8JsonWriter writer,
        string propertyName,
        IReadOnlyList<string> values)
    {
        writer.WriteStartArray(propertyName);
        foreach (string value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    private static void WriteGuids(
        Utf8JsonWriter writer,
        string propertyName,
        IReadOnlyList<Guid> values)
    {
        writer.WriteStartArray(propertyName);
        foreach (Guid value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    private static Guid CreateDeterministicId(Guid namespaceId, string value)
    {
        byte[] bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes($"{namespaceId:D}:{value}"));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static void EnsureIdentifier(Guid value)
    {
        if (value == Guid.Empty)
        {
            Reject(KnowledgeRejectionCode.InvalidOwnership);
        }
    }

    private static void EnsureUtf8Bound(string value, int maximumBytes)
    {
        if (Encoding.UTF8.GetByteCount(value) > maximumBytes)
        {
            Reject(KnowledgeRejectionCode.InvalidBound);
        }
    }

    private static void Reject(KnowledgeRejectionCode code) =>
        throw new KnowledgeStoreContractException(code);
}
