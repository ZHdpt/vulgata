using Vulgata.Tests.TestInfrastructure;
using System.Collections;
using System.Text.Json;
using Vulgata.Core.DomainServices;

namespace Vulgata.Tests.AnalysisRuntime;

public sealed class AnalysisRuntimeIdentityTests
{
    private static readonly Guid _repositoryId =
        Guid.Parse("12000000-0000-4000-8000-000000000012");

    private const string ProfiledRevision =
        "ABCDEF0123456789ABCDEF0123456789ABCDEF01";

    private const string NpmShimHash =
        "80419260F06862D7A422D13EF69E3B24C7A21DBEF4309BCE41DB02AD642B1877";

    private const string NodeHash =
        "B3094D0B49F9AD602262A9921551737BB97637C05DD357A06AE98188D7290AA3";

    private const string CodeGraphHash =
        "03E4C791CC0DD91ED264278461BF9A56C0278AA0670D5942FC4732311C66DE03";

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Create_ApprovedRuntime_NormalizesPinnedValuesAndRetainsRepositoryRevisionBinding()
    {
        AnalysisRuntimeIdentity identity = CreateIdentity();

        Assert.Equal("analysis-runtime/1", identity.SchemaIdentity);
        Assert.Equal("@colbymchenry/codegraph", identity.CodeGraphPackageName);
        Assert.Equal("1.5.0", identity.CodeGraphPackageVersion);
        Assert.Equal("https://github.com/colbymchenry/codegraph", identity.CodeGraphRepository);
        Assert.Equal("@colbymchenry/codegraph-win32-x64@1.5.0", identity.CodeGraphPlatformPackage);
        Assert.Equal("codegraph", identity.CodeGraphCommand);
        Assert.Equal("jdt-language-server-1.61.0-202608141332", identity.JdtLsReleaseIdentity);
        Assert.Equal(21, identity.JavaMajorVersion);
        Assert.Equal("StreamJsonRpc", identity.ClientName);
        Assert.Equal("2.25.29", identity.ClientVersion);
        Assert.Equal("stdio", identity.Transport);
        Assert.Equal(_repositoryId, identity.RepositoryId);
        Assert.Equal(ProfiledRevision.ToLowerInvariant(), identity.ProfiledRevision);
        Assert.Equal(
            new[] { "lib/dist/bin/codegraph.js", "node.exe", "npm-shim.js" },
            identity.CodeGraphFiles.Select(file => file.RelativeComponent).ToArray());
        Assert.Equal(
            new[] { CodeGraphHash.ToLowerInvariant(), NodeHash.ToLowerInvariant(), NpmShimHash.ToLowerInvariant() },
            identity.CodeGraphFiles.Select(file => file.Sha256).ToArray());
        Assert.Equal(64, identity.Sha256.Length);
        Assert.Equal(identity.Sha256, AnalysisRuntimeIdentity.ComputeSha256(identity.CanonicalJson));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void CanonicalJson_UsesSortedObjectFieldsAndExactClientVersionKey()
    {
        AnalysisRuntimeIdentity identity = CreateIdentity();
        using JsonDocument document = JsonDocument.Parse(identity.CanonicalJson);

        AssertSortedObjectFields(document.RootElement);
        JsonElement client = document.RootElement.GetProperty("client");
        Assert.True(client.TryGetProperty("clientVersion", out JsonElement clientVersion));
        Assert.Equal("2.25.29", clientVersion.GetString());
        Assert.False(client.TryGetProperty("version", out _));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Create_SameLogicalRuntimeWithDifferentHostRoots_HasIdenticalCanonicalJsonAndHash()
    {
        AnalysisRuntimeOptions firstOptions = new(
            CodeGraphCommandPath: @"G:\packages\npm\prefix\codegraph.ps1",
            JdtLsInstallationRoot: @"C:\Users\13728\.local\jdt-language-server-1.61.0-202608141332",
            JavaHome: @"C:\Program Files\Java\latest\jdk-21");
        AnalysisRuntimeOptions relocatedOptions = new(
            CodeGraphCommandPath: @"D:\tools\codegraph.ps1",
            JdtLsInstallationRoot: @"D:\runtime\jdt-language-server-1.61.0-202608141332",
            JavaHome: @"D:\java\jdk-21");

        AnalysisRuntimeIdentity first = CreateIdentity(firstOptions);
        AnalysisRuntimeIdentity relocated = CreateIdentity(relocatedOptions);

        Assert.Equal(first.CanonicalJson, relocated.CanonicalJson);
        Assert.Equal(first.Sha256, relocated.Sha256);
        Assert.DoesNotContain(@"G:\packages\npm\prefix", first.CanonicalJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"C:\Users\13728", first.CanonicalJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"C:\Program Files\Java", first.CanonicalJson, StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Create_InvalidVersionHashRevisionOrHostPathIdentity_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => CreateIdentity(profiledRevision: "../../workspace"));
        Assert.Throws<ArgumentException>(() => CreateIdentity(codeGraphPackageVersion: "1.5.1"));
        Assert.Throws<ArgumentException>(() => CreateIdentity(javaMajorVersion: 17));
        Assert.Throws<ArgumentException>(() => CreateIdentity(clientVersion: "2.25.28"));
        Assert.Throws<ArgumentException>(() => CreateIdentity(codeGraphHashes: [new("node.exe", new string('0', 64))]));
        Assert.Throws<ArgumentException>(() => CreateIdentity(jdtLsDistributionArchiveName: @"C:\temp\jdt.zip"));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Create_MissingJdtDistributionArchiveNameOrChecksum_IsRejected_CoreJarObservationCannotSubstitute()
    {
        Assert.Throws<ArgumentException>(() => CreateIdentity(jdtLsDistributionArchiveName: null));
        Assert.Throws<ArgumentException>(() => CreateIdentity(jdtLsDistributionArchiveSha256: null));
        Assert.Throws<ArgumentException>(() => CreateIdentity(
            jdtLsDistributionArchiveSha256:
                "A58994113B7E493AA8CED51AD02DAB3E424973223E7213F7ED5B9D70861582E2"));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void HealthResult_NotReady_UsesOnlyClosedCategoriesAndSanitizedBoundedDiagnostics()
    {
        AnalysisRuntimeHealthResult result = AnalysisRuntimeHealthResult.NotReady(
            AnalysisRuntimeHealthFailureCategory.JdtDistributionArchiveChecksumMissing,
            "JDT distribution archive checksum is not configured; root=C:\\Users\\13728\\.local");

        Assert.Equal(AnalysisRuntimeHealthStatus.NotReady, result.Status);
        Assert.Equal(
            AnalysisRuntimeHealthFailureCategory.JdtDistributionArchiveChecksumMissing,
            result.FailureCategory);
        Assert.Null(result.Identity);
        Assert.Null(result.IdentityHash);
        Assert.DoesNotContain("C:\\Users", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\\", result.Diagnostic);
        Assert.DoesNotContain("/", result.Diagnostic);
        Assert.DoesNotContain("stderr", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.InRange(result.Diagnostic.Length, 1, 256);
        Assert.NotNull(result.FailureCode);
        AssertNormalizedMachineCode(result.FailureCode!);
        AssertUtcObservedAt(result.ObservedAt);
        AssertOrderedImmutableComponentChecks(result.ComponentChecks);

        string[] allowed =
        [
            nameof(AnalysisRuntimeHealthFailureCategory.None),
            nameof(AnalysisRuntimeHealthFailureCategory.CodeGraphIntegrityMismatch),
            nameof(AnalysisRuntimeHealthFailureCategory.CodeGraphVersionMismatch),
            nameof(AnalysisRuntimeHealthFailureCategory.JdtDistributionArchiveMissing),
            nameof(AnalysisRuntimeHealthFailureCategory.JdtDistributionArchiveChecksumMissing),
            nameof(AnalysisRuntimeHealthFailureCategory.JavaMajorVersionMismatch),
            nameof(AnalysisRuntimeHealthFailureCategory.ClientVersionMismatch),
            nameof(AnalysisRuntimeHealthFailureCategory.WorkspaceConflict),
            nameof(AnalysisRuntimeHealthFailureCategory.InvalidIdentity),
        ];
        Assert.Equal(allowed.Order(), Enum.GetNames<AnalysisRuntimeHealthFailureCategory>().Order());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void HealthResult_Ready_CarriesCanonicalIdentityAndNoRawHostDiagnostics()
    {
        AnalysisRuntimeIdentity identity = CreateIdentity();
        AnalysisRuntimeHealthResult result = AnalysisRuntimeHealthResult.Ready(identity);

        Assert.Equal(AnalysisRuntimeHealthStatus.Ready, result.Status);
        Assert.Same(identity, result.Identity);
        Assert.Equal(identity.Sha256, result.IdentityHash);
        Assert.DoesNotContain("C:\\", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/", result.Diagnostic);
        Assert.InRange(result.Diagnostic.Length, 0, 256);
        Assert.NotNull(result.FailureCode);
        AssertNormalizedMachineCode(result.FailureCode!);
        AssertUtcObservedAt(result.ObservedAt);
        AssertOrderedImmutableComponentChecks(result.ComponentChecks);
    }

    private static AnalysisRuntimeIdentity CreateIdentity(
        AnalysisRuntimeOptions? options = null,
        string? profiledRevision = null,
        string? codeGraphPackageVersion = null,
        int javaMajorVersion = 21,
        string? clientVersion = null,
        IReadOnlyList<AnalysisRuntimeComponentHash>? codeGraphHashes = null,
        string? jdtLsDistributionArchiveName = "jdt-language-server-1.61.0-202608141332.zip",
        string? jdtLsDistributionArchiveSha256 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa") =>
        AnalysisRuntimeIdentity.Create(
            options ?? new AnalysisRuntimeOptions(
                CodeGraphCommandPath: @"G:\packages\npm\prefix\codegraph.ps1",
                JdtLsInstallationRoot: @"C:\Users\13728\.local\jdt-language-server-1.61.0-202608141332",
                JavaHome: @"C:\Program Files\Java\latest\jdk-21"),
            repositoryId: _repositoryId,
            profiledRevision: profiledRevision ?? ProfiledRevision,
            codeGraphPackageName: " @colbymchenry/codegraph ",
            codeGraphPackageVersion: codeGraphPackageVersion ?? " 1.5.0 ",
            codeGraphRepository: " https://github.com/ColbyMcHenry/CodeGraph ",
            codeGraphCommand: " codegraph ",
            codeGraphPlatformPackage: " @colbymchenry/codegraph-win32-x64@1.5.0 ",
            codeGraphHashes: codeGraphHashes ??
            [
                new(" npm-shim.js ", NpmShimHash),
                new(" node.exe ", NodeHash),
                new(" lib/dist/bin/codegraph.js ", CodeGraphHash),
            ],
            jdtLsReleaseIdentity: " jdt-language-server-1.61.0-202608141332 ",
            jdtLsDistributionArchiveName,
            jdtLsDistributionArchiveSha256,
            javaMajorVersion,
            clientName: " StreamJsonRpc ",
            clientVersion: clientVersion ?? " 2.25.29 ",
            transport: " stdio ");

    private static void AssertSortedObjectFields(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            string[] names = element.EnumerateObject().Select(property => property.Name).ToArray();
            Assert.Equal(names.Order(StringComparer.Ordinal), names);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                AssertSortedObjectFields(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in element.EnumerateArray())
            {
                AssertSortedObjectFields(child);
            }
        }
    }

    private static void AssertOrderedImmutableComponentChecks(object componentChecks)
    {
        Assert.NotNull(componentChecks);
        Type propertyType = typeof(AnalysisRuntimeHealthResult)
            .GetProperty(nameof(AnalysisRuntimeHealthResult.ComponentChecks))!
            .PropertyType;
        Assert.True(
            propertyType.IsGenericType &&
            propertyType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>));

        string[] serialized = ((IEnumerable)componentChecks)
            .Cast<object>()
            .Select(value => JsonSerializer.Serialize(value))
            .ToArray();
        Assert.NotEmpty(serialized);
        Assert.Equal(serialized.Order(StringComparer.Ordinal), serialized);
    }

    private static void AssertNormalizedMachineCode(string failureCode)
    {
        Assert.InRange(failureCode.Length, 1, 64);
        Assert.Equal(failureCode, failureCode.Trim());
        Assert.Equal(failureCode, failureCode.ToLowerInvariant());
        Assert.DoesNotContain(' ', failureCode);
        Assert.DoesNotContain('\\', failureCode);
        Assert.DoesNotContain('/', failureCode);
    }

    private static void AssertUtcObservedAt(DateTimeOffset observedAt)
    {
        Assert.Equal(TimeSpan.Zero, observedAt.Offset);
        Assert.InRange(observedAt, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
    }
}
