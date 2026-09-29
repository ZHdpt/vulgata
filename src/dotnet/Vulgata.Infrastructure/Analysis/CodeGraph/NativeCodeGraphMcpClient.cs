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
    private const int MaxResultBytes = 256 * 1024;
    private const int MaxVerificationFileBytes = 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly Guid _repositoryId;
    private readonly string _commandPath;
    private readonly string _projectPath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private McpClient? _client;
    private CodeGraphToolReadiness? _readiness;
    private bool _disposed;

    internal NativeCodeGraphMcpClient(Guid repositoryId, string commandPath, string projectPath)
    {
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

            try
            {
                _client = await McpClient.CreateAsync(CreateTransport(), cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return _readiness = Failure(CodeGraphToolReadinessStatus.Cancelled,
                    CodeGraphToolStage.Connection, CodeGraphToolIssueCode.Cancelled);
            }
            catch (Exception)
            {
                return _readiness = Failure(CodeGraphToolReadinessStatus.ConnectionFailed,
                    CodeGraphToolStage.Connection, CodeGraphToolIssueCode.ConnectionFailed);
            }

            try
            {
                IList<McpClientTool> tools = await _client.ListToolsAsync(cancellationToken: cancellationToken);
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

                return _readiness = new CodeGraphToolReadiness(
                    CodeGraphToolReadinessStatus.Ready, _repositoryId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return _readiness = Failure(CodeGraphToolReadinessStatus.Cancelled,
                    CodeGraphToolStage.Discovery, CodeGraphToolIssueCode.Cancelled);
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
            if (_disposed || _readiness?.IsReady != true || _client is null)
            {
                return Outcome(CodeGraphToolInvocationStatus.Unavailable,
                    CodeGraphToolIssueCode.SessionExpired);
            }

            if (string.IsNullOrWhiteSpace(query) || maxFiles is < 1 or > 12 ||
                !WithinQueryLimit(query))
            {
                return Outcome(CodeGraphToolInvocationStatus.Denied);
            }

            try
            {
                var arguments = new Dictionary<string, object?>
                {
                    ["query"] = query,
                    ["maxFiles"] = maxFiles,
                    ["projectPath"] = _projectPath,
                };
                CallToolResult result = await _client.CallToolAsync(
                    "codegraph_explore", arguments, cancellationToken: cancellationToken);
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

    private StdioClientTransport CreateTransport()
    {
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
        return new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "prepared-codegraph",
            Command = command,
            Arguments = arguments,
            WorkingDirectory = _projectPath,
            InheritEnvironmentVariables = false,
            EnvironmentVariables = environment,
        });
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
        string.Join("\n", text.Replace("\r\n", "\n", StringComparison.Ordinal)
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
