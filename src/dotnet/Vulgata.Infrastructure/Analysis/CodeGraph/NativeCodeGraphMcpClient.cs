using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Source;

namespace Vulgata.Infrastructure.Analysis.CodeGraph;

internal interface ICodeGraphMcpTransport : IAsyncDisposable
{
    Task<ClientCompletionDetails>? Completion => null;
    Task<IReadOnlyList<Tool>> ListToolsAsync(CancellationToken cancellationToken);
    Task<CallToolResult> CallToolAsync(
        IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken);
}

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
    private readonly long _sessionStartedAt;
    private bool SessionExpired => _timeProvider.GetElapsedTime(_sessionStartedAt) >= MaxSessionLifetime;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ICodeGraphMcpTransport? _client;
    private readonly Func<CancellationToken, Task<PreparedSourceIssueResult>>? _openRead;
    private readonly Func<CancellationToken, Task<ICodeGraphMcpTransport>>? _connect;
    private CodeGraphToolReadiness? _readiness;
    private int _nativeInvocationCount;
    private bool _expired;
    private bool _timedOut;
    private bool _disposed;

    internal Task<ClientCompletionDetails>? Completion => _client?.Completion;

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
        PreparedCodeGraphConnection connection,
        Func<CancellationToken, Task<PreparedSourceIssueResult>> openRead)
        : this(connection)
    {
        _openRead = openRead ?? throw new ArgumentNullException(nameof(openRead));
    }

    internal NativeCodeGraphMcpClient(
        PreparedCodeGraphConnection connection,
        TimeProvider timeProvider,
        Func<CancellationToken, Task<PreparedSourceIssueResult>> openRead,
        Func<CancellationToken, Task<ICodeGraphMcpTransport>> connect,
        TimeSpan? operationTimeout = null)
        : this(connection.RepositoryId, connection.CommandPath, connection.WorkingDirectory,
            timeProvider, operationTimeout ?? MaxOperationTimeout)
    {
        _connection = connection;
        _openRead = openRead ?? throw new ArgumentNullException(nameof(openRead));
        _connect = connect ?? throw new ArgumentNullException(nameof(connect));
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
        _sessionStartedAt = _timeProvider.GetTimestamp();
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

            if (_expired || SessionExpired)
            {
                await ExpireAsync();
                return _readiness!;
            }

            CodeGraphToolIssueCode authority = await CheckSourceAuthorityAsync(cancellationToken);
            if (authority != CodeGraphToolIssueCode.None)
            {
                return _readiness = Failure(authority == CodeGraphToolIssueCode.AuthorityUnavailable
                    ? CodeGraphToolReadinessStatus.AuthorityUnavailable : CodeGraphToolReadinessStatus.AuthorityDenied,
                    CodeGraphToolStage.Discovery, authority);
            }

            if (_readiness is not null)
            {
                return _readiness;
            }

            if (_connect is null && !File.Exists(_commandPath))
            {
                return _readiness = Failure(CodeGraphToolReadinessStatus.Unavailable,
                    CodeGraphToolStage.Preparation, CodeGraphToolIssueCode.InstallationUnavailable);
            }

            if (_connect is null && !Directory.Exists(Path.Combine(_projectPath, ".codegraph")))
            {
                return _readiness = Failure(CodeGraphToolReadinessStatus.Unprepared,
                    CodeGraphToolStage.Preparation, CodeGraphToolIssueCode.SourceUnprepared);
            }

            using var connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectionCancellation.CancelAfter(_operationTimeout);
            long connectionStartedAt = Stopwatch.GetTimestamp();
            try
            {
                _client = _connect is not null
                    ? await _connect(connectionCancellation.Token)
                    : new McpCodeGraphTransport(await McpClient.CreateAsync(CreateTransport(),
                        cancellationToken: connectionCancellation.Token));
                _connection?.ObserveNativeCompletion(_client.Completion);
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
                if (SessionExpired)
                {
                    await ExpireAsync();
                    return _readiness!;
                }

                IReadOnlyList<Tool> tools = await _client.ListToolsAsync(
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

                if (!IsReviewedSchema(tools[0].InputSchema))
                {
                    return _readiness = Failure(CodeGraphToolReadinessStatus.DiscoveryFailed,
                        CodeGraphToolStage.Discovery, CodeGraphToolIssueCode.SchemaChanged);
                }

                if (SessionExpired)
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

            if (_expired || SessionExpired)
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
                return Outcome(_readiness?.IssueCode switch
                {
                    CodeGraphToolIssueCode.AuthorityDenied => CodeGraphToolInvocationStatus.Denied,
                    CodeGraphToolIssueCode.AuthorityUnavailable => CodeGraphToolInvocationStatus.AuthorityUnavailable,
                    _ => CodeGraphToolInvocationStatus.Unavailable,
                },
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

            using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            operationCancellation.CancelAfter(_operationTimeout);
            long startedAt = Stopwatch.GetTimestamp();
            try
            {
                CodeGraphToolIssueCode authority = await CheckSourceAuthorityAsync(operationCancellation.Token);
                if (authority != CodeGraphToolIssueCode.None)
                {
                    return Outcome(authority == CodeGraphToolIssueCode.AuthorityUnavailable
                        ? CodeGraphToolInvocationStatus.AuthorityUnavailable : CodeGraphToolInvocationStatus.Denied, authority);
                }
                _nativeInvocationCount++;
                var arguments = new Dictionary<string, object?>
                {
                    ["query"] = query,
                    ["maxFiles"] = maxFiles,
                    ["projectPath"] = _projectPath,
                };
                CallToolResult result = await _client.CallToolAsync(
                    arguments,
                    cancellationToken: operationCancellation.Token);
                if (cancellationToken.IsCancellationRequested)
                {
                    await CancelAsync();
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

                if (SessionExpired)
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

                return _openRead is null
                    ? ParseBoundedResult(result, query)
                    : await ParseContainedResultAsync(result, query, operationCancellation.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await CancelAsync();
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

    private async Task CancelAsync()
    {
        _readiness = Failure(CodeGraphToolReadinessStatus.Cancelled,
            CodeGraphToolStage.Invocation, CodeGraphToolIssueCode.Cancelled);
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
                ShutdownTimeout = TimeSpan.FromSeconds(1),
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
            ShutdownTimeout = TimeSpan.FromSeconds(1),
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

    private async Task<CodeGraphToolInvocationResult> ParseContainedResultAsync(
        CallToolResult result, string query, CancellationToken cancellationToken)
    {
        if (result.Content.Count != 1 || result.Content[0] is not TextContentBlock block ||
            Encoding.UTF8.GetByteCount(block.Text) > MaxResultBytes)
        {
            return Outcome(CodeGraphToolInvocationStatus.Limited, CodeGraphToolIssueCode.ResultLimit);
        }

        bool noMatch = block.Text == $"No relevant code found for \"{query}\"";
        int section = block.Text.IndexOf("**Source Code**", StringComparison.Ordinal);
        if (!noMatch && section < 0)
        {
            return Outcome(CodeGraphToolInvocationStatus.Limited, CodeGraphToolIssueCode.IncompleteIndex);
        }

        var excerpts = new List<CodeGraphSourceExcerpt>();
        if (!noMatch)
        {
            foreach (Match match in SourceBlockRegex().Matches(block.Text[(section + 15)..]))
            {
                string relative = match.Groups["path"].Value.Replace('\\', '/');
                if (!PreparedSourceTraversal.IsCanonicalRelativePath(relative, allowEmpty: false))
                {
                    return Outcome(CodeGraphToolInvocationStatus.Denied, CodeGraphToolIssueCode.UncontainedSource);
                }
                excerpts.Add(new(new(_repositoryId, relative), StripLineNumbers(match.Groups["source"].Value)));
            }
            if (excerpts.Count == 0)
            {
                return Outcome(CodeGraphToolInvocationStatus.Limited, CodeGraphToolIssueCode.IncompleteIndex);
            }
        }

        PreparedSourceIssueResult issued;
        try
        {
            issued = await _openRead!(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return Outcome(CodeGraphToolInvocationStatus.Denied, CodeGraphToolIssueCode.UncontainedSource);
        }
        await using IPreparedSourceReadContext? source = issued.Context;
        if (issued.Status != PreparedSourceIssueStatus.Issued || source is null ||
            source.RepositoryId != _repositoryId)
        {
            return Outcome(CodeGraphToolInvocationStatus.Denied, CodeGraphToolIssueCode.UncontainedSource);
        }
        if (noMatch)
        {
            return Outcome(CodeGraphToolInvocationStatus.NoMatch);
        }

        var verified = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (CodeGraphSourceExcerpt excerpt in excerpts)
        {
            string path = excerpt.Location.RelativePath;
            if (!verified.TryGetValue(path, out string? text))
            {
                (text, bool denied) = await ReadContainedFileAsync(source, path, cancellationToken);
                if (text is null)
                {
                    return Outcome(denied ? CodeGraphToolInvocationStatus.Denied : CodeGraphToolInvocationStatus.Limited,
                        denied ? CodeGraphToolIssueCode.UncontainedSource : CodeGraphToolIssueCode.IncompleteIndex);
                }
                verified.Add(path, text);
            }
            if (!text.Replace("\r\n", "\n", StringComparison.Ordinal).Contains(excerpt.Text, StringComparison.Ordinal))
            {
                return Outcome(CodeGraphToolInvocationStatus.Limited, CodeGraphToolIssueCode.IncompleteIndex);
            }
        }

        // Re-read under the same contained context immediately before disclosure.
        // This rechecks live authority and rejects a changed revision, including
        // changes between verification of separate native excerpts.
        foreach ((string path, string expected) in verified)
        {
            (string? current, bool denied) = await ReadContainedFileAsync(source, path, cancellationToken);
            if (current != expected)
            {
                return Outcome(denied ? CodeGraphToolInvocationStatus.Denied : CodeGraphToolInvocationStatus.Limited,
                    denied ? CodeGraphToolIssueCode.UncontainedSource : CodeGraphToolIssueCode.IncompleteIndex);
            }
        }
        if (SessionExpired)
        {
            return Outcome(CodeGraphToolInvocationStatus.Expired, CodeGraphToolIssueCode.SessionExpired);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new CodeGraphToolInvocationResult
        {
            Status = CodeGraphToolInvocationStatus.Complete,
            RepositoryId = _repositoryId,
            Locations = excerpts.Select(excerpt => excerpt.Location).ToArray(),
            Excerpts = excerpts,
        };
    }

    private async Task<CodeGraphToolIssueCode> CheckSourceAuthorityAsync(CancellationToken cancellationToken)
    {
        if (_openRead is null) return CodeGraphToolIssueCode.None;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_operationTimeout);
        try
        {
            PreparedSourceIssueResult issued = await _openRead(deadline.Token);
            await using IPreparedSourceReadContext? source = issued.Context;
            deadline.Token.ThrowIfCancellationRequested();
            if (issued.Status == PreparedSourceIssueStatus.AuthorityUnavailable)
                return CodeGraphToolIssueCode.AuthorityUnavailable;
            return issued.Status == PreparedSourceIssueStatus.Issued && source?.RepositoryId == _repositoryId &&
                source.Lifetime == PreparedSourceContextLifetime.Open
                ? CodeGraphToolIssueCode.None : CodeGraphToolIssueCode.AuthorityDenied;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return CodeGraphToolIssueCode.AuthorityUnavailable; }
    }

    private async Task<(string? Text, bool Denied)> ReadContainedFileAsync(
        IPreparedSourceReadContext source, string path, CancellationToken cancellationToken)
    {
        var text = new StringBuilder();
        long offset = 0;
        long? length = null;
        do
        {
            PreparedSourceResult read = await source.ReadTextRangeAsync(
                new(path, offset, PreparedSourceCallLimits.MaximumReadBytes), cancellationToken);
            if (read.Outcome is PreparedSourceOutcome.Denied or PreparedSourceOutcome.AuthorityUnavailable or
                PreparedSourceOutcome.ChangedInput or PreparedSourceOutcome.DeadlineExceeded or PreparedSourceOutcome.Cancelled ||
                read.RepositoryId != _repositoryId)
            {
                return (null, true);
            }
            if (read.Outcome is not (PreparedSourceOutcome.Complete or PreparedSourceOutcome.CompleteEmpty) ||
                read.Text is null || read.ReadLocation is not { } location ||
                location.RepositoryId != _repositoryId || location.RelativePath != path || location.ByteStart != offset ||
                read.ObservedFileByteLength is not { } observedLength || observedLength < 0 ||
                observedLength > MaxVerificationFileBytes || length is not null && length != observedLength ||
                location.ByteEndExclusive < offset || location.ByteEndExclusive > observedLength ||
                location.ByteEndExclusive == offset && offset != observedLength)
            {
                return (null, false);
            }
            length = observedLength;
            text.Append(read.Text);
            offset = location.ByteEndExclusive;
        } while (offset < length);
        return (text.ToString(), false);
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

    private sealed class McpCodeGraphTransport(McpClient client) : ICodeGraphMcpTransport
    {
        public Task<ClientCompletionDetails>? Completion => client.Completion;

        public async Task<IReadOnlyList<Tool>> ListToolsAsync(CancellationToken cancellationToken)
            => (await client.ListToolsAsync(cancellationToken: cancellationToken))
                .Select(tool => tool.ProtocolTool).ToArray();

        public async Task<CallToolResult> CallToolAsync(
            IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken)
            => await client.CallToolAsync("codegraph_explore", new Dictionary<string, object?>(arguments),
                cancellationToken: cancellationToken);

        public ValueTask DisposeAsync() => client.DisposeAsync();
    }
}
