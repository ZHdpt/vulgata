using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Agents.ChatExecution;

public sealed class ChatCandidateExtractor
{
    private const int _maximumCandidateLength = 2_000;

    public IReadOnlyList<CandidateClaim> Extract(EvidencePacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        Dictionary<string, EvidenceDocument> documents = packet.Documents.ToDictionary(
            document => document.DocumentHandle,
            StringComparer.Ordinal);
        Dictionary<string, EvidenceAnchor> anchors = packet.SourceAnchors.ToDictionary(
            anchor => anchor.AnchorHandle,
            StringComparer.Ordinal);
        var extracted = new List<ExtractedCandidate>();

        foreach (EvidenceSection section in packet.Sections
            .OrderBy(section => section.DocumentHandle, StringComparer.Ordinal)
            .ThenBy(section => section.SectionOrder)
            .ThenBy(section => section.SectionHandle, StringComparer.Ordinal))
        {
            if (!documents.TryGetValue(section.DocumentHandle, out EvidenceDocument? document) ||
                !IsModeEligible(packet.Mode, document.DocumentType, section.SectionKind) ||
                !TryGetSupport(packet, section, document, documents, anchors, out CandidateSupport support))
            {
                continue;
            }

            bool allowsBusinessNarrative = packet.Mode == ChatMode.Business &&
                document.DocumentType == DocumentType.CodeLogic;
            foreach (TextSpan span in ExtractTextSpans(
                section.ContentMarkdown,
                packet.Mode,
                allowsBusinessNarrative))
            {
                extracted.Add(new ExtractedCandidate(document, section, span, support));
            }
        }

        CandidateClaim[] candidates = extracted.Select((candidate, index) => CandidateClaim.Create(
            $"k{index + 1}",
            packet.Fingerprint,
            packet.Mode,
            candidate.Document.DocumentVersionId,
            candidate.Section.StableSectionId,
            candidate.Span.Start,
            candidate.Section.ContentMarkdown.Substring(candidate.Span.Start, candidate.Span.Length),
            candidate.Support.AnchorHandles,
            candidate.Support.RelationshipHandles,
            packet.Mode == ChatMode.Business &&
                candidate.Document.DocumentType == DocumentType.CodeLogic)).ToArray();
        return Array.AsReadOnly(candidates);
    }

    private static bool IsModeEligible(
        ChatMode mode,
        DocumentType documentType,
        DocumentSectionKind sectionKind) =>
        mode switch
        {
            ChatMode.Business =>
                (documentType == DocumentType.BusinessLogic &&
                    sectionKind is DocumentSectionKind.Overview or DocumentSectionKind.ProcessStep) ||
                (documentType == DocumentType.CodeLogic &&
                    sectionKind is DocumentSectionKind.Overview or
                        DocumentSectionKind.BusinessRule or
                        DocumentSectionKind.ProcessStep),
            ChatMode.It =>
                documentType == DocumentType.CodeLogic &&
                sectionKind is DocumentSectionKind.Overview or
                    DocumentSectionKind.ProcessStep or
                    DocumentSectionKind.TechnicalDetail or
                    DocumentSectionKind.Evidence,
            _ => false,
        };

    private static bool TryGetSupport(
        EvidencePacket packet,
        EvidenceSection section,
        EvidenceDocument document,
        Dictionary<string, EvidenceDocument> documents,
        Dictionary<string, EvidenceAnchor> anchors,
        out CandidateSupport support)
    {
        string[] anchorHandles = section.AnchorHandles
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (anchorHandles.Length == 0 ||
            anchorHandles.Any(handle =>
                !anchors.TryGetValue(handle, out EvidenceAnchor? anchor) ||
                anchor.DocumentHandle != section.DocumentHandle ||
                anchor.DocumentVersionId != document.DocumentVersionId ||
                anchor.RepositoryId != document.RepositoryId))
        {
            support = null!;
            return false;
        }

        var relationshipHandles = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string supportingDocumentHandle in section.SupportingDocumentHandles
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal))
        {
            if (supportingDocumentHandle == section.DocumentHandle ||
                !documents.ContainsKey(supportingDocumentHandle))
            {
                support = null!;
                return false;
            }

            EvidenceRelationship[] matchingRelationships = packet.Relationships
                .Where(relationship =>
                    IsSupportedRelationship(relationship) &&
                    relationship.SourceDocumentHandle == section.DocumentHandle &&
                    relationship.TargetDocumentHandle == supportingDocumentHandle)
                .OrderBy(relationship => relationship.RelationshipHandle, StringComparer.Ordinal)
                .ToArray();
            if (matchingRelationships.Length == 0)
            {
                support = null!;
                return false;
            }

            foreach (EvidenceRelationship relationship in matchingRelationships)
            {
                _ = relationshipHandles.Add(relationship.RelationshipHandle);
            }
        }

        support = new CandidateSupport(anchorHandles, relationshipHandles.ToArray());
        return true;
    }

    private static bool IsSupportedRelationship(EvidenceRelationship relationship) =>
        relationship.RelationshipType != ChatRelationshipType.References ||
        (relationship.ProfileFingerprint is { Length: 64 } fingerprint &&
            fingerprint.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'));

    private static IEnumerable<TextSpan> ExtractTextSpans(
        string markdown,
        ChatMode mode,
        bool allowInlineCodeMarkup)
    {
        char fenceMarker = '\0';
        int fenceLength = 0;
        int position = 0;
        while (position < markdown.Length)
        {
            int newline = markdown.IndexOf('\n', position);
            int rawEnd = newline < 0 ? markdown.Length : newline;
            int contentEnd = rawEnd > position && markdown[rawEnd - 1] == '\r' ? rawEnd - 1 : rawEnd;
            ReadOnlySpan<char> line = markdown.AsSpan(position, contentEnd - position);
            ReadOnlySpan<char> trimmedStart = line.TrimStart();

            if (TryGetFence(trimmedStart, out char currentMarker, out int currentLength))
            {
                if (fenceMarker == '\0')
                {
                    fenceMarker = currentMarker;
                    fenceLength = currentLength;
                }
                else if (currentMarker == fenceMarker && currentLength >= fenceLength)
                {
                    fenceMarker = '\0';
                    fenceLength = 0;
                }
            }
            else if (fenceMarker == '\0' &&
                TryGetCandidateSpan(
                    line,
                    position,
                    mode,
                    allowInlineCodeMarkup,
                    out TextSpan candidateSpan))
            {
                bool isListItem = IsListItem(line);
                int candidateEnd = candidateSpan.Start + candidateSpan.Length;
                int continuationPosition = newline < 0 ? markdown.Length : newline + 1;
                while (continuationPosition < markdown.Length)
                {
                    int continuationNewline = markdown.IndexOf('\n', continuationPosition);
                    int continuationRawEnd = continuationNewline < 0 ? markdown.Length : continuationNewline;
                    int continuationContentEnd = continuationRawEnd > continuationPosition &&
                        markdown[continuationRawEnd - 1] == '\r'
                            ? continuationRawEnd - 1
                            : continuationRawEnd;
                    ReadOnlySpan<char> continuationLine = markdown.AsSpan(
                        continuationPosition,
                        continuationContentEnd - continuationPosition);
                    if (!TryGetContinuationEnd(continuationLine, isListItem, out int relativeEnd))
                    {
                        break;
                    }

                    candidateEnd = continuationPosition + relativeEnd;
                    continuationPosition = continuationNewline < 0 ? markdown.Length : continuationNewline + 1;
                }

                ReadOnlySpan<char> candidate = markdown.AsSpan(
                    candidateSpan.Start,
                    candidateEnd - candidateSpan.Start);
                if (candidate.Length <= _maximumCandidateLength &&
                    IsSafeText(candidate) &&
                    (mode != ChatMode.Business ||
                        IsBusinessSafeText(candidate, allowInlineCodeMarkup)))
                {
                    yield return new TextSpan(candidateSpan.Start, candidate.Length);
                }

                position = continuationPosition;
                continue;
            }

            if (newline < 0)
            {
                yield break;
            }

            position = newline + 1;
        }
    }

    private static bool IsListItem(ReadOnlySpan<char> line)
    {
        int start = 0;
        while (start < line.Length && char.IsWhiteSpace(line[start]))
        {
            start++;
        }

        return start < line.Length && TryGetListBodyOffset(line[start..], out _);
    }

    private static bool TryGetContinuationEnd(
        ReadOnlySpan<char> line,
        bool isListItem,
        out int contentEnd)
    {
        int start = 0;
        while (start < line.Length && char.IsWhiteSpace(line[start]))
        {
            start++;
        }

        contentEnd = line.Length;
        while (contentEnd > start && char.IsWhiteSpace(line[contentEnd - 1]))
        {
            contentEnd--;
        }

        if (contentEnd == start ||
            (isListItem && start < 2) ||
            (!isListItem && start >= 4))
        {
            return false;
        }

        ReadOnlySpan<char> body = line.Slice(start, contentEnd - start);
        return !TryGetFence(body, out _, out _) &&
            !TryGetListBodyOffset(body, out _) &&
            !StartsWithBlockMarker(body);
    }

    private static bool TryGetCandidateSpan(
        ReadOnlySpan<char> line,
        int lineStart,
        ChatMode mode,
        bool allowInlineCodeMarkup,
        out TextSpan candidateSpan)
    {
        if (TryGetBody(line, out int bodyOffset, out int bodyLength))
        {
            ReadOnlySpan<char> body = line.Slice(bodyOffset, bodyLength);
            if (bodyLength <= _maximumCandidateLength &&
                IsSafeText(body) &&
                (mode != ChatMode.Business ||
                    IsBusinessSafeText(body, allowInlineCodeMarkup)))
            {
                candidateSpan = new TextSpan(lineStart + bodyOffset, bodyLength);
                return true;
            }
        }

        candidateSpan = default;
        return false;
    }

    private static bool TryGetFence(
        ReadOnlySpan<char> trimmedStart,
        out char marker,
        out int markerLength)
    {
        marker = '\0';
        markerLength = 0;
        if (trimmedStart.Length < 3 || trimmedStart[0] is not ('`' or '~'))
        {
            return false;
        }

        marker = trimmedStart[0];
        while (markerLength < trimmedStart.Length && trimmedStart[markerLength] == marker)
        {
            markerLength++;
        }

        return markerLength >= 3;
    }

    private static bool TryGetBody(
        ReadOnlySpan<char> line,
        out int bodyOffset,
        out int bodyLength)
    {
        int start = 0;
        while (start < line.Length && char.IsWhiteSpace(line[start]))
        {
            start++;
        }

        int end = line.Length;
        while (end > start && char.IsWhiteSpace(line[end - 1]))
        {
            end--;
        }

        bodyOffset = start;
        bodyLength = end - start;
        if (bodyLength == 0 || start >= 4)
        {
            return false;
        }

        ReadOnlySpan<char> body = line.Slice(start, bodyLength);
        if (TryGetListBodyOffset(body, out int markerLength))
        {
            int listBodyStart = markerLength;
            while (listBodyStart < body.Length && char.IsWhiteSpace(body[listBodyStart]))
            {
                listBodyStart++;
            }

            bodyOffset += listBodyStart;
            bodyLength = end - bodyOffset;
            return bodyLength > 0;
        }

        return !StartsWithBlockMarker(body);
    }

    private static bool TryGetListBodyOffset(ReadOnlySpan<char> body, out int markerLength)
    {
        markerLength = 0;
        if (body.Length >= 2 && body[0] is '-' or '+' or '*' && char.IsWhiteSpace(body[1]))
        {
            markerLength = 1;
            return true;
        }

        int digits = 0;
        while (digits < body.Length && char.IsAsciiDigit(body[digits]))
        {
            digits++;
        }

        if (digits > 0 &&
            digits + 1 < body.Length &&
            body[digits] is '.' or ')' &&
            char.IsWhiteSpace(body[digits + 1]))
        {
            markerLength = digits + 1;
            return true;
        }

        return false;
    }

    private static bool StartsWithBlockMarker(ReadOnlySpan<char> body)
    {
        if (body[0] is '#' or '>' ||
            body.StartsWith("---", StringComparison.Ordinal) ||
            body.StartsWith("***", StringComparison.Ordinal) ||
            body.StartsWith("___", StringComparison.Ordinal))
        {
            return true;
        }

        return body.StartsWith("<", StringComparison.Ordinal) ||
            body.StartsWith("|", StringComparison.Ordinal);
    }

    private static bool IsSafeText(ReadOnlySpan<char> body)
    {
        if (body.Contains('|') ||
            body.Contains('<') ||
            body.Contains('>') ||
            body.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("https://", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("www.", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("](", StringComparison.Ordinal) ||
            body.Contains("][", StringComparison.Ordinal) ||
            body.Contains("![", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (char character in body)
        {
            if (char.IsControl(character) && character is not ('\r' or '\n'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsBusinessSafeText(
        ReadOnlySpan<char> body,
        bool allowInlineCodeMarkup)
    {
        if ((!allowInlineCodeMarkup && body.Contains('`')) ||
            body.Contains('/') ||
            body.Contains('\\') ||
            ContainsCodeFileExtension(body) ||
            ContainsDottedIdentifier(body) ||
            body.Contains(" line ", StringComparison.OrdinalIgnoreCase) ||
            (body.Contains('行') && ContainsAsciiDigit(body)))
        {
            return false;
        }

        for (int index = 0; index < body.Length - 1; index++)
        {
            if ((body[index] == ':' && char.IsAsciiDigit(body[index + 1])) ||
                ((body[index] is 'L' or 'l') && char.IsAsciiDigit(body[index + 1])))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsDottedIdentifier(ReadOnlySpan<char> body)
    {
        for (int index = 1; index < body.Length - 1; index++)
        {
            if (body[index] == '.' &&
                IsAsciiIdentifierPart(body[index - 1]) &&
                IsAsciiIdentifierStart(body[index + 1]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAsciiIdentifierPart(char character) =>
        IsAsciiIdentifierStart(character) || char.IsAsciiDigit(character);

    private static bool IsAsciiIdentifierStart(char character) =>
        char.IsAsciiLetter(character) || character == '_';

    private static bool ContainsCodeFileExtension(ReadOnlySpan<char> body)
    {
        string[] extensions = [".cs", ".java", ".py", ".js", ".ts", ".tsx", ".sql", ".razor", ".cshtml"];
        foreach (string extension in extensions)
        {
            if (body.Contains(extension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAsciiDigit(ReadOnlySpan<char> value)
    {
        foreach (char character in value)
        {
            if (char.IsAsciiDigit(character))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record CandidateSupport(
        IReadOnlyCollection<string> AnchorHandles,
        IReadOnlyCollection<string> RelationshipHandles);

    private sealed record ExtractedCandidate(
        EvidenceDocument Document,
        EvidenceSection Section,
        TextSpan Span,
        CandidateSupport Support);

    private readonly record struct TextSpan(int Start, int Length);
}
