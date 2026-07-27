using System.Diagnostics;
using System.Globalization;
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
    private const long _maximumInventoryOutputBytes = 16L * 1024 * 1024;
    private const int _maximumInventoryInputRecords = 100_000;
    private const int _maximumInventoryRecords = 1_000;
    private const int _maximumInventoryRecordBytes =
        _maximumRepositoryRelativePathLength + 96;
    private const int _maximumGitErrorBytes = 64 * 1024;
    private static readonly TimeSpan _inventoryDeadline = TimeSpan.FromSeconds(30);
    private static readonly XNamespace _legacyMsBuildNamespace =
        "http://schemas.microsoft.com/developer/msbuild/2003";
    private static readonly XNamespace _mavenPomNamespace =
        "http://maven.apache.org/POM/4.0.0";
    private static readonly HashSet<string> _nuGetRelevantElementNames =
        new(StringComparer.Ordinal)
        {
            "Project",
            "Sdk",
            "ItemGroup",
            "PackageReference",
            "FrameworkReference",
        };
    private static readonly HashSet<string> _mavenRelevantElementNames =
        new(StringComparer.Ordinal)
        {
            "project",
            "dependencies",
            "dependency",
            "groupId",
            "artifactId",
        };
    private static readonly UTF8Encoding _strictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly string[] _pythonVersionSpecifierOperators =
        ["===", "==", "!=", "~=", "<=", ">=", "<", ">"];
    private readonly FrameworkCatalog _catalog;
    private readonly FrameworkDetectionLimits _limits;
    private readonly ICommittedRepositorySnapshotSource _snapshotSource;

    public FrameworkEvidenceDetector(FrameworkCatalog catalog)
        : this(
            catalog,
            FrameworkDetectionLimits.Production,
            GitCommittedRepositorySnapshotSource.Instance)
    {
    }

    internal FrameworkEvidenceDetector(
        FrameworkCatalog catalog,
        FrameworkDetectionLimits limits)
        : this(catalog, limits, GitCommittedRepositorySnapshotSource.Instance)
    {
    }

    internal FrameworkEvidenceDetector(
        FrameworkCatalog catalog,
        FrameworkDetectionLimits limits,
        ICommittedRepositorySnapshotSource snapshotSource)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _limits = limits ?? throw new ArgumentNullException(nameof(limits));
        _snapshotSource = snapshotSource ?? throw new ArgumentNullException(nameof(snapshotSource));
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
        CommittedRepositorySnapshot snapshot =
            await _snapshotSource.CreateAsync(repositoryRoot, cancellationToken);
        string[] trackedPaths = [.. snapshot.TrackedEntries.Select(entry => entry.Path)];
        Dictionary<string, CommittedRepositoryEntry> trackedEntries =
            snapshot.TrackedEntries.ToDictionary(entry => entry.Path, StringComparer.Ordinal);
        var trackedSet = trackedPaths.ToHashSet(StringComparer.Ordinal);
        var readResults = new Dictionary<string, ArtifactReadResult>(StringComparer.Ordinal);
        var evidence = new List<PackageEvidence>();
        var directManifestScopes = new List<ValidatedDirectManifestOwner>();
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
                trackedEntries[artifactPath].Mode,
                trackedEntries[artifactPath].ObjectId,
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
                DirectManifestOwnerCandidate candidate = CreateDirectManifestOwnerCandidate(
                    artifactPath,
                    "nuget",
                    readResult);
                if (TryValidateDirectManifestOwner([candidate], out ValidatedDirectManifestOwner? owner))
                {
                    directManifestScopes.Add(owner!);
                }

                parseStatus = ValidateXml(readResult.Content!);
                if (parseStatus == ArtifactParseStatus.Valid)
                {
                    parseStatus = ParseNuGetProject(
                        artifactPath,
                        readResult.Content!,
                        evidence,
                        budget);
                }
            }
            else if (artifactKind == ArtifactKind.NpmManifest)
            {
                DirectManifestOwnerCandidate candidate = CreateDirectManifestOwnerCandidate(
                    artifactPath,
                    "npm",
                    readResult);
                if (TryValidateDirectManifestOwner([candidate], out ValidatedDirectManifestOwner? owner))
                {
                    directManifestScopes.Add(owner!);
                }

                parseStatus = ValidateJson(readResult.Content!, ArtifactKind.NpmManifest);
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
                    parseStatus = ParseMavenProject(
                        artifactPath,
                        readResult.Content!,
                        evidence,
                        budget);
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
                    parseStatus = ValidateJson(readResult.Content!, ArtifactKind.NuGetLock);
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
                    parseStatus = ValidateJson(readResult.Content!, ArtifactKind.NpmLock);
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
            budget.AuditOutcomeIdentities,
            directManifestScopes);
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
                detection.AuditOutcomeIdentities,
                detection.DirectManifestScopes);
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
            ValidatedDirectManifestOwner[] manifestScopes =
            [
                .. detection.DirectManifestScopes.Where(scope =>
                    StringComparer.Ordinal.Equals(scope.Ecosystem, ecosystem.Key) &&
                    IsWithinScope(normalizedCodeUnitPath, scope.ScopeRoot)),
            ];
            int deepestScope = Math.Max(
                ecosystem.Max(evidence => evidence.ScopeDepth),
                manifestScopes
                    .Select(scope => ScopeDepth(scope.ScopeRoot))
                    .DefaultIfEmpty(-1)
                    .Max());
            PackageEvidence[] winningScope =
            [
                .. ecosystem.Where(evidence => evidence.ScopeDepth == deepestScope),
            ];
            string[] directManifestPaths =
            [
                .. manifestScopes
                    .Where(scope => ScopeDepth(scope.ScopeRoot) == deepestScope)
                    .Select(scope => scope.ArtifactPath)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(path => path, StringComparer.Ordinal),
            ];
            if (directManifestPaths.Length == 0)
            {
                directManifestPaths =
                [
                    .. winningScope
                        .Where(evidence =>
                            evidence.Relationship == PackageEvidenceRelationship.Direct)
                        .Select(evidence => evidence.ArtifactPath)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(path => path, StringComparer.Ordinal),
                ];
            }

            if (directManifestPaths.Length > 1)
            {
                FrameworkEvidenceAuditOutcome audit =
                    CreateAuditOutcome(
                        directManifestPaths[0],
                        ArtifactParseStatus.AmbiguousScope);
                var identity = new FrameworkEvidenceAuditIdentity(
                    audit.ArtifactPath,
                    audit.Outcome,
                    audit.Reason);
                if (auditKeys.Add(identity))
                {
                    auditOutcomeIdentities.Add(identity);
                    additionalAuditOutcomeCount++;
                    if (auditOutcomes.Count < _limits.MaximumRetainedAuditDetails)
                    {
                        auditOutcomes.Add(audit);
                    }
                }

                continue;
            }

            if (winningScope.Length == 0)
            {
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
                auditOutcomeIdentities,
                detection.DirectManifestScopes);
    }

    internal static async Task<CommittedRepositoryEntry[]> InventoryTrackedEntriesAsync(
        string repositoryRoot,
        string committedRepositoryIdentity,
        CancellationToken cancellationToken) =>
        await InventoryTrackedEntriesAsync(
            repositoryRoot,
            committedRepositoryIdentity,
            _maximumInventoryOutputBytes,
            _maximumInventoryRecords,
            _inventoryDeadline,
            cancellationToken);

    internal static async Task<CommittedRepositoryEntry[]> InventoryTrackedEntriesAsync(
        string repositoryRoot,
        string committedRepositoryIdentity,
        long maximumOutputBytes,
        int maximumRecords,
        TimeSpan deadline,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumOutputBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRecords);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(deadline, TimeSpan.Zero);

        using Process process = CreateGitProcess(
            repositoryRoot,
            "ls-tree",
            "-r",
            "-z",
            "--full-tree",
            committedRepositoryIdentity,
            "--");
        using var deadlineSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        deadlineSource.CancelAfter(deadline);
        CancellationToken boundedToken = deadlineSource.Token;
        Task? standardError = null;
        var started = false;
        try
        {
            started = process.Start();
            if (!started)
            {
                throw new InvalidOperationException(
                    "Unable to inventory committed Repository evidence.");
            }

            standardError = DrainBoundedAsync(
                process.StandardError.BaseStream,
                _maximumGitErrorBytes,
                boundedToken);
            var entries = new List<CommittedRepositoryEntry>(
                Math.Min(maximumRecords, _maximumInventoryRecords));
            var inputRecordCount = 0;
            using var recordBuffer = new MemoryStream(_maximumInventoryRecordBytes);
            byte[] readBuffer = new byte[16 * 1024];
            long totalBytes = 0;
            while (true)
            {
                int read = await process.StandardOutput.BaseStream.ReadAsync(
                    readBuffer,
                    boundedToken);
                if (read == 0)
                {
                    break;
                }

                totalBytes = checked(totalBytes + read);
                if (totalBytes > maximumOutputBytes)
                {
                    throw new FrameworkDetectionLimitExceededException();
                }

                for (var index = 0; index < read; index++)
                {
                    byte value = readBuffer[index];
                    if (value == 0)
                    {
                        if (recordBuffer.Length == 0)
                        {
                            throw new InvalidOperationException(
                                "Unable to inventory committed Repository evidence.");
                        }

                        inputRecordCount++;
                        if (inputRecordCount > _maximumInventoryInputRecords)
                        {
                            throw new FrameworkDetectionLimitExceededException();
                        }

                        CommittedRepositoryEntry entry =
                            ParseCommittedRepositoryEntry(recordBuffer);
                        if (GetArtifactKind(entry.Path) is not null)
                        {
                            if (entries.Count >= maximumRecords)
                            {
                                throw new FrameworkDetectionLimitExceededException();
                            }

                            entries.Add(entry);
                        }

                        recordBuffer.SetLength(0);
                    }
                    else
                    {
                        if (recordBuffer.Length >= _maximumInventoryRecordBytes)
                        {
                            throw new FrameworkDetectionLimitExceededException();
                        }

                        recordBuffer.WriteByte(value);
                    }
                }
            }

            await process.WaitForExitAsync(boundedToken);
            await standardError;
            if (process.ExitCode != 0 || recordBuffer.Length != 0)
            {
                throw new InvalidOperationException(
                    "Unable to inventory committed Repository evidence.");
            }

            return [.. entries];
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FrameworkDetectionLimitExceededException();
        }
        finally
        {
            if (started && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            if (standardError is not null)
            {
                try
                {
                    await standardError;
                }
                catch (OperationCanceledException)
                {
                }
                catch (FrameworkDetectionLimitExceededException)
                {
                }
            }
        }
    }

    internal static async Task<string> ResolveCommittedRepositoryIdentityAsync(
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        using Process process = CreateGitProcess(repositoryRoot, "rev-parse", "--verify", "HEAD^{commit}");
        if (!process.Start())
        {
            throw new InvalidOperationException(
                "Unable to resolve committed Repository evidence.");
        }

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        await standardError;
        string identity = (await standardOutput).Trim();
        if (process.ExitCode != 0 ||
            identity.Length is not (40 or 64) ||
            !identity.All(char.IsAsciiHexDigit))
        {
            throw new InvalidOperationException(
                "Unable to resolve committed Repository evidence.");
        }

        return identity;
    }

    private static async Task<ArtifactReadResult> ReadTrackedTextAsync(
        string repositoryRoot,
        string artifactPath,
        string committedMode,
        string committedObjectId,
        FrameworkDetectionBudget budget,
        CancellationToken cancellationToken)
    {
        if (artifactPath.Length > _maximumRepositoryRelativePathLength)
        {
            return ArtifactReadResult.Rejected(
                CreateAuditOutcome(artifactPath, ArtifactParseStatus.ArtifactBoundExceeded));
        }

        if (!TryResolveRepositoryPath(repositoryRoot, artifactPath, out _))
        {
            return ArtifactReadResult.Rejected(
                CreateAuditOutcome(artifactPath, ArtifactParseStatus.UnsafePath));
        }

        if (!IsRegularCommittedBlobMode(committedMode))
        {
            ArtifactParseStatus status = committedMode == "120000"
                ? await ClassifyCommittedSymbolicLinkAsync(
                    repositoryRoot,
                    committedObjectId,
                    artifactPath,
                    cancellationToken)
                : ArtifactParseStatus.ReparsePoint;
            return ArtifactReadResult.Rejected(
                CreateAuditOutcome(artifactPath, status));
        }

        try
        {
            long? blobLength = await ReadCommittedBlobLengthAsync(
                repositoryRoot,
                committedObjectId,
                cancellationToken);
            if (blobLength is null)
            {
                return ArtifactReadResult.Rejected(
                    CreateAuditOutcome(artifactPath, ArtifactParseStatus.Unsupported));
            }

            budget.RecordArtifactBytes(blobLength.Value);
            if (blobLength > _maximumArtifactBytes)
            {
                return ArtifactReadResult.Rejected(
                    CreateAuditOutcome(artifactPath, ArtifactParseStatus.ArtifactBoundExceeded));
            }

            using Process process = CreateGitProcess(
                repositoryRoot,
                "cat-file",
                "blob",
                committedObjectId);
            if (!process.Start())
            {
                return ArtifactReadResult.Rejected(
                    CreateAuditOutcome(artifactPath, ArtifactParseStatus.Unsupported));
            }

            Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            using var contentBuffer = new MemoryStream(
                capacity: checked((int)blobLength.Value));
            byte[] buffer = new byte[64 * 1024];
            int totalBytes = 0;
            while (true)
            {
                int read = await process.StandardOutput.BaseStream.ReadAsync(
                    buffer,
                    cancellationToken);
                if (read == 0)
                {
                    break;
                }

                totalBytes += read;
                if (totalBytes > blobLength)
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

            await process.WaitForExitAsync(cancellationToken);
            await standardError;
            if (process.ExitCode != 0 || totalBytes != blobLength)
            {
                return ArtifactReadResult.Rejected(
                    CreateAuditOutcome(artifactPath, ArtifactParseStatus.Unsupported));
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

    private static async Task<ArtifactParseStatus> ClassifyCommittedSymbolicLinkAsync(
        string repositoryRoot,
        string committedObject,
        string artifactPath,
        CancellationToken cancellationToken)
    {
        long? blobLength = await ReadCommittedBlobLengthAsync(
            repositoryRoot,
            committedObject,
            cancellationToken);
        if (blobLength is null or < 1 or > _maximumRepositoryRelativePathLength)
        {
            return ArtifactParseStatus.UnsafePath;
        }

        using Process process = CreateGitProcess(
            repositoryRoot,
            "cat-file",
            "blob",
            committedObject);
        if (!process.Start())
        {
            return ArtifactParseStatus.ReparsePoint;
        }

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        await standardError;
        string target = await standardOutput;
        if (process.ExitCode != 0 || Encoding.UTF8.GetByteCount(target) != blobLength)
        {
            return ArtifactParseStatus.UnsafePath;
        }

        try
        {
            string linkPath = Path.Combine(
                repositoryRoot,
                artifactPath.Replace('/', Path.DirectorySeparatorChar));
            string targetPath = Path.IsPathFullyQualified(target)
                ? Path.GetFullPath(target)
                : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(linkPath)!, target));
            return IsWithinRepository(repositoryRoot, targetPath)
                ? ArtifactParseStatus.ReparsePoint
                : ArtifactParseStatus.UnsafePath;
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return ArtifactParseStatus.UnsafePath;
        }
    }

    private static async Task<long?> ReadCommittedBlobLengthAsync(
        string repositoryRoot,
        string committedObject,
        CancellationToken cancellationToken)
    {
        using Process process = CreateGitProcess(
            repositoryRoot,
            "cat-file",
            "-s",
            committedObject);
        if (!process.Start())
        {
            return null;
        }

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        await standardError;
        string output = (await standardOutput).Trim();
        return process.ExitCode == 0 &&
            long.TryParse(
                output,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long length) &&
            length >= 0
            ? length
            : null;
    }

    private static Process CreateGitProcess(
        string repositoryRoot,
        params string[] arguments)
    {
        var process = new Process
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
        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        return process;
    }

    private static CommittedRepositoryEntry ParseCommittedRepositoryEntry(
        MemoryStream recordBuffer)
    {
        string record = _strictUtf8.GetString(
            recordBuffer.GetBuffer(),
            0,
            checked((int)recordBuffer.Length));
        int pathSeparator = record.IndexOf('\t', StringComparison.Ordinal);
        string[] metadata = pathSeparator > 0
            ? record[..pathSeparator].Split(' ', StringSplitOptions.RemoveEmptyEntries)
            : [];
        string path = pathSeparator >= 0 && pathSeparator < record.Length - 1
            ? NormalizeRelativePath(record[(pathSeparator + 1)..])
            : string.Empty;
        if (metadata.Length != 3 ||
            !StringComparer.Ordinal.Equals(metadata[1], "blob") ||
            metadata[2].Length is not (40 or 64) ||
            !metadata[2].All(char.IsAsciiHexDigit) ||
            path.Length == 0 ||
            !StringComparer.Ordinal.Equals(path, NormalizeRelativePath(path)) ||
            Path.IsPathFullyQualified(path) ||
            path.Split('/').Any(segment =>
                segment.Length == 0 ||
                StringComparer.Ordinal.Equals(segment, ".") ||
                StringComparer.Ordinal.Equals(segment, "..")))
        {
            throw new InvalidOperationException(
                "Unable to inventory committed Repository evidence.");
        }

        return new(path, metadata[0], metadata[2]);
    }

    private static async Task DrainBoundedAsync(
        Stream stream,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[4 * 1024];
        var totalBytes = 0;
        while (true)
        {
            int read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return;
            }

            totalBytes = checked(totalBytes + read);
            if (totalBytes > maximumBytes)
            {
                throw new FrameworkDetectionLimitExceededException();
            }
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
            !StringComparer.Ordinal.Equals(project.Name.LocalName, "Project") ||
            !IsSupportedRootNamespace(project, _legacyMsBuildNamespace) ||
            HasForeignRelevantElement(project, _nuGetRelevantElementNames))
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

        XNamespace projectNamespace = project.Name.Namespace;
        foreach (XElement sdk in project.Elements(projectNamespace + "Sdk"))
        {
            if (HasCondition(sdk) || !IsLiteral(sdk.Attribute("Name")?.Value))
            {
                return DirectManifestOwnerParseStatus.ConditionalOrDynamic;
            }

            hasLiteralOwnership = true;
        }

        foreach (XElement declaration in project.Descendants().Where(element =>
                     element.Name.Namespace == projectNamespace &&
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
        ArtifactParseStatus validation = ValidateJson(content, ArtifactKind.NpmManifest);
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

    private static ArtifactParseStatus ValidateJson(
        string content,
        ArtifactKind artifactKind)
    {
        if (ExceedsJsonDepth(content))
        {
            return ArtifactParseStatus.ParseDepthExceeded;
        }

        using JsonDocument? document = ParseJson(content);
        if (document is null)
        {
            return ArtifactParseStatus.Malformed;
        }

        return HasDuplicateRelevantJsonProperties(document.RootElement, artifactKind)
            ? ArtifactParseStatus.Unsupported
            : ArtifactParseStatus.Valid;
    }

    private static bool HasDuplicateRelevantJsonProperties(
        JsonElement root,
        ArtifactKind artifactKind)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (artifactKind == ArtifactKind.NpmManifest)
        {
            foreach (string mapName in new[]
                     {
                         "dependencies",
                         "devDependencies",
                         "optionalDependencies",
                     })
            {
                if (HasDuplicateJsonProperty(root, mapName))
                {
                    return true;
                }

                if (root.TryGetProperty(mapName, out JsonElement dependencies) &&
                    dependencies.ValueKind == JsonValueKind.Object &&
                    HasDuplicateJsonPropertyNames(dependencies))
                {
                    return true;
                }
            }

            return false;
        }

        if (artifactKind == ArtifactKind.NpmLock)
        {
            return HasDuplicateJsonProperty(root, "lockfileVersion") ||
                HasDuplicateJsonProperty(root, "packages") ||
                (root.TryGetProperty("packages", out JsonElement packages) &&
                 packages.ValueKind == JsonValueKind.Object &&
                 HasDuplicateJsonPropertyNames(packages));
        }

        if (artifactKind != ArtifactKind.NuGetLock)
        {
            return false;
        }

        if (HasDuplicateJsonProperty(root, "dependencies"))
        {
            return true;
        }

        if (!root.TryGetProperty("dependencies", out JsonElement targets) ||
            targets.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (HasDuplicateJsonPropertyNames(targets))
        {
            return true;
        }

        foreach (JsonProperty target in targets.EnumerateObject())
        {
            if (target.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (HasDuplicateJsonPropertyNames(target.Value))
            {
                return true;
            }

            foreach (JsonProperty package in target.Value.EnumerateObject())
            {
                if (package.Value.ValueKind == JsonValueKind.Object &&
                    HasDuplicateJsonProperty(package.Value, "type"))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasDuplicateJsonProperty(
        JsonElement parent,
        string propertyName)
    {
        var found = false;
        foreach (JsonProperty property in parent.EnumerateObject())
        {
            if (!StringComparer.Ordinal.Equals(property.Name, propertyName))
            {
                continue;
            }

            if (found)
            {
                return true;
            }

            found = true;
        }

        return false;
    }

    private static bool HasDuplicateJsonPropertyNames(JsonElement parent)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        return parent.EnumerateObject().Any(property => !names.Add(property.Name));
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

    private ArtifactParseStatus ParseNuGetProject(
        string artifactPath,
        string content,
        ICollection<PackageEvidence> evidence,
        FrameworkDetectionBudget budget)
    {
        XDocument? document = ParseXml(content);
        XElement? project = document?.Root;
        if (project is null ||
            !StringComparer.Ordinal.Equals(project.Name.LocalName, "Project") ||
            !IsSupportedRootNamespace(project, _legacyMsBuildNamespace) ||
            HasForeignRelevantElement(project, _nuGetRelevantElementNames) ||
            HasCondition(project))
        {
            return ArtifactParseStatus.Unsupported;
        }

        XNamespace projectNamespace = project.Name.Namespace;
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

        foreach (XElement sdk in project.Elements(projectNamespace + "Sdk")
                     .Where(element => !HasCondition(element)))
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

        foreach (XElement itemGroup in project.Elements(projectNamespace + "ItemGroup")
                     .Where(element => !HasCondition(element)))
        {
            foreach (XElement item in itemGroup.Elements().Where(element =>
                         element.Name.Namespace == projectNamespace &&
                         !HasCondition(element)))
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

        return ArtifactParseStatus.Valid;
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

    private ArtifactParseStatus ParseMavenProject(
        string artifactPath,
        string content,
        ICollection<PackageEvidence> evidence,
        FrameworkDetectionBudget budget)
    {
        XDocument? document = ParseXml(content);
        XElement? project = document?.Root;
        if (project is null ||
            !StringComparer.Ordinal.Equals(project.Name.LocalName, "project") ||
            !IsSupportedRootNamespace(project, _mavenPomNamespace) ||
            HasForeignRelevantElement(project, _mavenRelevantElementNames))
        {
            return ArtifactParseStatus.Unsupported;
        }

        XNamespace projectNamespace = project.Name.Namespace;
        var coordinates = new List<(string GroupId, string ArtifactId)>();
        foreach (XElement dependencies in project.Elements(projectNamespace + "dependencies"))
        {
            foreach (XElement dependency in dependencies.Elements(projectNamespace + "dependency"))
            {
                budget.RecordParsedPackageEntry();
                if (!TryGetUnambiguousChildValue(
                        dependency,
                        projectNamespace,
                        "groupId",
                        out string? groupId) ||
                    !TryGetUnambiguousChildValue(
                        dependency,
                        projectNamespace,
                        "artifactId",
                        out string? artifactId))
                {
                    return ArtifactParseStatus.Unsupported;
                }

                if (!IsLiteral(groupId) || !IsLiteral(artifactId))
                {
                    continue;
                }

                coordinates.Add((groupId!, artifactId!));
            }
        }

        foreach ((string groupId, string artifactId) in coordinates)
        {
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

        return ArtifactParseStatus.Valid;
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

            if (!TryParsePythonLiteralRequirement(line, out string packageName))
            {
                continue;
            }

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

    private static bool TryParsePythonLiteralRequirement(
        string line,
        out string packageName)
    {
        packageName = string.Empty;
        int nameLength = 0;
        while (nameLength < line.Length &&
            (char.IsAsciiLetterOrDigit(line[nameLength]) ||
             line[nameLength] is '-' or '_' or '.'))
        {
            nameLength++;
        }

        if (nameLength == 0)
        {
            return false;
        }

        string specifiers = line[nameLength..];
        if (specifiers.Length > 0 &&
            specifiers.Split(',').Any(specifier => !IsPythonVersionSpecifier(specifier.Trim())))
        {
            return false;
        }

        packageName = NormalizePythonName(line[..nameLength]);
        return true;
    }

    private static bool IsPythonVersionSpecifier(string specifier)
    {
        string? operation = _pythonVersionSpecifierOperators
            .FirstOrDefault(candidate => specifier.StartsWith(candidate, StringComparison.Ordinal));
        if (operation is null || specifier.Length == operation.Length)
        {
            return false;
        }

        return specifier[operation.Length..].All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '.' or '-' or '_' or '+' or '*');
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

    private static bool IsRegularCommittedBlobMode(string mode) =>
        mode is "100644" or "100755";

    private static bool TryGetUnambiguousChildValue(
        XElement parent,
        XNamespace childNamespace,
        string childName,
        out string? value)
    {
        value = null;
        var found = false;
        foreach (XElement child in parent.Elements(childNamespace + childName))
        {
            if (found)
            {
                value = null;
                return false;
            }

            found = true;
            value = child.Value.Trim();
        }

        return true;
    }

    private static bool IsSupportedRootNamespace(
        XElement root,
        XNamespace supportedNamespace) =>
        root.Name.Namespace == XNamespace.None ||
        root.Name.Namespace == supportedNamespace;

    private static bool HasForeignRelevantElement(
        XElement root,
        HashSet<string> relevantElementNames)
    {
        XNamespace rootNamespace = root.Name.Namespace;
        return root.DescendantsAndSelf().Any(element =>
            relevantElementNames.Contains(element.Name.LocalName) &&
            element.Name.Namespace != rootNamespace);
    }

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

internal interface ICommittedRepositorySnapshotSource
{
    Task<CommittedRepositorySnapshot> CreateAsync(
        string repositoryRoot,
        CancellationToken cancellationToken);
}

internal sealed record CommittedRepositorySnapshot(
    string RepositoryIdentity,
    CommittedRepositoryEntry[] TrackedEntries);

internal sealed record CommittedRepositoryEntry(
    string Path,
    string Mode,
    string ObjectId);

internal sealed class GitCommittedRepositorySnapshotSource
    : ICommittedRepositorySnapshotSource
{
    private GitCommittedRepositorySnapshotSource()
    {
    }

    public static GitCommittedRepositorySnapshotSource Instance { get; } = new();

    public async Task<CommittedRepositorySnapshot> CreateAsync(
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        string repositoryIdentity =
            await FrameworkEvidenceDetector.ResolveCommittedRepositoryIdentityAsync(
                repositoryRoot,
                cancellationToken);
        CommittedRepositoryEntry[] trackedEntries =
            await FrameworkEvidenceDetector.InventoryTrackedEntriesAsync(
                repositoryRoot,
                repositoryIdentity,
                cancellationToken);
        return new CommittedRepositorySnapshot(repositoryIdentity, trackedEntries);
    }
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
