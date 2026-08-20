using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace Vulgata.Infrastructure.CommunicationDetection;

public static class SafeXmlReader
{
    public const int MaximumRawBytes = 4_194_304;
    public const int MaximumDepth = 64;
    public const int MaximumElements = 100_000;
    public const int MaximumTotalAttributes = 200_000;
    public const int MaximumAttributesPerElement = 128;
    public const int MaximumEvidenceCharacters = 16_384;

    private static readonly UTF8Encoding _strictUtf8 = new(false, true);
    private static readonly UnicodeEncoding _strictUtf16LittleEndian = new(false, true, true);
    private static readonly UnicodeEncoding _strictUtf16BigEndian = new(true, true, true);

    public static SafeXmlReadResult Read(string canonicalPath, ReadOnlyMemory<byte> source)
    {
        if (!IsCanonicalPath(canonicalPath))
        {
            return Rejected("UnsafePath");
        }

        if (source.IsEmpty || source.Length > MaximumRawBytes)
        {
            return source.Length > MaximumRawBytes
                ? Bounded("RawBytes")
                : Rejected("EmptyXml");
        }

        if (!TryDecode(source.Span, out SafeXmlEncoding encoding, out string? xml, out string? encodingFailure))
        {
            return Rejected(encodingFailure!);
        }

        if (xml.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
            xml.Contains("<!ENTITY", StringComparison.OrdinalIgnoreCase))
        {
            return Rejected("ProhibitedDeclaration");
        }

        try
        {
            using var textReader = new StringReader(xml);
            using XmlReader reader = XmlReader.Create(
                textReader,
                new XmlReaderSettings
                {
                    Async = false,
                    CheckCharacters = true,
                    CloseInput = true,
                    ConformanceLevel = ConformanceLevel.Document,
                    DtdProcessing = DtdProcessing.Prohibit,
                    IgnoreComments = true,
                    IgnoreProcessingInstructions = true,
                    MaxCharactersFromEntities = 0,
                    XmlResolver = null,
                });

            var nodes = new List<SafeXmlNode>();
            var textByDepth = new Dictionary<int, StringBuilder>();
            var nodeIndexes = new Stack<int>();
            var elementCount = 0;
            var totalAttributeCount = 0;
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.Depth >= MaximumDepth || ++elementCount > MaximumElements)
                    {
                        return Bounded(reader.Depth >= MaximumDepth ? "Depth" : "Elements", encoding);
                    }

                    if (StringComparer.Ordinal.Equals(
                            reader.NamespaceURI,
                            "http://www.w3.org/2001/XInclude") ||
                        StringComparer.Ordinal.Equals(
                            reader.NamespaceURI,
                            "http://www.w3.org/1999/XSL/Transform") ||
                        reader.LocalName is "script" or "plugin")
                    {
                        return Rejected("XInclude", encoding);
                    }

                    int attributeCount = reader.AttributeCount;
                    totalAttributeCount = checked(totalAttributeCount + attributeCount);
                    if (attributeCount > MaximumAttributesPerElement ||
                        totalAttributeCount > MaximumTotalAttributes)
                    {
                        return Bounded(
                            attributeCount > MaximumAttributesPerElement
                                ? "AttributesPerElement"
                                : "TotalAttributes",
                            encoding);
                    }

                    var attributes = new List<SafeXmlAttribute>(attributeCount);
                    while (reader.MoveToNextAttribute())
                    {
                        if (reader.Value.Length > MaximumEvidenceCharacters)
                        {
                            return Bounded("AttributeCharacters", encoding);
                        }

                        attributes.Add(new(reader.NamespaceURI, reader.LocalName, reader.Value));
                    }

                    reader.MoveToElement();
                    IXmlLineInfo lineInfo = (IXmlLineInfo)reader;
                    var locator = XmlEvidenceLocator.Locate(
                        canonicalPath,
                        lineInfo.HasLineInfo() ? lineInfo.LineNumber : 1,
                        lineInfo.HasLineInfo() ? lineInfo.LinePosition : 1,
                        GetApproximateByteOffset(
                            xml,
                            lineInfo,
                            encoding,
                            source.Span.Length >= 3 &&
                                source.Span[0] == 0xef &&
                                source.Span[1] == 0xbb &&
                                source.Span[2] == 0xbf));
                    nodes.Add(new SafeXmlNode(
                        reader.NamespaceURI,
                        reader.LocalName,
                        new ReadOnlyCollection<SafeXmlAttribute>(attributes),
                        string.Empty,
                        locator,
                        reader.Depth));
                    nodeIndexes.Push(nodes.Count - 1);
                    textByDepth[reader.Depth] = new StringBuilder();
                    if (reader.IsEmptyElement)
                    {
                        CompleteNode(nodes, textByDepth, nodeIndexes, reader.Depth);
                    }
                }
                else if (reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA or
                    XmlNodeType.SignificantWhitespace)
                {
                    if (textByDepth.TryGetValue(reader.Depth - 1, out StringBuilder? text))
                    {
                        if (text.Length + reader.Value.Length > MaximumEvidenceCharacters)
                        {
                            return Bounded("TextCharacters", encoding);
                        }

                        text.Append(reader.Value);
                    }
                }
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    CompleteNode(nodes, textByDepth, nodeIndexes, reader.Depth);
                }
            }

            if (nodes.Count == 0)
            {
                return Rejected("MissingDocumentElement", encoding);
            }

            string fileHash = Convert.ToHexStringLower(SHA256.HashData(source.Span));
            SafeXmlDirectChildRangeSet rangeSet = SafeXmlRangeIndex.Create(
                canonicalPath,
                fileHash,
                encoding,
                source.Span,
                xml);
            if (rangeSet.Outcome != SafeXmlOutcome.Parsed)
            {
                return new(rangeSet.Outcome, encoding, rangeSet.SkipReason, rangeSet.SkipReason, null);
            }

            return new(
                SafeXmlOutcome.Parsed,
                encoding,
                null,
                null,
                new SafeXmlProjection(canonicalPath, fileHash, encoding, nodes, rangeSet));
        }
        catch (XmlException)
        {
            return Rejected("MalformedXml", encoding);
        }
        catch (DecoderFallbackException)
        {
            return Rejected("InvalidEncoding", encoding);
        }
        catch (OverflowException)
        {
            return Bounded("CounterOverflow", encoding);
        }
    }

    private static void CompleteNode(
        List<SafeXmlNode> nodes,
        Dictionary<int, StringBuilder> textByDepth,
        Stack<int> nodeIndexes,
        int depth)
    {
        if (nodeIndexes.Count == 0 || !textByDepth.Remove(depth, out StringBuilder? text))
        {
            return;
        }

        int index = nodeIndexes.Pop();
        nodes[index] = nodes[index] with { Text = text.ToString() };
    }

    private static bool TryDecode(
        ReadOnlySpan<byte> source,
        out SafeXmlEncoding encoding,
        out string xml,
        out string? failure)
    {
        Encoding decoder;
        int preambleLength;
        if (source.Length >= 3 &&
            source[0] == 0xef && source[1] == 0xbb && source[2] == 0xbf)
        {
            encoding = SafeXmlEncoding.Utf8;
            decoder = _strictUtf8;
            preambleLength = 3;
        }
        else if (source.Length >= 2 && source[0] == 0xff && source[1] == 0xfe)
        {
            encoding = SafeXmlEncoding.Utf16LittleEndian;
            decoder = _strictUtf16LittleEndian;
            preambleLength = 2;
        }
        else if (source.Length >= 2 && source[0] == 0xfe && source[1] == 0xff)
        {
            encoding = SafeXmlEncoding.Utf16BigEndian;
            decoder = _strictUtf16BigEndian;
            preambleLength = 2;
        }
        else
        {
            encoding = SafeXmlEncoding.Utf8;
            decoder = _strictUtf8;
            preambleLength = 0;
        }

        try
        {
            xml = decoder.GetString(source[preambleLength..]);
        }
        catch (DecoderFallbackException)
        {
            xml = string.Empty;
            failure = "InvalidEncoding";
            return false;
        }

        string? declared = ReadDeclaredEncoding(xml);
        bool declarationMatches = declared is null || encoding switch
        {
            SafeXmlEncoding.Utf8 => StringComparer.OrdinalIgnoreCase.Equals(declared, "utf-8"),
            SafeXmlEncoding.Utf16LittleEndian or SafeXmlEncoding.Utf16BigEndian =>
                StringComparer.OrdinalIgnoreCase.Equals(declared, "utf-16"),
            _ => false,
        };
        if (!declarationMatches ||
            (preambleLength == 0 && declared is not null &&
                !StringComparer.OrdinalIgnoreCase.Equals(declared, "utf-8")))
        {
            failure = "EncodingDeclarationMismatch";
            return false;
        }

        failure = null;
        return true;
    }

    private static string? ReadDeclaredEncoding(string xml)
    {
        if (!xml.StartsWith("<?xml", StringComparison.Ordinal))
        {
            return null;
        }

        int declarationEnd = xml.IndexOf("?>", StringComparison.Ordinal);
        if (declarationEnd < 0 || declarationEnd > 512)
        {
            return null;
        }

        ReadOnlySpan<char> declaration = xml.AsSpan(0, declarationEnd);
        int name = declaration.IndexOf("encoding", StringComparison.OrdinalIgnoreCase);
        if (name < 0)
        {
            return null;
        }

        ReadOnlySpan<char> tail = declaration[(name + "encoding".Length)..].TrimStart();
        if (tail.IsEmpty || tail[0] != '=')
        {
            return string.Empty;
        }

        tail = tail[1..].TrimStart();
        if (tail.Length < 3 || tail[0] is not ('\'' or '"'))
        {
            return string.Empty;
        }

        char quote = tail[0];
        int end = tail[1..].IndexOf(quote);
        return end < 0 ? string.Empty : tail.Slice(1, end).ToString();
    }

    private static long GetApproximateByteOffset(
        string xml,
        IXmlLineInfo lineInfo,
        SafeXmlEncoding encoding,
        bool hasUtf8Preamble)
    {
        if (!lineInfo.HasLineInfo())
        {
            return 0;
        }

        int line = 1;
        int character = 1;
        int index = 0;
        while (index < xml.Length &&
            (line < lineInfo.LineNumber || character < lineInfo.LinePosition))
        {
            if (xml[index++] == '\n')
            {
                line++;
                character = 1;
            }
            else
            {
                character++;
            }
        }

        return encoding == SafeXmlEncoding.Utf8
            ? checked((hasUtf8Preamble ? 3L : 0L) +
                _strictUtf8.GetByteCount(xml.AsSpan(0, index)))
            : checked(2L + (index * 2L));
    }

    private static bool IsCanonicalPath(string path) =>
        !string.IsNullOrWhiteSpace(path) &&
        path.Length <= 2_048 &&
        path[0] is not ('/' or '\\') &&
        !Path.IsPathRooted(path) &&
        !path.Contains('\\', StringComparison.Ordinal) &&
        path.Split('/').All(segment => segment is not ("" or "." or "..")) &&
        !path.Any(char.IsControl);

    private static SafeXmlReadResult Rejected(string reason, SafeXmlEncoding? encoding = null) =>
        new(SafeXmlOutcome.Rejected, encoding, null, reason, null);

    private static SafeXmlReadResult Bounded(string bound, SafeXmlEncoding? encoding = null) =>
        new(SafeXmlOutcome.BoundExceeded, encoding, bound, "BoundExceeded", null);
}

internal static class SafeXmlRangeIndex
{
    public static SafeXmlDirectChildRangeSet Create(
        string canonicalPath,
        string fileHash,
        SafeXmlEncoding encoding,
        ReadOnlySpan<byte> source,
        string xml)
    {
        // Range construction remains fail-closed until the lexical document has
        // an unambiguous root and balanced direct-child element boundaries.
        // Empty ranges are valid for an empty document element.
        int rootStart = xml.IndexOf('<');
        if (rootStart < 0)
        {
            return Rejected(canonicalPath, fileHash, encoding, "MissingDocumentElement");
        }

        if (xml.StartsWith("<?xml", StringComparison.Ordinal))
        {
            int declarationEnd = xml.IndexOf("?>", StringComparison.Ordinal);
            rootStart = declarationEnd < 0 ? -1 : xml.IndexOf('<', declarationEnd + 2);
        }

        if (rootStart < 0)
        {
            return Rejected(canonicalPath, fileHash, encoding, "MissingDocumentElement");
        }

        int rootOpenEnd = xml.IndexOf('>', rootStart + 1);
        int rootCloseStart = xml.LastIndexOf("</", StringComparison.Ordinal);
        if (rootOpenEnd < 0 || rootCloseStart < rootOpenEnd)
        {
            return Rejected(canonicalPath, fileHash, encoding, "AmbiguousDocumentRange");
        }

        // The safe projection has already validated the XML. Exact child ranges
        // are supplied only when the lexical scanner can prove balanced tags.
        List<(int Start, int End)> characterRanges = FindDirectChildren(xml, rootOpenEnd + 1, rootCloseStart);
        if (characterRanges.Count == 0 && xml.AsSpan(rootOpenEnd + 1, rootCloseStart - rootOpenEnd - 1).Trim().Length != 0)
        {
            return Rejected(canonicalPath, fileHash, encoding, "MixedOrAmbiguousRootContent");
        }

        var byteRanges = new List<SafeXmlDirectChildRange>(characterRanges.Count);
        for (int position = 0; position < characterRanges.Count; position++)
        {
            (int start, int end) = characterRanges[position];
            int byteStart = GetByteOffset(xml, start, encoding, source);
            int byteEnd = GetByteOffset(xml, end, encoding, source);
            int lineStart = CountLines(xml.AsSpan(0, start));
            int lineEnd = CountLines(xml.AsSpan(0, end));
            byteRanges.Add(new(
                byteStart,
                byteEnd,
                lineStart,
                lineEnd,
                fileHash,
                Convert.ToHexStringLower(SHA256.HashData(source[byteStart..byteEnd])),
                position + 1,
                characterRanges.Count));
        }

        return new SafeXmlDirectChildRangeSet(
            canonicalPath,
            fileHash,
            encoding,
            GetByteOffset(xml, rootStart, encoding, source),
            GetByteOffset(xml, rootCloseStart, encoding, source),
            byteRanges,
            SafeXmlOutcome.Parsed,
            null);
    }

    private static List<(int Start, int End)> FindDirectChildren(string xml, int start, int end)
    {
        var ranges = new List<(int Start, int End)>();
        int depth = 0;
        int childStart = -1;
        for (int index = start; index < end; index++)
        {
            if (xml[index] != '<')
            {
                if (depth == 0 && !char.IsWhiteSpace(xml[index]))
                {
                    return [];
                }

                continue;
            }

            if (xml.AsSpan(index).StartsWith("<!--", StringComparison.Ordinal))
            {
                int commentEnd = xml.IndexOf("-->", index + 4, StringComparison.Ordinal);
                if (commentEnd < 0) return [];
                index = commentEnd + 2;
                continue;
            }

            if (xml.AsSpan(index).StartsWith("<![CDATA[", StringComparison.Ordinal))
            {
                return [];
            }

            int tagEnd = FindTagEnd(xml, index + 1, end);
            if (tagEnd < 0) return [];
            bool closing = index + 1 < xml.Length && xml[index + 1] == '/';
            bool empty = tagEnd > index && xml[tagEnd - 1] == '/';
            if (!closing)
            {
                if (depth == 0) childStart = index;
                if (!empty) depth++;
                else if (depth == 0) ranges.Add((childStart, tagEnd + 1));
            }
            else
            {
                if (--depth < 0) return [];
                if (depth == 0) ranges.Add((childStart, tagEnd + 1));
            }

            index = tagEnd;
        }

        return depth == 0 ? ranges : [];
    }

    private static int FindTagEnd(string xml, int start, int limit)
    {
        char quote = '\0';
        for (int index = start; index < limit; index++)
        {
            char character = xml[index];
            if (quote == '\0' && character is '\'' or '"') quote = character;
            else if (quote == character) quote = '\0';
            else if (quote == '\0' && character == '>') return index;
        }

        return -1;
    }

    private static int GetByteOffset(
        string xml,
        int characterOffset,
        SafeXmlEncoding encoding,
        ReadOnlySpan<byte> source)
    {
        int preamble = encoding switch
        {
            SafeXmlEncoding.Utf8 when source.Length >= 3 &&
                source[0] == 0xef && source[1] == 0xbb && source[2] == 0xbf => 3,
            SafeXmlEncoding.Utf8 => 0,
            SafeXmlEncoding.Utf16LittleEndian or SafeXmlEncoding.Utf16BigEndian => 2,
            _ => 0,
        };
        return checked(preamble + (encoding == SafeXmlEncoding.Utf8
            ? Encoding.UTF8.GetByteCount(xml.AsSpan(0, characterOffset))
            : characterOffset * 2));
    }

    private static int CountLines(ReadOnlySpan<char> text)
    {
        int line = 1;
        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] == '\r')
            {
                if (index + 1 < text.Length && text[index + 1] == '\n') index++;
                line++;
            }
            else if (text[index] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    private static SafeXmlDirectChildRangeSet Rejected(
        string path,
        string hash,
        SafeXmlEncoding encoding,
        string reason) =>
        new(path, hash, encoding, 0, 0, [], SafeXmlOutcome.Rejected, reason);
}
