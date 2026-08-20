using System.Collections.ObjectModel;

namespace Vulgata.Infrastructure.CommunicationDetection;

public enum SafeXmlOutcome
{
    Parsed,
    Rejected,
    BoundExceeded,
}

public enum SafeXmlEncoding
{
    Utf8,
    Utf16LittleEndian,
    Utf16BigEndian,
}

public sealed record SafeXmlAttribute(
    string NamespaceUri,
    string LocalName,
    string Value);

public sealed record SafeXmlNode(
    string NamespaceUri,
    string LocalName,
    IReadOnlyList<SafeXmlAttribute> Attributes,
    string Text,
    XmlEvidenceLocator Locator,
    int Depth);

public sealed record SafeXmlDirectChildRange(
    long ByteStart,
    long ByteEndExclusive,
    int LineStart,
    int LineEnd,
    string FileHash,
    string SourceHash,
    int SegmentOrdinal,
    int SegmentCount);

public sealed class SafeXmlDirectChildRangeSet
{
    public SafeXmlDirectChildRangeSet(
        string canonicalPath,
        string fileHash,
        SafeXmlEncoding encoding,
        long documentByteStart,
        long documentByteEndExclusive,
        IEnumerable<SafeXmlDirectChildRange> ranges,
        SafeXmlOutcome outcome,
        string? skipReason)
    {
        CanonicalPath = canonicalPath;
        FileHash = fileHash;
        Encoding = encoding;
        DocumentByteStart = documentByteStart;
        DocumentByteEndExclusive = documentByteEndExclusive;
        Ranges = new ReadOnlyCollection<SafeXmlDirectChildRange>([.. ranges]);
        Outcome = outcome;
        SkipReason = skipReason;
    }

    public string CanonicalPath { get; }
    public string FileHash { get; }
    public SafeXmlEncoding Encoding { get; }
    public long DocumentByteStart { get; }
    public long DocumentByteEndExclusive { get; }
    public IReadOnlyList<SafeXmlDirectChildRange> Ranges { get; }
    public SafeXmlOutcome Outcome { get; }
    public string? SkipReason { get; }
    public string? BoundFailure => Outcome == SafeXmlOutcome.BoundExceeded ? SkipReason : null;
    public bool Skipped => Outcome != SafeXmlOutcome.Parsed;
}

public sealed class SafeXmlProjection
{
    public SafeXmlProjection(
        string canonicalPath,
        string fileHash,
        SafeXmlEncoding encoding,
        IEnumerable<SafeXmlNode> nodes,
        SafeXmlDirectChildRangeSet directChildRanges)
    {
        CanonicalPath = canonicalPath;
        FileHash = fileHash;
        Encoding = encoding;
        Nodes = new ReadOnlyCollection<SafeXmlNode>([.. nodes]);
        DirectChildRanges = directChildRanges;
    }

    public string CanonicalPath { get; }
    public string FileHash { get; }
    public SafeXmlEncoding Encoding { get; }
    public IReadOnlyList<SafeXmlNode> Nodes { get; }
    public SafeXmlDirectChildRangeSet DirectChildRanges { get; }

    public static SafeXmlReadResult Project(string canonicalPath, ReadOnlyMemory<byte> source) =>
        SafeXmlReader.Read(canonicalPath, source);
}

public sealed record SafeXmlReadResult(
    SafeXmlOutcome Outcome,
    SafeXmlEncoding? Encoding,
    string? BoundFailure,
    string? FailureCode,
    SafeXmlProjection? Projection);
