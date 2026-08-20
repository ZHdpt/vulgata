namespace Vulgata.Infrastructure.CommunicationDetection;

public sealed record XmlEvidenceLocator(
    string RelativePath,
    int Line,
    int Column,
    long ByteOffset)
{
    public static XmlEvidenceLocator Locate(
        string relativePath,
        int line,
        int column,
        long byteOffset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (line < 1 || column < 1 || byteOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(line));
        }

        return new XmlEvidenceLocator(relativePath, line, column, byteOffset);
    }

    public override string ToString() => $"{RelativePath}#L{Line}:C{Column}:B{ByteOffset}";
}
