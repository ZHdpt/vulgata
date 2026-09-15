using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Source;

internal sealed class RetainedSourceCaptureService(
    RetainedSourceCapturePolicy policy,
    GitCommittedObjectReader git,
    DurableRetainedSourceStore store,
    IRetainedSourceRepository snapshots,
    ISourceRepositoryAuthorizationVerifier authorization,
    IUserContext user,
    TimeProvider timeProvider) : IRetainedSourceApplication
{
    public async Task<SourceCaptureResult> AcquireAsync(
        Guid repositoryId,
        string permittedRef,
        CancellationToken cancellationToken = default)
    {
        Guid correlationId = Guid.NewGuid();
        long startedTimestamp = timeProvider.GetTimestamp();
        string? operationRoot = null;
        using var deadlineCancellation = new CancellationTokenSource();
        using var deadlineMonitorStop = new CancellationTokenSource();
        Task deadlineMonitor = MonitorDeadlineAsync(
            startedTimestamp,
            deadlineCancellation,
            deadlineMonitorStop.Token);
        using CancellationTokenSource operationCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                deadlineCancellation.Token);
        CancellationToken operationToken = operationCancellation.Token;
        try
        {
            var request = new SourceAuthorizationRequest(
                new SourceAuthorizationSubject(user.UserId),
                SourceAuthorizationPurpose.Capture,
                repositoryId,
                null);
            AuthorizedSourceRepository? initial = await authorization.AuthorizeAsync(
                request,
                operationToken);
            if (initial is null)
            {
                return Failure(SourceCaptureStatus.Denied, "source_denied", correlationId, repositoryId);
            }

            operationToken.ThrowIfCancellationRequested();
            ThrowIfDeadlineExceeded(startedTimestamp);
            operationRoot = store.CreateOperationRoot();
            GitCommittedObjectResult acquired = await git.CaptureAsync(
                initial.GitOrigin,
                permittedRef,
                Path.Combine(operationRoot, "git"),
                operationToken);
            if (acquired.Capture is null)
            {
                return acquired.Failure switch
                {
                    GitCommittedObjectFailure.InvalidRef => Failure(
                        SourceCaptureStatus.AcquisitionFailure,
                        "source_ref_invalid",
                        correlationId,
                        repositoryId),
                    GitCommittedObjectFailure.CaptureLimitExceeded => Failure(
                        SourceCaptureStatus.StorageFailure,
                        "source_capture_limit_exceeded",
                        correlationId,
                        repositoryId),
                    GitCommittedObjectFailure.UnsupportedEntry => Failure(
                        SourceCaptureStatus.Unsupported,
                        "source_entry_unsupported",
                        correlationId,
                        repositoryId),
                    _ => Failure(
                        SourceCaptureStatus.AcquisitionFailure,
                        "source_acquisition_failed",
                        correlationId,
                        repositoryId),
                };
            }

            ThrowIfDeadlineExceeded(startedTimestamp);
            await using RetainedSourceStaging staging = await store.StageAsync(
                repositoryId,
                acquired.Capture,
                operationRoot,
                operationToken);
            AuthorizedSourceRepository? current = await authorization.AuthorizeAsync(
                request with { ProfiledRevision = acquired.Capture.ProfiledRevision },
                operationToken);
            if (current is null || current != initial)
            {
                return Failure(SourceCaptureStatus.Denied, "source_denied", correlationId, repositoryId);
            }

            operationToken.ThrowIfCancellationRequested();
            ThrowIfDeadlineExceeded(startedTimestamp);
            await using FileStream identityLock = await store.AcquireIdentityLockAsync(
                repositoryId,
                acquired.Capture.ProfiledRevision,
                timeProvider,
                startedTimestamp,
                operationToken);
            SourceSnapshot? existing = await snapshots.GetAsync(
                repositoryId,
                acquired.Capture.ProfiledRevision,
                operationToken);
            if (existing is not null)
            {
                if (!StringComparer.Ordinal.Equals(
                        existing.ManifestSha256,
                        staging.Manifest.Sha256) ||
                    !StringComparer.Ordinal.Equals(
                        existing.GitObjectFormat,
                        acquired.Capture.GitObjectFormat) ||
                    existing.EntryCount != staging.Manifest.EntryCount ||
                    existing.TotalBlobBytes != staging.Manifest.TotalBlobBytes)
                {
                    return Failure(
                        SourceCaptureStatus.IntegrityFailure,
                        "source_integrity_failed",
                        correlationId,
                        repositoryId,
                        acquired.Capture.ProfiledRevision);
                }

                await store.VerifySealedAsync(
                    existing.InternalStorageKey,
                    staging.Manifest,
                    operationToken);
                AuthorizedSourceRepository? finalReplayAuthorization =
                    await authorization.AuthorizeAsync(
                        request with { ProfiledRevision = acquired.Capture.ProfiledRevision },
                        operationToken);
                if (finalReplayAuthorization is null || finalReplayAuthorization != initial)
                {
                    return Failure(
                        SourceCaptureStatus.Denied,
                        "source_denied",
                        correlationId,
                        repositoryId);
                }

                operationToken.ThrowIfCancellationRequested();
                ThrowIfDeadlineExceeded(startedTimestamp);
                return Success(SourceCaptureStatus.Replay, existing, correlationId);
            }

            SealedRetainedSource sealedSource = await store.SealAsync(
                repositoryId,
                acquired.Capture.ProfiledRevision,
                staging,
                operationToken);
            AuthorizedSourceRepository? finalPublicationAuthorization =
                await authorization.AuthorizeAsync(
                    request with { ProfiledRevision = acquired.Capture.ProfiledRevision },
                    operationToken);
            if (finalPublicationAuthorization is null || finalPublicationAuthorization != initial)
            {
                return Failure(
                    SourceCaptureStatus.Denied,
                    "source_denied",
                    correlationId,
                    repositoryId);
            }

            operationToken.ThrowIfCancellationRequested();
            ThrowIfDeadlineExceeded(startedTimestamp);
            DateTimeOffset completedAt = timeProvider.GetUtcNow();
            SourceSnapshot snapshot = SourceSnapshot.Create(
                repositoryId,
                acquired.Capture.ProfiledRevision,
                acquired.Capture.GitObjectFormat,
                staging.Manifest.Sha256,
                staging.Manifest.EntryCount,
                staging.Manifest.TotalBlobBytes,
                sealedSource.InternalStorageKey,
                completedAt);
            await snapshots.AddAsync(snapshot, CancellationToken.None);
            await snapshots.SaveChangesAsync(CancellationToken.None);
            return Success(SourceCaptureStatus.Completed, snapshot, correlationId);
        }
        catch (OperationCanceledException)
        {
            return cancellationToken.IsCancellationRequested
                ? Failure(SourceCaptureStatus.Cancelled, "source_cancelled", correlationId, repositoryId)
                : Failure(
                    SourceCaptureStatus.DeadlineExceeded,
                    "source_deadline_exceeded",
                    correlationId,
                    repositoryId);
        }
        catch (TimeoutException)
        {
            return Failure(
                SourceCaptureStatus.DeadlineExceeded,
                "source_deadline_exceeded",
                correlationId,
                repositoryId);
        }
        catch (RetainedSourceIntegrityException)
        {
            return Failure(
                SourceCaptureStatus.IntegrityFailure,
                "source_integrity_failed",
                correlationId,
                repositoryId);
        }
        catch (RetainedSourceStorageException)
        {
            return Failure(
                SourceCaptureStatus.StorageFailure,
                "source_storage_unavailable",
                correlationId,
                repositoryId);
        }
        catch
        {
            return Failure(
                SourceCaptureStatus.StorageFailure,
                "source_storage_unavailable",
                correlationId,
                repositoryId);
        }
        finally
        {
            deadlineMonitorStop.Cancel();
            try
            {
                await deadlineMonitor;
            }
            catch (OperationCanceledException)
            {
            }

            if (operationRoot is not null)
            {
                DurableRetainedSourceStore.TryDeleteOwnedOperation(operationRoot);
            }
        }
    }

    public async Task<OpenSourceReadContextResult> OpenReadContextAsync(
        Guid repositoryId,
        string profiledRevision,
        SourceReadGrantRequest requestedGrant,
        CancellationToken cancellationToken = default)
    {
        Guid correlationId = Guid.NewGuid();
        try
        {
            var subject = new SourceAuthorizationSubject(user.UserId);
            var request = new SourceAuthorizationRequest(
                subject,
                SourceAuthorizationPurpose.Read,
                repositoryId,
                profiledRevision);
            AuthorizedSourceRepository? initial = await authorization.AuthorizeAsync(
                request,
                cancellationToken);
            if (initial is null)
            {
                return OpenFailure(
                    SourceReadStatus.Denied,
                    "source_denied",
                    correlationId,
                    repositoryId,
                    profiledRevision);
            }

            if (!IsValidGrant(requestedGrant))
            {
                return OpenFailure(
                    SourceReadStatus.BudgetExceeded,
                    "source_grant_invalid",
                    correlationId,
                    repositoryId,
                    profiledRevision);
            }

            SourceSnapshot? snapshot = await snapshots.GetAsync(
                repositoryId,
                profiledRevision,
                cancellationToken);
            if (snapshot is null)
            {
                return OpenFailure(
                    SourceReadStatus.NotFoundOrUnavailable,
                    "source_not_found_or_unavailable",
                    correlationId,
                    repositoryId,
                    profiledRevision);
            }

            VerifiedRetainedSourceObject source = await store.OpenVerifiedAsync(
                snapshot,
                cancellationToken);
            AuthorizedSourceRepository? current = await authorization.AuthorizeAsync(
                request,
                cancellationToken);
            if (current is null || current != initial)
            {
                return OpenFailure(
                    SourceReadStatus.Denied,
                    "source_denied",
                    correlationId,
                    repositoryId,
                    profiledRevision);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var context = new RetainedSourceReadContext(
                source,
                subject,
                initial,
                policy,
                requestedGrant,
                authorization,
                snapshots,
                store,
                timeProvider);
            return new(
                SourceReadStatus.Succeeded,
                "source_context_opened",
                correlationId,
                repositoryId,
                profiledRevision,
                snapshot.ManifestSha256,
                context);
        }
        catch (OperationCanceledException)
        {
            return OpenFailure(
                SourceReadStatus.Cancelled,
                "source_cancelled",
                correlationId,
                repositoryId,
                profiledRevision);
        }
        catch (RetainedSourceIntegrityException)
        {
            return OpenFailure(
                SourceReadStatus.IntegrityFailure,
                "source_integrity_failed",
                correlationId,
                repositoryId,
                profiledRevision);
        }
        catch (RetainedSourceStorageException)
        {
            return OpenFailure(
                SourceReadStatus.StorageFailure,
                "source_storage_unavailable",
                correlationId,
                repositoryId,
                profiledRevision);
        }
    }

    private void ThrowIfDeadlineExceeded(long startedTimestamp)
    {
        if (timeProvider.GetElapsedTime(startedTimestamp) >= policy.CaptureDeadline)
        {
            throw new TimeoutException();
        }
    }

    private async Task MonitorDeadlineAsync(
        long startedTimestamp,
        CancellationTokenSource deadlineCancellation,
        CancellationToken stopToken)
    {
        TimeSpan maximumDelay = TimeSpan.FromMinutes(1);
        while (!stopToken.IsCancellationRequested)
        {
            TimeSpan remaining = policy.CaptureDeadline -
                timeProvider.GetElapsedTime(startedTimestamp);
            if (remaining <= TimeSpan.Zero)
            {
                await deadlineCancellation.CancelAsync();
                return;
            }

            TimeSpan delay = remaining < maximumDelay ? remaining : maximumDelay;
            await Task.Delay(delay, timeProvider, stopToken);
        }
    }

    private static SourceCaptureResult Success(
        SourceCaptureStatus status,
        SourceSnapshot snapshot,
        Guid correlationId) =>
        new(
            status,
            status == SourceCaptureStatus.Completed ? "source_completed" : "source_replay",
            correlationId,
            snapshot.RepositoryId,
            snapshot.ProfiledRevision,
            snapshot.GitObjectFormat,
            snapshot.ManifestSha256,
            snapshot.EntryCount,
            snapshot.TotalBlobBytes,
            snapshot.CompletedAt);

    private static SourceCaptureResult Failure(
        SourceCaptureStatus status,
        string code,
        Guid correlationId,
        Guid repositoryId,
        string? profiledRevision = null) =>
        new(
            status,
            code,
            correlationId,
            repositoryId,
            profiledRevision,
            null,
            null,
            null,
            null,
            null);

    private static OpenSourceReadContextResult OpenFailure(
        SourceReadStatus status,
        string code,
        Guid correlationId,
        Guid repositoryId,
        string profiledRevision) =>
        new(status, code, correlationId, repositoryId, profiledRevision, null, null);

    private static bool IsValidGrant(SourceReadGrantRequest? grant) =>
        grant is not null &&
        grant.MaximumOperations is not <= 0 &&
        grant.MaximumMetadataEntries is not <= 0 &&
        grant.MaximumReturnedBytes is not <= 0 &&
        grant.MaximumEntriesPerOperation is not <= 0 &&
        grant.MaximumBytesPerOperation is not <= 0 &&
        (grant.Deadline is null || grant.Deadline > TimeSpan.Zero);
}
