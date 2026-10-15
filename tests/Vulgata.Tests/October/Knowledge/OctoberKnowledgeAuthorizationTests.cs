using System.Collections.Immutable;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;
using RepositoryEntity = Vulgata.Core.Entities.Repository;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests.October.Knowledge;

public sealed class OctoberKnowledgeAuthorizationTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ContextOverride_ExplicitPublicationAdvisoryVersions_RequireExactTupleAndDigestAsync()
    {
        await using var fixture = await B0bIdentityFixture.CreateAsync();
        var revised = fixture.Advisory with { ContentDigest = B0bIdentityFixture.Digest("Run-scoped revised interpretation.") };
        fixture.Selection.Snapshot = fixture.Selection.Snapshot! with
        {
            Advisory = fixture.Selection.Snapshot!.Advisory.Add(new(fixture.RepositoryId, revised.Kind,
                revised.AdvisoryRecordId!.Value, revised.ContentDigest))
        };
        var request = fixture.Request() with { AllContributors = [fixture.Source, fixture.Advisory, revised] };
        Assert.Equal(DisclosureStatus.Allowed, await fixture.Policy.CheckAsync(request));
        Assert.Equal(DisclosureStatus.Denied, await fixture.Policy.CheckAsync(request with
        { AllContributors = request.AllContributors.Add(revised with { ContentDigest = B0bIdentityFixture.Digest("Not selected") }) }));
        Assert.Equal(DisclosureStatus.Denied, await fixture.Policy.CheckAsync(request with
        { AllContributors = [fixture.Source, revised with { AdvisoryRecordId = Guid.NewGuid() }] }));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckAsync_OrdinaryUserWithoutManagementAssignment_AllowsExactReadAndNonemptyEditedAdvisoryAsync()
    {
        await using var fixture = await B0bIdentityFixture.CreateAsync();
        Assert.Equal(DisclosureStatus.Allowed, await fixture.Policy.CheckAsync(fixture.Request()));
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            await using var context = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<VulgataDbContext>>().CreateDbContextAsync();
            Assert.Equal(0, await context.SystemOwnerAssignments.CountAsync());
            var repository = await context.Repositories.SingleAsync();
            repository.UpdateDetails(repository.Name, repository.GitUrl, repository.Description, "Edited live context, historical selection remains exact.", DateTimeOffset.UtcNow);
            await context.SaveChangesAsync();
        }

        Assert.Equal(DisclosureStatus.Allowed, await fixture.Policy.CheckAsync(fixture.Request()));
        Assert.Equal(B0bIdentityFixture.Digest(B0bIdentityFixture.OriginalContext), fixture.Selection.Snapshot!.Advisory[0].ContentDigest);
        var revised = fixture.Advisory with { ContentDigest = B0bIdentityFixture.Digest("New accepted context revision") };
        var request = fixture.Request() with { AllContributors = [fixture.Advisory, revised] };
        Assert.Equal(DisclosureStatus.Denied, await fixture.Policy.CheckAsync(request));
        fixture.Selection.Snapshot = fixture.Selection.Snapshot with
        {
            Advisory = fixture.Selection.Snapshot.Advisory.Add(new(fixture.RepositoryId, revised.Kind, revised.AdvisoryRecordId!.Value, revised.ContentDigest))
        };
        Assert.Equal(DisclosureStatus.Allowed, await fixture.Policy.CheckAsync(request));
        Assert.Equal(DisclosureStatus.Denied, await fixture.Policy.CheckAsync(request with
        {
            AllContributors = [revised with { ContentDigest = new string('e', 64) }]
        }));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("role")]
    [InlineData("stamp")]
    [InlineData("lockout")]
    [InlineData("deleted-user")]
    [InlineData("foreign-principal")]
    public async Task CheckAsync_AccountChanges_UsesFreshIdentityInsteadOfCachedPrincipalClaimsAsync(string change)
    {
        await using var fixture = await B0bIdentityFixture.CreateAsync();
        Assert.Equal(DisclosureStatus.Allowed, await fixture.Policy.CheckAsync(fixture.Request()));
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(fixture.Subject.SubjectId))!;
            if (change == "role") Assert.True((await users.RemoveFromRoleAsync(user, "User")).Succeeded);
            if (change == "stamp") Assert.True((await users.UpdateSecurityStampAsync(user)).Succeeded);
            if (change == "lockout") Assert.True((await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddHours(1))).Succeeded);
            if (change == "deleted-user") Assert.True((await users.DeleteAsync(user)).Succeeded);
            if (change == "foreign-principal")
                fixture.Principal =
                    new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "foreign-user"), new("AspNet.Identity.SecurityStamp", fixture.Subject.AuthenticationBinding)],
                        IdentityConstants.ApplicationScheme));
        }

        Assert.Equal(DisclosureStatus.Denied, await fixture.Policy.CheckAsync(fixture.Request()));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("deleted-repository")]
    [InlineData("moved-repository")]
    [InlineData("cleared-repository-context")]
    [InlineData("deleted-global-context")]
    public async Task CheckAsync_ContributorChanges_DeniesCompositeEvenWhenPublicCitationRemainsAllowedAsync(string change)
    {
        await using var fixture = await B0bIdentityFixture.CreateAsync();
        var request = fixture.Request() with { AllContributors = [fixture.Source, fixture.Advisory, fixture.GlobalAdvisory] };
        Assert.Equal(DisclosureStatus.Allowed, await fixture.Policy.CheckAsync(request));
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            await using var context = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<VulgataDbContext>>().CreateDbContextAsync();
            var repository = await context.Repositories.SingleAsync();
            if (change == "deleted-repository") context.Repositories.Remove(repository);
            if (change == "moved-repository")
            {
                var replacement = new SystemEntity("Other system", null, "Other context", DateTimeOffset.UtcNow);
                context.Systems.Add(replacement);
                context.Entry(repository).Property(nameof(RepositoryEntity.SystemId)).CurrentValue = replacement.Id;
            }

            if (change == "cleared-repository-context") repository.UpdateDetails(repository.Name, repository.GitUrl, repository.Description, null, DateTimeOffset.UtcNow);
            if (change == "deleted-global-context") context.GlobalContexts.Remove(await context.GlobalContexts.SingleAsync());
            await context.SaveChangesAsync();
        }

        Assert.Equal(DisclosureStatus.Denied, await fixture.Policy.CheckAsync(request));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckAsync_ExactPublicationPurposeAndContributorScope_FailsClosedWithoutInventedManagementGateAsync()
    {
        await using var fixture = await B0bIdentityFixture.CreateAsync();
        var valid = fixture.Request();
        Assert.Equal(DisclosureStatus.Allowed, await fixture.Policy.CheckAsync(valid));
        Assert.Equal(DisclosureStatus.Denied, await fixture.Policy.CheckAsync(valid with { Selection = valid.Selection with { Purpose = KnowledgePurpose.Resolution } }));
        Assert.Equal(DisclosureStatus.Denied, await fixture.Policy.CheckAsync(valid with { Selection = valid.Selection with { Purpose = KnowledgePurpose.Synthesis } }));
        Assert.Equal(DisclosureStatus.Denied, await fixture.Policy.CheckAsync(valid with { Selection = valid.Selection with { Mode = KnowledgeMode.Business } }));
        Assert.Equal(DisclosureStatus.Denied, await fixture.Policy.CheckAsync(valid with { Selection = valid.Selection with { RepositoryScope = [] } }));
        Assert.Equal(DisclosureStatus.Denied, await fixture.Policy.CheckAsync(valid with { Selection = valid.Selection with { RepositoryScope = [Guid.NewGuid()] } }));
        Assert.Equal(DisclosureStatus.Denied,
            await fixture.Policy.CheckAsync(valid with
            {
                AllContributors = valid.AllContributors.Add(fixture.Source with { RepositoryId = Guid.NewGuid() }), Kind = DocumentKind.BusinessLogic
            }));
        Assert.Equal(DisclosureStatus.Denied, await fixture.Policy.CheckAsync(valid with { AllContributors = [fixture.Advisory with { ContentDigest = new string('f', 64) }] }));
        fixture.Selection.Snapshot = null;
        Assert.Equal(DisclosureStatus.Denied, await fixture.Policy.CheckAsync(valid));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("Administrator")]
    [InlineData("SystemOwner")]
    public async Task CheckAsync_OrdinarySupportedRole_DoesNotRequireManagementAssignmentAsync(string role)
    {
        await using var fixture = await B0bIdentityFixture.CreateAsync();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            Assert.True((await roles.CreateAsync(new(role))).Succeeded);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(fixture.Subject.SubjectId))!;
            Assert.True((await users.RemoveFromRoleAsync(user, "User")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
            await using var context = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<VulgataDbContext>>().CreateDbContextAsync();
            Assert.Empty(await context.SystemOwnerAssignments.ToListAsync());
        }

        Assert.Equal(DisclosureStatus.Allowed, await fixture.Policy.CheckAsync(fixture.Request()));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckAsync_SourceOmitsSystem_StillRejectsRepositoryMovedAwayFromSelectedSystemAsync()
    {
        await using var fixture = await B0bIdentityFixture.CreateAsync();
        var request = fixture.Request() with { AllContributors = [fixture.Source with { SystemId = null }] };
        Assert.Equal(DisclosureStatus.Allowed, await fixture.Policy.CheckAsync(request));
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            await using var context = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<VulgataDbContext>>().CreateDbContextAsync();
            var repository = await context.Repositories.SingleAsync();
            context.Entry(repository).Property(nameof(RepositoryEntity.SystemId)).CurrentValue = null;
            await context.SaveChangesAsync();
        }

        Assert.Equal(DisclosureStatus.Denied, await fixture.Policy.CheckAsync(request));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckAsync_SelectedStandaloneRepository_AllowsWithoutInventingSystemRequirementAsync()
    {
        await using var fixture = await B0bIdentityFixture.CreateAsync();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            await using var context = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<VulgataDbContext>>().CreateDbContextAsync();
            var repository = await context.Repositories.SingleAsync();
            context.Entry(repository).Property(nameof(RepositoryEntity.SystemId)).CurrentValue = null;
            await context.SaveChangesAsync();
            context.Systems.Remove(await context.Systems.SingleAsync());
            await context.SaveChangesAsync();
        }

        fixture.Selection.Snapshot = fixture.Selection.Snapshot! with { Repositories = [new(fixture.RepositoryId, null, "controlled-root")] };
        var request = fixture.Request() with { AllContributors = [fixture.Source with { SystemId = null }, fixture.Advisory with { SystemId = null }] };
        Assert.Equal(DisclosureStatus.Allowed, await fixture.Policy.CheckAsync(request));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckAsync_SystemAdvisoryEditThenClear_RetainsHistoricalHashUntilLiveContextRemovedAsync()
    {
        await using var fixture = await B0bIdentityFixture.CreateAsync();
        var contributor = new Contributor(ContributorKind.SystemAdvisory, null, fixture.SystemId, null, fixture.SystemId, B0bIdentityFixture.Digest("System context"));
        fixture.Selection.Snapshot = fixture.Selection.Snapshot! with
        {
            Advisory = fixture.Selection.Snapshot!.Advisory.Add(new(fixture.RepositoryId, ContributorKind.SystemAdvisory, fixture.SystemId, contributor.ContentDigest))
        };
        var request = fixture.Request() with { AllContributors = [fixture.Source, contributor] };
        Assert.Equal(DisclosureStatus.Allowed, await fixture.Policy.CheckAsync(request));
        foreach (string? contextText in new[] { "Edited nonempty system context", null })
        {
            await using (var scope = fixture.Services.CreateAsyncScope())
            {
                await using var context = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<VulgataDbContext>>().CreateDbContextAsync();
                var system = await context.Systems.SingleAsync();
                system.UpdateDetails(system.Name, system.Description, contextText, DateTimeOffset.UtcNow);
                await context.SaveChangesAsync();
            }

            Assert.Equal(contextText is null ? DisclosureStatus.Denied : DisclosureStatus.Allowed, await fixture.Policy.CheckAsync(request));
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckAsync_AuthorityOutageOrCallerCancellation_DistinguishesFailClosedFromCancelledAsync()
    {
        await using var fixture = await B0bIdentityFixture.CreateAsync();
        fixture.Selection.Fail = true;
        Assert.Equal(DisclosureStatus.AuthorityUnavailable, await fixture.Policy.CheckAsync(fixture.Request()));
        fixture.Selection.Fail = false;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Policy.CheckAsync(fixture.Request(), cancellation.Token));
    }
}

/// <summary>Real Identity/management EF stores on two owned SQLite files; the publication snapshot is explicitly controlled.</summary>
internal sealed class B0bIdentityFixture : IAsyncDisposable
{
    private readonly string _directory;

    private B0bIdentityFixture(string directory, ServiceProvider services)
    {
        _directory = directory;
        Services = services;
    }

    public const string OriginalContext = "Original repository advisory context.";
    public ServiceProvider Services { get; }
    public ClaimsPrincipal? Principal { get; set; }
    public KnowledgeSubject Subject { get; private set; } = null!;
    public PublicationBinding Publication { get; private set; } = null!;
    public Guid RepositoryId { get; private set; }
    public Guid SystemId { get; private set; }
    public Guid GlobalId { get; private set; }
    public B0bSelectionAuthority Selection { get; } = new();
    public IOctoberKnowledgeDisclosurePolicy Policy { get; private set; } = null!;
    public Contributor Source => new(ContributorKind.Source, RepositoryId, SystemId, "controlled-root", null, new string('b', 64));
    public Contributor Advisory => new(ContributorKind.RepositoryAdvisory, RepositoryId, SystemId, null, RepositoryId, Digest(OriginalContext));
    public Contributor GlobalAdvisory => new(ContributorKind.GlobalAdvisory, null, null, null, GlobalId, Digest("Original global context."));
    public DisclosureRequest Request() => new(new(Publication, Subject, KnowledgeMode.IT, KnowledgePurpose.Reader, [RepositoryId]), [Source, Advisory], DocumentKind.CodeLogic);
    public static string Digest(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    public static async Task<B0bIdentityFixture> CreateAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "vulgata-b0b-identity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddCookie(IdentityConstants.ApplicationScheme);
        services.AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite($"Data Source={Path.Combine(directory, "identity.db")};Pooling=False"));
        services.AddDbContextFactory<VulgataDbContext>(options => options.UseSqlite($"Data Source={Path.Combine(directory, "management.db")};Pooling=False"));
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddSignInManager();
        var provider = services.BuildServiceProvider();
        var fixture = new B0bIdentityFixture(directory, provider);
        try
        {
            await using var scope = provider.CreateAsyncScope();
            await using (var identity = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync())
                await identity.Database.EnsureCreatedAsync();
            await using (var management = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<VulgataDbContext>>().CreateDbContextAsync())
            {
                await management.Database.EnsureCreatedAsync();
                var system = new SystemEntity("Knowledge system", null, "System context", DateTimeOffset.UtcNow);
                var repository = RepositoryEntity.Create(system.Id, "Knowledge repository", "https://fixture.invalid/repository.git", null, OriginalContext, DateTimeOffset.UtcNow);
                var global = new Vulgata.Core.Entities.GlobalContext("Original global context.", DateTimeOffset.UtcNow);
                management.Systems.Add(system);
                management.Repositories.Add(repository);
                management.GlobalContexts.Add(global);
                await management.SaveChangesAsync();
                fixture.RepositoryId = repository.Id;
                fixture.SystemId = system.Id;
                fixture.GlobalId = global.Id;
            }

            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            Assert.True((await roles.CreateAsync(new("User"))).Succeeded);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = "ordinary-reader", Email = "reader@fixture.invalid", EmailConfirmed = true, LockoutEnabled = true };
            Assert.True((await users.CreateAsync(user)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, "User")).Succeeded);
            fixture.Subject = new(user.Id, (await users.GetSecurityStampAsync(user))!);
            // A cached Administrator claim must not override the actual live User role or later removal.
            fixture.Principal =
                new(new ClaimsIdentity(
                    [new(ClaimTypes.NameIdentifier, user.Id), new("AspNet.Identity.SecurityStamp", fixture.Subject.AuthenticationBinding), new(ClaimTypes.Role, "Administrator")],
                    IdentityConstants.ApplicationScheme));
            fixture.Publication = new(new("ControlledFixture/live-authority", "reader-target", Guid.NewGuid(), new string('a', 64)), Guid.NewGuid());
            fixture.Selection.Snapshot = new(fixture.Publication, [new(fixture.RepositoryId, fixture.SystemId, "controlled-root")],
            [
                new(fixture.RepositoryId, ContributorKind.RepositoryAdvisory, fixture.RepositoryId, Digest(OriginalContext)),
                new(fixture.RepositoryId, ContributorKind.GlobalAdvisory, fixture.GlobalId, Digest("Original global context."))
            ]);
            var live = new OctoberKnowledgeIdentityAdapter(provider.GetRequiredService<IServiceScopeFactory>(), _ => Task.FromResult(fixture.Principal));
            fixture.Policy = new OctoberKnowledgeDisclosurePolicy(live, fixture.Selection);
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        Directory.Delete(_directory, true);
    }
}

internal sealed class B0bSelectionAuthority : IOctoberKnowledgeSelectionAuthority
{
    public KnowledgeSelectionAuthoritySnapshot? Snapshot { get; set; }
    public bool Fail { get; set; }

    public Task<KnowledgeSelectionAuthoritySnapshot?> LoadAsync(PublicationBinding publication, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Fail) throw new IOException("Controlled authority outage.");
        return Task.FromResult(Snapshot?.Publication == publication ? Snapshot : null);
    }
}
