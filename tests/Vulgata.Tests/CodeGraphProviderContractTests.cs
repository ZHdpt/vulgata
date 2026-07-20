using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vulgata.Infrastructure.CodeGraph;

namespace Vulgata.Tests;

public sealed class CodeGraphProviderContractTests
{
    private const string _evidenceSchema = "cgc-provider-contract-evidence-v2";
    private const string _testIdentity =
        "Vulgata.Tests.CodeGraphProviderContractTests.FinalImage_ExactPinned051_ExactProductionProjections_MatchObservedContract";
    private const string _reviewIdentity = "user-approved-story-4-2-plan-2026-07-20";
    private static readonly string[] _planArtifacts =
    [
        "specs/001-codegraph-extraction/plan.md",
        "specs/001-codegraph-extraction/research.md",
        "specs/001-codegraph-extraction/data-model.md",
        "specs/001-codegraph-extraction/quickstart.md",
        "specs/001-codegraph-extraction/contracts/repository-profiling-api.md",
        "specs/002-language-framework-detection/spec.md",
        "specs/002-language-framework-detection/plan.md",
        "specs/002-language-framework-detection/research.md",
        "specs/002-language-framework-detection/data-model.md",
        "specs/002-language-framework-detection/quickstart.md",
        "specs/002-language-framework-detection/contracts/framework-detection.md",
        "specs/002-language-framework-detection/contracts/provider-language-projection.md",
        "specs/002-language-framework-detection/contracts/repository-scan-overview.md",
    ];

    [Fact]
    public void AcceptedCorrectionManifest_IsCanonicalAndArtifactBound()
    {
        string repositoryRoot = FindRepositoryRoot();
        byte[] first = CreateAcceptedCorrectionManifest(repositoryRoot, "sha256:" + new string('a', 64));
        byte[] second = CreateAcceptedCorrectionManifest(repositoryRoot, "sha256:" + new string('a', 64));

        Assert.Equal(first, second);
        using JsonDocument manifest = JsonDocument.Parse(first);
        Assert.Equal(_reviewIdentity, manifest.RootElement.GetProperty("reviewIdentity").GetString());
        Assert.Equal(
            _planArtifacts,
            manifest.RootElement.GetProperty("artifacts")
                .EnumerateArray()
                .Select(item => item.GetProperty("path").GetString()));
        Assert.Equal(64, Hash(first).Length);
    }

    [Fact]
    public void ProviderFixture_EmptyFileAndManyRows_AreExactCompletionInputs()
    {
        string repositoryRoot = FindRepositoryRoot();
        string fixtureRoot = Path.Combine(
            repositoryRoot,
            "tests",
            "Vulgata.Tests",
            "CodeGraphProviderContract",
            "ProviderFixture");
        Assert.Empty(File.ReadAllBytes(Path.Combine(fixtureRoot, "empty_file.cs")));
        Assert.False(
            File.Exists(Path.Combine(fixtureRoot, "empty_file.py")),
            "The exceptional zero-byte Python fixture must not remain in the completion boundary.");

        string manyRowsPath = Path.Combine(fixtureRoot, "many_rows.py");
        Assert.True(File.Exists(manyRowsPath), "The >100-row provider fixture is absent.");
        Assert.True(
            File.ReadLines(manyRowsPath).Count(line => line.StartsWith("def row_", StringComparison.Ordinal)) > 100,
            "The provider fixture does not contain more than 100 structural declarations.");
    }

    [Fact]
    public async Task FinalImage_ExactPinned051_ExactProductionProjections_MatchObservedContract()
    {
        string finalImage = RequireEnvironment("VULGATA_PROVIDER_CONTRACT_IMAGE");
        string acceptedCorrectionSha256 = RequireSha256Environment(
            "VULGATA_PROVIDER_CONTRACT_ACCEPTED_CORRECTION_SHA256");

        string repositoryRoot = FindRepositoryRoot();
        string resultsRoot = Path.Combine(repositoryRoot, "TestResults", "codegraph-provider-contract");
        Directory.CreateDirectory(resultsRoot);
        ProcessResult imageInspection = await RunAsync(
            repositoryRoot,
            "docker",
            ["image", "inspect", finalImage, "--format", "{{.Id}}"]);
        EnsureSuccess(imageInspection);
        string finalImageId = imageInspection.StandardOutput.Trim();
        Assert.Matches("^sha256:[0-9a-f]{64}$", finalImageId);

        byte[] manifestBytes = CreateAcceptedCorrectionManifest(repositoryRoot, finalImageId);
        string correctionRoot = Path.Combine(resultsRoot, acceptedCorrectionSha256);
        string claimPath = Path.Combine(correctionRoot, "claim.json");
        string evidencePath = Path.Combine(correctionRoot, "evidence.json");
        Assert.False(
            Directory.Exists(correctionRoot),
            $"Accepted correction {acceptedCorrectionSha256} already has an execution namespace.");
        Directory.CreateDirectory(correctionRoot);
        byte[] claimBytes = CreateClaim(manifestBytes, acceptedCorrectionSha256);
        WriteCreateNewDurable(claimPath, claimBytes);
        string claimSha256 = Hash(claimBytes);

        string contextRoot = Path.Combine(correctionRoot, "verifier-context");
        Directory.CreateDirectory(contextRoot);
        CopyDirectory(
            Path.Combine(repositoryRoot, "tests", "Vulgata.Tests", "CodeGraphProviderContract", "ProviderFixture"),
            Path.Combine(contextRoot, "fixture"));
        File.Copy(
            Path.Combine(repositoryRoot, "tests", "Vulgata.Tests", "CodeGraphProviderContract", "runner.py"),
            Path.Combine(contextRoot, "runner.py"));
        await File.WriteAllTextAsync(
            Path.Combine(contextRoot, "projection-a.cypher"),
            CodeGraphContextProviderQueries.CodeUnits,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await File.WriteAllTextAsync(
            Path.Combine(contextRoot, "projection-b.cypher"),
            CodeGraphContextProviderQueries.Relationships,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        string suffix = acceptedCorrectionSha256[..12];
        string exactBaseTag = $"vulgata-story42-exact-base:{suffix}";
        string verifierTag = $"vulgata-story42-contract-verifier:{suffix}";
        ProcessResult providerRun = new(-1, string.Empty, "provider-run-not-started");
        try
        {
            EnsureSuccess(await RunAsync(repositoryRoot, "docker", ["tag", finalImageId, exactBaseTag]));
            string dockerfile = """
                ARG BASE_IMAGE
                FROM ${BASE_IMAGE}
                USER root
                COPY runner.py /contract/runner.py
                COPY projection-a.cypher /contract/projection-a.cypher
                COPY projection-b.cypher /contract/projection-b.cypher
                COPY fixture /contract/fixture
                RUN chmod 0555 /contract/runner.py && chown -R app:app /contract
                USER app
                ENTRYPOINT ["python", "/contract/runner.py"]
                """;
            await File.WriteAllTextAsync(
                Path.Combine(contextRoot, "Dockerfile"),
                dockerfile,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            EnsureSuccess(await RunAsync(
                repositoryRoot,
                "docker",
                [
                    "build", "--build-arg", $"BASE_IMAGE={exactBaseTag}",
                    "--label", $"vulgata.provider-contract.base={finalImageId}",
                    "--tag", verifierTag, contextRoot,
                ],
                TimeSpan.FromMinutes(5)));
            string verifierBase = (await RunAsync(
                repositoryRoot,
                "docker",
                [
                    "image", "inspect", verifierTag,
                    "--format", "{{index .Config.Labels \"vulgata.provider-contract.base\"}}",
                ])).StandardOutput.Trim();
            Assert.Equal(finalImageId, verifierBase);

            providerRun = await RunAsync(
                repositoryRoot,
                "docker",
                [
                    "run", "--rm", "--network", "none",
                    "--env", $"FINAL_IMAGE_SHA256={finalImageId}",
                    "--env", $"VERIFIER_BASE_IMAGE_SHA256={verifierBase}",
                    "--env", $"ACCEPTED_CORRECTION_SHA256={acceptedCorrectionSha256}",
                    "--env", $"CLAIM_SHA256={claimSha256}",
                    "--volume", $"{correctionRoot}:/evidence",
                    verifierTag,
                ],
                TimeSpan.FromMinutes(5));

            Assert.True(File.Exists(evidencePath), $"Provider did not publish bounded evidence: {Bound(providerRun.StandardError)}");
            byte[] evidenceBytes = await File.ReadAllBytesAsync(evidencePath);
            Assert.InRange(evidenceBytes.Length, 1, 16 * 1_024);
            using JsonDocument evidence = JsonDocument.Parse(evidenceBytes);
            JsonElement root = evidence.RootElement;
            Assert.Equal(_evidenceSchema, root.GetProperty("schema").GetString());
            Assert.Equal(acceptedCorrectionSha256, root.GetProperty("acceptedCorrectionSha256").GetString());
            Assert.Equal(claimSha256, root.GetProperty("claimSha256").GetString());
            Assert.Equal(finalImageId, root.GetProperty("finalImageSha256").GetString());
            Assert.Equal(finalImageId, root.GetProperty("verifierBaseImageSha256").GetString());
            Assert.Equal("0.5.1", root.GetProperty("providerVersion").GetString());
            Assert.Equal(
                "10f09b78b02c886688c726bb93030380cfe54afc23798db24f6f4fc4cc4c7d95",
                root.GetProperty("providerLockSha256").GetString());
            Assert.Equal(
                "3cd79efe6c9525d69ae250c063404b500ef7595b6c63284d841c78a1ecf48658",
                root.GetProperty("providerDistributionSha256").GetString());
            Assert.Equal(Hash(CodeGraphContextProviderQueries.CodeUnits), root.GetProperty("projectionARequestSha256").GetString());
            Assert.Equal(Hash(CodeGraphContextProviderQueries.Relationships), root.GetProperty("projectionBRequestSha256").GetString());

            string verdict = root.GetProperty("verdict").GetString()!;
            Assert.True(
                providerRun.ExitCode == 0 && verdict == "pass",
                $"Provider contract {verdict}: {root.GetProperty("failureCategory").GetString()}; " +
                $"observations={root.GetProperty("observations").GetRawText()} cleanup={root.GetProperty("cleanup").GetRawText()}");

            JsonElement rowCounts = root.GetProperty("rowCounts");
            Assert.True(rowCounts.GetProperty("projectionA").GetInt32() > 100);
            Assert.True(rowCounts.GetProperty("projectionB").GetInt32() > 100);
            Assert.InRange(rowCounts.GetProperty("combined").GetInt32(), 1, 250_000);

            JsonElement completeness = root.GetProperty("completeness");
            Assert.All(completeness.EnumerateObject(), property => Assert.True(property.Value.GetBoolean()));

            JsonElement parserLanguage = root.GetProperty("coverage").GetProperty("parserLanguage");
            Assert.True(parserLanguage.GetProperty("aliasObserved").GetBoolean());
            Assert.True(parserLanguage.GetProperty("structuralBindingsObserved").GetBoolean());
            Assert.True(parserLanguage.GetProperty("fileNullObserved").GetBoolean());
            Assert.True(parserLanguage.GetProperty("oneIdentityPerPath").GetBoolean());
            Assert.True(parserLanguage.GetProperty("allCodeUnitsAssigned").GetBoolean());
            Assert.True(parserLanguage.GetProperty("emptyFileUnknownLanguage").GetBoolean());
            Assert.Equal(0, parserLanguage.GetProperty("emptyFileRetainedCodeUnitCount").GetInt32());
            Assert.Equal(1, parserLanguage.GetProperty("unknownLanguageCount").GetInt32());

            JsonElement immutableSnapshot = root.GetProperty("immutableSnapshot");
            Assert.Equal(1, immutableSnapshot.GetProperty("indexCount").GetInt32());
            Assert.False(immutableSnapshot.GetProperty("watcherStarted").GetBoolean());
            Assert.False(immutableSnapshot.GetProperty("mutationBetweenProjections").GetBoolean());
            Assert.True(immutableSnapshot.GetProperty("snapshotEqual").GetBoolean());

            JsonElement cleanup = root.GetProperty("cleanup");
            Assert.True(cleanup.GetProperty("gatewayStopped").GetBoolean());
            Assert.True(cleanup.GetProperty("contextDeleted").GetBoolean());
            Assert.True(cleanup.GetProperty("databaseDeleted").GetBoolean());
            Assert.True(cleanup.GetProperty("providerHomeDeleted").GetBoolean());
            Assert.All(root.GetProperty("forbiddenRetention").EnumerateObject(), property => Assert.False(property.Value.GetBoolean()));
        }
        finally
        {
            await RunAsync(repositoryRoot, "docker", ["image", "rm", "--force", verifierTag, exactBaseTag]);
            if (Directory.Exists(contextRoot))
            {
                Directory.Delete(contextRoot, recursive: true);
            }
        }
    }

    private static byte[] CreateAcceptedCorrectionManifest(string repositoryRoot, string finalImageId)
    {
        var artifacts = _planArtifacts.Select(path => new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["path"] = path,
            ["sha256"] = Hash(File.ReadAllBytes(Path.Combine(repositoryRoot, path.Replace('/', Path.DirectorySeparatorChar)))),
        }).ToArray();
        var manifest = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["artifacts"] = artifacts,
            ["evidenceSchema"] = _evidenceSchema,
            ["finalImageSha256"] = finalImageId,
            ["gate"] = "provider-contract-e2e",
            ["providerDistributionSha256"] = "3cd79efe6c9525d69ae250c063404b500ef7595b6c63284d841c78a1ecf48658",
            ["providerLockSha256"] = "10f09b78b02c886688c726bb93030380cfe54afc23798db24f6f4fc4cc4c7d95",
            ["providerVersion"] = "0.5.1",
            ["projectionASha256"] = Hash(CodeGraphContextProviderQueries.CodeUnits),
            ["projectionBSha256"] = Hash(CodeGraphContextProviderQueries.Relationships),
            ["reviewIdentity"] = _reviewIdentity,
            ["story"] = "4-2-language-and-framework-detection",
            ["testIdentity"] = _testIdentity,
        };
        return JsonSerializer.SerializeToUtf8Bytes(manifest);
    }

    private static byte[] CreateClaim(byte[] manifestBytes, string acceptedCorrectionSha256)
    {
        using JsonDocument manifest = JsonDocument.Parse(manifestBytes);
        var claim = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["acceptedCorrectionManifest"] = manifest.RootElement.Clone(),
            ["acceptedCorrectionSha256"] = acceptedCorrectionSha256,
            ["evidenceSchema"] = _evidenceSchema,
            ["executionOrdinal"] = 1,
            ["schema"] = "cgc-provider-contract-claim-v1",
        };
        return JsonSerializer.SerializeToUtf8Bytes(claim);
    }

    private static void WriteCreateNewDurable(string path, byte[] bytes)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));

    private static string Hash(byte[] value) =>
        Convert.ToHexStringLower(SHA256.HashData(value));

    private static string RequireEnvironment(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        Assert.False(string.IsNullOrWhiteSpace(value), $"Required completion-lane input {name} is absent.");
        return value!;
    }

    private static string RequireSha256Environment(string name)
    {
        string value = RequireEnvironment(name);
        Assert.Matches("^[0-9a-f]{64}$", value);
        return value;
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Vulgata.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (string directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private static async Task<ProcessResult> RunAsync(
        string workingDirectory,
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan? timeout = null)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        using var cancellation = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }

        return new ProcessResult(process.ExitCode, await standardOutput, await standardError);
    }

    private static void EnsureSuccess(ProcessResult result)
    {
        Assert.True(result.ExitCode == 0, $"Process exited {result.ExitCode}: {Bound(result.StandardError)}");
    }

    private static string Bound(string value)
    {
        string normalized = string.Join(' ', value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 1_024 ? normalized : normalized[..1_024];
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
