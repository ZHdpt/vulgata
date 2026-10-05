using System.Reflection;
using ModelContextProtocol.Client;
using Vulgata.Infrastructure.Analysis.CodeGraph;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.AnalysisRuntime;

public sealed class PreparedCodeGraphConnectionTests
{
    private static readonly Guid RepositoryId =
        Guid.Parse("18300000-0000-4000-8000-000000000184");

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PrepareAsync_SelectedOwnedFixture_ReturnsFixedDescriptorAndKeepsSourceUnchanged()
    {
        string parent = CreateOwnedParent();
        try
        {
            string root = Path.Combine(parent, "prepared");
            Directory.CreateDirectory(root);
            string source = Path.Combine(root, "Beacon.cs");
            string config = Path.Combine(root, "codegraph.json");
            string hooks = Path.Combine(root, ".git", "hooks");
            Directory.CreateDirectory(hooks);
            string hook = Path.Combine(hooks, "post-commit");
            await File.WriteAllTextAsync(source, "public class Beacon {}\n");
            await File.WriteAllTextAsync(config, "fixture configuration\n");
            await File.WriteAllTextAsync(hook, "fixture hook\n");
            string commandPath = CreateDummyCommand(parent);
            var runner = new RecordingInitRunner();
            var factory = new PreparedCodeGraphConnectionFactory(
                commandPath, runner, static (_, _) => Task.CompletedTask);

            await using (PreparedCodeGraphConnection connection = await factory.PrepareAsync(
                RepositoryId, root, CancellationToken.None))
            {
                Assert.Equal(RepositoryId, connection.RepositoryId);
                Assert.Equal(Path.GetFullPath(root), connection.WorkingDirectory);
                Assert.Equal("pwsh", connection.Command);
                Assert.Equal(
                    ["-NoProfile", "-File", Path.GetFullPath(commandPath),
                     "serve", "--mcp", "-p", Path.GetFullPath(root), "--no-watch"],
                    connection.Arguments);
                Assert.Equal("1", connection.Environment["CODEGRAPH_NO_DAEMON"]);
                Assert.Equal("1", connection.Environment["CODEGRAPH_NO_WATCH"]);
                Assert.Equal("1", connection.Environment["DO_NOT_TRACK"]);
                Assert.Equal("0", connection.Environment["CODEGRAPH_TELEMETRY"]);
                Assert.Equal("1", connection.Environment["CODEGRAPH_NO_UPDATE_CHECK"]);
                Assert.Equal("1", connection.Environment["CODEGRAPH_NO_DOWNLOAD"]);

                CodeGraphProcessRequest request = Assert.Single(runner.Requests);
                Assert.Equal(Path.GetFullPath(commandPath), request.FileName);
                Assert.Equal(["init", Path.GetFullPath(root)], request.Arguments);
                Assert.Equal(Path.GetFullPath(root), request.WorkingDirectory);
                Assert.True(request.CloseStandardInput);
                Assert.False(request.InheritEnvironmentVariables);
                Assert.Equal("1", request.EnvironmentVariables["CODEGRAPH_NO_DOWNLOAD"]);
                Assert.True(Directory.Exists(Path.Combine(root, ".codegraph")));
                Assert.Equal("public class Beacon {}\n", await File.ReadAllTextAsync(source));
                Assert.Equal("fixture configuration\n", await File.ReadAllTextAsync(config));
                Assert.Equal("fixture hook\n", await File.ReadAllTextAsync(hook));
            }

            Assert.True(Directory.Exists(Path.Combine(root, ".codegraph")),
                "Disposing one session must not delete the prepared index.");
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PrepareAsync_PreExistingState_RejectsWithoutStartingOrDeletingIt()
    {
        string parent = CreateOwnedParent();
        try
        {
            string root = Path.Combine(parent, "prepared");
            string state = Path.Combine(root, ".codegraph");
            Directory.CreateDirectory(state);
            string marker = Path.Combine(state, "foreign.txt");
            await File.WriteAllTextAsync(marker, "foreign owner");
            var runner = new RecordingInitRunner();
            var factory = new PreparedCodeGraphConnectionFactory(
                CreateDummyCommand(parent), runner, static (_, _) => Task.CompletedTask);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                factory.PrepareAsync(RepositoryId, root, CancellationToken.None));

            Assert.Empty(runner.Requests);
            Assert.Equal("foreign owner", await File.ReadAllTextAsync(marker));
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PrepareAsync_ActiveOwner_RejectsConcurrentPreparation()
    {
        string parent = CreateOwnedParent();
        try
        {
            string root = Path.Combine(parent, "prepared");
            Directory.CreateDirectory(root);
            var runner = new RecordingInitRunner();
            var factory = new PreparedCodeGraphConnectionFactory(
                CreateDummyCommand(parent), runner, static (_, _) => Task.CompletedTask);
            await using PreparedCodeGraphConnection first = await factory.PrepareAsync(
                RepositoryId, root, CancellationToken.None);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                factory.PrepareAsync(RepositoryId, root, CancellationToken.None));

            Assert.Single(runner.Requests);
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PrepareAsync_RelativeRoot_RejectsBeforeRunner()
    {
        string parent = CreateOwnedParent();
        try
        {
            var runner = new RecordingInitRunner();
            var factory = new PreparedCodeGraphConnectionFactory(
                CreateDummyCommand(parent), runner, static (_, _) => Task.CompletedTask);
            await Assert.ThrowsAsync<ArgumentException>(() =>
                factory.PrepareAsync(RepositoryId, "relative-project", CancellationToken.None));
            Assert.Empty(runner.Requests);
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PrepareAsync_CancelledInit_DoesNotReturnDescriptorOrDeleteState()
    {
        string parent = CreateOwnedParent();
        try
        {
            string root = Path.Combine(parent, "prepared");
            Directory.CreateDirectory(root);
            var runner = new RecordingInitRunner { CancelOnRun = true };
            var factory = new PreparedCodeGraphConnectionFactory(
                CreateDummyCommand(parent), runner, static (_, _) => Task.CompletedTask);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                factory.PrepareAsync(RepositoryId, root, CancellationToken.None));

            Assert.Single(runner.Requests);
            Assert.True(Directory.Exists(Path.Combine(root, ".codegraph")),
                "An interrupted attempt must not silently reclaim tool state.");
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PrepareAsync_ExitZeroWithoutIndex_RejectsFalseReadiness()
    {
        string parent = CreateOwnedParent();
        try
        {
            string root = Path.Combine(parent, "prepared");
            Directory.CreateDirectory(root);
            var runner = new RecordingInitRunner { SkipIndexWrite = true };
            var factory = new PreparedCodeGraphConnectionFactory(
                CreateDummyCommand(parent), runner, static (_, _) => Task.CompletedTask);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                factory.PrepareAsync(RepositoryId, root, CancellationToken.None));

            Assert.Single(runner.Requests);
            Assert.True(Directory.Exists(Path.Combine(root, ".codegraph")),
                "Uncertain state is retained for owner disposition, not silently deleted.");
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task NativeClient_ReturnedDescriptor_UsesExactStdioFieldsWithoutReassembly()
    {
        string parent = CreateOwnedParent();
        try
        {
            string root = Path.Combine(parent, "prepared");
            Directory.CreateDirectory(root);
            var factory = new PreparedCodeGraphConnectionFactory(
                CreateDummyCommand(parent), new RecordingInitRunner(),
                static (_, _) => Task.CompletedTask);
            await using PreparedCodeGraphConnection returned = await factory.PrepareAsync(
                RepositoryId, root, CancellationToken.None);

            await AssertTransportFieldsAsync(returned);

            // A sentinel value distinguishes descriptor consumption from a launch
            // reconstructed from the old command-path/project-path constructor.
            var environment = new Dictionary<string, string>(returned.Environment)
            {
                ["CODEGRAPH_TEST_SENTINEL"] = "returned-only",
            };
            await using PreparedCodeGraphConnection sentinel = new(
                RepositoryId,
                returned.CommandPath,
                "descriptor-only-command",
                ["descriptor-only-argument"],
                root,
                environment,
                static () => { });
            await AssertTransportFieldsAsync(sentinel);
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    private static async Task AssertTransportFieldsAsync(PreparedCodeGraphConnection connection)
    {
        Assert.NotNull(typeof(PreparedCodeGraphConnection).GetMethod(
            "OpenSession", BindingFlags.Instance | BindingFlags.Public));
        ConstructorInfo? constructor = typeof(NativeCodeGraphMcpClient).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(PreparedCodeGraphConnection)],
            modifiers: null);
        Assert.NotNull(constructor);
        await using NativeCodeGraphMcpClient client =
            (NativeCodeGraphMcpClient)constructor.Invoke([connection]);
        MethodInfo? optionsMethod = typeof(NativeCodeGraphMcpClient).GetMethod(
            "CreateTransportOptions", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(optionsMethod);
        StdioClientTransportOptions options = Assert.IsType<StdioClientTransportOptions>(
            optionsMethod.Invoke(client, null));

        Assert.Equal(connection.Command, options.Command);
        Assert.Equal(connection.Arguments, options.Arguments);
        Assert.Equal(connection.WorkingDirectory, options.WorkingDirectory);
        foreach ((string name, string value) in connection.Environment)
        {
            Assert.Equal(value, options.EnvironmentVariables[name]);
        }

        Assert.False(options.InheritEnvironmentVariables);
    }


    private static string CreateOwnedParent()
    {
        string parent = Path.Combine(
            Path.GetTempPath(), "vulgata-18-3-connection-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        return parent;
    }

    private static string CreateDummyCommand(string parent)
    {
        string path = Path.Combine(parent, "installation", "codegraph.ps1");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "# owned test command, never executed\n");
        return path;
    }

    private sealed class RecordingInitRunner : ICodeGraphProcessRunner
    {
        public List<CodeGraphProcessRequest> Requests { get; } = [];
        public bool CancelOnRun { get; init; }
        public bool SkipIndexWrite { get; init; }

        public Task<CodeGraphProcessResult> RunAsync(
            CodeGraphProcessRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            if (CancelOnRun)
            {
                throw new OperationCanceledException("Owned test cancellation", cancellationToken);
            }

            if (!SkipIndexWrite)
            {
                File.WriteAllText(
                    Path.Combine(request.WorkingDirectory, ".codegraph", "codegraph.db"),
                    "owned test index");
            }
            return Task.FromResult(new CodeGraphProcessResult(0, [], [], TimedOut: false));
        }
    }
}
