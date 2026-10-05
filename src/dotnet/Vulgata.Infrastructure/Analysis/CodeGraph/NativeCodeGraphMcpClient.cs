using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Analysis.CodeGraph;

/// <summary>
/// Native MCP transport for an already prepared, trusted workspace. This class
/// does not issue source authority or prepare an index; host composition must do
/// both before constructing a production session.
/// </summary>
internal sealed partial class NativeCodeGraphMcpClient : ICodeGraphWorkerToolSession
{
    private const int MaxNativeInvocations = 10;
    private static readonly TimeSpan MaxSessionLifetime = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MaxOperationTimeout = TimeSpan.FromSeconds(30);
    private const int MaxResultBytes = 256 * 1024;
    private const int MaxVerificationFileBytes = 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly Guid _repositoryId;
    private readonly string _commandPath;
    private readonly string _projectPath;
    private readonly PreparedCodeGraphConnection? _connection;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _operationTimeout;
    private readonly DateTimeOffset _expiresAt;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private McpClient? _client;
    private CodeGraphToolReadiness? _readiness;
    private int _nativeInvocationCount;
    private bool _expired;
    private bool _timedOut;
    private bool _disposed;

    internal NativeCodeGraphMcpClient(Guid repositoryId, string commandPath, string projectPath)
        : this(repositoryId, commandPath, projectPath, TimeProvider.System)
    {
    }

    internal NativeCodeGraphMcpClient(PreparedCodeGraphConnection connection)
        : this(
            (connection ?? throw new ArgumentNullException(nameof(connection))).RepositoryId,
            connection.CommandPath,
            connection.WorkingDirectory,
            TimeProvider.System)
    {
        _connection = connection;
    }

    internal NativeCodeGraphMcpClient(
        Guid repositoryId,
        string commandPath,
        string projectPath,
        TimeProvider timeProvider)
        : this(repositoryId, commandPath, projectPath, timeProvider, MaxOperationTimeout)
    {
    }

    internal NativeCodeGraphMcpClient(
        Guid repositoryId,
        string commandPath,
        string projectPath,
        TimeProvider timeProvider,
        TimeSpan operationTimeout)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        if (operationTimeout <= TimeSpan.Zero || operationTimeout > MaxOperationTimeout)
        {
            throw new ArgumentOutOfRangeException(nameof(operationTimeout));
        }

        _operationTimeout = operationTimeout;
        if (repositoryId == Guid.Empty)
        {
            throw new ArgumentException("A Repository binding is required.", nameof(repositoryId));
        }

        if (string.IsNullOrWhiteSpace(commandPath) || !Path.IsPathFullyQualified(commandPath))
        {
            throw new ArgumentException("A declared absolute CodeGraph command is required.", nameof(commandPath));
        }

        if (string.IsNullOrWhiteSpace(projectPath) || !Path.IsPathFullyQualified(projectPath))
        {
            throw new ArgumentException("A trusted absolute tool workspace is required.", nameof(projectPath));
        }

        _repositoryId = repositoryId;
        _commandPath = Path.GetFullPath(commandPath);
        _projectPath = Path.GetFullPath(projectPath);
        _expiresAt = _timeProvider.GetUtcNow() + MaxSessionLifetime;
    }

    public async Task<CodeGraphToolReadiness> DiscoverAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_disposed)
            {
                return Failure(CodeGraphToolReadinessStatus.Expired, CodeGraphToolStage.Discovery,
                    CodeGraphToolIssueCode.SessionExpired);
            }

            if (_expired || _timeProvider.GetUtcNow() >= _expiresAt)
            {
                await ExpireAsync();
                return _readiness!;
            }

            if (_readiness is not null)
            {
                return _readiness;
            }

            if (!File.Exists(_commandPath))
            {
                return _readiness = Failure(CodeGraphToolReadinessStatus.Unavailable,
                    CodeGraphToolStage.Preparation, CodeGraphToolIssueCode.InstallationUnavailable);
            }

            if (!Directory.Exists(Path.Combine(_projectPath, ".codegraph")))
            {
                return _readiness = Failure(CodeGraphToolReadinessStatus.Unprepared,
                    CodeGraphToolStage.Preparation, CodeGraphToolIssueCode.SourceUnprepared);
            }

            using var connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectionCancellation.CancelAfter(_operationTimeout);
            long connectionStartedAt = Stopwatch.GetTimestamp();
            try
            {
                _client = await McpClient.CreateAsync(CreateTransport(),
                    cancellationToken: connectionCancellation.Token);
                if (cancellationToken.IsCancellationRequested ||
                    connectionCancellation.IsCancellationRequested ||
                    Stopwatch.GetElapsedTime(connectionStartedAt) >= _operationTimeout)
                {
                    await _client.DisposeAsync();
                    _client = null;
                    return _readiness = cancellationToken.IsCancellationRequested
                        ? Failure(CodeGraphToolReadinessStatus.Cancelled,
                            CodeGraphToolStage.Connection, CodeGraphToolIssueCode.Cancelled)
                        : Failure(CodeGraphToolReadinessStatus.ConnectionFailed,
                            CodeGraphToolStage.Connection, CodeGraphToolIssueCode.DeadlineExceeded);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return _readiness = Failure(CodeGraphToolReadinessStatus.Cancelled,
                    CodeGraphToolStage.Connection, CodeGraphToolIssueCode.Cancelled);
            }
            catch (Exception) when (connectionCancellation.IsCancellationRequested &&
                !cancellationToken.IsCancellationRequested)
            {
                return _readiness = Failure(CodeGraphToolReadinessStatus.ConnectionFailed,
                    CodeGraphToolStage.Connection, CodeGraphToolIssueCode.DeadlineExceeded);
            }
            catch (Exception)
            {
                return _readiness = Failure(CodeGraphToolReadinessStatus.ConnectionFailed,
                    CodeGraphToolStage.Connection, CodeGraphToolIssueCode.ConnectionFailed);
            }

            using var discoveryCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            discoveryCancellation.CancelAfter(_operationTimeout);
            long discoveryStartedAt = Stopwatch.GetTimestamp();
            try
            {
                if (_timeProvider.GetUtcNow() >= _expiresAt)
                {
                    await ExpireAsync();
                    return _readiness!;
                }

                IList<McpClientTool> tools = await _client.ListToolsAsync(
                    cancellationToken: discoveryCancellation.Token);
                if (cancellationToken.IsCancellationRequested)
                {
                    return _readiness = Failure(CodeGraphToolReadinessStatus.Cancelled,
                        CodeGraphToolStage.Discovery, CodeGraphToolIssueCode.Cancelled);
                }

                if (discoveryCancellation.IsCancellationRequested ||
                    Stopwatch.GetElapsedTime(discoveryStartedAt) >= _operationTimeout)
                {
                    return _readiness = Failure(CodeGraphToolReadinessStatus.DiscoveryFailed,
                        CodeGraphToolStage.Discovery, CodeGraphToolIssueCode.DeadlineExceeded);
                }

                if (tools.Count != 1 || tools[0].Name != "codegraph_explore")
                {
                    return _readiness = Failure(CodeGraphToolReadinessStatus.DiscoveryFailed,
                        CodeGraphToolStage.Discovery, CodeGraphToolIssueCode.ToolMissing);
                }

                if (!IsReviewedSchema(tools[0].ProtocolTool.InputSchema))
                {
                    return _readiness = Failure(CodeGraphToolReadinessStatus.DiscoveryFailed,
                        CodeGraphToolStage.Discovery, CodeGraphToolIssueCode.SchemaChanged);
                }

                if (_timeProvider.GetUtcNow() >= _expiresAt)
                {
                    await ExpireAsync();
                    return _readiness!;
                }

                return _readiness = new CodeGraphToolReadiness(
                    CodeGraphToolReadinessStatus.Ready, _repositoryId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return _readiness = Failure(CodeGraphToolReadinessStatus.Cancelled,
                    CodeGraphToolStage.Discovery, CodeGraphToolIssueCode.Cancelled);
            }
            catch (Exception) when (discoveryCancellation.IsCancellationRequested &&
                !cancellationToken.IsCancellationRequested)
            {
                return _readiness = Failure(CodeGraphToolReadinessStatus.DiscoveryFailed,
                    CodeGraphToolStage.Discovery, CodeGraphToolIssueCode.DeadlineExceeded);
            }
            catch (Exception)
            {
                return _readiness = Failure(CodeGraphToolReadinessStatus.DiscoveryFailed,
                    CodeGraphToolStage.Discovery, CodeGraphToolIssueCode.ToolMissing);
            }
            finally
            {
                if (_readiness is { IsReady: false } && _client is not null)
                {
                    await _client.DisposeAsync();
                    _client = null;
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CodeGraphToolInvocationResult> ExploreAsync(
        string query,
        int maxFiles,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_disposed)
            {
                return Outcome(CodeGraphToolInvocationStatus.Unavailable,
                    CodeGraphToolIssueCode.SessionExpired);
            }

            if (_expired || _timeProvider.GetUtcNow() >= _expiresAt)
            {
                await ExpireAsync();
                return Outcome(CodeGraphToolInvocationStatus.Expired,
                    CodeGraphToolIssueCode.SessionExpired);
            }

            if (_timedOut)
            {
                return Outcome(CodeGraphToolInvocationStatus.TimedOut,
                    CodeGraphToolIssueCode.DeadlineExceeded);
            }

            if (_readiness?.IsReady != true || _client is null)
            {
                return Outcome(CodeGraphToolInvocationStatus.Unavailable,
                    _readiness?.IssueCode ?? CodeGraphToolIssueCode.SourceUnprepared);
            }

            if (string.IsNullOrWhiteSpace(query) || maxFiles is < 1 or > 12 ||
                !WithinQueryLimit(query))
            {
                return Outcome(CodeGraphToolInvocationStatus.Denied);
            }

            if (_nativeInvocationCount >= MaxNativeInvocations)
            {
                return Outcome(CodeGraphToolInvocationStatus.Limited,
                    CodeGraphToolIssueCode.ResultLimit);
            }

            _nativeInvocationCount++;

            using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            operationCancellation.CancelAfter(_operationTimeout);
            long startedAt = Stopwatch.GetTimestamp();
            try
            {
                var arguments = new Dictionary<string, object?>
                {
                    ["query"] = query,
                    ["maxFiles"] = maxFiles,
                    ["projectPath"] = _projectPath,
                };
                CallToolResult result = await _client.CallToolAsync(
                    "codegraph_explore", arguments,
                    cancellationToken: operationCancellation.Token);
                if (cancellationToken.IsCancellationRequested)
                {
                    return Outcome(CodeGraphToolInvocationStatus.Cancelled,
                        CodeGraphToolIssueCode.Cancelled);
                }

                if (operationCancellation.IsCancellationRequested ||
                    Stopwatch.GetElapsedTime(startedAt) >= _operationTimeout)
                {
                    await TimeoutAsync();
                    return Outcome(CodeGraphToolInvocationStatus.TimedOut,
                        CodeGraphToolIssueCode.DeadlineExceeded);
                }

                if (_timeProvider.GetUtcNow() >= _expiresAt)
                {
                    await ExpireAsync();
                    return Outcome(CodeGraphToolInvocationStatus.Expired,
                        CodeGraphToolIssueCode.SessionExpired);
                }

                if (result.IsError == true)
                {
                    return Outcome(CodeGraphToolInvocationStatus.Failed,
                        CodeGraphToolIssueCode.InvocationFailed);
                }

                return ParseBoundedResult(result, query);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Outcome(CodeGraphToolInvocationStatus.Cancelled,
                    CodeGraphToolIssueCode.Cancelled);
            }
            catch (OperationCanceledException) when (operationCancellation.IsCancellationRequested)
            {
                await TimeoutAsync();
                return Outcome(CodeGraphToolInvocationStatus.TimedOut,
                    CodeGraphToolIssueCode.DeadlineExceeded);
            }
            catch (Exception) when (operationCancellation.IsCancellationRequested &&
                !cancellationToken.IsCancellationRequested)
            {
                await TimeoutAsync();
                return Outcome(CodeGraphToolInvocationStatus.TimedOut,
                    CodeGraphToolIssueCode.DeadlineExceeded);
            }
            catch (Exception)
            {
                return Outcome(CodeGraphToolInvocationStatus.Failed,
                    CodeGraphToolIssueCode.InvocationFailed);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_client is not null)
            {
                await _client.DisposeAsync();
                _client = null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ExpireAsync()
    {
        _expired = true;
        _readiness = Failure(CodeGraphToolReadinessStatus.Expired,
            CodeGraphToolStage.Discovery, CodeGraphToolIssueCode.SessionExpired);
        if (_client is not null)
        {
            await _client.DisposeAsync();
            _client = null;
        }
    }

    private async Task TimeoutAsync()
    {
        _timedOut = true;
        _readiness = Failure(CodeGraphToolReadinessStatus.Unavailable,
            CodeGraphToolStage.Invocation, CodeGraphToolIssueCode.DeadlineExceeded);
        if (_client is not null)
        {
            await _client.DisposeAsync();
            _client = null;
        }
    }

    private StdioClientTransport CreateTransport()
    {
        return new StdioClientTransport(CreateTransportOptions());
    }

    internal StdioClientTransportOptions CreateTransportOptions()
    {
        if (_connection is not null)
        {
            return new StdioClientTransportOptions
            {
                Name = "prepared-codegraph",
                Command = _connection.Command,
                Arguments = _connection.Arguments.ToArray(),
                WorkingDirectory = _connection.WorkingDirectory,
                InheritEnvironmentVariables = false,
                EnvironmentVariables = new Dictionary<string, string>(_connection.Environment),
            };
        }

        // Historical fixture callers retain their independently constructed route.
        string command = _commandPath;
        string[] arguments = ["serve", "--mcp", "-p", _projectPath, "--no-watch"];
        if (Path.GetExtension(_commandPath).Equals(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            command = "pwsh";
            arguments = ["-NoProfile", "-File", _commandPath, .. arguments];
        }

        var environment = StdioClientTransportOptions.GetDefaultEnvironmentVariables();
        environment["CODEGRAPH_NO_DAEMON"] = "1";
        environment["CODEGRAPH_TELEMETRY"] = "0";
        environment["DO_NOT_TRACK"] = "1";
        return new StdioClientTransportOptions
        {
            Name = "prepared-codegraph",
            Command = command,
            Arguments = arguments,
            WorkingDirectory = _projectPath,
            InheritEnvironmentVariables = false,
            EnvironmentVariables = environment,
        };
    }

    private static bool IsReviewedSchema(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object ||
            !schema.TryGetProperty("type", out JsonElement type) || type.GetString() != "object" ||
            !schema.TryGetProperty("properties", out JsonElement properties) ||
            properties.ValueKind != JsonValueKind.Object ||
            properties.EnumerateObject().Count() != 3 ||
            !HasPropertyType(properties, "query", "string") ||
            !HasPropertyType(properties, "maxFiles", "number") ||
            !HasPropertyType(properties, "projectPath", "string") ||
            !schema.TryGetProperty("required", out JsonElement required) ||
            required.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        string?[] requiredNames = required.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : null)
            .ToArray();
        return requiredNames.Length == 1 && requiredNames[0] == "query";
    }

    private static bool HasPropertyType(JsonElement properties, string name, string expectedType) =>
        properties.TryGetProperty(name, out JsonElement property) &&
        property.ValueKind == JsonValueKind.Object &&
        property.TryGetProperty("type", out JsonElement type) &&
        type.ValueKind == JsonValueKind.String && type.GetString() == expectedType;

    private CodeGraphToolInvocationResult ParseBoundedResult(CallToolResult result, string query)
    {
        if (result.Content.Count != 1 || result.Content[0] is not TextContentBlock textBlock ||
            Encoding.UTF8.GetByteCount(textBlock.Text) > MaxResultBytes)
        {
            return Outcome(CodeGraphToolInvocationStatus.Limited, CodeGraphToolIssueCode.ResultLimit);
        }

        string text = textBlock.Text;
        if (text == $"No relevant code found for \"{query}\"")
        {
            return Outcome(CodeGraphToolInvocationStatus.NoMatch);
        }

        if (!text.Contains("**Source Code**", StringComparison.Ordinal))
        {
            return Outcome(CodeGraphToolInvocationStatus.Limited,
                CodeGraphToolIssueCode.IncompleteIndex);
        }

        var excerpts = new List<CodeGraphSourceExcerpt>();
        string sourceSection = text[(text.IndexOf("**Source Code**", StringComparison.Ordinal) + 15)..];
        foreach (Match match in SourceBlockRegex().Matches(sourceSection))
        {
            string relativePath = match.Groups["path"].Value;
            if (!TryContainedPath(relativePath, out string fullPath))
            {
                return Outcome(CodeGraphToolInvocationStatus.Denied,
                    CodeGraphToolIssueCode.UncontainedSource);
            }

            string source = StripLineNumbers(match.Groups["source"].Value);
            if (!File.Exists(fullPath) || new FileInfo(fullPath).Length > MaxVerificationFileBytes ||
                !File.ReadAllText(fullPath).Replace("\r\n", "\n", StringComparison.Ordinal).Contains(source,
                StringComparison.Ordinal))
            {
                return Outcome(CodeGraphToolInvocationStatus.Limited,
                    CodeGraphToolIssueCode.IncompleteIndex);
            }

            excerpts.Add(new CodeGraphSourceExcerpt(
                new PreparedSourceLocation(_repositoryId, relativePath.Replace('\\', '/')),
                source));
        }

        if (excerpts.Count == 0)
        {
            return Outcome(CodeGraphToolInvocationStatus.Limited,
                CodeGraphToolIssueCode.IncompleteIndex);
        }

        return new CodeGraphToolInvocationResult
        {
            Status = CodeGraphToolInvocationStatus.Complete,
            RepositoryId = _repositoryId,
            Locations = excerpts.Select(excerpt => excerpt.Location).ToArray(),
            Excerpts = excerpts,
        };
    }

    private bool TryContainedPath(string relativePath, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) ||
            relativePath.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }

        string candidate = Path.GetFullPath(Path.Combine(_projectPath, relativePath));
        string relative = Path.GetRelativePath(_projectPath, candidate);
        if (relative == "." || relative == ".." ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return false;
        }

        string current = _projectPath;
        foreach (string segment in relative.Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }
        }

        fullPath = candidate;
        return true;
    }

    private static string StripLineNumbers(string text) =>
        string.Join("\n", text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\r')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => Regex.Replace(line, "^\\d+\\t", string.Empty)));

    private static bool WithinQueryLimit(string query)
    {
        try
        {
            return StrictUtf8.GetByteCount(query) <= 512;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    private CodeGraphToolReadiness Failure(
        CodeGraphToolReadinessStatus status,
        CodeGraphToolStage stage,
        CodeGraphToolIssueCode issue) =>
        new(status, _repositoryId, stage, issue);

    private CodeGraphToolInvocationResult Outcome(
        CodeGraphToolInvocationStatus status,
        CodeGraphToolIssueCode issue = CodeGraphToolIssueCode.None) => new()
        {
            Status = status,
            RepositoryId = _repositoryId,
            IssueCode = issue,
            Limitations = issue == CodeGraphToolIssueCode.None ? [] : [issue],
        };

    [GeneratedRegex(@"\*\*`(?<path>[^`]+)`\*\*[^\n]*\n\s*```[^\n]*\n(?<source>[\s\S]*?)\n```", RegexOptions.CultureInvariant)]
    private static partial Regex SourceBlockRegex();
}
