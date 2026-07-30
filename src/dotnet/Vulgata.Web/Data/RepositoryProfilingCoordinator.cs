using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CommunicationDetection;
using Vulgata.Infrastructure.FrameworkDetection;
using Vulgata.Shared.Repositories;
using RepositoryEntity = Vulgata.Core.Entities.Repository;

namespace Vulgata.Web.Data;

public enum RepositoryProfilingOutcome
{
    Success,
    RepositoryNotFound,
    GitFailure,
    ProviderUnavailable,
    ProviderFailure,
    CandidateAuditBoundExceeded,
}

public sealed record RepositoryProfilingResult(
    RepositoryProfilingOutcome Outcome,
    ScanRunSummaryDto? Value = null,
    string? Message = null);

public interface IRepositoryProfilingCoordinator
{
    Task<RepositoryProfilingResult> StartSystemRepositoryAsync(
        Guid systemId,
        Guid repositoryId,
        IUserContext user,
        CancellationToken cancellationToken = default);

    Task<RepositoryProfilingResult> StartStandaloneRepositoryAsync(
        Guid repositoryId,
        IUserContext user,
        CancellationToken cancellationToken = default);

    Task<ScanRunSummaryDto?> GetSystemRepositoryRunAsync(
        Guid systemId,
        Guid repositoryId,
        Guid scanRunId,
        IUserContext user,
        CancellationToken cancellationToken = default);

    Task<ScanRunSummaryDto?> GetStandaloneRepositoryRunAsync(
        Guid repositoryId,
        Guid scanRunId,
        IUserContext user,
        CancellationToken cancellationToken = default);
}

public sealed class RepositoryProfilingCoordinator(
    ISystemRepository systemRepository,
    IRepositoryRepository repositoryRepository,
    IScanRunRepository scanRunRepository,
    IGitWorkingCopyService workingCopyService,
    ICommittedRepositorySnapshotService snapshotService,
    IScanCandidateEnumerator candidateEnumerator,
    ICodeGraphProvider codeGraphProvider,
    Vulgata.Infrastructure.CodeGraph.CodeGraphOptions options,
    ILogger<RepositoryProfilingCoordinator> logger,
    IFrameworkEvidenceDetector frameworkEvidenceDetector,
    FrameworkCatalog frameworkCatalog,
    IPreScanCommunicationDetector preScanCommunicationDetector,
    CommunicationEvidenceRuleCatalog communicationEvidenceRuleCatalog)
    : IRepositoryProfilingCoordinator
{
    private const int UnknownLanguagePathLimit = 10_000;
    private const int UnknownLanguagePathUtf8ByteLimit = 16 * 1024 * 1024;
    private const int FrameworkEvidenceAuditDetailLimit = 100;

    public RepositoryProfilingCoordinator(
        ISystemRepository systemRepository,
        IRepositoryRepository repositoryRepository,
        IScanRunRepository scanRunRepository,
        IGitWorkingCopyService workingCopyService,
        ICommittedRepositorySnapshotService snapshotService,
        IScanCandidateEnumerator candidateEnumerator,
        ICodeGraphProvider codeGraphProvider,
        Vulgata.Infrastructure.CodeGraph.CodeGraphOptions options,
        ILogger<RepositoryProfilingCoordinator> logger,
        IFrameworkEvidenceDetector frameworkEvidenceDetector,
        FrameworkCatalog frameworkCatalog,
        IPreScanCommunicationDetector preScanCommunicationDetector)
        : this(
            systemRepository,
            repositoryRepository,
            scanRunRepository,
            workingCopyService,
            snapshotService,
            candidateEnumerator,
            codeGraphProvider,
            options,
            logger,
            frameworkEvidenceDetector,
            frameworkCatalog,
            preScanCommunicationDetector,
            CommunicationEvidenceRuleCatalog.LoadEmbedded())
    {
    }

    public RepositoryProfilingCoordinator(
        ISystemRepository systemRepository,
        IRepositoryRepository repositoryRepository,
        IScanRunRepository scanRunRepository,
        IGitWorkingCopyService workingCopyService,
        ICommittedRepositorySnapshotService snapshotService,
        IScanCandidateEnumerator candidateEnumerator,
        ICodeGraphProvider codeGraphProvider,
        Vulgata.Infrastructure.CodeGraph.CodeGraphOptions options,
        ILogger<RepositoryProfilingCoordinator> logger,
        IFrameworkEvidenceDetector frameworkEvidenceDetector,
        FrameworkCatalog frameworkCatalog)
        : this(
            systemRepository,
            repositoryRepository,
            scanRunRepository,
            workingCopyService,
            snapshotService,
            candidateEnumerator,
            codeGraphProvider,
            options,
            logger,
            frameworkEvidenceDetector,
            frameworkCatalog,
            EmptyPreScanCommunicationDetector.Instance,
            CommunicationEvidenceRuleCatalog.LoadEmbedded())
    {
    }

    public RepositoryProfilingCoordinator(
        ISystemRepository systemRepository,
        IRepositoryRepository repositoryRepository,
        IScanRunRepository scanRunRepository,
        IGitWorkingCopyService workingCopyService,
        IScanCandidateEnumerator candidateEnumerator,
        ICodeGraphProvider codeGraphProvider,
        Vulgata.Infrastructure.CodeGraph.CodeGraphOptions options,
        ILogger<RepositoryProfilingCoordinator> logger,
        IFrameworkEvidenceDetector frameworkEvidenceDetector,
        FrameworkCatalog frameworkCatalog)
        : this(
            systemRepository,
            repositoryRepository,
            scanRunRepository,
            workingCopyService,
            MissingCommittedRepositorySnapshotService.Instance,
            candidateEnumerator,
            codeGraphProvider,
            options,
            logger,
            frameworkEvidenceDetector,
            frameworkCatalog,
            EmptyPreScanCommunicationDetector.Instance,
            CommunicationEvidenceRuleCatalog.LoadEmbedded())
    {
    }

    public async Task<RepositoryProfilingResult> StartSystemRepositoryAsync(
        Guid systemId,
        Guid repositoryId,
        IUserContext user,
        CancellationToken cancellationToken = default)
    {
        if (await systemRepository.GetVisibleByIdAsync(
                systemId,
                user.ToSystemOwnerFilter(),
                cancellationToken) is null)
        {
            return new(RepositoryProfilingOutcome.RepositoryNotFound);
        }

        RepositoryEntity? repository =
            await repositoryRepository.GetBySystemAndIdAsync(systemId, repositoryId, cancellationToken);
        if (repository is null)
        {
            return new(RepositoryProfilingOutcome.RepositoryNotFound);
        }

        RepositoryProfilingResult result = await ProfileAsync(repository, cancellationToken);
        return await systemRepository.GetVisibleByIdAsync(
            systemId,
            user.ToSystemOwnerFilter(),
            cancellationToken) is null
            ? new(RepositoryProfilingOutcome.RepositoryNotFound)
            : result;
    }

    public async Task<RepositoryProfilingResult> StartStandaloneRepositoryAsync(
        Guid repositoryId,
        IUserContext user,
        CancellationToken cancellationToken = default)
    {
        RepositoryEntity? repository =
            await repositoryRepository.GetStandaloneByIdAsync(repositoryId, cancellationToken);
        return repository is null
            ? new(RepositoryProfilingOutcome.RepositoryNotFound)
            : await ProfileAsync(repository, cancellationToken);
    }

    public async Task<ScanRunSummaryDto?> GetSystemRepositoryRunAsync(
        Guid systemId,
        Guid repositoryId,
        Guid scanRunId,
        IUserContext user,
        CancellationToken cancellationToken = default)
    {
        if (await systemRepository.GetVisibleByIdAsync(
                systemId,
                user.ToSystemOwnerFilter(),
                cancellationToken) is null ||
            await repositoryRepository.GetBySystemAndIdAsync(systemId, repositoryId, cancellationToken) is null)
        {
            return null;
        }

        ScanRun? run = await scanRunRepository.GetAsync(repositoryId, scanRunId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        return await systemRepository.GetVisibleByIdAsync(
            systemId,
            user.ToSystemOwnerFilter(),
            cancellationToken) is null
            ? null
            : Map(run);
    }

    public async Task<ScanRunSummaryDto?> GetStandaloneRepositoryRunAsync(
        Guid repositoryId,
        Guid scanRunId,
        IUserContext user,
        CancellationToken cancellationToken = default)
    {
        if (await repositoryRepository.GetStandaloneByIdAsync(repositoryId, cancellationToken) is null)
        {
            return null;
        }

        ScanRun? run = await scanRunRepository.GetAsync(repositoryId, scanRunId, cancellationToken);
        return run is null ? null : Map(run);
    }

    private async Task<RepositoryProfilingResult> ProfileAsync(
        RepositoryEntity repository,
        CancellationToken cancellationToken)
    {
        var scanRun = ScanRun.Start(
            repository.Id,
            options.WholeRepositoryProcessTimeoutSeconds,
            DateTimeOffset.UtcNow);
        await scanRunRepository.AddAsync(scanRun, cancellationToken);
        await scanRunRepository.SaveChangesAsync(cancellationToken);

        ICommittedRepositorySnapshotLease? snapshotLease = null;
        try
        {
        GitWorkingCopyResult workingCopy =
            await workingCopyService.AcquireAsync(repository, scanRun.Id, cancellationToken);
        if (!workingCopy.Succeeded)
        {
            const string message = "仓库克隆失败，请检查仓库地址和访问权限。";
            logger.LogError("Repository clone failed for {RepositoryId}.", repository.Id);
            scanRun.Fail(message, DateTimeOffset.UtcNow);
            await scanRunRepository.SaveChangesAsync(cancellationToken);
            return new(RepositoryProfilingOutcome.GitFailure, Map(scanRun), message);
        }

        snapshotLease = await snapshotService.AcquireAsync(
            repository,
            scanRun.Id,
            workingCopy,
            cancellationToken);
        await snapshotLease.VerifyIdentityAsync(cancellationToken);
        string snapshotRoot = snapshotLease.RootPath;
        IReadOnlyList<ScanSourceCandidate> candidates =
            await candidateEnumerator.EnumerateAsync(snapshotRoot, cancellationToken);
        HashSet<string> eligiblePaths = candidates
            .Where(candidate => !candidate.IsFiltered)
            .Select(candidate => candidate.RelativePath)
            .ToHashSet(StringComparer.Ordinal);
        if (ExceedsUnknownLanguageAuditBounds(eligiblePaths))
        {
            const string message = "候选源文件审计范围超出限制。";
            logger.LogWarning(
                "Repository {RepositoryId} exceeded the bounded unknown-language audit scale.",
                repository.Id);
            scanRun.Fail(message, DateTimeOffset.UtcNow);
            await scanRunRepository.SaveChangesAsync(cancellationToken);
            return new(
                RepositoryProfilingOutcome.CandidateAuditBoundExceeded,
                Map(scanRun),
                message);
        }

        scanRun.SetCandidateCount(eligiblePaths.Count);
        var filteredOutcomes = new List<ScanCandidateOutcome>();
        foreach (ScanSourceCandidate candidate in candidates.Where(candidate => candidate.IsFiltered))
        {
            var outcome = new ScanCandidateOutcome(
                scanRun.Id,
                candidate.RelativePath,
                ScanCandidateOutcomeKind.Filtered,
                candidate.Category,
                candidate.AuditGroupOrdinal,
                DateTimeOffset.UtcNow);
            filteredOutcomes.Add(outcome);
            scanRun.CandidateOutcomes.Add(outcome);
        }

        scanRunRepository.AddCandidateOutcomes(filteredOutcomes);
        await scanRunRepository.SaveChangesAsync(cancellationToken);

        CodeGraphProviderResult provider;
        try
        {
            provider = await codeGraphProvider.ProfileAsync(
                snapshotRoot,
                TimeSpan.FromSeconds(options.WholeRepositoryProcessTimeoutSeconds),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            const string display = "代码结构分析失败，请稍后重试。";
            logger.LogError(
                "CodeGraph profiling failed unexpectedly for {RepositoryId}.",
                repository.Id);
            scanRun.Fail(display, DateTimeOffset.UtcNow);
            await scanRunRepository.SaveChangesAsync(cancellationToken);
            return new(RepositoryProfilingOutcome.ProviderFailure, Map(scanRun), display);
        }

        if (!provider.Succeeded)
        {
            string error = provider.Error ?? "CodeGraph 执行失败。";
            RepositoryProfilingOutcome outcome = error.Contains(
                "CodeGraph CLI not found",
                StringComparison.Ordinal) ? RepositoryProfilingOutcome.ProviderUnavailable : RepositoryProfilingOutcome.ProviderFailure;
            string display = outcome == RepositoryProfilingOutcome.ProviderUnavailable
                ? "本地 CodeGraph 服务不可用，请联系管理员。"
                : "代码结构分析失败，请稍后重试。";
            logger.LogError("CodeGraph profiling failed for {RepositoryId}: {Reason}", repository.Id, display);
            scanRun.Fail(display, DateTimeOffset.UtcNow);
            await scanRunRepository.SaveChangesAsync(cancellationToken);
            return new(outcome, Map(scanRun), display);
        }

        ProviderCandidateOutcome[] safeProviderOutcomes =
        [
            .. provider.CandidateOutcomes.Select(outcome => outcome with
            {
                RelativePath = SafeProviderPath.Revalidate(outcome.RelativePath),
            }),
        ];
        HashSet<string> unknownLanguagePaths = safeProviderOutcomes
            .Where(outcome =>
                outcome.Outcome == ScanCandidateOutcomeKind.Filtered &&
                StringComparer.Ordinal.Equals(
                    outcome.Reason,
                    ScanCandidateOutcomeReasons.UnknownLanguage) &&
                eligiblePaths.Contains(outcome.RelativePath.Value))
            .Select(outcome => outcome.RelativePath.Value)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> unknownEndpointIdentities = provider.CodeUnits
            .Where(unit => unknownLanguagePaths.Contains(unit.RelativePath))
            .Select(unit => unit.EndpointIdentity)
            .ToHashSet(StringComparer.Ordinal);
        ProviderCodeUnit[] retainedProviderUnits =
        [
            .. provider.CodeUnits.Where(
                unit => !unknownLanguagePaths.Contains(unit.RelativePath)),
        ];
        ProviderStructuralRelationship[] retainedProviderRelationships =
        [
            .. provider.Relationships.Where(relationship =>
                !unknownEndpointIdentities.Contains(relationship.SourceEndpointIdentity) &&
                !unknownEndpointIdentities.Contains(relationship.TargetEndpointIdentity)),
        ];

        IReadOnlyDictionary<string, string> languagesByPath;
        try
        {
            languagesByPath =
                ReconcileProviderLanguages(retainedProviderUnits, eligiblePaths);
        }
        catch (InvalidOperationException)
        {
            const string display = "代码结构分析失败，请稍后重试。";
            logger.LogError(
                "CodeGraph returned an invalid language profile for {RepositoryId}.",
                repository.Id);
            scanRun.Fail(display, DateTimeOffset.UtcNow);
            await scanRunRepository.SaveChangesAsync(cancellationToken);
            return new(RepositoryProfilingOutcome.ProviderFailure, Map(scanRun), display);
        }

        FrameworkEvidenceDetectionResult frameworkDetection;
        try
        {
            frameworkDetection = await frameworkEvidenceDetector.DetectDetailedAsync(
                snapshotRoot,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            const string display = "框架证据检测失败。";
            logger.LogError(
                "Framework evidence detection failed unexpectedly for {RepositoryId}.",
                repository.Id);
            scanRun.Fail(display, DateTimeOffset.UtcNow);
            await scanRunRepository.SaveChangesAsync(cancellationToken);
            return new(RepositoryProfilingOutcome.ProviderFailure, Map(scanRun), display);
        }

        if (!frameworkDetection.Succeeded)
        {
            const string display = "框架证据检测失败。";
            logger.LogError(
                "Framework evidence detection failed for {RepositoryId}: {Reason}",
                repository.Id,
                frameworkDetection.Failure);
            scanRun.Fail(display, DateTimeOffset.UtcNow);
            await scanRunRepository.SaveChangesAsync(cancellationToken);
            return new(RepositoryProfilingOutcome.ProviderFailure, Map(scanRun), display);
        }

        var frameworkAuditOutcomes = frameworkDetection.AuditOutcomes.ToList();
        var frameworkAuditIdentities =
            frameworkDetection.AuditOutcomeIdentities.ToHashSet();
        var retainedFrameworkAuditIdentities = frameworkDetection.AuditOutcomes
            .Select(audit => new FrameworkEvidenceAuditIdentity(
                audit.ArtifactPath,
                audit.Outcome,
                audit.Reason))
            .ToHashSet();
        int frameworkAuditTotalOutcomeCount =
            frameworkDetection.TotalAuditOutcomeCount;
        var applicableFrameworksByPath =
            new Dictionary<string, FrameworkEvidenceDetectionResult>(StringComparer.Ordinal);
        foreach ((string path, string language) in languagesByPath.OrderBy(
                     pair => pair.Key,
                     StringComparer.Ordinal))
        {
            FrameworkEvidenceDetectionResult applicable =
                frameworkEvidenceDetector.SelectApplicable(
                    frameworkDetection,
                    path,
                    language);
            if (!applicable.Succeeded)
            {
                const string display = "框架证据检测失败。";
                logger.LogError(
                    "Framework evidence selection failed for {RepositoryId}: {Reason}",
                    repository.Id,
                    applicable.Failure);
                scanRun.Fail(display, DateTimeOffset.UtcNow);
                await scanRunRepository.SaveChangesAsync(cancellationToken);
                return new(RepositoryProfilingOutcome.ProviderFailure, Map(scanRun), display);
            }

            applicableFrameworksByPath.Add(path, applicable);
            foreach (FrameworkEvidenceAuditIdentity identity in
                     applicable.AuditOutcomeIdentities)
            {
                if (frameworkAuditIdentities.Add(identity))
                {
                    frameworkAuditTotalOutcomeCount++;
                }
            }

            foreach (FrameworkEvidenceAuditOutcome audit in applicable.AuditOutcomes)
            {
                var identity = new FrameworkEvidenceAuditIdentity(
                    audit.ArtifactPath,
                    audit.Outcome,
                    audit.Reason);
                if (!retainedFrameworkAuditIdentities.Add(identity))
                {
                    continue;
                }

                if (frameworkAuditOutcomes.Count < FrameworkEvidenceAuditDetailLimit)
                {
                    frameworkAuditOutcomes.Add(audit);
                }
            }
        }

        scanRun.SetFrameworkEvidenceAuditTotalOutcomeCount(
            frameworkAuditTotalOutcomeCount);
        var endpointUnits = new Dictionary<string, CodeUnit>(StringComparer.Ordinal);
        var structuralUnits = new Dictionary<(string Path, int Start, int End, string Symbol, CodeUnitType Type), CodeUnit>();
        var hashes = new Dictionary<string, (string Prefix, string Full)>(StringComparer.Ordinal);
        var providerAuditOutcomes = new List<ScanCandidateOutcome>();
        var auditKeys = new HashSet<(string Path, ScanCandidateOutcomeKind Outcome, string Reason)>();
        foreach (FrameworkEvidenceAuditOutcome audit in frameworkAuditOutcomes)
        {
            string auditPath = NormalizeAuditPath(audit.ArtifactPath);
            if (!auditKeys.Add((auditPath, audit.Outcome, audit.Reason)))
            {
                continue;
            }

            var candidateOutcome = new ScanCandidateOutcome(
                scanRun.Id,
                auditPath,
                audit.Outcome,
                audit.Reason,
                0,
                DateTimeOffset.UtcNow);
            providerAuditOutcomes.Add(candidateOutcome);
            scanRun.CandidateOutcomes.Add(candidateOutcome);
        }

        foreach (ProviderCodeUnit providerUnit in retainedProviderUnits)
        {
            if (!eligiblePaths.Contains(providerUnit.RelativePath) ||
                endpointUnits.ContainsKey(providerUnit.EndpointIdentity))
            {
                continue;
            }

            try
            {
                var structuralKey = (
                    providerUnit.RelativePath,
                    providerUnit.LineStart,
                    providerUnit.LineEnd,
                    providerUnit.SymbolName,
                    providerUnit.UnitType);
                if (structuralUnits.TryGetValue(structuralKey, out CodeUnit? existingUnit))
                {
                    endpointUnits.Add(providerUnit.EndpointIdentity, existingUnit);
                    continue;
                }

                (string prefix, string full) = await GetHashesAsync(
                    snapshotRoot,
                    providerUnit.RelativePath,
                    hashes,
                    cancellationToken);
                var unit = new CodeUnit(
                    scanRun.Id,
                    repository.Id,
                    providerUnit.RelativePath,
                    prefix,
                    full,
                    providerUnit.LineStart,
                    providerUnit.LineEnd,
                    providerUnit.SymbolName,
                    providerUnit.UnitType,
                    providerUnit.RangeConfidence,
                    languagesByPath[providerUnit.RelativePath]);
                if (endpointUnits.TryAdd(providerUnit.EndpointIdentity, unit))
                {
                    structuralUnits.Add(structuralKey, unit);
                    scanRun.CodeUnits.Add(unit);
                    foreach (PackageEvidence evidence in
                             applicableFrameworksByPath[providerUnit.RelativePath].Evidence)
                    {
                        FrameworkCatalogEntry catalogEntry =
                            frameworkCatalog.FindExact(evidence.PackageCoordinate)
                            ?? throw new InvalidOperationException(
                                "Framework evidence referenced an unknown catalog coordinate.");
                        var framework = new CodeUnitFramework(
                            unit.Id,
                            scanRun.Id,
                            repository.Id,
                            catalogEntry.FrameworkName,
                            evidence.Relationship == PackageEvidenceRelationship.Direct
                                ? FrameworkConfidence.High
                                : FrameworkConfidence.Medium,
                            evidence.DetectionSource,
                            DateTimeOffset.UtcNow);
                        unit.Frameworks.Add(framework);
                        scanRun.CodeUnitFrameworks.Add(framework);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                logger.LogWarning(
                    "Rejected CodeGraph unit for {FilePath}: {Reason}",
                    providerUnit.RelativePath,
                    exception.GetType().Name);
                if (auditKeys.Add((providerUnit.RelativePath, ScanCandidateOutcomeKind.Errored, "InvalidProviderUnit")))
                {
                    var outcome = new ScanCandidateOutcome(
                        scanRun.Id,
                        providerUnit.RelativePath,
                        ScanCandidateOutcomeKind.Errored,
                        "InvalidProviderUnit",
                        0,
                        DateTimeOffset.UtcNow);
                    providerAuditOutcomes.Add(outcome);
                    scanRun.CandidateOutcomes.Add(outcome);
                }
            }
        }

        foreach (ProviderCandidateOutcome outcome in safeProviderOutcomes)
        {
            SafeProviderPath auditPath = SafeProviderPath.Revalidate(outcome.RelativePath);
            logger.LogWarning(
                "Rejected CodeGraph record for {FilePath}: {Reason}",
                auditPath.Value,
                outcome.Reason);
            if (auditKeys.Add((auditPath.Value, outcome.Outcome, outcome.Reason)))
            {
                var candidateOutcome = new ScanCandidateOutcome(
                    scanRun.Id,
                    auditPath.Value,
                    outcome.Outcome,
                    outcome.Reason,
                    0,
                    DateTimeOffset.UtcNow);
                providerAuditOutcomes.Add(candidateOutcome);
                scanRun.CandidateOutcomes.Add(candidateOutcome);
            }
        }

        var reconciledEligiblePaths = providerAuditOutcomes
            .Select(outcome => outcome.FilePath)
            .Where(eligiblePaths.Contains)
            .ToHashSet(StringComparer.Ordinal);
        var parsedEligiblePaths = scanRun.CodeUnits
            .Select(unit => unit.FilePath)
            .ToHashSet(StringComparer.Ordinal);
        foreach (ScanSourceCandidate candidate in candidates.Where(candidate => !candidate.IsFiltered))
        {
            if (!reconciledEligiblePaths.Add(candidate.RelativePath))
            {
                continue;
            }

            bool parsed = parsedEligiblePaths.Contains(candidate.RelativePath);
            var reconciled = new ScanCandidateOutcome(
                scanRun.Id,
                candidate.RelativePath,
                parsed ? ScanCandidateOutcomeKind.Parsed : ScanCandidateOutcomeKind.Filtered,
                parsed ? "CodeUnitExtracted" : ScanCandidateOutcomeReasons.UnknownLanguage,
                candidate.AuditGroupOrdinal,
                DateTimeOffset.UtcNow);
            providerAuditOutcomes.Add(reconciled);
            scanRun.CandidateOutcomes.Add(reconciled);
        }

        var resolvedKeys = new HashSet<(Guid Source, Guid Target, StructuralRelationshipKind Kind)>();
        var unresolvedKeys = new HashSet<(Guid Source, StructuralRelationshipKind Kind, string Target)>();
        foreach (ProviderStructuralRelationship relationship in retainedProviderRelationships)
        {
            if (!endpointUnits.TryGetValue(relationship.SourceEndpointIdentity, out CodeUnit? source))
            {
                continue;
            }

            if (endpointUnits.TryGetValue(relationship.TargetEndpointIdentity, out CodeUnit? target))
            {
                if (resolvedKeys.Add((source.Id, target.Id, relationship.RelationshipKind)))
                {
                    scanRun.CodeUnitEdges.Add(new CodeUnitEdge(
                        scanRun.Id,
                        repository.Id,
                        source.Id,
                        target.Id,
                        relationship.RelationshipKind));
                }
            }
            else if (!string.IsNullOrWhiteSpace(relationship.TargetEndpointIdentity) &&
                unresolvedKeys.Add((source.Id, relationship.RelationshipKind, relationship.TargetEndpointIdentity)))
            {
                scanRun.UnresolvedRelationships.Add(new UnresolvedStructuralRelationshipCandidate(
                    scanRun.Id,
                    repository.Id,
                    source.Id,
                    relationship.RelationshipKind,
                    relationship.TargetEndpointIdentity,
                    DateTimeOffset.UtcNow));
            }
        }

        int parsedCount = parsedEligiblePaths.Count;
        scanRun.Complete(parsedCount, scanRun.CodeUnits.Count, DateTimeOffset.UtcNow);
        if (scanRun.NeedsHumanReview)
        {
            logger.LogWarning(
                "ScanRun {ScanRunId} produced zero Code Units from {CandidateCount} candidates and requires human review.",
                scanRun.Id,
                scanRun.TotalCandidateFileCount);
        }

        ScanRunLanguageSummary[] languageSummaries = scanRun.CodeUnits
            .Where(unit => unit.Language is not null)
            .GroupBy(unit => unit.Language!, StringComparer.Ordinal)
            .Select(group => new ScanRunLanguageSummary(
                scanRun.Id,
                repository.Id,
                group.Key,
                group.Select(unit => unit.FilePath).Distinct(StringComparer.Ordinal).Count()))
            .ToArray();
        foreach (ScanRunLanguageSummary languageSummary in languageSummaries)
        {
            scanRun.LanguageSummaries.Add(languageSummary);
        }

        RepositoryProfileDraft profile = RepositoryProfileDraft.Create(
            scanRun,
            scanRun.CodeUnits,
            scanRun.CodeUnitEdges,
            scanRun.UnresolvedRelationships,
            scanRun.CodeUnitFrameworks,
            languageSummaries,
            providerAuditOutcomes);
        PreScanResultDraft preScanResult = await preScanCommunicationDetector.DetectAsync(
            profile,
            snapshotLease.ProfiledRevision,
            snapshotLease,
            cancellationToken);
        scanRun.RecordApprovedPreScanOutcome(preScanResult.ApprovalMessage);
        await snapshotLease.VerifyIdentityAsync(cancellationToken);
        RepositoryProfileCommitDraft commit = RepositoryProfileCommitDraft.Create(
            profile,
            preScanResult);
        await scanRunRepository.PersistCompletedProfileAsync(commit, cancellationToken);
        logger.LogInformation(
            "Pre-scan approval persisted for Repository {RepositoryId} ScanRun {ScanRunId} with state {PreScanState}, schema {SchemaVersion}, confirmed evidence count {TotalConfirmedEvidenceCount}, hint count {TotalHintCount}, warning count {TotalWarningCount}, evidence truncated {EvidenceTruncated}, and communication rule-set version {CommunicationRuleSetVersion}.",
            repository.Id,
            scanRun.Id,
            preScanResult.State,
            preScanResult.SchemaVersion,
            preScanResult.TotalConfirmedEvidenceCount,
            preScanResult.TotalHintCount,
            preScanResult.TotalWarningCount,
            preScanResult.EvidenceTruncated,
            communicationEvidenceRuleCatalog.Version);

            return new(RepositoryProfilingOutcome.Success, Map(scanRun));
        }
        catch (OperationCanceledException)
        {
            await scanRunRepository.RecoverAbortedFinalCommitAsync(
                repository.Id,
                scanRun.Id,
                "代码结构分析已取消。",
                DateTimeOffset.UtcNow,
                CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to complete Repository profiling for ScanRun {ScanRunId}.",
                scanRun.Id);
            await scanRunRepository.RecoverFailedFinalCommitAsync(
                repository.Id,
                scanRun.Id,
                "代码结构分析失败，请稍后重试。",
                DateTimeOffset.UtcNow,
                CancellationToken.None);
            throw;
        }
        finally
        {
            if (snapshotLease is not null)
            {
                await snapshotLease.DisposeAsync();
            }
        }
    }

    private static string NormalizeAuditPath(string path)
        => SafeProviderPath.FromProviderScalar(path).Value;

    private static bool ExceedsUnknownLanguageAuditBounds(
        IReadOnlyCollection<string> eligiblePaths)
    {
        if (eligiblePaths.Count > UnknownLanguagePathLimit)
        {
            return true;
        }

        long combinedUtf8ByteCount = 0;
        foreach (string path in eligiblePaths)
        {
            combinedUtf8ByteCount += Encoding.UTF8.GetByteCount(path);
            if (combinedUtf8ByteCount > UnknownLanguagePathUtf8ByteLimit)
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyDictionary<string, string> ReconcileProviderLanguages(
        IEnumerable<ProviderCodeUnit> providerUnits,
        IReadOnlySet<string> eligiblePaths)
    {
        var languagesByPath = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (ProviderCodeUnit unit in providerUnits.Where(
                     unit => eligiblePaths.Contains(unit.RelativePath)))
        {
            string language;
            try
            {
                language = CodeUnit.NormalizeLanguage(unit.Language!);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException(
                    "CodeGraph returned an incomplete or invalid language profile.",
                    exception);
            }

            if (languagesByPath.TryGetValue(unit.RelativePath, out string? existing) &&
                !StringComparer.Ordinal.Equals(existing, language))
            {
                throw new InvalidOperationException(
                    "CodeGraph returned conflicting languages for one Repository path.");
            }

            languagesByPath[unit.RelativePath] = language;
        }

        return languagesByPath;
    }

    private static async Task<(string Prefix, string Full)> GetHashesAsync(
        string workingCopyPath,
        string relativePath,
        IDictionary<string, (string Prefix, string Full)> cache,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(relativePath, out (string Prefix, string Full) value))
        {
            return value;
        }

        string root = Path.GetFullPath(workingCopyPath);
        string fullPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("文件路径越过仓库边界。", nameof(relativePath));
        }

        EnsureNoReparsePoints(root, fullPath);
        byte[] bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
        value = (
            Convert.ToHexStringLower(SHA256.HashData(bytes.AsSpan(0, Math.Min(4_096, bytes.Length)))),
            Convert.ToHexStringLower(SHA256.HashData(bytes)));
        cache[relativePath] = value;
        return value;
    }

    private static void EnsureNoReparsePoints(string root, string fullPath)
    {
        string? current = fullPath;
        while (current is not null && !current.Equals(root, StringComparison.Ordinal))
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("拒绝读取符号链接或重解析点。");
            }

            current = Path.GetDirectoryName(current);
        }
    }

    private static ScanRunSummaryDto Map(ScanRun run)
    {
        const int sharedEvidenceDetailLimit = 100;
        string[] unknownLanguagePaths =
        [
            .. run.CandidateOutcomes
                .Where(outcome =>
                    outcome.Outcome == ScanCandidateOutcomeKind.Filtered &&
                    StringComparer.Ordinal.Equals(
                        outcome.ReasonOrCategory,
                        ScanCandidateOutcomeReasons.UnknownLanguage))
                .Select(outcome => outcome.FilePath)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal),
        ];
        CodeUnitFramework[] sortedFacts =
        [
            .. run.CodeUnitFrameworks
                .OrderBy(fact => fact.FrameworkName, StringComparer.Ordinal)
                .ThenBy(fact => fact.CodeUnitId)
                .ThenBy(fact => fact.Confidence == FrameworkConfidence.High ? 0 : 1)
                .ThenBy(fact => fact.DetectionSource, StringComparer.Ordinal),
        ];
        CodeUnitFramework[][] frameworkFactGroups =
        [
            .. sortedFacts
                .GroupBy(fact => fact.FrameworkName, StringComparer.Ordinal)
                .Select(group => group.ToArray()),
        ];
        var remainingDetails = sharedEvidenceDetailLimit;
        var sampledFactIds = new HashSet<Guid>();
        var sampledFactsByFramework =
            new Dictionary<string, List<CodeUnitFramework>>(StringComparer.Ordinal);
        foreach (CodeUnitFramework[] facts in frameworkFactGroups)
        {
            CodeUnitFramework anchor = facts
                .OrderBy(fact => fact.Confidence == FrameworkConfidence.High ? 0 : 1)
                .ThenBy(fact => fact.CodeUnitId)
                .ThenBy(fact => fact.DetectionSource, StringComparer.Ordinal)
                .First();
            sampledFactsByFramework.Add(anchor.FrameworkName, [anchor]);
            sampledFactIds.Add(anchor.Id);
            remainingDetails--;
        }

        FrameworkEvidenceAuditDetailDto[] auditDetails =
        [
            .. run.CandidateOutcomes
                .Where(outcome => outcome.ReasonOrCategory.StartsWith(
                    "FrameworkEvidence",
                    StringComparison.Ordinal))
                .OrderBy(outcome => outcome.FilePath, StringComparer.Ordinal)
                .ThenBy(outcome => outcome.Outcome)
                .ThenBy(outcome => outcome.ReasonOrCategory, StringComparer.Ordinal)
                .Take(remainingDetails)
                .Select(outcome => new FrameworkEvidenceAuditDetailDto
                {
                    Path = outcome.FilePath,
                    Outcome = MapFrameworkAuditOutcome(outcome.Outcome),
                    Reason = MapFrameworkAuditReason(outcome.ReasonOrCategory),
                }),
        ];
        remainingDetails -= auditDetails.Length;

        foreach (CodeUnitFramework[] facts in frameworkFactGroups)
        {
            List<CodeUnitFramework> sampledFacts =
                sampledFactsByFramework[facts[0].FrameworkName];
            foreach (CodeUnitFramework fact in facts)
            {
                if (remainingDetails == 0)
                {
                    break;
                }

                if (sampledFactIds.Add(fact.Id))
                {
                    sampledFacts.Add(fact);
                    remainingDetails--;
                }
            }
        }

        ScanRunFrameworkSummaryDto[] frameworks =
        [
            .. frameworkFactGroups.Select(facts =>
            {
                FrameworkEvidenceSampleDto[] samples =
                [
                    .. sampledFactsByFramework[facts[0].FrameworkName]
                        .OrderBy(fact => fact.CodeUnitId)
                        .ThenBy(fact => fact.Confidence == FrameworkConfidence.High ? 0 : 1)
                        .ThenBy(fact => fact.DetectionSource, StringComparer.Ordinal)
                        .Select(fact => new FrameworkEvidenceSampleDto
                        {
                            CodeUnitId = fact.CodeUnitId,
                            Confidence = fact.Confidence.ToString(),
                            DetectionSource = fact.DetectionSource,
                        }),
                ];
                return new ScanRunFrameworkSummaryDto
                {
                    FrameworkName = facts[0].FrameworkName,
                    CodeUnitCount = facts.Select(fact => fact.CodeUnitId).Distinct().Count(),
                    StrongestConfidence = facts.Any(
                        fact => fact.Confidence == FrameworkConfidence.High)
                        ? FrameworkConfidence.High.ToString()
                        : FrameworkConfidence.Medium.ToString(),
                    TotalFactCount = facts.Length,
                    EvidenceSamples = samples,
                };
            }),
        ];

        return new ScanRunSummaryDto
        {
            Id = run.Id,
            RepositoryId = run.RepositoryId,
            Status = run.Status switch
            {
                ScanRunStatus.Profiling => "分析中",
                ScanRunStatus.Completed => "已完成",
                ScanRunStatus.Failed => "失败",
                ScanRunStatus.Aborted => "已中止",
                _ => "未知",
            },
            ParsedFileCount = run.ParsedFileCount,
            TotalCandidateFileCount = run.TotalCandidateFileCount,
            NeedsHumanReview = run.NeedsHumanReview,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            Message = MapTerminalMessage(run),
            Languages = run.LanguageSummaries
                .OrderBy(summary => summary.Language, StringComparer.Ordinal)
                .Select(summary => new ScanRunLanguageSummaryDto
                {
                    Language = summary.Language,
                    DistinctFileCount = summary.DistinctFileCount,
                })
                .ToArray(),
            Frameworks = frameworks,
            UnknownLanguage = new UnknownLanguageSummaryDto
            {
                Paths = unknownLanguagePaths,
            },
            FrameworkEvidenceAudit = new FrameworkEvidenceAuditSummaryDto
            {
                SampledOutcomeCount = auditDetails.Length,
                TotalOutcomeCount = run.FrameworkEvidenceAuditTotalOutcomeCount,
                SamplingApplied =
                    run.FrameworkEvidenceAuditTotalOutcomeCount > auditDetails.Length,
                Details = auditDetails,
            },
        };
    }

    private static string? MapTerminalMessage(ScanRun run)
    {
        if (run.Status == ScanRunStatus.Aborted)
        {
            return "代码结构分析已取消。";
        }

        if (run.Status != ScanRunStatus.Failed)
        {
            return null;
        }

        return run.TerminalMessage switch
        {
            "仓库克隆失败，请检查仓库地址和访问权限。" => run.TerminalMessage,
            "候选源文件审计范围超出限制。" => run.TerminalMessage,
            "本地 CodeGraph 服务不可用，请联系管理员。" => run.TerminalMessage,
            "框架证据检测失败。" => run.TerminalMessage,
            "代码结构分析失败，请稍后重试。" => run.TerminalMessage,
            _ => "代码结构分析失败，请稍后重试。",
        };
    }

    private static string MapFrameworkAuditOutcome(ScanCandidateOutcomeKind outcome) =>
        outcome switch
        {
            ScanCandidateOutcomeKind.Skipped => "已跳过",
            ScanCandidateOutcomeKind.Errored => "处理失败",
            _ => "未处理",
        };

    private static string MapFrameworkAuditReason(string reason) =>
        reason switch
        {
            ScanCandidateOutcomeReasons.FrameworkEvidenceMalformed => "框架证据格式无效。",
            ScanCandidateOutcomeReasons.FrameworkEvidenceUnsafePath => "框架证据路径不安全。",
            ScanCandidateOutcomeReasons.FrameworkEvidenceReparsePoint => "框架证据包含重解析点。",
            ScanCandidateOutcomeReasons.FrameworkEvidenceUnsupported => "框架证据格式不受支持。",
            ScanCandidateOutcomeReasons.FrameworkEvidenceAmbiguousScope => "框架证据作用域不明确。",
            ScanCandidateOutcomeReasons.FrameworkEvidenceBoundExceeded => "框架证据超出安全限制。",
            _ => "框架证据处理失败。",
        };

    private sealed class MissingCommittedRepositorySnapshotService
        : ICommittedRepositorySnapshotService
    {
        private MissingCommittedRepositorySnapshotService()
        {
        }

        public static MissingCommittedRepositorySnapshotService Instance { get; } =
            new();

        public Task<ICommittedRepositorySnapshotLease> AcquireAsync(
            RepositoryEntity repository,
            Guid scanRunId,
            GitWorkingCopyResult workingCopy,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "A committed Repository snapshot service is required for profiling.");
    }

    private sealed class EmptyPreScanCommunicationDetector
        : IPreScanCommunicationDetector
    {
        private EmptyPreScanCommunicationDetector()
        {
        }

        public static EmptyPreScanCommunicationDetector Instance { get; } = new();

        public Task<PreScanResultDraft> DetectAsync(
            RepositoryProfileDraft profile,
            string profiledRevision,
            ICommittedRepositorySnapshotLease committedSnapshot,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] fingerprint = SHA256.HashData(
                Encoding.UTF8.GetBytes($"pre-scan-empty-v1\0{profiledRevision}"));
            PreScanResult result = PreScanResult.CreateApproved(
                profile.ScanRun.RepositoryId,
                profile.ScanRun.Id,
                profiledRevision,
                DateTimeOffset.UtcNow,
                Convert.ToHexStringLower(fingerprint),
                "预扫描已批准——继续进入顺序演示运行器",
                ConfirmedCommunicationKinds.None,
                0,
                0,
                0,
                false,
                []);
            return Task.FromResult(PreScanResultDraft.Create(result));
        }
    }
}
