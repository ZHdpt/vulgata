using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// T047 qualification: inspect the actual Story 12.3 production composition
/// and its closed Worker boundary. This is deliberately source/assembly
/// inspection rather than a structural placeholder assertion.
/// </summary>
public sealed class Pass1ProductionBoundaryQualificationTests
{
    private const string PinnedBoundaryCommit = "cb3fb0f17b2eaf92d93afe71c710cd7b92c1e3bd";
    private const string ArtifactRelativePath =
        "artifacts/qualification/story-12.3/production-boundary-inspection.json";

    private static readonly string[] InspectedRelativePaths =
    [
        "src/dotnet/Vulgata.Agents/WorkerExecution/PreparedPass1WorkerDispatcher.cs",
        "src/dotnet/Vulgata.Agents/WorkerExecution/PreparedPass1ResultParser.cs",
        "src/dotnet/Vulgata.Agents/WorkerExecution/PreparedNavigateCodeFunction.cs",
        "src/dotnet/Vulgata.Agents/Prompts/pass1-code-logic-worker.v2.md",
        "src/dotnet/Vulgata.Core/DomainServices/IPass1NavigationService.cs",
        "src/dotnet/Vulgata.Core/DomainServices/IPreparedPass1WorkerDispatcher.cs",
        "src/dotnet/Vulgata.Infrastructure/Analysis/PreparedNavigation/PreparedPass1NavigationService.cs",
        "src/dotnet/Vulgata.Infrastructure/Data/PreparedPass1Store.cs",
        "src/dotnet/Vulgata.Infrastructure/Data/Pass1CodeLogicDocumentCommitter.cs",
        "src/dotnet/Vulgata.Web/Data/PreparedPass1Coordinator.cs",
        "src/dotnet/Vulgata.Web/Data/PreparedPass1DispatchWorker.cs",
        "src/dotnet/Vulgata.Web/Api/Scans/Pass1ExecutionEndpoints.cs",
        "src/dotnet/Vulgata.Shared/Scans/Pass1ExecutionContracts.cs",
        "src/dotnet/Vulgata.Shared/Scans/Pass1ExecutionValidators.cs",
        "src/dotnet/Vulgata.Web/Api/ApiEndpointRegistrationExtensions.cs",
        "src/dotnet/Vulgata.Web/Program.cs",
    ];

    private static readonly string[] WorkerSurfaceRelativePaths =
    [
        "src/dotnet/Vulgata.Agents/WorkerExecution/PreparedPass1WorkerDispatcher.cs",
        "src/dotnet/Vulgata.Agents/WorkerExecution/PreparedPass1ResultParser.cs",
        "src/dotnet/Vulgata.Agents/WorkerExecution/PreparedNavigateCodeFunction.cs",
        "src/dotnet/Vulgata.Web/Data/PreparedPass1DispatchWorker.cs",
        "src/dotnet/Vulgata.Web/Api/Scans/Pass1ExecutionEndpoints.cs",
    ];

    private static readonly string[] ForbiddenWorkerLiterals =
    [
        "CodeGraphContext",
        "retrieve_code_logic",
        "PreparedPass2",
        "BusinessLogicDocument",
        "IChatAgentDispatcher",
        "IWorkerAgentDispatcher",
        "RawLsp",
        "JSON-RPC",
        "ProcessStartInfo",
        "HttpClient",
        "Socket",
        "NetworkStream",
        "File.Read",
        "Directory.",
        "DbSet",
        "DbContext",
        "Npgsql",
    ];

    [Fact]
    public void ProductionBoundary_InspectsClosedCompositionAndForbiddenCapabilities()
    {
        string repositoryRoot = FindRepositoryRoot();
        DateTimeOffset observedAt = DateTimeOffset.UtcNow;
        var inspected = new List<InspectedPath>();
        var forbiddenMatches = new List<ForbiddenMatch>();

        foreach (string relativePath in InspectedRelativePaths.Order(StringComparer.Ordinal))
        {
            string path = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), $"Required T047 production path is missing: {relativePath}");
            byte[] bytes = File.ReadAllBytes(path);
            inspected.Add(new(relativePath, bytes.LongLength, Sha256(bytes)));
        }

        foreach (string relativePath in WorkerSurfaceRelativePaths.Order(StringComparer.Ordinal))
        {
            string path = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string text = File.ReadAllText(path);
            foreach (string literal in ForbiddenWorkerLiterals)
            {
                int offset = 0;
                while ((offset = text.IndexOf(literal, offset, StringComparison.Ordinal)) >= 0)
                {
                    forbiddenMatches.Add(new(
                        relativePath,
                        literal,
                        GetLineNumber(text, offset),
                        GetLineText(text, offset)));
                    offset += literal.Length;
                }
            }
        }

        string dispatcherSource = ReadSource(repositoryRoot,
            "src/dotnet/Vulgata.Agents/WorkerExecution/PreparedPass1WorkerDispatcher.cs");
        string navigateSource = ReadSource(repositoryRoot,
            "src/dotnet/Vulgata.Agents/WorkerExecution/PreparedNavigateCodeFunction.cs");
        string endpointSource = ReadSource(repositoryRoot,
            "src/dotnet/Vulgata.Web/Api/Scans/Pass1ExecutionEndpoints.cs");
        string registrationSource = ReadSource(repositoryRoot,
            "src/dotnet/Vulgata.Web/Api/ApiEndpointRegistrationExtensions.cs");
        string programSource = ReadSource(repositoryRoot, "src/dotnet/Vulgata.Web/Program.cs");

        ConstructorBinding navigateBinding = ReadConstructorBinding(typeof(PreparedNavigateCodeFunction));
        ConstructorBinding dispatcherBinding = ReadConstructorBinding(typeof(PreparedPass1WorkerDispatcher));
        var checks = new CompositionChecks(
            navigateBinding.ParameterType == typeof(IPass1NavigationService).FullName,
            dispatcherBinding.ParameterType == typeof(IPreparedPass1JobExecutor).FullName,
            CountOrdinal(navigateSource, "Name = \"navigate_code\"") == 1,
            CountOrdinal(navigateSource, "AIFunctionFactory.Create") == 1,
            CountOrdinal(dispatcherSource, "_maximumToolAttempts = 8") == 1,
            CountOrdinal(dispatcherSource, "MaxDegreeOfParallelism = request.ConcurrencyLimit") == 1,
            CountOrdinal(endpointSource, "RequireAuthorization()") == 4,
            CountOrdinal(registrationSource, "MapPass1ExecutionApiEndpoints()") == 1,
            CountOrdinal(programSource, "AddScoped<PreparedNavigateCodeFunction>()") == 1,
            CountOrdinal(programSource, "AddScoped<IPass1NavigationService>") == 1,
            CountOrdinal(programSource, "AddScoped<PreparedPass1Coordinator>()") == 1);

        Assembly agentsAssembly = typeof(PreparedPass1WorkerDispatcher).Assembly;
        string promptResource = "Vulgata.Agents.Prompts.pass1-code-logic-worker.v2.md";
        string[] promptResources = agentsAssembly.GetManifestResourceNames()
            .Where(name => string.Equals(name, promptResource, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        bool promptClosed = promptResources.Length == 1 &&
            ReadResource(agentsAssembly, promptResources[0]).Contains(
                "code-logic/file-v2", StringComparison.Ordinal) &&
            ReadResource(agentsAssembly, promptResources[0]).Contains(
                "navigate_code", StringComparison.Ordinal);

        string actualSourceCommit = ReadGitValue(repositoryRoot, "rev-parse", "HEAD");
        var artifact = new QualificationArtifact(
            "story-12.3-production-boundary-inspection/1.0",
            "T047",
            PinnedBoundaryCommit,
            actualSourceCommit,
            observedAt,
            "dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter FullyQualifiedName~Pass1ProductionBoundaryQualificationTests",
            inspected,
            ForbiddenWorkerLiterals,
            forbiddenMatches.OrderBy(value => value.Path, StringComparer.Ordinal)
                .ThenBy(value => value.Line)
                .ThenBy(value => value.Literal, StringComparer.Ordinal)
                .ToArray(),
            checks,
            promptResources,
            promptClosed,
            forbiddenMatches.Count == 0 && IsGitObjectId(actualSourceCommit) &&
                checks.All && promptClosed ? "pass" : "fail");

        WriteArtifact(repositoryRoot, artifact);

        Assert.True(IsGitObjectId(actualSourceCommit), actualSourceCommit);
        Assert.Empty(forbiddenMatches);
        Assert.True(checks.All, JsonSerializer.Serialize(checks));
        Assert.True(promptClosed, "The v2 Worker prompt is not uniquely embedded or lacks its closed schema/tool identity.");
    }

    private static ConstructorBinding ReadConstructorBinding(Type type)
    {
        ConstructorInfo[] constructors = type.GetConstructors();
        Assert.Single(constructors);
        ParameterInfo[] parameters = constructors[0].GetParameters();
        Assert.Single(parameters);
        return new(type.FullName!, parameters[0].ParameterType.FullName!);
    }

    private static string ReadSource(string root, string relativePath) =>
        File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string ReadResource(Assembly assembly, string resourceName)
    {
        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static int CountOrdinal(string value, string literal)
    {
        int count = 0;
        int offset = 0;
        while ((offset = value.IndexOf(literal, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += literal.Length;
        }

        return count;
    }

    private static int GetLineNumber(string text, int offset) =>
        text.AsSpan(0, offset).Count('\n') + 1;

    private static string GetLineText(string text, int offset)
    {
        int start = text.LastIndexOf('\n', Math.Max(0, offset - 1));
        int end = text.IndexOf('\n', offset);
        return text[(start < 0 ? 0 : start + 1)..(end < 0 ? text.Length : end)].Trim();
    }

    private static string ReadGitValue(string repositoryRoot, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = repositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start(), "Unable to start git for source binding.");
        string output = process.StandardOutput.ReadToEnd().Trim();
        string error = process.StandardError.ReadToEnd().Trim();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(error), error);
        return output;
    }

    private static void WriteArtifact(string repositoryRoot, QualificationArtifact artifact)
    {
        string path = Path.Combine(repositoryRoot, ArtifactRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(artifact, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        });
        File.WriteAllBytes(path, [.. json, (byte)'\n']);
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static bool IsGitObjectId(string value) =>
        value.Length is 40 or 64 &&
        value.All(character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Vulgata repository root.");
    }

    private sealed record ConstructorBinding(string Type, string ParameterType);

    private sealed record CompositionChecks(
        bool NavigateBinding,
        bool DispatcherBinding,
        bool ExactlyOneNavigateCode,
        bool ExactlyOneFunctionFactory,
        bool EightToolAttempts,
        bool SharedConcurrency,
        bool FourAuthorizedEndpoints,
        bool EndpointRegistration,
        bool NavigateFunctionRegistration,
        bool NavigationPortRegistration,
        bool CoordinatorRegistration)
    {
        public bool All => NavigateBinding && DispatcherBinding && ExactlyOneNavigateCode &&
            ExactlyOneFunctionFactory && EightToolAttempts && SharedConcurrency &&
            FourAuthorizedEndpoints && EndpointRegistration && NavigateFunctionRegistration &&
            NavigationPortRegistration && CoordinatorRegistration;
    }

    private sealed record InspectedPath(string Path, long Bytes, string Sha256);

    private sealed record ForbiddenMatch(string Path, string Literal, int Line, string Text);

    private sealed record QualificationArtifact(
        string SchemaVersion,
        string Task,
        string ExpectedSourceCommit,
        string ObservedSourceCommit,
        DateTimeOffset ObservedAtUtc,
        string QualificationCommand,
        IReadOnlyList<InspectedPath> InspectedPaths,
        IReadOnlyList<string> ForbiddenLiterals,
        IReadOnlyList<ForbiddenMatch> ForbiddenMatches,
        CompositionChecks Composition,
        IReadOnlyList<string> PromptResources,
        bool PromptClosed,
        string Verdict);
}
