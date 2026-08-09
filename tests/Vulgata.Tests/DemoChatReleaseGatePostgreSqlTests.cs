using Microsoft.Playwright;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Vulgata.Infrastructure.Git;
using Vulgata.Tests.ReleaseGate;

namespace Vulgata.Tests;

[Collection(Story115ProvisioningConfigurationCollection.Name)]
public sealed class DemoChatReleaseGatePostgreSqlTests
{
    [Fact]
    public void ProvisioningHost_BeforeProductProvisioning_UsesOnlyNonceOwnedStorage()
    {
        string? originalCodeGraphDataRoot = Environment.GetEnvironmentVariable(
            "CodeGraph__DataRootPath");
        string? originalDataProtectionApplicationName = Environment.GetEnvironmentVariable(
            "DataProtection__ApplicationName");
        string? originalDataProtectionKeyRingPath = Environment.GetEnvironmentVariable(
            "DataProtection__KeyRingPath");
        string runDirectory = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story115-provisioning-configuration",
            Guid.NewGuid().ToString("N"));
        try
        {
            Story115ProvisioningConfiguration configuration =
                Story115ExecutableFinalRunOperations.ResolveProvisioningConfigurationForTests(
                    runDirectory);
            Assert.Equal(
                Path.Combine(runDirectory, "codegraph", "repositories"),
                configuration.GitWorkingCopyRootPath);
            Assert.Equal("Vulgata.Story115.ReleaseGate", configuration.DataProtectionApplicationName);
            Assert.Equal(Path.Combine(runDirectory, "keys"), configuration.DataProtectionKeyRingPath);
            Assert.True(configuration.ProcessConfigurationAppliedBeforeHostCreation);
            Assert.DoesNotContain(
                Path.Combine("src", "dotnet", "Vulgata.Web", "Data", "profiling"),
                configuration.GitWorkingCopyRootPath,
                StringComparison.OrdinalIgnoreCase);
            Assert.Equal(
                originalCodeGraphDataRoot,
                Environment.GetEnvironmentVariable("CodeGraph__DataRootPath"));
            Assert.Equal(
                originalDataProtectionApplicationName,
                Environment.GetEnvironmentVariable("DataProtection__ApplicationName"));
            Assert.Equal(
                originalDataProtectionKeyRingPath,
                Environment.GetEnvironmentVariable("DataProtection__KeyRingPath"));
        }
        finally
        {
            if (Directory.Exists(runDirectory))
            {
                Directory.Delete(runDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void BrowserEvidence_UncitedMaterialClaim_IsRejected()
    {
        Story115ObservedClaim[] claims = [new([])];

        Assert.Throws<InvalidOperationException>(() =>
            Story115ReleaseHarness.ValidateObservedAnswer(claims));
    }

    [Theory]
    [InlineData("", 1, 1, true)]
    [InlineData("../secret.cs", 1, 1, true)]
    [InlineData("src/Flow.cs", 0, 1, true)]
    [InlineData("src/Flow.cs", 2, 1, true)]
    [InlineData("src/Flow.cs", 1, 1, false)]
    public void BrowserEvidence_EmptyMalformedOrUnoperatedCitationDetails_IsRejected(
        string path,
        int lineStart,
        int lineEnd,
        bool detailsOperated)
    {
        Story115ObservedClaim[] claims =
        [
            new([new Story115ObservedCitation(path, lineStart, lineEnd, detailsOperated)]),
        ];

        Assert.Throws<InvalidOperationException>(() =>
            Story115ReleaseHarness.ValidateObservedAnswer(claims));
    }

    [Fact]
    public void ReleaseContract_AcceptedInputsAndFinalPath_AreClosedAndSourceBound()
    {
        Story115ReleaseContract contract = Story115ReleaseHarness.Contract;

        Assert.Equal("vulgata_story115_", contract.DatabasePrefix);
        Assert.Equal("mbs_account", contract.AccountRepository.Alias);
        Assert.Equal("refs/tags/vulgata/August15th-sample", contract.AccountRepository.Ref);
        Assert.Equal(
            "19c60cd3f3976b7697c487c7bafd96142e3a7625",
            contract.AccountRepository.Commit);
        Assert.Equal("mbs_channel", contract.ChannelRepository.Alias);
        Assert.Equal("refs/tags/vulgata/August15th-sample", contract.ChannelRepository.Ref);
        Assert.Equal(
            "47f6b627a7598f862873610413eec1e1e7bf46bf",
            contract.ChannelRepository.Commit);
        Assert.Equal(
            [
                "migrate",
                "product-provision",
                "profile",
                "pass1-account",
                "pass1-channel",
                "exact-cross-repository-resolution",
                "pass2",
                "chat-retrieval",
                "chat-selection",
                "chat-currentness",
                "chat-final-authorization",
                "source-bound-image",
                "label-inspection",
                "curl-health",
                "identity-playwright-circuit",
                "inaccessible-scope",
                "post-selection-revocation",
            ],
            contract.OrderedFinalPath);
        Assert.Equal("docker/Dockerfile", contract.DockerfilePath);
        Assert.Equal(
            "tests/Vulgata.Tests/ReleaseGate/loopback-openai-provider.py",
            contract.ProviderFixturePath);
        Assert.Contains("IAdministratorRoleCoordinator", contract.ProductProvisioningServices);
        Assert.Contains("ISystemOwnershipCoordinator", contract.ProductProvisioningServices);
        Assert.Contains("ILlmProviderManagementCoordinator", contract.ProductProvisioningServices);
        Assert.Contains("ISystemLlmProviderOverrideCoordinator", contract.ProductProvisioningServices);
        Assert.DoesNotContain("DbSet", contract.ProductProvisioningServices);
        Assert.DoesNotContain("SQL", contract.ProductProvisioningServices);
        Assert.Single(contract.FinalImageBuildSteps);
        Assert.NotNull(typeof(IPlaywright));
    }

    [Fact]
    public void DatabaseOwnership_CryptographicNonceIsExactAndUnsafeNamesAreRejected()
    {
        Story115RunIdentity first = Story115ReleaseHarness.CreateRunIdentity();
        Story115RunIdentity second = Story115ReleaseHarness.CreateRunIdentity();

        Assert.Matches("^vulgata_story115_[0-9a-f]{32}$", first.DatabaseName);
        Assert.NotEqual(first.Nonce, second.Nonce);
        Assert.True(Story115ReleaseHarness.IsOwnedDatabaseName(first.DatabaseName));
        Assert.All(
            new[]
            {
                "postgres",
                "vulgata",
                "production",
                "vulgata_story115_",
                "vulgata_story115_not-hex",
                "vulgata_story115_00000000000000000000000000000000_extra",
            },
            name => Assert.False(Story115ReleaseHarness.IsOwnedDatabaseName(name)));
        Assert.All(first.ResourceNames, Story115ReleaseHarness.RequireOwnedResourceName);
    }

    [Fact]
    public async Task Lifecycle_PrimaryAndCleanupFailures_AreBothRetainedAfterCatalogAbsenceAttempt()
    {
        Story115RunIdentity identity = Story115ReleaseHarness.CreateRunIdentity();
        var operations = new RecordingReleaseOperations(failDrop: true);

        Story115ReleaseGateException exception = await Assert.ThrowsAsync<
            Story115ReleaseGateException>(() => Story115ReleaseHarness.ExecuteLifecycleAsync(
                identity,
                operations,
                _ =>
                {
                    operations.Events.Add("primary");
                    throw new InvalidOperationException("primary-failure");
                }));

        Assert.Equal("primary-failure", exception.PrimaryFailure?.Message);
        Assert.Equal("cleanup-drop-failure", exception.CleanupFailure?.Message);
        Assert.Equal(
            [
                "prepare-infrastructure",
                "provider-docker-desktop-postgres-preflight",
                "database-absent-before-create",
                "database-create",
                "migrate",
                "primary",
                "remove-app-provider",
                "terminate-exact-database",
                "drop-exact-database",
                "database-absent-after-drop",
                "remove-owned-image-network-key-state",
            ],
            operations.Events);
    }

    [Theory]
    [InlineData(true, false, "provider-preflight-failure")]
    [InlineData(false, true, "database-create-ambiguous-failure")]
    public async Task Lifecycle_BeforeProvenDatabaseOwnership_NeverMutatesNonceDatabase(
        bool failPreflight,
        bool failCreate,
        string expectedFailure)
    {
        Story115RunIdentity identity = Story115ReleaseHarness.CreateRunIdentity();
        var operations = new RecordingReleaseOperations(
            failDrop: false,
            failPreflight,
            failCreate);

        Story115ReleaseGateException exception = await Assert.ThrowsAsync<
            Story115ReleaseGateException>(() => Story115ReleaseHarness.ExecuteLifecycleAsync(
                identity,
                operations,
                _ => Task.CompletedTask));

        Assert.Equal(expectedFailure, exception.PrimaryFailure?.Message);
        Assert.DoesNotContain("terminate-exact-database", operations.Events);
        Assert.DoesNotContain("drop-exact-database", operations.Events);
        Assert.DoesNotContain("database-absent-after-drop", operations.Events);
        Assert.DoesNotContain("migrate", operations.Events);
        Assert.Equal("remove-owned-image-network-key-state", operations.Events[^1]);
    }

    [Fact]
    public async Task Lifecycle_AmbiguousCreateCatalogAbsent_VerifiesAbsenceWithoutDatabaseMutation()
    {
        Story115RunIdentity identity = Story115ReleaseHarness.CreateRunIdentity();
        var operations = new RecordingReleaseOperations(
            failDrop: false,
            failCreate: true,
            databaseExistsAfterAmbiguousCreate: false);

        Story115ReleaseGateException exception = await Assert.ThrowsAsync<
            Story115ReleaseGateException>(() => Story115ReleaseHarness.ExecuteLifecycleAsync(
                identity,
                operations,
                _ => Task.CompletedTask));

        Assert.Equal("database-create-ambiguous-failure", exception.PrimaryFailure?.Message);
        Assert.Null(exception.CleanupFailure);
        Assert.Contains("database-exists-after-ambiguous-create", operations.Events);
        Assert.DoesNotContain("terminate-exact-database", operations.Events);
        Assert.DoesNotContain("drop-exact-database", operations.Events);
        Assert.Equal("remove-owned-image-network-key-state", operations.Events[^1]);
    }

    [Fact]
    public async Task Lifecycle_AmbiguousCreateCatalogPresent_RemovesExactNonceDatabase()
    {
        Story115RunIdentity identity = Story115ReleaseHarness.CreateRunIdentity();
        var operations = new RecordingReleaseOperations(
            failDrop: false,
            failCreate: true,
            databaseExistsAfterAmbiguousCreate: true);

        Story115ReleaseGateException exception = await Assert.ThrowsAsync<
            Story115ReleaseGateException>(() => Story115ReleaseHarness.ExecuteLifecycleAsync(
                identity,
                operations,
                _ => Task.CompletedTask));

        Assert.Equal("database-create-ambiguous-failure", exception.PrimaryFailure?.Message);
        Assert.Null(exception.CleanupFailure);
        Assert.Equal(
            [
                "prepare-infrastructure",
                "provider-docker-desktop-postgres-preflight",
                "database-absent-before-create",
                "database-create",
                "remove-app-provider",
                "database-exists-after-ambiguous-create",
                "terminate-exact-database",
                "drop-exact-database",
                "database-absent-after-drop",
                "remove-owned-image-network-key-state",
            ],
            operations.Events);
    }

    [Fact]
    public async Task Lifecycle_AmbiguousCreateCatalogQueryFails_RetainsCleanupFailure()
    {
        Story115RunIdentity identity = Story115ReleaseHarness.CreateRunIdentity();
        var operations = new RecordingReleaseOperations(
            failDrop: false,
            failCreate: true,
            failDatabaseExistsQuery: true);

        Story115ReleaseGateException exception = await Assert.ThrowsAsync<
            Story115ReleaseGateException>(() => Story115ReleaseHarness.ExecuteLifecycleAsync(
                identity,
                operations,
                _ => Task.CompletedTask));

        Assert.Equal("database-create-ambiguous-failure", exception.PrimaryFailure?.Message);
        Assert.Equal("database-catalog-query-failure", exception.CleanupFailure?.Message);
        Assert.Contains("database-exists-after-ambiguous-create", operations.Events);
        Assert.DoesNotContain("terminate-exact-database", operations.Events);
        Assert.DoesNotContain("drop-exact-database", operations.Events);
        Assert.Equal("remove-owned-image-network-key-state", operations.Events[^1]);
    }

    [Fact]
    public void Evidence_SerializerEmitsOnlyClosedSanitizedContractFields()
    {
        Story115RunIdentity identity = Story115ReleaseHarness.CreateRunIdentity();
        Story115ReleaseEvidence evidence = Story115ReleaseEvidence.CreateFastContractEvidence(
            identity,
            "4aeed1862c7058bff06598bc75666a7475d5c41b");

        string json = Story115ReleaseHarness.SerializeEvidence(evidence);
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.Equal(
            [
                "schemaVersion",
                "candidate",
                "preparedRepositories",
                "profileFingerprint",
                "runtimePass2",
                "commands",
                "positiveAnswer",
                "negativeAuthorizationOutcome",
                "database",
                "cleanup",
                "knownLimitations",
            ],
            document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connectionstring", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("apikey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("prompt", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("response", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("G:\\", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            "tests/Vulgata.Tests/TestResults/story-11-5-release-evidence.json",
            Story115ReleaseHarness.ReleaseEvidencePath);
    }

    [Fact]
    public async Task PortsAndPolling_AreDistinctLoopbackOnlyAndStrictlyBounded()
    {
        await using Story115PortReservation reservation =
            Story115ReleaseHarness.ReserveDistinctPorts();

        Assert.Equal(3, reservation.Ports.Distinct().Count());
        Assert.DoesNotContain(8080, reservation.Ports);
        Assert.All(reservation.Ports, port => Assert.InRange(port, 1, 65535));

        int attempts = 0;
        bool reached = await Story115ReleaseHarness.PollAsync(
            _ => Task.FromResult(++attempts == 3),
            maximumAttempts: 3,
            delay: TimeSpan.Zero);
        bool exhausted = await Story115ReleaseHarness.PollAsync(
            _ => Task.FromResult(false),
            maximumAttempts: 2,
            delay: TimeSpan.Zero);

        Assert.True(reached);
        Assert.False(exhausted);
        Assert.Equal(3, attempts);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            Story115ReleaseHarness.PollAsync(
                _ => Task.FromResult(true),
                maximumAttempts: 61,
                delay: TimeSpan.Zero));
    }

    [Fact]
    public async Task ProviderStart_ReservedPorts_AreReleasedAtPublishBoundary()
    {
        await using Story115PortReservation reservation =
            Story115ReleaseHarness.ReserveDistinctPorts();
        var rebound = new List<TcpListener>();

        try
        {
            await Story115ReleaseHarness.StartProviderAtReservedPortBoundaryAsync(
                reservation,
                _ =>
                {
                    foreach (int port in reservation.Ports)
                    {
                        var listener = new TcpListener(IPAddress.Loopback, port);
                        listener.Start();
                        rebound.Add(listener);
                    }

                    return Task.CompletedTask;
                });
        }
        finally
        {
            foreach (TcpListener listener in rebound)
            {
                listener.Stop();
            }
        }

        Assert.Equal(reservation.Ports.Count, rebound.Count);
    }

    [Theory]
    [InlineData(170000, true)]
    [InlineData(179999, true)]
    [InlineData(160999, false)]
    [InlineData(180000, false)]
    public void PostgreSqlMajorVersion_Only17IsAccepted(
        int serverVersionNumber,
        bool accepted)
    {
        Exception? exception = Record.Exception(() =>
            Story115ReleaseHarness.RequirePostgreSql17ServerVersion(serverVersionNumber));

        Assert.Equal(accepted, exception is null);
    }

    [Fact]
    public async Task AppEnvironment_PlaintextFile_IsCurrentUserOnlyAndDeletable()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"vulgata-story115-{Guid.NewGuid():N}-app.env");
        try
        {
            await Story115ReleaseHarness.WriteCurrentUserOnlyFileAsync(
                path,
                "ConnectionStrings__DefaultConnection=secret\n");

            Assert.True(Story115ReleaseHarness.HasCurrentUserOnlyAccess(path));
        }
        finally
        {
            File.Delete(path);
        }

        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task ProviderFixture_SelfTest_EmitsAcceptedPass1Pass2AndChatShapes()
    {
        string repositoryRoot = FindRepositoryRoot();
        string fixturePath = Path.Combine(
            repositoryRoot,
            Story115ReleaseHarness.Contract.ProviderFixturePath.Replace('/', Path.DirectorySeparatorChar));
        var startInfo = new ProcessStartInfo("python")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(fixturePath);
        startInfo.ArgumentList.Add("--self-test");

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the Story 11.5 provider fixture self-test.");
        string standardOutput = await process.StandardOutput.ReadToEndAsync();
        string standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(process.ExitCode == 0, standardError);
        using JsonDocument document = JsonDocument.Parse(standardOutput);
        JsonElement root = document.RootElement;
        Assert.Equal("CodeLogic", root.GetProperty("pass1").GetProperty("content").GetProperty("documentType").GetString());
        Assert.Equal("BusinessLogic", root.GetProperty("pass2").GetProperty("content").GetProperty("documentType").GetString());
        Assert.Equal("prepared-chat-answer-v1", root.GetProperty("chat").GetProperty("schemaVersion").GetString());
        Assert.Equal(2, root.GetProperty("pass2").GetProperty("mainSteps").GetArrayLength());
    }

    [Fact]
    public async Task FinalActivation_MissingOrMalformedEnvironment_FailsBeforeAcquisition()
    {
        Assert.Equal(
            [
                "VULGATA_STORY115_RUN_FINAL",
                "VULGATA_STORY115_POSTGRES_ADMIN_CONNECTION",
                "VULGATA_STORY115_ACCOUNT_REPOSITORY_URL",
                "VULGATA_STORY115_CHANNEL_REPOSITORY_URL",
                "VULGATA_STORY115_CANDIDATE_COMMIT",
                "VULGATA_STORY115_CANDIDATE_TREE",
                "VULGATA_STORY115_PRODUCTION_MANIFEST_SHA256",
            ],
            Story115ReleaseHarness.EnvironmentContract.Select(variable => variable.Name));
        Assert.True(Story115ReleaseHarness.EnvironmentContract.Single(variable =>
            variable.Name.EndsWith("ADMIN_CONNECTION", StringComparison.Ordinal)).IsSecret);
        Assert.All(
            Story115ReleaseHarness.EnvironmentContract.Where(variable =>
                !variable.Name.EndsWith("ADMIN_CONNECTION", StringComparison.Ordinal)),
            variable => Assert.False(variable.IsSecret));

        var values = Story115ReleaseHarness.CreateValidEnvironmentForFastTests();
        values.Remove("VULGATA_STORY115_POSTGRES_ADMIN_CONNECTION");
        var operations = new RecordingFinalRunOperations();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Story115ReleaseHarness.RunFinalAsync(values, operations));

        Assert.Empty(operations.Events);
    }

    [Fact]
    public async Task FinalPlan_ValidActivation_ProducesOneClosedNonceOwnedExternalPlan()
    {
        var operations = new RecordingFinalRunOperations();

        await Story115ReleaseHarness.RunFinalAsync(
            Story115ReleaseHarness.CreateValidEnvironmentForFastTests(),
            operations);

        Story115FinalRunPlan plan = Assert.IsType<Story115FinalRunPlan>(operations.Plan);
        Assert.Equal(3, plan.Ports.Distinct().Count());
        Assert.All(plan.Identity.ResourceNames, Story115ReleaseHarness.RequireOwnedResourceName);
        Assert.Contains(plan.Identity.Nonce, plan.RunDirectory, StringComparison.Ordinal);
        Assert.Contains(plan.Identity.Nonce, plan.KeyRingDirectory, StringComparison.Ordinal);
        Assert.Contains(plan.Identity.Nonce, plan.AppEnvironmentFile, StringComparison.Ordinal);
        Assert.Equal(
            [
                "network-create",
                "provider-run",
                "source-bound-image-build",
                "image-label-inspect",
                "app-run",
                "curl-health-inspect",
                "identity-playwright-circuit",
            ],
            plan.Commands.Select(command => command.Name));
        Assert.Single(plan.Commands.Where(command => command.Name == "source-bound-image-build"));
        Assert.DoesNotContain(plan.Commands, command => command.Name == "postgres-run");
        Assert.DoesNotContain(plan.Identity.ResourceNames, resource =>
            resource.Contains("postgres", StringComparison.OrdinalIgnoreCase) ||
            resource.Contains("volume", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            plan.Commands.SelectMany(command => command.Arguments),
            argument => argument.Contains("postgres", StringComparison.OrdinalIgnoreCase) ||
            argument.Contains("volume", StringComparison.OrdinalIgnoreCase));

        Story115ExternalCommand network = plan.Commands.Single(command =>
            command.Name == "network-create");
        Assert.DoesNotContain("--internal", network.Arguments);

        Story115ExternalCommand provider = plan.Commands.Single(command =>
            command.Name == "provider-run");
        Assert.Contains("--host", provider.Arguments);
        Assert.DoesNotContain("--add-host", provider.Arguments);
        Assert.DoesNotContain(
            provider.Arguments,
            argument => argument.Contains("host-gateway", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("0.0.0.0", provider.Arguments);
        Assert.Contains("127.0.0.1", string.Join(' ', provider.Arguments), StringComparison.Ordinal);
        Assert.Contains($"127.0.0.1:{plan.Ports[2]}:8080", provider.Arguments);
        Assert.All(
            provider.Arguments.Where(argument => argument.StartsWith("127.0.0.1:", StringComparison.Ordinal)),
            publication => Assert.StartsWith("127.0.0.1:", publication, StringComparison.Ordinal));
        Assert.DoesNotContain("--app-port", provider.Arguments);
        Assert.Contains(
            "python:3.12-bookworm@sha256:9bed8554e926c07c6f908841d5ee88c33e8df9236b191526bbce81a9062ab43a",
            provider.Arguments);

        Story115ExternalCommand app = plan.Commands.Single(command => command.Name == "app-run");
        Assert.DoesNotContain("--publish", app.Arguments);
        Assert.Contains($"container:{plan.Identity.ProviderContainer}", app.Arguments);
        Assert.Contains(
            $"type=bind,source={plan.KeyRingDirectory},target=/var/lib/vulgata-dpkeys,readonly",
            app.Arguments);
        Assert.Contains(
            "curl --fail --silent --show-error http://127.0.0.1:8080/Account/Login",
            app.Arguments);
        Story115ExternalCommand build = plan.Commands.Single(command =>
            command.Name == "source-bound-image-build");
        Assert.Equal(3, build.Arguments.Count(argument => argument == "--build-arg"));
        Assert.DoesNotContain("--label", build.Arguments);
        Assert.DoesNotContain(
            plan.Environment.PostgreSqlAdministratorConnection,
            plan.Commands.SelectMany(command => command.Arguments));

        Story115ExternalCommand preflight =
            Story115ReleaseHarness.CreateDockerDesktopPostgreSqlPreflightCommand(
                plan.Identity,
                5432);
        Assert.Equal("provider-docker-desktop-postgres-preflight", preflight.Name);
        Assert.Equal("docker", preflight.FileName);
        Assert.Equal("exec", preflight.Arguments[0]);
        Assert.Equal(plan.Identity.ProviderContainer, preflight.Arguments[1]);
        Assert.Contains("host.docker.internal", preflight.Arguments[^1], StringComparison.Ordinal);
        Assert.Contains("socket.getaddrinfo", preflight.Arguments[^1], StringComparison.Ordinal);
        Assert.Contains("socket.create_connection", preflight.Arguments[^1], StringComparison.Ordinal);
        Assert.DoesNotContain("Password", preflight.Arguments[^1], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gate", preflight.Arguments[^1], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessExecutor_BoundedCommand_ProducesHashOnlyEvidenceMaterial()
    {
        var executor = new Story115ProcessExecutor();
        var command = new Story115ExternalCommand(
            "dotnet-version",
            "dotnet",
            ["--version"]);

        Story115ProcessResult result = await executor.ExecuteAsync(
            command,
            FindRepositoryRoot(),
            new Dictionary<string, string>(StringComparer.Ordinal),
            TimeSpan.FromSeconds(15));

        Assert.Equal(0, result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.StandardOutput));
        Assert.Matches("^[0-9a-f]{64}$", result.LogSha256);
        var evidence = result.ToEvidence();
        Assert.Equal("dotnet-version", evidence.CommandName);
        Assert.Null(evidence.RelativeLogPath);
        Assert.Equal(result.LogSha256, evidence.LogSha256);
    }

    [Fact]
    public void ProductOrchestrator_CallGraph_IsClosedToAcceptedServicesAndSevenSlotPass2()
    {
        Assert.Equal(
            [
                "UserManager.CreateAsync",
                "IAdministratorRoleCoordinator.AssignInitialRoleAsync",
                "ISystemRepository.AddAsync",
                "ISystemOwnershipCoordinator.AssignOwnerAsync",
                "IRepositoryManagementCoordinator.CreateAsync",
                "ILlmProviderManagementCoordinator.CreateAsync",
                "ISystemLlmProviderOverrideCoordinator.UpsertAsync",
                "IRepositoryProfilingCoordinator.StartSystemRepositoryAsync",
                "SequentialScanRunner.StartAsync",
                "SequentialScanRunner.RunPass1Async",
                "ExactCrossRepositoryResolutionCoordinator.ResolveAfterPass1Async",
                "IPreparedDemoPass2FlowPlanner.PlanAsync",
                "SequentialScanRunner.RunPreparedPass2Async",
            ],
            Story115ProductOrchestrator.RequiredProductCalls);
        Assert.Equal(
            [
                "PreparedFlowSchemaIdentity",
                "ManifestId",
                "FlowId",
                "SystemId",
                "RepositoryId",
                "TraceEvidence",
                "CodeLogicDocumentVersions",
            ],
            Story115ProductOrchestrator.Pass2PayloadSlots);
    }

    [Fact]
    public void ExecutableFinalOperations_Construction_IsAcquisitionFreeAndCoversFinalCircuit()
    {
        var operations = new Story115ExecutableFinalRunOperations(FindRepositoryRoot());

        Assert.IsAssignableFrom<IStory115FinalRunOperations>(operations);
        Assert.IsAssignableFrom<IStory115ReleaseOperations>(operations);
        Assert.Equal(
            [
                "postgresql-17-administrator-database-lifecycle",
                "production-migrations-and-product-services",
                "isolated-worker-chat-provider",
                "single-source-bound-image",
                "label-and-health-inspection",
                "identity-interactive-positive",
                "identity-negative",
                "post-selection-revocation",
                "sanitized-evidence",
                "unconditional-owned-cleanup",
            ],
            Story115ExecutableFinalRunOperations.ConcreteStages);
        Assert.Equal(
            ["Loaded", "Empty", "Error"],
            Story115ExecutableFinalRunOperations.InteractiveWaitStates);
    }

    [Fact]
    public async Task FinalEntry_ActivationMissing_ReturnsBeforePlanAndAcquisitionAsync()
    {
        bool executed = await Story115ReleaseHarness.RunActivatedFinalAsync(
            new Dictionary<string, string>(StringComparer.Ordinal),
            Path.Combine(Path.GetTempPath(), "story115-root-must-not-be-inspected"));

        Assert.False(executed);
    }

    [Fact]
    public async Task FinalEntry_ProcessEnvironment_ProvidesTheSoleT064ExecutionPathAsync()
    {
        bool expected = StringComparer.Ordinal.Equals(
            Environment.GetEnvironmentVariable("VULGATA_STORY115_RUN_FINAL"),
            "1");

        bool executed = await Story115ReleaseHarness.RunFromProcessEnvironmentAsync(
            FindRepositoryRoot());

        Assert.Equal(expected, executed);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        for (int depth = 0; depth < 12 && current is not null; depth++, current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "Vulgata.slnx")))
            {
                return current.FullName;
            }
        }

        throw new InvalidOperationException("Could not locate the Vulgata repository root.");
    }

    private sealed class RecordingReleaseOperations(
        bool failDrop,
        bool failPreflight = false,
        bool failCreate = false,
        bool databaseExistsAfterAmbiguousCreate = false,
        bool failDatabaseExistsQuery = false) : IStory115ReleaseOperations
    {
        internal List<string> Events { get; } = [];

        public Task PrepareInfrastructureAsync(
            Story115RunIdentity identity,
            CancellationToken cancellationToken)
        {
            Events.Add("prepare-infrastructure");
            return Task.CompletedTask;
        }

        public Task PreflightDockerDesktopPostgreSqlAsync(
            Story115RunIdentity identity,
            CancellationToken cancellationToken)
        {
            Events.Add("provider-docker-desktop-postgres-preflight");
            return failPreflight
                ? Task.FromException(new InvalidOperationException("provider-preflight-failure"))
                : Task.CompletedTask;
        }

        public Task ProveDatabaseAbsentAsync(
            Story115RunIdentity identity,
            bool afterDrop,
            CancellationToken cancellationToken)
        {
            Events.Add(afterDrop
                ? "database-absent-after-drop"
                : "database-absent-before-create");
            return Task.CompletedTask;
        }

        public Task CreateDatabaseAsync(
            Story115RunIdentity identity,
            CancellationToken cancellationToken)
        {
            Events.Add("database-create");
            return failCreate
                ? Task.FromException(new InvalidOperationException("database-create-ambiguous-failure"))
                : Task.CompletedTask;
        }

        public Task<bool> ReconcileDatabaseExistsAfterAmbiguousCreateAsync(
            Story115RunIdentity identity,
            CancellationToken cancellationToken)
        {
            Events.Add("database-exists-after-ambiguous-create");
            return failDatabaseExistsQuery
                ? Task.FromException<bool>(
                    new InvalidOperationException("database-catalog-query-failure"))
                : Task.FromResult(databaseExistsAfterAmbiguousCreate);
        }

        public Task MigrateAsync(
            Story115RunIdentity identity,
            CancellationToken cancellationToken)
        {
            Events.Add("migrate");
            return Task.CompletedTask;
        }

        public Task RemoveAppAndProviderAsync(
            Story115RunIdentity identity,
            CancellationToken cancellationToken)
        {
            Events.Add("remove-app-provider");
            return Task.CompletedTask;
        }

        public Task TerminateDatabaseConnectionsAsync(
            Story115RunIdentity identity,
            CancellationToken cancellationToken)
        {
            Events.Add("terminate-exact-database");
            return Task.CompletedTask;
        }

        public Task DropDatabaseAsync(
            Story115RunIdentity identity,
            CancellationToken cancellationToken)
        {
            Events.Add("drop-exact-database");
            return failDrop
                ? Task.FromException(new InvalidOperationException("cleanup-drop-failure"))
                : Task.CompletedTask;
        }

        public Task RemoveRemainingOwnedStateAsync(
            Story115RunIdentity identity,
            CancellationToken cancellationToken)
        {
            Events.Add("remove-owned-image-network-key-state");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingFinalRunOperations : IStory115FinalRunOperations
    {
        internal List<string> Events { get; } = [];
        internal Story115FinalRunPlan? Plan { get; private set; }

        public Task ExecuteAsync(
            Story115FinalRunPlan plan,
            CancellationToken cancellationToken)
        {
            Events.Add("acquired");
            Plan = plan;
            return Task.CompletedTask;
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class Story115ProvisioningConfigurationCollection
{
    public const string Name = "Story 11.5 provisioning configuration";
}
