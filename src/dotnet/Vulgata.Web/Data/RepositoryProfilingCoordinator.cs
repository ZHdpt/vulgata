using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
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
    IScanCandidateEnumerator candidateEnumerator,
    ICodeGraphProvider codeGraphProvider,
    Vulgata.Infrastructure.CodeGraph.CodeGraphOptions options,
    ILogger<RepositoryProfilingCoordinator> logger)
    : IRepositoryProfilingCoordinator
{
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
        return run is null ? null : Map(run);
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

        try
        {
        GitWorkingCopyResult workingCopy =
            await workingCopyService.AcquireAsync(repository, scanRun.Id, cancellationToken);
        if (!workingCopy.Succeeded)
        {
            string message = $"仓库克隆失败：{workingCopy.Error}";
            logger.LogError("Repository clone failed for {RepositoryId}: {Reason}", repository.Id, workingCopy.Error);
            scanRun.Fail(message, DateTimeOffset.UtcNow);
            await scanRunRepository.SaveChangesAsync(cancellationToken);
            return new(RepositoryProfilingOutcome.GitFailure, Map(scanRun), message);
        }

        IReadOnlyList<ScanSourceCandidate> candidates =
            await candidateEnumerator.EnumerateAsync(workingCopy.WorkingCopyPath!, cancellationToken);
        HashSet<string> eligiblePaths = candidates
            .Where(candidate => !candidate.IsFiltered)
            .Select(candidate => candidate.RelativePath)
            .ToHashSet(StringComparer.Ordinal);
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

        CodeGraphProviderResult provider = await codeGraphProvider.ProfileAsync(
            workingCopy.WorkingCopyPath!,
            TimeSpan.FromSeconds(options.WholeRepositoryProcessTimeoutSeconds),
            cancellationToken);
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

        IReadOnlyDictionary<string, string> languagesByPath =
            ReconcileProviderLanguages(provider.CodeUnits, eligiblePaths);
        var endpointUnits = new Dictionary<string, CodeUnit>(StringComparer.Ordinal);
        var structuralUnits = new Dictionary<(string Path, int Start, int End, string Symbol, CodeUnitType Type), CodeUnit>();
        var hashes = new Dictionary<string, (string Prefix, string Full)>(StringComparer.Ordinal);
        var providerAuditOutcomes = new List<ScanCandidateOutcome>();
        var auditKeys = new HashSet<(string Path, ScanCandidateOutcomeKind Outcome, string Reason)>();
        foreach (ProviderCodeUnit providerUnit in provider.CodeUnits)
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
                    workingCopy.WorkingCopyPath!,
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

        foreach (ProviderCandidateOutcome outcome in provider.CandidateOutcomes)
        {
            logger.LogWarning("Rejected CodeGraph record for {FilePath}: {Reason}", outcome.RelativePath, outcome.Reason);
            string auditPath = NormalizeAuditPath(outcome.RelativePath);
            if (auditKeys.Add((auditPath, outcome.Outcome, outcome.Reason)))
            {
                var candidateOutcome = new ScanCandidateOutcome(
                    scanRun.Id,
                    auditPath,
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
                parsed ? ScanCandidateOutcomeKind.Parsed : ScanCandidateOutcomeKind.Skipped,
                parsed ? "CodeUnitExtracted" : "NoCodeUnitExtracted",
                candidate.AuditGroupOrdinal,
                DateTimeOffset.UtcNow);
            providerAuditOutcomes.Add(reconciled);
            scanRun.CandidateOutcomes.Add(reconciled);
        }

        var resolvedKeys = new HashSet<(Guid Source, Guid Target, StructuralRelationshipKind Kind)>();
        var unresolvedKeys = new HashSet<(Guid Source, StructuralRelationshipKind Kind, string Target)>();
        foreach (ProviderStructuralRelationship relationship in provider.Relationships)
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

        int parsedCount = Math.Min(provider.ParsedFileCount, eligiblePaths.Count);
        scanRun.Complete(parsedCount, scanRun.CodeUnits.Count, DateTimeOffset.UtcNow);
        if (scanRun.NeedsHumanReview)
        {
            logger.LogWarning(
                "ScanRun {ScanRunId} produced zero Code Units from {CandidateCount} candidates and requires human review.",
                scanRun.Id,
                scanRun.TotalCandidateFileCount);
        }

        try
        {
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

            RepositoryProfileDraft draft = RepositoryProfileDraft.Create(
                scanRun,
                scanRun.CodeUnits,
                scanRun.CodeUnitEdges,
                scanRun.UnresolvedRelationships,
                Array.Empty<CodeUnitFramework>(),
                languageSummaries,
                providerAuditOutcomes);
            await scanRunRepository.PersistCompletedProfileAsync(draft, cancellationToken);
        }
        catch (Exception exception) when (exception is DbUpdateException or InvalidOperationException)
        {
            logger.LogError(exception, "Failed to atomically persist CodeGraph result for ScanRun {ScanRunId}.", scanRun.Id);
            await scanRunRepository.RecoverFailedFinalCommitAsync(
                repository.Id,
                scanRun.Id,
                "代码结构分析失败，请稍后重试。",
                DateTimeOffset.UtcNow,
                CancellationToken.None);
            throw;
        }

            return new(RepositoryProfilingOutcome.Success, Map(scanRun));
        }
        catch (OperationCanceledException)
        {
            if (scanRun.Status == ScanRunStatus.Profiling)
            {
                scanRun.Abort("代码结构分析已取消。", DateTimeOffset.UtcNow);
                await scanRunRepository.SaveChangesAsync(CancellationToken.None);
            }

            throw;
        }
        catch (Exception)
        {
            if (scanRun.Status == ScanRunStatus.Profiling)
            {
                scanRun.Fail("代码结构分析失败，请稍后重试。", DateTimeOffset.UtcNow);
                await scanRunRepository.SaveChangesAsync(CancellationToken.None);
            }

            throw;
        }
    }

    private static string NormalizeAuditPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "(unknown)";
        }

        string normalized = path.Trim().Replace('\\', '/');
        if (normalized.Length > 2_048 ||
            Path.IsPathRooted(normalized) ||
            normalized.Split('/').Any(segment => segment == ".."))
        {
            return "(unknown)";
        }

        return normalized.TrimStart('/');
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

    private static ScanRunSummaryDto Map(ScanRun run) => new()
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
        Message = run.TerminalMessage,
        Languages = run.LanguageSummaries
            .OrderBy(summary => summary.Language, StringComparer.Ordinal)
            .Select(summary => new ScanRunLanguageSummaryDto
            {
                Language = summary.Language,
                DistinctFileCount = summary.DistinctFileCount,
            })
            .ToArray(),
    };
}
