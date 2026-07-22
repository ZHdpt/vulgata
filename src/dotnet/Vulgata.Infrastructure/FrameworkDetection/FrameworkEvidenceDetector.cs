using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.FrameworkDetection;

public sealed class FrameworkEvidenceDetector : IFrameworkEvidenceDetector
{
    private const int _maximumArtifactBytes = 8 * 1024 * 1024;
    private const int _maximumParseDepth = 64;
    private const int _maximumRepositoryRelativePathLength = 2_048;
    private const int _maximumDetectionSourceLength = 4_096;
    private static readonly UTF8Encoding _strictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private readonly FrameworkCatalog _catalog;
    private readonly FrameworkDetectionLimits _limits;

    public FrameworkEvidenceDetector(FrameworkCatalog catalog)
        : this(catalog, FrameworkDetectionLimits.Production)
    {
    }

    internal FrameworkEvidenceDetector(
        FrameworkCatalog catalog,
        FrameworkDetectionLimits limits)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _limits = limits ?? throw new ArgumentNullException(nameof(limits));
    }

    public async Task<IReadOnlyList<PackageEvidence>> DetectAsync(
        string workingCopyPath,
        CancellationToken cancellationToken = default)
    {
        FrameworkEvidenceDetectionResult result = await DetectDetailedAsync(
            workingCopyPath,
            cancellationToken);
        return result.Succeeded
            ? result.Evidence
            : throw new InvalidOperationException(result.Error);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The detector is a fail-closed boundary and must convert unexpected faults to a safe result.")]
    public async Task<FrameworkEvidenceDetectionResult> DetectDetailedAsync(
        string workingCopyPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingCopyPath);
        try
        {
            return await DetectDetailedCoreAsync(workingCopyPath, cancellationToken);
        }
        catch (FrameworkDetectionLimitExceededException)
        {
            return FrameworkEvidenceDetectionResult.Failed(
                FrameworkEvidenceDetectionFailure.RepositoryBoundExceeded,
                "框架证据超出仓库级限制。");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return FrameworkEvidenceDetectionResult.Failed(
                FrameworkEvidenceDetectionFailure.DetectorFailed,
                "框架证据检测失败。");
        }
    }

    private async Task<FrameworkEvidenceDetectionResult> DetectDetailedCoreAsync(
        string workingCopyPath,
        CancellationToken cancellationToken)
    {
        string repositoryRoot = Path.GetFullPath(workingCopyPath);
        string[] trackedPaths = await InventoryTrackedPathsAsync(repositoryRoot, cancellationToken);
        var trackedSet = trackedPaths.ToHashSet(StringComparer.Ordinal);
        var readResults = new Dictionary<string, ArtifactReadResult>(StringComparer.Ordinal);
        var evidence = new List<PackageEvidence>();
        var auditOutcomes = new List<FrameworkEvidenceAuditOutcome>();
        var budget = new FrameworkDetectionBudget(_limits);
        HashSet<string> nugetLockRoots =
        [
            .. trackedPaths
                .Where(path => GetArtifactKind(path) == ArtifactKind.NuGetLock)
                .Select(ScopeRoot),
        ];
        HashSet<string> npmLockRoots =
        [
            .. trackedPaths
                .Where(path => GetArtifactKind(path) == ArtifactKind.NpmLock)
                .Select(ScopeRoot),
        ];

        async Task<ArtifactReadResult> ReadArtifactAsync(string artifactPath)
        {
            if (readResults.TryGetValue(artifactPath, out ArtifactReadResult? cached))
            {
                return cached;
            }

            ArtifactReadResult result = await ReadTrackedTextAsync(
                repositoryRoot,
                artifactPath,
                budget,
                cancellationToken);
            readResults.Add(artifactPath, result);
            return result;
        }

        foreach (string artifactPath in trackedPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArtifactKind? artifactKind = GetArtifactKind(artifactPath);
            if (artifactKind is null)
            {
                continue;
            }

            budget.RecordSupportedArtifact();
            ArtifactReadResult readResult = await ReadArtifactAsync(artifactPath);
            if (readResult.AuditOutcome is not null)
            {
                if (!IsDirectOwnerCoveredByLock(
                        artifactKind.Value,
                        ScopeRoot(artifactPath),
                        nugetLockRoots,
                        npmLockRoots))
                {
                    budget.RecordAuditOutcome(auditOutcomes, readResult.AuditOutcome);
                }

                continue;
            }

            int priorEvidenceCount = evidence.Count;
            ArtifactParseStatus parseStatus;
            if (artifactKind == ArtifactKind.NuGetProject)
            {
                parseStatus = ValidateXml(readResult.Content!);
                if (parseStatus == ArtifactParseStatus.Valid)
                {
                    ParseNuGetProject(artifactPath, readResult.Content!, evidence, budget);
                }
            }
            else if (artifactKind == ArtifactKind.NpmManifest)
            {
                parseStatus = ValidateJson(readResult.Content!);
                if (parseStatus == ArtifactParseStatus.Valid)
                {
                    ParseNpmManifest(artifactPath, readResult.Content!, evidence, budget);
                }
            }
            else if (artifactKind == ArtifactKind.MavenProject)
            {
                parseStatus = ValidateXml(readResult.Content!);
                if (parseStatus == ArtifactParseStatus.Valid)
                {
                    ParseMavenProject(artifactPath, readResult.Content!, evidence, budget);
                }
            }
            else if (artifactKind == ArtifactKind.PythonRequirements)
            {
                parseStatus = ArtifactParseStatus.Valid;
                ParsePythonRequirements(artifactPath, readResult.Content!, evidence, budget);
            }
            else if (artifactKind == ArtifactKind.NuGetLock)
            {
                var candidates = new List<DirectManifestOwnerCandidate>();
                foreach (string ownerPath in trackedPaths
                             .Where(path =>
                                 StringComparer.Ordinal.Equals(
                                     ScopeRoot(path),
                                     ScopeRoot(artifactPath)) &&
                                 path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                             .OrderBy(path => path, StringComparer.Ordinal))
                {
                    candidates.Add(CreateDirectManifestOwnerCandidate(
                        ownerPath,
                        "nuget",
                        await ReadArtifactAsync(ownerPath)));
                }

                if (TryValidateDirectManifestOwner(candidates, out _))
                {
                    parseStatus = ValidateJson(readResult.Content!);
                    if (parseStatus == ArtifactParseStatus.Valid)
                    {
                        ParseNuGetLock(artifactPath, readResult.Content!, evidence, budget);
                    }
                }
                else
                {
                    parseStatus = OwnerFailureStatus(candidates);
                }
            }
            else if (artifactKind == ArtifactKind.NpmLock)
            {
                string ownerPath = CombineRelative(ScopeRoot(artifactPath), "package.json");
                DirectManifestOwnerCandidate[] candidates = trackedSet.Contains(ownerPath)
                    ?
                    [
                        CreateDirectManifestOwnerCandidate(
                            ownerPath,
                            "npm",
                            await ReadArtifactAsync(ownerPath)),
                    ]
                    : [];
                if (TryValidateDirectManifestOwner(candidates, out _))
                {
                    parseStatus = ValidateJson(readResult.Content!);
                    if (parseStatus == ArtifactParseStatus.Valid)
                    {
                        ParseNpmLock(artifactPath, readResult.Content!, evidence, budget);
                    }
                }
                else
                {
                    parseStatus = OwnerFailureStatus(candidates);
                }
            }
            else
            {
                parseStatus = ArtifactParseStatus.Unsupported;
            }

            if (parseStatus != ArtifactParseStatus.Valid ||
                evidence.Count == priorEvidenceCount)
            {
                if (!IsDirectOwnerCoveredByLock(
                        artifactKind.Value,
                        ScopeRoot(artifactPath),
                        nugetLockRoots,
                        npmLockRoots))
                {
                    budget.RecordAuditOutcome(
                        auditOutcomes,
                        CreateAuditOutcome(artifactPath, parseStatus));
                }
            }
        }

        return FrameworkEvidenceDetectionResult.Success(
            evidence,
            auditOutcomes,
            budget.TotalAuditOutcomeCount,
            budget.AuditOutcomeIdentities);
    }

    public async Task<IReadOnlyList<PackageEvidence>> DetectApplicableAsync(
        string workingCopyPath,
        string codeUnitPath,
        string parserLanguage,
        CancellationToken cancellationToken = default)
    {
        FrameworkEvidenceDetectionResult result = await DetectApplicableDetailedAsync(
            workingCopyPath,
            codeUnitPath,
            parserLanguage,
            cancellationToken);
        return result.Succeeded
            ? result.Evidence
            : throw new InvalidOperationException(result.Error);
    }

    public async Task<FrameworkEvidenceDetectionResult> DetectApplicableDetailedAsync(
        string workingCopyPath,
        string codeUnitPath,
        string parserLanguage,
        CancellationToken cancellationToken = default)
    {
        FrameworkEvidenceDetectionResult detection = await DetectDetailedAsync(
            workingCopyPath,
            cancellationToken);
        return SelectApplicable(detection, codeUnitPath, parserLanguage);
    }

    public FrameworkEvidenceDetectionResult SelectApplicable(
        FrameworkEvidenceDetectionResult detection,
        string codeUnitPath,
        string parserLanguage)
    {
        ArgumentNullException.ThrowIfNull(detection);
        if (!detection.Succeeded)
        {
            return detection;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(codeUnitPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(parserLanguage);
        string normalizedCodeUnitPath = NormalizeRelativePath(codeUnitPath);
        if (normalizedCodeUnitPath.Length > _maximumRepositoryRelativePathLength)
        {
            return FrameworkEvidenceDetectionResult.Success(
                [],
                detection.AuditOutcomes,
                detection.TotalAuditOutcomeCount,
                detection.AuditOutcomeIdentities);
        }

        IReadOnlyList<PackageEvidence> detected = detection.Evidence;
        var applicable = new List<PackageEvidence>();
        var auditOutcomes = detection.AuditOutcomes.ToList();
        var auditOutcomeIdentities = detection.AuditOutcomeIdentities.ToList();
        var auditKeys = auditOutcomeIdentities.ToHashSet();
        var additionalAuditOutcomeCount = 0;

        foreach (IGrouping<string, PackageEvidence> ecosystem in detected
                     .Where(evidence =>
                         IsWithinScope(normalizedCodeUnitPath, evidence.ScopeRoot) &&
                         IsLanguageCompatible(evidence, parserLanguage))
                     .GroupBy(evidence => evidence.Ecosystem, StringComparer.Ordinal))
        {
            int deepestScope = ecosystem.Max(evidence => evidence.ScopeDepth);
            PackageEvidence[] winningScope =
            [
                .. ecosystem.Where(evidence => evidence.ScopeDepth == deepestScope),
            ];
            int manifestCount = winningScope
                .Where(evidence => evidence.Relationship == PackageEvidenceRelationship.Direct)
                .Select(evidence => evidence.ArtifactPath)
                .Distinct(StringComparer.Ordinal)
                .Count();
            if (manifestCount > 1)
            {
                foreach (string artifactPath in winningScope
                             .Where(evidence =>
                                 evidence.Relationship ==
                                 PackageEvidenceRelationship.Direct)
                             .Select(evidence => evidence.ArtifactPath)
                             .Distinct(StringComparer.Ordinal)
                             .OrderBy(path => path, StringComparer.Ordinal))
                {
                    FrameworkEvidenceAuditOutcome audit =
                        CreateAuditOutcome(
                            artifactPath,
                            ArtifactParseStatus.AmbiguousScope);
                    var identity = new FrameworkEvidenceAuditIdentity(
                        audit.ArtifactPath,
                        audit.Outcome,
                        audit.Reason);
                    if (!auditKeys.Add(identity))
                    {
                        continue;
                    }

                    auditOutcomeIdentities.Add(identity);
                    additionalAuditOutcomeCount++;
                    if (auditOutcomes.Count < _limits.MaximumRetainedAuditDetails)
                    {
                        auditOutcomes.Add(audit);
                    }
                }

                continue;
            }

            applicable.AddRange(winningScope);
        }

        PackageEvidence[] winners =
        [
            .. applicable
                .GroupBy(GetFrameworkName, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => group
                    .OrderBy(
                        evidence => evidence.Relationship ==
                            PackageEvidenceRelationship.Direct
                            ? 0
                            : 1)
                    .ThenByDescending(evidence => evidence.ScopeDepth)
                    .ThenBy(evidence => evidence.DetectionSource, StringComparer.Ordinal)
                    .First()),
        ];
        return winners.Length > _limits.MaximumFinalFactsPerCodeUnit
            ? FrameworkEvidenceDetectionResult.Failed(
                FrameworkEvidenceDetectionFailure.RepositoryBoundExceeded,
                "框架证据超出仓库级限制。")
            : FrameworkEvidenceDetectionResult.Success(
                winners,
                auditOutcomes,
                detection.TotalAuditOutcomeCount + additionalAuditOutcomeCount,
                auditOutcomeIdentities);
    }

    private static async Task<string[]> InventoryTrackedPathsAsync(
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = repositoryRoot,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            },
        };
        process.StartInfo.ArgumentList.Add("ls-files");
        process.StartInfo.ArgumentList.Add("--cached");
        process.StartInfo.ArgumentList.Add("-z");
        process.StartInfo.ArgumentList.Add("--");

        if (!process.Start())
        {
            throw new InvalidOperationException(
                "Unable to inventory committed Repository evidence.");
        }

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        await standardError;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Unable to inventory committed Repository evidence.");
        }

        return
        [
            .. (await standardOutput)
                .Split('\0', StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeRelativePath),
        ];
    }

    private static async Task<ArtifactReadResult> ReadTrackedTextAsync(
        string repositoryRoot,
        string artifactPath,
        FrameworkDetectionBudget budget,
        CancellationToken cancellationToken)
    {
        if (artifactPath.Length > _maximumRepositoryRelativePathLength)
        {
            return ArtifactReadResult.Rejected(
                CreateAuditOutcome(artifactPath, ArtifactParseStatus.ArtifactBoundExceeded));
        }

        if (!TryResolveRepositoryPath(repositoryRoot, artifactPath, out string fullPath))
        {
            return ArtifactReadResult.Rejected(
                CreateAuditOutcome(artifactPath, ArtifactParseStatus.UnsafePath));
        }

        try
        {
            string currentPath = repositoryRoot;
            foreach (string segment in artifactPath.Split('/'))
            {
                currentPath = Path.Combine(currentPath, segment);
                if (!File.GetAttributes(currentPath).HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }

                FileSystemInfo? target = File.ResolveLinkTarget(
                    currentPath,
                    returnFinalTarget: true);
                ArtifactParseStatus status = target is null ||
                    !IsWithinRepository(repositoryRoot, target.FullName)
                    ? ArtifactParseStatus.UnsafePath
                    : ArtifactParseStatus.ReparsePoint;
                return ArtifactReadResult.Rejected(
                    CreateAuditOutcome(artifactPath, status));
            }

            await using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            long accountedBytes = stream.Length;
            budget.RecordArtifactBytes(accountedBytes);
            if (stream.Length > _maximumArtifactBytes)
            {
                return ArtifactReadResult.Rejected(
                    CreateAuditOutcome(artifactPath, ArtifactParseStatus.ArtifactBoundExceeded));
            }

            using var contentBuffer = new MemoryStream(
                capacity: checked((int)stream.Length));
            byte[] buffer = new byte[64 * 1024];
            int totalBytes = 0;
            while (true)
            {
                int read = await stream.ReadAsync(
                    buffer,
                    cancellationToken);
                if (read == 0)
                {
                    break;
                }

                totalBytes += read;
                if (totalBytes > accountedBytes)
                {
                    budget.RecordArtifactBytes(totalBytes - accountedBytes);
                    accountedBytes = totalBytes;
                }

                if (totalBytes > _maximumArtifactBytes)
                {
                    return ArtifactReadResult.Rejected(
                        CreateAuditOutcome(
                            artifactPath,
                            ArtifactParseStatus.ArtifactBoundExceeded));
                }

                await contentBuffer.WriteAsync(
                    buffer.AsMemory(0, read),
                    cancellationToken);
            }

            return ArtifactReadResult.Accepted(
                _strictUtf8.GetString(contentBuffer.GetBuffer(), 0, totalBytes));
        }
        catch (DecoderFallbackException)
        {
            return ArtifactReadResult.Rejected(
                CreateAuditOutcome(artifactPath, ArtifactParseStatus.Malformed));
        }
        catch (IOException)
        {
            return ArtifactReadResult.Rejected(
                CreateAuditOutcome(artifactPath, ArtifactParseStatus.Unsupported));
        }
        catch (UnauthorizedAccessException)
        {
            return ArtifactReadResult.Rejected(
                CreateAuditOutcome(artifactPath, ArtifactParseStatus.Unsupported));
        }
    }

    private static ArtifactKind? GetArtifactKind(string artifactPath)
    {
        string fileName = Path.GetFileName(artifactPath);
        if (artifactPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return ArtifactKind.NuGetProject;
        }

        return fileName switch
        {
            "package.json" => ArtifactKind.NpmManifest,
            "pom.xml" => ArtifactKind.MavenProject,
            "requirements.txt" => ArtifactKind.PythonRequirements,
            "packages.lock.json" => ArtifactKind.NuGetLock,
            "package-lock.json" => ArtifactKind.NpmLock,
            _ => null,
        };
    }

    private static bool IsDirectOwnerCoveredByLock(
        ArtifactKind artifactKind,
        string scopeRoot,
        IReadOnlySet<string> nugetLockRoots,
        IReadOnlySet<string> npmLockRoots) =>
        (artifactKind == ArtifactKind.NuGetProject && nugetLockRoots.Contains(scopeRoot)) ||
        (artifactKind == ArtifactKind.NpmManifest && npmLockRoots.Contains(scopeRoot));

    private static DirectManifestOwnerCandidate CreateDirectManifestOwnerCandidate(
        string artifactPath,
        string ecosystem,
        ArtifactReadResult readResult)
    {
        DirectManifestOwnerReadStatus readStatus = readResult.AuditOutcome?.Reason switch
        {
            ScanCandidateOutcomeReasons.FrameworkEvidenceUnsafePath =>
                DirectManifestOwnerReadStatus.UnsafePath,
            ScanCandidateOutcomeReasons.FrameworkEvidenceReparsePoint =>
                DirectManifestOwnerReadStatus.ReparsePoint,
            ScanCandidateOutcomeReasons.FrameworkEvidenceBoundExceeded =>
                DirectManifestOwnerReadStatus.BoundExceeded,
            ScanCandidateOutcomeReasons.FrameworkEvidenceUnsupported =>
                DirectManifestOwnerReadStatus.Unreadable,
            _ => DirectManifestOwnerReadStatus.SafeAndBounded,
        };
        DirectManifestOwnerParseStatus parseStatus = readResult.AuditOutcome?.Reason switch
        {
            ScanCandidateOutcomeReasons.FrameworkEvidenceMalformed =>
                DirectManifestOwnerParseStatus.Malformed,
            null when ecosystem == "nuget" => ValidateNuGetOwnerManifest(readResult.Content!),
            null when ecosystem == "npm" => ValidateNpmOwnerManifest(readResult.Content!),
            _ => DirectManifestOwnerParseStatus.Unsupported,
        };
        bool applicable = readStatus == DirectManifestOwnerReadStatus.SafeAndBounded &&
            parseStatus == DirectManifestOwnerParseStatus.ParsedSupported;
        return new(
            artifactPath,
            ScopeRoot(artifactPath),
            ecosystem,
            readStatus,
            parseStatus,
            applicable);
    }

    private static bool TryValidateDirectManifestOwner(
        IReadOnlyList<DirectManifestOwnerCandidate> candidates,
        out ValidatedDirectManifestOwner? owner)
    {
        owner = null;
        if (candidates.Count != 1)
        {
            return false;
        }

        DirectManifestOwnerCandidate candidate = candidates[0];
        if (candidate.ReadStatus != DirectManifestOwnerReadStatus.SafeAndBounded ||
            candidate.ParseStatus != DirectManifestOwnerParseStatus.ParsedSupported ||
            !candidate.Applicable)
        {
            return false;
        }

        owner = new(
            candidate.ArtifactPath,
            candidate.ScopeRoot,
            candidate.Ecosystem);
        return true;
    }

    private static ArtifactParseStatus OwnerFailureStatus(
        IReadOnlyList<DirectManifestOwnerCandidate> candidates)
    {
        DirectManifestOwnerCandidate? invalid = candidates
            .OrderBy(candidate => candidate.ArtifactPath, StringComparer.Ordinal)
            .FirstOrDefault(candidate =>
                candidate.ReadStatus != DirectManifestOwnerReadStatus.SafeAndBounded ||
                candidate.ParseStatus != DirectManifestOwnerParseStatus.ParsedSupported ||
                !candidate.Applicable);
        if (invalid is not null)
        {
            return invalid.ReadStatus switch
            {
                DirectManifestOwnerReadStatus.UnsafePath => ArtifactParseStatus.UnsafePath,
                DirectManifestOwnerReadStatus.ReparsePoint => ArtifactParseStatus.ReparsePoint,
                DirectManifestOwnerReadStatus.BoundExceeded =>
                    ArtifactParseStatus.ArtifactBoundExceeded,
                DirectManifestOwnerReadStatus.Unreadable => ArtifactParseStatus.Unsupported,
                _ => invalid.ParseStatus == DirectManifestOwnerParseStatus.Malformed
                    ? ArtifactParseStatus.Malformed
                    : ArtifactParseStatus.Unsupported,
            };
        }

        return candidates.Count > 1
            ? ArtifactParseStatus.AmbiguousScope
            : ArtifactParseStatus.Unsupported;
    }

    private static DirectManifestOwnerParseStatus ValidateNuGetOwnerManifest(string content)
    {
        ArtifactParseStatus validation = ValidateXml(content);
        if (validation == ArtifactParseStatus.Malformed)
        {
            return DirectManifestOwnerParseStatus.Malformed;
        }

        if (validation != ArtifactParseStatus.Valid)
        {
            return DirectManifestOwnerParseStatus.Unsupported;
        }

        XDocument? document = ParseXml(content);
        XElement? project = document?.Root;
        if (project is null ||
            !StringComparer.Ordinal.Equals(project.Name.LocalName, "Project"))
        {
            return DirectManifestOwnerParseStatus.Unsupported;
        }

        if (HasCondition(project))
        {
            return DirectManifestOwnerParseStatus.ConditionalOrDynamic;
        }

        var hasLiteralOwnership = false;
        string? projectSdk = project.Attribute("Sdk")?.Value;
        if (projectSdk is not null)
        {
            if (!IsLiteral(projectSdk))
            {
                return DirectManifestOwnerParseStatus.ConditionalOrDynamic;
            }

            hasLiteralOwnership = true;
        }

        foreach (XElement sdk in project.Elements()
                     .Where(element => element.Name.LocalName == "Sdk"))
        {
            if (HasCondition(sdk) || !IsLiteral(sdk.Attribute("Name")?.Value))
            {
                return DirectManifestOwnerParseStatus.ConditionalOrDynamic;
            }

            hasLiteralOwnership = true;
        }

        foreach (XElement declaration in project.Descendants().Where(element =>
                     element.Name.LocalName is "PackageReference" or "FrameworkReference"))
        {
            if (declaration.AncestorsAndSelf().Any(HasCondition) ||
                !IsLiteral(declaration.Attribute("Include")?.Value))
            {
                return DirectManifestOwnerParseStatus.ConditionalOrDynamic;
            }

            hasLiteralOwnership = true;
        }

        return hasLiteralOwnership
            ? DirectManifestOwnerParseStatus.ParsedSupported
            : DirectManifestOwnerParseStatus.Unsupported;
    }

    private static DirectManifestOwnerParseStatus ValidateNpmOwnerManifest(string content)
    {
        ArtifactParseStatus validation = ValidateJson(content);
        if (validation == ArtifactParseStatus.Malformed)
        {
            return DirectManifestOwnerParseStatus.Malformed;
        }

        if (validation != ArtifactParseStatus.Valid)
        {
            return DirectManifestOwnerParseStatus.Unsupported;
        }

        using JsonDocument? document = ParseJson(content);
        if (document is null || document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return DirectManifestOwnerParseStatus.Unsupported;
        }

        var hasLiteralOwnership = false;
        foreach (string mapName in new[]
                 {
                     "dependencies",
                     "devDependencies",
                     "optionalDependencies",
                 })
        {
            if (!document.RootElement.TryGetProperty(mapName, out JsonElement dependencies))
            {
                continue;
            }

            if (dependencies.ValueKind != JsonValueKind.Object)
            {
                return DirectManifestOwnerParseStatus.Unsupported;
            }

            foreach (JsonProperty dependency in dependencies.EnumerateObject())
            {
                if (dependency.Value.ValueKind != JsonValueKind.String ||
                    !IsLiteral(dependency.Value.GetString()))
                {
                    return DirectManifestOwnerParseStatus.Unsupported;
                }

                hasLiteralOwnership = true;
            }
        }

        return hasLiteralOwnership
            ? DirectManifestOwnerParseStatus.ParsedSupported
            : DirectManifestOwnerParseStatus.Unsupported;
    }

    private static ArtifactParseStatus ValidateJson(string content)
    {
        if (ExceedsJsonDepth(content))
        {
            return ArtifactParseStatus.ParseDepthExceeded;
        }

        using JsonDocument? document = ParseJson(content);
        return document is null
            ? ArtifactParseStatus.Malformed
            : ArtifactParseStatus.Valid;
    }

    private static ArtifactParseStatus ValidateXml(string content)
    {
        try
        {
            using var textReader = new StringReader(content);
            using XmlReader reader = XmlReader.Create(
                textReader,
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                });
            while (reader.Read())
            {
                if (reader.Depth >= _maximumParseDepth)
                {
                    return ArtifactParseStatus.ParseDepthExceeded;
                }
            }

            return ArtifactParseStatus.Valid;
        }
        catch (XmlException)
        {
            return ArtifactParseStatus.Malformed;
        }
    }

    private static bool ExceedsJsonDepth(string content)
    {
        var depth = 0;
        var inString = false;
        var escaped = false;
        foreach (char character in content)
        {
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (character == '"')
            {
                inString = true;
            }
            else if (character is '{' or '[')
            {
                depth++;
                if (depth > _maximumParseDepth)
                {
                    return true;
                }
            }
            else if (character is '}' or ']')
            {
                depth--;
            }
        }

        return false;
    }

    private void ParseNuGetProject(
        string artifactPath,
        string content,
        ICollection<PackageEvidence> evidence,
        FrameworkDetectionBudget budget)
    {
        XDocument? document = ParseXml(content);
        XElement? project = document?.Root;
        if (project is null ||
            !StringComparer.Ordinal.Equals(project.Name.LocalName, "Project") ||
            HasCondition(project))
        {
            return;
        }

        string? projectSdk = project.Attribute("Sdk")?.Value;
        if (projectSdk is not null)
        {
            budget.RecordParsedPackageEntry();
        }

        AddMatched(
            artifactPath,
            "nuget",
            projectSdk is null ? null : $"nuget-sdk:{projectSdk}",
            PackageEvidenceRelationship.Direct,
            projectSdk is null ? null : $"xml:Sdk[{projectSdk}]",
            evidence,
            budget);

        foreach (XElement sdk in project.Elements()
                     .Where(element => element.Name.LocalName == "Sdk" && !HasCondition(element)))
        {
            string? sdkName = sdk.Attribute("Name")?.Value;
            budget.RecordParsedPackageEntry();
            AddMatched(
                artifactPath,
                "nuget",
                sdkName is null ? null : $"nuget-sdk:{sdkName}",
                PackageEvidenceRelationship.Direct,
                sdkName is null ? null : $"xml:Sdk[{sdkName}]",
                evidence,
                budget);
        }

        foreach (XElement itemGroup in project.Elements()
                     .Where(element => element.Name.LocalName == "ItemGroup" && !HasCondition(element)))
        {
            foreach (XElement item in itemGroup.Elements().Where(element => !HasCondition(element)))
            {
                string? include = item.Attribute("Include")?.Value;
                string? prefix = item.Name.LocalName switch
                {
                    "PackageReference" => "nuget:",
                    "FrameworkReference" => "nuget-framework:",
                    _ => null,
                };
                if (prefix is null)
                {
                    continue;
                }

                budget.RecordParsedPackageEntry();
                if (!IsLiteral(include))
                {
                    continue;
                }

                AddMatched(
                    artifactPath,
                    "nuget",
                    $"{prefix}{include}",
                    PackageEvidenceRelationship.Direct,
                    $"xml:{item.Name.LocalName}[{include}]",
                    evidence,
                    budget);
            }
        }
    }

    private void ParseNuGetLock(
        string artifactPath,
        string content,
        ICollection<PackageEvidence> evidence,
        FrameworkDetectionBudget budget)
    {
        using JsonDocument? document = ParseJson(content);
        if (document is null ||
            !TryGetObject(document.RootElement, "dependencies", out JsonElement targets))
        {
            return;
        }

        foreach (JsonProperty target in targets.EnumerateObject())
        {
            if (target.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (JsonProperty package in target.Value.EnumerateObject())
            {
                budget.RecordParsedPackageEntry();
                if (!TryGetString(package.Value, "type", out string? relationship) ||
                    !StringComparer.Ordinal.Equals(relationship, "Transitive"))
                {
                    continue;
                }

                AddMatched(
                    artifactPath,
                    "nuget",
                    $"nuget:{package.Name}",
                    PackageEvidenceRelationship.Transitive,
                    $"json:/dependencies/{EscapeJsonPointer(target.Name)}/{EscapeJsonPointer(package.Name)}",
                    evidence,
                    budget);
            }
        }
    }

    private void ParseNpmManifest(
        string artifactPath,
        string content,
        ICollection<PackageEvidence> evidence,
        FrameworkDetectionBudget budget)
    {
        using JsonDocument? document = ParseJson(content);
        if (document is null || document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (string mapName in new[]
                 {
                     "dependencies",
                     "devDependencies",
                     "optionalDependencies",
                 })
        {
            if (!TryGetObject(document.RootElement, mapName, out JsonElement dependencies))
            {
                continue;
            }

            foreach (JsonProperty dependency in dependencies.EnumerateObject())
            {
                budget.RecordParsedPackageEntry();
                if (dependency.Value.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                AddMatched(
                    artifactPath,
                    "npm",
                    $"npm:{dependency.Name}",
                    PackageEvidenceRelationship.Direct,
                    $"json:/{mapName}/{EscapeJsonPointer(dependency.Name)}",
                    evidence,
                    budget);
            }
        }
    }

    private void ParseNpmLock(
        string artifactPath,
        string content,
        ICollection<PackageEvidence> evidence,
        FrameworkDetectionBudget budget)
    {
        using JsonDocument? document = ParseJson(content);
        if (document is null ||
            !TryGetInt32(document.RootElement, "lockfileVersion", out int version) ||
            version is not (2 or 3) ||
            !TryGetObject(document.RootElement, "packages", out JsonElement packages))
        {
            return;
        }

        foreach (JsonProperty package in packages.EnumerateObject())
        {
            budget.RecordParsedPackageEntry();
            const string marker = "node_modules/";
            int markerIndex = package.Name.LastIndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0 || package.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string packageName = package.Name[(markerIndex + marker.Length)..];
            AddMatched(
                artifactPath,
                "npm",
                $"npm:{packageName}",
                PackageEvidenceRelationship.Transitive,
                $"json:/packages/{EscapeJsonPointer(package.Name)}",
                evidence,
                budget);
        }
    }

    private void ParseMavenProject(
        string artifactPath,
        string content,
        ICollection<PackageEvidence> evidence,
        FrameworkDetectionBudget budget)
    {
        XDocument? document = ParseXml(content);
        XElement? project = document?.Root;
        if (project is null || !StringComparer.Ordinal.Equals(project.Name.LocalName, "project"))
        {
            return;
        }

        foreach (XElement dependencies in project.Elements()
                     .Where(element => element.Name.LocalName == "dependencies"))
        {
            foreach (XElement dependency in dependencies.Elements()
                         .Where(element => element.Name.LocalName == "dependency"))
            {
                budget.RecordParsedPackageEntry();
                string? groupId = ChildValue(dependency, "groupId");
                string? artifactId = ChildValue(dependency, "artifactId");
                if (!IsLiteral(groupId) || !IsLiteral(artifactId))
                {
                    continue;
                }

                string coordinate = $"maven:{groupId}:{artifactId}";
                AddMatched(
                    artifactPath,
                    "maven",
                    coordinate,
                    PackageEvidenceRelationship.Direct,
                    $"xml:dependency[{groupId}:{artifactId}]",
                    evidence,
                    budget);
            }
        }
    }

    private void ParsePythonRequirements(
        string artifactPath,
        string content,
        ICollection<PackageEvidence> evidence,
        FrameworkDetectionBudget budget)
    {
        string[] lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            string line = lines[index].Trim();
            if (line.Length == 0 ||
                line.StartsWith('#'))
            {
                continue;
            }

            budget.RecordParsedPackageEntry();
            if (
                line.StartsWith('-') ||
                line.Contains('@', StringComparison.Ordinal) ||
                line.Contains(';', StringComparison.Ordinal))
            {
                continue;
            }

            int nameLength = 0;
            while (nameLength < line.Length &&
                (char.IsAsciiLetterOrDigit(line[nameLength]) ||
                 line[nameLength] is '-' or '_' or '.'))
            {
                nameLength++;
            }

            if (nameLength == 0)
            {
                continue;
            }

            string packageName = NormalizePythonName(line[..nameLength]);
            string coordinate = $"python:{packageName}";
            AddMatched(
                artifactPath,
                "python-requirements",
                coordinate,
                PackageEvidenceRelationship.Direct,
                $"line:{index + 1}:{coordinate}",
                evidence,
                budget);
        }
    }

    private void AddMatched(
        string artifactPath,
        string ecosystem,
        string? packageCoordinate,
        PackageEvidenceRelationship relationship,
        string? locator,
        ICollection<PackageEvidence> evidence,
        FrameworkDetectionBudget budget)
    {
        if (packageCoordinate is null || locator is null)
        {
            return;
        }

        FrameworkCatalogEntry? entry = FindNormalizedCatalogEntry(ecosystem, packageCoordinate);
        string evidenceKind = relationship == PackageEvidenceRelationship.Direct
            ? "direct"
            : "paired-lock";
        if (entry is null ||
            !StringComparer.Ordinal.Equals(entry.Ecosystem, ecosystem) ||
            !entry.EvidenceKinds.Contains(evidenceKind, StringComparer.Ordinal) ||
            !TryBuildDetectionSource(
                artifactPath,
                locator,
                entry.PackageCoordinate,
                out string detectionSource))
        {
            return;
        }

        string scopeRoot = ScopeRoot(artifactPath);
        budget.RecordCandidateFact();
        evidence.Add(
            new PackageEvidence(
                artifactPath,
                scopeRoot,
                ecosystem,
                entry.PackageCoordinate,
                relationship,
                detectionSource,
                ScopeDepth(scopeRoot)));
    }

    private string GetFrameworkName(PackageEvidence evidence) =>
        _catalog.FindExact(evidence.PackageCoordinate)!.FrameworkName;

    private static bool TryBuildDetectionSource(
        string artifactPath,
        string locator,
        string packageCoordinate,
        out string detectionSource)
    {
        detectionSource = string.Empty;
        if (artifactPath.Length is < 1 or > _maximumRepositoryRelativePathLength ||
            !StringComparer.Ordinal.Equals(artifactPath, NormalizeRelativePath(artifactPath)) ||
            !IsClosedLocator(locator, packageCoordinate))
        {
            return false;
        }

        string candidate = $"{artifactPath}#{locator}";
        if (candidate.Length > _maximumDetectionSourceLength)
        {
            return false;
        }

        detectionSource = candidate;
        return true;
    }

    private static bool IsClosedLocator(string locator, string packageCoordinate)
    {
        if (locator.StartsWith("json:/", StringComparison.Ordinal) ||
            (locator.StartsWith("xml:", StringComparison.Ordinal) && locator.Length > 4))
        {
            return true;
        }

        const string linePrefix = "line:";
        if (!locator.StartsWith(linePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        int coordinateSeparator = locator.IndexOf(':', linePrefix.Length);
        ReadOnlySpan<char> lineNumber = coordinateSeparator > linePrefix.Length
            ? locator.AsSpan(linePrefix.Length, coordinateSeparator - linePrefix.Length)
            : [];
        if (lineNumber.Length == 0 || lineNumber[0] == '0')
        {
            return false;
        }

        foreach (char character in lineNumber)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return StringComparer.Ordinal.Equals(
            locator[(coordinateSeparator + 1)..],
            packageCoordinate);
    }

    private FrameworkCatalogEntry? FindNormalizedCatalogEntry(
        string ecosystem,
        string packageCoordinate)
    {
        FrameworkCatalogEntry? exact = _catalog.FindExact(packageCoordinate);
        if (exact is not null)
        {
            return exact;
        }

        if (ecosystem is not ("nuget" or "python-requirements"))
        {
            return null;
        }

        return _catalog.Entries.FirstOrDefault(
            entry =>
                StringComparer.Ordinal.Equals(entry.Ecosystem, ecosystem) &&
                StringComparer.OrdinalIgnoreCase.Equals(
                    entry.PackageCoordinate,
                    packageCoordinate));
    }

    private bool IsLanguageCompatible(PackageEvidence evidence, string parserLanguage)
    {
        FrameworkCatalogEntry? entry = _catalog.FindExact(evidence.PackageCoordinate);
        return entry is not null &&
            entry.AllowedLanguages.Contains(parserLanguage, StringComparer.Ordinal);
    }

    private static XDocument? ParseXml(string content)
    {
        try
        {
            using var textReader = new StringReader(content);
            using XmlReader reader = XmlReader.Create(
                textReader,
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                });
            return XDocument.Load(reader, LoadOptions.None);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static JsonDocument? ParseJson(string content)
    {
        try
        {
            return JsonDocument.Parse(
                content,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 64,
                });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetObject(
        JsonElement parent,
        string propertyName,
        out JsonElement value)
    {
        if (parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty(propertyName, out value) &&
            value.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryGetString(
        JsonElement parent,
        string propertyName,
        out string? value)
    {
        value = parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty(propertyName, out JsonElement element) &&
            element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
        return value is not null;
    }

    private static bool TryGetInt32(
        JsonElement parent,
        string propertyName,
        out int value)
    {
        if (parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty(propertyName, out JsonElement element) &&
            element.TryGetInt32(out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static bool HasCondition(XElement element) =>
        element.Attributes().Any(
            attribute => StringComparer.Ordinal.Equals(
                attribute.Name.LocalName,
                "Condition"));

    private static bool IsLiteral(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !value.Contains("$(", StringComparison.Ordinal) &&
        !value.Contains("${", StringComparison.Ordinal) &&
        !value.Contains("%(", StringComparison.Ordinal) &&
        !value.Contains("@(", StringComparison.Ordinal);

    private static string? ChildValue(XElement parent, string childName) =>
        parent.Elements()
            .SingleOrDefault(element => element.Name.LocalName == childName)
            ?.Value
            .Trim();

    private static string NormalizePythonName(string value)
    {
        var result = new System.Text.StringBuilder(value.Length);
        var previousSeparator = false;
        foreach (char character in value)
        {
            bool separator = character is '-' or '_' or '.';
            if (separator)
            {
                if (!previousSeparator)
                {
                    result.Append('-');
                }
            }
            else
            {
                result.Append(char.ToLowerInvariant(character));
            }

            previousSeparator = separator;
        }

        return result.ToString();
    }

    private static string EscapeJsonPointer(string value) =>
        value.Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal);

    private static string NormalizeRelativePath(string value) =>
        value.Replace('\\', '/');

    private static bool TryResolveRepositoryPath(
        string repositoryRoot,
        string artifactPath,
        out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(artifactPath) ||
            !StringComparer.Ordinal.Equals(artifactPath, NormalizeRelativePath(artifactPath)) ||
            Path.IsPathFullyQualified(artifactPath) ||
            artifactPath.Split('/').Any(segment =>
                segment.Length == 0 ||
                StringComparer.Ordinal.Equals(segment, ".") ||
                StringComparer.Ordinal.Equals(segment, "..")))
        {
            return false;
        }

        try
        {
            fullPath = Path.GetFullPath(
                Path.Combine(
                    repositoryRoot,
                    artifactPath.Replace('/', Path.DirectorySeparatorChar)));
            return IsWithinRepository(repositoryRoot, fullPath);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            fullPath = string.Empty;
            return false;
        }
    }

    private static bool IsWithinRepository(string repositoryRoot, string candidatePath)
    {
        string normalizedRoot = Path.GetFullPath(repositoryRoot);
        string normalizedCandidate = Path.GetFullPath(candidatePath);
        string rootPrefix = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;
        return normalizedCandidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static FrameworkEvidenceAuditOutcome CreateAuditOutcome(
        string artifactPath,
        ArtifactParseStatus status)
    {
        string safePath = artifactPath.Length is > 0 and <= _maximumRepositoryRelativePathLength &&
            StringComparer.Ordinal.Equals(artifactPath, NormalizeRelativePath(artifactPath))
            ? artifactPath
            : "(unknown)";
        return status switch
        {
            ArtifactParseStatus.Malformed => new(
                safePath,
                ScanCandidateOutcomeKind.Errored,
                ScanCandidateOutcomeReasons.FrameworkEvidenceMalformed,
                "框架证据格式无效。"),
            ArtifactParseStatus.UnsafePath => new(
                safePath,
                ScanCandidateOutcomeKind.Errored,
                ScanCandidateOutcomeReasons.FrameworkEvidenceUnsafePath,
                "框架证据路径不安全。"),
            ArtifactParseStatus.ReparsePoint => new(
                safePath,
                ScanCandidateOutcomeKind.Errored,
                ScanCandidateOutcomeReasons.FrameworkEvidenceReparsePoint,
                "框架证据包含重解析点。"),
            ArtifactParseStatus.ParseDepthExceeded => new(
                safePath,
                ScanCandidateOutcomeKind.Errored,
                ScanCandidateOutcomeReasons.FrameworkEvidenceBoundExceeded,
                "框架证据超出解析深度限制。"),
            ArtifactParseStatus.ArtifactBoundExceeded => new(
                safePath,
                ScanCandidateOutcomeKind.Errored,
                ScanCandidateOutcomeReasons.FrameworkEvidenceBoundExceeded,
                "框架证据超出单个文件限制。"),
            ArtifactParseStatus.AmbiguousScope => new(
                safePath,
                ScanCandidateOutcomeKind.Skipped,
                ScanCandidateOutcomeReasons.FrameworkEvidenceAmbiguousScope,
                "框架证据作用域不明确。"),
            ArtifactParseStatus.Valid or ArtifactParseStatus.Unsupported => new(
                safePath,
                ScanCandidateOutcomeKind.Skipped,
                ScanCandidateOutcomeReasons.FrameworkEvidenceUnsupported,
                "框架证据格式不受支持。"),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };
    }

    private static bool IsWithinScope(string codeUnitPath, string scopeRoot) =>
        scopeRoot.Length == 0 ||
        codeUnitPath.StartsWith($"{scopeRoot}/", StringComparison.Ordinal);

    private static string ScopeRoot(string artifactPath)
    {
        int separator = artifactPath.LastIndexOf('/');
        return separator < 0 ? string.Empty : artifactPath[..separator];
    }

    private static int ScopeDepth(string scopeRoot) =>
        scopeRoot.Length == 0 ? 0 : scopeRoot.Count(character => character == '/') + 1;

    private static string CombineRelative(string scopeRoot, string fileName) =>
        scopeRoot.Length == 0 ? fileName : $"{scopeRoot}/{fileName}";

    private enum ArtifactKind
    {
        NuGetProject,
        NuGetLock,
        NpmManifest,
        NpmLock,
        MavenProject,
        PythonRequirements,
    }

    private enum ArtifactParseStatus
    {
        Valid,
        Malformed,
        Unsupported,
        UnsafePath,
        ReparsePoint,
        ArtifactBoundExceeded,
        ParseDepthExceeded,
        AmbiguousScope,
    }

    private sealed record ArtifactReadResult(
        string? Content,
        FrameworkEvidenceAuditOutcome? AuditOutcome)
    {
        public static ArtifactReadResult Accepted(string content) => new(content, null);

        public static ArtifactReadResult Rejected(FrameworkEvidenceAuditOutcome auditOutcome) =>
            new(null, auditOutcome);
    }

    private sealed class FrameworkDetectionBudget(FrameworkDetectionLimits limits)
    {
        private long _aggregateArtifactBytes;
        private int _supportedArtifacts;
        private int _parsedPackageEntries;
        private int _candidateFacts;
        private readonly HashSet<FrameworkEvidenceAuditIdentity> _auditOutcomeIdentityKeys = [];
        private readonly List<FrameworkEvidenceAuditIdentity> _auditOutcomeIdentities = [];

        public int TotalAuditOutcomeCount { get; private set; }
        public IReadOnlyList<FrameworkEvidenceAuditIdentity> AuditOutcomeIdentities =>
            _auditOutcomeIdentities;

        public void RecordArtifactBytes(long bytes)
        {
            if (bytes < 0 ||
                _aggregateArtifactBytes > limits.MaximumAggregateArtifactBytes - bytes)
            {
                throw new FrameworkDetectionLimitExceededException();
            }

            _aggregateArtifactBytes += bytes;
        }

        public void RecordSupportedArtifact()
        {
            _supportedArtifacts++;
            EnsureWithinLimit(_supportedArtifacts, limits.MaximumSupportedArtifacts);
        }

        public void RecordParsedPackageEntry()
        {
            _parsedPackageEntries++;
            EnsureWithinLimit(
                _parsedPackageEntries,
                limits.MaximumParsedPackageEntries);
        }

        public void RecordCandidateFact()
        {
            _candidateFacts++;
            EnsureWithinLimit(_candidateFacts, limits.MaximumCandidateFacts);
        }

        public void RecordAuditOutcome(
            List<FrameworkEvidenceAuditOutcome> retainedOutcomes,
            FrameworkEvidenceAuditOutcome outcome)
        {
            var identity = new FrameworkEvidenceAuditIdentity(
                outcome.ArtifactPath,
                outcome.Outcome,
                outcome.Reason);
            if (!_auditOutcomeIdentityKeys.Add(identity))
            {
                return;
            }

            _auditOutcomeIdentities.Add(identity);
            TotalAuditOutcomeCount++;
            if (retainedOutcomes.Count < limits.MaximumRetainedAuditDetails)
            {
                retainedOutcomes.Add(outcome);
            }
        }

        private static void EnsureWithinLimit(int value, int maximum)
        {
            if (value > maximum)
            {
                throw new FrameworkDetectionLimitExceededException();
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1032:Implement standard exception constructors",
        Justification = "This private sentinel is used only for internal fail-closed budget control flow.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1064:Exceptions should be public",
        Justification = "The sentinel must not escape the detector boundary.")]
    private sealed class FrameworkDetectionLimitExceededException : Exception;
}

internal sealed class FrameworkDetectionLimits
{
    public FrameworkDetectionLimits(
        long maximumAggregateArtifactBytes,
        int maximumSupportedArtifacts,
        int maximumParsedPackageEntries,
        int maximumCandidateFacts,
        int maximumFinalFactsPerCodeUnit,
        int maximumRetainedAuditDetails)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumAggregateArtifactBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSupportedArtifacts);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumParsedPackageEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCandidateFacts);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumFinalFactsPerCodeUnit);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRetainedAuditDetails);

        MaximumAggregateArtifactBytes = maximumAggregateArtifactBytes;
        MaximumSupportedArtifacts = maximumSupportedArtifacts;
        MaximumParsedPackageEntries = maximumParsedPackageEntries;
        MaximumCandidateFacts = maximumCandidateFacts;
        MaximumFinalFactsPerCodeUnit = maximumFinalFactsPerCodeUnit;
        MaximumRetainedAuditDetails = maximumRetainedAuditDetails;
    }

    public static FrameworkDetectionLimits Production { get; } = new(
        maximumAggregateArtifactBytes: 64L * 1024 * 1024,
        maximumSupportedArtifacts: 1_000,
        maximumParsedPackageEntries: 100_000,
        maximumCandidateFacts: 250_000,
        maximumFinalFactsPerCodeUnit: 64,
        maximumRetainedAuditDetails: 100);

    public long MaximumAggregateArtifactBytes { get; }
    public int MaximumSupportedArtifacts { get; }
    public int MaximumParsedPackageEntries { get; }
    public int MaximumCandidateFacts { get; }
    public int MaximumFinalFactsPerCodeUnit { get; }
    public int MaximumRetainedAuditDetails { get; }
}
