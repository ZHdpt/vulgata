using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.BackupB;
using Vulgata.Core.Entities;
using RepositoryEntity = Vulgata.Core.Entities.Repository;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Infrastructure.Git.BackupB;

/// <summary>
/// Test/demo-only provisioner for the Backup B deadline contingency.
/// Creates pre-scan state directly from a reviewed manifest without invoking
/// the retired legacy analysis provider.
/// </summary>
public sealed class BackupBDirectProvisioner : IBackupBDirectProvisioner
{
    private readonly ISystemRepository _systemRepository;
    private readonly IRepositoryRepository _repositoryRepository;
    private readonly IScanRunRepository _scanRunRepository;
    private readonly IScanExecutionStore _scanExecutionStore;
    private readonly BackupBGitSourceReader _gitSourceReader;

    public BackupBDirectProvisioner(
        ISystemRepository systemRepository,
        IRepositoryRepository repositoryRepository,
        IScanRunRepository scanRunRepository,
        IScanExecutionStore scanExecutionStore)
    {
        _systemRepository = systemRepository;
        _repositoryRepository = repositoryRepository;
        _scanRunRepository = scanRunRepository;
        _scanExecutionStore = scanExecutionStore;
        _gitSourceReader = new BackupBGitSourceReader();
    }

    public async Task<BackupBProvisioningResult> ProvisionAsync(
        BackupBProvisioningRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        var now = DateTimeOffset.UtcNow;

        // Step 1: Create the System
        var system = new SystemEntity(
            request.SystemName,
            request.SystemDescription,
            context: "Backup B: prepared-scope LLM demonstration",
            now);
        system = await _systemRepository.AddAsync(system, cancellationToken);
        await _systemRepository.SaveChangesAsync(cancellationToken);

        var repositoryIds = new Dictionary<string, Guid>();
        var scanRunIds = new Dictionary<string, Guid>();
        var codeUnitIds = new Dictionary<(string RepositoryAlias, string FilePath, string SymbolName), Guid>();
        var preScanResultIds = new Dictionary<string, Guid>();

        // Step 2: For each repository, create entities from manifest
        foreach (var (repoAlias, repoInput) in request.Repositories)
        {
            // Create Repository entity
            var repository = RepositoryEntity.Create(
                system.Id,
                repoInput.Name,
                repoInput.CloneUrl,
                repoInput.Description,
                context: null,
                now);
            repository = await _repositoryRepository.AddAsync(repository, cancellationToken);
            repositoryIds[repoAlias] = repository.Id;

            // Create ScanRun for this repository
            var scanRun = ScanRun.Start(
                repository.Id,
                providerProcessTimeoutSeconds: 300,
                now);
            await _scanRunRepository.AddAsync(scanRun, cancellationToken);
            scanRunIds[repoAlias] = scanRun.Id;

            // Set candidate count (number of files)
            scanRun.SetCandidateCount(repoInput.ManifestEntries.Count);
            scanRun.BindProfiledRevision(repoInput.ExpectedCommit.ToLowerInvariant());

            // Flush ScanRun to database in Profiling status
            await _scanRunRepository.SaveChangesAsync(cancellationToken);

            var codeUnits = new List<CodeUnit>();
            var codeUnitsByFile = new Dictionary<string, List<CodeUnit>>();

            // For each manifest entry (file)
            foreach (var entry in repoInput.ManifestEntries)
            {
                // Read file from Git at exact commit (use local working copy, NOT clone URL)
                var sourceFile = await _gitSourceReader.ReadFileAsync(
                    repoInput.LocalGitPath,
                    repoInput.ExpectedCommit,
                    entry.FilePath,
                    cancellationToken);

                // Verify file identity hash
                var computedFileHash = ComputeSha256(sourceFile.Content);

                // For each code unit in the manifest entry
                foreach (var codeUnitEntry in entry.CodeUnits)
                {
                    // Parse CodeUnitType from string
                    if (!Enum.TryParse<CodeUnitType>(codeUnitEntry.UnitType, ignoreCase: true, out var unitType))
                    {
                        throw new ArgumentException(
                            $"Invalid CodeUnitType: {codeUnitEntry.UnitType}. " +
                            $"Valid values: {string.Join(", ", Enum.GetNames<CodeUnitType>())}");
                    }

                    // Compute source slice hash from line range
                    var sourceSliceHash = _gitSourceReader.ComputeSourceSliceHash(
                        sourceFile.Content,
                        codeUnitEntry.LineStart,
                        codeUnitEntry.LineEnd);

                    var codeUnit = new CodeUnit(
                        scanRun.Id,
                        repository.Id,
                        entry.FilePath,
                        computedFileHash,
                        sourceSliceHash,
                        codeUnitEntry.LineStart,
                        codeUnitEntry.LineEnd,
                        codeUnitEntry.SymbolName,
                        unitType,
                        RangeConfidence.Exact,
                        codeUnitEntry.Language);

                    codeUnits.Add(codeUnit);

                    if (!codeUnitsByFile.TryGetValue(entry.FilePath, out var fileList))
                    {
                        fileList = new List<CodeUnit>();
                        codeUnitsByFile[entry.FilePath] = fileList;
                    }
                    fileList.Add(codeUnit);

                    codeUnitIds[(repoAlias, entry.FilePath, codeUnitEntry.SymbolName)] = codeUnit.Id;
                }
            }

            // Create ScanRunLanguageSummary for each distinct language
            var languageSummaries = new List<ScanRunLanguageSummary>();
            var languageGroups = codeUnits.GroupBy(cu => cu.Language!, StringComparer.Ordinal);
            foreach (var languageGroup in languageGroups)
            {
                var distinctFileCount = languageGroup
                    .Select(cu => cu.FilePath)
                    .Distinct(StringComparer.Ordinal)
                    .Count();
                var languageSummary = new ScanRunLanguageSummary(
                    scanRun.Id,
                    repository.Id,
                    languageGroup.Key,
                    distinctFileCount);
                languageSummaries.Add(languageSummary);
            }

            // Create ScanCandidateOutcome for each file
            var candidateOutcomes = new List<ScanCandidateOutcome>();
            foreach (var entry in repoInput.ManifestEntries)
            {
                var outcome = new ScanCandidateOutcome(
                    scanRun.Id,
                    entry.FilePath,
                    ScanCandidateOutcomeKind.Parsed,
                    "Backup B prepared scope",
                    auditGroupOrdinal: 0,
                    now);
                candidateOutcomes.Add(outcome);
            }

            // Add structural records (code units, edges, unresolved relationships)
            _scanRunRepository.AddStructuralRecords(
                codeUnits,
                edges: [],
                unresolvedRelationships: []);

            // Add candidate outcomes
            _scanRunRepository.AddCandidateOutcomes(candidateOutcomes);

            // Complete the ScanRun lifecycle in-memory (NOT persisted yet).
            // RepositoryProfileDraft.Validate() requires a completed ScanRun.
            // PersistCompletedProfileAsync reads from DB and expects Profiling status,
            // then transitions it to Completed atomically within its transaction.
            scanRun.Complete(
                parsedFileCount: repoInput.ManifestEntries.Count,
                codeUnitCount: codeUnits.Count,
                now,
                message: "Backup B prepared scope provisioning complete");

            // Create PreScanResult (approved)
            var fingerprint = ComputePreScanFingerprint(scanRun.Id, repository.Id, repoInput.ExpectedCommit);
            var classifications = BuildPreScanClassifications(scanRun.Id, repoInput.ManifestEntries);
            var totalConfirmed = classifications
                .Where(c => c.Level == ClassificationLevel.Confirmed)
                .Sum(c => c.OccurrenceCount);
            var totalHints = classifications
                .Where(c => c.Level == ClassificationLevel.Hint)
                .Sum(c => c.OccurrenceCount);
            var totalWarnings = classifications
                .Where(c => c.Level == ClassificationLevel.Warning)
                .Sum(c => c.OccurrenceCount);
            var confirmedKinds = ConfirmedCommunicationKinds.Rpc;

            var preScanResult = PreScanResult.CreateApproved(
                repository.Id,
                scanRun.Id,
                repoInput.ExpectedCommit.ToLowerInvariant(),
                now,
                fingerprint,
                "预扫描已批准——继续进入顺序演示运行器",
                confirmedKinds,
                totalConfirmed,
                totalHints,
                totalWarnings,
                evidenceTruncated: false,
                classifications);

            preScanResultIds[repoAlias] = preScanResult.Id;

            // Create RepositoryProfileCommitDraft and persist
            var profileDraft = RepositoryProfileDraft.Create(
                scanRun,
                codeUnits,
                codeUnitEdges: [],
                unresolvedRelationships: [],
                codeUnitFrameworks: [],
                languageSummaries,
                candidateOutcomes);

            var preScanResultDraft = PreScanResultDraft.Create(preScanResult);

            var commitDraft = RepositoryProfileCommitDraft.Create(profileDraft, preScanResultDraft);

            await _scanRunRepository.PersistCompletedProfileAsync(commitDraft, cancellationToken);

            // NOTE: Do NOT create ScanExecution here. SequentialScanRunner.StartAsync
            // will create it when the demo runner starts the pipeline. Creating it
            // here would cause DuplicateActiveScan failure.
        }

        await _repositoryRepository.SaveChangesAsync(cancellationToken);

        return new BackupBProvisioningResult
        {
            ScanRunIds = scanRunIds,
            CodeUnitIds = codeUnitIds,
            PreScanResultIds = preScanResultIds,
            SystemId = system.Id,
            RepositoryIds = repositoryIds
        };
    }

    private static void ValidateRequest(BackupBProvisioningRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SystemName))
        {
            throw new ArgumentException("System name is required", nameof(request));
        }

        if (request.Repositories.Count == 0)
        {
            throw new ArgumentException("At least one repository is required", nameof(request));
        }

        foreach (var kvp in request.Repositories)
        {
            var repo = kvp.Value;

            if (string.IsNullOrWhiteSpace(repo.Name))
            {
                throw new ArgumentException("Repository name is required");
            }

            if (string.IsNullOrWhiteSpace(repo.CloneUrl))
            {
                throw new ArgumentException("Repository clone URL is required");
            }

            if (string.IsNullOrWhiteSpace(repo.ExpectedCommit))
            {
                throw new ArgumentException("Expected commit is required");
            }

            if (repo.ExpectedCommit.Length != 40 ||
                repo.ExpectedCommit.Any(c => !Uri.IsHexDigit(c)))
            {
                throw new ArgumentException(
                    "Expected commit must be a 40-character hex Git SHA");
            }

            if (repo.ManifestEntries.Count == 0)
            {
                throw new ArgumentException(
                    "At least one manifest entry is required per repository");
            }

            foreach (var entry in repo.ManifestEntries)
            {
                if (string.IsNullOrWhiteSpace(entry.FilePath))
                {
                    throw new ArgumentException("File path is required in manifest entry");
                }

                if (entry.CodeUnits.Count == 0)
                {
                    throw new ArgumentException(
                        "At least one code unit is required per manifest entry");
                }

                foreach (var cu in entry.CodeUnits)
                {
                    if (string.IsNullOrWhiteSpace(cu.SymbolName))
                    {
                        throw new ArgumentException("Symbol name is required in code unit entry");
                    }

                    if (cu.LineStart < 1)
                    {
                        throw new ArgumentException("Line start must be >= 1");
                    }

                    if (cu.LineEnd < cu.LineStart)
                    {
                        throw new ArgumentException("Line end must be >= line start");
                    }
                }
            }
        }
    }

    private static string ComputeSha256(string content)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputePreScanFingerprint(Guid scanRunId, Guid repositoryId, string commit)
    {
        var input = $"{scanRunId}:{repositoryId}:{commit}";
        return ComputeSha256(input);
    }

    private static List<CommunicationKindClassification> BuildPreScanClassifications(
        Guid scanRunId,
        IReadOnlyList<BackupBManifestEntry> manifestEntries)
    {
        // Single RPC classification for the prepared scope
        var fingerprint = ComputeSha256($"prepared-scope:{scanRunId}:rpc");

        // Use the first manifest entry for path information
        var firstEntry = manifestEntries[0];
        var relativePath = firstEntry.FilePath;
        var locator = $"{relativePath}#rpc-invocation";

        var classification = CommunicationKindClassification.Create(
            scanRunId,
            CommunicationKind.Rpc,
            ClassificationLevel.Confirmed,
            ConfidenceBand.High,
            EvidenceRule.RpcDirectAnnotation,
            EvidenceProvenance.StructuralAnnotation,
            relativePath,
            locator,
            occurrenceCount: manifestEntries.Count,
            fingerprint);

        return [classification];
    }
}
