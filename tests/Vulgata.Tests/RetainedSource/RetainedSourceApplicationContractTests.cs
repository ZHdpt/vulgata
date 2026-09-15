using System.Reflection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Tests.RetainedSource;

public sealed class RetainedSourceApplicationContractTests
{
    [Theory]
    [MemberData(nameof(InvalidSourceSnapshots))]
    public void SourceSnapshot_Create_InvalidIdentityOrValue_ThrowsArgumentException(
        InvalidSourceSnapshot input)
    {
        Assert.ThrowsAny<ArgumentException>(() => SourceSnapshot.Create(
            input.RepositoryId,
            input.ProfiledRevision!,
            input.GitObjectFormat!,
            input.ManifestSha256!,
            input.EntryCount,
            input.TotalBlobBytes,
            input.InternalStorageKey!,
            input.CompletedAt));
    }

    [Fact]
    public void SourceSnapshot_Create_ValidIdentityAndValues_AcceptsSourceOnlyRecord()
    {
        SourceSnapshot snapshot = SourceSnapshot.Create(
            Guid.Parse("12000000-0000-4000-8000-000000000001"),
            new string('a', 40),
            "sha1",
            new string('b', 64),
            entryCount: 2,
            totalBlobBytes: 17,
            "snapshots/12000000000040008000000000000001/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal("retained-source-manifest/1.0", snapshot.ManifestSchemaIdentity);
        Assert.Equal(2, snapshot.EntryCount);
        Assert.Equal(17, snapshot.TotalBlobBytes);
    }

    [Fact]
    public void PublicCaptureContract_AlreadyCorrect_UsesOnlyRepositoryRefAndCancellation()
    {
        MethodInfo acquire = typeof(IRetainedSourceApplication).GetMethod(
            nameof(IRetainedSourceApplication.AcquireAsync))!;

        Assert.Equal(
            typeof(Task<SourceCaptureResult>),
            acquire.ReturnType);
        string?[] expectedParameterNames =
            ["repositoryId", "permittedRef", "cancellationToken"];
        Assert.Equal(
            expectedParameterNames,
            acquire.GetParameters().Select(parameter => parameter.Name).ToArray());
        Type[] expectedParameterTypes =
            [typeof(Guid), typeof(string), typeof(CancellationToken)];
        Assert.Equal(
            expectedParameterTypes,
            acquire.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
        Assert.DoesNotContain(
            acquire.GetParameters(),
            parameter => parameter.Name is "maximumEntries" or "maximumBlobBytes" or
                "maximumAggregateBlobBytes" or "deadline" or "limits");
    }

    [Fact]
    public void OrdinaryResults_AlreadyCorrect_ContainNoPhysicalWorkspaceLocators()
    {
        Type[] ordinarySurface =
        [
            typeof(SourceCaptureResult),
            typeof(OpenSourceReadContextResult),
            typeof(SourceEntry),
            typeof(SourceEntryResult),
            typeof(SourceListResult),
            typeof(SourceBytesResult),
            typeof(SourceTextResult),
        ];
        string[] forbiddenFragments =
        [
            "RootPath",
            "PhysicalPath",
            "WorkspacePath",
            "StorageKey",
            "Credential",
            "OriginUrl",
        ];

        foreach (Type surface in ordinarySurface)
        {
            Assert.DoesNotContain(
                surface.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                property => forbiddenFragments.Any(fragment =>
                    property.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public static IEnumerable<object[]> InvalidSourceSnapshots()
    {
        InvalidSourceSnapshot valid = ValidSourceSnapshot();

        yield return [valid with { RepositoryId = Guid.Empty }];
        yield return [valid with { ProfiledRevision = null }];
        yield return [valid with { ProfiledRevision = string.Empty }];
        yield return [valid with { ProfiledRevision = new string('A', 40) }];
        yield return [valid with { ProfiledRevision = new string('a', 39) }];
        yield return [valid with { ProfiledRevision = new string('a', 41) }];
        yield return [valid with { ProfiledRevision = new string('a', 64), GitObjectFormat = "sha1" }];
        yield return [valid with { GitObjectFormat = null }];
        yield return [valid with { GitObjectFormat = "sha512" }];
        yield return [valid with { GitObjectFormat = "sha256" }];
        yield return [valid with { ManifestSha256 = null }];
        yield return [valid with { ManifestSha256 = new string('B', 64) }];
        yield return [valid with { ManifestSha256 = new string('b', 63) }];
        yield return [valid with { ManifestSha256 = new string('b', 65) }];
        yield return [valid with { EntryCount = -1 }];
        yield return [valid with { TotalBlobBytes = -1 }];
        yield return [valid with { InternalStorageKey = null }];
        yield return [valid with { InternalStorageKey = string.Empty }];
        yield return [valid with { InternalStorageKey = "../outside" }];
        yield return [valid with { InternalStorageKey = "/absolute" }];
        yield return [valid with { InternalStorageKey = "C:\\absolute" }];
        yield return [valid with { CompletedAt = new DateTimeOffset(
            2026,
            9,
            15,
            8,
            0,
            0,
            TimeSpan.FromHours(8)) }];
    }

    private static InvalidSourceSnapshot ValidSourceSnapshot() =>
        new(
            Guid.Parse("12000000-0000-4000-8000-000000000001"),
            new string('a', 40),
            "sha1",
            new string('b', 64),
            2,
            17,
            "snapshots/12000000000040008000000000000001/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero));

    public sealed record InvalidSourceSnapshot(
        Guid RepositoryId,
        string? ProfiledRevision,
        string? GitObjectFormat,
        string? ManifestSha256,
        long EntryCount,
        long TotalBlobBytes,
        string? InternalStorageKey,
        DateTimeOffset CompletedAt);
}
