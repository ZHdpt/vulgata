namespace Vulgata.Core.Entities;

public enum CodeUnitType
{
    Function,
    Class,
    Method,
    Namespace,
    Module,
    File,
}

public enum RangeConfidence
{
    Exact,
    Approximate,
    Heuristic,
}

public enum CodeUnitStatus
{
    Extracted,
    Pending,
    Dispatched,
    Completed,
    Failed,
    Skipped,
    Filtered,
}

public sealed class CodeUnit
{
    private CodeUnit()
    {
    }

    public CodeUnit(
        Guid scanRunId,
        Guid repositoryId,
        string filePath,
        string fileIdentityHash,
        string sourceHash,
        int lineStart,
        int lineEnd,
        string symbolName,
        CodeUnitType unitType,
        RangeConfidence rangeConfidence,
        string? language)
    {
        if (scanRunId == Guid.Empty || repositoryId == Guid.Empty)
        {
            throw new ArgumentException("扫描运行和仓库标识不能为空。");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(lineStart, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(lineEnd, lineStart);
        Id = Guid.NewGuid();
        ScanRunId = scanRunId;
        RepositoryId = repositoryId;
        FilePath = NormalizePath(filePath);
        FileIdentityHash = RequireSha256(fileIdentityHash);
        SourceHash = RequireSha256(sourceHash);
        SymbolName = RequireText(symbolName, 512);
        LineStart = lineStart;
        LineEnd = lineEnd;
        UnitType = unitType;
        RangeConfidence = rangeConfidence;
        Language = string.IsNullOrWhiteSpace(language) ? null : language.Trim();
        Status = CodeUnitStatus.Pending;
    }

    public Guid Id { get; private set; }
    public Guid ScanRunId { get; private set; }
    public Guid RepositoryId { get; private set; }
    public string FilePath { get; private set; } = string.Empty;
    public string FileIdentityHash { get; private set; } = string.Empty;
    public string SourceHash { get; private set; } = string.Empty;
    public int LineStart { get; private set; }
    public int LineEnd { get; private set; }
    public string SymbolName { get; private set; } = string.Empty;
    public CodeUnitType UnitType { get; private set; }
    public RangeConfidence RangeConfidence { get; private set; }
    public string? Language { get; private set; }
    public CodeUnitStatus Status { get; private set; }
    public ScanRun? ScanRun { get; private set; }
    public Repository? Repository { get; private set; }

    internal static string NormalizePath(string path)
    {
        string value = RequireText(path, 2_048).Replace('\\', '/').TrimStart('/');
        if (Path.IsPathRooted(value) || value.Split('/').Any(segment => segment == ".."))
        {
            throw new ArgumentException("文件路径必须是仓库相对路径。", nameof(path));
        }

        return value;
    }

    private static string RequireSha256(string value)
    {
        string normalized = RequireText(value, 64).ToLowerInvariant();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("哈希值必须是 SHA-256。");
        }

        return normalized;
    }

    private static string RequireText(string value, int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException("字段长度超过限制。");
        }

        return normalized;
    }
}
