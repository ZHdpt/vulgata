using System.Diagnostics;
using System.Security.Cryptography;

namespace Vulgata.Tests.CodeGraphQualification;

public sealed class CgcProviderQualificationTests
{
    [Fact]
    [Trait("Category", "CodeGraphQualificationProvider")]
    public async Task Attempt0003_WhenAuthorizedAndUnclaimed_RecordsSchema12EvidenceAsync()
    {
        string repositoryRoot = QualificationTestPaths.RepositoryRoot;
        string attemptsRoot = QualificationTestPaths.FromRepositoryRoot(
            "specs", "001-codegraph-extraction", "qualification", "attempts");
        Dictionary<string, string?> environment = new(StringComparer.Ordinal)
        {
            [ProviderQualificationEntry.AttemptIdEnvironmentVariable] =
                Environment.GetEnvironmentVariable(ProviderQualificationEntry.AttemptIdEnvironmentVariable),
            [ProviderQualificationEntry.TokenEnvironmentVariable] =
                Environment.GetEnvironmentVariable(ProviderQualificationEntry.TokenEnvironmentVariable),
            [ProviderQualificationEntry.ExactCommandEnvironmentVariable] =
                Environment.GetEnvironmentVariable(ProviderQualificationEntry.ExactCommandEnvironmentVariable),
            [ProviderQualificationEntry.BoundaryPathEnvironmentVariable] =
                Environment.GetEnvironmentVariable(ProviderQualificationEntry.BoundaryPathEnvironmentVariable),
            [ProviderQualificationEntry.BoundarySha256EnvironmentVariable] =
                Environment.GetEnvironmentVariable(ProviderQualificationEntry.BoundarySha256EnvironmentVariable),
            [ProviderQualificationEntry.RepositoryRootEnvironmentVariable] =
                Environment.GetEnvironmentVariable(ProviderQualificationEntry.RepositoryRootEnvironmentVariable),
        };

        await ProviderQualificationEntry.ExecuteAsync(
            attemptsRoot,
            environment,
            () => DateTimeOffset.UtcNow,
            async (lease, cancellationToken) =>
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                var launcher = new DockerQualificationProcessLauncher(lease);
                var runner = new CgcQualificationRunner(repositoryRoot, TimeSpan.FromSeconds(1_800), launcher);
                CgcQualificationExecution execution = await runner.RunAsync(cancellationToken);
                stopwatch.Stop();

                string resultPath = QualificationTestPaths.FromRepositoryRoot(
                    "specs", "001-codegraph-extraction", "qualification", "qualification-result.json");
                string manifestPath = QualificationTestPaths.FromRepositoryRoot(
                    "tests", "Vulgata.Tests", "CodeGraphQualification", "Fixtures", "qualification-fixture-manifest.json");
                string requirementsPath = QualificationTestPaths.FromRepositoryRoot(
                    "docker", "codegraph", "requirements.lock");
                string pythonVersionPath = QualificationTestPaths.FromRepositoryRoot(
                    "docker", "codegraph", "python-version.txt");
                string manifestHash = await HashFileAsync(manifestPath, cancellationToken);
                string requirementsHash = await HashFileAsync(requirementsPath, cancellationToken);
                string pythonVersion = (await File.ReadAllTextAsync(pythonVersionPath, cancellationToken)).Trim();
                int elapsedMilliseconds = (int)Math.Min(stopwatch.ElapsedMilliseconds, 1_800_000L);
                CgcStreamingEvidence structuralEvidence = execution.StructuralEvidence ?? CreateEmptyEvidence();

                if (execution.Succeeded)
                {
                    await QualificationResultWriter.WriteQualifiedAsync(
                        resultPath,
                        new QualifiedResultInput(
                            "vulgata-codegraph-qualification:0.5.1",
                            execution.ImageDigest ?? throw new InvalidOperationException("The qualified image digest was not observed."),
                            pythonVersion,
                            requirementsHash,
                            manifestHash,
                            "app",
                            execution.CommandEvidence,
                            ["evidence/verification.md"],
                            elapsedMilliseconds,
                            execution.CleanupSucceeded,
                            structuralEvidence),
                        cancellationToken);
                    return new ProviderQualificationTerminal("passed", "qualified");
                }

                string category = ClassifyCorrectiveCategory(execution.FailureSummary);
                await QualificationResultWriter.WriteCorrectiveAsync(
                    resultPath,
                    new UnqualifiedResultInput(
                        "vulgata-codegraph-qualification:0.5.1",
                        execution.ImageDigest,
                        pythonVersion,
                        requirementsHash,
                        manifestHash,
                        execution.RanAsNonRoot ? "app" : null,
                        execution.RanAsNonRoot,
                        execution.CommandEvidence,
                        ["evidence/verification.md"],
                        elapsedMilliseconds,
                        category,
                        structuralEvidence),
                    cancellationToken);
                return new ProviderQualificationTerminal("failed", category);
            });
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken) =>
        Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(path, cancellationToken)));

    private static CgcStreamingEvidence CreateEmptyEvidence()
    {
        using CgcStreamingEvidenceObserver observer = new();
        return observer.Complete();
    }

    private static string ClassifyCorrectiveCategory(string? failureSummary)
    {
        if (failureSummary?.Contains("timed out", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "timeout";
        }

        if (failureSummary?.Contains("image build", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "image-build-failure";
        }

        return failureSummary?.Contains("streaming", StringComparison.OrdinalIgnoreCase) == true
            ? "unsafe-evidence"
            : "provider-contract-failure";
    }
}
