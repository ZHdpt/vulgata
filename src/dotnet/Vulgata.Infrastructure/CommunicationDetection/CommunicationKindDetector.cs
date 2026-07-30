using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.CommunicationDetection;

public sealed class CommunicationKindDetector(
    CommunicationEvidenceRuleCatalog catalog) : IPreScanCommunicationDetector
{
    private const long _maximumArtifactBytes = 8L * 1024 * 1024;
    private const long _maximumAggregateArtifactBytes = 64L * 1024 * 1024;
    private const int _maximumSupportedArtifacts = 1_000;
    private const int _maximumSourceRangeBytes = 64 * 1024;
    private const int _maximumAggregateSourceRangeBytes = 16 * 1024 * 1024;
    private const int _maximumCandidateFacts = 250_000;
    private const int _maximumRetainedClassifications = 100;
    private readonly CommunicationEvidenceRuleCatalog _catalog =
        catalog ?? throw new ArgumentNullException(nameof(catalog));

    public async Task<PreScanResultDraft> DetectAsync(
        RepositoryProfileDraft profile,
        string profiledRevision,
        ICommittedRepositorySnapshotLease committedSnapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(profiledRevision);
        ArgumentNullException.ThrowIfNull(committedSnapshot);
        ValidateIdentity(profile, profiledRevision, committedSnapshot);
        await committedSnapshot.VerifyIdentityAsync(cancellationToken);

        var candidates = new List<ClassificationCandidate>();
        MatchPersistedAnomalies(profile, candidates);
        MatchPersistedFrameworkFacts(profile, candidates);
        var aggregateRangeBytes = 0;
        foreach (CodeUnit codeUnit in profile.CodeUnits)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SourceRangeReadResult range = await ReadSourceRangeAsync(
                committedSnapshot.RootPath,
                codeUnit,
                cancellationToken);
            if (range.Source is null)
            {
                AddCandidate(candidates, CreateWarning(
                    range.SafeRelativePath,
                    range.Rule,
                    range.Selector));
                continue;
            }

            if (aggregateRangeBytes > _maximumAggregateSourceRangeBytes - range.ByteCount)
            {
                AddCandidate(candidates, CreateWarning(
                    range.SafeRelativePath,
                    EvidenceRule.BoundExceeded,
                    "range-bound"));
                continue;
            }

            aggregateRangeBytes += range.ByteCount;
            MatchDirectAnnotations(codeUnit, range.Source, candidates);
            MatchImports(codeUnit, range.Source, candidates);
        }

        await MatchCommittedConfigurationsAsync(
            committedSnapshot.RootPath,
            candidates,
            cancellationToken);

        ClassificationCandidate[] normalized = candidates
            .GroupBy(candidate => candidate.Identity, StringComparer.Ordinal)
            .Select(group => group.First() with
            {
                OccurrenceCount = checked(group.Sum(candidate => candidate.OccurrenceCount)),
            })
            .OrderBy(candidate => candidate.Level)
            .ThenBy(candidate => candidate.Kind)
            .ThenBy(candidate => candidate.Rule)
            .ThenBy(candidate => candidate.RelativePath, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Selector, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Confidence)
            .ThenBy(candidate => candidate.Provenance)
            .ToArray();
        bool evidenceTruncated = normalized.Length > _maximumRetainedClassifications;
        if (evidenceTruncated)
        {
            normalized =
            [
                .. normalized,
                CreateWarning(
                    CommunicationKindClassification.UnknownRelativePath,
                    EvidenceRule.BoundExceeded,
                    "sampling-bound"),
            ];
            Array.Sort(normalized, ClassificationCandidateComparer.Instance);
        }

        ConfirmedCommunicationKinds confirmedKinds = normalized
            .Where(candidate => candidate.Level == ClassificationLevel.Confirmed)
            .Aggregate(
                ConfirmedCommunicationKinds.None,
                static (kinds, candidate) =>
                    kinds | ToConfirmedFlag(candidate.Kind));
        int confirmedCount = CountOccurrences(normalized, ClassificationLevel.Confirmed);
        int hintCount = CountOccurrences(normalized, ClassificationLevel.Hint);
        int warningCount = CountOccurrences(normalized, ClassificationLevel.Warning);
        string aggregateFingerprint = ComputeAggregateFingerprint(
            profiledRevision,
            confirmedKinds,
            confirmedCount,
            hintCount,
            warningCount,
            normalized);
        CommunicationKindClassification[] classifications = SelectRetainedSamples(
                normalized,
                confirmedKinds,
                evidenceTruncated)
            .Select(candidate => CreateClassification(profile.ScanRun.Id, candidate))
            .ToArray();

        await committedSnapshot.VerifyIdentityAsync(cancellationToken);
        ValidateIdentity(profile, profiledRevision, committedSnapshot);
        PreScanResult result = PreScanResult.CreateApproved(
            profile.ScanRun.RepositoryId,
            profile.ScanRun.Id,
            profiledRevision,
            profile.ScanRun.CompletedAt!.Value,
            aggregateFingerprint,
            warningCount == 0
                ? "预扫描已批准——继续进入顺序演示运行器"
                : "预扫描已批准并保留审慎警告——继续进入顺序演示运行器",
            confirmedKinds,
            confirmedCount,
            hintCount,
            warningCount,
            evidenceTruncated,
            classifications);
        return PreScanResultDraft.Create(result);
    }

    private void MatchDirectAnnotations(
        CodeUnit codeUnit,
        string source,
        List<ClassificationCandidate> candidates)
    {
        string[] lines = source.Split('\n');
        foreach (CommunicationEvidenceRule rule in _catalog.Rules.Where(
                     rule => rule.Category is
                         CommunicationEvidenceCategory.DirectAnnotation or
                         CommunicationEvidenceCategory.ControllerAnnotation or
                         CommunicationEvidenceCategory.ClientAnnotation))
        {
            foreach (string token in rule.Tokens)
            {
                for (var index = 0; index < lines.Length; index++)
                {
                    string line = lines[index].TrimStart();
                    int occurrenceCount = CountDirectInvocations(line, token);
                    for (var occurrence = 0; occurrence < occurrenceCount; occurrence++)
                    {
                        AddCandidate(candidates, CreateDirectCandidate(
                            rule,
                            codeUnit.FilePath,
                            $"L{codeUnit.LineStart + index}"));
                    }
                }
            }
        }
    }

    private async Task MatchCommittedConfigurationsAsync(
        string snapshotRoot,
        List<ClassificationCandidate> candidates,
        CancellationToken cancellationToken)
    {
        List<string> relativePaths =
            EnumerateConfigurationArtifactPaths(snapshotRoot);
        if (relativePaths.Count > _maximumSupportedArtifacts)
        {
            AddCandidate(candidates, CreateWarning(
                CommunicationKindClassification.UnknownRelativePath,
                EvidenceRule.BoundExceeded,
                "artifact-count-bound"));
            return;
        }

        long aggregateBytes = 0;
        foreach (string relativePath in relativePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolveContainedPath(
                    snapshotRoot,
                    relativePath,
                    out string fullPath,
                    out string safeRelativePath))
            {
                AddCandidate(candidates, CreateWarning(
                    safeRelativePath,
                    EvidenceRule.UnsafePath,
                    "unsafe-path"));
                continue;
            }

            try
            {
                if (!File.Exists(fullPath) ||
                    HasReparsePoint(snapshotRoot, fullPath))
                {
                    AddCandidate(candidates, CreateWarning(
                        safeRelativePath,
                        EvidenceRule.UnsafePath,
                        "unsafe-path"));
                    continue;
                }

                var file = new FileInfo(fullPath);
                if (file.Length > _maximumArtifactBytes ||
                    aggregateBytes > _maximumAggregateArtifactBytes - file.Length)
                {
                    AddCandidate(candidates, CreateWarning(
                        safeRelativePath,
                        EvidenceRule.BoundExceeded,
                        "artifact-bound"));
                    continue;
                }

                aggregateBytes += file.Length;
                var artifactCandidates = new List<ClassificationCandidate>();
                await MatchConfigurationArtifactAsync(
                    fullPath,
                    safeRelativePath,
                    artifactCandidates,
                    cancellationToken);
                foreach (ClassificationCandidate candidate in artifactCandidates)
                {
                    AddCandidate(candidates, candidate);
                }
            }
            catch (Exception exception) when (
                exception is IOException or
                    UnauthorizedAccessException or
                    DecoderFallbackException)
            {
                AddCandidate(candidates, CreateWarning(
                    safeRelativePath,
                    EvidenceRule.MalformedEvidence,
                    "unreadable-artifact"));
            }
        }
    }

    private async Task MatchConfigurationArtifactAsync(
        string fullPath,
        string relativePath,
        List<ClassificationCandidate> candidates,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true);
        var lineNumber = 0;
        string? sourceLine;
        while ((sourceLine = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            lineNumber++;
            MatchConfigurationLine(
                relativePath,
                sourceLine.TrimStart(),
                lineNumber,
                candidates);
        }
    }

    private void MatchConfigurationLine(
        string relativePath,
        string line,
        int lineNumber,
        List<ClassificationCandidate> candidates)
    {
        foreach (CommunicationEvidenceRule rule in _catalog.Rules.Where(
                     rule => rule.Category ==
                         CommunicationEvidenceCategory.MatchedConfiguration))
        {
            foreach (string key in rule.Keys)
            {
                if (!line.StartsWith(key, StringComparison.Ordinal) ||
                    line.Length == key.Length ||
                    line[key.Length] is not ('=' or ':'))
                {
                    continue;
                }

                AddCandidate(candidates, new(
                    rule.Kind,
                    ClassificationLevel.Confirmed,
                    ConfidenceBand.High,
                    rule.Kind == CommunicationKind.Rpc
                        ? EvidenceRule.RpcMatchedConfiguration
                        : EvidenceRule.HttpMatchedConfiguration,
                    EvidenceProvenance.MatchedConfiguration,
                    relativePath,
                    $"L{lineNumber}",
                    OccurrenceCount: 1));
            }
        }
    }

    private static List<string> EnumerateConfigurationArtifactPaths(
        string snapshotRoot)
    {
        string normalizedRoot = Path.GetFullPath(snapshotRoot);
        if (!Directory.Exists(normalizedRoot) ||
            (File.GetAttributes(normalizedRoot) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                "Committed configuration snapshot is unavailable.");
        }

        var relativePaths = new List<string>();
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(normalizedRoot);
        while (pendingDirectories.TryPop(out string? directory))
        {
            foreach (string entryPath in Directory.EnumerateFileSystemEntries(
                         directory,
                         "*",
                         SearchOption.TopDirectoryOnly))
            {
                FileAttributes attributes = File.GetAttributes(entryPath);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidOperationException(
                        "Committed configuration snapshot contains a reparse point.");
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pendingDirectories.Push(entryPath);
                    continue;
                }

                string relativePath = Path.GetRelativePath(
                        normalizedRoot,
                        entryPath)
                    .Replace(Path.DirectorySeparatorChar, '/');
                if (!IsSupportedConfigurationArtifact(relativePath))
                {
                    continue;
                }

                relativePaths.Add(relativePath);
                if (relativePaths.Count > _maximumSupportedArtifacts)
                {
                    return relativePaths;
                }
            }
        }

        relativePaths.Sort(StringComparer.Ordinal);
        return relativePaths;
    }

    private static bool IsSupportedConfigurationArtifact(string relativePath) =>
        Path.GetFileName(relativePath) is string fileName &&
        (fileName.Equals("application.properties", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("application.yml", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("application.yaml", StringComparison.OrdinalIgnoreCase));

    private static int CountDirectInvocations(string line, string token)
    {
        if (line.Length == 0 || token.Length < 2)
        {
            return 0;
        }

        string expectedName = token[0] switch
        {
            '@' => token[1..],
            '[' => token.EndsWith(']')
                ? token[1..^1]
                : token[1..],
            _ => string.Empty,
        };
        if (expectedName.Length == 0)
        {
            return 0;
        }

        return token[0] == '@'
            ? CountJavaAnnotationInvocations(line, expectedName)
            : CountDotNetAttributeInvocations(line, expectedName);
    }

    private static int CountJavaAnnotationInvocations(
        string line,
        string expectedName)
    {
        var count = 0;
        var index = 0;
        while (true)
        {
            SkipWhitespace(line, ref index);
            if (index >= line.Length || line[index] != '@')
            {
                return count;
            }

            index++;
            int nameStart = index;
            while (index < line.Length && IsIdentifierCharacter(line[index]))
            {
                index++;
            }

            if (index == nameStart)
            {
                return count;
            }

            string name = line[nameStart..index];
            SkipWhitespace(line, ref index);
            if (index < line.Length && line[index] == '(' &&
                !TrySkipBalancedInvocation(line, ref index, '(', ')'))
            {
                return count;
            }

            if (StringComparer.Ordinal.Equals(name, expectedName))
            {
                count++;
            }

            SkipWhitespace(line, ref index);
            if (index >= line.Length || line[index] != '@')
            {
                return count;
            }
        }
    }

    private static int CountDotNetAttributeInvocations(
        string line,
        string expectedName)
    {
        var count = 0;
        var index = 0;
        while (true)
        {
            SkipWhitespace(line, ref index);
            if (index >= line.Length || line[index] != '[')
            {
                return count;
            }

            int contentStart = index + 1;
            if (!TrySkipBalancedInvocation(line, ref index, '[', ']'))
            {
                return count;
            }

            string content = line[contentStart..(index - 1)];
            count += CountMatchingAttributeNames(content, expectedName);
        }
    }

    private static int CountMatchingAttributeNames(
        string content,
        string expectedName)
    {
        var count = 0;
        var segmentStart = 0;
        var parenthesisDepth = 0;
        char quote = '\0';
        var escaped = false;
        for (var index = 0; index <= content.Length; index++)
        {
            if (index == content.Length)
            {
                count += AttributeSegmentMatches(
                    content[segmentStart..],
                    expectedName)
                    ? 1
                    : 0;
                break;
            }

            char character = content[index];
            if (quote != '\0')
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (character is '"' or '\'')
            {
                quote = character;
            }
            else if (character == '(')
            {
                parenthesisDepth++;
            }
            else if (character == ')')
            {
                if (parenthesisDepth == 0)
                {
                    return 0;
                }

                parenthesisDepth--;
            }
            else if (character == ',' && parenthesisDepth == 0)
            {
                count += AttributeSegmentMatches(
                    content[segmentStart..index],
                    expectedName)
                    ? 1
                    : 0;
                segmentStart = index + 1;
            }
        }

        return quote == '\0' && parenthesisDepth == 0 ? count : 0;
    }

    private static bool AttributeSegmentMatches(
        string segment,
        string expectedName)
    {
        var index = 0;
        SkipWhitespace(segment, ref index);
        int nameStart = index;
        while (index < segment.Length && IsIdentifierCharacter(segment[index]))
        {
            index++;
        }

        if (!segment.AsSpan(nameStart, index - nameStart).SequenceEqual(expectedName))
        {
            return false;
        }

        SkipWhitespace(segment, ref index);
        if (index == segment.Length)
        {
            return true;
        }

        return segment[index] == '(' &&
            TrySkipBalancedInvocation(segment, ref index, '(', ')') &&
            segment[index..].Trim().Length == 0;
    }

    private static bool TrySkipBalancedInvocation(
        string value,
        ref int index,
        char opening,
        char closing)
    {
        if (index >= value.Length || value[index] != opening)
        {
            return false;
        }

        var depth = 0;
        char quote = '\0';
        var escaped = false;
        for (; index < value.Length; index++)
        {
            char character = value[index];
            if (quote != '\0')
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (character is '"' or '\'')
            {
                quote = character;
            }
            else if (character == opening)
            {
                depth++;
            }
            else if (character == closing && --depth == 0)
            {
                index++;
                return true;
            }
        }

        return false;
    }

    private static void SkipWhitespace(string value, ref int index)
    {
        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            index++;
        }
    }

    private void MatchImports(
        CodeUnit codeUnit,
        string source,
        List<ClassificationCandidate> candidates)
    {
        string[] lines = source.Split('\n');
        foreach (CommunicationEvidenceRule rule in _catalog.Rules.Where(
                     rule => rule.Category == CommunicationEvidenceCategory.Import))
        {
            foreach (string token in rule.Tokens)
            {
                for (var index = 0; index < lines.Length; index++)
                {
                    string line = lines[index].TrimStart();
                    if (!IsImportOrNamespaceLine(line) ||
                        !TryFindExactToken(line, token, 0, out _))
                    {
                        continue;
                    }

                    AddCandidate(candidates, CreateHintCandidate(
                        rule.Kind,
                        EvidenceProvenance.Import,
                        codeUnit.FilePath,
                        $"L{codeUnit.LineStart + index}"));
                }
            }
        }
    }

    private void MatchPersistedFrameworkFacts(
        RepositoryProfileDraft profile,
        List<ClassificationCandidate> candidates)
    {
        IReadOnlyDictionary<Guid, CodeUnit> unitsById =
            profile.CodeUnits.ToDictionary(unit => unit.Id);
        foreach (CodeUnitFramework framework in profile.CodeUnitFrameworks)
        {
            if (!unitsById.TryGetValue(framework.CodeUnitId, out CodeUnit? owner))
            {
                continue;
            }

            foreach (CommunicationEvidenceRule rule in _catalog.Rules.Where(
                         rule => rule.Category == CommunicationEvidenceCategory.FrameworkFact &&
                             rule.Tokens.Contains(framework.FrameworkName, StringComparer.Ordinal)))
            {
                AddCandidate(candidates, CreateHintCandidate(
                    rule.Kind,
                    EvidenceProvenance.FrameworkFact,
                    owner.FilePath,
                    $"framework:{rule.Id}"));
            }

            int locatorSeparator = framework.DetectionSource.IndexOf('#', StringComparison.Ordinal);
            string artifactPath = locatorSeparator > 0
                ? framework.DetectionSource[..locatorSeparator]
                : string.Empty;
            if (!IsSafeRelativePath(artifactPath))
            {
                continue;
            }

            foreach (CommunicationEvidenceRule rule in _catalog.Rules.Where(
                         rule => rule.Category == CommunicationEvidenceCategory.PackageManifest &&
                             rule.Tokens.Any(token =>
                                 DetectionSourceContainsPackage(
                                     framework.DetectionSource,
                                     token))))
            {
                AddCandidate(candidates, CreateHintCandidate(
                    rule.Kind,
                    EvidenceProvenance.PackageManifest,
                    artifactPath,
                    $"package:{rule.Id}"));
            }
        }
    }

    private static void MatchPersistedAnomalies(
        RepositoryProfileDraft profile,
        List<ClassificationCandidate> candidates)
    {
        foreach (ScanCandidateOutcome outcome in profile.CandidateOutcomes)
        {
            EvidenceRule? rule = outcome.ReasonOrCategory switch
            {
                ScanCandidateOutcomeReasons.FrameworkEvidenceMalformed =>
                    EvidenceRule.MalformedEvidence,
                ScanCandidateOutcomeReasons.FrameworkEvidenceUnsafePath or
                    ScanCandidateOutcomeReasons.FrameworkEvidenceReparsePoint =>
                    EvidenceRule.UnsafePath,
                ScanCandidateOutcomeReasons.FrameworkEvidenceBoundExceeded =>
                    EvidenceRule.BoundExceeded,
                ScanCandidateOutcomeReasons.FrameworkEvidenceAmbiguousScope =>
                    EvidenceRule.ContradictoryEvidence,
                ScanCandidateOutcomeReasons.FrameworkEvidenceUnsupported =>
                    EvidenceRule.UnsupportedFamily,
                _ => null,
            };
            if (rule.HasValue && IsSafeRelativePath(outcome.FilePath))
            {
                AddCandidate(candidates, CreateWarning(
                    outcome.FilePath,
                    rule.Value,
                    $"audit:{rule.Value}"));
            }
        }
    }

    private static ClassificationCandidate CreateHintCandidate(
        CommunicationKind kind,
        EvidenceProvenance provenance,
        string relativePath,
        string selector) =>
        new(
            kind,
            ClassificationLevel.Hint,
            ConfidenceBand.Conservative,
            kind == CommunicationKind.Rpc
                ? EvidenceRule.RpcImportOnly
                : EvidenceRule.HttpImportOnly,
            provenance,
            relativePath,
            selector,
            OccurrenceCount: 1);

    private static bool IsImportOrNamespaceLine(string line) =>
        line.StartsWith("import ", StringComparison.Ordinal) ||
        line.StartsWith("using ", StringComparison.Ordinal) ||
        line.StartsWith("namespace ", StringComparison.Ordinal) ||
        line.StartsWith("from ", StringComparison.Ordinal);

    private static bool DetectionSourceContainsPackage(
        string detectionSource,
        string packageCoordinate)
    {
        int ecosystemSeparator = packageCoordinate.IndexOf(':', StringComparison.Ordinal);
        string packageToken = ecosystemSeparator >= 0
            ? packageCoordinate[(ecosystemSeparator + 1)..]
            : packageCoordinate;
        return detectionSource.Contains(packageToken, StringComparison.Ordinal) ||
            detectionSource.Contains(packageToken, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSafeRelativePath(string relativePath) =>
        !string.IsNullOrWhiteSpace(relativePath) &&
        relativePath.Length <= 2_048 &&
        relativePath.Length == relativePath.Trim().Length &&
        !relativePath.Any(char.IsControl) &&
        !relativePath.Contains('\\', StringComparison.Ordinal) &&
        !Path.IsPathFullyQualified(relativePath) &&
        !relativePath.Split('/').Any(segment => segment is "" or "." or "..");

    private static ClassificationCandidate CreateDirectCandidate(
        CommunicationEvidenceRule rule,
        string relativePath,
        string selector)
    {
        EvidenceRule evidenceRule = rule.Category switch
        {
            CommunicationEvidenceCategory.DirectAnnotation =>
                EvidenceRule.RpcDirectAnnotation,
            CommunicationEvidenceCategory.ControllerAnnotation =>
                EvidenceRule.HttpControllerAnnotation,
            CommunicationEvidenceCategory.ClientAnnotation =>
                EvidenceRule.HttpClientAnnotation,
            _ => throw new InvalidOperationException(
                "Only direct annotation categories can create a direct candidate."),
        };
        return new(
            rule.Kind,
            ClassificationLevel.Confirmed,
            ConfidenceBand.High,
            evidenceRule,
            EvidenceProvenance.StructuralAnnotation,
            relativePath,
            selector,
            OccurrenceCount: 1);
    }

    private static ClassificationCandidate CreateWarning(
        string relativePath,
        EvidenceRule rule,
        string selector) =>
        new(
            Kind: null,
            ClassificationLevel.Warning,
            ConfidenceBand.None,
            rule,
            EvidenceProvenance.Detector,
            relativePath,
            selector,
            OccurrenceCount: 1);

    private static CommunicationKindClassification CreateClassification(
        Guid scanRunId,
        ClassificationCandidate candidate)
    {
        string fingerprint = ComputeClassificationFingerprint(candidate);
        return CommunicationKindClassification.Create(
            scanRunId,
            candidate.Kind,
            candidate.Level,
            candidate.Confidence,
            candidate.Rule,
            candidate.Provenance,
            candidate.RelativePath,
            $"{candidate.RelativePath}#{candidate.Selector}",
            candidate.OccurrenceCount,
            fingerprint);
    }

    private static async Task<SourceRangeReadResult> ReadSourceRangeAsync(
        string snapshotRoot,
        CodeUnit codeUnit,
        CancellationToken cancellationToken)
    {
        if (!TryResolveContainedPath(
                snapshotRoot,
                codeUnit.FilePath,
                out string fullPath,
                out string safeRelativePath))
        {
            return SourceRangeReadResult.Rejected(
                safeRelativePath,
                EvidenceRule.UnsafePath,
                "unsafe-path");
        }

        try
        {
            if (!File.Exists(fullPath) ||
                HasReparsePoint(snapshotRoot, fullPath))
            {
                return SourceRangeReadResult.Rejected(
                    safeRelativePath,
                    EvidenceRule.UnsafePath,
                    "unsafe-path");
            }

            var file = new FileInfo(fullPath);
            if (file.Length > _maximumArtifactBytes)
            {
                return SourceRangeReadResult.Rejected(
                    safeRelativePath,
                    EvidenceRule.BoundExceeded,
                    "artifact-bound");
            }

            await using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var reader = new StreamReader(
                stream,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false,
                    throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: false);
            var source = new StringBuilder();
            var byteCount = 0;
            for (var lineNumber = 1; lineNumber <= codeUnit.LineEnd; lineNumber++)
            {
                string? line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    return SourceRangeReadResult.Rejected(
                        safeRelativePath,
                        EvidenceRule.MalformedEvidence,
                        "invalid-range");
                }

                if (lineNumber < codeUnit.LineStart)
                {
                    continue;
                }

                int lineBytes = Encoding.UTF8.GetByteCount(line) + 1;
                if (byteCount > _maximumSourceRangeBytes - lineBytes)
                {
                    return SourceRangeReadResult.Rejected(
                        safeRelativePath,
                        EvidenceRule.BoundExceeded,
                        "range-bound");
                }

                byteCount += lineBytes;
                source.Append(line);
                source.Append('\n');
            }

            return SourceRangeReadResult.Accepted(
                safeRelativePath,
                source.ToString(),
                byteCount);
        }
        catch (Exception exception) when (
            exception is IOException or
                UnauthorizedAccessException or
                DecoderFallbackException)
        {
            return SourceRangeReadResult.Rejected(
                safeRelativePath,
                EvidenceRule.MalformedEvidence,
                "unreadable-range");
        }
    }

    private static bool TryResolveContainedPath(
        string snapshotRoot,
        string relativePath,
        out string fullPath,
        out string safeRelativePath)
    {
        fullPath = string.Empty;
        safeRelativePath = CommunicationKindClassification.UnknownRelativePath;
        if (string.IsNullOrWhiteSpace(snapshotRoot) ||
            string.IsNullOrWhiteSpace(relativePath) ||
            relativePath.Length > 2_048 ||
            relativePath.Any(char.IsControl) ||
            relativePath.Length != relativePath.Trim().Length ||
            relativePath.Contains('\\', StringComparison.Ordinal) ||
            Path.IsPathFullyQualified(relativePath) ||
            relativePath.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            return false;
        }

        safeRelativePath = relativePath;
        try
        {
            string normalizedRoot = Path.GetFullPath(snapshotRoot);
            fullPath = Path.GetFullPath(
                Path.Combine(
                    normalizedRoot,
                    relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string rootPrefix = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
                ? normalizedRoot
                : normalizedRoot + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(rootPrefix, PathComparison);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
                NotSupportedException or
                PathTooLongException)
        {
            fullPath = string.Empty;
            return false;
        }
    }

    private static bool HasReparsePoint(string snapshotRoot, string fullPath)
    {
        string normalizedRoot = Path.GetFullPath(snapshotRoot);
        string relative = Path.GetRelativePath(normalizedRoot, fullPath);
        string current = normalizedRoot;
        foreach (string segment in relative.Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindExactToken(
        string value,
        string token,
        int startIndex,
        out int matchIndex)
    {
        while (startIndex <= value.Length - token.Length)
        {
            matchIndex = value.IndexOf(token, startIndex, StringComparison.Ordinal);
            if (matchIndex < 0)
            {
                return false;
            }

            int endIndex = matchIndex + token.Length;
            bool hasSafeStart = matchIndex == 0 ||
                !IsIdentifierCharacter(value[matchIndex - 1]);
            bool hasSafeEnd = endIndex == value.Length ||
                !IsIdentifierCharacter(value[endIndex]);
            if (hasSafeStart && hasSafeEnd)
            {
                return true;
            }

            startIndex = matchIndex + 1;
        }

        matchIndex = -1;
        return false;
    }

    private static bool IsIdentifierCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) ||
        character is '_' or '$';

    private static int CountOccurrences(
        IEnumerable<ClassificationCandidate> candidates,
        ClassificationLevel level) =>
        checked(candidates
            .Where(candidate => candidate.Level == level)
            .Sum(candidate => candidate.OccurrenceCount));

    private static ConfirmedCommunicationKinds ToConfirmedFlag(CommunicationKind? kind) =>
        kind switch
        {
            CommunicationKind.Rpc => ConfirmedCommunicationKinds.Rpc,
            CommunicationKind.Http => ConfirmedCommunicationKinds.Http,
            _ => ConfirmedCommunicationKinds.None,
        };

    private static void AddCandidate(
        List<ClassificationCandidate> candidates,
        ClassificationCandidate candidate)
    {
        if (candidates.Count == _maximumCandidateFacts)
        {
            throw new InvalidDataException(
                "Communication evidence exceeded the bounded candidate-fact ceiling.");
        }

        candidates.Add(candidate);
    }

    private static string ComputeAggregateFingerprint(
        string profiledRevision,
        ConfirmedCommunicationKinds confirmedKinds,
        int confirmedCount,
        int hintCount,
        int warningCount,
        IEnumerable<ClassificationCandidate> normalized)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendCanonicalField(hash, profiledRevision);
        AppendCanonicalField(
            hash,
            ((int)confirmedKinds).ToString(CultureInfo.InvariantCulture));
        AppendCanonicalField(hash, confirmedCount.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalField(hash, hintCount.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalField(hash, warningCount.ToString(CultureInfo.InvariantCulture));
        foreach (ClassificationCandidate candidate in normalized)
        {
            AppendCandidate(hash, candidate);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static string ComputeClassificationFingerprint(
        ClassificationCandidate candidate)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendCandidate(hash, candidate);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void AppendCandidate(
        IncrementalHash hash,
        ClassificationCandidate candidate)
    {
        AppendCanonicalField(hash, candidate.Kind?.ToString() ?? string.Empty);
        AppendCanonicalField(hash, candidate.Level.ToString());
        AppendCanonicalField(hash, candidate.Confidence.ToString());
        AppendCanonicalField(hash, candidate.Rule.ToString());
        AppendCanonicalField(hash, candidate.Provenance.ToString());
        AppendCanonicalField(hash, candidate.RelativePath);
        AppendCanonicalField(hash, candidate.Selector);
        AppendCanonicalField(
            hash,
            candidate.OccurrenceCount.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendCanonicalField(IncrementalHash hash, string value)
    {
        int byteCount = Encoding.UTF8.GetByteCount(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, byteCount);
        hash.AppendData(length);
        Span<byte> buffer = byteCount <= 4_096
            ? stackalloc byte[byteCount]
            : new byte[byteCount];
        Encoding.UTF8.GetBytes(value, buffer);
        hash.AppendData(buffer);
    }

    private static ClassificationCandidate[] SelectRetainedSamples(
        ClassificationCandidate[] normalized,
        ConfirmedCommunicationKinds confirmedKinds,
        bool evidenceTruncated)
    {
        if (!evidenceTruncated)
        {
            return normalized;
        }

        var selected = new HashSet<string>(StringComparer.Ordinal);
        ReserveConfirmedSample(CommunicationKind.Rpc, ConfirmedCommunicationKinds.Rpc);
        ReserveConfirmedSample(CommunicationKind.Http, ConfirmedCommunicationKinds.Http);
        ClassificationCandidate boundWarning = normalized.First(candidate =>
            candidate.Level == ClassificationLevel.Warning &&
            candidate.Rule == EvidenceRule.BoundExceeded);
        selected.Add(boundWarning.Identity);
        foreach (ClassificationCandidate candidate in normalized)
        {
            if (selected.Count == _maximumRetainedClassifications)
            {
                break;
            }

            selected.Add(candidate.Identity);
        }

        return
        [
            .. normalized.Where(candidate => selected.Contains(candidate.Identity)),
        ];

        void ReserveConfirmedSample(
            CommunicationKind kind,
            ConfirmedCommunicationKinds flag)
        {
            if ((confirmedKinds & flag) == 0)
            {
                return;
            }

            ClassificationCandidate sample = normalized.First(candidate =>
                candidate.Level == ClassificationLevel.Confirmed &&
                candidate.Kind == kind);
            selected.Add(sample.Identity);
        }
    }

    private static void ValidateIdentity(
        RepositoryProfileDraft profile,
        string profiledRevision,
        ICommittedRepositorySnapshotLease committedSnapshot)
    {
        if (profile.ScanRun.RepositoryId != committedSnapshot.RepositoryId ||
            profile.ScanRun.Id != committedSnapshot.ScanRunId ||
            !StringComparer.Ordinal.Equals(
                profiledRevision,
                committedSnapshot.ProfiledRevision))
        {
            throw new InvalidOperationException(
                "Communication detection requires one identity-bound committed snapshot.");
        }
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private sealed record ClassificationCandidate(
        CommunicationKind? Kind,
        ClassificationLevel Level,
        ConfidenceBand Confidence,
        EvidenceRule Rule,
        EvidenceProvenance Provenance,
        string RelativePath,
        string Selector,
        int OccurrenceCount)
    {
        public string Identity => string.Join(
            "\u001f",
            Kind?.ToString() ?? string.Empty,
            Level.ToString(),
            Confidence.ToString(),
            Rule.ToString(),
            Provenance.ToString(),
            RelativePath,
            Selector);
    }

    private sealed class ClassificationCandidateComparer :
        IComparer<ClassificationCandidate>
    {
        public static ClassificationCandidateComparer Instance { get; } = new();

        public int Compare(ClassificationCandidate? left, ClassificationCandidate? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return -1;
            }

            if (right is null)
            {
                return 1;
            }

            int comparison = left.Level.CompareTo(right.Level);
            comparison = comparison != 0
                ? comparison
                : Nullable.Compare(left.Kind, right.Kind);
            comparison = comparison != 0
                ? comparison
                : left.Rule.CompareTo(right.Rule);
            comparison = comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(left.RelativePath, right.RelativePath);
            comparison = comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(left.Selector, right.Selector);
            comparison = comparison != 0
                ? comparison
                : left.Confidence.CompareTo(right.Confidence);
            return comparison != 0
                ? comparison
                : left.Provenance.CompareTo(right.Provenance);
        }
    }

    private sealed record SourceRangeReadResult(
        string SafeRelativePath,
        string? Source,
        int ByteCount,
        EvidenceRule Rule,
        string Selector)
    {
        public static SourceRangeReadResult Accepted(
            string safeRelativePath,
            string source,
            int byteCount) =>
            new(
                safeRelativePath,
                source,
                byteCount,
                EvidenceRule.MalformedEvidence,
                string.Empty);

        public static SourceRangeReadResult Rejected(
            string safeRelativePath,
            EvidenceRule rule,
            string selector) =>
            new(safeRelativePath, null, 0, rule, selector);
    }
}
