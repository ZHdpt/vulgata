using System.Collections.Immutable;
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Shared;

namespace Vulgata.Web.Data;

public sealed class OctoberAuthorityConfiguration
{
    public OctoberAuthorityConfiguration(
        string authorityId,
        string targetId,
        Guid batchId,
        string canonicalDatabaseIdentity,
        string dataConnectionString,
        string identityConnectionString,
        string identityCanonicalDatabaseIdentity,
        ImmutableDictionary<string, string> allowedTargets,
        TimeSpan operationTimeout,
        TimeSpan sessionLifetime)
    {
        AuthorityId = RequireIdentity(authorityId, nameof(authorityId));
        TargetId = RequireIdentity(targetId, nameof(targetId));
        BatchId = batchId != Guid.Empty
            ? batchId
            : throw new ArgumentException("A batch identity is required.", nameof(batchId));
        CanonicalDatabaseIdentity = RequireIdentity(canonicalDatabaseIdentity, nameof(canonicalDatabaseIdentity));
        DataConnectionString = RequireConnection(dataConnectionString, nameof(dataConnectionString));
        IdentityConnectionString = RequireConnection(identityConnectionString, nameof(identityConnectionString));
        IdentityCanonicalDatabaseIdentity = RequireIdentity(
            identityCanonicalDatabaseIdentity, nameof(identityCanonicalDatabaseIdentity));

        ArgumentNullException.ThrowIfNull(allowedTargets);
        if (allowedTargets.Count == 0 || allowedTargets.Any(entry =>
                !IsExactIdentity(entry.Key) || !IsExactIdentity(entry.Value)) ||
            !allowedTargets.Any(entry =>
                string.Equals(entry.Key, TargetId, StringComparison.Ordinal) &&
                string.Equals(entry.Value, CanonicalDatabaseIdentity, StringComparison.Ordinal)))
        {
            throw new ArgumentException("An exact selected target is required.", nameof(allowedTargets));
        }

        AllowedTargets = allowedTargets.ToImmutableDictionary(
            entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        if (operationTimeout <= TimeSpan.Zero || operationTimeout > TimeSpan.FromSeconds(30))
        {
            throw new ArgumentOutOfRangeException(nameof(operationTimeout));
        }

        if (sessionLifetime <= TimeSpan.Zero || sessionLifetime > TimeSpan.FromHours(1))
        {
            throw new ArgumentOutOfRangeException(nameof(sessionLifetime));
        }

        OperationTimeout = operationTimeout;
        SessionLifetime = sessionLifetime;
    }

    public string AuthorityId { get; }
    public string TargetId { get; }
    public Guid BatchId { get; }
    public string CanonicalDatabaseIdentity { get; }
    public string DataConnectionString { get; }
    public string IdentityConnectionString { get; }
    public string IdentityCanonicalDatabaseIdentity { get; }
    public ImmutableDictionary<string, string> AllowedTargets { get; }
    public TimeSpan OperationTimeout { get; }
    public TimeSpan SessionLifetime { get; }

    public override string ToString() => nameof(OctoberAuthorityConfiguration);

    private static string RequireIdentity(string value, string parameterName) =>
        IsExactIdentity(value)
            ? value
            : throw new ArgumentException("An exact identity is required.", parameterName);

    private static bool IsExactIdentity(string? value) =>
        !string.IsNullOrWhiteSpace(value) && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static string RequireConnection(string value, string parameterName) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException("An explicit connection is required.", parameterName);
}

public sealed class OctoberLocalAuthority : IOctoberLocalAuthority
{
    private readonly IServiceScopeFactory _scopes;
    private readonly OctoberAuthorityConfiguration _configuration;
    private readonly IOctoberOperatorCredentialSource _credentials;
    private readonly TimeProvider _clock;

    public OctoberLocalAuthority(
        IServiceScopeFactory scopes,
        OctoberAuthorityConfiguration configuration,
        IOctoberOperatorCredentialSource credentials,
        TimeProvider clock)
    {
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<OctoberAuthenticationResult> AuthenticateAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new(OctoberAuthenticationStatus.Cancelled, null);
        }

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_configuration.OperationTimeout);
        try
        {
            OctoberOperatorCredentials? credentialInput;
            try
            {
                credentialInput = await _credentials.ReadAsync(timeout.Token);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                CryptographicException or JsonException or InvalidDataException or FormatException or
                InvalidOperationException)
            {
                return new(OctoberAuthenticationStatus.Unavailable, null);
            }

            using OctoberOperatorCredentials? input = credentialInput;
            if (input is null || string.IsNullOrWhiteSpace(input.UserName) ||
                string.IsNullOrEmpty(input.Password) ||
                input.AuthenticatorCode is not null && input.RecoveryCode is not null)
            {
                return new(OctoberAuthenticationStatus.Denied, null);
            }

            await using AsyncServiceScope scope = _scopes.CreateAsyncScope();
            UserManager<ApplicationUser> users = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            SignInManager<ApplicationUser> signIn = scope.ServiceProvider
                .GetRequiredService<SignInManager<ApplicationUser>>();

            ApplicationUser? user = await users.FindByNameAsync(input.UserName);
            timeout.Token.ThrowIfCancellationRequested();
            if (user is null)
            {
                return new(OctoberAuthenticationStatus.Denied, null);
            }

            SignInResult password = await signIn.CheckPasswordSignInAsync(
                user, input.Password, lockoutOnFailure: false);
            timeout.Token.ThrowIfCancellationRequested();
            if (!password.Succeeded)
            {
                return new(OctoberAuthenticationStatus.Denied, null);
            }

            if (!users.SupportsUserTwoFactor)
            {
                return new(OctoberAuthenticationStatus.Denied, null);
            }

            if (await users.GetTwoFactorEnabledAsync(user))
            {
                timeout.Token.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(input.RecoveryCode) &&
                    string.IsNullOrWhiteSpace(input.AuthenticatorCode))
                {
                    return new(OctoberAuthenticationStatus.AdditionalAuthenticationRequired, null);
                }

                bool factorSucceeded;
                if (input.RecoveryCode is not null)
                {
                    string recoveryCode = input.RecoveryCode.Replace(" ", string.Empty, StringComparison.Ordinal);
                    IdentityResult redeemed = await users.RedeemTwoFactorRecoveryCodeAsync(user, recoveryCode);
                    factorSucceeded = redeemed.Succeeded;
                }
                else
                {
                    string authenticatorCode = input.AuthenticatorCode!
                        .Replace(" ", string.Empty, StringComparison.Ordinal)
                        .Replace("-", string.Empty, StringComparison.Ordinal);
                    try
                    {
                        factorSucceeded = await users.VerifyTwoFactorTokenAsync(
                            user, users.Options.Tokens.AuthenticatorTokenProvider, authenticatorCode);
                    }
                    catch (NotSupportedException)
                    {
                        return new(OctoberAuthenticationStatus.Denied, null);
                    }
                }

                timeout.Token.ThrowIfCancellationRequested();
                if (!factorSucceeded)
                {
                    IdentityResult failed = await users.AccessFailedAsync(user);
                    timeout.Token.ThrowIfCancellationRequested();
                    return new(failed.Succeeded
                        ? OctoberAuthenticationStatus.Denied
                        : OctoberAuthenticationStatus.Unavailable, null);
                }

                IdentityResult reset = await users.ResetAccessFailedCountAsync(user);
                timeout.Token.ThrowIfCancellationRequested();
                if (!reset.Succeeded)
                {
                    return new(OctoberAuthenticationStatus.Unavailable, null);
                }
            }

            string subjectId = await users.GetUserIdAsync(user);
            string stamp = await users.GetSecurityStampAsync(user);
            timeout.Token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(subjectId) || string.IsNullOrWhiteSpace(stamp))
            {
                return new(OctoberAuthenticationStatus.Denied, null);
            }

            DateTimeOffset expiry = _clock.GetUtcNow().Add(_configuration.SessionLifetime);
            return new(OctoberAuthenticationStatus.Authenticated,
                new OperatorSession(this, subjectId, _configuration.AuthorityId, stamp, expiry));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(OctoberAuthenticationStatus.Cancelled, null);
        }
        catch (OperationCanceledException)
        {
            return new(OctoberAuthenticationStatus.Unavailable, null);
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException or TimeoutException)
        {
            return new(OctoberAuthenticationStatus.Unavailable, null);
        }
    }

    private sealed class OperatorSession(
        OctoberLocalAuthority owner,
        string subjectId,
        string managementAuthorityId,
        string securityStamp,
        DateTimeOffset expiresAtUtc) : IOctoberOperatorSession
    {
        private readonly OctoberLocalAuthority _owner = owner;
        private readonly string _securityStamp = securityStamp;
        private volatile bool _closed;

        public string SubjectId { get; } = subjectId;
        public string ManagementAuthorityId { get; } = managementAuthorityId;
        public DateTimeOffset ExpiresAtUtc { get; } = expiresAtUtc;

        public bool IsCurrentFor(OctoberLocalAuthority authority)
        {
            if (_closed || !ReferenceEquals(_owner, authority))
            {
                return false;
            }

            if (ExpiresAtUtc <= authority._clock.GetUtcNow())
            {
                _closed = true;
                return false;
            }

            return true;
        }

        public bool HasSecurityStamp(string currentStamp) =>
            string.Equals(_securityStamp, currentStamp, StringComparison.Ordinal);

        public void Close() => _closed = true;

        public ValueTask DisposeAsync()
        {
            _closed = true;
            return ValueTask.CompletedTask;
        }
    }

    public async Task<OctoberAuthoritySelectionResult> AuthorizeSelectionAsync(
        IOctoberOperatorSession session,
        OctoberAuthoritySelectionRequest request,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new(OctoberAuthorityDecisionStatus.Cancelled, null, null);
        }

        if (session is not OperatorSession current || !current.IsCurrentFor(this) ||
            request is null ||
            !string.Equals(request.TargetId, _configuration.TargetId, StringComparison.Ordinal) ||
            request.BatchId != _configuration.BatchId ||
            request.PrepareRequest is not null &&
                (!string.Equals(request.PrepareRequest.TargetId, request.TargetId, StringComparison.Ordinal) ||
                 request.PrepareRequest.BatchId != request.BatchId ||
                 request.PrepareRequest.Repositories is null or { Count: 0 } ||
                 request.PrepareRequest.ContextSelections is null) ||
            !_configuration.AllowedTargets.TryGetValue(request.TargetId, out string? databaseIdentity) ||
            !string.Equals(databaseIdentity, _configuration.CanonicalDatabaseIdentity, StringComparison.Ordinal))
        {
            return new(OctoberAuthorityDecisionStatus.Denied, null, null);
        }

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_configuration.OperationTimeout);
        try
        {
            await using AsyncServiceScope scope = _scopes.CreateAsyncScope();
            UserManager<ApplicationUser> users = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            SignInManager<ApplicationUser> signIn = scope.ServiceProvider
                .GetRequiredService<SignInManager<ApplicationUser>>();
            (bool allowed, bool administrator) = await CheckCurrentSubjectAsync(
                current, users, signIn, timeout.Token);
            if (!allowed)
            {
                return new(OctoberAuthorityDecisionStatus.Denied, null, null);
            }

            OctoberSetupRequest frozen;
            if (request.PrepareRequest is null)
            {
                ExistingBatchMetadata existing = await ReadExactBatchMetadataAsync(
                    scope.ServiceProvider, request, current, timeout.Token);
                if (existing.Request is null)
                {
                    return new(existing.Status, null, null);
                }

                frozen = existing.Request;
            }
            else
            {
                frozen = request.PrepareRequest with
                {
                    Repositories = request.PrepareRequest.Repositories.ToImmutableArray(),
                    ContextSelections = request.PrepareRequest.ContextSelections.ToImmutableArray()
                };
            }

            if (!ValidPrepareShape(frozen))
            {
                return new(OctoberAuthorityDecisionStatus.Denied, null, null);
            }

            await using VulgataDbContext management = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<VulgataDbContext>>()
                .CreateDbContextAsync(timeout.Token);
            OctoberSelectedAdvisoryContext? context = await CaptureCurrentContextAsync(
                management, frozen, current, administrator, timeout.Token);
            if (context is null)
            {
                return new(OctoberAuthorityDecisionStatus.Denied, null, null);
            }

            string digest = OctoberSelectionDigest.Compute(frozen, context);
            if (request.PrepareRequest is not null && !string.IsNullOrEmpty(frozen.SelectionDigest) &&
                !string.Equals(frozen.SelectionDigest, digest, StringComparison.Ordinal))
            {
                return new(OctoberAuthorityDecisionStatus.Denied, null, null);
            }

            (allowed, _) = await CheckCurrentSubjectAsync(current, users, signIn, timeout.Token);
            if (!allowed)
            {
                return new(OctoberAuthorityDecisionStatus.Denied, null, null);
            }

            OctoberSetupRequest canonical = frozen with { SelectionDigest = digest };
            OctoberAuthorizedSelection authority = new(
                canonical.TargetId, canonical.BatchId, current.SubjectId,
                _configuration.CanonicalDatabaseIdentity,
                canonical.Repositories.Select(value => value.RepositoryId).ToImmutableArray(),
                digest, context);
            timeout.Token.ThrowIfCancellationRequested();
            if (!current.IsCurrentFor(this))
            {
                return new(OctoberAuthorityDecisionStatus.Denied, null, null);
            }

            return new(OctoberAuthorityDecisionStatus.Authorized, canonical, authority);
        }
        catch (ArgumentException)
        {
            return new(OctoberAuthorityDecisionStatus.Denied, null, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(OctoberAuthorityDecisionStatus.Cancelled, null, null);
        }
        catch (OperationCanceledException)
        {
            return new(OctoberAuthorityDecisionStatus.Unavailable, null, null);
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException or TimeoutException)
        {
            return new(OctoberAuthorityDecisionStatus.Unavailable, null, null);
        }
    }

    private sealed record ExistingBatchMetadata(
        OctoberAuthorityDecisionStatus Status,
        OctoberSetupRequest? Request);

    private async Task<ExistingBatchMetadata> ReadExactBatchMetadataAsync(
        IServiceProvider services,
        OctoberAuthoritySelectionRequest selection,
        OperatorSession session,
        CancellationToken cancellationToken)
    {
        OctoberDbContext database = services.GetRequiredService<OctoberDbContext>();
        OctoberBatchRecord? batch = await database.Batches.AsNoTracking()
            .SingleOrDefaultAsync(value => value.BatchId == selection.BatchId, cancellationToken);
        if (batch is null)
        {
            bool anotherBatch = await database.Batches.AsNoTracking().AnyAsync(cancellationToken);
            return new(anotherBatch
                ? OctoberAuthorityDecisionStatus.BatchMismatch
                : OctoberAuthorityDecisionStatus.MissingBatch, null);
        }

        if (batch.State != OctoberBatchState.Selectable ||
            !string.Equals(batch.TargetId, selection.TargetId, StringComparison.Ordinal) ||
            !string.Equals(batch.SubjectId, session.SubjectId, StringComparison.Ordinal) ||
            !string.Equals(batch.CanonicalDatabaseIdentity,
                _configuration.CanonicalDatabaseIdentity, StringComparison.Ordinal) ||
            !string.Equals(batch.ManagementAuthorityId,
                _configuration.AuthorityId, StringComparison.Ordinal))
        {
            return new(OctoberAuthorityDecisionStatus.Denied, null);
        }

        OctoberRepositoryRecord[] repositories = await database.RepositorySelections.AsNoTracking()
            .Where(value => value.BatchId == batch.BatchId).ToArrayAsync(cancellationToken);
        OctoberContextReferenceRecord[] references = await database.ContextReferences.AsNoTracking()
            .Where(value => value.BatchId == batch.BatchId).ToArrayAsync(cancellationToken);
        OctoberSetupRequest request = new(
            batch.TargetId,
            batch.BatchId,
            repositories.Select(value => new OctoberRepositorySelection(
                value.RepositoryId, value.SystemId,
                value.RootBindingIdentity, value.OperatorLabel)).ToImmutableArray(),
            references.Select(value => new OctoberContextSelection(
                (OctoberContextScope)value.Scope, value.RecordId)).ToImmutableArray(),
            string.Empty);
        return new(OctoberAuthorityDecisionStatus.Authorized, request);
    }

    private async Task<(bool Allowed, bool Administrator)> CheckCurrentSubjectAsync(
        OperatorSession session,
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        CancellationToken cancellationToken)
    {
        if (!session.IsCurrentFor(this) ||
            !string.Equals(session.ManagementAuthorityId, _configuration.AuthorityId, StringComparison.Ordinal))
        {
            return (false, false);
        }

        ApplicationUser? user = await users.FindByIdAsync(session.SubjectId);
        cancellationToken.ThrowIfCancellationRequested();
        if (user is null || !await signIn.CanSignInAsync(user) ||
            await users.IsLockedOutAsync(user))
        {
            session.Close();
            return (false, false);
        }

        string stamp = await users.GetSecurityStampAsync(user);
        cancellationToken.ThrowIfCancellationRequested();
        if (!session.HasSecurityStamp(stamp))
        {
            session.Close();
            return (false, false);
        }

        bool administrator = await users.IsInRoleAsync(user, RoleNames.Administrator);
        bool systemOwner = await users.IsInRoleAsync(user, RoleNames.SystemOwner);
        cancellationToken.ThrowIfCancellationRequested();
        if (!administrator && !systemOwner)
        {
            session.Close();
            return (false, false);
        }

        return (true, administrator);
    }

    private static bool ValidPrepareShape(OctoberSetupRequest request)
    {
        HashSet<Guid> repositories = [];
        foreach (OctoberRepositorySelection repository in request.Repositories)
        {
            if (repository is null || repository.RepositoryId == Guid.Empty ||
                repository.SystemId == Guid.Empty || string.IsNullOrWhiteSpace(repository.RootBindingIdentity) ||
                string.IsNullOrWhiteSpace(repository.OperatorLabel) || !repositories.Add(repository.RepositoryId))
            {
                return false;
            }
        }

        HashSet<(OctoberContextScope, Guid)> references = [];
        foreach (OctoberContextSelection reference in request.ContextSelections)
        {
            if (reference is null || reference.RecordId == Guid.Empty ||
                !Enum.IsDefined(reference.Scope) || !references.Add((reference.Scope, reference.RecordId)))
            {
                return false;
            }
        }

        return references.Count(value => value.Item1 == OctoberContextScope.Global) <= 1;
    }

    private async Task<OctoberSelectedAdvisoryContext?> CaptureCurrentContextAsync(
        VulgataDbContext management,
        OctoberSetupRequest request,
        OperatorSession session,
        bool administrator,
        CancellationToken cancellationToken)
    {
        await using var transaction = await management.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        Dictionary<Guid, Vulgata.Core.Entities.Repository> repositories = [];
        Dictionary<Guid, Vulgata.Core.Entities.System> systems = [];
        foreach (OctoberRepositorySelection selection in request.Repositories)
        {
            Vulgata.Core.Entities.Repository? repository = await management.Repositories.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == selection.RepositoryId, cancellationToken);
            if (repository is null || repository.SystemId != selection.SystemId)
            {
                return null;
            }

            repositories.Add(selection.RepositoryId, repository);
            if (selection.SystemId is Guid systemId)
            {
                if (!systems.ContainsKey(systemId))
                {
                    Vulgata.Core.Entities.System? system = await management.Systems.AsNoTracking()
                        .SingleOrDefaultAsync(value => value.Id == systemId, cancellationToken);
                    if (system is null)
                    {
                        return null;
                    }

                    systems.Add(systemId, system);
                }

                if (!administrator && !await management.SystemOwnerAssignments.AsNoTracking()
                        .AnyAsync(value => value.SystemId == systemId && value.UserId == session.SubjectId,
                            cancellationToken))
                {
                    session.Close();
                    return null;
                }
            }
        }

        Guid? globalId = null;
        string? globalValue = null;
        Dictionary<Guid, string?> systemValues = [];
        Dictionary<Guid, string?> repositoryValues = [];
        foreach (OctoberContextSelection reference in request.ContextSelections)
        {
            switch (reference.Scope)
            {
                case OctoberContextScope.Global:
                    Vulgata.Core.Entities.GlobalContext? global = await management.GlobalContexts.AsNoTracking()
                        .SingleOrDefaultAsync(value => value.Id == reference.RecordId, cancellationToken);
                    if (global is null || globalId is not null) return null;
                    globalId = global.Id;
                    globalValue = global.Context;
                    break;
                case OctoberContextScope.System:
                    if (!systems.TryGetValue(reference.RecordId, out Vulgata.Core.Entities.System? system))
                        return null;
                    systemValues.Add(reference.RecordId, system.Context);
                    break;
                case OctoberContextScope.Repository:
                    if (!repositories.TryGetValue(reference.RecordId,
                            out Vulgata.Core.Entities.Repository? repository)) return null;
                    repositoryValues.Add(reference.RecordId, repository.Context);
                    break;
                default:
                    return null;
            }
        }

        List<OctoberAdvisoryContextEntry> entries = [];
        List<OctoberContextDifference> differences = [];
        foreach (OctoberRepositorySelection selection in request.Repositories)
        {
            List<OctoberAdvisoryContextEntry> selected = [];
            Add(OctoberContextScope.Global, globalId, globalValue, false);
            bool hasSystem = selection.SystemId is Guid sid && systemValues.TryGetValue(sid, out _);
            Add(OctoberContextScope.System, hasSystem ? selection.SystemId : null,
                hasSystem ? systemValues[selection.SystemId!.Value] : null, selection.SystemId is null);
            bool hasRepository = repositoryValues.TryGetValue(selection.RepositoryId, out string? repositoryValue);
            Add(OctoberContextScope.Repository, hasRepository ? selection.RepositoryId : null,
                repositoryValue, false);

            for (int left = 0; left < selected.Count; left++)
            for (int right = left + 1; right < selected.Count; right++)
            {
                if (!string.Equals(selected[left].Value, selected[right].Value, StringComparison.Ordinal))
                {
                    differences.Add(new OctoberContextDifference(selection.RepositoryId,
                        selected[left].Scope, selected[right].Scope));
                }
            }

            void Add(OctoberContextScope scope, Guid? origin, string? value, bool notApplicable)
            {
                OctoberAdvisoryContextEntry entry = new(
                    selection.RepositoryId, scope,
                    notApplicable ? OctoberContextApplicability.NotApplicable : origin is null
                        ? OctoberContextApplicability.NotSelected : OctoberContextApplicability.Selected,
                    origin, origin is null ? null : value,
                    OctoberContextEvidenceKind.AdvisoryNotSourceProof);
                entries.Add(entry);
                if (origin is not null) selected.Add(entry);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return new OctoberSelectedAdvisoryContext(
            _configuration.AuthorityId,
            request.Repositories.OrderBy(value => value.RepositoryId).ToImmutableArray(),
            entries.ToImmutableArray(), differences.ToImmutableArray());
    }

    public async Task<OctoberSourceApprovalResult> IssueSourceApprovalAsync(
        IOctoberOperatorSession session,
        IOctoberHistoricalBoundSetupData boundSetup,
        Guid repositoryId,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new(OctoberSourceApprovalStatus.Cancelled, null, null);
        }

        if (session is not OperatorSession current || !current.IsCurrentFor(this) ||
            boundSetup is null || repositoryId == Guid.Empty ||
            !string.Equals(boundSetup.TargetId, _configuration.TargetId, StringComparison.Ordinal) ||
            boundSetup.BatchId != _configuration.BatchId ||
            !_configuration.AllowedTargets.TryGetValue(boundSetup.TargetId, out string? databaseIdentity) ||
            !string.Equals(databaseIdentity, _configuration.CanonicalDatabaseIdentity,
                StringComparison.Ordinal))
        {
            return new(OctoberSourceApprovalStatus.Denied, null, null);
        }

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_configuration.OperationTimeout);
        try
        {
            OctoberBoundSetupSnapshot supplied = boundSetup.Snapshot;
            if (supplied?.Request is null || supplied.Authority?.SelectedContext is null ||
                !string.Equals(supplied.Request.TargetId, boundSetup.TargetId, StringComparison.Ordinal) ||
                supplied.Request.BatchId != boundSetup.BatchId ||
                !string.Equals(supplied.Authority.TargetId, boundSetup.TargetId, StringComparison.Ordinal) ||
                supplied.Authority.BatchId != boundSetup.BatchId ||
                !string.Equals(supplied.Authority.SubjectId, current.SubjectId, StringComparison.Ordinal) ||
                !string.Equals(supplied.Authority.CanonicalDatabaseIdentity,
                    _configuration.CanonicalDatabaseIdentity, StringComparison.Ordinal) ||
                !string.Equals(supplied.Authority.SelectedContext.ManagementAuthorityId,
                    _configuration.AuthorityId, StringComparison.Ordinal) ||
                !string.Equals(supplied.Request.SelectionDigest, supplied.Authority.SelectionDigest,
                    StringComparison.Ordinal) ||
                !string.Equals(OctoberSelectionDigest.Compute(
                    supplied.Request, supplied.Authority.SelectedContext),
                    supplied.Request.SelectionDigest, StringComparison.Ordinal))
            {
                return new(OctoberSourceApprovalStatus.Denied, null, null);
            }

            await using AsyncServiceScope scope = _scopes.CreateAsyncScope();
            UserManager<ApplicationUser> users = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            SignInManager<ApplicationUser> signIn = scope.ServiceProvider
                .GetRequiredService<SignInManager<ApplicationUser>>();
            (bool allowed, bool administrator) = await CheckCurrentSubjectAsync(
                current, users, signIn, timeout.Token);
            if (!allowed)
            {
                return new(OctoberSourceApprovalStatus.Denied, null, null);
            }

            OctoberDbContext october = scope.ServiceProvider.GetRequiredService<OctoberDbContext>();
            OctoberBatchSelector selector = new(october, _configuration.TargetId,
                _configuration.CanonicalDatabaseIdentity);
            OctoberSelectionResult selected = await selector.BindAsync(
                new OctoberStartupSelection(boundSetup.TargetId, boundSetup.BatchId, supplied.Authority),
                timeout.Token);
            if (selected.Status == OctoberSelectionStatus.AuthorityUnavailable)
            {
                return new(OctoberSourceApprovalStatus.Unavailable, null, null);
            }

            if (selected.Status != OctoberSelectionStatus.Selected ||
                selected.BoundSetupData is not IOctoberHistoricalBoundSetupData persisted ||
                supplied.SelectedAtUtc != persisted.Snapshot.SelectedAtUtc ||
                !string.Equals(supplied.Request.SelectionDigest,
                    persisted.Snapshot.Request.SelectionDigest, StringComparison.Ordinal) ||
                !string.Equals(supplied.Authority.SelectionDigest,
                    persisted.Snapshot.Authority.SelectionDigest, StringComparison.Ordinal) ||
                !ValidPrepareShape(persisted.Snapshot.Request))
            {
                return new(selected.Status == OctoberSelectionStatus.Cancelled
                    ? cancellationToken.IsCancellationRequested
                        ? OctoberSourceApprovalStatus.Cancelled
                        : OctoberSourceApprovalStatus.Unavailable
                    : OctoberSourceApprovalStatus.Denied, null, null);
            }

            OctoberRepositorySelection[] bindings = persisted.Snapshot.Request.Repositories
                .Where(value => value.RepositoryId == repositoryId).ToArray();
            if (bindings.Length != 1)
            {
                return new(OctoberSourceApprovalStatus.Denied, null, null);
            }

            await using VulgataDbContext management = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<VulgataDbContext>>()
                .CreateDbContextAsync(timeout.Token);
            if (await CaptureCurrentContextAsync(management, persisted.Snapshot.Request,
                    current, administrator, timeout.Token) is null)
            {
                return new(OctoberSourceApprovalStatus.Denied, null, null);
            }

            (allowed, _) = await CheckCurrentSubjectAsync(current, users, signIn, timeout.Token);
            if (!allowed)
            {
                return new(OctoberSourceApprovalStatus.Denied, null, null);
            }

            DateTimeOffset now = _clock.GetUtcNow().ToUniversalTime();
            DateTimeOffset expiresAtUtc = now.AddMinutes(10) < current.ExpiresAtUtc
                ? now.AddMinutes(10) : current.ExpiresAtUtc;
            if (expiresAtUtc <= now || !current.IsCurrentFor(this))
            {
                return new(OctoberSourceApprovalStatus.Denied, null, null);
            }

            OctoberRepositorySelection binding = bindings[0];
            PreparedSourceApproval approval = new(
                boundSetup.BatchId, repositoryId, current.SubjectId,
                PreparedSourcePurpose.WorkerSourceRead,
                Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)),
                binding.RootBindingIdentity, binding.OperatorLabel);
            october.PreparedSourceApprovals.Add(new OctoberPreparedSourceApprovalRecord
            {
                ApprovalIdentity = approval.ApprovalIdentity,
                BatchId = approval.BatchId,
                RepositoryId = approval.RepositoryId,
                SubjectId = approval.SubjectId,
                Purpose = (int)approval.Purpose,
                RootBindingIdentity = approval.RootBindingIdentity,
                OperatorLabel = approval.OperatorLabel,
                ExpiresAtUtc = expiresAtUtc,
                IsRevoked = false
            });
            timeout.Token.ThrowIfCancellationRequested();
            await october.SaveChangesAsync(timeout.Token);
            return new(OctoberSourceApprovalStatus.Issued, approval, expiresAtUtc);
        }
        catch (ArgumentException)
        {
            return new(OctoberSourceApprovalStatus.Denied, null, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(OctoberSourceApprovalStatus.Cancelled, null, null);
        }
        catch (OperationCanceledException)
        {
            return new(OctoberSourceApprovalStatus.Unavailable, null, null);
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException or TimeoutException)
        {
            return new(OctoberSourceApprovalStatus.Unavailable, null, null);
        }
    }

    public async Task<PreparedSourceAuthorityResult> VerifySourceApprovalAsync(
        IOctoberOperatorSession session,
        string targetId,
        PreparedSourceApproval approval,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (session is not OperatorSession current || !current.IsCurrentFor(this) ||
            !string.Equals(targetId, _configuration.TargetId, StringComparison.Ordinal) ||
            approval is null || approval.BatchId != _configuration.BatchId ||
            approval.RepositoryId == Guid.Empty ||
            !string.Equals(approval.SubjectId, current.SubjectId, StringComparison.Ordinal) ||
            approval.Purpose != PreparedSourcePurpose.WorkerSourceRead ||
            string.IsNullOrWhiteSpace(approval.ApprovalIdentity) ||
            string.IsNullOrWhiteSpace(approval.RootBindingIdentity) ||
            string.IsNullOrWhiteSpace(approval.OperatorLabel) ||
            !_configuration.AllowedTargets.TryGetValue(targetId, out string? databaseIdentity) ||
            !string.Equals(databaseIdentity, _configuration.CanonicalDatabaseIdentity,
                StringComparison.Ordinal))
        {
            return new(PreparedSourceAuthorityStatus.Revoked, null);
        }

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_configuration.OperationTimeout);
        try
        {
            await using AsyncServiceScope scope = _scopes.CreateAsyncScope();
            UserManager<ApplicationUser> users = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            SignInManager<ApplicationUser> signIn = scope.ServiceProvider
                .GetRequiredService<SignInManager<ApplicationUser>>();
            (bool allowed, bool administrator) = await CheckCurrentSubjectAsync(
                current, users, signIn, timeout.Token);
            if (!allowed)
            {
                return new(PreparedSourceAuthorityStatus.Revoked, null);
            }

            OctoberDbContext october = scope.ServiceProvider.GetRequiredService<OctoberDbContext>();
            ExistingBatchMetadata batch = await ReadExactBatchMetadataAsync(scope.ServiceProvider,
                new OctoberAuthoritySelectionRequest(targetId, approval.BatchId, null),
                current, timeout.Token);
            if (batch.Request is null)
            {
                return new(PreparedSourceAuthorityStatus.Revoked, null);
            }

            OctoberRepositorySelection[] bindings = batch.Request.Repositories
                .Where(value => value.RepositoryId == approval.RepositoryId).ToArray();
            if (bindings.Length != 1 ||
                !string.Equals(bindings[0].RootBindingIdentity,
                    approval.RootBindingIdentity, StringComparison.Ordinal) ||
                !string.Equals(bindings[0].OperatorLabel,
                    approval.OperatorLabel, StringComparison.Ordinal))
            {
                return new(PreparedSourceAuthorityStatus.Revoked, null);
            }

            OctoberPreparedSourceApprovalRecord? row = await october.PreparedSourceApprovals.AsNoTracking()
                .SingleOrDefaultAsync(value => value.ApprovalIdentity == approval.ApprovalIdentity,
                    timeout.Token);
            if (row is null || row.IsRevoked || row.ExpiresAtUtc <= _clock.GetUtcNow() ||
                row.BatchId != approval.BatchId || row.RepositoryId != approval.RepositoryId ||
                !string.Equals(row.SubjectId, approval.SubjectId, StringComparison.Ordinal) ||
                row.Purpose != (int)PreparedSourcePurpose.WorkerSourceRead ||
                !string.Equals(row.RootBindingIdentity, approval.RootBindingIdentity,
                    StringComparison.Ordinal) ||
                !string.Equals(row.OperatorLabel, approval.OperatorLabel, StringComparison.Ordinal))
            {
                return new(PreparedSourceAuthorityStatus.Revoked, null);
            }

            await using VulgataDbContext management = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<VulgataDbContext>>()
                .CreateDbContextAsync(timeout.Token);
            if (!await HasCurrentRepositoryAuthorityAsync(management, bindings[0],
                    current.SubjectId, administrator, timeout.Token))
            {
                current.Close();
                return new(PreparedSourceAuthorityStatus.Revoked, null);
            }

            (allowed, _) = await CheckCurrentSubjectAsync(current, users, signIn, timeout.Token);
            timeout.Token.ThrowIfCancellationRequested();
            if (!allowed || !current.IsCurrentFor(this) || row.ExpiresAtUtc <= _clock.GetUtcNow())
            {
                return new(PreparedSourceAuthorityStatus.Revoked, null);
            }

            return new(PreparedSourceAuthorityStatus.Current,
                new PreparedSourceApproval(row.BatchId, row.RepositoryId, row.SubjectId,
                    (PreparedSourcePurpose)row.Purpose, row.ApprovalIdentity,
                    row.RootBindingIdentity, row.OperatorLabel));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new(PreparedSourceAuthorityStatus.Unavailable, null);
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException or TimeoutException)
        {
            return new(PreparedSourceAuthorityStatus.Unavailable, null);
        }
    }

    private static async Task<bool> HasCurrentRepositoryAuthorityAsync(
        VulgataDbContext management,
        OctoberRepositorySelection binding,
        string subjectId,
        bool administrator,
        CancellationToken cancellationToken)
    {
        Vulgata.Core.Entities.Repository? repository = await management.Repositories.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == binding.RepositoryId, cancellationToken);
        if (repository is null || repository.SystemId != binding.SystemId)
        {
            return false;
        }

        if (binding.SystemId is not Guid systemId)
        {
            return true;
        }

        return await management.Systems.AsNoTracking()
                .AnyAsync(value => value.Id == systemId, cancellationToken) &&
            (administrator || await management.SystemOwnerAssignments.AsNoTracking()
                .AnyAsync(value => value.SystemId == systemId && value.UserId == subjectId,
                    cancellationToken));
    }

    public async Task<OctoberRevocationStatus> RevokeSourceApprovalAsync(
        IOctoberOperatorSession session,
        string targetId,
        string approvalIdentity,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return OctoberRevocationStatus.Cancelled;
        }

        if (session is not OperatorSession current || !current.IsCurrentFor(this) ||
            !string.Equals(targetId, _configuration.TargetId, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(approvalIdentity) ||
            !_configuration.AllowedTargets.TryGetValue(targetId, out string? databaseIdentity) ||
            !string.Equals(databaseIdentity, _configuration.CanonicalDatabaseIdentity,
                StringComparison.Ordinal))
        {
            return OctoberRevocationStatus.Denied;
        }

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_configuration.OperationTimeout);
        try
        {
            await using AsyncServiceScope scope = _scopes.CreateAsyncScope();
            UserManager<ApplicationUser> users = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            SignInManager<ApplicationUser> signIn = scope.ServiceProvider
                .GetRequiredService<SignInManager<ApplicationUser>>();
            (bool allowed, bool administrator) = await CheckCurrentSubjectAsync(
                current, users, signIn, timeout.Token);
            if (!allowed)
            {
                return OctoberRevocationStatus.Denied;
            }

            OctoberDbContext october = scope.ServiceProvider.GetRequiredService<OctoberDbContext>();
            OctoberPreparedSourceApprovalRecord? row = await october.PreparedSourceApprovals.AsNoTracking()
                .SingleOrDefaultAsync(value => value.ApprovalIdentity == approvalIdentity,
                    timeout.Token);
            if (row is null || row.BatchId != _configuration.BatchId ||
                row.RepositoryId == Guid.Empty ||
                row.Purpose != (int)PreparedSourcePurpose.WorkerSourceRead ||
                string.IsNullOrWhiteSpace(row.SubjectId) ||
                string.IsNullOrWhiteSpace(row.RootBindingIdentity) ||
                string.IsNullOrWhiteSpace(row.OperatorLabel) ||
                !administrator && !string.Equals(row.SubjectId, current.SubjectId,
                    StringComparison.Ordinal))
            {
                return OctoberRevocationStatus.Denied;
            }

            OctoberBatchRecord? batch = await october.Batches.AsNoTracking()
                .SingleOrDefaultAsync(value => value.BatchId == row.BatchId, timeout.Token);
            OctoberRepositoryRecord? binding = await october.RepositorySelections.AsNoTracking()
                .SingleOrDefaultAsync(value => value.BatchId == row.BatchId &&
                    value.RepositoryId == row.RepositoryId, timeout.Token);
            if (batch is null || batch.State != OctoberBatchState.Selectable ||
                !string.Equals(batch.TargetId, targetId, StringComparison.Ordinal) ||
                !string.Equals(batch.CanonicalDatabaseIdentity,
                    _configuration.CanonicalDatabaseIdentity, StringComparison.Ordinal) ||
                !string.Equals(batch.ManagementAuthorityId,
                    _configuration.AuthorityId, StringComparison.Ordinal) ||
                !string.Equals(batch.SubjectId, row.SubjectId, StringComparison.Ordinal) ||
                binding is null ||
                !string.Equals(binding.RootBindingIdentity,
                    row.RootBindingIdentity, StringComparison.Ordinal) ||
                !string.Equals(binding.OperatorLabel, row.OperatorLabel, StringComparison.Ordinal))
            {
                return OctoberRevocationStatus.Denied;
            }

            await using VulgataDbContext management = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<VulgataDbContext>>()
                .CreateDbContextAsync(timeout.Token);
            OctoberRepositorySelection selection = new(binding.RepositoryId, binding.SystemId,
                binding.RootBindingIdentity, binding.OperatorLabel);
            if (!await HasCurrentRepositoryAuthorityAsync(management, selection,
                    current.SubjectId, administrator, timeout.Token))
            {
                current.Close();
                return OctoberRevocationStatus.Denied;
            }

            (allowed, _) = await CheckCurrentSubjectAsync(current, users, signIn, timeout.Token);
            timeout.Token.ThrowIfCancellationRequested();
            if (!allowed || !current.IsCurrentFor(this))
            {
                return OctoberRevocationStatus.Denied;
            }

            if (row.IsRevoked)
            {
                return OctoberRevocationStatus.AlreadyRevoked;
            }

            int changed = await october.PreparedSourceApprovals
                .Where(value => value.ApprovalIdentity == approvalIdentity && !value.IsRevoked &&
                    value.BatchId == row.BatchId && value.RepositoryId == row.RepositoryId &&
                    value.SubjectId == row.SubjectId && value.Purpose == row.Purpose &&
                    value.RootBindingIdentity == row.RootBindingIdentity &&
                    value.OperatorLabel == row.OperatorLabel)
                .ExecuteUpdateAsync(update => update.SetProperty(value => value.IsRevoked, true),
                    timeout.Token);
            if (changed == 1)
            {
                return OctoberRevocationStatus.Revoked;
            }

            OctoberPreparedSourceApprovalRecord? latest = await october.PreparedSourceApprovals.AsNoTracking()
                .SingleOrDefaultAsync(value => value.ApprovalIdentity == approvalIdentity,
                    timeout.Token);
            return latest is not null && latest.IsRevoked && latest.BatchId == row.BatchId &&
                latest.RepositoryId == row.RepositoryId && latest.SubjectId == row.SubjectId &&
                latest.Purpose == row.Purpose && latest.RootBindingIdentity == row.RootBindingIdentity &&
                latest.OperatorLabel == row.OperatorLabel
                    ? OctoberRevocationStatus.AlreadyRevoked
                    : OctoberRevocationStatus.Denied;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return OctoberRevocationStatus.Cancelled;
        }
        catch (OperationCanceledException)
        {
            return OctoberRevocationStatus.Unavailable;
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException or TimeoutException)
        {
            return OctoberRevocationStatus.Unavailable;
        }
    }
}
