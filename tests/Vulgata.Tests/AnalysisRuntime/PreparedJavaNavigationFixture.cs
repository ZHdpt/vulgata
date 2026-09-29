using System.Text;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.AnalysisRuntime;

/// <summary>
/// Owns a tiny Java Repository, a separate foreign root, an escaping directory
/// reparse point, and disposable JDT tool state. Creating this fixture never
/// starts Java or JDT-LS.
/// </summary>
internal sealed class PreparedJavaNavigationFixture : IAsyncDisposable
{
    private const string SandboxPrefix = "vulgata-story182-navigation-";
    private const string ForeignPrefix = "vulgata-story182-foreign-";
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _sandboxPath;
    private readonly string _foreignRootPath;
    private bool _disposed;

    private PreparedJavaNavigationFixture(string sandboxPath, string foreignRootPath, Guid fixtureId)
    {
        _sandboxPath = sandboxPath;
        _foreignRootPath = foreignRootPath;
        FixtureId = fixtureId;

        RepositoryRoot = Path.Combine(sandboxPath, "repository");
        ToolStateRoot = Path.Combine(sandboxPath, "jdt-state");
        ToolDataPath = Path.Combine(ToolStateRoot, "data");
        ToolConfigurationPath = Path.Combine(ToolStateRoot, "configuration");
        EscapeLinkPath = Path.Combine(
            RepositoryRoot,
            "src",
            "main",
            "java",
            "example",
            "navigation",
            "foreign");
        DeclarationPath = Path.Combine(
            RepositoryRoot,
            "src",
            "main",
            "java",
            "example",
            "navigation",
            "Greeting.java");
        CallerPath = Path.Combine(
            RepositoryRoot,
            "src",
            "main",
            "java",
            "example",
            "navigation",
            "GreetingCaller.java");
        ForeignSentinelPath = Path.Combine(foreignRootPath, "host-only.txt");
    }

    internal Guid FixtureId { get; }

    internal string RepositoryRoot { get; }

    internal string ToolStateRoot { get; }

    internal string ToolDataPath { get; }

    internal string ToolConfigurationPath { get; }

    internal string EscapeLinkPath { get; }

    internal string DeclarationPath { get; }

    internal string CallerPath { get; }

    internal string ForeignRootPath => _foreignRootPath;

    internal string ForeignSentinelPath { get; }

    internal static async Task<PreparedJavaNavigationFixture> CreateAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Guid fixtureId = Guid.NewGuid();
        string tempRoot = Path.GetFullPath(Path.GetTempPath());
        string sandboxPath = Path.Combine(tempRoot, SandboxPrefix + fixtureId.ToString("N"));
        string foreignRootPath = Path.Combine(tempRoot, ForeignPrefix + fixtureId.ToString("N"));
        var fixture = new PreparedJavaNavigationFixture(sandboxPath, foreignRootPath, fixtureId);

        try
        {
            Directory.CreateDirectory(fixture.RepositoryRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(fixture.DeclarationPath)!);
            Directory.CreateDirectory(fixture.ToolDataPath);
            Directory.CreateDirectory(fixture.ToolConfigurationPath);
            Directory.CreateDirectory(fixture.ForeignRootPath);

            await File.WriteAllTextAsync(
                fixture.DeclarationPath,
                "package example.navigation;\n\n" +
                "public interface Greeting {\n" +
                "    String message();\n" +
                "}\n",
                Utf8,
                cancellationToken);
            await File.WriteAllTextAsync(
                fixture.CallerPath,
                "package example.navigation;\n\n" +
                "public final class GreetingCaller {\n" +
                "    public String render(Greeting greeting) {\n" +
                "        return greeting.message();\n" +
                "    }\n" +
                "}\n",
                Utf8,
                cancellationToken);
            await File.WriteAllTextAsync(
                fixture.ForeignSentinelPath,
                "outside the prepared Repository",
                Utf8,
                cancellationToken);

            try
            {
                Directory.CreateSymbolicLink(fixture.EscapeLinkPath, fixture.ForeignRootPath);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new InvalidOperationException(
                    "The fixture requires permission to create its owned directory reparse point.",
                    exception);
            }
            catch (PlatformNotSupportedException exception)
            {
                throw new InvalidOperationException(
                    "The fixture requires directory reparse-point support on this platform.",
                    exception);
            }
            catch (IOException exception) when (
                exception.HResult is unchecked((int)0x80070522) or unchecked((int)0x80070005))
            {
                throw new InvalidOperationException(
                    "The fixture requires permission to create its owned directory reparse point.",
                    exception);
            }

            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    internal bool IsOwnedPath(string path)
    {
        string candidate = Path.GetFullPath(path);
        return IsWithin(candidate, _sandboxPath) || IsWithin(candidate, _foreignRootPath);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        string tempRoot = Path.GetFullPath(Path.GetTempPath());
        string expectedSandbox = Path.Combine(tempRoot, SandboxPrefix + FixtureId.ToString("N"));
        string expectedForeign = Path.Combine(tempRoot, ForeignPrefix + FixtureId.ToString("N"));
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!string.Equals(Path.GetFullPath(_sandboxPath), expectedSandbox, comparison) ||
            !string.Equals(Path.GetFullPath(_foreignRootPath), expectedForeign, comparison))
        {
            throw new InvalidOperationException("Navigation fixture cleanup target is not owned.");
        }

        DeleteOwnedTreeWithoutFollowingLinks(_sandboxPath);
        DeleteOwnedTreeWithoutFollowingLinks(_foreignRootPath);
        _disposed = true;
        return ValueTask.CompletedTask;
    }

    private static bool IsWithin(string candidate, string root)
    {
        string normalizedRoot = Path.GetFullPath(root).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(candidate, normalizedRoot, comparison) ||
            candidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, comparison);
    }

    private static void DeleteOwnedTreeWithoutFollowingLinks(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        if ((File.GetAttributes(directoryPath) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("Fixture cleanup encountered a reparse root.");
        }

        foreach (string entryPath in Directory.EnumerateFileSystemEntries(directoryPath))
        {
            FileAttributes attributes = File.GetAttributes(entryPath);
            bool isDirectory = (attributes & FileAttributes.Directory) != 0;
            bool isReparsePoint = (attributes & FileAttributes.ReparsePoint) != 0;

            if (isDirectory && !isReparsePoint)
            {
                DeleteOwnedTreeWithoutFollowingLinks(entryPath);
            }
            else if (isDirectory)
            {
                Directory.Delete(entryPath);
            }
            else
            {
                File.Delete(entryPath);
            }
        }

        Directory.Delete(directoryPath);
    }
}

public sealed class PreparedJavaNavigationFixtureTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CreateAsync_PreparesJavaRepositoryAndIsolatedRoots_DisposalRemovesOwnedTrees()
    {
        PreparedJavaNavigationFixture fixture =
            await PreparedJavaNavigationFixture.CreateAsync();
        string repositoryRoot = fixture.RepositoryRoot;
        string toolStateRoot = fixture.ToolStateRoot;
        string foreignRoot = fixture.ForeignRootPath;
        string declarationPath = fixture.DeclarationPath;
        string callerPath = fixture.CallerPath;
        string escapeLinkPath = fixture.EscapeLinkPath;
        string foreignSentinelPath = fixture.ForeignSentinelPath;
        string unrelatedPath = Path.GetFullPath(Path.GetTempPath());

        try
        {
            Assert.True(fixture.IsOwnedPath(repositoryRoot));
            Assert.True(fixture.IsOwnedPath(foreignRoot));
            Assert.False(fixture.IsOwnedPath(unrelatedPath));
            Assert.NotEqual(
                Path.GetFullPath(repositoryRoot),
                Path.GetFullPath(toolStateRoot));
            Assert.True(Directory.Exists(fixture.ToolDataPath));
            Assert.True(Directory.Exists(fixture.ToolConfigurationPath));

            string declaration = await File.ReadAllTextAsync(declarationPath);
            string caller = await File.ReadAllTextAsync(callerPath);
            Assert.Contains("public interface Greeting", declaration, StringComparison.Ordinal);
            Assert.Contains("String message();", declaration, StringComparison.Ordinal);
            Assert.Contains("Greeting greeting", caller, StringComparison.Ordinal);
            Assert.Contains("greeting.message()", caller, StringComparison.Ordinal);
            Assert.Equal("outside the prepared Repository", await File.ReadAllTextAsync(foreignSentinelPath));

            FileAttributes linkAttributes = File.GetAttributes(escapeLinkPath);
            Assert.True(
                (linkAttributes & FileAttributes.ReparsePoint) != 0,
                "The escape path must be a reparse point, not a copied foreign directory.");
            Assert.Equal(
                "outside the prepared Repository",
                await File.ReadAllTextAsync(Path.Combine(escapeLinkPath, "host-only.txt")));
        }
        finally
        {
            await fixture.DisposeAsync();
        }

        Assert.False(Directory.Exists(repositoryRoot));
        Assert.False(Directory.Exists(toolStateRoot));
        Assert.False(Directory.Exists(foreignRoot));
        Assert.False(File.Exists(foreignSentinelPath));
    }
}
