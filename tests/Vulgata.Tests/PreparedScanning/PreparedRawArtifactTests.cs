using System.Reflection;
using Vulgata.Infrastructure.Git;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// T017 RED coverage for immutable raw artifacts.  The test asserts the
/// accepted public seam before T018 supplies the implementation; this avoids
/// introducing a test-owned hashing helper that could mask production drift.
/// </summary>
public sealed class PreparedRawArtifactTests
{
    [Fact]
    public void PreparedRawArtifactReader_ExposesRawByteBoundedReadPort()
    {
        Type reader = RequirePublicType(
            typeof(ExactGitRevisionSnapshotService).Assembly,
            "Vulgata.Infrastructure.Git.PreparedScanRawArtifactReader");

        MethodInfo read = Assert.Single(
            reader.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "ReadAsync");

        Assert.True(typeof(Task).IsAssignableFrom(read.ReturnType));
        Assert.Contains(
            read.GetParameters(),
            parameter => parameter.ParameterType == typeof(CancellationToken));
        Assert.Contains(
            read.GetParameters(),
            parameter => parameter.ParameterType == typeof(string));
    }

    [Fact]
    public void PreparedRawArtifactReader_ReturnModelCarriesBothExactHashesAndSnapshotBinding()
    {
        Type reader = RequirePublicType(
            typeof(ExactGitRevisionSnapshotService).Assembly,
            "Vulgata.Infrastructure.Git.PreparedScanRawArtifactReader");
        MethodInfo read = Assert.Single(
            reader.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "ReadAsync");
        Type resultType = UnwrapTask(read.ReturnType);

        Assert.Contains(
            resultType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name == "FileHash");
        Assert.Contains(
            resultType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name == "SourceHash");
        Assert.Contains(
            resultType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name is "ProfiledRevision" or "SnapshotIdentity");
        Assert.DoesNotContain(
            resultType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name.Contains("AbsolutePath", StringComparison.OrdinalIgnoreCase));
    }

    private static Type UnwrapTask(Type returnType)
    {
        Assert.True(returnType.IsGenericType);
        Assert.Equal(typeof(Task<>), returnType.GetGenericTypeDefinition());
        return returnType.GetGenericArguments()[0];
    }

    private static Type RequirePublicType(Assembly assembly, string fullName)
    {
        Type? type = assembly.GetType(fullName);
        Assert.NotNull(type);
        Assert.True(type!.IsPublic, $"{fullName} must be a public raw-artifact seam.");
        Assert.False(type.IsAbstract);
        return type;
    }
}
