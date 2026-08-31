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

        return new PreparedJavaSourceResult(ranges, null)
        {
            Evidence = symbols.Evidence.ToArray(),
        };
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

    public static PreparedJavaSemanticOwnerResult BindSemanticOwner(
        ReadOnlyMemory<byte> strictUtf8Source,
        string expectedFileHash,
        string relativePath,
        string fullyQualifiedSymbol,
        int selectorLine,
        PreparedJavaSourceResult observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (string.IsNullOrWhiteSpace(fullyQualifiedSymbol) || selectorLine < 1 ||
            !StringComparer.OrdinalIgnoreCase.Equals(Path.GetExtension(relativePath), ".java") ||
            observation.Failure is not null)
        {
            return PreparedJavaSemanticOwnerResult.Unavailable("reviewed_owner_invalid");
        }

        string simpleName = fullyQualifiedSymbol.Split('.').Last();
        AnalysisSymbolEvidence[] matches = observation.Evidence
            .Where(value => value.Provenance == AnalysisSymbolHierarchyProvenance.RootDocumentSymbol &&
                StringComparer.Ordinal.Equals(value.RelativePath, relativePath) &&
                StringComparer.Ordinal.Equals(value.ExactUtf16Range?.TypeName, simpleName) &&
                value.ExactUtf16Range is not null && ContainsLine(value.ExactUtf16Range, selectorLine))
            .ToArray();
        if (matches.Length != 1)
        {
            return PreparedJavaSemanticOwnerResult.Unavailable("reviewed_owner_ambiguous");
        }

        int logicalLines = PreparedSourceRangePlanner.CountLogicalLines(strictUtf8Source.Span);
        PreparedSourceSplitPlan split;
        if (strictUtf8Source.Length <= PreparedSourceRangePlanner.MaximumRawBytes &&
            logicalLines is >= 1 and <= PreparedSourceRangePlanner.MaximumLogicalLines)
        {
            string actualFileHash = Convert.ToHexStringLower(
                System.Security.Cryptography.SHA256.HashData(strictUtf8Source.Span));
            if (!StringComparer.Ordinal.Equals(actualFileHash, expectedFileHash))
            {
                return PreparedJavaSemanticOwnerResult.Unavailable("reviewed_owner_file_hash_mismatch");
            }

            PreparedSourceRange range = PreparedSourceRangePlanner.Create(
                strictUtf8Source.Span,
                0,
                strictUtf8Source.Length,
                1);
            split = new(
                PreparedRangeOutcome.Exact,
                null,
                [new PreparedJavaRange(
                    range.ByteStart,
                    range.ByteEndExclusive,
                    range.LineStart,
                    range.LineEnd,
                    actualFileHash,
                    range.SourceHash,
                    1,
                    1,
                    PreparedRangeOutcome.Exact,
                    null)]);
        }
        else
        {
            split = PreparedSourceRangePlanner.PlanJavaRanges(
                strictUtf8Source,
                expectedFileHash,
                observation.Ranges);
        }

        if (split.Outcome != PreparedRangeOutcome.Exact)
        {
            return PreparedJavaSemanticOwnerResult.Unavailable("reviewed_owner_split_unavailable");
        }

        PreparedJavaRange[] owners = split.Ranges
            .Where(range => selectorLine >= range.LineStart && selectorLine <= range.LineEnd)
            .ToArray();
        if (owners.Length != 1)
        {
            return PreparedJavaSemanticOwnerResult.Unavailable("reviewed_owner_segment_ambiguous");
        }

        int ownerOrdinal = owners[0].SegmentOrdinal;
        PreparedJavaRange[] bound = [.. split.Ranges.Select(range => range with
        {
            SymbolName = range.SegmentOrdinal == ownerOrdinal ? fullyQualifiedSymbol : relativePath,
        })];
        return new(PreparedRangeOutcome.Exact, null, Array.AsReadOnly(bound), ownerOrdinal);
    }

    private static bool ContainsLine(PreparedJdtSymbolSpan span, int oneBasedLine)
    {
        int zeroBasedLine = oneBasedLine - 1;
        int inclusiveEndLine = span.EndCharacter == 0 ? span.EndLine - 1 : span.EndLine;
        return zeroBasedLine >= span.StartLine && zeroBasedLine <= inclusiveEndLine;
    }
}

public sealed record PreparedJavaSemanticOwnerResult(
    PreparedRangeOutcome Outcome,
    string? FailureCode,
    IReadOnlyList<PreparedJavaRange> Ranges,
    int? OwningSegmentOrdinal)
{
    public static PreparedJavaSemanticOwnerResult Unavailable(string failureCode) =>
        new(PreparedRangeOutcome.Skipped, failureCode, [], null);
}
