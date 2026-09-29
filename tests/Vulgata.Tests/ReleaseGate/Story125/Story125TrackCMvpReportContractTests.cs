using Vulgata.Tests.TestInfrastructure;
using System.Runtime.CompilerServices;

namespace Vulgata.Tests.ReleaseGate;

public sealed class Story125TrackCMvpReportContractTests
{
    private const string ContractPath =
        "specs/014-grounded-llm-wiki-chat-release-evidence/contracts/release-evidence-contract.md";

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void CurrentMarkdownContract_IsTheTrackCMvpAuthority_WithFourTruthfulDispositions()
    {
        string contract = LoadContract();

        Assert.Contains("This Markdown contract is the current August 30 Track C-MVP release-evidence authority.", contract, StringComparison.Ordinal);
        Assert.Contains("supersedes the Track C acceptance role", contract, StringComparison.Ordinal);

        foreach (string disposition in new[] { "passed", "failed", "blocked", "not-run" })
        {
            Assert.Contains($"- {disposition} ", contract, StringComparison.Ordinal);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void CurrentMarkdownContract_ContainsRequiredPreflightRunQueryEvaluationRestartScreenshotAndFailureSections()
    {
        string contract = LoadContract();
        string[] requiredSections =
        [
            "## Lightweight preflight",
            "## Command and exit table",
            "## Combined host integration and real release run",
            "## Straightforward database query table",
            "## Manual twelve-question table",
            "## Application-process restart",
            "## Manual screenshots and redaction",
            "## Failures and limitations",
            "## Passing checklist",
        ];

        Assert.All(requiredSections, section => Assert.Contains(section, contract, StringComparison.Ordinal));
        Assert.Contains("sanitized blocked condition", contract, StringComparison.Ordinal);
        Assert.Contains("vulgata_story125_*", contract, StringComparison.Ordinal);
        Assert.Contains("exact Profiled Revisions", contract, StringComparison.Ordinal);
        Assert.Contains("at least one repository-relative manually captured real screenshot in each category", contract, StringComparison.Ordinal);
        Assert.Contains("manual redaction result", contract, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void CurrentMarkdownContract_DefersHistoricalProofMachinery_AndRetainsNoRequiredMechanismForIt()
    {
        string contract = LoadContract();

        Assert.Contains("historical JSON schema remains", contract, StringComparison.Ordinal);
        Assert.Contains("Explicit post-competition backlog", contract, StringComparison.Ordinal);

        string[] deferredMechanisms =
        [
            "ECDSA/key attestation",
            "sealed raw-adapter admission",
            "transport-counter recomputation",
            "rigid ten-check/schema-2.2 release envelope",
            "supersession/closure graphs",
            "exhaustive command/artifact/screenshot hashing",
            "separate controlled-provider orchestration",
            "live outage injection",
            "automated screenshot binding",
            "release-summary product feature",
        ];

        foreach (string mechanism in deferredMechanisms)
        {
            Assert.Contains(mechanism, contract, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("ECDSA/key attestation MUST", contract, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rigid ten-check/schema-2.2 release envelope MUST", contract, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("automated screenshot binding MUST", contract, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("release-summary product feature MUST", contract, StringComparison.OrdinalIgnoreCase);
    }

    private static string LoadContract()
    {
        DirectoryInfo? directory = FindRepositoryRoot(Directory.GetCurrentDirectory())
            ?? FindRepositoryRoot(AppContext.BaseDirectory)
            ?? FindRepositoryRootFromSource();

        string root = directory?.FullName ?? throw new InvalidOperationException("The Vulgata repository root could not be located.");
        string path = Path.Combine(root, ContractPath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"The current Markdown release contract is missing: {path}");
        return File.ReadAllText(path);
    }

    private static DirectoryInfo? FindRepositoryRoot(string startPath)
    {
        DirectoryInfo? directory = new(startPath);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
        {
            directory = directory.Parent;
        }

        return directory;
    }

    private static DirectoryInfo? FindRepositoryRootFromSource(
        [CallerFilePath] string sourcePath = "") =>
        FindRepositoryRoot(Path.GetDirectoryName(sourcePath) ?? string.Empty);
}
