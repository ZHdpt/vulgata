using Vulgata.Tests.TestInfrastructure;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Vulgata.Tests.ReleaseGate;

public sealed class Story125ReleaseHarnessTests
{
    private const string ProvisionalBaseCommit =
        "0d835c5f65f70a3c6a9561c6ff600069bb93e832";
    private const string AcceptedStory124CompletionCommit =
        "4798b97d9f23aef587829175df7ab7ebc3daf995";
    private const string Story125TerminationCommit =
        "8d56f4601de7e04b51e12a82be05600ce90af496";
    private const string AuthorizedStory124MetadataCorrectionPath =
        "specs/013-exact-cross-repository-flow-pass2/workflow-state.json";

    [Trait("Suite", "HistoricalBoundary")]
    [SuiteFact("HistoricalBoundary")]
    public void Story125Boundary_NoMigrationDbContextStory124OrUncoordinatedSharedDelta_RemainsIntact()
    {
        string repositoryRoot = FindRepositoryRoot();
        IReadOnlyList<string> changedPaths = GetChangedPaths(repositoryRoot);
        IReadOnlyList<string> historicalBoundaryChanges = GetCommittedChangedPaths(
            repositoryRoot,
            AcceptedStory124CompletionCommit,
            Story125TerminationCommit);

        string[] forbiddenPaths =
        [
            "src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs",
            "src/dotnet/Vulgata.Infrastructure/Data/Migrations/VulgataDbContextModelSnapshot.cs",
        ];

        Assert.DoesNotContain(
            historicalBoundaryChanges,
            path => forbiddenPaths.Contains(path, StringComparer.Ordinal));
        Assert.DoesNotContain(
            historicalBoundaryChanges,
            path => path.StartsWith(
                "src/dotnet/Vulgata.Infrastructure/Data/Migrations/",
                StringComparison.Ordinal));

        HashSet<string> acceptedCompletionPaths = GetCommittedChangedPaths(
                repositoryRoot,
                ProvisionalBaseCommit,
                AcceptedStory124CompletionCommit)
            .ToHashSet(StringComparer.Ordinal);

        string mergeBase = RunGit(
            repositoryRoot,
            "merge-base",
            AcceptedStory124CompletionCommit,
            "HEAD").Trim();
        Assert.Equal(
            AcceptedStory124CompletionCommit,
            mergeBase);

        HashSet<string> acceptedStory124Paths = acceptedCompletionPaths
            .Where(IsStory124Path)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> candidateStory124Paths = changedPaths
            .Where(IsStory124Path)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> story124PathDifferences = acceptedStory124Paths.ToHashSet(
            StringComparer.Ordinal);
        story124PathDifferences.SymmetricExceptWith(candidateStory124Paths);
        Assert.True(
            acceptedStory124Paths.SetEquals(candidateStory124Paths),
            $"Story 12.4 paths must be exactly those in accepted commit {AcceptedStory124CompletionCommit}. " +
            $"Unexpected or missing paths: {string.Join(", ", story124PathDifferences)}");

        IReadOnlyList<string> postIntegrationChanges = GetChangedPathsSinceCommit(
            repositoryRoot,
            AcceptedStory124CompletionCommit);
        Assert.DoesNotContain(
            postIntegrationChanges,
            path => IsStory124Path(path) &&
                !StringComparer.Ordinal.Equals(
                    path,
                    AuthorizedStory124MetadataCorrectionPath));

        string ownershipManifest = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "specs",
            "014-grounded-llm-wiki-chat-release-evidence",
            "evidence",
            "ownership",
            "README.md"));
        Assert.Contains(
            "src/dotnet/Vulgata.Web/Program.cs",
            ownershipManifest,
            StringComparison.Ordinal);
        Assert.Contains(
            "one root-coordinated owner",
            ownershipManifest,
            StringComparison.Ordinal);
        Assert.Contains(
            "Track A may",
            ownershipManifest,
            StringComparison.Ordinal);
        Assert.Contains(
            "accepted T019 Scan status registrations",
            ownershipManifest,
            StringComparison.Ordinal);
        Assert.Contains(
            "Agents DI remains outside Track A",
            ownershipManifest,
            StringComparison.Ordinal);

        string[] uncoordinatedSharedIntegrationPaths =
        [
            "src/dotnet/Vulgata.Web/Api/ApiEndpointRegistrationExtensions.cs",
            "src/dotnet/Vulgata.Agents/DependencyInjection.cs",
            "Directory.Packages.props",
            "Directory.Build.props",
        ];
        Assert.DoesNotContain(
            historicalBoundaryChanges,
            path => path is not null &&
                IsUncoordinatedSharedIntegrationPath(
                    path,
                    uncoordinatedSharedIntegrationPaths) &&
                !acceptedCompletionPaths.Contains(path) &&
                !ownershipManifest.Contains(path, StringComparison.Ordinal));
        Assert.DoesNotContain(
            historicalBoundaryChanges,
            path => path is not null &&
                IsUncoordinatedSharedIntegrationPath(
                    path,
                    uncoordinatedSharedIntegrationPaths) &&
                !ownershipManifest.Contains(path, StringComparison.Ordinal));
    }

    [Trait("Suite", "HistoricalBoundary")]
    [SuiteFact("HistoricalBoundary")]
    public void Story125DatabaseAndScreenshotPreflight_RejectsProtectedOrFakeBackupBState()
    {
        Assert.True(Story125ReleaseHarness.IsOwnedDatabaseName(
            "vulgata_story125_0123456789abcdef0123456789abcdef"));
        Assert.False(Story125ReleaseHarness.IsOwnedDatabaseName("vulgata"));
        Assert.False(Story125ReleaseHarness.IsOwnedDatabaseName(
            "vulgata_story124_0123456789abcdef0123456789abcdef"));
        Assert.False(Story125ReleaseHarness.IsOwnedDatabaseName("vulgata_demo_backup_b"));

        string schemaPath = Path.Combine(
            FindRepositoryRoot(),
            "specs",
            "014-grounded-llm-wiki-chat-release-evidence",
            "contracts",
            "story125-release-evidence.schema.json");
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(schemaPath));
        JsonElement screenshot = schema.RootElement
            .GetProperty("$defs")
            .GetProperty("screenshot");
        Assert.True(
            screenshot.GetProperty("properties")
                .GetProperty("productionPersistedState")
                .GetProperty("const")
                .GetBoolean());
    }

    public static IEnumerable<object[]> SemanticEnvelopeRejectionVectors()
    {
        yield return ["planned-result-claims-passed", MutatePlanned(envelope =>
        {
            envelope["result"] = new JsonObject { ["kind"] = "passed" };
        })];
        yield return ["planned-has-release-receipt", MutatePlanned(envelope =>
        {
            envelope["releaseReceipt"] = new JsonObject();
        })];
        yield return ["self-supersession", MutatePlanned(envelope =>
        {
            envelope["supersedesRunId"] = envelope["runId"]!.GetValue<string>();
        })];
        yield return ["missing-supersession-target", MutatePlanned(envelope =>
        {
            envelope["supersedesRunId"] = "story125-20260827t120001z-12345678";
        })];
        yield return ["later-supersession-target", MutatePlanned(envelope =>
        {
            envelope["supersedesRunId"] = "story125-20260827t120001z-abcdef12";
        })];
        yield return ["non-passed-success-claim", MutatePlanned(envelope =>
        {
            envelope["status"] = "failed";
            envelope["result"] = new JsonObject { ["kind"] = "passed" };
        })];
        yield return ["passed-without-receipt", MutatePlanned(envelope =>
        {
            envelope["status"] = "passed";
            envelope["result"] = new JsonObject { ["kind"] = "passed" };
        })];
        yield return ["duplicate-mandatory-check", MutatePlanned(envelope =>
        {
            envelope["status"] = "passed";
            envelope["checks"] = new JsonArray(
                new JsonObject
                {
                    ["id"] = "tier1-contract-ranking-relationship",
                    ["tier"] = 1,
                    ["status"] = "passed",
                    ["commandIds"] = new JsonArray(),
                    ["artifacts"] = new JsonArray(),
                    ["attemptedObservationIds"] = new JsonArray(),
                    ["reason"] = null,
                },
                new JsonObject
                {
                    ["id"] = "tier1-contract-ranking-relationship",
                    ["tier"] = 1,
                    ["status"] = "passed",
                    ["commandIds"] = new JsonArray(),
                    ["artifacts"] = new JsonArray(),
                    ["attemptedObservationIds"] = new JsonArray(),
                    ["reason"] = null,
                });
        })];
        yield return ["omitted-mandatory-check", MutatePlanned(envelope =>
        {
            envelope["status"] = "passed";
            envelope["checks"] = new JsonArray();
        })];
        yield return ["mis-tiered-mandatory-check", MutatePlanned(envelope =>
        {
            envelope["status"] = "passed";
            ((JsonObject)envelope["checks"]![0]!)!["tier"] = 3;
        })];
        yield return ["reordered-mandatory-checks", MutatePlanned(envelope =>
        {
            envelope["status"] = "passed";
            envelope["checks"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "tier3-screenshot-release-integrity",
                    ["tier"] = 3,
                    ["status"] = "passed",
                    ["commandIds"] = new JsonArray(),
                    ["artifacts"] = new JsonArray(),
                    ["attemptedObservationIds"] = new JsonArray(),
                    ["reason"] = null,
                },
                new JsonObject
                {
                    ["id"] = "tier1-contract-ranking-relationship",
                    ["tier"] = 1,
                    ["status"] = "passed",
                    ["commandIds"] = new JsonArray(),
                    ["artifacts"] = new JsonArray(),
                    ["attemptedObservationIds"] = new JsonArray(),
                    ["reason"] = null,
                },
            };
        })];
        yield return ["incomplete-screenshot-categories", MutatePlanned(envelope =>
        {
            envelope["status"] = "passed";
            envelope["releaseReceipt"] = new JsonObject
            {
                ["screenshots"] = new JsonArray
                {
                    new JsonObject { ["viewKind"] = "business-answer" },
                },
            };
        })];
        yield return ["half-up-score-drift", MutatePlanned(envelope =>
        {
            envelope["status"] = "passed";
            envelope["releaseReceipt"] = new JsonObject
            {
                ["chat"] = new JsonObject
                {
                    ["caseCount"] = 12,
                    ["usefulCount"] = 10,
                    ["usefulPercent"] = 83.34,
                },
            };
        })];
        yield return ["content-bearing-stream-observation", MutatePlanned(envelope =>
        {
            envelope["attemptedObservations"] = new JsonArray
            {
                new JsonObject
                {
                    ["observationId"] = "obs-stream-01",
                    ["content"] = "provider answer must not be exported",
                },
            };
        })];
        yield return ["altered-candidate-stream-provenance", MutatePlanned(envelope =>
        {
            envelope["attemptedObservations"] = new JsonArray
            {
                new JsonObject
                {
                    ["observationId"] = "obs-stream-01",
                    ["candidateCommit"] = new string('f', 40),
                    ["signature"] = new string('s', 96),
                },
            };
        })];
        yield return ["foreign-process-stream-provenance", MutatePlanned(envelope =>
        {
            envelope["attemptedObservations"] = new JsonArray
            {
                new JsonObject
                {
                    ["observationId"] = "obs-stream-01",
                    ["processObservationId"] = "process-foreign-01",
                },
            };
        })];
        yield return ["unsigned-stream-provenance", MutatePlanned(envelope =>
        {
            envelope["attemptedObservations"] = new JsonArray
            {
                new JsonObject
                {
                    ["observationId"] = "obs-stream-01",
                    ["observationSignature"] = null,
                },
            };
        })];
        yield return ["aggregate-fake-chunk-provenance", MutatePlanned(envelope =>
        {
            envelope["attemptedObservations"] = new JsonArray
            {
                new JsonObject
                {
                    ["observationId"] = "obs-stream-01",
                    ["source"] = "sdk-aggregate-fake-chunk",
                    ["streamingTransport"] = true,
                    ["answerDeltaCount"] = 1,
                },
            };
        })];
        yield return ["omitted-stream-attempt", MutatePlanned(envelope =>
        {
            envelope["attemptedObservations"] = new JsonArray
            {
                new JsonObject
                {
                    ["caseId"] = "q01",
                    ["providerDisposition"] = "invoked",
                    ["streamAttempts"] = new JsonArray(),
                },
            };
        })];
        yield return ["duplicate-stream-attempt", MutatePlanned(envelope =>
        {
            envelope["attemptedObservations"] = new JsonArray
            {
                new JsonObject
                {
                    ["caseId"] = "q01",
                    ["providerDisposition"] = "invoked",
                    ["streamAttempts"] = new JsonArray
                    {
                        new JsonObject { ["attemptKind"] = "primary" },
                        new JsonObject { ["attemptKind"] = "primary" },
                    },
                },
            };
        })];
        yield return ["reordered-repair-attempt", MutatePlanned(envelope =>
        {
            envelope["attemptedObservations"] = new JsonArray
            {
                new JsonObject
                {
                    ["caseId"] = "q01",
                    ["providerDisposition"] = "invoked",
                    ["streamAttempts"] = new JsonArray
                    {
                        new JsonObject { ["attemptKind"] = "syntax-repair" },
                        new JsonObject { ["attemptKind"] = "primary" },
                    },
                },
            };
        })];
        yield return ["terminal-outcome-mismatch", MutatePlanned(envelope =>
        {
            envelope["attemptedObservations"] = new JsonArray
            {
                new JsonObject
                {
                    ["transportOutcome"] = "completed",
                    ["terminalEventCount"] = 0,
                    ["rejectionCategory"] = "missing-terminal",
                },
            };
        })];
        yield return ["repair-outcome-mismatch", MutatePlanned(envelope =>
        {
            envelope["attemptedObservations"] = new JsonArray
            {
                new JsonObject
                {
                    ["attemptKind"] = "syntax-repair",
                    ["validationOutcome"] = "accepted",
                    ["transportOutcome"] = "rejected-syntax",
                },
            };
        })];
        yield return ["deadline-mismatch", MutatePlanned(envelope =>
        {
            envelope["attemptedObservations"] = new JsonArray
            {
                new JsonObject
                {
                    ["overallDeadlineMilliseconds"] = 30000,
                    ["inactivityLimitMilliseconds"] = 10000,
                },
            };
        })];
        yield return ["transport-vocabulary-mismatch", MutatePlanned(envelope =>
        {
            envelope["attemptedObservations"] = new JsonArray
            {
                new JsonObject
                {
                    ["transportOutcome"] = "utf8-error",
                    ["rejectionCategory"] = "utf8-error",
                    ["validationOutcome"] = "accepted",
                },
            };
        })];
        yield return ["unauthorized-content-leakage", MutatePlanned(envelope =>
        {
            envelope["attemptedObservations"] = new JsonArray
            {
                new JsonObject
                {
                    ["unauthorizedDisclosure"] = true,
                    ["content"] = "secret connection string",
                },
            };
        })];
    }

    [Trait("Suite", "HistoricalBoundary")]
    [SuiteTheory("HistoricalBoundary")]
    [MemberData(nameof(SemanticEnvelopeRejectionVectors))]
    public void Story125SemanticValidation_RedVector_IsRejectedBeforeT016(
        string vector,
        JsonObject envelope)
    {
        IReadOnlyList<string> errors = Story125SemanticHarnessProbe.Validate(envelope);

        Assert.NotEmpty(errors);
        Assert.True(
            errors.Any(error => error.Length > 0),
            $"T015 RED vector {vector} must have a diagnostic.");
    }

    private static JsonObject MutatePlanned(Action<JsonObject> mutation)
    {
        JsonObject envelope = new()
        {
            ["schemaVersion"] = "story125-release-evidence/2.1",
            ["runId"] = "story125-20260827t120000z-abcdef12",
            ["supersedesRunId"] = null,
            ["status"] = "planned",
            ["candidate"] = null,
            ["environment"] = null,
            ["commands"] = new JsonArray(),
            ["checks"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "tier1-contract-ranking-relationship",
                    ["tier"] = 1,
                    ["status"] = "planned",
                    ["commandIds"] = new JsonArray(),
                    ["artifacts"] = new JsonArray(),
                    ["attemptedObservationIds"] = new JsonArray(),
                    ["reason"] = null,
                },
            },
            ["attemptedObservations"] = new JsonArray(),
            ["result"] = new JsonObject { ["kind"] = "planned" },
            ["releaseReceipt"] = null,
        };
        mutation(envelope);
        return envelope;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException(
                "The Vulgata repository root could not be located.");
    }

    private static IReadOnlyList<string> GetChangedPaths(string repositoryRoot)
    {
        return GetChangedPathsSinceCommit(repositoryRoot, ProvisionalBaseCommit);
    }

    private static IReadOnlyList<string> GetChangedPathsSinceCommit(
        string repositoryRoot,
        string commit)
    {
        string committedAndWorkingTree = RunGit(
            repositoryRoot,
            "diff",
            "--name-only",
            "--no-renames",
            commit,
            "--");
        string untracked = RunGit(
            repositoryRoot,
            "ls-files",
            "--others",
            "--exclude-standard");

        return committedAndWorkingTree
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Concat(untracked.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
            .Select(path => path.Replace('\\', '/'))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> GetCommittedChangedPaths(
        string repositoryRoot,
        string fromCommit,
        string toCommit)
    {
        return RunGit(
                repositoryRoot,
                "diff",
                "--name-only",
                "--no-renames",
                $"{fromCommit}..{toCommit}",
                "--")
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(path => path.Replace('\\', '/'))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsStory124Path(string path) =>
        path.StartsWith(
            "specs/013-exact-cross-repository-flow-pass2/",
            StringComparison.Ordinal);

    private static bool IsUncoordinatedSharedIntegrationPath(
        string path,
        IReadOnlyCollection<string> exactPaths) =>
        exactPaths.Contains(path, StringComparer.Ordinal) ||
        path.EndsWith(".csproj", StringComparison.Ordinal);

    private static string RunGit(string repositoryRoot, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Failed to start git.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(
            process.ExitCode == 0,
            $"git {string.Join(' ', arguments)} failed: {error}");
        return output;
    }
}

/// <summary>
/// Test-only probe for the T016 public harness seam.  Reflection keeps this
/// slice compiling while the seam is intentionally absent in the T015 RED.
/// T016 must expose the internal static ValidateSemanticEnvelope(string)
/// operation on Story125ReleaseHarness.
/// </summary>
internal static class Story125SemanticHarnessProbe
{
    internal static IReadOnlyList<string> Validate(JsonObject envelope)
    {
        MethodInfo? validator = typeof(Story125ReleaseHarness).GetMethod(
            "ValidateSemanticEnvelope",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(string)],
            modifiers: null);
        Assert.NotNull(
            validator);

        object? rawResult = validator!.Invoke(null, [envelope.ToJsonString()]);
        return rawResult switch
        {
            IReadOnlyList<string> errors => errors,
            IEnumerable<string> errors => errors.ToArray(),
            bool valid => valid ? [] : ["semantic validation rejected the envelope"],
            _ => throw new InvalidOperationException(
                "ValidateSemanticEnvelope must return errors or a validity boolean."),
        };
    }

    internal static IReadOnlyList<string> Validate(
        JsonObject envelope,
        IReadOnlyDictionary<string, byte[]> artifactContents)
    {
        MethodInfo? validator = typeof(Story125ReleaseHarness).GetMethod(
            "ValidateSemanticEnvelope",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(string), typeof(IReadOnlyDictionary<string, byte[]>)],
            modifiers: null);
        Assert.NotNull(validator);

        object? rawResult = validator!.Invoke(null, [envelope.ToJsonString(), artifactContents]);
        return rawResult switch
        {
            IReadOnlyList<string> errors => errors,
            IEnumerable<string> errors => errors.ToArray(),
            bool valid => valid ? [] : ["semantic validation rejected the envelope"],
            _ => throw new InvalidOperationException(
                "ValidateSemanticEnvelope must return errors or a validity boolean."),
        };
    }
}
