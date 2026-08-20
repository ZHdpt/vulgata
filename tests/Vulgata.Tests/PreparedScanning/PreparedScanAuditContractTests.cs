using System.Reflection;
using Vulgata.Core.Entities;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// T019 RED coverage for the one canonical audit constructor.  The factory
/// is deliberately located through the public Core seam so no test-side
/// fingerprint or UUID implementation can become a second authority.
/// </summary>
public sealed class PreparedScanAuditContractTests
{
    [Fact]
    public void PreparedScanAuditFactory_ExposesOneCanonicalConstructionSeam()
    {
        Type factory = RequirePublicType(
            typeof(CodeUnit).Assembly,
            "Vulgata.Core.DomainServices.PreparedScanAuditFactory");

        MethodInfo[] createMethods = factory
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name is "Create" or "CreateSelected" or "CreateOutcome")
            .ToArray();

        Assert.Single(createMethods.Where(method => method.Name == "Create"));

        MethodInfo create = createMethods.Single(method => method.Name == "Create");
        Assert.NotEqual(typeof(void), create.ReturnType);
        Assert.Contains(
            create.ReturnType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name == "DetailFingerprint");
        Assert.Contains(
            create.ReturnType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name == "Id");
    }

    [Fact]
    public void PreparedScanAuditContract_PersistsAllTenPreimageFieldsAndExcludesDerivedIdentityInputs()
    {
        Type factory = RequirePublicType(
            typeof(CodeUnit).Assembly,
            "Vulgata.Core.DomainServices.PreparedScanAuditFactory");
        MethodInfo create = Assert.Single(
            factory.GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name == "Create");
        Type resultType = create.ReturnType;
        PropertyInfo[] properties = resultType.GetProperties(
            BindingFlags.Public | BindingFlags.Instance);

        string[] preimageNames =
        [
            "RepositoryId", "ScanRunId", "RelativePath", "Outcome", "Reason",
            "FileHash", "RawByteCount", "LogicalLineCount",
            "FileCodeUnitIdentityFingerprint", "PairSelectionOrdinal",
        ];
        Assert.All(
            preimageNames,
            name => Assert.Contains(properties, property => property.Name == name));
        Assert.DoesNotContain(properties, property => property.Name == "CapturedAt");
        Assert.DoesNotContain(properties, property => property.Name == "AbsolutePath");
        Assert.DoesNotContain(properties, property => property.Name == "SourceContent");
    }

    private static Type RequirePublicType(Assembly assembly, string fullName)
    {
        Type? type = assembly.GetType(fullName);
        Assert.NotNull(type);
        Assert.True(type!.IsPublic, $"{fullName} must be the public Core audit seam.");
        Assert.False(type.IsAbstract);
        return type;
    }
}
