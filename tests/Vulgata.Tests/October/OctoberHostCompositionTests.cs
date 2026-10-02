using System.Collections.Immutable;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Vulgata.Core.Entities;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.Data.October;
using Vulgata.OctoberGenerator;
using Vulgata.Shared;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests.October;

public sealed class OctoberHostCompositionTests
{
    private const string TargetId = "october-host-target-a";
    private const string CanonicalDatabaseIdentity = "sqlite://owned/october-host-target-a";
    private const string ExpectedManagementAuthorityId = "ordinary-management-a";
    private const string OtherManagementAuthorityId = "ordinary-management-b";
    private const string SubjectId = "october-host-subject";
    private const string OperatorLabel = "october-host-operator";
    private static readonly Guid BatchId = Guid.Parse("e3576576-f6ca-49e0-9ec6-aa784c5bab64");
    private static readonly Guid RepositoryId = Guid.Parse("a579c66a-2b3d-476f-9f2d-abdb90066289");
    private static readonly Guid SystemId = Guid.Parse("b268fa5c-d81b-4a88-a4c3-94e347348d4e");
    private static readonly Guid GlobalId = Guid.Parse("72e41d20-e11f-4df8-b46a-cdfc6288a7ae");
    private static readonly DateTimeOffset FirstCommitUtc =
        new(2026, 9, 30, 11, 20, 0, TimeSpan.Zero);
    private const string SelectedWebTargetId = "october-host-target-b";
    private const string SelectedWebAuthorityId = "october-host-management-b";
    private const string SelectedWebCanonicalDatabaseIdentity =
        "postgres://owned-test/october-host-target-b";
    private const string SelectedWebUserName = "october-host-operator@example.test";
    private const string SelectedWebPassword = "OctoberHost!Passw0rd8";
    private const string BaselineWebNpgsqlConnection =
        "Host=localhost;Port=5432;Database=october_host_baseline_a;Username=test;Password=test-only";
    private const string SelectedWebNpgsqlConnection =
        "Host=localhost;Port=5432;Database=october_host_selected_b;Username=test;Password=test-only";
    private static readonly Guid SelectedWebBatchId =
        Guid.Parse("2746c8b4-bd3a-4fc6-a603-6bacda77be11");

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_PrepareRunsLocalAuthorityThenIdentityMigrationAndPolicy()
    {
        SelectionBundle selection = CreateSelection();
        OctoberSetupRequest callerRequest = selection.Request with { SelectionDigest = string.Empty };
        GeneratorRig rig = CreateRig(selection);
        OctoberGeneratorCommand command = new(
            OctoberGeneratorMode.Prepare,
            TargetId,
            BatchId,
            callerRequest);

        OctoberGeneratorExecutionResult result = await rig.Module.ExecuteAsync(command);

        Assert.Equal(OctoberGeneratorExecutionStatus.Prepared, result.Status);
        Assert.Equal(
            new[] { "authenticate", "authorize-selection", "target-identity", "migrate", "write" },
            rig.Events.ToArray());
        Assert.Equal(TargetId, rig.Identity.LastTargetId);
        Assert.Equal(CanonicalDatabaseIdentity, rig.Identity.LastCanonicalIdentity);
        Assert.NotEqual(callerRequest.SelectionDigest, selection.Request.SelectionDigest);
        Assert.Equal(selection.Request.TargetId, rig.Writer.LastRequest!.TargetId);
        Assert.Equal(selection.Request.BatchId, rig.Writer.LastRequest.BatchId);
        Assert.Equal(selection.Request.SelectionDigest, rig.Writer.LastRequest.SelectionDigest);
        Assert.Equal(selection.Authority.SelectionDigest, rig.Writer.LastAuthority!.SelectionDigest);
        Assert.Equal(OctoberPrepareStatus.Created, rig.Writer.LastResult.Status);
        Assert.Equal(FirstCommitUtc, result.CommittedBatch!.SelectedAtUtc);
        Assert.Null(result.BoundSetupData);
        Assert.Equal(0, rig.Selector.CallCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_CancellationAfterAuthorizedSelection_DoesNotReachTargetOrWrite()
    {
        SelectionBundle selection = CreateSelection();
        using CancellationTokenSource cancellation = new();
        GeneratorRig rig = CreateRig(selection, cancelAfterAuthorization: cancellation);
        OctoberGeneratorCommand command = new(
            OctoberGeneratorMode.Prepare,
            TargetId,
            BatchId,
            selection.Request);

        OctoberGeneratorExecutionResult result = await rig.Module.ExecuteAsync(
            command, cancellation.Token);

        Assert.Equal(OctoberGeneratorExecutionStatus.Cancelled, result.Status);
        Assert.Equal(new[] { "authenticate", "authorize-selection" }, rig.Events.ToArray());
        Assert.Null(result.CommittedBatch);
        Assert.Null(result.BoundSetupData);
        Assert.Equal(0, rig.Identity.CallCount);
        Assert.Equal(0, rig.Migrator.CallCount);
        Assert.Equal(0, rig.Writer.CallCount);
        Assert.Equal(0, rig.Selector.CallCount);
        Assert.True(rig.Authority.LastSession.IsDisposed,
            "The authenticated session must be disposed when cancellation ends the command.");
    }



    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SelectedDataStartup_RejectsInvalidProviderDocumentBeforeAuthentication()
    {
        using OwnedProviderDocument providerDocument =
            await OwnedProviderDocument.CreateAsync("unknown-api-type");
        using LoginLogoutTests.CustomWebApplicationFactory ordinaryBaseFactory = new();
        using WebApplicationFactory<Program> ordinaryFactory = ordinaryBaseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["October:Providers:ConfigurationFile"] = providerDocument.ConfigurationFilePath
                })));
        using HttpClient ordinaryClient = ordinaryFactory.CreateClient();
        using (IServiceScope scope = ordinaryFactory.Services.CreateScope())
        {
            Assert.Equal(OctoberWebMode.Ordinary,
                scope.ServiceProvider.GetRequiredService<OctoberWebStartupSelection>().Mode);
        }

        SelectedWebStartupAttempt attempt = await AttemptSelectedDataStartupAsync(providerDocument);
        AssertRejectedProviderDocument(attempt, providerDocument);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("duplicate-root-property")]
    [InlineData("unknown-root-property")]
    [InlineData("duplicate-profile-property")]
    [InlineData("unknown-profile-property")]
    public async Task SelectedDataStartup_RejectsDuplicateOrUnknownProviderPropertiesAtomically(
        string schemaViolation)
    {
        using OwnedProviderDocument providerDocument =
            await OwnedProviderDocument.CreateAsync(schemaViolation);

        SelectedWebStartupAttempt attempt = await AttemptSelectedDataStartupAsync(providerDocument);

        AssertRejectedProviderDocument(attempt, providerDocument);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SelectedDataStartup_RejectsDistinctProviderFileSelectorsBeforeCredentialWork()
    {
        using OwnedProviderDocument providerDocument = await OwnedProviderDocument.CreateAsync();
        string competingSelectorPath = providerDocument.CreateAlternateSelectorFile();

        SelectedWebStartupAttempt attempt = await AttemptSelectedDataStartupAsync(
            providerDocument, competingSelectorPath);

        AssertRejectedProviderDocument(attempt, providerDocument);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SelectedDataStartup_RejectsCompetingProviderSourceIdentityBeforeCredentialWork()
    {
        using OwnedProviderDocument providerDocument = await OwnedProviderDocument.CreateAsync();

        SelectedWebStartupAttempt attempt = await AttemptSelectedDataStartupAsync(
            providerDocument,
            competingProviderSourceIdentity: "selected-web-provider-source-override");

        AssertRejectedProviderDocument(attempt, providerDocument);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("malformed-json")]
    [InlineData("missing-file")]
    [InlineData("wrong-required-type")]
    [InlineData("malformed-utf8-source-identity")]
    public async Task SelectedDataStartup_RejectsMalformedOrUnreadableProviderDocumentBeforeCredentialWork(
        string documentFailure)
    {
        string? documentVariant = documentFailure == "missing-file" ? null : documentFailure;
        using OwnedProviderDocument providerDocument = await OwnedProviderDocument.CreateAsync(documentVariant);
        if (documentFailure == "missing-file")
            File.Delete(providerDocument.ConfigurationFilePath);

        SelectedWebStartupAttempt attempt = await AttemptSelectedDataStartupAsync(providerDocument);

        AssertRejectedProviderDocument(attempt, providerDocument);
    }

    private static void AssertRejectedProviderDocument(
        SelectedWebStartupAttempt attempt,
        OwnedProviderDocument providerDocument)
    {
        Assert.True(attempt.DecryptionCount == 0 && attempt.CredentialReadCount == 0,
            $"Rejected provider documents must stop before credential work " +
            $"(decryptions={attempt.DecryptionCount}, credentialReads={attempt.CredentialReadCount}).");
        Exception startupFailure = Assert.IsType<InvalidOperationException>(attempt.StartupFailure);
        string failureDetails = startupFailure.ToString();
        Assert.True(failureDetails.Contains("provider", StringComparison.OrdinalIgnoreCase),
            "Selected-data startup should classify the rejected provider document.");
        Assert.True(failureDetails.Contains("configuration", StringComparison.OrdinalIgnoreCase),
            "Selected-data startup should report a sanitized configuration failure.");
        Assert.DoesNotContain(providerDocument.WorkerCiphertext, failureDetails, StringComparison.Ordinal);
        Assert.DoesNotContain(providerDocument.ChatCiphertext, failureDetails, StringComparison.Ordinal);
    }

    private static Dictionary<string, string?> CreateSelectedWebConfiguration(
        string selectedConnection,
        string? identityConnectionName = null,
        Guid? configuredBatchId = null) =>
        new(StringComparer.Ordinal)
        {
            ["ConnectionStrings:DefaultConnection"] = selectedConnection,
            ["ConnectionStrings:Identity"] = identityConnectionName is null
                ? null
                : BaselineWebNpgsqlConnection,
            ["October:Web:Mode"] = "selected-data",
            ["October:Web:TargetId"] = SelectedWebTargetId,
            ["October:Web:BatchId"] = (configuredBatchId ?? SelectedWebBatchId).ToString("D"),
            ["October:Authority:Id"] = SelectedWebAuthorityId,
            ["October:Targets:0:TargetId"] = SelectedWebTargetId,
            ["October:Targets:0:CanonicalDatabaseIdentity"] = SelectedWebCanonicalDatabaseIdentity,
            ["October:Identity:ConnectionName"] = identityConnectionName,
            ["October:Identity:CanonicalDatabaseIdentity"] = identityConnectionName is null
                ? null
                : "sqlite://owned/october-host-identity-a",
            ["October:Operator:CredentialSource"] = "console",
            ["October:Operator:CredentialFile"] = null,
            ["October:Web:ActivationGrant"] = null,
            ["October:Web:Endpoint"] = null,
            ["October:Web:ManagementAuthorityId"] = null,
            ["October:Web:AllowedTargets"] = null,
            ["October:Web:GeneratorCertificates"] = null
        };

    private static async Task SeedIdentityReplicaAsync(
        LoginLogoutTests.CustomWebApplicationFactory factory,
        string subjectId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RoleManager<IdentityRole> roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync(RoleNames.SystemOwner))
        {
            IdentityResult roleCreated = await roles.CreateAsync(new IdentityRole(RoleNames.SystemOwner));
            Assert.True(roleCreated.Succeeded,
                string.Join("; ", roleCreated.Errors.Select(error => error.Code)));
        }

        UserManager<ApplicationUser> users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = new()
        {
            Id = subjectId,
            UserName = SelectedWebUserName,
            Email = SelectedWebUserName,
            EmailConfirmed = true,
            LockoutEnabled = true
        };
        IdentityResult created = await users.CreateAsync(user, SelectedWebPassword);
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(error => error.Code)));
        IdentityResult roleAssigned = await users.AddToRoleAsync(user, RoleNames.SystemOwner);
        Assert.True(roleAssigned.Succeeded,
            string.Join("; ", roleAssigned.Errors.Select(error => error.Code)));
    }

    private static async Task<Dictionary<string, string>> GetSelectedHostLoginFieldsAsync(
        HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync("/Account/Login");
        response.EnsureSuccessStatusCode();
        string html = await response.Content.ReadAsStringAsync();
        Match[] loginForms = Regex.Matches(
                html,
                @"<form\b(?<attributes>[^>]*)>(?<content>.*?)</form>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline)
            .Cast<Match>()
            .Where(form => string.Equals(
                ReadHtmlAttribute(form.Groups["attributes"].Value, "method"),
                "post",
                StringComparison.OrdinalIgnoreCase))
            .Where(form =>
            {
                Dictionary<string, string> values = ReadHtmlInputValues(form.Groups["content"].Value);
                bool namedLoginForm =
                    (values.TryGetValue("_handler", out string? handler) && handler == "login") ||
                    (values.TryGetValue("__formName", out string? formName) && formName == "login");
                string? action = ReadHtmlAttribute(form.Groups["attributes"].Value, "action");
                bool targetsLoginRoute = string.IsNullOrWhiteSpace(action) ||
                    (Uri.TryCreate(new Uri("http://localhost"), action, out Uri? actionUri) &&
                     string.Equals(actionUri.AbsolutePath, "/Account/Login", StringComparison.OrdinalIgnoreCase));
                return namedLoginForm && targetsLoginRoute;
            })
            .ToArray();

        Match loginForm = Assert.Single(loginForms);
        return ReadHtmlInputValues(loginForm.Groups["content"].Value);
    }

    private static Dictionary<string, string> ReadHtmlInputValues(string content)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        foreach (Match input in Regex.Matches(
                     content,
                     @"<input\b(?<attributes>[^>]*)>",
                     RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            string attributes = input.Groups["attributes"].Value;
            string? name = ReadHtmlAttribute(attributes, "name");
            string? value = ReadHtmlAttribute(attributes, "value");
            if (name is not null && value is not null)
                values[name] = value;
        }

        return values;
    }

    private static string? ReadHtmlAttribute(string attributes, string attributeName)
    {
        Match attribute = Regex.Match(
            attributes,
            "(?:^|\\s)" + Regex.Escape(attributeName) +
                "\\s*=\\s*(?:\"(?<double>[^\"]*)\"|'(?<single>[^']*)'|(?<bare>[^\\s/>]+))",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!attribute.Success)
            return null;

        string value = attribute.Groups["double"].Success
            ? attribute.Groups["double"].Value
            : attribute.Groups["single"].Success
                ? attribute.Groups["single"].Value
                : attribute.Groups["bare"].Value;
        return WebUtility.HtmlDecode(value);
    }

    private static async Task<SelectedWebStartupAttempt> AttemptSelectedDataStartupAsync(
        OwnedProviderDocument providerDocument,
        string? competingSelectorPath = null,
        string? competingProviderSourceIdentity = null,
        Guid? configuredBatchId = null,
        bool captureBatchState = false)
    {
        using LoginLogoutTests.CustomWebApplicationFactory selectedDataFactory = new();
        using HttpClient selectedDataClient = selectedDataFactory.CreateClient();
        string subjectId = await SeedSelectedWebBatchAsync(
            selectedDataFactory, SelectedWebAuthorityId, SelectedWebCanonicalDatabaseIdentity,
            SelectedWebTargetId, SelectedWebBatchId);
        if (captureBatchState)
        {
            await using AsyncServiceScope identityScope = selectedDataFactory.Services.CreateAsyncScope();
            UserManager<ApplicationUser> users = identityScope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser seededOperator = Assert.IsAssignableFrom<ApplicationUser>(
                await users.FindByNameAsync(SelectedWebUserName));
            Assert.True(await users.CheckPasswordAsync(seededOperator, SelectedWebPassword));
            Assert.True(await users.IsInRoleAsync(seededOperator, RoleNames.SystemOwner));
        }
        ImmutableArray<SelectedWebBatchState> batchStateBefore = captureBatchState
            ? await CaptureSelectedWebBatchStateAsync(selectedDataFactory.ConnectionString)
            : [];

        SelectedWebConnectionTranslation translation = new(
            BaselineWebNpgsqlConnection,
            SelectedWebNpgsqlConnection,
            selectedDataFactory.ConnectionString,
            selectedDataFactory.ConnectionString);
        List<string> startupEvents = [];
        ProductionNpgsqlDeclarations? productionDeclarations = null;
        RecordingSecretEncryptionService? recordingEncryption = null;
        CountingSelectedWebCredentialSource credentialSource = new(
            SelectedWebUserName, SelectedWebPassword, startupEvents);
        using WebApplicationFactory<Program> selectedFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = BaselineWebNpgsqlConnection
                    });
                    configuration.AddInMemoryCollection(
                        CreateSelectedWebConfiguration(
                            SelectedWebNpgsqlConnection,
                            configuredBatchId: configuredBatchId));
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["October:Providers:ConfigurationFile"] = providerDocument.ConfigurationFilePath,
                        ["DataProtection:KeyRingPath"] = providerDocument.KeyRingPath,
                        ["DataProtection:ApplicationName"] = providerDocument.ApplicationName
                    });
                    if (competingSelectorPath is not null)
                    {
                        configuration.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["October:Providers:ConfigurationFile"] = competingSelectorPath
                        });
                    }
                    if (competingProviderSourceIdentity is not null)
                    {
                        configuration.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["October:Providers:SourceIdentity"] = competingProviderSourceIdentity
                        });
                    }
                });
                builder.ConfigureTestServices(services =>
                {
                    productionDeclarations = CaptureProductionNpgsqlDeclarations(services);
                    services.RemoveAll<IOctoberOperatorCredentialSource>();
                    services.AddSingleton<IOctoberOperatorCredentialSource>(credentialSource);
                    services.RemoveAll<IDataProtectionProvider>();
                    services.AddSingleton(providerDocument.DataProtectionProvider);
                    services.RemoveAll<ISecretEncryptionService>();
                    services.AddSingleton<ISecretEncryptionService>(_ =>
                        recordingEncryption = new RecordingSecretEncryptionService(
                            new SecretEncryptionService(providerDocument.DataProtectionProvider),
                            startupEvents));
                    ReplaceSelectedWebSqliteOptions(
                        services, productionDeclarations, translation);
                });
            });

        Exception? startupFailure = Record.Exception(() =>
        {
            using HttpClient selectedClient = selectedFactory.CreateClient();
        });
        ImmutableArray<SelectedWebBatchState> batchStateAfter = captureBatchState
            ? await CaptureSelectedWebBatchStateAsync(selectedDataFactory.ConnectionString)
            : [];
        return new SelectedWebStartupAttempt(
            startupFailure,
            recordingEncryption?.Decryptions.Count ?? 0,
            credentialSource.ReadCount,
            subjectId,
            batchStateBefore,
            batchStateAfter);
    }

    private static async Task<ImmutableArray<SelectedWebBatchState>> CaptureSelectedWebBatchStateAsync(
        string sqliteConnectionString)
    {
        await using OctoberDbContext database = new(
            new DbContextOptionsBuilder<OctoberDbContext>()
                .UseSqlite(sqliteConnectionString)
                .Options);
        OctoberBatchRecord[] records = await database.Batches.AsNoTracking().ToArrayAsync();
        return records
            .OrderBy(record => record.BatchId)
            .Select(record => new SelectedWebBatchState(
                record.BatchId,
                record.TargetId,
                record.SubjectId,
                record.CanonicalDatabaseIdentity,
                record.SelectionDigest,
                record.ManagementAuthorityId,
                record.SelectedAtUtc,
                record.State))
            .ToImmutableArray();
    }

    private static async Task<string> SeedSelectedWebBatchAsync(
        LoginLogoutTests.CustomWebApplicationFactory factory,
        string authorityId,
        string canonicalDatabaseIdentity,
        string targetId,
        Guid batchId)
    {
        DateTimeOffset now = new(2026, 10, 1, 4, 0, 0, TimeSpan.Zero);
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RoleManager<IdentityRole> roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync(RoleNames.SystemOwner))
        {
            IdentityResult roleCreated = await roles.CreateAsync(new IdentityRole(RoleNames.SystemOwner));
            Assert.True(roleCreated.Succeeded,
                string.Join("; ", roleCreated.Errors.Select(error => error.Code)));
        }

        UserManager<ApplicationUser> users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = new()
        {
            UserName = SelectedWebUserName,
            Email = SelectedWebUserName,
            EmailConfirmed = true,
            LockoutEnabled = true
        };
        IdentityResult created = await users.CreateAsync(user, SelectedWebPassword);
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(error => error.Code)));
        IdentityResult roleAssigned = await users.AddToRoleAsync(user, RoleNames.SystemOwner);
        Assert.True(roleAssigned.Succeeded,
            string.Join("; ", roleAssigned.Errors.Select(error => error.Code)));

        VulgataDbContext management = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>()
            .CreateDbContextAsync();
        await using (management)
        {
            SystemEntity system = new("October selected Web System", "Owned test fixture",
                "System advisory", now);
            Repository repository = system.AddRepository("October selected Web Repository",
                "https://example.test/october-selected-web.git", "Owned test fixture",
                "Repository advisory", now);
            GlobalContext global = new("Global advisory", now);
            management.Systems.Add(system);
            management.SystemOwnerAssignments.Add(SystemOwnerAssignment.Create(system.Id, user.Id, now));
            management.Repositories.Add(repository);
            management.GlobalContexts.Add(global);
            await management.SaveChangesAsync();

            OctoberRepositorySelection selectedRepository = new(
                repository.Id, system.Id, "selected-web-root-v1", "selected-web-operator");
            OctoberContextSelection[] references =
            [
                new OctoberContextSelection(OctoberContextScope.Global, global.Id),
                new OctoberContextSelection(OctoberContextScope.System, system.Id),
                new OctoberContextSelection(OctoberContextScope.Repository, repository.Id)
            ];
            OctoberAdvisoryContextEntry[] entries =
            [
                new(repository.Id, OctoberContextScope.Global, OctoberContextApplicability.Selected,
                    global.Id, "Global advisory", OctoberContextEvidenceKind.AdvisoryNotSourceProof),
                new(repository.Id, OctoberContextScope.System, OctoberContextApplicability.Selected,
                    system.Id, "System advisory", OctoberContextEvidenceKind.AdvisoryNotSourceProof),
                new(repository.Id, OctoberContextScope.Repository, OctoberContextApplicability.Selected,
                    repository.Id, "Repository advisory", OctoberContextEvidenceKind.AdvisoryNotSourceProof)
            ];
            OctoberContextDifference[] differences =
            [
                new(repository.Id, OctoberContextScope.Global, OctoberContextScope.System),
                new(repository.Id, OctoberContextScope.Global, OctoberContextScope.Repository),
                new(repository.Id, OctoberContextScope.System, OctoberContextScope.Repository)
            ];
            OctoberSelectedAdvisoryContext context = new(
                authorityId,
                ImmutableArray.Create(selectedRepository),
                ImmutableArray.Create(entries),
                ImmutableArray.Create(differences));
            OctoberSetupRequest request = new(targetId, batchId,
                [selectedRepository], references, string.Empty);
            string digest = OctoberSelectionDigest.Compute(request, context);
            request = request with { SelectionDigest = digest };
            OctoberAuthorizedSelection authority = new(targetId, batchId, user.Id,
                canonicalDatabaseIdentity, [repository.Id], digest, context);

            await using OctoberDbContext october = new(
                new DbContextOptionsBuilder<OctoberDbContext>()
                    .UseSqlite(factory.ConnectionString)
                    .Options);
            await october.Database.ExecuteSqlRawAsync(october.Database.GenerateCreateScript());
            OctoberPrepareResult prepared = await new OctoberBatchSetupPolicy(
                    new OctoberSetupStore(october, TimeProvider.System))
                .PrepareAsync(request, authority, CancellationToken.None);
            Assert.Equal(OctoberPrepareStatus.Created, prepared.Status);
            Assert.Equal(batchId, prepared.CommittedBatch!.BatchId);
        }

        return user.Id;
    }

    private static ProductionNpgsqlDeclarations CaptureProductionNpgsqlDeclarations(
        IServiceCollection services) =>
        new(
            CaptureProductionDeclarations<ApplicationDbContext>(services),
            CaptureProductionDeclarations<VulgataDbContext>(services));

    private static ImmutableArray<IDbContextOptionsConfiguration<TContext>>
        CaptureProductionDeclarations<TContext>(IServiceCollection services)
        where TContext : DbContext
    {
        ServiceDescriptor[] descriptors = services
            .Where(descriptor => descriptor.ServiceType ==
                typeof(IDbContextOptionsConfiguration<TContext>))
            .ToArray();
        Assert.NotEmpty(descriptors);

        return descriptors.Select(descriptor =>
        {
            object? declaration = descriptor.ImplementationInstance ??
                descriptor.ImplementationFactory?.Invoke(EmptyServiceProvider.Instance);
            return Assert.IsAssignableFrom<IDbContextOptionsConfiguration<TContext>>(
                declaration ?? throw new InvalidOperationException(
                    $"The production {typeof(TContext).Name} options declaration was not captured."));
        }).ToImmutableArray();
    }

    private static void ReplaceSelectedWebSqliteOptions(
        IServiceCollection services,
        ProductionNpgsqlDeclarations declarations,
        SelectedWebConnectionTranslation translation)
    {
        services.RemoveAll<ApplicationDbContext>();
        services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
        services.RemoveAll<IConfigureOptions<DbContextOptions<ApplicationDbContext>>>();
        services.AddDbContext<ApplicationDbContext>((provider, options) =>
            options.UseSqlite(translation.ToSqliteConnection(
                CaptureProductionNpgsqlConnection(provider, declarations.Identity))));

        services.RemoveAll<VulgataDbContext>();
        services.RemoveAll<DbContextOptions<VulgataDbContext>>();
        services.RemoveAll<IDbContextFactory<VulgataDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<VulgataDbContext>>();
        services.RemoveAll<IConfigureOptions<DbContextOptions<VulgataDbContext>>>();
        services.AddDbContext<VulgataDbContext>((provider, options) =>
                options.UseSqlite(translation.ToSqliteConnection(
                    CaptureProductionNpgsqlConnection(provider, declarations.Management))),
            contextLifetime: ServiceLifetime.Scoped,
            optionsLifetime: ServiceLifetime.Singleton);
        services.AddDbContextFactory<VulgataDbContext>((provider, options) =>
            options.UseSqlite(translation.ToSqliteConnection(
                CaptureProductionNpgsqlConnection(provider, declarations.Management))));

        services.RemoveAll<OctoberDbContext>();
        services.RemoveAll<DbContextOptions<OctoberDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<OctoberDbContext>>();
        services.RemoveAll<IConfigureOptions<DbContextOptions<OctoberDbContext>>>();
        services.AddDbContext<OctoberDbContext>((provider, options) =>
        {
            OctoberAuthorityConfiguration authority =
                provider.GetRequiredService<OctoberAuthorityConfiguration>();
            options.UseSqlite(translation.ToSqliteConnection(authority.DataConnectionString));
        });
    }

    private static string CaptureProductionNpgsqlConnection<TContext>(
        IServiceProvider provider,
        ImmutableArray<IDbContextOptionsConfiguration<TContext>> declarations)
        where TContext : DbContext
    {
        string[] configuredConnections = CaptureProductionNpgsqlConnections(provider, declarations);
        Assert.NotEmpty(configuredConnections);
        Assert.All(configuredConnections,
            connection => Assert.Equal(configuredConnections[0], connection));
        return configuredConnections[0];
    }

    private static string[] CaptureProductionNpgsqlConnections<TContext>(
        IServiceProvider provider,
        ImmutableArray<IDbContextOptionsConfiguration<TContext>> declarations)
        where TContext : DbContext => declarations.Select(declaration =>
        {
            DbContextOptionsBuilder<TContext> options = new();
            declaration.Configure(provider, options);
            NpgsqlOptionsExtension npgsql = Assert.Single(
                options.Options.Extensions.OfType<NpgsqlOptionsExtension>());
            return Assert.IsType<string>(npgsql.ConnectionString);
        }).ToArray();

    private static void AssertSelectedSqliteConnection(string selectedConnection, string actualConnection)
    {
        string expectedDataSource = new SqliteConnectionStringBuilder(selectedConnection).DataSource;
        string actualDataSource = new SqliteConnectionStringBuilder(actualConnection).DataSource;
        Assert.Equal(expectedDataSource, actualDataSource);
    }

    private sealed record ProductionNpgsqlDeclarations(
        ImmutableArray<IDbContextOptionsConfiguration<ApplicationDbContext>> Identity,
        ImmutableArray<IDbContextOptionsConfiguration<VulgataDbContext>> Management);

    private sealed record SelectedWebStartupAttempt(
        Exception? StartupFailure,
        int DecryptionCount,
        int CredentialReadCount,
        string SubjectId,
        ImmutableArray<SelectedWebBatchState> BatchStateBefore,
        ImmutableArray<SelectedWebBatchState> BatchStateAfter);

    private sealed record SelectedWebBatchState(
        Guid BatchId,
        string TargetId,
        string SubjectId,
        string CanonicalDatabaseIdentity,
        string SelectionDigest,
        string ManagementAuthorityId,
        DateTimeOffset SelectedAtUtc,
        OctoberBatchState State);

    private sealed record SelectedWebConnectionTranslation(
        string BaselineNpgsqlConnection,
        string SelectedNpgsqlConnection,
        string BaselineSqliteConnection,
        string SelectedSqliteConnection)
    {
        public string ToSqliteConnection(string productionConnection) =>
            StringComparer.Ordinal.Equals(productionConnection, BaselineNpgsqlConnection)
                ? BaselineSqliteConnection
                : StringComparer.Ordinal.Equals(productionConnection, SelectedNpgsqlConnection)
                    ? SelectedSqliteConnection
                    : throw new InvalidOperationException(
                        "A production October context selected an unowned test database.");
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static EmptyServiceProvider Instance { get; } = new();

        public object? GetService(Type serviceType) => null;
    }

    private sealed class CountingSelectedWebCredentialSource(
        string userName,
        string password,
        ICollection<string>? events = null)
        : IOctoberOperatorCredentialSource
    {
        private int _readCount;

        public int ReadCount => Volatile.Read(ref _readCount);

        public ValueTask<OctoberOperatorCredentials?> ReadAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _readCount);
            events?.Add("operator-credentials");
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<OctoberOperatorCredentials?>(
                new OctoberOperatorCredentials(userName, password));
        }
    }

    private sealed class RecordingSecretEncryptionService(
        ISecretEncryptionService inner,
        ICollection<string> events) : ISecretEncryptionService
    {
        public List<(string Purpose, string Ciphertext)> Decryptions { get; } = [];

        public Task<string> EncryptAsync(
            string purpose,
            string plain,
            CancellationToken cancellationToken = default) =>
            inner.EncryptAsync(purpose, plain, cancellationToken);

        public Task<string> DecryptAsync(
            string purpose,
            string cipher,
            CancellationToken cancellationToken = default)
        {
            Decryptions.Add((purpose, cipher));
            events.Add("provider-decrypt");
            return inner.DecryptAsync(purpose, cipher, cancellationToken);
        }
    }

    private sealed class OwnedProviderDocument(
        string directoryPath,
        string configurationFilePath,
        string keyRingPath,
        string applicationName,
        ServiceProvider protectionServices,
        IDataProtectionProvider dataProtectionProvider,
        string workerCiphertext,
        string chatCiphertext) : IDisposable
    {
        public string ConfigurationFilePath { get; } = configurationFilePath;
        public string KeyRingPath { get; } = keyRingPath;
        public string ApplicationName { get; } = applicationName;
        public IDataProtectionProvider DataProtectionProvider { get; } = dataProtectionProvider;
        public string WorkerCiphertext { get; } = workerCiphertext;
        public string ChatCiphertext { get; } = chatCiphertext;

        public string CreateAlternateSelectorFile()
        {
            string alternatePath = Path.Combine(directoryPath, "providers-alternate.json");
            File.Copy(configurationFilePath, alternatePath);
            return alternatePath;
        }

        public async Task WriteAlternativeValidDocumentAsync()
        {
            JsonObject document = JsonNode.Parse(
                await File.ReadAllTextAsync(configurationFilePath))?.AsObject() ??
                throw new InvalidOperationException("The owned provider document was not a JSON object.");
            document["sourceIdentity"] = "selected-web-provider-after-startup";
            JsonArray profiles = document["profiles"]?.AsArray() ??
                throw new InvalidOperationException("The owned provider profiles were not an array.");
            JsonObject worker = profiles[0]?.AsObject() ??
                throw new InvalidOperationException("The owned worker profile was not an object.");
            worker["model"] = "worker-model-after-startup";
            await File.WriteAllTextAsync(
                configurationFilePath, document.ToJsonString(),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        public static async Task<OwnedProviderDocument> CreateAsync(string? documentVariant = null)
        {
            const string applicationName = "Vulgata.Tests.October.ProviderStartup";
            const string purpose = "Vulgata.LlmProviders.ApiKey";
            string directoryPath = Path.Combine(
                Path.GetTempPath(), $"vulgata-provider-startup-{Guid.NewGuid():N}");
            string keyRingPath = Path.Combine(directoryPath, "keys");
            string configurationFilePath = Path.Combine(directoryPath, "providers.json");
            Directory.CreateDirectory(keyRingPath);
            ServiceProvider? providerToDispose = null;

            try
            {
                ServiceCollection services = new();
                services.AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
                    .SetApplicationName(applicationName);
                ServiceProvider provider = services.BuildServiceProvider();
                providerToDispose = provider;
                SecretEncryptionService encryption = new(
                    provider.GetRequiredService<IDataProtectionProvider>());
                string workerCiphertext = await encryption.EncryptAsync(
                    purpose, "synthetic-worker-api-key");
                string chatCiphertext = await encryption.EncryptAsync(
                    purpose, "synthetic-chat-api-key");
                string document = JsonSerializer.Serialize(new
                {
                    sourceIdentity = "selected-web-provider-startup-test",
                    precedence = "only-source",
                    workerProfile = "worker-primary",
                    chatProfile = "chat-primary",
                    profiles = new[]
                    {
                        new
                        {
                            name = "worker-primary",
                            apiType = "chat-completions",
                            endpoint = "https://worker.example.test/v1",
                            model = "worker-model",
                            credentialReference = "worker-key"
                        },
                        new
                        {
                            name = "chat-primary",
                            apiType = "responses",
                            endpoint = "https://chat.example.test/v1",
                            model = "chat-model",
                            credentialReference = "chat-key"
                        }
                    },
                    protectedCredentials = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["worker-key"] = workerCiphertext,
                        ["chat-key"] = chatCiphertext
                    }
                });
                document = documentVariant switch
                {
                    null => document,
                    "unknown-api-type" => ReplaceRequired(
                        document,
                        "\"apiType\":\"chat-completions\"",
                        "\"apiType\":\"unknown-provider-type\""),
                    "malformed-json" => ReplaceRequired(
                        document,
                        "\"precedence\":\"only-source\"",
                        "\"precedence\" \"only-source\""),
                    "wrong-required-type" => ReplaceRequired(
                        document,
                        "\"sourceIdentity\":\"selected-web-provider-startup-test\"",
                        "\"sourceIdentity\":42"),
                    "malformed-utf8-source-identity" => document,
                    "duplicate-root-property" => ReplaceRequired(
                        document,
                        "\"sourceIdentity\":\"selected-web-provider-startup-test\"",
                        "\"sourceIdentity\":\"selected-web-provider-startup-test\",\"sourceIdentity\":\"selected-web-provider-duplicate\""),
                    "unknown-root-property" => document.Insert(
                        1, "\"unexpectedRootProperty\":\"unused\","),
                    "duplicate-profile-property" => ReplaceRequired(
                        document,
                        "\"model\":\"worker-model\"",
                        "\"model\":\"worker-model\",\"model\":\"worker-model\""),
                    "unknown-profile-property" => ReplaceRequired(
                        document,
                        "\"model\":\"worker-model\"",
                        "\"model\":\"worker-model\",\"unexpectedProfileProperty\":\"unused\""),
                    _ => throw new ArgumentOutOfRangeException(nameof(documentVariant), documentVariant, null)
                };
                if (documentVariant == "malformed-utf8-source-identity")
                {
                    byte[] documentBytes = Encoding.UTF8.GetBytes(document);
                    byte[] identityBytes = Encoding.UTF8.GetBytes("selected-web-provider-startup-test");
                    int identityStart = FindSequenceIndex(documentBytes, identityBytes);
                    if (identityStart < 0)
                        throw new InvalidOperationException("The provider source identity was not found in the fixture.");
                    documentBytes[identityStart] = 0xff;
                    await File.WriteAllBytesAsync(configurationFilePath, documentBytes);
                }
                else
                {
                    await File.WriteAllTextAsync(
                        configurationFilePath, document, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                }
                return new OwnedProviderDocument(
                    directoryPath, configurationFilePath, keyRingPath, applicationName,
                    provider, provider.GetRequiredService<IDataProtectionProvider>(),
                    workerCiphertext, chatCiphertext);
            }
            catch
            {
                providerToDispose?.Dispose();
                if (Directory.Exists(directoryPath))
                    Directory.Delete(directoryPath, recursive: true);
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                protectionServices.Dispose();
            }
            finally
            {
                if (Directory.Exists(directoryPath))
                    Directory.Delete(directoryPath, recursive: true);
            }
        }

        private static string ReplaceRequired(string document, string existingProperty, string replacement)
        {
            string updated = document.Replace(existingProperty, replacement, StringComparison.Ordinal);
            if (string.Equals(updated, document, StringComparison.Ordinal))
                throw new InvalidOperationException("The requested provider-document test mutation was not applied.");
            return updated;
        }

        private static int FindSequenceIndex(byte[] source, byte[] sequence)
        {
            for (int start = 0; start <= source.Length - sequence.Length; start++)
            {
                int offset = 0;
                while (offset < sequence.Length && source[start + offset] == sequence[offset])
                    offset++;
                if (offset == sequence.Length)
                    return start;
            }

            return -1;
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_SelectUsesExactSelectorWithoutMigrationOrWriter()
    {
        SelectionBundle selection = CreateSelection();
        OctoberBoundSetupSnapshot history = CreateSnapshot(selection);
        OctoberHistoricalBoundData bound = new(TargetId, BatchId, history);
        GeneratorRig rig = CreateRig(selection, selectorResult:
            new OctoberSelectionResult(OctoberSelectionStatus.Selected, bound));
        OctoberGeneratorCommand command = new(
            OctoberGeneratorMode.Select,
            TargetId,
            BatchId,
            SetupRequest: null);

        OctoberGeneratorExecutionResult result = await rig.Module.ExecuteAsync(command);

        Assert.Equal(OctoberGeneratorExecutionStatus.Selected, result.Status);
        Assert.Equal(new[] { "authenticate", "authorize-selection", "target-identity", "selector" },
            rig.Events.ToArray());
        Assert.Equal(TargetId, rig.Selector.LastSelection!.TargetId);
        Assert.Equal(BatchId, rig.Selector.LastSelection.BatchId);
        Assert.Equal(selection.Authority.SelectionDigest,
            rig.Selector.LastSelection.Authority.SelectionDigest);
        Assert.Same(bound, result.BoundSetupData);
        Assert.Null(result.CommittedBatch);
        Assert.Equal(0, rig.Migrator.CallCount);
        Assert.Equal(0, rig.Writer.CallCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_SelectWithFreshSameAuthorityContext_PreservesHistoricalSnapshot()
    {
        SelectionBundle stored = CreateSelection();
        SelectionBundle fresh = ChangeAdvisoryValues(stored);
        OctoberHistoricalBoundData historical = new(
            TargetId, BatchId, CreateSnapshot(stored));
        GeneratorRig rig = CreateRig(fresh, selectorResult:
            new OctoberSelectionResult(OctoberSelectionStatus.Selected, historical));
        OctoberGeneratorCommand command = new(
            OctoberGeneratorMode.Select,
            TargetId,
            BatchId,
            SetupRequest: null);

        OctoberGeneratorExecutionResult result = await rig.Module.ExecuteAsync(command);

        Assert.Equal(OctoberGeneratorExecutionStatus.Selected, result.Status);
        Assert.Same(historical, result.BoundSetupData);
        Assert.Equal(stored.Request.SelectionDigest,
            Assert.IsAssignableFrom<IOctoberHistoricalBoundSetupData>(result.BoundSetupData)
                .Snapshot.Request.SelectionDigest);
        Assert.Equal(FirstCommitUtc,
            Assert.IsAssignableFrom<IOctoberHistoricalBoundSetupData>(result.BoundSetupData)
                .Snapshot.SelectedAtUtc);
        Assert.NotEqual(fresh.Authority.SelectionDigest,
            Assert.IsAssignableFrom<IOctoberHistoricalBoundSetupData>(result.BoundSetupData)
                .Snapshot.Request.SelectionDigest);
        Assert.DoesNotContain("migrate", rig.Events);
        Assert.DoesNotContain("write", rig.Events);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_PrepareAuthorityBWithCollidingSelection_DeniesBeforeTargetAccess()
    {
        SelectionBundle authorityB = CreateSelection(OtherManagementAuthorityId);
        GeneratorRig rig = CreateRig(authorityB);
        OctoberGeneratorCommand command = new(
            OctoberGeneratorMode.Prepare,
            TargetId,
            BatchId,
            authorityB.Request);

        OctoberGeneratorExecutionResult result = await rig.Module.ExecuteAsync(command);

        Assert.Equal(OctoberGeneratorExecutionStatus.Denied, result.Status);
        Assert.Equal(new[] { "authenticate", "authorize-selection" }, rig.Events.ToArray());
        Assert.Null(result.CommittedBatch);
        Assert.Null(result.BoundSetupData);
        Assert.Equal(0, rig.Identity.CallCount);
        Assert.Equal(0, rig.Migrator.CallCount);
        Assert.Equal(0, rig.Writer.CallCount);
        Assert.Equal(0, rig.Selector.CallCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_AuthorityDenialStopsBeforeTargetAndReturnsClosedFailure()
    {
        SelectionBundle selection = CreateSelection();
        GeneratorRig rig = CreateRig(selection, OctoberAuthorityDecisionStatus.Denied);
        OctoberGeneratorCommand command = new(
            OctoberGeneratorMode.Prepare,
            TargetId,
            BatchId,
            selection.Request);

        OctoberGeneratorExecutionResult result = await rig.Module.ExecuteAsync(command);

        Assert.Equal(OctoberGeneratorExecutionStatus.Denied, result.Status);
        Assert.Equal(new[] { "authenticate", "authorize-selection" }, rig.Events.ToArray());
        Assert.Null(result.CommittedBatch);
        Assert.Null(result.BoundSetupData);
        Assert.Equal(0, rig.Identity.CallCount);
        Assert.Equal(0, rig.Migrator.CallCount);
        Assert.Equal(0, rig.Writer.CallCount);
        Assert.Equal(0, rig.Selector.CallCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_ConnectedTargetMismatchStopsBeforeMigrationOrSetupWrite()
    {
        SelectionBundle selection = CreateSelection();
        GeneratorRig rig = CreateRig(selection,
            connectedTargetStatus: OctoberConnectedTargetStatus.Mismatched);
        OctoberGeneratorCommand command = new(
            OctoberGeneratorMode.Prepare,
            TargetId,
            BatchId,
            selection.Request);

        OctoberGeneratorExecutionResult result = await rig.Module.ExecuteAsync(command);

        Assert.Equal(OctoberGeneratorExecutionStatus.TargetUnavailable, result.Status);
        Assert.Equal(new[] { "authenticate", "authorize-selection", "target-identity" }, rig.Events.ToArray());
        Assert.Null(result.CommittedBatch);
        Assert.Equal(0, rig.Migrator.CallCount);
        Assert.Equal(0, rig.Writer.CallCount);
        Assert.Equal(0, rig.Selector.CallCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_PrepareConflictReturnsNoCommittedCapability()
    {
        SelectionBundle selection = CreateSelection();
        GeneratorRig rig = CreateRig(selection,
            prepareResult: new OctoberPrepareResult(OctoberPrepareStatus.Conflict, null));
        OctoberGeneratorCommand command = new(
            OctoberGeneratorMode.Prepare,
            TargetId,
            BatchId,
            selection.Request);

        OctoberGeneratorExecutionResult result = await rig.Module.ExecuteAsync(command);

        Assert.Equal(OctoberGeneratorExecutionStatus.Denied, result.Status);
        Assert.Null(result.CommittedBatch);
        Assert.Null(result.BoundSetupData);
        Assert.Equal(new[] { "authenticate", "authorize-selection", "target-identity", "migrate", "write" },
            rig.Events.ToArray());
    }

    private static GeneratorRig CreateRig(
        SelectionBundle selection,
        OctoberAuthorityDecisionStatus authorityStatus = OctoberAuthorityDecisionStatus.Authorized,
        OctoberConnectedTargetStatus connectedTargetStatus = OctoberConnectedTargetStatus.Matched,
        OctoberPrepareResult? prepareResult = null,
        OctoberSelectionResult? selectorResult = null,
        CancellationTokenSource? cancelAfterAuthorization = null)
    {
        List<string> events = [];
        var authority = new RecordingLocalAuthority(
            events, selection, authorityStatus, cancelAfterAuthorization);
        var identity = new RecordingIdentityVerifier(events, connectedTargetStatus);
        var migrator = new RecordingMigrator(events);
        var writer = new RecordingSetupWriter(events, prepareResult);
        var selector = new RecordingSelector(events, selectorResult ??
            new OctoberSelectionResult(OctoberSelectionStatus.MissingBatch, null));
        var configuredTarget = new OctoberConfiguredTarget(
            TargetId, CanonicalDatabaseIdentity, identity, migrator, writer, selector);
        var module = new OctoberGeneratorModule(authority, configuredTarget,
            ExpectedManagementAuthorityId);
        return new GeneratorRig(module, events, authority, identity, migrator, writer, selector);
    }

    private static SelectionBundle CreateSelection(string managementAuthorityId = ExpectedManagementAuthorityId)
    {
        OctoberRepositorySelection repository = new(
            RepositoryId, SystemId, "root-binding-v1", OperatorLabel);
        OctoberSetupRequest request = new(
            TargetId,
            BatchId,
            [repository],
            [
                new OctoberContextSelection(OctoberContextScope.Global, GlobalId),
                new OctoberContextSelection(OctoberContextScope.System, SystemId),
                new OctoberContextSelection(OctoberContextScope.Repository, RepositoryId),
            ],
            string.Empty);
        OctoberSelectedAdvisoryContext context = new(
            managementAuthorityId,
            ImmutableArray.Create(repository),
            ImmutableArray.Create(
                Entry(OctoberContextScope.Global, GlobalId, "global advisory v1"),
                Entry(OctoberContextScope.System, SystemId, "system advisory v1"),
                Entry(OctoberContextScope.Repository, RepositoryId, "repository advisory v1")),
            ImmutableArray.Create(
                Difference(OctoberContextScope.Global, OctoberContextScope.System),
                Difference(OctoberContextScope.Global, OctoberContextScope.Repository),
                Difference(OctoberContextScope.System, OctoberContextScope.Repository)));
        string digest = OctoberSelectionDigest.Compute(request, context);
        request = request with { SelectionDigest = digest };
        OctoberAuthorizedSelection authority = new(
            TargetId,
            BatchId,
            SubjectId,
            CanonicalDatabaseIdentity,
            [RepositoryId],
            digest,
            context);
        return new SelectionBundle(request, authority);
    }

    private static SelectionBundle ChangeAdvisoryValues(SelectionBundle original)
    {
        OctoberSelectedAdvisoryContext context = original.Authority.SelectedContext!;
        OctoberAdvisoryContextEntry[] entries = context.Entries.ToArray();
        entries[0] = entries[0] with { Value = "global advisory revised" };
        entries[1] = entries[1] with { Value = "system advisory revised" };
        entries[2] = entries[2] with { Value = "repository advisory revised" };
        OctoberSelectedAdvisoryContext freshContext = context with
        {
            Entries = entries.ToImmutableArray(),
        };
        string digest = OctoberSelectionDigest.Compute(original.Request, freshContext);
        OctoberSetupRequest freshRequest = original.Request with { SelectionDigest = digest };
        return new SelectionBundle(freshRequest, original.Authority with
        {
            SelectionDigest = digest,
            SelectedContext = freshContext,
        });
    }

    private static OctoberBoundSetupSnapshot CreateSnapshot(SelectionBundle selection) =>
        new(selection.Request, selection.Authority, FirstCommitUtc);

    private static OctoberAdvisoryContextEntry Entry(
        OctoberContextScope scope,
        Guid origin,
        string value) => new(
            RepositoryId,
            scope,
            OctoberContextApplicability.Selected,
            origin,
            value,
            OctoberContextEvidenceKind.AdvisoryNotSourceProof);

    private static OctoberContextDifference Difference(
        OctoberContextScope left,
        OctoberContextScope right) => new(RepositoryId, left, right);

    private sealed record SelectionBundle(
        OctoberSetupRequest Request,
        OctoberAuthorizedSelection Authority);

    private sealed record OctoberHistoricalBoundData(
        string TargetId,
        Guid BatchId,
        OctoberBoundSetupSnapshot Snapshot) : IOctoberHistoricalBoundSetupData;

    private sealed record GeneratorRig(
        IOctoberGeneratorModule Module,
        List<string> Events,
        RecordingLocalAuthority Authority,
        RecordingIdentityVerifier Identity,
        RecordingMigrator Migrator,
        RecordingSetupWriter Writer,
        RecordingSelector Selector);

    private sealed class RecordingLocalAuthority(
        List<string> events,
        SelectionBundle selection,
        OctoberAuthorityDecisionStatus selectionStatus,
        CancellationTokenSource? cancelAfterAuthorization) : IOctoberLocalAuthority
    {
        private readonly RecordingOperatorSession _session = new(
            SubjectId,
            selection.Authority.SelectedContext?.ManagementAuthorityId ?? ExpectedManagementAuthorityId,
            FirstCommitUtc.AddHours(1));

        public RecordingOperatorSession LastSession => _session;

        public Task<OctoberAuthenticationResult> AuthenticateAsync(CancellationToken cancellationToken)
        {
            events.Add("authenticate");
            return Task.FromResult(new OctoberAuthenticationResult(
                OctoberAuthenticationStatus.Authenticated, _session));
        }

        public Task<OctoberAuthoritySelectionResult> AuthorizeSelectionAsync(
            IOctoberOperatorSession session,
            OctoberAuthoritySelectionRequest request,
            CancellationToken cancellationToken)
        {
            events.Add("authorize-selection");
            OctoberAuthoritySelectionResult result = new(
                selectionStatus,
                selectionStatus == OctoberAuthorityDecisionStatus.Authorized
                    ? selection.Request
                    : null,
                selectionStatus == OctoberAuthorityDecisionStatus.Authorized
                    ? selection.Authority
                    : null);
            if (selectionStatus == OctoberAuthorityDecisionStatus.Authorized)
                cancelAfterAuthorization?.Cancel();
            return Task.FromResult(result);
        }

        public Task<OctoberSourceApprovalResult> IssueSourceApprovalAsync(
            IOctoberOperatorSession session,
            IOctoberHistoricalBoundSetupData boundSetup,
            Guid repositoryId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Generator prepare/select tests do not issue source approvals.");

        public Task<PreparedSourceAuthorityResult> VerifySourceApprovalAsync(
            IOctoberOperatorSession session,
            string targetId,
            PreparedSourceApproval approval,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Generator prepare/select tests do not verify source approvals.");

        public Task<OctoberRevocationStatus> RevokeSourceApprovalAsync(
            IOctoberOperatorSession session,
            string targetId,
            string approvalIdentity,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Generator prepare/select tests do not revoke source approvals.");
    }

    private sealed class RecordingOperatorSession(
        string subjectId,
        string authorityId,
        DateTimeOffset expiresAtUtc) : IOctoberOperatorSession
    {
        public string SubjectId { get; } = subjectId;
        public string ManagementAuthorityId { get; } = authorityId;
        public DateTimeOffset ExpiresAtUtc { get; } = expiresAtUtc;
        public bool IsDisposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingIdentityVerifier(
        List<string> events,
        OctoberConnectedTargetStatus status) : IOctoberConnectedTargetIdentityVerifier
    {
        public int CallCount { get; private set; }
        public string? LastTargetId { get; private set; }
        public string? LastCanonicalIdentity { get; private set; }

        public Task<OctoberConnectedTargetStatus> VerifyAsync(
            string targetId,
            string canonicalDatabaseIdentity,
            CancellationToken cancellationToken = default)
        {
            events.Add("target-identity");
            CallCount++;
            LastTargetId = targetId;
            LastCanonicalIdentity = canonicalDatabaseIdentity;
            return Task.FromResult(status);
        }
    }

    private sealed class RecordingMigrator(List<string> events) : IOctoberTargetMigrator
    {
        public int CallCount { get; private set; }

        public Task MigrateAsync(CancellationToken cancellationToken = default)
        {
            events.Add("migrate");
            CallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSetupWriter(
        List<string> events,
        OctoberPrepareResult? configuredResult) : IOctoberSetupWriter
    {
        public int CallCount { get; private set; }
        public OctoberSetupRequest? LastRequest { get; private set; }
        public OctoberAuthorizedSelection? LastAuthority { get; private set; }
        public OctoberPrepareResult LastResult { get; private set; } = new(OctoberPrepareStatus.Denied, null);

        public Task<OctoberPrepareResult> CommitAsync(
            OctoberSetupRequest request,
            OctoberAuthorizedSelection authority,
            CancellationToken cancellationToken = default)
        {
            events.Add("write");
            CallCount++;
            LastRequest = request;
            LastAuthority = authority;
            LastResult = configuredResult ?? new OctoberPrepareResult(
                OctoberPrepareStatus.Created,
                new OctoberCommittedBatch(
                    TargetId, BatchId, CanonicalDatabaseIdentity,
                    request.SelectionDigest, FirstCommitUtc));
            return Task.FromResult(LastResult);
        }
    }

    private sealed class RecordingSelector(
        List<string> events,
        OctoberSelectionResult configuredResult) : IOctoberBatchSelector
    {
        public int CallCount { get; private set; }
        public OctoberStartupSelection? LastSelection { get; private set; }

        public Task<OctoberSelectionResult> BindAsync(
            OctoberStartupSelection selection,
            CancellationToken cancellationToken = default)
        {
            events.Add("selector");
            CallCount++;
            LastSelection = selection;
            return Task.FromResult(configuredResult);
        }
    }
}
