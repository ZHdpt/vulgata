using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task IssueSourceApprovalAsync_DoesNotIssueWhenFreshIdentityDatabaseCannotBeConfirmed()
    {
        await using ApprovalIdentityFixture fixture = await ApprovalIdentityFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        IOctoberHistoricalBoundSetupData boundSetup = await fixture.CreateCommittedBoundSetupAsync(session);

        fixture.UseAlternateIdentityDatabase();
        OctoberSourceApprovalResult result = await fixture.Authority.IssueSourceApprovalAsync(
            session, boundSetup, fixture.RepositoryId, CancellationToken.None);

        Assert.True(result.Status != OctoberSourceApprovalStatus.Issued,
            $"Issue must fail closed when fresh Identity resolves to a different owned database; " +
            $"configured={fixture.ConfiguredIdentityDatabaseName}, actual={fixture.AlternateIdentityDatabaseName}.");
        Assert.Null(result.Approval);
        Assert.Null(result.ExpiresAtUtc);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task VerifySourceApprovalAsync_DoesNotReturnCurrentWhenFreshIdentityDatabaseChanges()
    {
        await using ApprovalIdentityFixture fixture = await ApprovalIdentityFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        IOctoberHistoricalBoundSetupData boundSetup = await fixture.CreateCommittedBoundSetupAsync(session);
        OctoberSourceApprovalResult issued = await fixture.Authority.IssueSourceApprovalAsync(
            session, boundSetup, fixture.RepositoryId, CancellationToken.None);
        Assert.Equal(OctoberSourceApprovalStatus.Issued, issued.Status);
        PreparedSourceApproval approval = Assert.IsAssignableFrom<PreparedSourceApproval>(issued.Approval);

        PreparedSourceAuthorityResult initiallyCurrent = await fixture.Authority.VerifySourceApprovalAsync(
            session, TargetId, approval, CancellationToken.None);
        Assert.Equal(PreparedSourceAuthorityStatus.Current, initiallyCurrent.Status);
        Assert.Equal(approval, initiallyCurrent.CurrentApproval);

        fixture.UseAlternateIdentityDatabase();
        PreparedSourceAuthorityResult afterIdentityDatabaseChange = await fixture.Authority
            .VerifySourceApprovalAsync(session, TargetId, approval, CancellationToken.None);

        Assert.NotEqual(PreparedSourceAuthorityStatus.Current, afterIdentityDatabaseChange.Status);
        Assert.Null(afterIdentityDatabaseChange.CurrentApproval);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task RevokeSourceApprovalAsync_DoesNotRevokeWhenFreshIdentityDatabaseChanges()
    {
        await using ApprovalIdentityFixture fixture = await ApprovalIdentityFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        IOctoberHistoricalBoundSetupData boundSetup = await fixture.CreateCommittedBoundSetupAsync(session);
        OctoberSourceApprovalResult issued = await fixture.Authority.IssueSourceApprovalAsync(
            session, boundSetup, fixture.RepositoryId, CancellationToken.None);
        Assert.Equal(OctoberSourceApprovalStatus.Issued, issued.Status);
        PreparedSourceApproval approval = Assert.IsAssignableFrom<PreparedSourceApproval>(issued.Approval);

        PreparedSourceAuthorityResult initiallyCurrent = await fixture.Authority.VerifySourceApprovalAsync(
            session, TargetId, approval, CancellationToken.None);
        Assert.Equal(PreparedSourceAuthorityStatus.Current, initiallyCurrent.Status);
        Assert.Equal(approval, initiallyCurrent.CurrentApproval);

        fixture.UseAlternateIdentityDatabase();
        OctoberRevocationStatus afterIdentityDatabaseChange = await fixture.Authority.RevokeSourceApprovalAsync(
            session, TargetId, approval.ApprovalIdentity, CancellationToken.None);

        Assert.False(afterIdentityDatabaseChange is OctoberRevocationStatus.Revoked or
            OctoberRevocationStatus.AlreadyRevoked,
            $"A fresh Identity mismatch must not revoke an approval; " +
            $"configured={fixture.ConfiguredIdentityDatabaseName}, actual={fixture.AlternateIdentityDatabaseName}.");

        fixture.UseConfiguredIdentityDatabase();
        PreparedSourceAuthorityResult remainsCurrent = await fixture.Authority.VerifySourceApprovalAsync(
            session, TargetId, approval, CancellationToken.None);
        Assert.Equal(PreparedSourceAuthorityStatus.Current, remainsCurrent.Status);
        Assert.Equal(approval, remainsCurrent.CurrentApproval);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task IssueSourceApprovalAsync_IssuesExactApprovalForCommittedRepository()
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        (OctoberCommittedBatch committed, IOctoberHistoricalBoundSetupData boundSetup) =
            await CreateCommittedBoundSetupAsync(fixture, session);

        OctoberSourceApprovalResult result = await fixture.Authority.IssueSourceApprovalAsync(
            session, boundSetup, fixture.RepositoryId, CancellationToken.None);

        Assert.Equal(OctoberSourceApprovalStatus.Issued, result.Status);
        PreparedSourceApproval approval = Assert.IsAssignableFrom<PreparedSourceApproval>(result.Approval);
        Assert.Equal(committed.BatchId, approval.BatchId);
        Assert.Equal(fixture.RepositoryId, approval.RepositoryId);
        Assert.Equal(SubjectId, approval.SubjectId);
        Assert.Equal(PreparedSourcePurpose.WorkerSourceRead, approval.Purpose);
        Assert.False(string.IsNullOrWhiteSpace(approval.ApprovalIdentity));
        Assert.Equal("prepared-root-v3", approval.RootBindingIdentity);
        Assert.Equal("October test Repository", approval.OperatorLabel);

        DateTimeOffset expiresAtUtc = Assert.IsType<DateTimeOffset>(result.ExpiresAtUtc);
        Assert.Equal(TimeSpan.Zero, expiresAtUtc.Offset);
        Assert.True(expiresAtUtc > fixture.Clock.GetUtcNow());
        Assert.True(expiresAtUtc <= fixture.Clock.GetUtcNow().AddMinutes(10));
        Assert.True(expiresAtUtc <= session.ExpiresAtUtc);

        await using AsyncServiceScope verificationScope = fixture.Provider.CreateAsyncScope();
        OctoberDbContext verificationStore = verificationScope.ServiceProvider
            .GetRequiredService<OctoberDbContext>();
        OctoberPreparedSourceApprovalRecord row = Assert.Single(await verificationStore.PreparedSourceApprovals
            .Where(value => value.BatchId == committed.BatchId && value.RepositoryId == fixture.RepositoryId)
            .ToListAsync());
        Assert.Equal(approval.ApprovalIdentity, row.ApprovalIdentity);
        Assert.Equal(approval.BatchId, row.BatchId);
        Assert.Equal(approval.RepositoryId, row.RepositoryId);
        Assert.Equal(approval.SubjectId, row.SubjectId);
        Assert.Equal((int)approval.Purpose, row.Purpose);
        Assert.Equal(approval.RootBindingIdentity, row.RootBindingIdentity);
        Assert.Equal(approval.OperatorLabel, row.OperatorLabel);
        Assert.Equal(expiresAtUtc, row.ExpiresAtUtc);
        Assert.False(row.IsRevoked);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task IssueSourceApprovalAsync_DeniesRepositoryOutsideBoundSelectionWithoutPayload()
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        IOctoberHistoricalBoundSetupData boundSetup =
            (await CreateCommittedBoundSetupAsync(fixture, session)).BoundSetup;

        OctoberSourceApprovalResult result = await fixture.Authority.IssueSourceApprovalAsync(
            session, boundSetup, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(OctoberSourceApprovalStatus.Denied, result.Status);
        Assert.Null(result.Approval);
        Assert.Null(result.ExpiresAtUtc);
        await using AsyncServiceScope verificationScope = fixture.Provider.CreateAsyncScope();
        OctoberDbContext october = verificationScope.ServiceProvider.GetRequiredService<OctoberDbContext>();
        Assert.Empty(await october.PreparedSourceApprovals.AsNoTracking().ToListAsync());
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task VerifySourceApprovalAsync_ReturnsCurrentIssuedApproval()
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        IOctoberHistoricalBoundSetupData boundSetup =
            (await CreateCommittedBoundSetupAsync(fixture, session)).BoundSetup;

        OctoberSourceApprovalResult issued = await fixture.Authority.IssueSourceApprovalAsync(
            session, boundSetup, fixture.RepositoryId, CancellationToken.None);
        Assert.Equal(OctoberSourceApprovalStatus.Issued, issued.Status);
        PreparedSourceApproval approval = Assert.IsAssignableFrom<PreparedSourceApproval>(issued.Approval);

        PreparedSourceAuthorityResult verification = await fixture.Authority.VerifySourceApprovalAsync(
            session, TargetId, approval, CancellationToken.None);

        Assert.Equal(PreparedSourceAuthorityStatus.Current, verification.Status);
        Assert.Equal(approval, verification.CurrentApproval);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task VerifySourceApprovalAsync_RejectsExpiredApprovalWithoutPayload()
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        IOctoberHistoricalBoundSetupData boundSetup =
            (await CreateCommittedBoundSetupAsync(fixture, session)).BoundSetup;
        OctoberSourceApprovalResult issued = await fixture.Authority.IssueSourceApprovalAsync(
            session, boundSetup, fixture.RepositoryId, CancellationToken.None);
        Assert.Equal(OctoberSourceApprovalStatus.Issued, issued.Status);
        PreparedSourceApproval approval = Assert.IsAssignableFrom<PreparedSourceApproval>(issued.Approval);

        fixture.Clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));

        PreparedSourceAuthorityResult expired = await fixture.Authority.VerifySourceApprovalAsync(
            session, TargetId, approval, CancellationToken.None);
        Assert.Equal(PreparedSourceAuthorityStatus.Revoked, expired.Status);
        Assert.Null(expired.CurrentApproval);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task VerifySourceApprovalAsync_RejectsApprovalAtSessionExpiryWithoutPayload()
    {
        TimeSpan sessionLifetime = TimeSpan.FromMinutes(2);
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync(
            sessionLifetime: sessionLifetime);
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        IOctoberHistoricalBoundSetupData boundSetup =
            (await CreateCommittedBoundSetupAsync(fixture, session)).BoundSetup;
        OctoberSourceApprovalResult issued = await fixture.Authority.IssueSourceApprovalAsync(
            session, boundSetup, fixture.RepositoryId, CancellationToken.None);
        Assert.Equal(OctoberSourceApprovalStatus.Issued, issued.Status);
        PreparedSourceApproval approval = Assert.IsAssignableFrom<PreparedSourceApproval>(issued.Approval);
        Assert.Equal(session.ExpiresAtUtc, issued.ExpiresAtUtc);

        fixture.Clock.Advance(sessionLifetime + TimeSpan.FromSeconds(1));

        PreparedSourceAuthorityResult expired = await fixture.Authority.VerifySourceApprovalAsync(
            session, TargetId, approval, CancellationToken.None);
        Assert.Equal(PreparedSourceAuthorityStatus.Revoked, expired.Status);
        Assert.Null(expired.CurrentApproval);
        Assert.Equal(OctoberRevocationStatus.Denied,
            await fixture.Authority.RevokeSourceApprovalAsync(
                session, TargetId, approval.ApprovalIdentity, CancellationToken.None));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task VerifyAndRevokeSourceApprovalAsync_DenyDisposedAuthenticatedSession()
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        IOctoberHistoricalBoundSetupData boundSetup =
            (await CreateCommittedBoundSetupAsync(fixture, session)).BoundSetup;
        OctoberSourceApprovalResult issued = await fixture.Authority.IssueSourceApprovalAsync(
            session, boundSetup, fixture.RepositoryId, CancellationToken.None);
        Assert.Equal(OctoberSourceApprovalStatus.Issued, issued.Status);
        PreparedSourceApproval approval = Assert.IsAssignableFrom<PreparedSourceApproval>(issued.Approval);

        await session.DisposeAsync();

        PreparedSourceAuthorityResult verification = await fixture.Authority.VerifySourceApprovalAsync(
            session, TargetId, approval, CancellationToken.None);
        Assert.Equal(PreparedSourceAuthorityStatus.Revoked, verification.Status);
        Assert.Null(verification.CurrentApproval);
        Assert.Equal(OctoberRevocationStatus.Denied,
            await fixture.Authority.RevokeSourceApprovalAsync(
                session, TargetId, approval.ApprovalIdentity, CancellationToken.None));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task IssueVerifyAndRevokeSourceApprovalAsync_RespectCancellationContracts()
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        IOctoberHistoricalBoundSetupData boundSetup =
            (await CreateCommittedBoundSetupAsync(fixture, session)).BoundSetup;

        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        OctoberSourceApprovalResult cancelledIssue = await fixture.Authority.IssueSourceApprovalAsync(
            session, boundSetup, fixture.RepositoryId, cancelled.Token);
        Assert.Equal(OctoberSourceApprovalStatus.Cancelled, cancelledIssue.Status);
        Assert.Null(cancelledIssue.Approval);
        Assert.Null(cancelledIssue.ExpiresAtUtc);

        OctoberSourceApprovalResult issued = await fixture.Authority.IssueSourceApprovalAsync(
            session, boundSetup, fixture.RepositoryId, CancellationToken.None);
        Assert.Equal(OctoberSourceApprovalStatus.Issued, issued.Status);
        PreparedSourceApproval approval = Assert.IsAssignableFrom<PreparedSourceApproval>(issued.Approval);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await fixture.Authority.VerifySourceApprovalAsync(session, TargetId, approval, cancelled.Token));
        Assert.Equal(OctoberRevocationStatus.Cancelled,
            await fixture.Authority.RevokeSourceApprovalAsync(
                session, TargetId, approval.ApprovalIdentity, cancelled.Token));

        await using AsyncServiceScope verificationScope = fixture.Provider.CreateAsyncScope();
        OctoberDbContext october = verificationScope.ServiceProvider.GetRequiredService<OctoberDbContext>();
        OctoberPreparedSourceApprovalRecord row = Assert.Single(await october.PreparedSourceApprovals
            .Where(value => value.BatchId == approval.BatchId &&
                value.RepositoryId == approval.RepositoryId &&
                value.ApprovalIdentity == approval.ApprovalIdentity)
            .ToListAsync());
        Assert.False(row.IsRevoked);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task IssueSourceApprovalAsync_ReturnsUnavailableWithoutPayloadWhenApprovalStoreFails()
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        IOctoberHistoricalBoundSetupData boundSetup =
            (await CreateCommittedBoundSetupAsync(fixture, session)).BoundSetup;
        await using (AsyncServiceScope outageScope = fixture.Provider.CreateAsyncScope())
        {
            OctoberDbContext october = outageScope.ServiceProvider.GetRequiredService<OctoberDbContext>();
            await october.Database.ExecuteSqlRawAsync("DROP TABLE OctoberPreparedSourceApprovals;");
        }

        OctoberSourceApprovalResult result = await fixture.Authority.IssueSourceApprovalAsync(
            session, boundSetup, fixture.RepositoryId, CancellationToken.None);

        Assert.Equal(OctoberSourceApprovalStatus.Unavailable, result.Status);
        Assert.Null(result.Approval);
        Assert.Null(result.ExpiresAtUtc);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task VerifyAndRevokeSourceApprovalAsync_ReturnUnavailableWhenApprovalStoreFails()
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        IOctoberHistoricalBoundSetupData boundSetup =
            (await CreateCommittedBoundSetupAsync(fixture, session)).BoundSetup;
        OctoberSourceApprovalResult issued = await fixture.Authority.IssueSourceApprovalAsync(
            session, boundSetup, fixture.RepositoryId, CancellationToken.None);
        Assert.Equal(OctoberSourceApprovalStatus.Issued, issued.Status);
        PreparedSourceApproval approval = Assert.IsAssignableFrom<PreparedSourceApproval>(issued.Approval);

        await using (AsyncServiceScope outageScope = fixture.Provider.CreateAsyncScope())
        {
            OctoberDbContext october = outageScope.ServiceProvider.GetRequiredService<OctoberDbContext>();
            await october.Database.ExecuteSqlRawAsync("DROP TABLE OctoberPreparedSourceApprovals;");
        }

        PreparedSourceAuthorityResult verification = await fixture.Authority.VerifySourceApprovalAsync(
            session, TargetId, approval, CancellationToken.None);
        Assert.Equal(PreparedSourceAuthorityStatus.Unavailable, verification.Status);
        Assert.Null(verification.CurrentApproval);

        OctoberRevocationStatus revocation = await fixture.Authority.RevokeSourceApprovalAsync(
            session, TargetId, approval.ApprovalIdentity, CancellationToken.None);
        Assert.Equal(OctoberRevocationStatus.Unavailable, revocation);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task VerifySourceApprovalAsync_SameAuthorityAdvisoryEditsPreserveApprovalAndHistory()
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        IOctoberHistoricalBoundSetupData boundSetup =
            (await CreateCommittedBoundSetupAsync(fixture, session)).BoundSetup;
        OctoberSourceApprovalResult issued = await fixture.Authority.IssueSourceApprovalAsync(
            session, boundSetup, fixture.RepositoryId, CancellationToken.None);
        Assert.Equal(OctoberSourceApprovalStatus.Issued, issued.Status);
        PreparedSourceApproval approval = Assert.IsAssignableFrom<PreparedSourceApproval>(issued.Approval);

        string?[] storedHistory;
        await using (AsyncServiceScope beforeScope = fixture.Provider.CreateAsyncScope())
        {
            OctoberDbContext october = beforeScope.ServiceProvider.GetRequiredService<OctoberDbContext>();
            storedHistory = await october.AdvisoryEntries.AsNoTracking()
                .Where(value => value.BatchId == approval.BatchId && value.RepositoryId == approval.RepositoryId)
                .OrderBy(value => value.Scope)
                .Select(value => value.Value)
                .ToArrayAsync();
        }

        await using (VulgataDbContext management = await fixture.CreateManagementContextAsync())
        {
            DateTimeOffset updatedAt = fixture.Clock.GetUtcNow().AddSeconds(1);
            GlobalContext global = await management.GlobalContexts
                .SingleAsync(value => value.Id == fixture.GlobalContextId);
            global.UpdateContext("revised global advisory text", updatedAt);

            Guid systemId = Assert.IsType<Guid>(fixture.SystemId);
            SystemEntity system = await management.Systems.SingleAsync(value => value.Id == systemId);
            system.UpdateDetails(system.Name, system.Description, "revised System advisory text", updatedAt);

            Repository repository = await management.Repositories
                .SingleAsync(value => value.Id == fixture.RepositoryId);
            repository.UpdateDetails(repository.Name, repository.GitUrl, repository.Description,
                "revised Repository advisory text", updatedAt);
            await management.SaveChangesAsync();
        }

        PreparedSourceAuthorityResult current = await fixture.Authority.VerifySourceApprovalAsync(
            session, TargetId, approval, CancellationToken.None);
        Assert.Equal(PreparedSourceAuthorityStatus.Current, current.Status);
        Assert.Equal(approval, current.CurrentApproval);

        await using AsyncServiceScope afterScope = fixture.Provider.CreateAsyncScope();
        OctoberDbContext after = afterScope.ServiceProvider.GetRequiredService<OctoberDbContext>();
        string?[] retainedHistory = await after.AdvisoryEntries.AsNoTracking()
            .Where(value => value.BatchId == approval.BatchId && value.RepositoryId == approval.RepositoryId)
            .OrderBy(value => value.Scope)
            .Select(value => value.Value)
            .ToArrayAsync();
        Assert.Equal(storedHistory, retainedHistory);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task VerifySourceApprovalAsync_ClosesSessionAfterOwnershipLossWithoutReactivation()
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        IOctoberHistoricalBoundSetupData boundSetup =
            (await CreateCommittedBoundSetupAsync(fixture, session)).BoundSetup;
        OctoberSourceApprovalResult issued = await fixture.Authority.IssueSourceApprovalAsync(
            session, boundSetup, fixture.RepositoryId, CancellationToken.None);
        Assert.Equal(OctoberSourceApprovalStatus.Issued, issued.Status);
        PreparedSourceApproval approval = Assert.IsAssignableFrom<PreparedSourceApproval>(issued.Approval);

        Guid systemId = Assert.IsType<Guid>(fixture.SystemId);
        await using (VulgataDbContext management = await fixture.CreateManagementContextAsync())
        {
            SystemOwnerAssignment assignment = await management.SystemOwnerAssignments
                .SingleAsync(value => value.SystemId == systemId && value.UserId == session.SubjectId);
            management.SystemOwnerAssignments.Remove(assignment);
            await management.SaveChangesAsync();
        }

        PreparedSourceAuthorityResult afterLoss = await fixture.Authority.VerifySourceApprovalAsync(
            session, TargetId, approval, CancellationToken.None);
        Assert.Equal(PreparedSourceAuthorityStatus.Revoked, afterLoss.Status);
        Assert.Null(afterLoss.CurrentApproval);

        await using (VulgataDbContext management = await fixture.CreateManagementContextAsync())
        {
            management.SystemOwnerAssignments.Add(SystemOwnerAssignment.Create(
                systemId, session.SubjectId, fixture.Clock.GetUtcNow()));
            await management.SaveChangesAsync();
        }

        PreparedSourceAuthorityResult afterRestoration = await fixture.Authority.VerifySourceApprovalAsync(
            session, TargetId, approval, CancellationToken.None);
        Assert.Equal(PreparedSourceAuthorityStatus.Revoked, afterRestoration.Status);
        Assert.Null(afterRestoration.CurrentApproval);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task VerifySourceApprovalAsync_RevokesAfterManagementRoleRemoval()
    {
        await AssertApprovalRevokedAfterIdentityChangeAsync(async (users, user) =>
        {
            IdentityResult removed = await users.RemoveFromRoleAsync(user, RoleNames.SystemOwner);
            Assert.True(removed.Succeeded, string.Join("; ", removed.Errors.Select(error => error.Code)));
        });
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task VerifySourceApprovalAsync_RevokesAfterSecurityStampChange()
    {
        await AssertApprovalRevokedAfterIdentityChangeAsync(async (users, user) =>
        {
            IdentityResult updated = await users.UpdateSecurityStampAsync(user);
            Assert.True(updated.Succeeded, string.Join("; ", updated.Errors.Select(error => error.Code)));
        });
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task VerifySourceApprovalAsync_RevokesWhenIdentityAccountIsLockedOut()
    {
        await AssertApprovalRevokedAfterIdentityChangeAsync(async (users, user) =>
        {
            IdentityResult locked = await users.SetLockoutEndDateAsync(
                user, DateTimeOffset.UtcNow.AddMinutes(10));
            Assert.True(locked.Succeeded, string.Join("; ", locked.Errors.Select(error => error.Code)));
            Assert.True(await users.IsLockedOutAsync(user));
        });
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task VerifySourceApprovalAsync_RevokesAfterIdentityAccountDeletion()
    {
        await AssertApprovalRevokedAfterIdentityChangeAsync(async (users, user) =>
        {
            IdentityResult deleted = await users.DeleteAsync(user);
            Assert.True(deleted.Succeeded, string.Join("; ", deleted.Errors.Select(error => error.Code)));
        });
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task RevokeSourceApprovalAsync_RevokesCurrentSubjectApproval()
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        (OctoberCommittedBatch committed, IOctoberHistoricalBoundSetupData boundSetup) =
            await CreateCommittedBoundSetupAsync(fixture, session);

        OctoberSourceApprovalResult issued = await fixture.Authority.IssueSourceApprovalAsync(
            session, boundSetup, fixture.RepositoryId, CancellationToken.None);
        Assert.Equal(OctoberSourceApprovalStatus.Issued, issued.Status);
        PreparedSourceApproval approval = Assert.IsAssignableFrom<PreparedSourceApproval>(issued.Approval);
        Assert.Equal(session.SubjectId, approval.SubjectId);

        PreparedSourceAuthorityResult current = await fixture.Authority.VerifySourceApprovalAsync(
            session, TargetId, approval, CancellationToken.None);
        Assert.Equal(PreparedSourceAuthorityStatus.Current, current.Status);
        Assert.Equal(approval, current.CurrentApproval);

        Assert.Equal(OctoberRevocationStatus.Denied,
            await fixture.Authority.RevokeSourceApprovalAsync(
                session, "another-october-target", approval.ApprovalIdentity, CancellationToken.None));

        OctoberRevocationStatus revoked = await fixture.Authority.RevokeSourceApprovalAsync(
            session, TargetId, approval.ApprovalIdentity, CancellationToken.None);

        Assert.Equal(OctoberRevocationStatus.Revoked, revoked);
        Assert.Equal(OctoberRevocationStatus.AlreadyRevoked,
            await fixture.Authority.RevokeSourceApprovalAsync(
                session, TargetId, approval.ApprovalIdentity, CancellationToken.None));
        await using (AsyncServiceScope verificationScope = fixture.Provider.CreateAsyncScope())
        {
            OctoberDbContext october = verificationScope.ServiceProvider.GetRequiredService<OctoberDbContext>();
            OctoberPreparedSourceApprovalRecord row = Assert.Single(await october.PreparedSourceApprovals
                .Where(value => value.BatchId == committed.BatchId &&
                    value.RepositoryId == fixture.RepositoryId &&
                    value.ApprovalIdentity == approval.ApprovalIdentity)
                .ToListAsync());
            Assert.True(row.IsRevoked);
        }

        PreparedSourceAuthorityResult afterRevocation = await fixture.Authority.VerifySourceApprovalAsync(
            session, TargetId, approval, CancellationToken.None);
        Assert.Equal(PreparedSourceAuthorityStatus.Revoked, afterRevocation.Status);
        Assert.Null(afterRevocation.CurrentApproval);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task RevokeSourceApprovalAsync_AllowsCurrentAdministratorForExactAuthority()
    {
        const string administratorId = "october-approval-administrator";
        const string administratorName = "october.approval.admin@example.test";
        const string administratorPassword = "AdminApproval!Pass2038";

        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession ownerSession = await fixture.AuthenticateAsync();
        (OctoberCommittedBatch committed, IOctoberHistoricalBoundSetupData boundSetup) =
            await CreateCommittedBoundSetupAsync(fixture, ownerSession);
        OctoberSourceApprovalResult issued = await fixture.Authority.IssueSourceApprovalAsync(
            ownerSession, boundSetup, fixture.RepositoryId, CancellationToken.None);
        Assert.Equal(OctoberSourceApprovalStatus.Issued, issued.Status);
        PreparedSourceApproval approval = Assert.IsAssignableFrom<PreparedSourceApproval>(issued.Approval);

        await using (AsyncServiceScope identityScope = fixture.Provider.CreateAsyncScope())
        {
            RoleManager<IdentityRole> roles = identityScope.ServiceProvider
                .GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roles.RoleExistsAsync(RoleNames.Administrator))
            {
                IdentityResult roleCreated = await roles.CreateAsync(new IdentityRole(RoleNames.Administrator));
                Assert.True(roleCreated.Succeeded,
                    string.Join("; ", roleCreated.Errors.Select(error => error.Code)));
            }

            UserManager<ApplicationUser> users = identityScope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser administrator = new()
            {
                Id = administratorId,
                UserName = administratorName,
                Email = administratorName,
                EmailConfirmed = true,
                LockoutEnabled = true,
            };
            IdentityResult created = await users.CreateAsync(administrator, administratorPassword);
            Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(error => error.Code)));
            IdentityResult assigned = await users.AddToRoleAsync(administrator, RoleNames.Administrator);
            Assert.True(assigned.Succeeded, string.Join("; ", assigned.Errors.Select(error => error.Code)));
        }

        fixture.CredentialSource.Credentials = new OctoberOperatorCredentials(
            administratorName, administratorPassword);
        OctoberAuthenticationResult authentication = await fixture.Authority.AuthenticateAsync(CancellationToken.None);
        Assert.Equal(OctoberAuthenticationStatus.Authenticated, authentication.Status);
        await using IOctoberOperatorSession administratorSession =
            Assert.IsAssignableFrom<IOctoberOperatorSession>(authentication.Session);
        Assert.Equal(administratorId, administratorSession.SubjectId);
        Assert.NotEqual(ownerSession.SubjectId, administratorSession.SubjectId);

        OctoberRevocationStatus revoked = await fixture.Authority.RevokeSourceApprovalAsync(
            administratorSession, TargetId, approval.ApprovalIdentity, CancellationToken.None);

        Assert.Equal(OctoberRevocationStatus.Revoked, revoked);
        await using AsyncServiceScope verificationScope = fixture.Provider.CreateAsyncScope();
        OctoberDbContext october = verificationScope.ServiceProvider.GetRequiredService<OctoberDbContext>();
        OctoberPreparedSourceApprovalRecord row = Assert.Single(await october.PreparedSourceApprovals
            .Where(value => value.BatchId == committed.BatchId &&
                value.RepositoryId == fixture.RepositoryId &&
                value.ApprovalIdentity == approval.ApprovalIdentity)
            .ToListAsync());
        Assert.True(row.IsRevoked);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task RevokeSourceApprovalAsync_DeniesNonOwnerAndUnknownIdentityWithoutDisclosure()
    {
        const string otherOperatorId = "october-unprivileged-operator";
        const string otherOperatorName = "october.unprivileged@example.test";
        const string otherOperatorPassword = "NonOwnerApproval!Pass2038";

        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession ownerSession = await fixture.AuthenticateAsync();
        (OctoberCommittedBatch committed, IOctoberHistoricalBoundSetupData boundSetup) =
            await CreateCommittedBoundSetupAsync(fixture, ownerSession);
        OctoberSourceApprovalResult issued = await fixture.Authority.IssueSourceApprovalAsync(
            ownerSession, boundSetup, fixture.RepositoryId, CancellationToken.None);
        Assert.Equal(OctoberSourceApprovalStatus.Issued, issued.Status);
        PreparedSourceApproval approval = Assert.IsAssignableFrom<PreparedSourceApproval>(issued.Approval);

        await using (AsyncServiceScope identityScope = fixture.Provider.CreateAsyncScope())
        {
            UserManager<ApplicationUser> users = identityScope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser otherOperator = new()
            {
                Id = otherOperatorId,
                UserName = otherOperatorName,
                Email = otherOperatorName,
                EmailConfirmed = true,
                LockoutEnabled = true,
            };
            IdentityResult created = await users.CreateAsync(otherOperator, otherOperatorPassword);
            Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(error => error.Code)));
        }

        fixture.CredentialSource.Credentials = new OctoberOperatorCredentials(
            otherOperatorName, otherOperatorPassword);
        OctoberAuthenticationResult authentication = await fixture.Authority.AuthenticateAsync(CancellationToken.None);
        Assert.Equal(OctoberAuthenticationStatus.Authenticated, authentication.Status);
        await using IOctoberOperatorSession otherOperatorSession =
            Assert.IsAssignableFrom<IOctoberOperatorSession>(authentication.Session);
        Assert.Equal(otherOperatorId, otherOperatorSession.SubjectId);

        OctoberRevocationStatus existingForOtherOperator = await fixture.Authority.RevokeSourceApprovalAsync(
            otherOperatorSession, TargetId, approval.ApprovalIdentity, CancellationToken.None);
        Assert.Equal(OctoberRevocationStatus.Denied, existingForOtherOperator);

        fixture.CredentialSource.Credentials = new OctoberOperatorCredentials(
            otherOperatorName, otherOperatorPassword);
        OctoberAuthenticationResult freshAuthentication = await fixture.Authority.AuthenticateAsync(
            CancellationToken.None);
        Assert.Equal(OctoberAuthenticationStatus.Authenticated, freshAuthentication.Status);
        await using IOctoberOperatorSession freshOtherOperatorSession =
            Assert.IsAssignableFrom<IOctoberOperatorSession>(freshAuthentication.Session);
        Assert.Equal(OctoberRevocationStatus.Denied,
            await fixture.Authority.RevokeSourceApprovalAsync(
                freshOtherOperatorSession, TargetId, "unknown-approval-identity", CancellationToken.None));

        Assert.Equal(OctoberRevocationStatus.Denied,
            await fixture.Authority.RevokeSourceApprovalAsync(
                ownerSession, TargetId, "unknown-approval-identity", CancellationToken.None));
        Assert.Equal(OctoberRevocationStatus.Revoked,
            await fixture.Authority.RevokeSourceApprovalAsync(
                ownerSession, TargetId, approval.ApprovalIdentity, CancellationToken.None));

        await using AsyncServiceScope verificationScope = fixture.Provider.CreateAsyncScope();
        OctoberDbContext october = verificationScope.ServiceProvider.GetRequiredService<OctoberDbContext>();
        OctoberPreparedSourceApprovalRecord row = Assert.Single(await october.PreparedSourceApprovals
            .Where(value => value.BatchId == committed.BatchId &&
                value.RepositoryId == fixture.RepositoryId &&
                value.ApprovalIdentity == approval.ApprovalIdentity)
            .ToListAsync());
        Assert.True(row.IsRevoked);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task VerifySourceApprovalAsync_RejectsMismatchedTupleAndAuthorityWithoutPayload()
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        IOctoberHistoricalBoundSetupData boundSetup =
            (await CreateCommittedBoundSetupAsync(fixture, session)).BoundSetup;
        OctoberSourceApprovalResult issued = await fixture.Authority.IssueSourceApprovalAsync(
            session, boundSetup, fixture.RepositoryId, CancellationToken.None);
        Assert.Equal(OctoberSourceApprovalStatus.Issued, issued.Status);
        PreparedSourceApproval approval = Assert.IsAssignableFrom<PreparedSourceApproval>(issued.Approval);

        PreparedSourceApproval[] mismatches =
        [
            approval with { BatchId = Guid.NewGuid() },
            approval with { RepositoryId = Guid.NewGuid() },
            approval with { SubjectId = SubjectId + "-other" },
            approval with { Purpose = (PreparedSourcePurpose)int.MaxValue },
            approval with { ApprovalIdentity = "unknown-approval-identity" },
            approval with { RootBindingIdentity = approval.RootBindingIdentity + "-old" },
            approval with { OperatorLabel = approval.OperatorLabel + "-other" },
        ];

        foreach (PreparedSourceApproval mismatch in mismatches)
        {
            PreparedSourceAuthorityResult rejected = await fixture.Authority.VerifySourceApprovalAsync(
                session, TargetId, mismatch, CancellationToken.None);
            Assert.Equal(PreparedSourceAuthorityStatus.Revoked, rejected.Status);
            Assert.Null(rejected.CurrentApproval);
        }

        PreparedSourceAuthorityResult wrongTarget = await fixture.Authority.VerifySourceApprovalAsync(
            session, TargetId + "-other", approval, CancellationToken.None);
        Assert.Equal(PreparedSourceAuthorityStatus.Revoked, wrongTarget.Status);
        Assert.Null(wrongTarget.CurrentApproval);

        ControlledCredentialSource foreignCredentials = new(
            new OctoberOperatorCredentials(UserName, Password));
        await using ServiceProvider foreignProvider = CreateAuthorityProvider(
            fixture.Connection,
            fixture.IdentityDatabase,
            foreignCredentials,
            fixture.Clock,
            authorityId: "october-foreign-approval-authority");
        await using AsyncServiceScope foreignScope = foreignProvider.CreateAsyncScope();
        IOctoberLocalAuthority foreignAuthority = foreignScope.ServiceProvider
            .GetRequiredService<IOctoberLocalAuthority>();
        OctoberAuthenticationResult foreignAuthentication = await foreignAuthority.AuthenticateAsync(
            CancellationToken.None);
        Assert.Equal(OctoberAuthenticationStatus.Authenticated, foreignAuthentication.Status);
        await using IOctoberOperatorSession foreignSession =
            Assert.IsAssignableFrom<IOctoberOperatorSession>(foreignAuthentication.Session);
        PreparedSourceAuthorityResult foreignAuthorityResult = await foreignAuthority.VerifySourceApprovalAsync(
            foreignSession, TargetId, approval, CancellationToken.None);
        Assert.Equal(PreparedSourceAuthorityStatus.Revoked, foreignAuthorityResult.Status);
        Assert.Null(foreignAuthorityResult.CurrentApproval);

        PreparedSourceAuthorityResult unchanged = await fixture.Authority.VerifySourceApprovalAsync(
            session, TargetId, approval, CancellationToken.None);
        Assert.Equal(PreparedSourceAuthorityStatus.Current, unchanged.Status);
        Assert.Equal(approval, unchanged.CurrentApproval);
    }

    private static async Task<(OctoberCommittedBatch Batch, IOctoberHistoricalBoundSetupData BoundSetup)>
        CreateCommittedBoundSetupAsync(
            SelectionFixture fixture,
            IOctoberOperatorSession session,
            IOctoberLocalAuthority? authorityOverride = null)
    {
        IOctoberLocalAuthority authority = authorityOverride ?? fixture.Authority;
        OctoberAuthoritySelectionResult authorization = await authority.AuthorizeSelectionAsync(
            session, fixture.CreateRequest(), CancellationToken.None);
        Assert.Equal(OctoberAuthorityDecisionStatus.Authorized, authorization.Status);
        OctoberSetupRequest request = Assert.IsAssignableFrom<OctoberSetupRequest>(authorization.Request);
        OctoberAuthorizedSelection trustedSelection =
            Assert.IsAssignableFrom<OctoberAuthorizedSelection>(authorization.Authority);

        await using AsyncServiceScope writerScope = fixture.Provider.CreateAsyncScope();
        IOctoberSetupWriter writer = writerScope.ServiceProvider.GetRequiredService<IOctoberSetupWriter>();
        OctoberPrepareResult prepared = await new OctoberBatchSetupPolicy(writer).PrepareAsync(
            request, trustedSelection, CancellationToken.None);
        Assert.Equal(OctoberPrepareStatus.Created, prepared.Status);
        OctoberCommittedBatch committed = Assert.IsAssignableFrom<OctoberCommittedBatch>(prepared.CommittedBatch);

        await using AsyncServiceScope selectorScope = fixture.Provider.CreateAsyncScope();
        OctoberDbContext october = selectorScope.ServiceProvider.GetRequiredService<OctoberDbContext>();
        OctoberBatchSelector selector = new(
            october, TargetId, "sqlite://owned/october-authentication-target");
        OctoberSelectionResult selected = await selector.BindAsync(
            new OctoberStartupSelection(TargetId, committed.BatchId, trustedSelection),
            CancellationToken.None);
        Assert.Equal(OctoberSelectionStatus.Selected, selected.Status);
        IOctoberHistoricalBoundSetupData boundSetup = Assert.IsAssignableFrom<IOctoberHistoricalBoundSetupData>(
            selected.BoundSetupData);
        return (committed, boundSetup);
    }

    private static async Task AssertApprovalRevokedAfterIdentityChangeAsync(
        Func<UserManager<ApplicationUser>, ApplicationUser, Task> change)
    {
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        IOctoberHistoricalBoundSetupData boundSetup =
            (await CreateCommittedBoundSetupAsync(fixture, session)).BoundSetup;
        OctoberSourceApprovalResult issued = await fixture.Authority.IssueSourceApprovalAsync(
            session, boundSetup, fixture.RepositoryId, CancellationToken.None);
        Assert.Equal(OctoberSourceApprovalStatus.Issued, issued.Status);
        PreparedSourceApproval approval = Assert.IsAssignableFrom<PreparedSourceApproval>(issued.Approval);

        await using (AsyncServiceScope identityScope = fixture.Provider.CreateAsyncScope())
        {
            UserManager<ApplicationUser> users = identityScope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = Assert.IsAssignableFrom<ApplicationUser>(
                await users.FindByIdAsync(SubjectId));
            await change(users, user);
        }

        PreparedSourceAuthorityResult rejected = await fixture.Authority.VerifySourceApprovalAsync(
            session, TargetId, approval, CancellationToken.None);
        Assert.Equal(PreparedSourceAuthorityStatus.Revoked, rejected.Status);
        Assert.Null(rejected.CurrentApproval);
    }

    private sealed class ApprovalIdentityFixture : IAsyncDisposable
    {
        private readonly SelectionFixture _selectionFixture;
        private readonly OctoberOwnedIdentityFixture _alternateIdentityDatabase;
        private readonly ServiceProvider _alternateIdentityProvider;
        private readonly SwitchableIdentityScopeFactory _scopes;

        private ApprovalIdentityFixture(
            SelectionFixture selectionFixture,
            OctoberOwnedIdentityFixture alternateIdentityDatabase,
            ServiceProvider alternateIdentityProvider,
            SwitchableIdentityScopeFactory scopes,
            IOctoberLocalAuthority authority)
        {
            _selectionFixture = selectionFixture;
            _alternateIdentityDatabase = alternateIdentityDatabase;
            _alternateIdentityProvider = alternateIdentityProvider;
            _scopes = scopes;
            Authority = authority;
        }

        public ServiceProvider Provider => _selectionFixture.Provider;
        public SqliteConnection Connection => _selectionFixture.Connection;
        public OctoberOwnedIdentityFixture IdentityDatabase => _selectionFixture.IdentityDatabase;
        public ControlledCredentialSource CredentialSource => _selectionFixture.CredentialSource;
        public FixedTimeProvider Clock => _selectionFixture.Clock;
        public IOctoberLocalAuthority Authority { get; }
        public string ConfiguredIdentityDatabaseName => IdentityDatabase.DatabaseName;
        public string AlternateIdentityDatabaseName => _alternateIdentityDatabase.DatabaseName;
        public Guid? SystemId => _selectionFixture.SystemId;
        public Guid RepositoryId => _selectionFixture.RepositoryId;
        public Guid GlobalContextId => _selectionFixture.GlobalContextId;

        public static async Task<ApprovalIdentityFixture> CreateAsync()
        {
            SelectionFixture selectionFixture = await SelectionFixture.CreateAsync();
            OctoberOwnedIdentityFixture? alternateIdentityDatabase = null;
            ServiceProvider? alternateIdentityProvider = null;
            try
            {
                alternateIdentityDatabase = await OctoberOwnedIdentityFixture.CreateAsync();
                alternateIdentityProvider = CreateAuthorityProvider(
                    selectionFixture.Connection,
                    alternateIdentityDatabase,
                    selectionFixture.CredentialSource,
                    selectionFixture.Clock);
                string subjectSecurityStamp;
                await using (AsyncServiceScope identityScope = selectionFixture.Provider.CreateAsyncScope())
                {
                    UserManager<ApplicationUser> users = identityScope.ServiceProvider
                        .GetRequiredService<UserManager<ApplicationUser>>();
                    ApplicationUser user = Assert.IsAssignableFrom<ApplicationUser>(
                        await users.FindByIdAsync(SubjectId));
                    subjectSecurityStamp = await users.GetSecurityStampAsync(user);
                }

                await SeedMatchingIdentityAsync(alternateIdentityProvider, subjectSecurityStamp);

                SwitchableIdentityScopeFactory scopes = new(selectionFixture.Provider, alternateIdentityProvider);
                OctoberLocalAuthority authority = new(
                    scopes,
                    CreateConfiguration(selectionFixture.Connection.ConnectionString,
                        selectionFixture.IdentityDatabase),
                    selectionFixture.CredentialSource,
                    selectionFixture.Clock);
                return new ApprovalIdentityFixture(selectionFixture, alternateIdentityDatabase,
                    alternateIdentityProvider, scopes, authority);
            }
            catch
            {
                try
                {
                    if (alternateIdentityProvider is not null)
                        await alternateIdentityProvider.DisposeAsync();
                }
                finally
                {
                    try
                    {
                        if (alternateIdentityDatabase is not null)
                            await alternateIdentityDatabase.DisposeAsync();
                    }
                    finally
                    {
                        await selectionFixture.DisposeAsync();
                    }
                }
                throw;
            }
        }

        public void UseAlternateIdentityDatabase() => _scopes.UseAlternateIdentityDatabase();
        public void UseConfiguredIdentityDatabase() => _scopes.UseConfiguredIdentityDatabase();

        public async Task<IOctoberOperatorSession> AuthenticateAsync()
        {
            CredentialSource.Credentials = new OctoberOperatorCredentials(UserName, Password);
            OctoberAuthenticationResult authentication = await Authority.AuthenticateAsync(CancellationToken.None);
            Assert.Equal(OctoberAuthenticationStatus.Authenticated, authentication.Status);
            return Assert.IsAssignableFrom<IOctoberOperatorSession>(authentication.Session);
        }

        public async Task<IOctoberHistoricalBoundSetupData> CreateCommittedBoundSetupAsync(
            IOctoberOperatorSession session)
        {
            return (await OctoberLocalAuthorityTests.CreateCommittedBoundSetupAsync(
                _selectionFixture, session, Authority)).BoundSetup;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await _alternateIdentityProvider.DisposeAsync();
            }
            finally
            {
                try
                {
                    await _alternateIdentityDatabase.DisposeAsync();
                }
                finally
                {
                    await _selectionFixture.DisposeAsync();
                }
            }
        }

        private static async Task SeedMatchingIdentityAsync(ServiceProvider provider, string securityStamp)
        {
            await using AsyncServiceScope scope = provider.CreateAsyncScope();
            ApplicationDbContext identity = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await identity.Database.MigrateAsync();

            RoleManager<IdentityRole> roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            IdentityResult roleCreated = await roles.CreateAsync(new IdentityRole(RoleNames.SystemOwner));
            Assert.True(roleCreated.Succeeded, string.Join("; ", roleCreated.Errors.Select(error => error.Code)));

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
            user.SecurityStamp = securityStamp;
            IdentityResult stampSet = await users.UpdateAsync(user);
            Assert.True(stampSet.Succeeded, string.Join("; ", stampSet.Errors.Select(error => error.Code)));
            Assert.Equal(securityStamp, await users.GetSecurityStampAsync(user));
            IdentityResult roleAssigned = await users.AddToRoleAsync(user, RoleNames.SystemOwner);
            Assert.True(roleAssigned.Succeeded, string.Join("; ", roleAssigned.Errors.Select(error => error.Code)));
        }

        private sealed class SwitchableIdentityScopeFactory(
            ServiceProvider configuredIdentityProvider,
            ServiceProvider alternateIdentityProvider) : IServiceScopeFactory
        {
            private int _useAlternateIdentity;

            public IServiceScope CreateScope()
            {
                ServiceProvider provider = Volatile.Read(ref _useAlternateIdentity) == 0
                    ? configuredIdentityProvider
                    : alternateIdentityProvider;
                return provider.GetRequiredService<IServiceScopeFactory>().CreateScope();
            }

            public void UseAlternateIdentityDatabase() => Volatile.Write(ref _useAlternateIdentity, 1);
            public void UseConfiguredIdentityDatabase() => Volatile.Write(ref _useAlternateIdentity, 0);
        }
    }
}
