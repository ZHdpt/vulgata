namespace Vulgata.Core.Entities;

public sealed class ScanRunLanguageSummary
{
    private ScanRunLanguageSummary()
    {
    }

    public ScanRunLanguageSummary(
        Guid scanRunId,
        Guid repositoryId,
        string language,
        int distinctFileCount)
    {
        if (scanRunId == Guid.Empty || repositoryId == Guid.Empty)
        {
            throw new ArgumentException("扫描运行和仓库标识不能为空。");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(distinctFileCount, 1);
        ScanRunId = scanRunId;
        RepositoryId = repositoryId;
        Language = CodeUnit.NormalizeLanguage(language);
        DistinctFileCount = distinctFileCount;
    }

    public Guid ScanRunId { get; private set; }
    public Guid RepositoryId { get; private set; }
    public string Language { get; private set; } = string.Empty;
    public int DistinctFileCount { get; private set; }
    public ScanRun? ScanRun { get; private set; }
}
