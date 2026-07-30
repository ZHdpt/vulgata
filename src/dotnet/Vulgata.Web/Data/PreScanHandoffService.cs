using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Shared.Repositories;
using RepositoryEntity = Vulgata.Core.Entities.Repository;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Web.Data;

public interface IPreScanHandoffService
{
    Task<ApprovedPreScanHandoffDto?> GetSystemRepositoryAsync(
        Guid systemId,
        Guid repositoryId,
        Guid scanRunId,
        string? requestedProfiledRevision,
        IUserContext user,
        CancellationToken cancellationToken = default);

    Task<ApprovedPreScanHandoffDto?> GetStandaloneRepositoryAsync(
        Guid repositoryId,
        Guid scanRunId,
        string? requestedProfiledRevision,
        IUserContext user,
        CancellationToken cancellationToken = default);
}

public sealed class PreScanHandoffService(
    ISystemRepository systemRepository,
    IRepositoryRepository repositoryRepository,
    IScanRunRepository scanRunRepository)
    : IPreScanHandoffService
{
    public async Task<ApprovedPreScanHandoffDto?> GetSystemRepositoryAsync(
        Guid systemId,
        Guid repositoryId,
        Guid scanRunId,
        string? requestedProfiledRevision,
        IUserContext user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        SystemOwnerFilter filter = user.ToSystemOwnerFilter();
        if (!IsExactVisibleSystem(
                await systemRepository.GetVisibleByIdAsync(
                    systemId,
                    filter,
                    cancellationToken),
                systemId) ||
            !IsExactSystemRepository(
                await repositoryRepository.GetBySystemAndIdAsync(
                    systemId,
                    repositoryId,
                    cancellationToken),
                systemId,
                repositoryId))
        {
            return null;
        }

        ApprovedPreScanHandoff? handoff = await MaterializeAsync(
            repositoryId,
            scanRunId,
            cancellationToken);
        if (handoff is null)
        {
            return null;
        }

        if (!IsExactVisibleSystem(
                await systemRepository.GetVisibleByIdAsync(
                    systemId,
                    filter,
                    cancellationToken),
                systemId) ||
            !IsExactSystemRepository(
                await repositoryRepository.GetBySystemAndIdAsync(
                    systemId,
                    repositoryId,
                    cancellationToken),
                systemId,
                repositoryId))
        {
            return null;
        }

        return IsRequestedRevision(handoff, requestedProfiledRevision) &&
            IsExactHandoff(handoff, repositoryId, scanRunId)
            ? Map(handoff)
            : null;
    }

    public async Task<ApprovedPreScanHandoffDto?> GetStandaloneRepositoryAsync(
        Guid repositoryId,
        Guid scanRunId,
        string? requestedProfiledRevision,
        IUserContext user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!IsExactStandaloneRepository(
                await repositoryRepository.GetStandaloneByIdAsync(
                    repositoryId,
                    cancellationToken),
                repositoryId))
        {
            return null;
        }

        ApprovedPreScanHandoff? handoff = await MaterializeAsync(
            repositoryId,
            scanRunId,
            cancellationToken);
        if (handoff is null)
        {
            return null;
        }

        if (!IsExactStandaloneRepository(
                await repositoryRepository.GetStandaloneByIdAsync(
                    repositoryId,
                    cancellationToken),
                repositoryId))
        {
            return null;
        }

        return IsRequestedRevision(handoff, requestedProfiledRevision) &&
            IsExactHandoff(handoff, repositoryId, scanRunId)
            ? Map(handoff)
            : null;
    }

    private async Task<ApprovedPreScanHandoff?> MaterializeAsync(
        Guid repositoryId,
        Guid scanRunId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await scanRunRepository.GetApprovedPreScanHandoffAsync(
                repositoryId,
                scanRunId,
                cancellationToken);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static bool IsExactSystemRepository(
        RepositoryEntity? repository,
        Guid systemId,
        Guid repositoryId) =>
        repository is not null &&
        repository.Id == repositoryId &&
        repository.SystemId == systemId;

    private static bool IsExactVisibleSystem(
        SystemEntity? system,
        Guid systemId) =>
        system is not null &&
        system.Id == systemId;

    private static bool IsExactStandaloneRepository(
        RepositoryEntity? repository,
        Guid repositoryId) =>
        repository is not null &&
        repository.Id == repositoryId &&
        repository.SystemId is null;

    private static bool IsExactHandoff(
        ApprovedPreScanHandoff handoff,
        Guid repositoryId,
        Guid scanRunId) =>
        handoff.RepositoryId == repositoryId &&
        handoff.ScanRunId == scanRunId &&
        handoff.ResultId == scanRunId &&
        handoff.State == PreScanApprovalState.PreScanApproved &&
        StringComparer.Ordinal.Equals(
            handoff.SchemaVersion,
            PreScanResult.CurrentSchemaVersion);

    private static bool IsRequestedRevision(
        ApprovedPreScanHandoff handoff,
        string? requestedProfiledRevision)
    {
        if (requestedProfiledRevision is null)
        {
            return true;
        }

        try
        {
            return handoff.MatchesProfiledRevision(requestedProfiledRevision);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static ApprovedPreScanHandoffDto Map(
        ApprovedPreScanHandoff handoff) =>
        new()
        {
            ResultId = handoff.ResultId,
            RepositoryId = handoff.RepositoryId,
            ScanRunId = handoff.ScanRunId,
            ProfiledRevision = handoff.ProfiledRevision,
            State = handoff.State.ToString(),
            SchemaVersion = handoff.SchemaVersion,
            ApprovedAt = handoff.ApprovedAt,
            ApprovalMessage = handoff.ApprovalMessage,
            ConfirmedKinds =
            [
                .. handoff.ConfirmedKinds.Select(kind => kind.ToString()),
            ],
            TotalConfirmedEvidenceCount = handoff.TotalConfirmedEvidenceCount,
            TotalHintCount = handoff.TotalHintCount,
            TotalWarningCount = handoff.TotalWarningCount,
            EvidenceTruncated = handoff.EvidenceTruncated,
            ConfirmedEvidence = Map(handoff.ConfirmedEvidence),
            Hints = Map(handoff.Hints),
            Warnings = Map(handoff.Warnings),
        };

    private static ApprovedPreScanClassificationDto[] Map(
        IEnumerable<ApprovedPreScanClassification> classifications) =>
        [
            .. classifications.Select(classification =>
                new ApprovedPreScanClassificationDto
                {
                    Kind = classification.Kind?.ToString(),
                    Level = classification.Level.ToString(),
                    Confidence = classification.Confidence.ToString(),
                    Rule = classification.Rule.ToString(),
                    Provenance = classification.Provenance.ToString(),
                    EvidenceLocator = classification.EvidenceLocator,
                    OccurrenceCount = classification.OccurrenceCount,
                }),
        ];
}
