namespace Vulgata.Core.Entities;

public enum FrameworkConfidence
{
    High,
    Medium,
}

public sealed class CodeUnitFramework
{
    private CodeUnitFramework()
    {
    }

    public CodeUnitFramework(
        Guid codeUnitId,
        Guid scanRunId,
        Guid repositoryId,
        string frameworkName,
        FrameworkConfidence confidence,
        string detectionSource,
        DateTimeOffset createdAt)
    {
        if (codeUnitId == Guid.Empty || scanRunId == Guid.Empty || repositoryId == Guid.Empty)
        {
            throw new ArgumentException("代码单元、扫描运行和仓库标识不能为空。");
        }

        Id = Guid.NewGuid();
        CodeUnitId = codeUnitId;
        ScanRunId = scanRunId;
        RepositoryId = repositoryId;
        FrameworkName = NormalizeFrameworkName(frameworkName);
        Confidence = confidence;
        DetectionSource = NormalizeDetectionSource(detectionSource);
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid CodeUnitId { get; private set; }
    public Guid ScanRunId { get; private set; }
    public Guid RepositoryId { get; private set; }
    public string FrameworkName { get; private set; } = string.Empty;
    public FrameworkConfidence Confidence { get; private set; }
    public string DetectionSource { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public CodeUnit? CodeUnit { get; private set; }
    public ScanRun? ScanRun { get; private set; }

    private static string NormalizeFrameworkName(string frameworkName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frameworkName);
        string value = frameworkName.Trim().ToLowerInvariant();
        if (value.Length > 64 ||
            value[0] is < 'a' or > 'z' ||
            value.Any(character =>
                character is not (>= 'a' and <= 'z') &&
                character is not (>= '0' and <= '9') &&
                character != '-'))
        {
            throw new ArgumentException("框架名称格式无效。", nameof(frameworkName));
        }

        return value;
    }

    private static string NormalizeDetectionSource(string detectionSource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detectionSource);
        string value = detectionSource.Trim().Replace('\\', '/');
        if (value.Length > 4_096)
        {
            throw new ArgumentException("检测依据长度超过限制。", nameof(detectionSource));
        }

        int locatorIndex = value.IndexOf('#', StringComparison.Ordinal);
        string artifactPath = locatorIndex < 0 ? value : value[..locatorIndex];
        _ = CodeUnit.NormalizePath(artifactPath);
        if (locatorIndex <= 0 || locatorIndex == value.Length - 1)
        {
            throw new ArgumentException("检测依据必须包含受支持的精确定位器。", nameof(detectionSource));
        }

        return value;
    }
}
