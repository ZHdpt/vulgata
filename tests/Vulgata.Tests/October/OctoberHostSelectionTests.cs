using System.Diagnostics;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Shared;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;
using Xunit.Abstractions;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests.October;

public sealed class OctoberHostSelectionTests(ITestOutputHelper output)
{
    [Trait("Suite", "E2E")]
    [SuiteFact("E2E")]
    public async Task GeneratorPrepare_WithWebStopped_CommitsOwnedBatch()
    {
        await using GeneratorPrepareFixture fixture = await GeneratorPrepareFixture.CreateAsync(output);
        OctoberSetupRequest request = fixture.CreatePrepareRequest();
        string requestJson = JsonSerializer.Serialize(request);
        OctoberSetupRequest parsedRequest = Assert.IsType<OctoberSetupRequest>(
            JsonSerializer.Deserialize<OctoberSetupRequest>(requestJson));
        Assert.Equal(fixture.TargetId, parsedRequest.TargetId);
        Assert.Equal(fixture.BatchId, parsedRequest.BatchId);
        Assert.Empty(parsedRequest.SelectionDigest);

        await fixture.AssertOwnedOperatorCanPrepareAsync(request);
        await fixture.AssertOctoberSchemaAbsentAsync();
        using Process process = fixture.StartGenerator(requestJson);
        int processId = process.Id;
        output.WriteLine($"generator-process-id={processId}");
        (int exitCode, string standardOutput, string standardError) = await fixture.WaitForGeneratorAsync(process);
        output.WriteLine($"generator-exit-code={exitCode}");

        string childOutput = standardOutput + standardError;
        Assert.DoesNotContain(fixture.OperatorPassword, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyWorker, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProviderApiKeyChat, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ConnectionString, childOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.OwnedRoot, childOutput, StringComparison.OrdinalIgnoreCase);
        Assert.True(exitCode == 0, "The actual generator prepare command should commit the authorized owned batch.");

        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        IOctoberLocalAuthority authority = scope.ServiceProvider.GetRequiredService<IOctoberLocalAuthority>();
        OctoberAuthenticationResult authentication = await authority.AuthenticateAsync(CancellationToken.None);
        Assert.Equal(OctoberAuthenticationStatus.Authenticated, authentication.Status);
        await using IOctoberOperatorSession session = Assert.IsAssignableFrom<IOctoberOperatorSession>(
            authentication.Session);
        OctoberAuthoritySelectionResult current = await authority.AuthorizeSelectionAsync(
            session,
            new OctoberAuthoritySelectionRequest(fixture.TargetId, fixture.BatchId, PrepareRequest: null),
            CancellationToken.None);
        Assert.Equal(OctoberAuthorityDecisionStatus.Authorized, current.Status);
        OctoberAuthorizedSelection freshAuthority = Assert.IsType<OctoberAuthorizedSelection>(current.Authority);

        await using OctoberDbContext october = fixture.CreateOctoberContext();
        IOctoberBatchSelector selector = new OctoberBatchSelector(
            october, fixture.TargetId, fixture.CanonicalDatabaseIdentity);
        OctoberSelectionResult selected = await selector.BindAsync(
            new OctoberStartupSelection(fixture.TargetId, fixture.BatchId, freshAuthority),
            CancellationToken.None);
        Assert.Equal(OctoberSelectionStatus.Selected, selected.Status);
        IOctoberHistoricalBoundSetupData bound =
            Assert.IsAssignableFrom<IOctoberHistoricalBoundSetupData>(selected.BoundSetupData);
        Assert.Equal(fixture.TargetId, bound.Snapshot.Request.TargetId);
        Assert.Equal(fixture.BatchId, bound.Snapshot.Request.BatchId);
        Assert.Equal(fixture.CanonicalDatabaseIdentity, bound.Snapshot.Authority.CanonicalDatabaseIdentity);
        Assert.Equal(fixture.SubjectId, bound.Snapshot.Authority.SubjectId);
        Assert.Equal(fixture.AuthorityId, bound.Snapshot.Authority.SelectedContext!.ManagementAuthorityId);
        Assert.Equal(new[] { fixture.RepositoryId }, bound.Snapshot.Request.Repositories
            .Select(repository => repository.RepositoryId));
        Assert.Matches("^[0-9a-f]{64}$", bound.Snapshot.Request.SelectionDigest);
        Assert.Equal(bound.Snapshot.Request.SelectionDigest, bound.Snapshot.Authority.SelectionDigest);
        Assert.Equal(TimeSpan.Zero, bound.Snapshot.SelectedAtUtc.Offset);
    }

    private sealed class GeneratorPrepareFixture : IAsyncDisposable
    {
        private const string DatabasePrefix = "vulgata_story184_generator_";
        private const string DataProtectionApplicationName = "Vulgata.Tests.Story184.GeneratorPrepare";
        private readonly string _administratorConnectionString;
        private readonly ITestOutputHelper _output;
        private readonly DataProtectionProviderRegistration _protection;
        private readonly string _databaseName;
        private readonly string _runId;
        private bool _disposed;

        private GeneratorPrepareFixture(
            ITestOutputHelper output,
            string runId,
            string administratorConnectionString,
            string databaseName,
            string connectionString,
            string canonicalDatabaseIdentity,
            string ownedRoot,
            ServiceProvider services,
            DataProtectionProviderRegistration protection,
            string subjectId,
            string operatorUserName,
            string operatorPassword,
            Guid systemId,
            Guid repositoryId,
            Guid globalContextId,
            Guid batchId,
            string targetId)
        {
            _output = output;
            _runId = runId;
            _administratorConnectionString = administratorConnectionString;
            _databaseName = databaseName;
            ConnectionString = connectionString;
            CanonicalDatabaseIdentity = canonicalDatabaseIdentity;
            OwnedRoot = ownedRoot;
            Services = services;
            _protection = protection;
            SubjectId = subjectId;
            OperatorUserName = operatorUserName;
            OperatorPassword = operatorPassword;
            SystemId = systemId;
            RepositoryId = repositoryId;
            GlobalContextId = globalContextId;
            BatchId = batchId;
            TargetId = targetId;
            AuthorityId = $"generator-tracer-authority-{runId}";
            ProviderApiKeyWorker = $"synthetic-worker-key-{runId}";
            ProviderApiKeyChat = $"synthetic-chat-key-{runId}";
        }

        internal string ConnectionString { get; }
        internal string CanonicalDatabaseIdentity { get; }
        internal string OwnedRoot { get; }
        internal ServiceProvider Services { get; }
        internal string SubjectId { get; }
        internal string OperatorUserName { get; }
        internal string OperatorPassword { get; }
        internal Guid SystemId { get; }
        internal Guid RepositoryId { get; }
        internal Guid GlobalContextId { get; }
        internal Guid BatchId { get; }
        internal string TargetId { get; }
        internal string AuthorityId { get; }
        internal string ProviderApiKeyWorker { get; }
        internal string ProviderApiKeyChat { get; }

        internal static async Task<GeneratorPrepareFixture> CreateAsync(ITestOutputHelper output)
        {
            string runId = Environment.GetEnvironmentVariable("VULGATA_TEST_RESOURCE_RUN") ?? string.Empty;
            Assert.True(Guid.TryParseExact(runId, "N", out _),
                "The E2E launcher must provide its exact owned PostgreSQL run identifier.");
            string administrator = TestResourceScope.RequireAdministrator();
            TestResourceScope.ValidateAdministrator(administrator);
            NpgsqlConnectionStringBuilder adminBuilder = new(administrator)
            {
                Pooling = false,
                Timeout = 5,
                CommandTimeout = 15,
            };
            Assert.Equal("127.0.0.1", adminBuilder.Host);

            string databaseName = DatabasePrefix + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            string? ownedRoot = null;
            ServiceProvider? services = null;
            DataProtectionProviderRegistration? protection = null;
        string? targetConnection = null;
            bool databaseCreated = false;
            try
            {
                await using (NpgsqlConnection connection = new(adminBuilder.ConnectionString))
                {
                    await connection.OpenAsync();
                    int major = Convert.ToInt32(await new NpgsqlCommand(
                        "SELECT current_setting('server_version_num')::integer;", connection)
                        .ExecuteScalarAsync(), CultureInfo.InvariantCulture) / 10000;
                    Assert.Equal(17, major);
                    await using NpgsqlCommand exists = new(
                        "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);", connection);
                    exists.Parameters.AddWithValue("name", databaseName);
                    Assert.False((bool)(await exists.ExecuteScalarAsync() ?? true));
                    await using NpgsqlCommand create = new(
                        $"CREATE DATABASE \"{databaseName}\";", connection);
                    await create.ExecuteNonQueryAsync();
                    databaseCreated = true;
                    TestResourceScope.RecordCreated(adminBuilder.ConnectionString, databaseName);
                }

                NpgsqlConnectionStringBuilder targetBuilder = new(adminBuilder.ConnectionString)
                {
                    Database = databaseName,
                    Pooling = false,
                    Timeout = 5,
                    CommandTimeout = 30,
                };
                targetConnection = targetBuilder.ConnectionString;
                TestResourceScope.ValidateOwnedTarget(targetConnection);
                string canonicalIdentity =
                    $"postgres://{adminBuilder.Host}:{adminBuilder.Port.ToString(CultureInfo.InvariantCulture)}/{databaseName}";
                ownedRoot = CreateOwnedRoot(runId);
                string keyRingPath = Path.Combine(ownedRoot, "keys");
                Directory.CreateDirectory(keyRingPath);
                string subjectId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
                string operatorUserName = CreateOperatorUserName(subjectId);
                string operatorPassword = $"Valid1!Generator-{Guid.NewGuid():N}";
                Guid batchId = Guid.NewGuid();
                string targetId = $"generator-target-{runId}";
                string authorityId = $"generator-tracer-authority-{runId}";
                ImmutableDictionary<string, string> allowedTargets =
                    ImmutableDictionary<string, string>.Empty.WithComparers(StringComparer.Ordinal)
                        .Add(targetId, canonicalIdentity);
                OctoberAuthorityConfiguration authorityConfiguration = new(
                    authorityId,
                    targetId,
                    batchId,
                    canonicalIdentity,
                    targetConnection,
                    targetConnection,
                    canonicalIdentity,
                    allowedTargets,
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromHours(1));

                protection = CreateDataProtectionProvider(keyRingPath);
                string credentialFile = Path.Combine(ownedRoot, "operator.credentials.protected");
                string providerFile = Path.Combine(ownedRoot, "providers.json");
                string cliHome = Path.Combine(ownedRoot, "dotnet-cli");
                Directory.CreateDirectory(cliHome);
                WriteProtectedInputs(
                    protection.Provider,
                    credentialFile,
                    providerFile,
                    operatorUserName,
                    operatorPassword,
                    runId);

                IOctoberOperatorCredentialSource credentialSource =
                    new OctoberProtectedFileCredentialSource(credentialFile, protection.Provider);
                ServiceCollection registrations = new();
                registrations.AddLogging();
                registrations.AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
                    .SetApplicationName(DataProtectionApplicationName);
                OctoberHeadlessAuthorityRegistration.AddOctoberLocalAuthority(
                    registrations, authorityConfiguration, credentialSource, TimeProvider.System);
                registrations.AddDbContext<OctoberDbContext>(options =>
                    options.UseNpgsql(targetConnection, npgsql =>
                        npgsql.MigrationsHistoryTable("__OctoberMigrationsHistory", "october")));
                services = registrations.BuildServiceProvider(validateScopes: true);
                (Guid systemId, Guid repositoryId, Guid globalContextId) =
                    await MigrateAuthoritySchemasAndSeedAsync(services, subjectId, operatorPassword);

                await using (NpgsqlConnection target = new(targetConnection))
                {
                    TestResourceScope.ValidateOwnedTarget(targetConnection);
                    await target.OpenAsync();
                    await using NpgsqlCommand octoberSchema = new(
                        "SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'october');", target);
                    Assert.False((bool)(await octoberSchema.ExecuteScalarAsync() ?? true),
                        "The supported Identity/domain fixture must leave October unmigrated before generator prepare.");
                }

                GeneratorPrepareFixture fixture = new(
                    output, runId, adminBuilder.ConnectionString, databaseName, targetConnection,
                    canonicalIdentity, ownedRoot, services, protection, subjectId, operatorUserName, operatorPassword,
                    systemId, repositoryId, globalContextId, batchId, targetId);
                services = null;
                ownedRoot = null;
                protection = null;
                databaseCreated = false;

                output.WriteLine($"resource-run={runId}");
                output.WriteLine($"runner-container=vulgata-test-{runId}");
                output.WriteLine($"runner-volume=vulgata-test-data-{runId}");
                output.WriteLine($"owned-database={databaseName}");
                output.WriteLine($"owned-temp-root={fixture.OwnedRoot}");
                return fixture;
            }
            catch
            {
                if (services is not null)
                    await services.DisposeAsync();
                if (protection is not null)
                    await protection.Services.DisposeAsync();
                if (databaseCreated)
                {
                    string cleanupTarget = targetConnection ?? new NpgsqlConnectionStringBuilder(
                        adminBuilder.ConnectionString) { Database = databaseName, Pooling = false }.ConnectionString;
                    await DropOwnedDatabaseAsync(adminBuilder.ConnectionString, databaseName, cleanupTarget);
                }
                if (ownedRoot is not null)
                    DeleteOwnedRoot(ownedRoot, runId);
                throw;
            }
        }

        internal OctoberSetupRequest CreatePrepareRequest() => new(
            TargetId,
            BatchId,
            [new OctoberRepositorySelection(RepositoryId, SystemId,
                "owned-generator-root-v1", "Generator prepare tracer")],
            [
                new OctoberContextSelection(OctoberContextScope.Global, GlobalContextId),
                new OctoberContextSelection(OctoberContextScope.System, SystemId),
                new OctoberContextSelection(OctoberContextScope.Repository, RepositoryId),
            ],
            string.Empty);

        internal async Task AssertOwnedOperatorCanPrepareAsync(OctoberSetupRequest request)
        {
            TestResourceScope.ValidateOwnedTarget(ConnectionString);
            await using AsyncServiceScope scope = Services.CreateAsyncScope();
            IOctoberLocalAuthority authority = scope.ServiceProvider.GetRequiredService<IOctoberLocalAuthority>();
            OctoberAuthenticationResult authentication = await authority.AuthenticateAsync(CancellationToken.None);
            Assert.Equal(OctoberAuthenticationStatus.Authenticated, authentication.Status);
            await using IOctoberOperatorSession session = Assert.IsAssignableFrom<IOctoberOperatorSession>(
                authentication.Session);
            Assert.Equal(SubjectId, session.SubjectId);
            Assert.Equal(AuthorityId, session.ManagementAuthorityId);

            OctoberAuthoritySelectionResult selection = await authority.AuthorizeSelectionAsync(
                session,
                new OctoberAuthoritySelectionRequest(TargetId, BatchId, request),
                CancellationToken.None);
            Assert.Equal(OctoberAuthorityDecisionStatus.Authorized, selection.Status);
            Assert.Equal(TargetId, Assert.IsType<OctoberSetupRequest>(selection.Request).TargetId);
            Assert.Equal(BatchId, selection.Authority!.BatchId);
        }

        internal async Task AssertOctoberSchemaAbsentAsync()
        {
            TestResourceScope.ValidateOwnedTarget(ConnectionString);
            await using NpgsqlConnection target = new(ConnectionString);
            await target.OpenAsync();
            await using NpgsqlCommand command = new(
                "SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'october');", target);
            Assert.False((bool)(await command.ExecuteScalarAsync() ?? true));
        }

        internal OctoberDbContext CreateOctoberContext()
        {
            TestResourceScope.ValidateOwnedTarget(ConnectionString);
            return new OctoberDbContext(new DbContextOptionsBuilder<OctoberDbContext>()
                .UseNpgsql(ConnectionString, npgsql =>
                    npgsql.MigrationsHistoryTable("__OctoberMigrationsHistory", "october"))
                .Options);
        }

        internal Process StartGenerator(string requestJson)
        {
            string repositoryRoot = FindRepositoryRoot();
            string project = Path.Combine(repositoryRoot,
                "src", "dotnet", "Vulgata.OctoberGenerator", "Vulgata.OctoberGenerator.csproj");
            ProcessStartInfo startInfo = new("dotnet")
            {
                WorkingDirectory = OwnedRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            string? path = Environment.GetEnvironmentVariable("PATH");
            string? systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
            string? temp = Environment.GetEnvironmentVariable("TEMP");
            string? tmp = Environment.GetEnvironmentVariable("TMP");
            string? dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
            startInfo.Environment.Clear();
            if (!string.IsNullOrWhiteSpace(path)) startInfo.Environment["PATH"] = path;
            if (!string.IsNullOrWhiteSpace(systemRoot)) startInfo.Environment["SystemRoot"] = systemRoot;
            if (!string.IsNullOrWhiteSpace(temp)) startInfo.Environment["TEMP"] = temp;
            if (!string.IsNullOrWhiteSpace(tmp)) startInfo.Environment["TMP"] = tmp;
            if (!string.IsNullOrWhiteSpace(dotnetRoot)) startInfo.Environment["DOTNET_ROOT"] = dotnetRoot;
            startInfo.Environment["DOTNET_CLI_HOME"] = Path.Combine(OwnedRoot, "dotnet-cli");
            startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
            startInfo.Environment["DOTNET_NOLOGO"] = "1";
            startInfo.Environment["DOTNET_ENVIRONMENT"] = "Production";
            startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
            startInfo.Environment["ConnectionStrings__DefaultConnection"] = ConnectionString;
            startInfo.Environment["DataProtection__KeyRingPath"] = Path.Combine(OwnedRoot, "keys");
            startInfo.Environment["DataProtection__ApplicationName"] = DataProtectionApplicationName;
            startInfo.Environment["October__Mode"] = "prepare";
            startInfo.Environment["October__TargetId"] = TargetId;
            startInfo.Environment["October__BatchId"] = BatchId.ToString("D", CultureInfo.InvariantCulture);
            startInfo.Environment["October__SetupRequestJson"] = requestJson;
            startInfo.Environment["October__Authority__Id"] = AuthorityId;
            startInfo.Environment["October__Targets__0__TargetId"] = TargetId;
            startInfo.Environment["October__Targets__0__CanonicalDatabaseIdentity"] = CanonicalDatabaseIdentity;
            startInfo.Environment["October__Operator__CredentialSource"] = "protected-file";
            startInfo.Environment["October__Operator__CredentialFile"] =
                Path.Combine(OwnedRoot, "operator.credentials.protected");
            startInfo.Environment["October__Providers__ConfigurationFile"] =
                Path.Combine(OwnedRoot, "providers.json");

            startInfo.ArgumentList.Add("run");
            startInfo.ArgumentList.Add("--project");
            startInfo.ArgumentList.Add(project);
            startInfo.ArgumentList.Add("--no-build");
            startInfo.ArgumentList.Add("--no-restore");
            Assert.Equal(Path.GetFullPath(OwnedRoot), Path.GetFullPath(startInfo.WorkingDirectory));
            Assert.DoesNotContain(OperatorPassword, string.Join(' ', startInfo.ArgumentList), StringComparison.Ordinal);
            Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("The owned generator child did not start.");
            return process;
        }

        internal async Task<(int ExitCode, string StandardOutput, string StandardError)> WaitForGeneratorAsync(
            Process process)
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromMinutes(3));
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            Task<string> stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None);
                }
                catch (InvalidOperationException)
                {
                }

                throw new TimeoutException("The owned generator child exceeded its bounded startup deadline.");
            }

            return (process.ExitCode, await stdoutTask, await stderrTask);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;
            _disposed = true;
            await Services.DisposeAsync();
            await _protection.Services.DisposeAsync();
            await DropOwnedDatabaseAsync(_administratorConnectionString, _databaseName, ConnectionString);
            DeleteOwnedRoot(OwnedRoot, _runId);
            _output.WriteLine($"verified-database-cleanup={_databaseName}");
            _output.WriteLine($"verified-temp-cleanup={_runId}");
        }

        private static DataProtectionProviderRegistration CreateDataProtectionProvider(string keyRingPath)
        {
            ServiceCollection services = new();
            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
                .SetApplicationName(DataProtectionApplicationName);
            ServiceProvider provider = services.BuildServiceProvider();
            return new DataProtectionProviderRegistration(provider,
                provider.GetRequiredService<IDataProtectionProvider>());
        }

        private static void WriteProtectedInputs(
            IDataProtectionProvider protection,
            string credentialFile,
            string providerFile,
            string userName,
            string password,
            string runId)
        {
            string credentials = JsonSerializer.Serialize(new
            {
                userName,
                password,
                authenticatorCode = (string?)null,
                recoveryCode = (string?)null,
            });
            string protectedCredentials = protection
                .CreateProtector("october-operator-credentials-v1")
                .Protect(credentials);
            File.WriteAllText(credentialFile, protectedCredentials);

            string workerKey = $"synthetic-worker-key-{runId}";
            string chatKey = $"synthetic-chat-key-{runId}";
            string workerCiphertext = protection.CreateProtector(SecretPurposes.LlmApiKey).Protect(workerKey);
            string chatCiphertext = protection.CreateProtector(SecretPurposes.LlmApiKey).Protect(chatKey);
            string providerDocument = JsonSerializer.Serialize(new
            {
                sourceIdentity = $"owned-generator-provider-{runId}",
                precedence = "only-source",
                workerProfile = "worker-profile",
                chatProfile = "chat-profile",
                profiles = new[]
                {
                    new
                    {
                        name = "worker-profile",
                        apiType = "chat-completions",
                        endpoint = "https://worker.invalid/v1",
                        model = "controlled-worker-model",
                        credentialReference = "worker-credential",
                    },
                    new
                    {
                        name = "chat-profile",
                        apiType = "chat-completions",
                        endpoint = "https://chat.invalid/v1",
                        model = "controlled-chat-model",
                        credentialReference = "chat-credential",
                    },
                },
                protectedCredentials = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["worker-credential"] = workerCiphertext,
                    ["chat-credential"] = chatCiphertext,
                },
            });
            File.WriteAllText(providerFile, providerDocument);
        }

        private static async Task<(Guid SystemId, Guid RepositoryId, Guid GlobalContextId)> MigrateAuthoritySchemasAndSeedAsync(
            ServiceProvider services,
            string subjectId,
            string password)
        {
            await using (AsyncServiceScope scope = services.CreateAsyncScope())
            {
                TestResourceScope.ValidateOwnedTarget(
                    scope.ServiceProvider.GetRequiredService<OctoberAuthorityConfiguration>().DataConnectionString);
                ApplicationDbContext identity = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await identity.Database.MigrateAsync();
                RoleManager<IdentityRole> roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                IdentityResult role = await roles.CreateAsync(new IdentityRole(RoleNames.SystemOwner));
                Assert.True(role.Succeeded, string.Join(";", role.Errors.Select(error => error.Code)));
                UserManager<ApplicationUser> users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                ApplicationUser user = new()
                {
                    Id = subjectId,
                    UserName = CreateOperatorUserName(subjectId),
                    Email = CreateOperatorUserName(subjectId),
                    EmailConfirmed = true,
                    LockoutEnabled = true,
                };
                IdentityResult created = await users.CreateAsync(user, password);
                Assert.True(created.Succeeded, string.Join(";", created.Errors.Select(error => error.Code)));
                IdentityResult assignment = await users.AddToRoleAsync(user, RoleNames.SystemOwner);
                Assert.True(assignment.Succeeded, string.Join(";", assignment.Errors.Select(error => error.Code)));
            }

            await using AsyncServiceScope managementScope = services.CreateAsyncScope();
            TestResourceScope.ValidateOwnedTarget(
                managementScope.ServiceProvider.GetRequiredService<OctoberAuthorityConfiguration>().DataConnectionString);
            await using VulgataDbContext management = await managementScope.ServiceProvider
                .GetRequiredService<IDbContextFactory<VulgataDbContext>>()
                .CreateDbContextAsync();
            await management.Database.MigrateAsync();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            SystemEntity system = new("Generator tracer system", "Owned test data", "System advisory context", now);
            Repository repository = system.AddRepository(
                "Generator tracer repository",
                "https://example.invalid/generator-tracer.git",
                "Owned test data",
                "Repository advisory context",
                now);
            GlobalContext global = new("Global advisory context", now);
            management.Systems.Add(system);
            management.Repositories.Add(repository);
            management.SystemOwnerAssignments.Add(SystemOwnerAssignment.Create(system.Id, subjectId, now));
            management.GlobalContexts.Add(global);
            await management.SaveChangesAsync();
            return (system.Id, repository.Id, global.Id);
        }

        private static string CreateOperatorUserName(string subjectId) =>
            $"generator-tracer-{subjectId}@example.invalid";

        private static async Task DropOwnedDatabaseAsync(
            string administrator,
            string databaseName,
            string targetConnection)
        {
            Assert.StartsWith(DatabasePrefix, databaseName, StringComparison.Ordinal);
            Assert.Matches("^vulgata_story184_generator_[0-9a-f]{32}$", databaseName);
            TestResourceScope.ValidateOwnedTarget(targetConnection);
            NpgsqlConnectionStringBuilder builder = new(administrator) { Pooling = false, Timeout = 5 };
            TestResourceScope.ValidateAdministrator(builder.ConnectionString);
            await using NpgsqlConnection connection = new(builder.ConnectionString);
            await connection.OpenAsync();
            await using (NpgsqlCommand terminate = new(
                "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @name AND pid <> pg_backend_pid();",
                connection))
            {
                terminate.Parameters.AddWithValue("name", databaseName);
                await terminate.ExecuteNonQueryAsync();
            }

            await using (NpgsqlCommand drop = new(
                $"DROP DATABASE IF EXISTS \"{databaseName}\";", connection))
            {
                await drop.ExecuteNonQueryAsync();
            }

            await using NpgsqlCommand exists = new(
                "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);", connection);
            exists.Parameters.AddWithValue("name", databaseName);
            Assert.False((bool)(await exists.ExecuteScalarAsync() ?? true),
                "The recorded test-created database must be absent after cleanup.");
        }

        private static string CreateOwnedRoot(string runId)
        {
            string parent = Path.Combine(Path.GetTempPath(), "vulgata-tests");
            Directory.CreateDirectory(parent);
            string root = Path.Combine(parent, "story184-generator-" + runId + "-" + Guid.NewGuid().ToString("N"));
            string resolvedParent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent));
            string resolvedRoot = Path.GetFullPath(root);
            Assert.StartsWith(resolvedParent + Path.DirectorySeparatorChar, resolvedRoot,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            Directory.CreateDirectory(resolvedRoot);
            return resolvedRoot;
        }

        private static void DeleteOwnedRoot(string root, string runId)
        {
            string parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(
                Path.Combine(Path.GetTempPath(), "vulgata-tests")));
            string resolved = Path.GetFullPath(root);
            string prefix = "story184-generator-" + runId + "-";
            string leaf = Path.GetFileName(resolved);
            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            Assert.StartsWith(parent + Path.DirectorySeparatorChar, resolved, comparison);
            Assert.StartsWith(prefix, leaf, StringComparison.Ordinal);
            if (Directory.Exists(resolved))
                Directory.Delete(resolved, recursive: true);
            Assert.False(Directory.Exists(resolved), "The exact owned temporary root must be removed.");
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("The assigned repository root was not found.");
        }
    }

    private sealed record DataProtectionProviderRegistration(
        ServiceProvider Services,
        IDataProtectionProvider Provider);
}
