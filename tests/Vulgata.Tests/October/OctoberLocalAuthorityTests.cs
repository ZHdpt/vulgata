using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Shared;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests.October;

public sealed partial class OctoberLocalAuthorityTests
{
    private const string TargetId = "october-authentication-target";
    private const string AuthorityId = "october-authentication-authority";
    private const string SubjectId = "october-authenticated-subject";
    private const string UserName = "october.operator@example.test";
    private const string Password = "CorrectHorse!2038";

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AuthorityConfiguration_OperationTimeoutAboveMaximum_RejectsConstruction()
    {
        const string canonicalDatabaseIdentity = "sqlite://owned/october-authentication-target";
        const string connectionString = "Host=synthetic.invalid;Password=synthetic-only-not-a-credential";

        ArgumentException failure = Assert.ThrowsAny<ArgumentException>(() =>
        {
            _ = new OctoberAuthorityConfiguration(
                AuthorityId,
                TargetId,
                Guid.Parse("862de94c-c28a-4c86-9ac6-4cfca8a69c5c"),
                canonicalDatabaseIdentity,
                connectionString,
                connectionString,
                canonicalDatabaseIdentity,
                ImmutableDictionary<string, string>.Empty.Add(TargetId, canonicalDatabaseIdentity),
                TimeSpan.FromSeconds(31),
                TimeSpan.FromHours(1));
        });

        Assert.DoesNotContain("synthetic-only-not-a-credential", failure.ToString(), StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AuthorityConfiguration_ValidMaximumBounds_RetainsExplicitConnectionsAndOrdinalAllowlist()
    {
        const string canonicalDatabaseIdentity = "sqlite://owned/october-authentication-target";
        const string dataConnection = "Data Source=synthetic-october-data;Password=data-only-test-value";
        const string identityConnection = "Data Source=synthetic-october-identity;Password=identity-only-test-value";
        ImmutableDictionary<string, string> inputAllowlist =
            ImmutableDictionary.Create<string, string>(StringComparer.OrdinalIgnoreCase)
                .Add(TargetId, canonicalDatabaseIdentity)
                .Add("october-secondary-target", "sqlite://owned/october-secondary-target");

        OctoberAuthorityConfiguration configuration = new(
            AuthorityId,
            TargetId,
            Guid.Parse("862de94c-c28a-4c86-9ac6-4cfca8a69c5c"),
            canonicalDatabaseIdentity,
            dataConnection,
            identityConnection,
            "sqlite://owned/october-identity-database",
            inputAllowlist,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromHours(1));

        Assert.Equal(dataConnection, configuration.DataConnectionString);
        Assert.Equal(identityConnection, configuration.IdentityConnectionString);
        Assert.Equal("sqlite://owned/october-identity-database", configuration.IdentityCanonicalDatabaseIdentity);
        Assert.Equal(TimeSpan.FromSeconds(30), configuration.OperationTimeout);
        Assert.Equal(TimeSpan.FromHours(1), configuration.SessionLifetime);
        Assert.True(configuration.AllowedTargets.ContainsKey(TargetId));
        Assert.True(configuration.AllowedTargets.ContainsKey("october-secondary-target"));
        Assert.False(configuration.AllowedTargets.ContainsKey(TargetId.ToUpperInvariant()));
        Assert.DoesNotContain(dataConnection, configuration.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(identityConnection, configuration.ToString(), StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AuthorityConfiguration_NonExactIdentity_RejectsConstruction()
    {
        ArgumentException failure = Assert.ThrowsAny<ArgumentException>(() =>
            CreateAuthorityConfiguration(targetId: $" {TargetId}"));

        Assert.DoesNotContain(TargetId, failure.ToString(), StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("data")]
    [InlineData("identity")]
    public void AuthorityConfiguration_MissingExplicitConnection_RejectsConstruction(string connection)
    {
        ArgumentException failure = Assert.ThrowsAny<ArgumentException>(() =>
            CreateAuthorityConfiguration(
                dataConnectionString: connection == "data" ? string.Empty : "Data Source=identity-test",
                identityConnectionString: connection == "identity" ? string.Empty : "Data Source=data-test"));

        Assert.DoesNotContain("Data Source=", failure.ToString(), StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("timeout")]
    [InlineData("lifetime")]
    public void AuthorityConfiguration_NonPositiveBound_RejectsConstruction(string bound)
    {
        ArgumentException failure = Assert.ThrowsAny<ArgumentException>(() =>
            CreateAuthorityConfiguration(
                operationTimeout: bound == "timeout" ? TimeSpan.Zero : TimeSpan.FromSeconds(30),
                sessionLifetime: bound == "lifetime" ? TimeSpan.Zero : TimeSpan.FromHours(1)));

        Assert.DoesNotContain("synthetic", failure.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AuthorityConfiguration_SessionLifetimeAboveMaximum_RejectsConstruction()
    {
        ArgumentException failure = Assert.ThrowsAny<ArgumentException>(() =>
            CreateAuthorityConfiguration(sessionLifetime: TimeSpan.FromMinutes(61)));

        Assert.DoesNotContain("synthetic", failure.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AuthorityConfiguration_EmptyBatchIdentity_RejectsConstruction()
    {
        ArgumentException failure = Assert.ThrowsAny<ArgumentException>(() =>
            CreateAuthorityConfiguration(batchId: Guid.Empty));

        Assert.DoesNotContain("synthetic", failure.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AuthorityConfiguration_AllowlistWithoutExactSelectedBinding_RejectsConstruction()
    {
        ImmutableDictionary<string, string> allowlist = ImmutableDictionary<string, string>.Empty
            .Add(TargetId.ToUpperInvariant(), "sqlite://owned/october-authentication-target");

        ArgumentException failure = Assert.ThrowsAny<ArgumentException>(() =>
            CreateAuthorityConfiguration(allowedTargets: allowlist));

        Assert.DoesNotContain("sqlite://", failure.ToString(), StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthenticateAsync_ValidIdentityPassword_ReturnsOpaqueSessionForAuthenticatedUser()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        OctoberOperatorCredentials suppliedCredentials = new(UserName, Password);
        ControlledCredentialSource credentialSource = new(suppliedCredentials);
        await using ServiceProvider provider = CreateIdentityProvider(
            connection,
            credentialSource,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero)));
        await SeedUserAsync(provider);

        await using AsyncServiceScope operationScope = provider.CreateAsyncScope();
        IOctoberLocalAuthority authority = operationScope.ServiceProvider.GetRequiredService<IOctoberLocalAuthority>();
        OctoberAuthenticationResult result = await authority.AuthenticateAsync(CancellationToken.None);

        Assert.Equal(OctoberAuthenticationStatus.Authenticated, result.Status);
        await using IOctoberOperatorSession session =
            Assert.IsAssignableFrom<IOctoberOperatorSession>(result.Session);
        Assert.Equal(SubjectId, session.SubjectId);
        Assert.Equal(AuthorityId, session.ManagementAuthorityId);
        Assert.Equal(new DateTimeOffset(2026, 9, 30, 13, 0, 0, TimeSpan.Zero), session.ExpiresAtUtc);
        Assert.Throws<ObjectDisposedException>(() => suppliedCredentials.Password);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(UserName, "IncorrectHorse!2038")]
    [InlineData("unknown.october.operator@example.test", Password)]
    public async Task AuthenticateAsync_UnknownUserOrWrongPassword_ReturnsDeniedWithoutSession(
        string userName,
        string password)
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        ControlledCredentialSource credentialSource = new(new OctoberOperatorCredentials(UserName, Password));
        await using ServiceProvider provider = CreateIdentityProvider(
            connection,
            credentialSource,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero)));
        await SeedUserAsync(provider);
        credentialSource.Credentials = new OctoberOperatorCredentials(userName, password);

        await using AsyncServiceScope operationScope = provider.CreateAsyncScope();
        IOctoberLocalAuthority authority = operationScope.ServiceProvider.GetRequiredService<IOctoberLocalAuthority>();
        OctoberAuthenticationResult result = await authority.AuthenticateAsync(CancellationToken.None);

        Assert.Equal(OctoberAuthenticationStatus.Denied, result.Status);
        Assert.Null(result.Session);

        await using AsyncServiceScope verificationScope = provider.CreateAsyncScope();
        UserManager<ApplicationUser> users = verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser storedUser = Assert.IsAssignableFrom<ApplicationUser>(await users.FindByIdAsync(SubjectId));
        Assert.Equal(0, await users.GetAccessFailedCountAsync(storedUser));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthenticateAsync_LockedIdentityUser_ReturnsDeniedWithoutSession()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        ControlledCredentialSource credentialSource = new(new OctoberOperatorCredentials(UserName, Password));
        await using ServiceProvider provider = CreateIdentityProvider(
            connection,
            credentialSource,
            TimeProvider.System);
        await SeedUserAsync(provider);

        await using (AsyncServiceScope setupScope = provider.CreateAsyncScope())
        {
            UserManager<ApplicationUser> users = setupScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = Assert.IsAssignableFrom<ApplicationUser>(await users.FindByIdAsync(SubjectId));
            IdentityResult locked = await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(10));
            Assert.True(locked.Succeeded, string.Join("; ", locked.Errors.Select(error => error.Code)));
        }

        await using AsyncServiceScope operationScope = provider.CreateAsyncScope();
        IOctoberLocalAuthority authority = operationScope.ServiceProvider.GetRequiredService<IOctoberLocalAuthority>();
        OctoberAuthenticationResult result = await authority.AuthenticateAsync(CancellationToken.None);

        Assert.Equal(OctoberAuthenticationStatus.Denied, result.Status);
        Assert.Null(result.Session);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthenticateAsync_UnconfirmedIdentityAccount_RemainsEligibleUnderCurrentOptions()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        ControlledCredentialSource credentialSource = new(new OctoberOperatorCredentials(UserName, Password));
        await using ServiceProvider provider = CreateIdentityProvider(
            connection,
            credentialSource,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero)));
        await SeedUserAsync(provider);

        await using (AsyncServiceScope setupScope = provider.CreateAsyncScope())
        {
            UserManager<ApplicationUser> users = setupScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = Assert.IsAssignableFrom<ApplicationUser>(await users.FindByIdAsync(SubjectId));
            user.EmailConfirmed = false;
            IdentityResult updated = await users.UpdateAsync(user);
            Assert.True(updated.Succeeded, string.Join("; ", updated.Errors.Select(error => error.Code)));
        }

        await using AsyncServiceScope operationScope = provider.CreateAsyncScope();
        IOctoberLocalAuthority authority = operationScope.ServiceProvider.GetRequiredService<IOctoberLocalAuthority>();
        OctoberAuthenticationResult result = await authority.AuthenticateAsync(CancellationToken.None);

        Assert.Equal(OctoberAuthenticationStatus.Authenticated, result.Status);
        await using IOctoberOperatorSession session = Assert.IsAssignableFrom<IOctoberOperatorSession>(result.Session);
        Assert.Equal(SubjectId, session.SubjectId);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthenticateAsync_TwoFactorUserWithoutFactor_ReturnsAdditionalAuthenticationRequired()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        ControlledCredentialSource credentialSource = new(new OctoberOperatorCredentials(UserName, Password));
        await using ServiceProvider provider = CreateIdentityProvider(
            connection,
            credentialSource,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero)));
        await SeedUserAsync(provider);

        await using (AsyncServiceScope setupScope = provider.CreateAsyncScope())
        {
            UserManager<ApplicationUser> users = setupScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = Assert.IsAssignableFrom<ApplicationUser>(await users.FindByIdAsync(SubjectId));
            IdentityResult keySet = await users.ResetAuthenticatorKeyAsync(user);
            Assert.True(keySet.Succeeded, string.Join("; ", keySet.Errors.Select(error => error.Code)));
            IdentityResult enabled = await users.SetTwoFactorEnabledAsync(user, true);
            Assert.True(enabled.Succeeded, string.Join("; ", enabled.Errors.Select(error => error.Code)));
        }

        await using AsyncServiceScope operationScope = provider.CreateAsyncScope();
        IOctoberLocalAuthority authority = operationScope.ServiceProvider.GetRequiredService<IOctoberLocalAuthority>();
        OctoberAuthenticationResult result = await authority.AuthenticateAsync(CancellationToken.None);

        Assert.Equal(OctoberAuthenticationStatus.AdditionalAuthenticationRequired, result.Status);
        Assert.Null(result.Session);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthenticateAsync_InvalidRecoveryCode_DeniesAndPersistsFailedAttempt()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        ControlledCredentialSource credentialSource = new(new OctoberOperatorCredentials(UserName, Password));
        await using ServiceProvider provider = CreateIdentityProvider(
            connection,
            credentialSource,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero)));
        await SeedUserAsync(provider);

        await using (AsyncServiceScope setupScope = provider.CreateAsyncScope())
        {
            UserManager<ApplicationUser> users = setupScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = Assert.IsAssignableFrom<ApplicationUser>(await users.FindByIdAsync(SubjectId));
            IdentityResult enabled = await users.SetTwoFactorEnabledAsync(user, true);
            Assert.True(enabled.Succeeded, string.Join("; ", enabled.Errors.Select(error => error.Code)));
            IEnumerable<string>? recoveryCodes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, number: 1);
            Assert.Single(recoveryCodes ?? Array.Empty<string>());
            Assert.True((await users.AccessFailedAsync(user)).Succeeded);
            Assert.True((await users.AccessFailedAsync(user)).Succeeded);
        }

        credentialSource.Credentials = new OctoberOperatorCredentials(
            UserName,
            Password,
            recoveryCode: "not-a-valid-identity-recovery-code");

        await using AsyncServiceScope operationScope = provider.CreateAsyncScope();
        IOctoberLocalAuthority authority = operationScope.ServiceProvider.GetRequiredService<IOctoberLocalAuthority>();
        OctoberAuthenticationResult result = await authority.AuthenticateAsync(CancellationToken.None);

        Assert.Equal(OctoberAuthenticationStatus.Denied, result.Status);
        Assert.Null(result.Session);

        await using AsyncServiceScope verificationScope = provider.CreateAsyncScope();
        UserManager<ApplicationUser> verificationUsers =
            verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser storedUser = Assert.IsAssignableFrom<ApplicationUser>(
            await verificationUsers.FindByIdAsync(SubjectId));
        Assert.Equal(3, await verificationUsers.GetAccessFailedCountAsync(storedUser));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthenticateAsync_InvalidAuthenticatorCode_DeniesAndPersistsFailedAttempt()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        ControlledCredentialSource credentialSource = new(new OctoberOperatorCredentials(UserName, Password));
        await using ServiceProvider provider = CreateIdentityProvider(
            connection,
            credentialSource,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero)));
        await SeedUserAsync(provider);
        await using (AsyncServiceScope setupScope = provider.CreateAsyncScope())
        {
            UserManager<ApplicationUser> users = setupScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = Assert.IsAssignableFrom<ApplicationUser>(await users.FindByIdAsync(SubjectId));
            IdentityResult keyCreated = await users.ResetAuthenticatorKeyAsync(user);
            IdentityResult factorEnabled = await users.SetTwoFactorEnabledAsync(user, true);
            Assert.True(keyCreated.Succeeded && factorEnabled.Succeeded);
        }

        credentialSource.Credentials = new OctoberOperatorCredentials(
            UserName,
            Password,
            authenticatorCode: "invalid-authenticator-code");
        await using AsyncServiceScope operationScope = provider.CreateAsyncScope();
        IOctoberLocalAuthority authority = operationScope.ServiceProvider.GetRequiredService<IOctoberLocalAuthority>();
        OctoberAuthenticationResult result = await authority.AuthenticateAsync(CancellationToken.None);

        Assert.Equal(OctoberAuthenticationStatus.Denied, result.Status);
        Assert.Null(result.Session);

        await using AsyncServiceScope verificationScope = provider.CreateAsyncScope();
        UserManager<ApplicationUser> verificationUsers =
            verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser storedUser = Assert.IsAssignableFrom<ApplicationUser>(
            await verificationUsers.FindByIdAsync(SubjectId));
        Assert.Equal(1, await verificationUsers.GetAccessFailedCountAsync(storedUser));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthenticateAsync_UnsupportedAuthenticatorProvider_DeniesWithoutSession()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        ControlledCredentialSource credentialSource = new(new OctoberOperatorCredentials(UserName, Password));
        await using ServiceProvider provider = CreateIdentityProvider(
            connection,
            credentialSource,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero)));
        await SeedUserAsync(provider);
        await using (AsyncServiceScope setupScope = provider.CreateAsyncScope())
        {
            UserManager<ApplicationUser> users = setupScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = Assert.IsAssignableFrom<ApplicationUser>(await users.FindByIdAsync(SubjectId));
            IdentityResult factorEnabled = await users.SetTwoFactorEnabledAsync(user, true);
            Assert.True(factorEnabled.Succeeded);
            users.Options.Tokens.AuthenticatorTokenProvider = "unregistered-october-authenticator";
        }

        credentialSource.Credentials = new OctoberOperatorCredentials(
            UserName,
            Password,
            authenticatorCode: "synthetic-authenticator-code");
        await using AsyncServiceScope operationScope = provider.CreateAsyncScope();
        IOctoberLocalAuthority authority = operationScope.ServiceProvider.GetRequiredService<IOctoberLocalAuthority>();
        OctoberAuthenticationResult result = await authority.AuthenticateAsync(CancellationToken.None);

        Assert.Equal(OctoberAuthenticationStatus.Denied, result.Status);
        Assert.Null(result.Session);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthenticateAsync_UnsupportedTwoFactorStoreCapability_DeniesWithoutSession()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        ControlledCredentialSource credentialSource = new(new OctoberOperatorCredentials(UserName, Password));
        await using ServiceProvider provider = CreateAuthorityProvider(
            connection,
            credentialSource,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero)),
            supportsTwoFactorStore: false);
        await SeedUserAsync(provider);

        await using AsyncServiceScope operationScope = provider.CreateAsyncScope();
        IOctoberLocalAuthority authority = operationScope.ServiceProvider
            .GetRequiredService<IOctoberLocalAuthority>();
        OctoberAuthenticationResult result = await authority.AuthenticateAsync(CancellationToken.None);

        Assert.Equal(OctoberAuthenticationStatus.Denied, result.Status);
        Assert.Null(result.Session);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthenticateAsync_IdentityCommandsRespectShorterOperationTimeout()
    {
        TimeSpan operationTimeout = TimeSpan.FromSeconds(2);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        ControlledCredentialSource credentialSource = new(new OctoberOperatorCredentials(UserName, Password));
        IdentityCommandTimeoutObserver observer = new();
        await using ServiceProvider provider = CreateAuthorityProvider(
            connection,
            credentialSource,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero)),
            operationTimeout: operationTimeout,
            identityCommandInterceptor: observer);
        await SeedUserAsync(provider);
        observer.StartObserving();

        await using AsyncServiceScope operationScope = provider.CreateAsyncScope();
        IOctoberLocalAuthority authority = operationScope.ServiceProvider
            .GetRequiredService<IOctoberLocalAuthority>();
        OctoberAuthenticationResult result = await authority.AuthenticateAsync(CancellationToken.None);

        Assert.Equal(OctoberAuthenticationStatus.Authenticated, result.Status);
        int[] observedTimeouts = observer.ObservedTimeouts;
        Assert.NotEmpty(observedTimeouts);
        Assert.All(observedTimeouts, commandTimeout =>
            Assert.True(commandTimeout > 0 && commandTimeout <= operationTimeout.TotalSeconds,
                $"Identity EF command timeout {commandTimeout}s exceeded the {operationTimeout.TotalSeconds}s operation bound."));
        await Assert.IsAssignableFrom<IOctoberOperatorSession>(result.Session).DisposeAsync();
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthenticateAsync_CancellationDuringIdentityCommandAwaitsOwnedWork()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        ControlledCredentialSource credentialSource = new(new OctoberOperatorCredentials(UserName, Password));
        IdentityCommandCancellationBarrier barrier = new();
        await using ServiceProvider provider = CreateAuthorityProvider(
            connection,
            credentialSource,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero)),
            identityCommandInterceptor: barrier);
        await SeedUserAsync(provider);
        barrier.StartBlocking();

        await using AsyncServiceScope operationScope = provider.CreateAsyncScope();
        IOctoberLocalAuthority authority = operationScope.ServiceProvider
            .GetRequiredService<IOctoberLocalAuthority>();
        using CancellationTokenSource cancellation = new();
        Task<OctoberAuthenticationResult> authentication = authority.AuthenticateAsync(cancellation.Token);
        bool commandEntered = false;
        bool completedBeforeCommandRelease = false;

        try
        {
            await barrier.CommandEntered.WaitAsync(TimeSpan.FromSeconds(15));
            commandEntered = true;
            cancellation.Cancel();
            completedBeforeCommandRelease = authentication.IsCompleted;
        }
        catch (TimeoutException)
        {
            cancellation.Cancel();
        }
        finally
        {
            barrier.Release();
        }

        OctoberAuthenticationResult result = await authentication.WaitAsync(TimeSpan.FromSeconds(15));
        if (result.Session is not null)
            await result.Session.DisposeAsync();

        Assert.True(commandEntered, "Authentication should reach an Identity EF command after valid credentials.");
        Assert.False(completedBeforeCommandRelease,
            "Authentication must await the in-flight Identity command while its operation scope remains owned.");
        Assert.Equal(OctoberAuthenticationStatus.Cancelled, result.Status);
        Assert.Null(result.Session);
        Assert.Equal(1, barrier.BlockedCommandCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthenticateAsync_CancelledMissingAndUnavailableInput_ReturnsClosedResults()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        ControlledCredentialSource credentialSource = new(new OctoberOperatorCredentials(UserName, Password));
        await using ServiceProvider provider = CreateIdentityProvider(
            connection,
            credentialSource,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero)));
        await using AsyncServiceScope operationScope = provider.CreateAsyncScope();
        IOctoberLocalAuthority authority = operationScope.ServiceProvider.GetRequiredService<IOctoberLocalAuthority>();

        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        OctoberAuthenticationResult cancellation = await authority.AuthenticateAsync(cancelled.Token);
        Assert.Equal(OctoberAuthenticationStatus.Cancelled, cancellation.Status);
        Assert.Null(cancellation.Session);
        Assert.Equal(0, credentialSource.ReadCount);

        credentialSource.Credentials = null;
        OctoberAuthenticationResult missing = await authority.AuthenticateAsync(CancellationToken.None);
        Assert.Equal(OctoberAuthenticationStatus.Denied, missing.Status);
        Assert.Null(missing.Session);

        credentialSource.Credentials = new OctoberOperatorCredentials(UserName, Password);
        credentialSource.CancelRead = true;
        OctoberAuthenticationResult unavailable = await authority.AuthenticateAsync(CancellationToken.None);
        Assert.Equal(OctoberAuthenticationStatus.Unavailable, unavailable.Status);
        Assert.Null(unavailable.Session);
        Assert.Equal(2, credentialSource.ReadCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthenticateAsync_CredentialInputUnavailable_ReturnsUnavailableWithoutSession()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        ControlledCredentialSource credentialSource = new(new OctoberOperatorCredentials(UserName, Password))
        {
            ReadFailure = new IOException("Test-owned operator credential input is unavailable."),
        };
        await using ServiceProvider provider = CreateIdentityProvider(
            connection,
            credentialSource,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero)));
        await using AsyncServiceScope operationScope = provider.CreateAsyncScope();
        IOctoberLocalAuthority authority = operationScope.ServiceProvider.GetRequiredService<IOctoberLocalAuthority>();

        OctoberAuthenticationResult result = await authority.AuthenticateAsync(CancellationToken.None);

        Assert.Equal(OctoberAuthenticationStatus.Unavailable, result.Status);
        Assert.Null(result.Session);
        Assert.Equal(1, credentialSource.ReadCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthenticateAsync_UnavailableIdentityStore_ReturnsUnavailableWithoutSession()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        ControlledCredentialSource credentialSource = new(new OctoberOperatorCredentials(UserName, Password));
        await using ServiceProvider provider = CreateIdentityProvider(
            connection,
            credentialSource,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero)));
        await SeedUserAsync(provider);

        await using (AsyncServiceScope storeScope = provider.CreateAsyncScope())
        {
            ApplicationDbContext db = storeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.ExecuteSqlRawAsync("DROP TABLE AspNetUsers;");
        }

        await using AsyncServiceScope operationScope = provider.CreateAsyncScope();
        IOctoberLocalAuthority authority = operationScope.ServiceProvider.GetRequiredService<IOctoberLocalAuthority>();
        OctoberAuthenticationResult result = await authority.AuthenticateAsync(CancellationToken.None);

        Assert.Equal(OctoberAuthenticationStatus.Unavailable, result.Status);
        Assert.Null(result.Session);
        Assert.Equal(1, credentialSource.ReadCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthenticateAsync_ValidPasswordAndRecoveryCode_ReturnsSessionForTwoFactorUser()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        ControlledCredentialSource credentialSource = new(new OctoberOperatorCredentials(UserName, Password));
        await using ServiceProvider provider = CreateIdentityProvider(
            connection,
            credentialSource,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero)));
        await SeedUserAsync(provider);
        string recoveryCode;
        await using (AsyncServiceScope seedScope = provider.CreateAsyncScope())
        {
            UserManager<ApplicationUser> users = seedScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = Assert.IsAssignableFrom<ApplicationUser>(await users.FindByIdAsync(SubjectId));

            IdentityResult enabled = await users.SetTwoFactorEnabledAsync(user, true);
            Assert.True(enabled.Succeeded, string.Join("; ", enabled.Errors.Select(error => error.Code)));

            IdentityResult firstFailure = await users.AccessFailedAsync(user);
            IdentityResult secondFailure = await users.AccessFailedAsync(user);
            Assert.True(firstFailure.Succeeded && secondFailure.Succeeded);

            IEnumerable<string>? generatedCodes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, number: 1);
            recoveryCode = Assert.Single(generatedCodes ?? Array.Empty<string>());
        }

        credentialSource.Credentials = new OctoberOperatorCredentials(UserName, Password, recoveryCode: recoveryCode);

        await using AsyncServiceScope operationScope = provider.CreateAsyncScope();
        IOctoberLocalAuthority authority = operationScope.ServiceProvider.GetRequiredService<IOctoberLocalAuthority>();
        OctoberAuthenticationResult result = await authority.AuthenticateAsync(CancellationToken.None);

        Assert.Equal(OctoberAuthenticationStatus.Authenticated, result.Status);
        await using IOctoberOperatorSession session =
            Assert.IsAssignableFrom<IOctoberOperatorSession>(result.Session);
        Assert.Equal(SubjectId, session.SubjectId);
        Assert.Equal(AuthorityId, session.ManagementAuthorityId);
        Assert.Equal(new DateTimeOffset(2026, 9, 30, 13, 0, 0, TimeSpan.Zero), session.ExpiresAtUtc);

        await using AsyncServiceScope verificationScope = provider.CreateAsyncScope();
        UserManager<ApplicationUser> verificationUsers =
            verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser storedUser = Assert.IsAssignableFrom<ApplicationUser>(
            await verificationUsers.FindByIdAsync(SubjectId));
        Assert.Equal(0, await verificationUsers.GetAccessFailedCountAsync(storedUser));
        IdentityResult reusedCode = await verificationUsers.RedeemTwoFactorRecoveryCodeAsync(storedUser, recoveryCode);
        Assert.False(reusedCode.Succeeded);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthenticateAsync_ValidPasswordAndAuthenticatorCode_ReturnsSessionForTwoFactorUser()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        ControlledCredentialSource credentialSource = new(new OctoberOperatorCredentials(UserName, Password));
        TimeProvider clock = TimeProvider.System;
        await using ServiceProvider provider = CreateIdentityProvider(connection, credentialSource, clock);
        await SeedUserAsync(provider);
        string authenticatorCode;
        await using (AsyncServiceScope seedScope = provider.CreateAsyncScope())
        {
            UserManager<ApplicationUser> users = seedScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = Assert.IsAssignableFrom<ApplicationUser>(await users.FindByIdAsync(SubjectId));

            IdentityResult keySet = await users.ResetAuthenticatorKeyAsync(user);
            Assert.True(keySet.Succeeded, string.Join("; ", keySet.Errors.Select(error => error.Code)));

            IdentityResult enabled = await users.SetTwoFactorEnabledAsync(user, true);
            Assert.True(enabled.Succeeded, string.Join("; ", enabled.Errors.Select(error => error.Code)));

            string? authenticatorKey = await users.GetAuthenticatorKeyAsync(user);
            Assert.False(string.IsNullOrWhiteSpace(authenticatorKey), "Identity must persist the generated authenticator key.");
            string providerName = users.Options.Tokens.AuthenticatorTokenProvider;
            authenticatorCode = CreateAuthenticatorCode(authenticatorKey, clock.GetUtcNow());
            Assert.True(
                await users.VerifyTwoFactorTokenAsync(user, providerName, authenticatorCode),
                "The configured Identity authenticator provider must recognize its freshly generated code.");
        }

        credentialSource.Credentials = new OctoberOperatorCredentials(
            UserName,
            Password,
            authenticatorCode: authenticatorCode);

        await using AsyncServiceScope operationScope = provider.CreateAsyncScope();
        IOctoberLocalAuthority authority = operationScope.ServiceProvider.GetRequiredService<IOctoberLocalAuthority>();
        OctoberAuthenticationResult result = await authority.AuthenticateAsync(CancellationToken.None);

        Assert.Equal(OctoberAuthenticationStatus.Authenticated, result.Status);
        await using IOctoberOperatorSession session =
            Assert.IsAssignableFrom<IOctoberOperatorSession>(result.Session);
        Assert.Equal(SubjectId, session.SubjectId);
        Assert.Equal(AuthorityId, session.ManagementAuthorityId);
        Assert.InRange(
            session.ExpiresAtUtc,
            clock.GetUtcNow().AddMinutes(59),
            clock.GetUtcNow().AddHours(1));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthorizeSelectionAsync_PreparesOwnedSystemSelectionWithTrustedContext()
    {
        const string globalContextValue = "Owned global advisory context.";
        const string systemContextValue = "Owned System advisory context.";
        const string repositoryContextValue = "Owned Repository advisory context.";
        const string rootBindingIdentity = "prepared-root-v3";
        const string operatorLabel = "October test Repository";
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        Guid batchId = Guid.Parse("862de94c-c28a-4c86-9ac6-4cfca8a69c5c");
        Guid systemId = fixture.SystemId!.Value;
        Guid repositoryId = fixture.RepositoryId;
        Guid globalContextId = fixture.GlobalContextId;
        OctoberRepositorySelection selected = new(repositoryId, systemId, rootBindingIdentity, operatorLabel);
        OctoberAuthoritySelectionRequest request = fixture.CreateRequest();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        Assert.Equal(SubjectId, session.SubjectId);

        OctoberAuthoritySelectionResult result = await fixture.Authority.AuthorizeSelectionAsync(
            session, request, CancellationToken.None);

        Assert.Equal(OctoberAuthorityDecisionStatus.Authorized, result.Status);
        OctoberSetupRequest authorizedRequest = Assert.IsAssignableFrom<OctoberSetupRequest>(result.Request);
        OctoberAuthorizedSelection authorized = Assert.IsAssignableFrom<OctoberAuthorizedSelection>(result.Authority);
        Assert.Equal(TargetId, authorizedRequest.TargetId);
        Assert.Equal(batchId, authorizedRequest.BatchId);
        Assert.Equal(new[] { selected }, authorizedRequest.Repositories);
        Assert.Equal(new[] { repositoryId }, authorized.RepositoryIds);
        Assert.Equal(SubjectId, authorized.SubjectId);
        Assert.Equal("sqlite://owned/october-authentication-target", authorized.CanonicalDatabaseIdentity);
        Assert.Equal(authorizedRequest.SelectionDigest, authorized.SelectionDigest);
        Assert.Matches("^[0-9a-f]{64}$", authorizedRequest.SelectionDigest);

        OctoberSelectedAdvisoryContext context = Assert.IsType<OctoberSelectedAdvisoryContext>(
            authorized.SelectedContext);
        Assert.Equal(AuthorityId, context.ManagementAuthorityId);
        Assert.Single(context.Repositories);
        Assert.Equal(selected, Assert.Single(context.Repositories));
        Assert.Equal(3, context.Entries.Length);
        Assert.Contains(context.Entries, entry => entry.RepositoryId == repositoryId &&
            entry.Scope == OctoberContextScope.Global && entry.Applicability == OctoberContextApplicability.Selected &&
            entry.OriginRecordId == globalContextId && entry.Value == globalContextValue &&
            entry.EvidenceKind == OctoberContextEvidenceKind.AdvisoryNotSourceProof);
        Assert.Contains(context.Entries, entry => entry.RepositoryId == repositoryId &&
            entry.Scope == OctoberContextScope.System && entry.Applicability == OctoberContextApplicability.Selected &&
            entry.OriginRecordId == systemId && entry.Value == systemContextValue &&
            entry.EvidenceKind == OctoberContextEvidenceKind.AdvisoryNotSourceProof);
        Assert.Contains(context.Entries, entry => entry.RepositoryId == repositoryId &&
            entry.Scope == OctoberContextScope.Repository && entry.Applicability == OctoberContextApplicability.Selected &&
            entry.OriginRecordId == repositoryId && entry.Value == repositoryContextValue &&
            entry.EvidenceKind == OctoberContextEvidenceKind.AdvisoryNotSourceProof);
        Assert.Equal(3, context.Differences.Length);
        Assert.Contains(new OctoberContextDifference(
            repositoryId, OctoberContextScope.Global, OctoberContextScope.System), context.Differences);
        Assert.Contains(new OctoberContextDifference(
            repositoryId, OctoberContextScope.Global, OctoberContextScope.Repository), context.Differences);
        Assert.Contains(new OctoberContextDifference(
            repositoryId, OctoberContextScope.System, OctoberContextScope.Repository), context.Differences);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthorizeSelectionAsync_SelectsAnExistingExactBatchWithCurrentAuthority()
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();

        OctoberAuthoritySelectionResult prepared = await fixture.Authority.AuthorizeSelectionAsync(
            session, fixture.CreateRequest(), CancellationToken.None);
        Assert.Equal(OctoberAuthorityDecisionStatus.Authorized, prepared.Status);
        OctoberSetupRequest preparedRequest = Assert.IsAssignableFrom<OctoberSetupRequest>(prepared.Request);
        OctoberAuthorizedSelection preparedAuthority =
            Assert.IsAssignableFrom<OctoberAuthorizedSelection>(prepared.Authority);

        OctoberPrepareResult committed;
        await using (AsyncServiceScope writerScope = fixture.Provider.CreateAsyncScope())
        {
            IOctoberSetupWriter writer = writerScope.ServiceProvider.GetRequiredService<IOctoberSetupWriter>();
            committed = await new OctoberBatchSetupPolicy(writer).PrepareAsync(
                preparedRequest, preparedAuthority, CancellationToken.None);
        }

        Assert.Equal(OctoberPrepareStatus.Created, committed.Status);
        OctoberCommittedBatch committedBatch = Assert.IsAssignableFrom<OctoberCommittedBatch>(
            committed.CommittedBatch);
        Assert.Equal(TargetId, committedBatch.TargetId);
        Assert.Equal(preparedRequest.BatchId, committedBatch.BatchId);
        Assert.Equal(preparedRequest.SelectionDigest, committedBatch.SelectionDigest);

        OctoberAuthoritySelectionResult selected = await fixture.Authority.AuthorizeSelectionAsync(
            session,
            new OctoberAuthoritySelectionRequest(TargetId, preparedRequest.BatchId, PrepareRequest: null),
            CancellationToken.None);

        Assert.Equal(OctoberAuthorityDecisionStatus.Authorized, selected.Status);
        OctoberSetupRequest selectedRequest = Assert.IsAssignableFrom<OctoberSetupRequest>(selected.Request);
        OctoberAuthorizedSelection selectedAuthority =
            Assert.IsAssignableFrom<OctoberAuthorizedSelection>(selected.Authority);
        Assert.Equal(preparedRequest.TargetId, selectedRequest.TargetId);
        Assert.Equal(preparedRequest.BatchId, selectedRequest.BatchId);
        Assert.Equal(preparedRequest.SelectionDigest, selectedRequest.SelectionDigest);
        Assert.Equal(committedBatch.CanonicalDatabaseIdentity, selectedAuthority.CanonicalDatabaseIdentity);
        Assert.Equal(committedBatch.SelectionDigest, selectedAuthority.SelectionDigest);
        Assert.Equal(SubjectId, selectedAuthority.SubjectId);
        Assert.Equal(preparedRequest.Repositories, selectedRequest.Repositories);
        OctoberSelectedAdvisoryContext preparedContext =
            Assert.IsType<OctoberSelectedAdvisoryContext>(preparedAuthority.SelectedContext);
        OctoberSelectedAdvisoryContext selectedContext =
            Assert.IsType<OctoberSelectedAdvisoryContext>(selectedAuthority.SelectedContext);
        Assert.Equal(preparedContext.ManagementAuthorityId, selectedContext.ManagementAuthorityId);
        Assert.Equal(preparedContext.Repositories.ToArray(), selectedContext.Repositories.ToArray());
        Assert.Equal(preparedContext.Entries.ToArray(), selectedContext.Entries.ToArray());
        Assert.Equal(preparedContext.Differences.ToArray(), selectedContext.Differences.ToArray());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthorizeSelectionAsync_DistinguishesMissingConfiguredBatchFromAnotherBatch()
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        OctoberAuthoritySelectionRequest configuredBatch = new(
            TargetId, Guid.Parse("862de94c-c28a-4c86-9ac6-4cfca8a69c5c"), PrepareRequest: null);

        OctoberAuthoritySelectionResult missing = await fixture.Authority.AuthorizeSelectionAsync(
            session, configuredBatch, CancellationToken.None);
        Assert.Equal(OctoberAuthorityDecisionStatus.MissingBatch, missing.Status);
        Assert.Null(missing.Request);
        Assert.Null(missing.Authority);

        await SeedSelectableBatchUnderAuthorityAsync(
            fixture,
            AuthorityId,
            Guid.Parse("cdbeb7c2-82df-4aa4-bd71-5e76b83f8a02"));

        OctoberAuthoritySelectionResult mismatch = await fixture.Authority.AuthorizeSelectionAsync(
            session, configuredBatch, CancellationToken.None);
        Assert.Equal(OctoberAuthorityDecisionStatus.BatchMismatch, mismatch.Status);
        Assert.Null(mismatch.Request);
        Assert.Null(mismatch.Authority);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthorizeSelectionAsync_DeniesBatchWrittenUnderCollidingForeignAuthority()
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        await SeedSelectableBatchUnderAuthorityAsync(
            fixture,
            "october-foreign-management-authority",
            Guid.Parse("862de94c-c28a-4c86-9ac6-4cfca8a69c5c"));

        OctoberAuthoritySelectionResult result = await fixture.Authority.AuthorizeSelectionAsync(
            session,
            new OctoberAuthoritySelectionRequest(
                TargetId, Guid.Parse("862de94c-c28a-4c86-9ac6-4cfca8a69c5c"), PrepareRequest: null),
            CancellationToken.None);

        AssertDeniedSelection(result);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthorizeSelectionAsync_DeniesMalformedStoredSelectionMetadataWithoutPayload()
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        await SeedSelectableBatchUnderAuthorityAsync(
            fixture,
            AuthorityId,
            Guid.Parse("862de94c-c28a-4c86-9ac6-4cfca8a69c5c"));
        OctoberAuthoritySelectionRequest request = new(
            TargetId, Guid.Parse("862de94c-c28a-4c86-9ac6-4cfca8a69c5c"), PrepareRequest: null);

        await using AsyncServiceScope storageScope = fixture.Provider.CreateAsyncScope();
        OctoberDbContext october = storageScope.ServiceProvider.GetRequiredService<OctoberDbContext>();
        OctoberBatchRecord batch = await october.Batches.SingleAsync(value => value.BatchId == request.BatchId);

        batch.TargetId = TargetId + "-other";
        await october.SaveChangesAsync();
        AssertDeniedSelection(await fixture.Authority.AuthorizeSelectionAsync(
            session, request, CancellationToken.None));
        batch.TargetId = TargetId;

        batch.SubjectId = SubjectId + "-other";
        await october.SaveChangesAsync();
        AssertDeniedSelection(await fixture.Authority.AuthorizeSelectionAsync(
            session, request, CancellationToken.None));
        batch.SubjectId = SubjectId;

        OctoberRepositoryRecord repository = await october.RepositorySelections
            .SingleAsync(value => value.BatchId == request.BatchId);
        Guid originalSystemId = Assert.IsType<Guid>(repository.SystemId);
        repository.SystemId = Guid.NewGuid();
        await october.SaveChangesAsync();
        AssertDeniedSelection(await fixture.Authority.AuthorizeSelectionAsync(
            session, request, CancellationToken.None));
        repository.SystemId = originalSystemId;

        OctoberContextReferenceRecord globalReference = await october.ContextReferences
            .SingleAsync(value => value.BatchId == request.BatchId &&
                value.Scope == (int)OctoberContextScope.Global);
        Guid originalGlobalContextId = globalReference.RecordId;
        october.ContextReferences.Remove(globalReference);
        october.ContextReferences.Add(new OctoberContextReferenceRecord
        {
            BatchId = request.BatchId,
            Scope = (int)OctoberContextScope.Global,
            RecordId = Guid.NewGuid()
        });
        await october.SaveChangesAsync();
        AssertDeniedSelection(await fixture.Authority.AuthorizeSelectionAsync(
            session, request, CancellationToken.None));
        october.ContextReferences.Remove(await october.ContextReferences.SingleAsync(value =>
            value.BatchId == request.BatchId && value.Scope == (int)OctoberContextScope.Global));
        october.ContextReferences.Add(new OctoberContextReferenceRecord
        {
            BatchId = request.BatchId,
            Scope = (int)OctoberContextScope.Global,
            RecordId = originalGlobalContextId
        });
        await october.SaveChangesAsync();

        OctoberAuthoritySelectionResult restored = await fixture.Authority.AuthorizeSelectionAsync(
            session, request, CancellationToken.None);
        Assert.Equal(OctoberAuthorityDecisionStatus.Authorized, restored.Status);
        Assert.NotNull(restored.Request);
        Assert.NotNull(restored.Authority);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthorizeSelectionAsync_ReportsSelectCancellationAndOctoberStoreOutageWithoutPayload()
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        OctoberAuthoritySelectionRequest request = new(
            TargetId, Guid.Parse("862de94c-c28a-4c86-9ac6-4cfca8a69c5c"), PrepareRequest: null);

        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        OctoberAuthoritySelectionResult cancellation = await fixture.Authority.AuthorizeSelectionAsync(
            session, request, cancelled.Token);
        Assert.Equal(OctoberAuthorityDecisionStatus.Cancelled, cancellation.Status);
        Assert.Null(cancellation.Request);
        Assert.Null(cancellation.Authority);

        await using AsyncServiceScope storageScope = fixture.Provider.CreateAsyncScope();
        OctoberDbContext october = storageScope.ServiceProvider.GetRequiredService<OctoberDbContext>();
        await october.Database.ExecuteSqlRawAsync("DROP TABLE OctoberBatches;");

        OctoberAuthoritySelectionResult unavailable = await fixture.Authority.AuthorizeSelectionAsync(
            session, request, CancellationToken.None);
        Assert.Equal(OctoberAuthorityDecisionStatus.Unavailable, unavailable.Status);
        Assert.Null(unavailable.Request);
        Assert.Null(unavailable.Authority);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthorizeSelectionAsync_RejectsFabricatedForeignDisposedAndExpiredSessions()
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        OctoberAuthoritySelectionRequest request = fixture.CreateRequest();

        OctoberAuthoritySelectionResult fabricated = await fixture.Authority.AuthorizeSelectionAsync(
            new UntrustedOperatorSession(SubjectId, AuthorityId, fixture.Clock.GetUtcNow().AddHours(1)),
            request,
            CancellationToken.None);
        AssertDeniedSelection(fabricated);

        await using IOctoberOperatorSession foreignSession = await fixture.AuthenticateAsync();
        await using (AsyncServiceScope otherScope = fixture.Provider.CreateAsyncScope())
        {
            IOctoberLocalAuthority otherAuthority = otherScope.ServiceProvider
                .GetRequiredService<IOctoberLocalAuthority>();
            OctoberAuthoritySelectionResult foreign = await otherAuthority.AuthorizeSelectionAsync(
                foreignSession, request, CancellationToken.None);
            AssertDeniedSelection(foreign);
        }

        OctoberAuthoritySelectionResult current = await fixture.Authority.AuthorizeSelectionAsync(
            foreignSession, request, CancellationToken.None);
        Assert.Equal(OctoberAuthorityDecisionStatus.Authorized, current.Status);

        await foreignSession.DisposeAsync();
        OctoberAuthoritySelectionResult disposed = await fixture.Authority.AuthorizeSelectionAsync(
            foreignSession, request, CancellationToken.None);
        AssertDeniedSelection(disposed);

        await using IOctoberOperatorSession expiredSession = await fixture.AuthenticateAsync();
        fixture.Clock.Advance(TimeSpan.FromHours(1));
        OctoberAuthoritySelectionResult expired = await fixture.Authority.AuthorizeSelectionAsync(
            expiredSession, request, CancellationToken.None);
        AssertDeniedSelection(expired);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthorizeSelectionAsync_DeniesAfterManagementRoleIsRemoved()
    {
        await AssertIdentityChangeDeniesSelectionAsync(async (users, user) =>
        {
            IdentityResult removed = await users.RemoveFromRoleAsync(user, RoleNames.SystemOwner);
            Assert.True(removed.Succeeded, string.Join("; ", removed.Errors.Select(error => error.Code)));
        });
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthorizeSelectionAsync_DeniesAfterSecurityStampChanges()
    {
        await AssertIdentityChangeDeniesSelectionAsync(async (users, user) =>
        {
            IdentityResult changed = await users.UpdateSecurityStampAsync(user);
            Assert.True(changed.Succeeded, string.Join("; ", changed.Errors.Select(error => error.Code)));
        });
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthorizeSelectionAsync_DeniesWhenIdentityAccountBecomesLockedOut()
    {
        await AssertIdentityChangeDeniesSelectionAsync(async (users, user) =>
        {
            IdentityResult locked = await users.SetLockoutEndDateAsync(
                user, DateTimeOffset.UtcNow.AddMinutes(10));
            Assert.True(locked.Succeeded, string.Join("; ", locked.Errors.Select(error => error.Code)));
            Assert.True(await users.IsLockedOutAsync(user),
                "The selection-denial check requires a currently locked Identity account.");
        });
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthorizeSelectionAsync_LatchesClosedWhenSystemOwnershipIsRevoked()
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        await using (VulgataDbContext management = await fixture.CreateManagementContextAsync())
        {
            SystemOwnerAssignment assignment = await management.SystemOwnerAssignments
                .SingleAsync(value => value.SystemId == fixture.SystemId && value.UserId == SubjectId);
            management.SystemOwnerAssignments.Remove(assignment);
            await management.SaveChangesAsync();
        }

        OctoberAuthoritySelectionResult revoked = await fixture.Authority.AuthorizeSelectionAsync(
            session, fixture.CreateRequest(), CancellationToken.None);
        AssertDeniedSelection(revoked);

        await using (VulgataDbContext management = await fixture.CreateManagementContextAsync())
        {
            management.SystemOwnerAssignments.Add(SystemOwnerAssignment.Create(
                fixture.SystemId!.Value, SubjectId, fixture.Clock.GetUtcNow()));
            await management.SaveChangesAsync();
        }

        OctoberAuthoritySelectionResult restored = await fixture.Authority.AuthorizeSelectionAsync(
            session, fixture.CreateRequest(), CancellationToken.None);
        AssertDeniedSelection(restored);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthorizeSelectionAsync_AcceptsOnlyTheCanonicalCallerDigest()
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        OctoberAuthoritySelectionRequest requestWithoutDigest = fixture.CreateRequest();
        string expectedDigest = OctoberSelectionDigest.Compute(
            requestWithoutDigest.PrepareRequest!, fixture.ExpectedFullySelectedContext());

        OctoberAuthoritySelectionResult matching = await fixture.Authority.AuthorizeSelectionAsync(
            session,
            fixture.CreateRequest(selectionDigest: expectedDigest),
            CancellationToken.None);

        Assert.Equal(OctoberAuthorityDecisionStatus.Authorized, matching.Status);
        OctoberSetupRequest canonical = Assert.IsAssignableFrom<OctoberSetupRequest>(matching.Request);
        OctoberAuthorizedSelection authority = Assert.IsAssignableFrom<OctoberAuthorizedSelection>(matching.Authority);
        Assert.Equal(expectedDigest, canonical.SelectionDigest);
        Assert.Equal(expectedDigest, authority.SelectionDigest);

        OctoberAuthoritySelectionResult inconsistent = await fixture.Authority.AuthorizeSelectionAsync(
            session,
            fixture.CreateRequest(selectionDigest: new string('0', 64)),
            CancellationToken.None);
        AssertDeniedSelection(inconsistent);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthorizeSelectionAsync_DeniesTargetBatchAndInnerRequestMismatchWithoutPayload()
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        OctoberAuthoritySelectionRequest request = fixture.CreateRequest();
        OctoberSetupRequest prepare = Assert.IsType<OctoberSetupRequest>(request.PrepareRequest);

        AssertDeniedSelection(await fixture.Authority.AuthorizeSelectionAsync(
            session, request with { TargetId = TargetId + "-unlisted" }, CancellationToken.None));
        string unlistedTarget = TargetId + "-unlisted";
        AssertDeniedSelection(await fixture.Authority.AuthorizeSelectionAsync(
            session,
            request with
            {
                TargetId = unlistedTarget,
                PrepareRequest = prepare with { TargetId = unlistedTarget }
            },
            CancellationToken.None));
        AssertDeniedSelection(await fixture.Authority.AuthorizeSelectionAsync(
            session, request with { BatchId = Guid.NewGuid() }, CancellationToken.None));
        Guid unconfiguredBatch = Guid.NewGuid();
        AssertDeniedSelection(await fixture.Authority.AuthorizeSelectionAsync(
            session,
            request with
            {
                BatchId = unconfiguredBatch,
                PrepareRequest = prepare with { BatchId = unconfiguredBatch }
            },
            CancellationToken.None));
        AssertDeniedSelection(await fixture.Authority.AuthorizeSelectionAsync(
            session,
            request with { PrepareRequest = prepare with { TargetId = TargetId + "-other" } },
            CancellationToken.None));
        AssertDeniedSelection(await fixture.Authority.AuthorizeSelectionAsync(
            session,
            request with { PrepareRequest = prepare with { BatchId = Guid.NewGuid() } },
            CancellationToken.None));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthorizeSelectionAsync_RejectsReferencesOutsideCurrentRepositoryScope()
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        OctoberAuthoritySelectionRequest request = fixture.CreateRequest();
        OctoberSetupRequest prepare = Assert.IsType<OctoberSetupRequest>(request.PrepareRequest);
        OctoberContextSelection globalReference = new(OctoberContextScope.Global, fixture.GlobalContextId);
        OctoberContextSelection systemReference = new(OctoberContextScope.System, fixture.SystemId!.Value);
        OctoberContextSelection repositoryReference =
            new(OctoberContextScope.Repository, fixture.RepositoryId);

        AssertDeniedSelection(await fixture.Authority.AuthorizeSelectionAsync(
            session,
            request with
            {
                PrepareRequest = prepare with
                {
                    ContextSelections =
                    [globalReference, new OctoberContextSelection(OctoberContextScope.System, Guid.NewGuid())]
                }
            },
            CancellationToken.None));
        AssertDeniedSelection(await fixture.Authority.AuthorizeSelectionAsync(
            session,
            request with
            {
                PrepareRequest = prepare with
                {
                    ContextSelections =
                    [globalReference, new OctoberContextSelection(OctoberContextScope.Repository, Guid.NewGuid())]
                }
            },
            CancellationToken.None));
        AssertDeniedSelection(await fixture.Authority.AuthorizeSelectionAsync(
            session,
            request with
            {
                PrepareRequest = prepare with
                {
                    ContextSelections =
                    [new OctoberContextSelection(OctoberContextScope.Global, Guid.NewGuid())]
                }
            },
            CancellationToken.None));
        AssertDeniedSelection(await fixture.Authority.AuthorizeSelectionAsync(
            session,
            request with
            {
                PrepareRequest = prepare with
                {
                    Repositories =
                    [prepare.Repositories.Single() with { SystemId = Guid.NewGuid() }]
                }
            },
            CancellationToken.None));
        AssertDeniedSelection(await fixture.Authority.AuthorizeSelectionAsync(
            session,
            request with
            {
                PrepareRequest = prepare with
                {
                    ContextSelections = [globalReference, systemReference, repositoryReference, globalReference]
                }
            },
            CancellationToken.None));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthorizeSelectionAsync_PreservesNullEmptyAndUnselectedContextProvenance()
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync(
            globalContextValue: string.Empty,
            systemContextValue: null,
            repositoryContextValue: null);
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();

        OctoberAuthoritySelectionResult globalOnly = await fixture.Authority.AuthorizeSelectionAsync(
            session,
            fixture.CreateRequest(contextSelections:
                [new OctoberContextSelection(OctoberContextScope.Global, fixture.GlobalContextId)]),
            CancellationToken.None);
        Assert.Equal(OctoberAuthorityDecisionStatus.Authorized, globalOnly.Status);
        OctoberSelectedAdvisoryContext globalContext = Assert.IsType<OctoberSelectedAdvisoryContext>(
            Assert.IsType<OctoberAuthorizedSelection>(globalOnly.Authority).SelectedContext);
        Assert.Contains(globalContext.Entries, entry => entry.Scope == OctoberContextScope.Global &&
            entry.Applicability == OctoberContextApplicability.Selected &&
            entry.OriginRecordId == fixture.GlobalContextId && entry.Value == string.Empty);
        Assert.Contains(globalContext.Entries, entry => entry.Scope == OctoberContextScope.System &&
            entry.Applicability == OctoberContextApplicability.NotSelected &&
            entry.OriginRecordId is null && entry.Value is null);
        Assert.Contains(globalContext.Entries, entry => entry.Scope == OctoberContextScope.Repository &&
            entry.Applicability == OctoberContextApplicability.NotSelected &&
            entry.OriginRecordId is null && entry.Value is null);
        Assert.Empty(globalContext.Differences);

        OctoberContextSelection[] mutableReferences =
        [
            new OctoberContextSelection(OctoberContextScope.Global, fixture.GlobalContextId),
            new OctoberContextSelection(OctoberContextScope.System, fixture.SystemId!.Value),
            new OctoberContextSelection(OctoberContextScope.Repository, fixture.RepositoryId),
        ];
        OctoberRepositorySelection[] mutableRepositories =
        [new OctoberRepositorySelection(fixture.RepositoryId, fixture.SystemId,
            "prepared-root-v3", "October test Repository")];
        OctoberSetupRequest mutablePrepare = new(
            TargetId,
            Guid.Parse("862de94c-c28a-4c86-9ac6-4cfca8a69c5c"),
            mutableRepositories,
            mutableReferences,
            string.Empty);
        OctoberAuthoritySelectionResult allScopes = await fixture.Authority.AuthorizeSelectionAsync(
            session,
            new OctoberAuthoritySelectionRequest(TargetId, mutablePrepare.BatchId, mutablePrepare),
            CancellationToken.None);

        Assert.Equal(OctoberAuthorityDecisionStatus.Authorized, allScopes.Status);
        OctoberSetupRequest copiedRequest = Assert.IsType<OctoberSetupRequest>(allScopes.Request);
        OctoberSelectedAdvisoryContext allContext = Assert.IsType<OctoberSelectedAdvisoryContext>(
            Assert.IsType<OctoberAuthorizedSelection>(allScopes.Authority).SelectedContext);
        Assert.Contains(allContext.Entries, entry => entry.Scope == OctoberContextScope.Global &&
            entry.Applicability == OctoberContextApplicability.Selected &&
            entry.OriginRecordId == fixture.GlobalContextId && entry.Value == string.Empty);
        Assert.Contains(allContext.Entries, entry => entry.Scope == OctoberContextScope.System &&
            entry.Applicability == OctoberContextApplicability.Selected &&
            entry.OriginRecordId == fixture.SystemId && entry.Value is null);
        Assert.Contains(allContext.Entries, entry => entry.Scope == OctoberContextScope.Repository &&
            entry.Applicability == OctoberContextApplicability.Selected &&
            entry.OriginRecordId == fixture.RepositoryId && entry.Value is null);
        Assert.Equal(2, allContext.Differences.Length);
        Assert.Contains(new OctoberContextDifference(
            fixture.RepositoryId, OctoberContextScope.Global, OctoberContextScope.System), allContext.Differences);
        Assert.Contains(new OctoberContextDifference(
            fixture.RepositoryId, OctoberContextScope.Global, OctoberContextScope.Repository), allContext.Differences);

        mutableReferences[0] = new OctoberContextSelection(OctoberContextScope.System, Guid.NewGuid());
        mutableRepositories[0] = mutableRepositories[0] with { OperatorLabel = "caller changed" };
        Assert.Equal(new OctoberRepositorySelection(
            fixture.RepositoryId, fixture.SystemId, "prepared-root-v3", "October test Repository"),
            Assert.Single(copiedRequest.Repositories));
        Assert.Equal(new[]
        {
            new OctoberContextSelection(OctoberContextScope.Global, fixture.GlobalContextId),
            new OctoberContextSelection(OctoberContextScope.System, fixture.SystemId.Value),
            new OctoberContextSelection(OctoberContextScope.Repository, fixture.RepositoryId),
        }, copiedRequest.ContextSelections);
        OctoberAdvisoryContextEntry copiedGlobal = Assert.Single(allContext.Entries
            .Where(entry => entry.Scope == OctoberContextScope.Global));
        Assert.Equal(fixture.GlobalContextId, copiedGlobal.OriginRecordId);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthorizeSelectionAsync_AllowsOnlyManagementAccessForStandaloneRepository()
    {
        await using (SelectionFixture allowed = await SelectionFixture.CreateAsync(standalone: true))
        {
            Assert.Null(allowed.SystemId);
            await using IOctoberOperatorSession session = await allowed.AuthenticateAsync();
            OctoberAuthoritySelectionResult result = await allowed.Authority.AuthorizeSelectionAsync(
                session, allowed.CreateRequest(), CancellationToken.None);

            Assert.Equal(OctoberAuthorityDecisionStatus.Authorized, result.Status);
            OctoberSelectedAdvisoryContext context = Assert.IsType<OctoberSelectedAdvisoryContext>(
                Assert.IsType<OctoberAuthorizedSelection>(result.Authority).SelectedContext);
            Assert.Contains(context.Entries, entry => entry.Scope == OctoberContextScope.System &&
                entry.Applicability == OctoberContextApplicability.NotApplicable &&
                entry.OriginRecordId is null && entry.Value is null);
        }

        await using SelectionFixture denied = await SelectionFixture.CreateAsync(
            standalone: true, roleName: RoleNames.User);
        await using IOctoberOperatorSession deniedSession = await denied.AuthenticateAsync();
        AssertDeniedSelection(await denied.Authority.AuthorizeSelectionAsync(
            deniedSession, denied.CreateRequest(), CancellationToken.None));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthorizeSelectionAsync_ClosesSessionWhenIdentityAccountIsDeleted()
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        string originalStamp;
        await using (AsyncServiceScope identityScope = fixture.Provider.CreateAsyncScope())
        {
            UserManager<ApplicationUser> users = identityScope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = Assert.IsAssignableFrom<ApplicationUser>(
                await users.FindByIdAsync(SubjectId));
            originalStamp = await users.GetSecurityStampAsync(user);
            IdentityResult deleted = await users.DeleteAsync(user);
            Assert.True(deleted.Succeeded, string.Join("; ", deleted.Errors.Select(error => error.Code)));
        }

        AssertDeniedSelection(await fixture.Authority.AuthorizeSelectionAsync(
            session, fixture.CreateRequest(), CancellationToken.None));

        await using (AsyncServiceScope identityScope = fixture.Provider.CreateAsyncScope())
        {
            UserManager<ApplicationUser> users = identityScope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser restored = new()
            {
                Id = SubjectId,
                UserName = UserName,
                Email = UserName,
                EmailConfirmed = true,
                LockoutEnabled = true,
            };
            IdentityResult created = await users.CreateAsync(restored, Password);
            Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(error => error.Code)));
            restored.SecurityStamp = originalStamp;
            IdentityResult stampRestored = await users.UpdateAsync(restored);
            Assert.True(stampRestored.Succeeded,
                string.Join("; ", stampRestored.Errors.Select(error => error.Code)));
            Assert.Equal(originalStamp, await users.GetSecurityStampAsync(restored));
            IdentityResult roleAdded = await users.AddToRoleAsync(restored, RoleNames.SystemOwner);
            Assert.True(roleAdded.Succeeded, string.Join("; ", roleAdded.Errors.Select(error => error.Code)));
        }

        AssertDeniedSelection(await fixture.Authority.AuthorizeSelectionAsync(
            session, fixture.CreateRequest(), CancellationToken.None));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthorizeSelectionAsync_ReportsCancellationAndManagementStoreOutageWithoutPayload()
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();

        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        OctoberAuthoritySelectionResult cancellation = await fixture.Authority.AuthorizeSelectionAsync(
            session, fixture.CreateRequest(), cancelled.Token);
        Assert.Equal(OctoberAuthorityDecisionStatus.Cancelled, cancellation.Status);
        Assert.Null(cancellation.Request);
        Assert.Null(cancellation.Authority);

        await using (VulgataDbContext management = await fixture.CreateManagementContextAsync())
        {
            await management.Database.ExecuteSqlRawAsync("DROP TABLE Repositories;");
        }

        OctoberAuthoritySelectionResult unavailable = await fixture.Authority.AuthorizeSelectionAsync(
            session, fixture.CreateRequest(), CancellationToken.None);
        Assert.Equal(OctoberAuthorityDecisionStatus.Unavailable, unavailable.Status);
        Assert.Null(unavailable.Request);
        Assert.Null(unavailable.Authority);
    }

    private static string CreateAuthenticatorCode(string authenticatorKey, DateTimeOffset now)
    {
        byte[] key = DecodeBase32(authenticatorKey);
        Span<byte> timestep = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(timestep, now.ToUnixTimeSeconds() / 30);
        byte[] hash = HMACSHA1.HashData(key, timestep);
        int offset = hash[^1] & 0x0f;
        int binaryCode = ((hash[offset] & 0x7f) << 24) |
                         (hash[offset + 1] << 16) |
                         (hash[offset + 2] << 8) |
                         hash[offset + 3];

        return (binaryCode % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static void AssertDeniedSelection(OctoberAuthoritySelectionResult result)
    {
        Assert.Equal(OctoberAuthorityDecisionStatus.Denied, result.Status);
        Assert.Null(result.Request);
        Assert.Null(result.Authority);
    }

    private static async Task AssertIdentityChangeDeniesSelectionAsync(
        Func<UserManager<ApplicationUser>, ApplicationUser, Task> change)
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        await using (AsyncServiceScope identityScope = fixture.Provider.CreateAsyncScope())
        {
            UserManager<ApplicationUser> users = identityScope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = Assert.IsAssignableFrom<ApplicationUser>(
                await users.FindByIdAsync(SubjectId));
            await change(users, user);
        }

        OctoberAuthoritySelectionResult result = await fixture.Authority.AuthorizeSelectionAsync(
            session, fixture.CreateRequest(), CancellationToken.None);
        AssertDeniedSelection(result);
    }

    private static async Task SeedSelectableBatchUnderAuthorityAsync(
        SelectionFixture fixture,
        string authorityId,
        Guid batchId)
    {
        ControlledCredentialSource credentials = new(new OctoberOperatorCredentials(UserName, Password));
        await using ServiceProvider provider = CreateAuthorityProvider(
            fixture.Connection,
            credentials,
            fixture.Clock,
            authorityId: authorityId,
            batchId: batchId);
        await using AsyncServiceScope authorityScope = provider.CreateAsyncScope();
        IOctoberLocalAuthority authority = authorityScope.ServiceProvider
            .GetRequiredService<IOctoberLocalAuthority>();
        OctoberAuthenticationResult authentication = await authority.AuthenticateAsync(CancellationToken.None);
        Assert.Equal(OctoberAuthenticationStatus.Authenticated, authentication.Status);
        await using IOctoberOperatorSession session = Assert.IsAssignableFrom<IOctoberOperatorSession>(
            authentication.Session);

        OctoberAuthoritySelectionResult authorization = await authority.AuthorizeSelectionAsync(
            session, fixture.CreateRequest(batchId: batchId), CancellationToken.None);
        Assert.Equal(OctoberAuthorityDecisionStatus.Authorized, authorization.Status);
        OctoberSetupRequest request = Assert.IsAssignableFrom<OctoberSetupRequest>(authorization.Request);
        OctoberAuthorizedSelection trustedSelection = Assert.IsAssignableFrom<OctoberAuthorizedSelection>(
            authorization.Authority);

        await using AsyncServiceScope writerScope = provider.CreateAsyncScope();
        IOctoberSetupWriter writer = writerScope.ServiceProvider.GetRequiredService<IOctoberSetupWriter>();
        OctoberPrepareResult committed = await new OctoberBatchSetupPolicy(writer).PrepareAsync(
            request, trustedSelection, CancellationToken.None);
        Assert.Equal(OctoberPrepareStatus.Created, committed.Status);
        Assert.Equal(batchId, committed.CommittedBatch!.BatchId);
    }

    private static byte[] DecodeBase32(string value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        List<byte> bytes = [];
        int buffer = 0;
        int bitCount = 0;

        foreach (char character in value)
        {
            if (character == '=')
            {
                break;
            }

            int digit = alphabet.IndexOf(char.ToUpperInvariant(character));
            if (digit < 0)
            {
                throw new FormatException("Identity returned an invalid Base32 authenticator key.");
            }

            buffer = (buffer << 5) | digit;
            bitCount += 5;
            if (bitCount >= 8)
            {
                bitCount -= 8;
                bytes.Add((byte)(buffer >> bitCount));
                buffer &= (1 << bitCount) - 1;
            }
        }

        return bytes.ToArray();
    }

    private static ServiceProvider CreateIdentityProvider(
        SqliteConnection connection,
        ControlledCredentialSource credentialSource,
        TimeProvider clock)
    {
        ServiceCollection services = new();
        services.AddLogging();
        OctoberHeadlessAuthorityRegistration.AddOctoberLocalAuthority(
            services,
            CreateConfiguration(connection.ConnectionString),
            credentialSource,
            clock);
        ReplaceIdentityStoreWithSqlite(services, connection);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static ServiceProvider CreateAuthorityProvider(
        SqliteConnection connection,
        ControlledCredentialSource credentialSource,
        TimeProvider clock,
        bool supportsTwoFactorStore = true,
        TimeSpan? operationTimeout = null,
        IDbCommandInterceptor? identityCommandInterceptor = null,
        string authorityId = AuthorityId,
        Guid? batchId = null,
        TimeSpan? sessionLifetime = null)
    {
        ServiceCollection services = new();
        services.AddLogging();
        OctoberHeadlessAuthorityRegistration.AddOctoberLocalAuthority(
            services,
            CreateConfiguration(connection.ConnectionString, operationTimeout, authorityId, batchId, sessionLifetime),
            credentialSource,
            clock);
        ReplaceIdentityStoreWithSqlite(services, connection, identityCommandInterceptor);
        services.RemoveAll<IDbContextFactory<VulgataDbContext>>();
        services.RemoveAll<VulgataDbContext>();
        services.RemoveAll<DbContextOptions<VulgataDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<VulgataDbContext>>();
        services.RemoveAll<IConfigureOptions<DbContextOptions<VulgataDbContext>>>();
        services.AddDbContextFactory<VulgataDbContext>(options => options.UseSqlite(connection));
        services.AddDbContext<OctoberDbContext>(options => options.UseSqlite(connection));
        services.AddScoped<IOctoberSetupWriter, OctoberSetupStore>();
        if (!supportsTwoFactorStore)
        {
            services.AddScoped<UserStore<ApplicationUser, IdentityRole, ApplicationDbContext>>();
            services.Replace(ServiceDescriptor.Scoped<IUserStore<ApplicationUser>,
                UserStoreWithoutTwoFactorStore>());
        }

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static async Task SeedOwnedAuthorityAsync(
        ServiceProvider provider,
        SqliteConnection connection,
        DateTimeOffset now,
        string globalContextValue,
        string? systemContextValue,
        string? repositoryContextValue,
        string roleName = RoleNames.SystemOwner,
        bool standalone = false)
    {
        await using (AsyncServiceScope identityScope = provider.CreateAsyncScope())
        {
            ApplicationDbContext identity = identityScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await identity.Database.ExecuteSqlRawAsync(identity.Database.GenerateCreateScript());
            RoleManager<IdentityRole> roles = identityScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            IdentityResult roleCreated = await roles.CreateAsync(new IdentityRole(roleName));
            Assert.True(roleCreated.Succeeded, string.Join("; ", roleCreated.Errors.Select(error => error.Code)));

            UserManager<ApplicationUser> users = identityScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = new()
            {
                Id = SubjectId,
                UserName = UserName,
                Email = UserName,
                EmailConfirmed = true,
                LockoutEnabled = true,
            };
            IdentityResult userCreated = await users.CreateAsync(user, Password);
            Assert.True(userCreated.Succeeded, string.Join("; ", userCreated.Errors.Select(error => error.Code)));
            IdentityResult assigned = await users.AddToRoleAsync(user, roleName);
            Assert.True(assigned.Succeeded, string.Join("; ", assigned.Errors.Select(error => error.Code)));
        }

        await using (AsyncServiceScope managementScope = provider.CreateAsyncScope())
        {
            VulgataDbContext management = await managementScope.ServiceProvider
                .GetRequiredService<IDbContextFactory<VulgataDbContext>>().CreateDbContextAsync();
            await management.Database.ExecuteSqlRawAsync(management.Database.GenerateCreateScript());
            SystemEntity? system = standalone
                ? null
                : new SystemEntity("October authority System", "Owned fixture", systemContextValue, now);
            Repository repository = system is null
                ? Repository.CreateStandalone("October authority Repository",
                    "https://example.test/october-authority.git", "Owned fixture", repositoryContextValue, now)
                : system.AddRepository("October authority Repository",
                    "https://example.test/october-authority.git", "Owned fixture", repositoryContextValue, now);
            if (system is not null)
            {
                management.Systems.Add(system);
                management.SystemOwnerAssignments.Add(SystemOwnerAssignment.Create(system.Id, SubjectId, now));
            }
            management.Repositories.Add(repository);
            management.GlobalContexts.Add(new GlobalContext(globalContextValue, now));
            await management.SaveChangesAsync();
        }

        await using OctoberDbContext october = new(
            new DbContextOptionsBuilder<OctoberDbContext>().UseSqlite(connection).Options);
        await october.Database.ExecuteSqlRawAsync(october.Database.GenerateCreateScript());
    }

    private static async Task SeedUserAsync(ServiceProvider provider)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();

        UserManager<ApplicationUser> users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = new()
        {
            Id = SubjectId,
            UserName = UserName,
            Email = UserName,
            EmailConfirmed = true,
            LockoutEnabled = true,
        };
        IdentityResult created = await users.CreateAsync(user, Password);
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(error => error.Code)));
    }

    private static OctoberAuthorityConfiguration CreateConfiguration(
        string connectionString,
        TimeSpan? operationTimeout = null,
        string authorityId = AuthorityId,
        Guid? batchId = null,
        TimeSpan? sessionLifetime = null)
    {
        ImmutableDictionary<string, string> allowedTargets = ImmutableDictionary<string, string>.Empty
            .Add(TargetId, "sqlite://owned/october-authentication-target");

        return new OctoberAuthorityConfiguration(
            authorityId,
            TargetId,
            batchId ?? Guid.Parse("862de94c-c28a-4c86-9ac6-4cfca8a69c5c"),
            "sqlite://owned/october-authentication-target",
            connectionString,
            connectionString,
            "sqlite://owned/october-authentication-target",
            allowedTargets,
            operationTimeout ?? TimeSpan.FromSeconds(30),
            sessionLifetime ?? TimeSpan.FromHours(1));
    }

    private static OctoberAuthorityConfiguration CreateAuthorityConfiguration(
        string authorityId = AuthorityId,
        string targetId = TargetId,
        Guid? batchId = null,
        string canonicalDatabaseIdentity = "sqlite://owned/october-authentication-target",
        string dataConnectionString = "Data Source=synthetic-october-data",
        string identityConnectionString = "Data Source=synthetic-october-identity",
        string identityCanonicalDatabaseIdentity = "sqlite://owned/october-authentication-target",
        ImmutableDictionary<string, string>? allowedTargets = null,
        TimeSpan? operationTimeout = null,
        TimeSpan? sessionLifetime = null)
    {
        ImmutableDictionary<string, string> selectedTargets = allowedTargets ??
            ImmutableDictionary<string, string>.Empty.Add(targetId, canonicalDatabaseIdentity);

        return new OctoberAuthorityConfiguration(
            authorityId,
            targetId,
            batchId ?? Guid.Parse("862de94c-c28a-4c86-9ac6-4cfca8a69c5c"),
            canonicalDatabaseIdentity,
            dataConnectionString,
            identityConnectionString,
            identityCanonicalDatabaseIdentity,
            selectedTargets,
            operationTimeout ?? TimeSpan.FromSeconds(30),
            sessionLifetime ?? TimeSpan.FromHours(1));
    }

    private static void ReplaceIdentityStoreWithSqlite(
        IServiceCollection services,
        SqliteConnection connection,
        IDbCommandInterceptor? commandInterceptor = null)
    {
        int? configuredCommandTimeout = ReadConfiguredIdentityCommandTimeout(services);
        services.RemoveAll<ApplicationDbContext>();
        services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
        services.RemoveAll<IConfigureOptions<DbContextOptions<ApplicationDbContext>>>();
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseSqlite(connection, sqlite =>
            {
                if (configuredCommandTimeout is int timeoutSeconds)
                    sqlite.CommandTimeout(timeoutSeconds);
            });
            if (commandInterceptor is not null)
                options.AddInterceptors(commandInterceptor);
        });
    }

    private static int? ReadConfiguredIdentityCommandTimeout(IServiceCollection services)
    {
        using ServiceProvider optionsProvider = services.BuildServiceProvider(validateScopes: false);
        DbContextOptions<ApplicationDbContext> options =
            optionsProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>();
        return options.Extensions.OfType<RelationalOptionsExtension>()
            .SingleOrDefault()?.CommandTimeout;
    }

    private sealed class ControlledCredentialSource(OctoberOperatorCredentials? credentials) : IOctoberOperatorCredentialSource
    {
        public OctoberOperatorCredentials? Credentials { get; set; } = credentials;
        public bool CancelRead { get; set; }
        public int ReadCount { get; private set; }
        public Exception? ReadFailure { get; set; }

        public ValueTask<OctoberOperatorCredentials?> ReadAsync(CancellationToken cancellationToken)
        {
            ReadCount++;
            cancellationToken.ThrowIfCancellationRequested();
            if (ReadFailure is not null)
            {
                throw ReadFailure;
            }

            if (CancelRead)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return ValueTask.FromResult<OctoberOperatorCredentials?>(Credentials);
        }
    }

    private sealed class IdentityCommandTimeoutObserver : DbCommandInterceptor
    {
        private readonly object _sync = new();
        private readonly List<int> _observedTimeouts = [];
        private bool _recording;

        public int[] ObservedTimeouts
        {
            get
            {
                lock (_sync)
                    return _observedTimeouts.ToArray();
            }
        }

        public void StartObserving() => _recording = true;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Capture(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Capture(command);
            return ValueTask.FromResult(result);
        }

        private void Capture(DbCommand command)
        {
            if (!_recording)
                return;

            lock (_sync)
                _observedTimeouts.Add(command.CommandTimeout);
        }
    }

    private sealed class IdentityCommandCancellationBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _commandEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _armed;
        private int _blockedCommandCount;

        public Task CommandEntered => _commandEntered.Task;

        public int BlockedCommandCount => Volatile.Read(ref _blockedCommandCount);

        public void StartBlocking() => Volatile.Write(ref _armed, 1);

        public void Release() => _release.TrySetResult();

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref _armed) != 0 &&
                Interlocked.CompareExchange(ref _blockedCommandCount, 1, 0) == 0)
            {
                _commandEntered.TrySetResult();
                await _release.Task.ConfigureAwait(false);
            }

            return result;
        }
    }

    private sealed class UserStoreWithoutTwoFactorStore(
        UserStore<ApplicationUser, IdentityRole, ApplicationDbContext> inner) :
        IUserStore<ApplicationUser>,
        IUserPasswordStore<ApplicationUser>,
        IUserEmailStore<ApplicationUser>,
        IUserSecurityStampStore<ApplicationUser>,
        IUserLockoutStore<ApplicationUser>
    {
        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            inner.CreateAsync(user, cancellationToken);

        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            inner.UpdateAsync(user, cancellationToken);

        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            inner.DeleteAsync(user, cancellationToken);

        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            inner.GetUserIdAsync(user, cancellationToken);

        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            inner.GetUserNameAsync(user, cancellationToken);

        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken) =>
            inner.SetUserNameAsync(user, userName, cancellationToken);

        public Task<string?> GetNormalizedUserNameAsync(
            ApplicationUser user, CancellationToken cancellationToken) =>
            inner.GetNormalizedUserNameAsync(user, cancellationToken);

        public Task SetNormalizedUserNameAsync(
            ApplicationUser user, string? normalizedName, CancellationToken cancellationToken) =>
            inner.SetNormalizedUserNameAsync(user, normalizedName, cancellationToken);

        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
            inner.FindByIdAsync(userId, cancellationToken);

        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
            inner.FindByNameAsync(normalizedUserName, cancellationToken);

        public Task SetPasswordHashAsync(ApplicationUser user, string? passwordHash, CancellationToken cancellationToken) =>
            inner.SetPasswordHashAsync(user, passwordHash, cancellationToken);

        public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            inner.GetPasswordHashAsync(user, cancellationToken);

        public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            inner.HasPasswordAsync(user, cancellationToken);

        public Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken cancellationToken) =>
            inner.SetEmailAsync(user, email, cancellationToken);

        public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            inner.GetEmailAsync(user, cancellationToken);

        public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            inner.GetEmailConfirmedAsync(user, cancellationToken);

        public Task SetEmailConfirmedAsync(
            ApplicationUser user, bool confirmed, CancellationToken cancellationToken) =>
            inner.SetEmailConfirmedAsync(user, confirmed, cancellationToken);

        public Task<ApplicationUser?> FindByEmailAsync(
            string normalizedEmail, CancellationToken cancellationToken) =>
            inner.FindByEmailAsync(normalizedEmail, cancellationToken);

        public Task<string?> GetNormalizedEmailAsync(
            ApplicationUser user, CancellationToken cancellationToken) =>
            inner.GetNormalizedEmailAsync(user, cancellationToken);

        public Task SetNormalizedEmailAsync(
            ApplicationUser user, string? normalizedEmail, CancellationToken cancellationToken) =>
            inner.SetNormalizedEmailAsync(user, normalizedEmail, cancellationToken);

        public Task SetSecurityStampAsync(
            ApplicationUser user, string? stamp, CancellationToken cancellationToken) =>
            inner.SetSecurityStampAsync(user, stamp, cancellationToken);

        public Task<string?> GetSecurityStampAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            inner.GetSecurityStampAsync(user, cancellationToken);

        public Task<DateTimeOffset?> GetLockoutEndDateAsync(
            ApplicationUser user, CancellationToken cancellationToken) =>
            inner.GetLockoutEndDateAsync(user, cancellationToken);

        public Task SetLockoutEndDateAsync(
            ApplicationUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken) =>
            inner.SetLockoutEndDateAsync(user, lockoutEnd, cancellationToken);

        public Task<int> IncrementAccessFailedCountAsync(
            ApplicationUser user, CancellationToken cancellationToken) =>
            inner.IncrementAccessFailedCountAsync(user, cancellationToken);

        public Task ResetAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            inner.ResetAccessFailedCountAsync(user, cancellationToken);

        public Task<int> GetAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            inner.GetAccessFailedCountAsync(user, cancellationToken);

        public Task<bool> GetLockoutEnabledAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            inner.GetLockoutEnabledAsync(user, cancellationToken);

        public Task SetLockoutEnabledAsync(
            ApplicationUser user, bool enabled, CancellationToken cancellationToken) =>
            inner.SetLockoutEnabledAsync(user, enabled, cancellationToken);

        public void Dispose() => inner.Dispose();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now = _now.Add(duration);
    }

    private sealed class UntrustedOperatorSession(
        string subjectId,
        string authorityId,
        DateTimeOffset expiresAtUtc) : IOctoberOperatorSession
    {
        public string SubjectId { get; } = subjectId;
        public string ManagementAuthorityId { get; } = authorityId;
        public DateTimeOffset ExpiresAtUtc { get; } = expiresAtUtc;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class SelectionFixture : IAsyncDisposable
    {
        private const string GlobalContextValue = "Owned global advisory context.";
        private const string SystemContextValue = "Owned System advisory context.";
        private const string RepositoryContextValue = "Owned Repository advisory context.";
        private const string RootBindingIdentity = "prepared-root-v3";
        private const string OperatorLabel = "October test Repository";
        private static readonly Guid BatchId = Guid.Parse("862de94c-c28a-4c86-9ac6-4cfca8a69c5c");

        private readonly SqliteConnection _connection;
        private readonly AsyncServiceScope _operationScope;

        private SelectionFixture(
            SqliteConnection connection,
            ServiceProvider provider,
            AsyncServiceScope operationScope,
            ControlledCredentialSource credentialSource,
            FixedTimeProvider clock,
            IOctoberLocalAuthority authority,
            Guid? systemId,
            Guid repositoryId,
            Guid globalContextId)
        {
            _connection = connection;
            Provider = provider;
            _operationScope = operationScope;
            CredentialSource = credentialSource;
            Clock = clock;
            Authority = authority;
            SystemId = systemId;
            RepositoryId = repositoryId;
            GlobalContextId = globalContextId;
        }

        public ServiceProvider Provider { get; }
        public SqliteConnection Connection => _connection;
        public ControlledCredentialSource CredentialSource { get; }
        public FixedTimeProvider Clock { get; }
        public IOctoberLocalAuthority Authority { get; }
        public Guid? SystemId { get; }
        public Guid RepositoryId { get; }
        public Guid GlobalContextId { get; }

        public static async Task<SelectionFixture> CreateAsync(
            bool standalone = false,
            string roleName = RoleNames.SystemOwner,
            string globalContextValue = GlobalContextValue,
            string? systemContextValue = SystemContextValue,
            string? repositoryContextValue = RepositoryContextValue,
            TimeSpan? sessionLifetime = null)
        {
            DateTimeOffset now = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
            SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            ControlledCredentialSource credentialSource =
                new(new OctoberOperatorCredentials(UserName, Password));
            FixedTimeProvider clock = new(now);
            ServiceProvider provider = CreateAuthorityProvider(
                connection, credentialSource, clock, sessionLifetime: sessionLifetime);
            AsyncServiceScope operationScope = default;
            bool scopeCreated = false;
            try
            {
                await SeedOwnedAuthorityAsync(provider, connection, now,
                    globalContextValue, systemContextValue, repositoryContextValue, roleName, standalone);
                Guid? systemId = null;
                Guid repositoryId;
                Guid globalContextId;
                await using (VulgataDbContext management = await provider
                    .GetRequiredService<IDbContextFactory<VulgataDbContext>>().CreateDbContextAsync())
                {
                    if (!standalone)
                    {
                        SystemEntity system = await management.Systems.AsNoTracking()
                            .SingleAsync(value => value.Name == "October authority System");
                        systemId = system.Id;
                    }
                    Repository repository = await management.Repositories.AsNoTracking()
                        .SingleAsync(value => value.Name == "October authority Repository");
                    GlobalContext global = await management.GlobalContexts.AsNoTracking()
                        .SingleAsync(value => value.Context == globalContextValue);
                    repositoryId = repository.Id;
                    globalContextId = global.Id;
                }

                operationScope = provider.CreateAsyncScope();
                scopeCreated = true;
                IOctoberLocalAuthority authority = operationScope.ServiceProvider
                    .GetRequiredService<IOctoberLocalAuthority>();
                return new SelectionFixture(connection, provider, operationScope,
                    credentialSource, clock, authority, systemId, repositoryId, globalContextId);
            }
            catch
            {
                if (scopeCreated)
                    await operationScope.DisposeAsync();
                await provider.DisposeAsync();
                await connection.DisposeAsync();
                throw;
            }
        }

        public OctoberAuthoritySelectionRequest CreateRequest(
            string targetId = TargetId,
            Guid? batchId = null,
            string selectionDigest = "",
            IReadOnlyList<OctoberContextSelection>? contextSelections = null)
        {
            OctoberRepositorySelection repository = new(
                RepositoryId, SystemId, RootBindingIdentity, OperatorLabel);
            IReadOnlyList<OctoberContextSelection> references = contextSelections ??
                (SystemId is Guid systemId
                    ? [
                        new OctoberContextSelection(OctoberContextScope.Global, GlobalContextId),
                        new OctoberContextSelection(OctoberContextScope.System, systemId),
                        new OctoberContextSelection(OctoberContextScope.Repository, RepositoryId),
                    ]
                    : [
                        new OctoberContextSelection(OctoberContextScope.Global, GlobalContextId),
                        new OctoberContextSelection(OctoberContextScope.Repository, RepositoryId),
                    ]);
            OctoberSetupRequest prepare = new(
                TargetId,
                batchId ?? BatchId,
                [repository],
                references,
                selectionDigest);
            return new OctoberAuthoritySelectionRequest(TargetId, batchId ?? BatchId, prepare);
        }

        public OctoberSelectedAdvisoryContext ExpectedFullySelectedContext() => new(
            AuthorityId,
            [new OctoberRepositorySelection(RepositoryId, SystemId, RootBindingIdentity, OperatorLabel)],
            [
                new OctoberAdvisoryContextEntry(RepositoryId, OctoberContextScope.Global,
                    OctoberContextApplicability.Selected, GlobalContextId, GlobalContextValue,
                    OctoberContextEvidenceKind.AdvisoryNotSourceProof),
                new OctoberAdvisoryContextEntry(RepositoryId, OctoberContextScope.System,
                    OctoberContextApplicability.Selected, SystemId, SystemContextValue,
                    OctoberContextEvidenceKind.AdvisoryNotSourceProof),
                new OctoberAdvisoryContextEntry(RepositoryId, OctoberContextScope.Repository,
                    OctoberContextApplicability.Selected, RepositoryId, RepositoryContextValue,
                    OctoberContextEvidenceKind.AdvisoryNotSourceProof),
            ],
            [
                new OctoberContextDifference(RepositoryId, OctoberContextScope.Global, OctoberContextScope.System),
                new OctoberContextDifference(RepositoryId, OctoberContextScope.Global, OctoberContextScope.Repository),
                new OctoberContextDifference(RepositoryId, OctoberContextScope.System, OctoberContextScope.Repository),
            ]);

        public async Task<IOctoberOperatorSession> AuthenticateAsync()
        {
            CredentialSource.Credentials = new OctoberOperatorCredentials(UserName, Password);
            OctoberAuthenticationResult authentication = await Authority.AuthenticateAsync(CancellationToken.None);
            Assert.Equal(OctoberAuthenticationStatus.Authenticated, authentication.Status);
            return Assert.IsAssignableFrom<IOctoberOperatorSession>(authentication.Session);
        }

        public Task<VulgataDbContext> CreateManagementContextAsync() => Provider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>().CreateDbContextAsync();

        public async ValueTask DisposeAsync()
        {
            await _operationScope.DisposeAsync();
            await Provider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
