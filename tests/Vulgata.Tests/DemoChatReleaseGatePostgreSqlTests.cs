using Microsoft.Playwright;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.CodeGraph;
using Vulgata.Infrastructure.Git;
using Vulgata.Tests.ReleaseGate;
using Vulgata.Web.Data;

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
    public void OptionAProvisioner_PublicWebComposition_ResolvesRequiredProductServices()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] =
                "Host=127.0.0.1;Database=not-opened;Username=not-opened;Password=not-opened",
            ["CodeGraph:DataRootPath"] = "/var/lib/vulgata-codegraph",
            ["DataProtection:KeyRingPath"] = "/var/lib/vulgata-dpkeys",
            ["DataProtection:ApplicationName"] = "Vulgata.Story115.ReleaseGate",
        });

        VulgataWebComposition.ConfigureServices(builder);

        using ServiceProvider provider = builder.Services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });
        using IServiceScope scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RoleSeeder>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAdministratorRoleCoordinator>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISystemOwnershipCoordinator>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRepositoryManagementCoordinator>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRepositoryProfilingCoordinator>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ILlmProviderManagementCoordinator>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISystemLlmProviderOverrideCoordinator>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<SequentialScanRunner>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPreparedDemoPass2FlowPlanner>());
    }

    [Fact]
    public async Task OptionAProvisioner_Publish_ContainsOnlyThreeMountCandidateFilesAsync()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectPath = Path.Combine(
            repositoryRoot,
            "tests",
            "Vulgata.Tests",
            "ReleaseGate",
            "Provisioner",
            "Vulgata.Story115.Provisioner.csproj");
        string outputPath = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story115-provisioner-publish-" + Guid.NewGuid().ToString("N"));
        try
        {
            var startInfo = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = repositoryRoot,
            };
            foreach (string argument in new[]
            {
                "publish",
                projectPath,
                "--configuration",
                "Release",
                "--no-restore",
                "--disable-build-servers",
                "--maxcpucount:1",
                "-nodeReuse:false",
                "-p:UseSharedCompilation=false",
                "--output",
                outputPath,
            })
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(startInfo) ??
                throw new InvalidOperationException("Failed to publish the Story 11.5 provisioner.");
            Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            string standardOutput = await standardOutputTask;
            string standardError = await standardErrorTask;
            Assert.True(process.ExitCode == 0, standardOutput + Environment.NewLine + standardError);

            string[] mountCandidates = Directory.EnumerateFiles(outputPath)
                .Select(Path.GetFileName)
                .Where(name => name!.StartsWith("Vulgata.", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .ToArray()!;
            Assert.Equal(
                [
                    "Vulgata.Story115.Provisioner.deps.json",
                    "Vulgata.Story115.Provisioner.dll",
                    "Vulgata.Story115.Provisioner.runtimeconfig.json",
                ],
                mountCandidates);
            Assert.DoesNotContain(
                mountCandidates,
                name => new[]
                {
                    "Vulgata.Core.dll",
                    "Vulgata.Infrastructure.dll",
                    "Vulgata.Shared.dll",
                    "Vulgata.Agents.dll",
                    "Vulgata.Web.dll",
                }.Contains(name, StringComparer.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task OptionAProvisioner_Contract_IsClosedImageOwnedAndSevenSlotAsync()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectPath = Path.Combine(
            repositoryRoot,
            "tests",
            "Vulgata.Tests",
            "ReleaseGate",
            "Provisioner",
            "Vulgata.Story115.Provisioner.csproj");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = repositoryRoot,
        };
        foreach (string argument in new[]
        {
            "run",
            "--project",
            projectPath,
            "--configuration",
            "Release",
            "--no-build",
            "--no-restore",
            "--",
            "--describe-contract",
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Failed to describe the Story 11.5 provisioner contract.");
        Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        string standardOutput = await standardOutputTask;
        string standardError = await standardErrorTask;
        Assert.True(process.ExitCode == 0, standardError);

        using JsonDocument document = JsonDocument.Parse(standardOutput);
        JsonElement root = document.RootElement;
        Assert.Equal("vulgata.story115.provisioner-contract/1", root.GetProperty("schemaIdentity").GetString());
        Assert.Equal("/app", root.GetProperty("productAssemblyRoot").GetString());
        Assert.Equal(
            [
                "Vulgata.Web",
                "Vulgata.Core",
                "Vulgata.Infrastructure",
                "Vulgata.Agents",
                "Vulgata.Shared",
                "Vulgata.Web.ViewModels",
            ],
            root.GetProperty("imageProductAssemblyAllowlist")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray());
        Assert.Equal(
            [
                "/vulgata/sources/mbs_account/.git",
                "/vulgata/sources/mbs_channel/.git",
            ],
            root.GetProperty("mountedRepositorySafeDirectories")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray());
        Assert.Equal("0.5.1", root.GetProperty("codeGraphContextVersion").GetString());
        Assert.Equal(
            14_400,
            root.GetProperty("wholeRepositoryProcessTimeoutSeconds").GetInt32());
        Assert.Equal(65536, root.GetProperty("maximumInputBytes").GetInt32());
        Assert.Equal(65536, root.GetProperty("maximumResultBytes").GetInt32());
        Assert.Equal(
            [
                "SchemaIdentity",
                "RunNonce",
                "ConnectionString",
                "AccountRepositoryUri",
                "ChannelRepositoryUri",
                "WorkerEndpoint",
                "ChatEndpoint",
                "ProviderApiKey",
                "DataProtectionApplicationName",
                "AdministratorEmail",
                "AdministratorPassword",
                "OwnerEmail",
                "OwnerPassword",
                "NegativeEmail",
                "NegativePassword",
            ],
            root.GetProperty("inputProperties")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray());
        Assert.Equal("temp-file-atomic-rename-no-overwrite", root.GetProperty("resultPublication").GetString());
        Assert.Equal(
            [
                "SchemaIdentity",
                "RunNonceHash",
                "MigrationOutcome",
                "ProductStageOutcome",
                "SystemId",
                "AccountRepositoryId",
                "ChannelRepositoryId",
                "AccountScanRunId",
                "ChannelScanRunId",
                "AccountScanExecutionId",
                "ChannelScanExecutionId",
                "AccountAlias",
                "AccountRef",
                "AccountCommit",
                "ChannelAlias",
                "ChannelRef",
                "ChannelCommit",
                "ProfileFingerprint",
                "RuntimeSupportFingerprint",
                "OrdinaryPass1DispatchCount",
                "Pass2DispatchCount",
                "Pass2CommitCount",
                "CodeGraphContextVersion",
                "CodeGraphRequirementsLockSha256",
                "CompletedAtUtc",
                "ContentHash",
            ],
            root.GetProperty("resultProperties")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray());
        Assert.Equal(
            [
                "mbs_account|refs/tags/vulgata/August15th-sample|19c60cd3f3976b7697c487c7bafd96142e3a7625",
                "mbs_channel|refs/tags/vulgata/August15th-sample|47f6b627a7598f862873610413eec1e1e7bf46bf",
            ],
            root.GetProperty("acceptedProfiledRevisions")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray());
        Assert.Equal(2, root.GetProperty("ordinaryPass1DispatchCount").GetInt32());
        Assert.Equal(2, root.GetProperty("runtimeFlowRepositoryCount").GetInt32());
        Assert.Equal(1, root.GetProperty("pass2DispatchCount").GetInt32());
        Assert.Equal(1, root.GetProperty("pass2CommitCount").GetInt32());
        Assert.Equal(
            [
                "Vulgata.Story115.Provisioner.dll",
                "Vulgata.Story115.Provisioner.deps.json",
                "Vulgata.Story115.Provisioner.runtimeconfig.json",
            ],
            root.GetProperty("mountCandidates")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray());
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
            root.GetProperty("pass2PayloadSlots")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray());
        string[] services = root.GetProperty("requiredProductServices")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
        Assert.Contains("IRepositoryProfilingCoordinator", services);
        Assert.Contains("SequentialScanRunner", services);
        Assert.Contains("IPreparedDemoPass2FlowPlanner", services);
        Assert.DoesNotContain("DbSet", standardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("reflection", standardOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OptionAProvisioner_ClosedInput_RejectsUnknownSecretWithoutDiagnosticLeakAsync()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectPath = Path.Combine(
            repositoryRoot,
            "tests",
            "Vulgata.Tests",
            "ReleaseGate",
            "Provisioner",
            "Vulgata.Story115.Provisioner.csproj");
        string runPath = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story115-provisioner-contract-" + Guid.NewGuid().ToString("N"));
        string inputPath = Path.Combine(runPath, "input.json");
        string outputPath = Path.Combine(runPath, "result.json");
        const string secretSentinel = "must-not-escape-story115-secret";
        Directory.CreateDirectory(runPath);
        try
        {
            await File.WriteAllTextAsync(
                inputPath,
                $$"""
                {"schemaIdentity":"vulgata.story115.provisioner-input/1","unexpectedSecret":"{{secretSentinel}}"}
                """);
            var startInfo = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = repositoryRoot,
            };
            foreach (string argument in new[]
            {
                "run",
                "--project",
                projectPath,
                "--configuration",
                "Release",
                "--no-build",
                "--no-restore",
                "--",
                "--input",
                inputPath,
                "--output",
                outputPath,
            })
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(startInfo) ??
                throw new InvalidOperationException("Failed to run the Story 11.5 provisioner input contract.");
            Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            string standardOutput = await standardOutputTask;
            string standardError = await standardErrorTask;

            Assert.Equal(10, process.ExitCode);
            Assert.Empty(standardOutput);
            Assert.Equal("Story 11.5 provisioning failed." + Environment.NewLine, standardError);
            Assert.DoesNotContain(secretSentinel, standardError, StringComparison.Ordinal);
            Assert.DoesNotContain(runPath, standardError, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(outputPath));
        }
        finally
        {
            if (Directory.Exists(runPath))
            {
                Directory.Delete(runPath, recursive: true);
            }
        }
    }

    [Fact]
    public void ProvisionerResult_HostValidator_RejectsUnknownAndDuplicateFields()
    {
        const string nonce = "0123456789abcdef0123456789abcdef";
        string valid = CreateValidProvisionerResultJson(nonce);
        string unknown = valid.Insert(valid.Length - 1, ",\"unexpected\":true");
        string duplicate = valid.Replace(
            "\"schemaIdentity\":\"vulgata.story115.provisioner-result/1\"",
            "\"schemaIdentity\":\"vulgata.story115.provisioner-result/1\",\"schemaIdentity\":\"vulgata.story115.provisioner-result/1\"",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() =>
            Story115ReleaseHarness.ValidateProvisionerResult(unknown, nonce));
        Assert.Throws<InvalidDataException>(() =>
            Story115ReleaseHarness.ValidateProvisionerResult(duplicate, nonce));
    }

    [Fact]
    public void ProvisionerResult_HostValidator_BindsNonceContentLockAndEveryRequiredValue()
    {
        const string nonce = "0123456789abcdef0123456789abcdef";
        string valid = CreateValidProvisionerResultJson(nonce);

        Story115ReleaseHarness.ValidateProvisionerResult(valid, nonce);
        Assert.All(
            new[]
            {
                CreateValidProvisionerResultJson("fedcba9876543210fedcba9876543210"),
                MutateProvisionerResult(valid, result =>
                    result["migrationOutcome"] = "tampered", resign: false),
                MutateProvisionerResult(valid, result =>
                    result["codeGraphRequirementsLockSha256"] = new string('a', 64)),
                MutateProvisionerResult(valid, result =>
                    result.Remove("completedAtUtc")),
                MutateProvisionerResult(valid, result =>
                    result["schemaIdentity"] = "vulgata.story115.provisioner-result/2"),
                MutateProvisionerResult(valid, result =>
                    result["profileFingerprint"] = "not-a-hash"),
                MutateProvisionerResult(valid, result =>
                    result["systemId"] = Guid.Empty.ToString()),
                MutateProvisionerResult(valid, result =>
                    result["completedAtUtc"] = "not-a-timestamp"),
            },
            invalid => Assert.Throws<InvalidDataException>(() =>
                Story115ReleaseHarness.ValidateProvisionerResult(invalid, nonce)));
    }

    [Fact]
    public async Task ProvisionerDriverBinding_ReplacedCollisionOrMissingAttestation_FailsBeforeProvisionerCreationAsync()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story115-driver-binding-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            foreach (string name in Story115ReleaseHarness.ProvisionerDriverFileNames)
            {
                await File.WriteAllTextAsync(Path.Combine(root, name), "candidate:" + name);
            }

            string candidate = new string('a', 40);
            string[] imageInventory = ["Vulgata.Core.dll", "Vulgata.Web.dll"];
            Story115ProvisionerDriverAttestation attestation =
                Story115ReleaseHarness.CreateProvisionerDriverAttestation(
                    root,
                    candidate,
                    imageInventory);

            Story115ReleaseHarness.ValidateProvisionerDriverAttestation(
                root,
                candidate,
                imageInventory,
                attestation);
            await File.WriteAllTextAsync(
                Path.Combine(root, Story115ReleaseHarness.ProvisionerDriverFileNames[0]),
                "replaced");
            Assert.Throws<InvalidDataException>(() =>
                Story115ReleaseHarness.ValidateProvisionerDriverAttestation(
                    root,
                    candidate,
                    imageInventory,
                    attestation));
            Assert.Throws<InvalidDataException>(() =>
                Story115ReleaseHarness.CreateProvisionerDriverAttestation(
                    root,
                    candidate,
                    [.. imageInventory, Story115ReleaseHarness.ProvisionerDriverFileNames[0]]));
            Assert.Throws<InvalidDataException>(() =>
                Story115ReleaseHarness.ValidateProvisionerDriverAttestation(
                    root,
                    candidate,
                    imageInventory,
                    attestation: null));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
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
    public async Task Cleanup_NonceRunDirectoryWithReadOnlyGitPackIndex_RemovesExactOwnedDirectoryAsync()
    {
        string runDirectory = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story115-" + Guid.NewGuid().ToString("N"));
        string packIndex = Path.Combine(runDirectory, ".git", "objects", "pack", "pack-test.idx");
        Directory.CreateDirectory(Path.GetDirectoryName(packIndex)!);
        await File.WriteAllTextAsync(packIndex, "test");
        File.SetAttributes(packIndex, FileAttributes.ReadOnly);
        FileStream? transientLease = new(
            packIndex,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None);

        try
        {
            Task cleanup = Story115ReleaseHarness.DeleteOwnedRunDirectoryAsync(runDirectory);
            await Task.Delay(TimeSpan.FromMilliseconds(300));
            transientLease.Dispose();
            transientLease = null;
            await cleanup;

            Assert.False(Directory.Exists(runDirectory));
        }
        finally
        {
            transientLease?.Dispose();
            if (File.Exists(packIndex))
            {
                File.SetAttributes(packIndex, FileAttributes.Normal);
            }

            if (Directory.Exists(runDirectory))
            {
                Directory.Delete(runDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Cleanup_PersistentGitPackIndexLease_ReportsFixedSanitizedFailureAsync()
    {
        string runDirectory = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story115-" + Guid.NewGuid().ToString("N"));
        string packIndex = Path.Combine(runDirectory, ".git", "objects", "pack", "pack-test.idx");
        Directory.CreateDirectory(Path.GetDirectoryName(packIndex)!);
        await File.WriteAllTextAsync(packIndex, "test");
        FileStream persistentLease = new(
            packIndex,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None);
        try
        {
            IOException exception = await Assert.ThrowsAsync<IOException>(() =>
                Story115ReleaseHarness.DeleteOwnedRunDirectoryAsync(runDirectory));

            Assert.Equal("The Story 11.5 nonce run directory could not be removed.", exception.Message);
            Assert.DoesNotContain(runDirectory, exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            persistentLease.Dispose();
            if (Directory.Exists(runDirectory))
            {
                Directory.Delete(runDirectory, recursive: true);
            }
        }
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
                "remove-provisioner",
                "remove-app-provider",
                "terminate-exact-database",
                "drop-exact-database",
                "database-absent-after-drop",
                "remove-owned-image-network-key-state",
            ],
            operations.Events);
    }

    [Fact]
    public async Task OptionALifecycle_AlwaysRemovesProvisionerBeforeAppProviderAndDatabase()
    {
        Story115RunIdentity identity = Story115ReleaseHarness.CreateRunIdentity();
        var operations = new RecordingReleaseOperations(failDrop: false);

        await Story115ReleaseHarness.ExecuteLifecycleAsync(
            identity,
            operations,
            _ => Task.CompletedTask);

        Assert.Equal(
            [
                "remove-provisioner",
                "remove-app-provider",
                "terminate-exact-database",
                "drop-exact-database",
                "database-absent-after-drop",
                "remove-owned-image-network-key-state",
            ],
            operations.Events.SkipWhile(value => value != "remove-provisioner"));
    }

    [Theory]
    [InlineData("prepare-infrastructure", false)]
    [InlineData("provider-docker-desktop-postgres-preflight", false)]
    [InlineData("database-absent-before-create", false)]
    [InlineData("migrate", true)]
    public async Task OptionALifecycle_FailureAfterEachDeterministicAcquisition_CleansOnlyProvenOwnershipAsync(
        string failurePoint,
        bool databaseOwnershipProven)
    {
        Story115RunIdentity identity = Story115ReleaseHarness.CreateRunIdentity();
        var operations = new RecordingReleaseOperations(
            failDrop: false,
            failAfterEvent: failurePoint);

        Story115ReleaseGateException exception = await Assert.ThrowsAsync<
            Story115ReleaseGateException>(() => Story115ReleaseHarness.ExecuteLifecycleAsync(
                identity,
                operations,
                _ => Task.CompletedTask));

        Assert.Equal($"failure-after-{failurePoint}", exception.PrimaryFailure?.Message);
        Assert.Equal(
            [
                "remove-provisioner",
                "remove-app-provider",
            ],
            operations.Events
                .SkipWhile(value => value != "remove-provisioner")
                .Take(2));
        Assert.Equal(
            databaseOwnershipProven,
            operations.Events.Contains("terminate-exact-database"));
        Assert.Equal(
            databaseOwnershipProven,
            operations.Events.Contains("drop-exact-database"));
        Assert.Equal(
            databaseOwnershipProven,
            operations.Events.Contains("database-absent-after-drop"));
        Assert.Equal("remove-owned-image-network-key-state", operations.Events[^1]);
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
                "remove-provisioner",
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
                "provisioner",
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
        Assert.Equal(
            [
                "buildCount",
                "provisionerImageId",
                "appImageId",
                "provisionerRemovedBeforeAppStart",
                "provisionerImageCreated",
                "driverInventorySha256",
                "imageAppInventorySha256",
                "driverFiles",
                "mountIdentities",
                "resultSha256",
            ],
            document.RootElement.GetProperty("provisioner")
                .EnumerateObject()
                .Select(property => property.Name));
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
                "candidate-commit-inspect",
                "candidate-tree-inspect",
                "candidate-driver-diff-inspect",
                "candidate-untracked-driver-inspect",
                "candidate-worktree-inspect",
                "provisioner-driver-publish",
                "source-bound-image-build",
                "image-label-inspect",
                "image-app-inventory-inspect",
                "network-create",
                "provider-run",
                "provisioner-run",
                "provisioner-image-inspect",
                "provisioner-remove",
                "provisioner-absence-inspect",
                "app-run",
                "app-image-inspect",
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
    public async Task FinalPlan_DriverPublishAndInventoryBinding_PrecedeProvisionerCreationAsync()
    {
        var operations = new RecordingFinalRunOperations();

        await Story115ReleaseHarness.RunFinalAsync(
            Story115ReleaseHarness.CreateValidEnvironmentForFastTests(),
            operations);

        Story115FinalRunPlan plan = Assert.IsType<Story115FinalRunPlan>(operations.Plan);
        Assert.Equal(
            [
                "candidate-commit-inspect",
                "candidate-tree-inspect",
                "candidate-driver-diff-inspect",
                "candidate-untracked-driver-inspect",
                "candidate-worktree-inspect",
                "provisioner-driver-publish",
                "source-bound-image-build",
                "image-label-inspect",
                "image-app-inventory-inspect",
            ],
            plan.Commands.Take(9).Select(command => command.Name));
        Story115ExternalCommand publish = plan.Commands.Single(command =>
            command.Name == "provisioner-driver-publish");
        string driverRoot = Path.Combine(plan.RunDirectory, "driver");
        Assert.Contains(driverRoot, publish.Arguments);
        Assert.Contains("--disable-build-servers", publish.Arguments);
        Assert.Contains("--maxcpucount:1", publish.Arguments);
        Assert.Contains("-nodeReuse:false", publish.Arguments);
        Assert.Contains("-p:UseSharedCompilation=false", publish.Arguments);
        Story115ExternalCommand inventory = plan.Commands.Single(command =>
            command.Name == "image-app-inventory-inspect");
        Assert.Contains(Story115ReleaseHarness.ImmutableImageIdPlaceholder, inventory.Arguments);
        Story115ExternalCommand provisioner = plan.Commands.Single(command =>
            command.Name == "provisioner-run");
        Assert.Equal(
            3,
            provisioner.Arguments.Count(argument =>
                argument.Contains("source=" + driverRoot, StringComparison.Ordinal) &&
                argument.EndsWith(",readonly", StringComparison.Ordinal)));
        List<Story115ExternalCommand> commands = plan.Commands.ToList();
        Assert.True(commands.IndexOf(inventory) < commands.IndexOf(provisioner));
    }

    [Fact]
    public async Task OptionAFinalPlan_UsesOneImmutableImageForProvisionerThenApp()
    {
        var operations = new RecordingFinalRunOperations();

        await Story115ReleaseHarness.RunFinalAsync(
            Story115ReleaseHarness.CreateValidEnvironmentForFastTests(),
            operations);

        Story115FinalRunPlan plan = Assert.IsType<Story115FinalRunPlan>(operations.Plan);
        Assert.Equal(
            [
                "candidate-commit-inspect",
                "candidate-tree-inspect",
                "candidate-driver-diff-inspect",
                "candidate-untracked-driver-inspect",
                "candidate-worktree-inspect",
                "provisioner-driver-publish",
                "source-bound-image-build",
                "image-label-inspect",
                "image-app-inventory-inspect",
                "network-create",
                "provider-run",
                "provisioner-run",
                "provisioner-image-inspect",
                "provisioner-remove",
                "provisioner-absence-inspect",
                "app-run",
                "app-image-inspect",
                "curl-health-inspect",
                "identity-playwright-circuit",
            ],
            plan.Commands.Select(command => command.Name));
        Assert.Single(plan.Commands.Where(command => command.Name == "source-bound-image-build"));

        Story115ExternalCommand provisioner = plan.Commands.Single(command =>
            command.Name == "provisioner-run");
        Story115ExternalCommand app = plan.Commands.Single(command => command.Name == "app-run");
        Assert.Contains("{{FINAL_IMAGE_ID}}", provisioner.Arguments);
        Assert.Contains("{{FINAL_IMAGE_ID}}", app.Arguments);
        Assert.Contains($"container:{plan.Identity.ProviderContainer}", provisioner.Arguments);
        Assert.Contains($"container:{plan.Identity.ProviderContainer}", app.Arguments);
        Assert.Equal(9, provisioner.Arguments.Count(argument => argument == "--mount"));
        Assert.Contains(
            provisioner.Arguments,
            argument => argument.Contains(
                "target=/vulgata/sources/mbs_account,readonly",
                StringComparison.Ordinal));
        Assert.Contains(
            provisioner.Arguments,
            argument => argument.Contains(
                "target=/vulgata/sources/mbs_channel,readonly",
                StringComparison.Ordinal));
        Assert.Contains(
            provisioner.Arguments,
            argument => argument.Contains(
                "target=/vulgata/run/config/input.json,readonly",
                StringComparison.Ordinal));
        Assert.Equal(
            3,
            provisioner.Arguments.Count(argument => argument.Contains(
                "target=/app/Vulgata.Story115.Provisioner.",
                StringComparison.Ordinal)));
        Assert.DoesNotContain(
            provisioner.Arguments,
            argument => new[]
            {
                "Vulgata.Core.dll",
                "Vulgata.Infrastructure.dll",
                "Vulgata.Shared.dll",
                "Vulgata.Agents.dll",
                "Vulgata.Web.dll",
            }.Any(name => argument.Contains(name, StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(
            plan.Commands.SelectMany(command => command.Arguments),
            argument => argument is "--add-host" or "host-gateway" or "--internal" ||
                argument.Contains("pip", StringComparison.OrdinalIgnoreCase) ||
                argument.Contains("cgc", StringComparison.OrdinalIgnoreCase));
        List<Story115ExternalCommand> commands = plan.Commands.ToList();
        Assert.True(
            commands.FindIndex(command => command.Name == "provisioner-remove") <
            commands.FindIndex(command => command.Name == "app-run"));
    }

    [Fact]
    public void OptionAImmutableImageBinding_RejectsTagsAndReplacesOnlyTheClosedToken()
    {
        var command = new Story115ExternalCommand(
            "app-run",
            "docker",
            ["run", "{{FINAL_IMAGE_ID}}"]);
        string imageId = "sha256:" + new string('a', 64);

        Story115ExternalCommand bound = Story115ReleaseHarness.BindImmutableImageId(
            command,
            imageId);

        Assert.Equal(["run", imageId], bound.Arguments);
        Assert.Throws<InvalidOperationException>(() =>
            Story115ReleaseHarness.BindImmutableImageId(command, "vulgata-story115-final"));
        Assert.Throws<InvalidOperationException>(() =>
            Story115ReleaseHarness.BindImmutableImageId(
                new Story115ExternalCommand("inspect", "docker", ["inspect"]),
                imageId));
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
    public async Task ProcessExecutor_SourceImageBuildBound_AllowsFortyFiveMinutesButRejectsLonger()
    {
        var executor = new Story115ProcessExecutor();
        var command = new Story115ExternalCommand(
            "dotnet-version-build-bound",
            "dotnet",
            ["--version"]);

        Story115ProcessResult result = await executor.ExecuteAsync(
            command,
            FindRepositoryRoot(),
            new Dictionary<string, string>(StringComparer.Ordinal),
            TimeSpan.FromMinutes(45));

        Assert.Equal(0, result.ExitCode);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            executor.ExecuteAsync(
                command,
                FindRepositoryRoot(),
                new Dictionary<string, string>(StringComparer.Ordinal),
                TimeSpan.FromMinutes(45) + TimeSpan.FromTicks(1)));
    }

    [Fact]
    public async Task ProcessExecutor_ProvisionerBound_AllowsResearchBackedEnvelopeOnlyForProvisioner()
    {
        var executor = new Story115ProcessExecutor();
        var provisioner = new Story115ExternalCommand(
            "provisioner-run",
            "dotnet",
            ["--version"]);
        var unrelated = provisioner with { Name = "unrelated-run" };

        Assert.Equal(
            TimeSpan.FromMinutes(510),
            Story115ReleaseHarness.ProvisionerProcessTimeout);
        Story115ProcessResult result = await executor.ExecuteAsync(
            provisioner,
            FindRepositoryRoot(),
            new Dictionary<string, string>(StringComparer.Ordinal),
            Story115ReleaseHarness.ProvisionerProcessTimeout);

        Assert.Equal(0, result.ExitCode);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            executor.ExecuteAsync(
                provisioner,
                FindRepositoryRoot(),
                new Dictionary<string, string>(StringComparer.Ordinal),
                Story115ReleaseHarness.ProvisionerProcessTimeout + TimeSpan.FromTicks(1)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            executor.ExecuteAsync(
                unrelated,
                FindRepositoryRoot(),
                new Dictionary<string, string>(StringComparer.Ordinal),
                TimeSpan.FromMinutes(45) + TimeSpan.FromTicks(1)));
    }

    [Fact]
    public async Task ProcessExecutor_ExitedParentWithInheritedOutputHandle_StillHonorsTimeoutAsync()
    {
        var executor = new Story115ProcessExecutor();
        var command = new Story115ExternalCommand(
            "inherited-output-handle",
            "python",
            [
                "-c",
                "import subprocess,sys; subprocess.Popen([sys.executable,'-c','import time; time.sleep(2)'])",
            ]);

        await Assert.ThrowsAsync<TimeoutException>(() => executor.ExecuteAsync(
            command,
            FindRepositoryRoot(),
            new Dictionary<string, string>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(200)));
    }

    [Fact]
    public async Task CodeGraphProvider_ExitedCgcWithInheritedOutputHandle_StillHonorsProfileDeadlineAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), $"vulgata-cgc-timeout-{Guid.NewGuid():N}");
        string snapshot = Path.Combine(root, "snapshot");
        string state = Path.Combine(root, "state");
        Directory.CreateDirectory(snapshot);
        try
        {
            using var httpClient = new HttpClient();
            var provider = new CodeGraphContextProvider(
                httpClient,
                new CodeGraphOptions(state)
                {
                    ExecutablePath = OperatingSystem.IsWindows() ? "python" : "python3",
                    ExecutablePrefix =
                    [
                        "-c",
                        "import subprocess,sys; subprocess.Popen([sys.executable,'-c','import time; time.sleep(2)'])",
                    ],
                });
            var stopwatch = Stopwatch.StartNew();

            CodeGraphProviderResult result = await provider.ProfileAsync(
                snapshot,
                TimeSpan.FromMilliseconds(200));

            Assert.False(result.Succeeded);
            Assert.Equal("CodeGraph 执行超时。", result.Error);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1.5));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
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
    public void RepositoryAcquisition_ExactTagRef_UsesCloneCompatibleShortTag()
    {
        Story115ExternalCommand command = Story115ReleaseHarness.CreateRepositoryAcquisitionCommand(
            new Uri("file:///G:/source/repos/mbs/mbs_account"),
            Story115ReleaseHarness.Contract.AccountRepository,
            "C:/temp/story115-account");

        Assert.Equal("git", command.FileName);
        Assert.Equal(
            [
                "clone",
                "--quiet",
                "--branch",
                "vulgata/August15th-sample",
                "--single-branch",
                "file:///G:/source/repos/mbs/mbs_account",
                "C:/temp/story115-account",
            ],
            command.Arguments);
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

    private static string CreateValidProvisionerResultJson(string nonce)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        var unsigned = new Story115ProvisionerResult(
            "vulgata.story115.provisioner-result/1",
            Sha256(nonce),
            "applied",
            "completed",
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            Guid.Parse("22222222-2222-4222-8222-222222222222"),
            Guid.Parse("33333333-3333-4333-8333-333333333333"),
            Guid.Parse("44444444-4444-4444-8444-444444444444"),
            Guid.Parse("55555555-5555-4555-8555-555555555555"),
            Guid.Parse("66666666-6666-4666-8666-666666666666"),
            Guid.Parse("77777777-7777-4777-8777-777777777777"),
            "mbs_account",
            "refs/tags/vulgata/August15th-sample",
            "19c60cd3f3976b7697c487c7bafd96142e3a7625",
            "mbs_channel",
            "refs/tags/vulgata/August15th-sample",
            "47f6b627a7598f862873610413eec1e1e7bf46bf",
            new string('8', 64),
            new string('9', 64),
            2,
            1,
            1,
            "0.5.1",
            "10f09b78b02c886688c726bb93030380cfe54afc23798db24f6f4fc4cc4c7d95",
            new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero),
            string.Empty);
        string unsignedJson = JsonSerializer.Serialize(unsigned, options);
        return JsonSerializer.Serialize(
            unsigned with { ContentHash = Sha256(unsignedJson) },
            options);
    }

    private static string MutateProvisionerResult(
        string json,
        Action<JsonObject> mutate,
        bool resign = true)
    {
        JsonObject result = JsonNode.Parse(json)?.AsObject() ??
            throw new InvalidOperationException("The result test fixture was malformed.");
        mutate(result);
        result["contentHash"] = string.Empty;
        string unsignedJson = result.ToJsonString();
        if (resign)
        {
            result["contentHash"] = Sha256(unsignedJson);
        }
        else
        {
            using JsonDocument original = JsonDocument.Parse(json);
            result["contentHash"] = original.RootElement.GetProperty("contentHash").GetString();
        }

        return result.ToJsonString();
    }

    private static string Sha256(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class RecordingReleaseOperations(
        bool failDrop,
        bool failPreflight = false,
        bool failCreate = false,
        bool databaseExistsAfterAmbiguousCreate = false,
        bool failDatabaseExistsQuery = false,
        string? failAfterEvent = null) : IStory115ReleaseOperations
    {
        internal List<string> Events { get; } = [];

        public Task PrepareInfrastructureAsync(
            Story115RunIdentity identity,
            CancellationToken cancellationToken)
        {
            Events.Add("prepare-infrastructure");
            return FailAfterAsync("prepare-infrastructure");
        }

        public Task PreflightDockerDesktopPostgreSqlAsync(
            Story115RunIdentity identity,
            CancellationToken cancellationToken)
        {
            Events.Add("provider-docker-desktop-postgres-preflight");
            return failPreflight
                ? Task.FromException(new InvalidOperationException("provider-preflight-failure"))
                : FailAfterAsync("provider-docker-desktop-postgres-preflight");
        }

        public Task ProveDatabaseAbsentAsync(
            Story115RunIdentity identity,
            bool afterDrop,
            CancellationToken cancellationToken)
        {
            string eventName = afterDrop
                ? "database-absent-after-drop"
                : "database-absent-before-create";
            Events.Add(eventName);
            return FailAfterAsync(eventName);
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
            return FailAfterAsync("migrate");
        }

        public Task RemoveAppAndProviderAsync(
            Story115RunIdentity identity,
            CancellationToken cancellationToken)
        {
            Events.Add("remove-app-provider");
            return Task.CompletedTask;
        }

        public Task RemoveProvisionerAsync(
            Story115RunIdentity identity,
            CancellationToken cancellationToken)
        {
            Events.Add("remove-provisioner");
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

        private Task FailAfterAsync(string eventName) =>
            StringComparer.Ordinal.Equals(failAfterEvent, eventName)
                ? Task.FromException(
                    new InvalidOperationException($"failure-after-{eventName}"))
                : Task.CompletedTask;
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
