using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests.RetainedSource;

/// <summary>
/// Capture-only manifest characterization. The application seam is public; the
/// manifest file is read only from this test's owned retained-source object so
/// that the exact persisted bytes can be checked independently of the result DTO.
/// </summary>
public sealed class RetainedSourceManifestTests
{
    [Fact]
    public async Task AcquireAsync_PersistsCanonicalManifestWithRawGitIdentityAndByteEvidence()
    {
        var files = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal)
        {
            [".gitattributes"] = Encoding.UTF8.GetBytes(
                "/ignored.txt export-ignore\n" +
                "/substituted.txt export-subst\n"),
            ["z.txt"] = Encoding.UTF8.GetBytes("z-line\r\n"),
            ["a.txt"] = Encoding.UTF8.GetBytes("a-line\n"),
            ["binary.bin"] = new byte[] { 0x00, 0xFF, 0x0A, 0x80 },
            ["ignored.txt"] = Encoding.UTF8.GetBytes("archive exclusion does not change raw Git\n"),
            ["substituted.txt"] = Encoding.UTF8.GetBytes("$Format:%H$\n"),
        };

        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync(files);
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);

        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        string expectedRevision = await fixture.Repository.ResolveRevisionAsync(permittedRef);
        SourceCaptureResult result = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .AcquireAsync(fixture.RepositoryId, permittedRef);

        Assert.Equal(SourceCaptureStatus.Completed, result.Status);
        Assert.Equal(fixture.RepositoryId, result.RepositoryId);
        Assert.Equal(expectedRevision, result.ProfiledRevision);
        Assert.Equal("sha1", result.GitObjectFormat);
        Assert.Equal(files.Count, result.EntryCount);
        Assert.Equal(files.Values.Sum(value => (long)value.Length), result.TotalBlobBytes);
        Assert.NotNull(result.ManifestSha256);

        IDbContextFactory<VulgataDbContext> contextFactory = appFixture.Scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        await using VulgataDbContext context = await contextFactory.CreateDbContextAsync();
        SourceSnapshot snapshot = await context.SourceSnapshots
            .AsNoTracking()
            .SingleAsync(value => value.RepositoryId == fixture.RepositoryId);

        Assert.Equal(result.ManifestSha256, snapshot.ManifestSha256);
        Assert.Equal(result.EntryCount, snapshot.EntryCount);
        Assert.Equal(result.TotalBlobBytes, snapshot.TotalBlobBytes);
        Assert.Equal(expectedRevision, snapshot.ProfiledRevision);
        Assert.Equal("sha1", snapshot.GitObjectFormat);
        Assert.Equal(SourceSnapshot.RetainedSourceManifestSchemaIdentity, snapshot.ManifestSchemaIdentity);

        string objectRoot = Path.Combine(
            new[] { fixture.SourceStorageRoot }.Concat(snapshot.InternalStorageKey.Split('/')).ToArray());
        string manifestPath = Path.Combine(objectRoot, "manifest.json");
        byte[] manifestBytes = await File.ReadAllBytesAsync(manifestPath);
        Assert.NotEmpty(manifestBytes);
        Assert.False(manifestBytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.Equal(result.ManifestSha256, Convert.ToHexStringLower(SHA256.HashData(manifestBytes)));
        Assert.Equal(manifestBytes, Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(manifestBytes)));

        using JsonDocument document = JsonDocument.Parse(manifestBytes);
        JsonElement root = document.RootElement;
        Assert.Equal(
            new[]
            {
                "schemaIdentity", "repositoryId", "profiledRevision", "gitObjectFormat",
                "entryCount", "totalBlobBytes", "entries",
            },
            root.EnumerateObject().Select(property => property.Name));
        Assert.Equal("retained-source-manifest/1.0", root.GetProperty("schemaIdentity").GetString());
        Assert.Equal(fixture.RepositoryId.ToString("D"), root.GetProperty("repositoryId").GetString());
        Assert.Equal(expectedRevision, root.GetProperty("profiledRevision").GetString());
        Assert.Equal("sha1", root.GetProperty("gitObjectFormat").GetString());
        Assert.Equal(files.Count, root.GetProperty("entryCount").GetInt32());
        Assert.Equal(files.Values.Sum(value => (long)value.Length), root.GetProperty("totalBlobBytes").GetInt64());

        JsonElement entries = root.GetProperty("entries");
        Assert.Equal(files.Count, entries.GetArrayLength());
        byte[]? previousPathBytes = null;
        var seenPaths = new HashSet<string>(StringComparer.Ordinal);
        int ordinal = 0;
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            Assert.Equal(ordinal, entry.GetProperty("ordinal").GetInt32());
            byte[] pathBytes = Convert.FromBase64String(entry.GetProperty("pathBytesBase64").GetString()!);
            if (previousPathBytes is not null)
            {
                Assert.True(
                    previousPathBytes.AsSpan().SequenceCompareTo(pathBytes) < 0,
                    "Manifest entries must be sorted by raw path bytes.");
            }

            previousPathBytes = pathBytes;
            string relativePath = entry.GetProperty("relativePath").GetString()!;
            Assert.True(seenPaths.Add(relativePath));
            Assert.True(files.ContainsKey(relativePath));
            byte[] expectedBytes = files[relativePath].ToArray();
            Assert.Equal(Encoding.UTF8.GetBytes(relativePath), pathBytes);
            Assert.Equal("regularFile", entry.GetProperty("entryKind").GetString());
            Assert.Equal("100644", entry.GetProperty("gitMode").GetString());

            string expectedObjectId = await fixture.Repository.RunGitAsync(
                ["rev-parse", $"{expectedRevision}:{relativePath}"]);
            string objectId = entry.GetProperty("gitObjectId").GetString()!;
            Assert.Equal(expectedObjectId, objectId);
            Assert.Matches("^[0-9a-f]{40}$", objectId);
            Assert.Equal(expectedBytes.LongLength, entry.GetProperty("byteLength").GetInt64());

            string expectedContentSha = Convert.ToHexStringLower(SHA256.HashData(expectedBytes));
            Assert.Equal(expectedContentSha, entry.GetProperty("contentSha256").GetString());
            Assert.Equal($"blobs/{expectedContentSha}", entry.GetProperty("storageBlobKey").GetString());

            JsonElement capabilities = entry.GetProperty("capabilities");
            if (string.Equals(relativePath, "binary.bin", StringComparison.Ordinal))
            {
                Assert.Equal(new[] { "metadata", "byteRange" }, capabilities.EnumerateArray()
                    .Select(value => value.GetString()).ToArray());
                Assert.Equal("source_text_unsupported", entry.GetProperty("limitationCode").GetString());
            }
            else
            {
                Assert.Equal(new[] { "metadata", "byteRange", "strictUtf8Text" }, capabilities.EnumerateArray()
                    .Select(value => value.GetString()).ToArray());
                Assert.True(entry.GetProperty("limitationCode").ValueKind == JsonValueKind.Null);
            }

            string blobPath = Path.Combine(
                new[] { objectRoot }.Concat(entry.GetProperty("storageBlobKey").GetString()!.Split('/')).ToArray());
            Assert.Equal(expectedBytes, await File.ReadAllBytesAsync(blobPath));
            ordinal++;
        }

        Assert.Equal(
            files.Keys.OrderBy(value => value, StringComparer.Ordinal),
            seenPaths.OrderBy(value => value, StringComparer.Ordinal));
    }

    [Fact]
    public async Task AcquireAsync_ManifestRetainsSymlinkAndGitlinkAsExplicitMetadataLimitations()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await fixture.Repository.AddSymbolicLinkEntryAsync("link", "target.txt");
        await fixture.Repository.AddGitlinkEntryAsync("submodule");
        string permittedRef = await fixture.Repository.CommitIndexAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);

        SourceCaptureResult result = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .AcquireAsync(fixture.RepositoryId, await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]));

        Assert.Equal(SourceCaptureStatus.Completed, result.Status);
        Assert.Equal(permittedRef, result.ProfiledRevision);
        Assert.Equal(3, result.EntryCount);

        IDbContextFactory<VulgataDbContext> contextFactory = appFixture.Scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        await using VulgataDbContext context = await contextFactory.CreateDbContextAsync();
        SourceSnapshot snapshot = await context.SourceSnapshots
            .AsNoTracking()
            .SingleAsync(value => value.RepositoryId == fixture.RepositoryId);
        string objectRoot = Path.Combine(
            new[] { fixture.SourceStorageRoot }.Concat(snapshot.InternalStorageKey.Split('/')).ToArray());
        using JsonDocument document = JsonDocument.Parse(
            await File.ReadAllBytesAsync(Path.Combine(objectRoot, "manifest.json")));
        Dictionary<string, JsonElement> entries = document.RootElement
            .GetProperty("entries")
            .EnumerateArray()
            .ToDictionary(
                entry => entry.GetProperty("relativePath").GetString()!,
                StringComparer.Ordinal);

        JsonElement symbolicLink = entries["link"];
        Assert.Equal("symbolicLink", symbolicLink.GetProperty("entryKind").GetString());
        Assert.Equal("120000", symbolicLink.GetProperty("gitMode").GetString());
        Assert.Equal(new[] { "metadata" }, symbolicLink.GetProperty("capabilities")
            .EnumerateArray().Select(value => value.GetString()).ToArray());
        Assert.Equal("source_symbolic_link_unsupported", symbolicLink.GetProperty("limitationCode").GetString());
        Assert.Equal("target.txt".Length, symbolicLink.GetProperty("byteLength").GetInt64());
        Assert.NotEqual(JsonValueKind.Null, symbolicLink.GetProperty("contentSha256").ValueKind);

        JsonElement gitlink = entries["submodule"];
        Assert.Equal("gitlink", gitlink.GetProperty("entryKind").GetString());
        Assert.Equal("160000", gitlink.GetProperty("gitMode").GetString());
        Assert.Equal(new[] { "metadata" }, gitlink.GetProperty("capabilities")
            .EnumerateArray().Select(value => value.GetString()).ToArray());
        Assert.Equal("source_gitlink_unsupported", gitlink.GetProperty("limitationCode").GetString());
        Assert.Equal(JsonValueKind.Null, gitlink.GetProperty("byteLength").ValueKind);
        Assert.Equal(JsonValueKind.Null, gitlink.GetProperty("contentSha256").ValueKind);
        Assert.Equal(JsonValueKind.Null, gitlink.GetProperty("storageBlobKey").ValueKind);
    }

    [Fact]
    public async Task AcquireAsync_ManifestRetainsUnsafeAndInvalidUtf8PathBytesWithoutLogicalTraversal()
    {
        byte[] invalidUtf8Path = [(byte)'b', (byte)'a', (byte)'d', 0xFF, (byte)'n', (byte)'a', (byte)'m', (byte)'e'];
        byte[] unsafePath = Encoding.ASCII.GetBytes("unsafe\\name");
        byte[] invalidUtf8Content = [0x01, 0x02, 0xFF];
        byte[] unsafeContent = Encoding.UTF8.GetBytes("unsafe path fixture\n");
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        string expectedRevision = await fixture.Repository.CommitRawTreeAsync(
            [
                (invalidUtf8Path, invalidUtf8Content),
                (unsafePath, unsafeContent),
            ]);
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);

        SourceCaptureResult result = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .AcquireAsync(fixture.RepositoryId, await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]));

        Assert.Equal(SourceCaptureStatus.Completed, result.Status);
        Assert.Equal(expectedRevision, result.ProfiledRevision);
        Assert.Equal(2, result.EntryCount);
        Assert.Equal(invalidUtf8Content.LongLength + unsafeContent.LongLength, result.TotalBlobBytes);

        IDbContextFactory<VulgataDbContext> contextFactory = appFixture.Scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        await using VulgataDbContext context = await contextFactory.CreateDbContextAsync();
        SourceSnapshot snapshot = await context.SourceSnapshots
            .AsNoTracking()
            .SingleAsync(value => value.RepositoryId == fixture.RepositoryId);
        string objectRoot = Path.Combine(
            new[] { fixture.SourceStorageRoot }.Concat(snapshot.InternalStorageKey.Split('/')).ToArray());
        using JsonDocument document = JsonDocument.Parse(
            await File.ReadAllBytesAsync(Path.Combine(objectRoot, "manifest.json")));
        JsonElement[] manifestEntries = document.RootElement
            .GetProperty("entries")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(2, manifestEntries.Length);

        var expectedEntries = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [Convert.ToBase64String(invalidUtf8Path)] = invalidUtf8Content,
            [Convert.ToBase64String(unsafePath)] = unsafeContent,
        };
        foreach (JsonElement entry in manifestEntries)
        {
            string pathBase64 = entry.GetProperty("pathBytesBase64").GetString()!;
            Assert.True(expectedEntries.TryGetValue(pathBase64, out byte[]? expectedContent));
            Assert.Equal(JsonValueKind.Null, entry.GetProperty("relativePath").ValueKind);
            Assert.Equal("regularFile", entry.GetProperty("entryKind").GetString());
            Assert.Equal("100644", entry.GetProperty("gitMode").GetString());
            Assert.Equal(new[] { "metadata" }, entry.GetProperty("capabilities")
                .EnumerateArray().Select(value => value.GetString()).ToArray());
            Assert.Equal("source_path_unsupported", entry.GetProperty("limitationCode").GetString());
            Assert.Equal(expectedContent!.LongLength, entry.GetProperty("byteLength").GetInt64());

            string contentSha = Convert.ToHexStringLower(SHA256.HashData(expectedContent));
            Assert.Equal(contentSha, entry.GetProperty("contentSha256").GetString());
            Assert.Equal($"blobs/{contentSha}", entry.GetProperty("storageBlobKey").GetString());
            string blobPath = Path.Combine(objectRoot, "blobs", contentSha);
            Assert.Equal(expectedContent, await File.ReadAllBytesAsync(blobPath));
        }
    }
}
