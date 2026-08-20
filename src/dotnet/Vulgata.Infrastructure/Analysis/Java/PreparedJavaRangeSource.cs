using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Analysis.Java;

public sealed class PreparedJavaRangeSource(
    IJavaAnalysisSessionFactory sessionFactory) : IPreparedTopLevelSymbolSource
{
    private const int MaximumTopLevelRanges = 1_000;
    private const int MaximumResponseBytes = 1_048_576;

    public async Task<PreparedJavaSourceResult> GetRangesAsync(
        AnalysisScope scope,
        string relativePath,
        string fileHash,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        _ = RequireRelativePath(relativePath);
        _ = RequireSha256(fileHash);
        cancellationToken.ThrowIfCancellationRequested();

        await using IJavaAnalysisSession session = await sessionFactory
            .OpenAsync(scope, cancellationToken)
            .ConfigureAwait(false);
        JavaNavigationResult symbols = await session.GetDocumentSymbolsAsync(
            scope,
            new JavaNavigationRequest(
                relativePath,
                null,
                new AnalysisOperationBounds(MaximumTopLevelRanges, MaximumResponseBytes)),
            cancellationToken).ConfigureAwait(false);
        if (symbols.Status != AnalysisOperationStatus.Succeeded || symbols.Failure is not null)
        {
            return new PreparedJavaSourceResult([], symbols.Failure ?? Failure(
                AnalysisPortFailureReason.NotReady,
                "jdt_ranges_not_ready"));
        }

        PreparedJdtSymbolSpan[] ranges = symbols.Evidence
            .Where(evidence => string.Equals(
                evidence.RelativePath,
                relativePath,
                StringComparison.Ordinal))
            .Select(evidence => evidence.ExactUtf16Range)
            .OfType<PreparedJdtSymbolSpan>()
            .OrderBy(range => range.StartLine)
            .ThenBy(range => range.StartCharacter)
            .ThenBy(range => range.EndLine)
            .ThenBy(range => range.EndCharacter)
            .ToArray();
        if (ranges.Length == 0 || ranges.Length != symbols.Evidence.Count)
        {
            return new PreparedJavaSourceResult([], Failure(
                AnalysisPortFailureReason.MalformedResponse,
                "jdt_ranges_incomplete"));
        }

        return new PreparedJavaSourceResult(ranges, null);
    }

    private static string RequireRelativePath(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (Path.IsPathRooted(value) ||
            value.Contains('\\', StringComparison.Ordinal) ||
            value.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException("A canonical relative path is required.", nameof(value));
        }

        return value;
    }

    private static string RequireSha256(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length != 64 ||
            value.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("An exact lowercase SHA-256 is required.", nameof(value));
        }

        return value;
    }

    private static AnalysisPortFailure Failure(
        AnalysisPortFailureReason reason,
        string code) => new(reason, code, code.Replace('_', ' '));
}
