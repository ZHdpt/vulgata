using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Data;
using Vulgata.Web.Data;

namespace Vulgata.Tests.PreparedScanning;

public sealed class PreparedScanBoundaryTests
{
    private const string AcceptedBoundary =
        "e997c182a466694bc5eab405bb68fc7396edee53ce7559f443bcbb2e7beaefb6";
    private const string AcceptedSpec =
        "175c367d8a547e5e420e9115cb41d81573f6787597c46a9e6ae6656396ea81cf";
    private const string AcceptedPlan =
        "73991cb36213db17ce00500d43c5040690450c0f50bda733fab0e779362b82ff";

    private static readonly string[] ForbiddenTypeFragments =
    [
        "WorkerAgent",
        "ScanJobExecution",
        "DocumentVersion",
        "LogicalDocument",
        "DocumentEdge",
        "Uncertainty",
        "ExactCrossRepository",
        "Pass2",
        "Chat",
        "BackupB",
        "CodeGraphContext",
        "LlmProvider",
    ];

    private static readonly string[] PreparedSourcePrefixes =
    [
        "PreparedScan",
        "PreparedFile",
        "PreparedRevision",
        "PreparedCodeGraph",
        "PreparedRelated",
        "PreparedJava",
        "PreparedCommunication",
        "SafeXml",
        "XmlEvidence",
        "CommunicationCatalog",
        "CommunicationPatternCatalog",
    ];

    [Fact]
    public void PreparedScanComposition_ReferencesNoStory123OrDemoService()
    {
        Type coordinator = typeof(PreparedScanCoordinator);
        ConstructorInfo constructor = Assert.Single(coordinator.GetConstructors());
        string[] dependencies =
        [
            .. constructor.GetParameters().Select(parameter => TypeIdentity(parameter.ParameterType)),
        ];

        Assert.Equal(
            [
                typeof(ISystemRepository).FullName,
                typeof(IRepositoryRepository).FullName,
                typeof(IPreparedScanPairBuilder).FullName,
                typeof(IPreparedScanStore).FullName,
                typeof(IPreparedScanHandoffSource).FullName,
            ],
            dependencies);
        AssertNoForbiddenTypeIdentity(dependencies);
    }

    [Fact]
    public void PreparedScanPublicGraph_ExposesNoDispatchDocumentOrResolutionAuthority()
    {
        Assembly[] assemblies =
        [
            typeof(PreparedScanHandoff).Assembly,
            typeof(PreparedScanStore).Assembly,
            typeof(PreparedScanCoordinator).Assembly,
        ];
        Type[] preparedTypes =
        [
            .. assemblies
                .SelectMany(assembly => assembly.GetTypes())
                .Where(IsStory12_2PreparedType)
                .Distinct(),
        ];
        Assert.NotEmpty(preparedTypes);

        var referencedIdentities = new List<string>();
        foreach (Type type in preparedTypes)
        {
            referencedIdentities.Add(TypeIdentity(type));
            referencedIdentities.AddRange(type.GetInterfaces().Select(TypeIdentity));
            if (type.BaseType is not null)
            {
                referencedIdentities.Add(TypeIdentity(type.BaseType));
            }

            referencedIdentities.AddRange(type.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Select(parameter => TypeIdentity(parameter.ParameterType)));
            referencedIdentities.AddRange(type.GetMethods(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .SelectMany(method => method.GetParameters()
                    .Select(parameter => TypeIdentity(parameter.ParameterType))
                    .Append(TypeIdentity(method.ReturnType))));
            referencedIdentities.AddRange(type.GetProperties()
                .Select(property => TypeIdentity(property.PropertyType)));
        }

        AssertNoForbiddenTypeIdentity(referencedIdentities);
    }

    [Fact]
    public void PreparedScanProductionSources_ContainNoForbiddenInvocationOrCreation()
    {
        string repositoryRoot = FindRepositoryRoot();
        string sourceRoot = Path.Combine(repositoryRoot, "src", "dotnet");
        string[] preparedFiles =
        [
            .. Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => PreparedSourcePrefixes.Any(prefix =>
                    Path.GetFileNameWithoutExtension(path).StartsWith(prefix, StringComparison.Ordinal)))
                .Where(path => !Path.GetFileNameWithoutExtension(path)
                    .StartsWith("PreparedScanBoundary", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal),
        ];
        Assert.NotEmpty(preparedFiles);

        string[] forbiddenTokens =
        [
            "IWorkerAgentDispatcher",
            "WorkerAgentJobPacket",
            "ScanJobExecution",
            "navigate_code",
            "DocumentVersion",
            "LogicalDocument",
            "DocumentEdge",
            "Uncertainty",
            "IExactCrossRepositoryResolver",
            "ExactCrossRepositoryResolution",
            "PreparedPass2",
            "IChatAgentDispatcher",
            "IPreparedChatScopeResolver",
            "BackupB",
            "CodeGraphContext",
            "ILlmProviderRepository",
        ];
        string[] matches =
        [
            .. preparedFiles.SelectMany(path =>
            {
                string text = File.ReadAllText(path);
                return forbiddenTokens
                    .Where(token => text.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)}: {token}");
            }),
        ];

        Assert.True(matches.Length == 0, string.Join(Environment.NewLine, matches));
    }

    [Fact]
    public void ApprovedRuntimeAndWorkflowPins_MatchAcceptedBoundary()
    {
        string repositoryRoot = FindRepositoryRoot();
        string featureRoot = Path.Combine(repositoryRoot, "specs", "011-java-xml-prescan-manifest");
        using JsonDocument state = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(featureRoot, "workflow-state.json")));
        JsonElement root = state.RootElement;

        Assert.Equal("3.0", root.GetProperty("schema_version").GetString());
        Assert.Contains(
            root.GetProperty("stage").GetString(),
            new[] { "implement", "converge", "quality-review", "conditional-e2e", "verify", "complete" });
        JsonElement implementationApproval = Assert.Single(
            root.GetProperty("approvals").EnumerateArray().Where(approval =>
                approval.GetProperty("decision_id").GetString() ==
                "story-12-2-implementation-approval-01"));
        Assert.Equal("accepted", implementationApproval.GetProperty("status").GetString());
        Assert.Equal(AcceptedBoundary, implementationApproval.GetProperty("scope_sha256").GetString());

        Assert.Equal(AcceptedSpec, Sha256(Path.Combine(featureRoot, "spec.md")));
        Assert.Equal(AcceptedPlan, Sha256(Path.Combine(featureRoot, "plan.md")));

        string program = File.ReadAllText(Path.Combine(
            repositoryRoot, "src", "dotnet", "Vulgata.Web", "Program.cs"));
        string packages = File.ReadAllText(Path.Combine(repositoryRoot, "Directory.Packages.props"));
        string compose = File.ReadAllText(Path.Combine(repositoryRoot, "docker-compose.yml"));
        Assert.Contains("@colbymchenry/codegraph-win32-x64@1.5.0", program, StringComparison.Ordinal);
        Assert.Contains("jdt-language-server-1.61.0-202608141332", program, StringComparison.Ordinal);
        Assert.Contains("JavaMajorVersion\"), \"21\"", program, StringComparison.Ordinal);
        Assert.Contains("StreamJsonRpc\" Version=\"2.25.29\"", packages, StringComparison.Ordinal);
        Assert.Contains("image: postgres:17-alpine", compose, StringComparison.Ordinal);
    }

    private static bool IsStory12_2PreparedType(Type type) =>
        PreparedSourcePrefixes.Any(prefix => type.Name.StartsWith(prefix, StringComparison.Ordinal));

    private static void AssertNoForbiddenTypeIdentity(IEnumerable<string> identities)
    {
        string[] violations =
        [
            .. identities
                .Distinct(StringComparer.Ordinal)
                .Where(identity => ForbiddenTypeFragments.Any(fragment =>
                    identity.Contains(fragment, StringComparison.Ordinal)))
                .OrderBy(identity => identity, StringComparer.Ordinal),
        ];
        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    private static string TypeIdentity(Type type)
    {
        if (type.IsArray)
        {
            return $"{TypeIdentity(type.GetElementType()!)}[]";
        }

        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        return $"{type.GetGenericTypeDefinition().FullName}[{string.Join(",", type.GetGenericArguments().Select(TypeIdentity))}]";
    }

    private static string Sha256(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

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
}
