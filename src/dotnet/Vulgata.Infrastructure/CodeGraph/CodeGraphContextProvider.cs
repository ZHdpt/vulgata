using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.CodeGraph;

public sealed record CodeGraphOptions(string StateRootPath, int WholeRepositoryProcessTimeoutSeconds = 1_800)
{
    internal string ExecutablePath { get; init; } = "cgc";
    internal IReadOnlyList<string> ExecutablePrefix { get; init; } = [];
}

public sealed class CodeGraphContextProvider(HttpClient httpClient, CodeGraphOptions options) : ICodeGraphProvider
{
    private const int _maximumCombinedResponseBytes = 64 * 1_024 * 1_024;
    public async Task<CodeGraphProviderResult> ProfileAsync(
        string workingCopyPath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        string contextName = $"vulgata-{Guid.NewGuid():N}";
        string snapshotRoot = Path.GetFullPath(workingCopyPath);
        string stateRoot = Path.GetFullPath(options.StateRootPath);
        string homePath = Path.Combine(stateRoot, contextName, "home");
        string databasePath = Path.Combine(stateRoot, contextName, "graph.kuzu");
        Directory.CreateDirectory(homePath);
        GatewayProcess? gateway = null;
        bool contextCreated = false;
        bool cleanupSucceeded = true;
        CodeGraphProviderResult? result = null;
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);

        try
        {
            ProcessCommandResult version = await RunAsync(homePath, ["version"], deadline.Token);
            if (version.ExitCode == -2)
            {
                result = CodeGraphProviderResult.Failure("CodeGraph CLI not found", stopwatch.Elapsed);
            }
            else
            {
                EnsureSuccess(version, "CodeGraph version");
                EnsureSuccess(await RunAsync(
                    homePath,
                    ["context", "create", contextName, "--database", "kuzudb", "--db-path", databasePath],
                    deadline.Token), "CodeGraph context create");
                contextCreated = true;
                EnsureSuccess(await RunAsync(homePath, ["context", "mode", "named"], deadline.Token), "CodeGraph context mode");
                EnsureSuccess(await RunAsync(homePath, ["context", "default", contextName], deadline.Token), "CodeGraph context default");
                EnsureSuccess(await RunAsync(
                    homePath,
                    ["index", snapshotRoot, "--force", "--context", contextName],
                    deadline.Token), "CodeGraph index");

                int port = ReserveLoopbackPort();
                gateway = StartGateway(homePath, port);
                Uri baseUri = new($"http://127.0.0.1:{port}/");
                await WaitUntilHealthyAsync(baseUri, gateway.Process, deadline.Token);
                byte[] codeUnitResponse = await QueryAsync(
                    baseUri,
                    CodeGraphContextProviderQueries.CodeUnits,
                    _maximumCombinedResponseBytes,
                    deadline.Token);
                byte[] relationshipResponse = await QueryAsync(
                    baseUri,
                    CodeGraphContextProviderQueries.Relationships,
                    _maximumCombinedResponseBytes - codeUnitResponse.Length,
                    deadline.Token);
                CodeGraphProviderResult parsed = CodeGraphHttpProjectionParser.Parse(
                    codeUnitResponse,
                    relationshipResponse,
                    snapshotRoot,
                    stopwatch.Elapsed);
                result = parsed with { Duration = stopwatch.Elapsed };
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result = CodeGraphProviderResult.Failure("CodeGraph 执行超时。", stopwatch.Elapsed);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            result = CodeGraphProviderResult.Failure(Sanitize(exception.Message), stopwatch.Elapsed);
        }
        finally
        {
            cleanupSucceeded &= await StopGatewayAsync(gateway);
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            if (contextCreated)
            {
                ProcessCommandResult contextDelete = await RunAsync(
                    homePath,
                    ["context", "delete", contextName],
                    cleanup.Token,
                    "y\n");
                cleanupSucceeded &= contextDelete.ExitCode == 0;
            }

            cleanupSucceeded &= TryDelete(Path.Combine(stateRoot, contextName));
        }

        if (!cleanupSucceeded && result?.Succeeded == true)
        {
            return CodeGraphProviderResult.Failure("CodeGraph 私有上下文清理失败。", stopwatch.Elapsed);
        }

        return result ?? CodeGraphProviderResult.Failure("CodeGraph 执行失败。", stopwatch.Elapsed);
    }

    private async Task<byte[]> QueryAsync(
        Uri baseUri,
        string query,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(new { query, @params = new { } });
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, "api/v1/query"))
        {
            Content = new ByteArrayContent(body),
        };
        request.Content.Headers.ContentType = new("application/json");
        using HttpResponseMessage response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadBoundedAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            maximumBytes,
            cancellationToken);
    }

    private GatewayProcess StartGateway(string homePath, int port)
    {
        ProcessStartInfo startInfo = CreateGatewayStartInfo(homePath, port);
        var process = new Process { StartInfo = startInfo };
        process.Start();
        return new GatewayProcess(
            process,
            DrainAsync(process.StandardOutput),
            DrainAsync(process.StandardError));
    }

    private ProcessStartInfo CreateGatewayStartInfo(string homePath, int port)
    {
        ProcessStartInfo startInfo = CreateStartInfo(homePath);
        startInfo.Environment["TOOL_RESULT_LIMITS"] = """{"execute_cypher_query":250001}""";
        startInfo.ArgumentList.Add("api");
        startInfo.ArgumentList.Add("start");
        startInfo.ArgumentList.Add("--host");
        startInfo.ArgumentList.Add("127.0.0.1");
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return startInfo;
    }

    private async Task WaitUntilHealthyAsync(Uri baseUri, Process gateway, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (gateway.HasExited)
            {
                throw new InvalidOperationException("CodeGraph API 启动失败。");
            }

            try
            {
                using HttpResponseMessage response = await httpClient.GetAsync(
                    new Uri(baseUri, "health"),
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    await response.Content.CopyToAsync(Stream.Null, cancellationToken);
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // The local gateway is still starting.
            }

            await Task.Delay(100, cancellationToken);
        }

        throw new TimeoutException("CodeGraph API 就绪超时。");
    }

    private async Task<ProcessCommandResult> RunAsync(
        string homePath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        string? standardInput = null)
    {
        ProcessStartInfo startInfo = CreateStartInfo(homePath, standardInput is not null);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
            if (standardInput is not null)
            {
                await process.StandardInput.WriteAsync(standardInput);
                process.StandardInput.Close();
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new ProcessCommandResult(-2, string.Empty, "CodeGraph CLI not found");
        }

        Task<string> stdout = ReadAndDiscardAsync(process.StandardOutput, cancellationToken);
        Task<string> stderr = ReadAndDiscardAsync(process.StandardError, cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            return new ProcessCommandResult(
                process.ExitCode,
                await stdout,
                await stderr);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync(CancellationToken.None);
            try
            {
                await Task.WhenAll(stdout, stderr);
            }
            catch (OperationCanceledException)
            {
                // Preserve the caller's timeout/cancellation as the authoritative failure.
            }
            throw;
        }
    }

    private ProcessStartInfo CreateStartInfo(string homePath, bool redirectStandardInput = false)
    {
        var startInfo = new ProcessStartInfo(options.ExecutablePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = redirectStandardInput,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["HOME"] = homePath;
        foreach (string argument in options.ExecutablePrefix)
        {
            startInfo.ArgumentList.Add(argument);
        }
        return startInfo;
    }

    private static async Task<string> ReadAndDiscardAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        char[] buffer = new char[1_024];
        var retained = new StringBuilder(512);
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            if (retained.Length < 512)
            {
                retained.Append(buffer, 0, Math.Min(read, 512 - retained.Length));
            }
        }

        return retained.ToString();
    }

    private static async Task DrainAsync(StreamReader reader)
    {
        char[] buffer = new char[1_024];
        while (await reader.ReadAsync(buffer) > 0)
        {
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream source, int maximumBytes, CancellationToken cancellationToken)
    {
        using var destination = new MemoryStream();
        byte[] buffer = new byte[8_192];
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (destination.Length + read > maximumBytes)
            {
                throw new InvalidDataException("CodeGraph 响应超过安全限制。");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return destination.ToArray();
    }

    private static int ReserveLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static void EnsureSuccess(ProcessCommandResult result, string operation)
    {
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"{operation} 失败：{Sanitize(result.StandardError)}");
        }
    }

    private static async Task<bool> StopGatewayAsync(GatewayProcess? gateway)
    {
        if (gateway is null)
        {
            return true;
        }

        Process process = gateway.Process;
        try
        {
            if (!process.HasExited)
            {
                if (!OperatingSystem.IsWindows())
                {
                    using var term = Process.Start(new ProcessStartInfo("kill")
                    {
                        ArgumentList = { "-TERM", process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    });
                    term?.WaitForExit(2_000);
                }

                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5_000);
                }
            }

            bool exited = process.HasExited;
            if (exited)
            {
                await Task.WhenAll(gateway.StandardOutputDrain, gateway.StandardErrorDrain);
            }

            return exited;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
        finally
        {
            process.Dispose();
        }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }

            return !Directory.Exists(path);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string Sanitize(string value)
    {
        string normalized = string.Join(' ', value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 512 ? normalized : normalized[..512];
    }

    private sealed record ProcessCommandResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed record GatewayProcess(
        Process Process,
        Task StandardOutputDrain,
        Task StandardErrorDrain);
}

internal static class CodeGraphHttpProjectionParser
{
    private const int _maximumCombinedRows = 250_000;
    private const int _maximumRowBytes = 4_096;
    private const int _maximumWarningDetails = 100;
    private static readonly string[] _codeUnitFields =
    [
        "record_kind", "provider_kind", "path", "name", "start_line", "end_line", "parser_language",
    ];
    private static readonly string[] _relationshipFields =
    [
        "record_kind", "relationship_kind",
        "source_provider_kind", "source_path", "source_name", "source_start_line", "source_end_line",
        "target_provider_kind", "target_path", "target_name", "target_start_line", "target_end_line",
    ];

    internal static CodeGraphProviderResult Parse(
        byte[] codeUnitsUtf8Json,
        byte[] relationshipsUtf8Json,
        string workingCopyPath,
        TimeSpan duration)
    {
        using JsonDocument codeUnitDocument = ParseEnvelope(
            codeUnitsUtf8Json,
            CodeGraphContextProviderQueries.CodeUnits,
            out JsonElement codeUnitRows);
        using JsonDocument relationshipDocument = ParseEnvelope(
            relationshipsUtf8Json,
            CodeGraphContextProviderQueries.Relationships,
            out JsonElement relationshipRows);
        if ((long)codeUnitRows.GetArrayLength() + relationshipRows.GetArrayLength() > _maximumCombinedRows)
        {
            throw new InvalidDataException("CodeGraph 记录数量超过安全限制。");
        }

        var units = new Dictionary<string, ProviderCodeUnit>(StringComparer.Ordinal);
        var endpointTuples = new Dictionary<string, EndpointTuple>(StringComparer.Ordinal);
        var languagesByPath = new Dictionary<string, string>(StringComparer.Ordinal);
        var relationships = new HashSet<ProviderStructuralRelationship>();
        var outcomes = new List<ProviderCandidateOutcome>();
        var acceptedPaths = new HashSet<string>(StringComparer.Ordinal);
        var parsedPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement row in codeUnitRows.EnumerateArray())
        {
            string? parserLanguage = ReadParserLanguageAtomically(row);
            EndpointTuple? acceptedEndpoint = null;
            try
            {
                EnsureBoundedClosedRow(row, _codeUnitFields);
                if (RequireString(row, "record_kind") != "code_unit")
                {
                    throw new InvalidDataException("CodeGraph 记录类型无效。");
                }

                EndpointTuple endpoint = ParseEndpoint(
                    RequireString(row, "provider_kind"),
                    ReadNullableString(row, "path"),
                    ReadNullableString(row, "name"),
                    ReadNullablePositiveInt(row, "start_line"),
                    ReadNullablePositiveInt(row, "end_line"));
                RegisterEndpoint(endpointTuples, endpoint);
                ProviderCodeUnit? unit = MapCodeUnit(endpoint, workingCopyPath);
                if (unit is not null && units.TryAdd(endpoint.Identity, unit))
                {
                    parsedPaths.Add(unit.RelativePath);
                }

                acceptedEndpoint = endpoint;
            }
            catch (Exception exception) when (IsMalformedRowException(exception))
            {
                AddWarning(outcomes, ReadSafeProviderPath(row, "path"));
            }

            if (acceptedEndpoint is not null)
            {
                if (acceptedEndpoint.Path.Length > 0)
                {
                    acceptedPaths.Add(acceptedEndpoint.Path);
                }

                ReconcileParserLanguage(languagesByPath, acceptedEndpoint.Path, parserLanguage);
            }
        }

        var unknownLanguagePaths = acceptedPaths
            .Where(path => !languagesByPath.ContainsKey(path))
            .ToHashSet(StringComparer.Ordinal);
        var warnedPaths = outcomes
            .Select(outcome => outcome.RelativePath.Value)
            .ToHashSet(StringComparer.Ordinal);
        foreach (string path in unknownLanguagePaths
                     .Where(path => !warnedPaths.Contains(path))
                     .Order(StringComparer.Ordinal))
        {
            outcomes.Add(new ProviderCandidateOutcome(
                SafeProviderPath.FromProviderScalar(path),
                ScanCandidateOutcomeKind.Filtered,
                ScanCandidateOutcomeReasons.UnknownLanguage));
        }

        foreach ((string identity, ProviderCodeUnit unit) in units.ToArray())
        {
            if (unknownLanguagePaths.Contains(unit.RelativePath))
            {
                units.Remove(identity);
            }
            else if (languagesByPath.TryGetValue(unit.RelativePath, out string? language))
            {
                units[identity] = unit with { Language = language };
            }
        }

        foreach (JsonElement row in relationshipRows.EnumerateArray())
        {
            try
            {
                EnsureBoundedClosedRow(row, _relationshipFields);
                if (RequireString(row, "record_kind") != "relationship")
                {
                    throw new InvalidDataException("CodeGraph 记录类型无效。");
                }

                StructuralRelationshipKind relationshipKind = RequireString(row, "relationship_kind") switch
                {
                    "CALLS" => StructuralRelationshipKind.Calls,
                    "IMPORTS" => StructuralRelationshipKind.Imports,
                    "INHERITS" => StructuralRelationshipKind.Inherits,
                    "CONTAINS" => StructuralRelationshipKind.Contains,
                    _ => throw new InvalidDataException("不支持的结构关系。"),
                };
                EndpointTuple source = ParseEndpoint(
                    RequireString(row, "source_provider_kind"),
                    ReadNullableString(row, "source_path"),
                    ReadNullableString(row, "source_name"),
                    ReadNullablePositiveInt(row, "source_start_line"),
                    ReadNullablePositiveInt(row, "source_end_line"));
                EndpointTuple target = ParseEndpoint(
                    RequireString(row, "target_provider_kind"),
                    ReadNullableString(row, "target_path"),
                    ReadNullableString(row, "target_name"),
                    ReadNullablePositiveInt(row, "target_start_line"),
                    ReadNullablePositiveInt(row, "target_end_line"));
                RegisterEndpoint(endpointTuples, source);
                RegisterEndpoint(endpointTuples, target);
                relationships.Add(new ProviderStructuralRelationship(
                    source.Identity,
                    target.Identity,
                    relationshipKind));
            }
            catch (Exception exception) when (IsMalformedRowException(exception))
            {
                AddWarning(outcomes, ReadSafeProviderPath(row, "source_path"));
            }
        }

        relationships.RemoveWhere(relationship =>
            (endpointTuples.TryGetValue(
                    relationship.SourceEndpointIdentity,
                    out EndpointTuple? source) &&
                unknownLanguagePaths.Contains(source.Path)) ||
            (endpointTuples.TryGetValue(
                    relationship.TargetEndpointIdentity,
                    out EndpointTuple? target) &&
                unknownLanguagePaths.Contains(target.Path)));
        parsedPaths.Clear();
        parsedPaths.UnionWith(units.Values.Select(unit => unit.RelativePath));

        return new CodeGraphProviderResult(
            true,
            units.Values.ToArray(),
            relationships.ToArray(),
            outcomes,
            parsedPaths.Count,
            duration,
            null);
    }

    private static JsonDocument ParseEnvelope(byte[] utf8Json, string query, out JsonElement rows)
    {
        JsonDocument document = JsonDocument.Parse(
            utf8Json,
            new JsonDocumentOptions { AllowTrailingCommas = false, MaxDepth = 32 });
        try
        {
            JsonElement root = document.RootElement;
            EnsureRequiredPropertiesOnce(root, ["status", "message", "data", "error"]);
            if (root.GetProperty("status").ValueKind is not JsonValueKind.String ||
                root.GetProperty("status").GetString() != "ok" ||
                root.GetProperty("message").ValueKind is not JsonValueKind.Null ||
                root.GetProperty("error").ValueKind is not JsonValueKind.Null)
            {
                throw new InvalidDataException("CodeGraph 返回失败状态。");
            }

            JsonElement data = root.GetProperty("data");
            EnsureRequiredPropertiesOnce(data, ["success", "query", "record_count", "results"]);
            RejectReservedCompletenessMetadata(data);
            if (data.ValueKind is not JsonValueKind.Object ||
                data.GetProperty("success").ValueKind is not JsonValueKind.True ||
                data.GetProperty("query").ValueKind is not JsonValueKind.String ||
                data.GetProperty("query").GetString() != query ||
                data.GetProperty("record_count").ValueKind is not JsonValueKind.Number ||
                !data.GetProperty("record_count").TryGetInt32(out int recordCount) ||
                recordCount < 0 ||
                data.GetProperty("results").ValueKind is not JsonValueKind.Array)
            {
                throw new InvalidDataException("CodeGraph 响应与请求不匹配。");
            }

            rows = data.GetProperty("results");
            if (recordCount != rows.GetArrayLength())
            {
                throw new InvalidDataException("CodeGraph 记录数量不一致。");
            }

            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    private static void RejectReservedCompletenessMetadata(JsonElement data)
    {
        int truncatedCount = 0;
        int resultLimitCount = 0;
        JsonElement truncated = default;
        JsonElement resultLimit = default;
        foreach (JsonProperty property in data.EnumerateObject())
        {
            if (property.NameEquals("truncated"))
            {
                truncatedCount++;
                truncated = property.Value;
            }
            else if (property.NameEquals("result_limit"))
            {
                resultLimitCount++;
                resultLimit = property.Value;
            }
        }

        if (truncatedCount == 0 && resultLimitCount == 0)
        {
            return;
        }

        if (truncatedCount != 1 ||
            resultLimitCount != 1 ||
            truncated.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
            resultLimit.ValueKind is not JsonValueKind.Number ||
            !resultLimit.TryGetInt32(out int limit) ||
            limit <= 0)
        {
            throw new InvalidDataException("CodeGraph 完整性元数据无效。");
        }

        throw new InvalidDataException("CodeGraph 返回了不完整结果。");
    }

    private static void EnsureRequiredPropertiesOnce(JsonElement element, IReadOnlyList<string> required)
    {
        if (element.ValueKind is not JsonValueKind.Object)
        {
            throw new InvalidDataException("CodeGraph JSON 对象无效。");
        }

        var counts = required.ToDictionary(property => property, _ => 0, StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (counts.ContainsKey(property.Name))
            {
                counts[property.Name]++;
            }
        }

        if (counts.Values.Any(count => count != 1))
        {
            throw new InvalidDataException("CodeGraph 必需字段缺失或重复。");
        }
    }

    private static void EnsureBoundedClosedRow(JsonElement row, IReadOnlyList<string> fields)
    {
        if (row.ValueKind is not JsonValueKind.Object ||
            Encoding.UTF8.GetByteCount(row.GetRawText()) > _maximumRowBytes)
        {
            throw new InvalidDataException("CodeGraph 行超过安全限制。");
        }

        var expected = fields.ToHashSet(StringComparer.Ordinal);
        var observed = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in row.EnumerateObject())
        {
            if (!expected.Contains(property.Name) ||
                !observed.Add(property.Name) ||
                property.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
            {
                throw new InvalidDataException("CodeGraph 行字段无效。");
            }
        }

        if (observed.Count != expected.Count)
        {
            throw new InvalidDataException("CodeGraph 行字段缺失。");
        }
    }

    private static EndpointTuple ParseEndpoint(
        string kind,
        string? path,
        string? name,
        int? startLine,
        int? endLine)
    {
        string normalizedKind = kind switch
        {
            "File" or "Module" or "Class" or "Function" => kind,
            _ => throw new InvalidDataException("CodeGraph 节点类型无效。"),
        };
        string normalizedPath = path is null ? string.Empty : NormalizePath(path);
        string normalizedName = name?.Trim() ?? string.Empty;
        if (normalizedName.Length > 512 ||
            normalizedKind == "File" && normalizedPath.Length == 0 ||
            normalizedKind == "Module" && normalizedName.Length == 0 ||
            normalizedKind is "Class" or "Function" &&
            (normalizedPath.Length == 0 || normalizedName.Length == 0 || startLine is null || endLine is null))
        {
            throw new InvalidDataException("CodeGraph 端点字段无效。");
        }

        if ((startLine is null) != (endLine is null) || endLine < startLine)
        {
            throw new InvalidDataException("CodeGraph 代码范围无效。");
        }

        string canonical = string.Join(
            '\0',
            normalizedKind,
            normalizedPath,
            normalizedName,
            startLine?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            endLine?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
        string identity = "cgc-v1:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return new EndpointTuple(
            normalizedKind,
            normalizedPath,
            normalizedName,
            startLine,
            endLine,
            identity);
    }

    private static ProviderCodeUnit? MapCodeUnit(EndpointTuple endpoint, string workingCopyPath)
    {
        if (endpoint.Kind == "Module" &&
            (endpoint.Path.Length == 0 || endpoint.StartLine is null || endpoint.EndLine is null))
        {
            return null;
        }

        string path = endpoint.Path;
        string name = endpoint.Name;
        int start = endpoint.StartLine ?? 0;
        int end = endpoint.EndLine ?? 0;
        RangeConfidence confidence = RangeConfidence.Exact;
        CodeUnitType type = endpoint.Kind switch
        {
            "File" => CodeUnitType.File,
            "Module" => CodeUnitType.Module,
            "Class" => CodeUnitType.Class,
            "Function" => CodeUnitType.Function,
            _ => throw new InvalidDataException("CodeGraph 节点类型无效。"),
        };
        if (endpoint.Kind == "File")
        {
            name = name.Length == 0 ? Path.GetFileName(path) : name;
            start = 1;
            end = CountLocalLines(workingCopyPath, path);
        }
        else if (endpoint.Kind == "Module")
        {
            confidence = RangeConfidence.Approximate;
        }

        return new ProviderCodeUnit(path, name, type, start, end, endpoint.Identity, confidence);
    }

    private static int CountLocalLines(string workingCopyPath, string relativePath)
    {
        string root = Path.GetFullPath(workingCopyPath);
        string fullPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidDataException("CodeGraph 返回了非仓库相对路径。");
        }

        byte[] bytes = File.ReadAllBytes(fullPath);
        if (bytes.Length == 0)
        {
            return 1;
        }

        int count = 1;
        foreach (byte value in bytes)
        {
            if (value == (byte)'\n')
            {
                count++;
            }
        }

        return bytes[^1] == (byte)'\n' ? Math.Max(1, count - 1) : count;
    }

    private static void RegisterEndpoint(
        IDictionary<string, EndpointTuple> endpoints,
        EndpointTuple endpoint)
    {
        if (endpoints.TryGetValue(endpoint.Identity, out EndpointTuple? existing) && existing != endpoint)
        {
            throw new InvalidDataException("CodeGraph 端点标识冲突。");
        }

        endpoints[endpoint.Identity] = endpoint;
    }

    private static string RequireString(JsonElement row, string property)
    {
        JsonElement value = row.GetProperty(property);
        string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return string.IsNullOrWhiteSpace(text)
            ? throw new InvalidDataException($"CodeGraph 字段 {property} 为空。")
            : text.Trim();
    }

    private static string? ReadNullableString(JsonElement row, string property)
    {
        JsonElement value = row.GetProperty(property);
        return value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString(),
            _ => throw new InvalidDataException($"CodeGraph 字段 {property} 类型无效。"),
        };
    }

    private static int? ReadNullablePositiveInt(JsonElement row, string property)
    {
        JsonElement value = row.GetProperty(property);
        if (value.ValueKind is JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind is not JsonValueKind.Number ||
            !value.TryGetInt32(out int number) ||
            number <= 0)
        {
            throw new InvalidDataException($"CodeGraph 字段 {property} 无效。");
        }

        return number;
    }

    private static string? ReadParserLanguageAtomically(JsonElement row)
    {
        if (row.ValueKind is not JsonValueKind.Object)
        {
            throw new InvalidDataException("CodeGraph 语言记录无效。");
        }

        int count = 0;
        JsonElement value = default;
        foreach (JsonProperty property in row.EnumerateObject())
        {
            if (property.NameEquals("parser_language"))
            {
                count++;
                value = property.Value;
            }
        }

        if (count != 1)
        {
            throw new InvalidDataException("CodeGraph 语言字段缺失或重复。");
        }

        if (value.ValueKind is JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind is not JsonValueKind.String)
        {
            throw new InvalidDataException("CodeGraph 语言字段类型无效。");
        }

        string normalized;
        try
        {
            normalized = (value.GetString() ?? string.Empty)
                .Normalize(NormalizationForm.FormKC)
                .Trim()
                .ToLowerInvariant();
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("CodeGraph 语言字段无效。", exception);
        }

        if (normalized.Length is < 1 or > 64 ||
            normalized[0] is < 'a' or > 'z' ||
            normalized.Any(character =>
                character is not (>= 'a' and <= 'z') and
                    not (>= '0' and <= '9') and
                    not ('_' or '+' or '#' or '.' or '-')))
        {
            throw new InvalidDataException("CodeGraph 语言字段无效。");
        }

        return normalized;
    }

    private static void ReconcileParserLanguage(
        IDictionary<string, string> languagesByPath,
        string path,
        string? parserLanguage)
    {
        if (path.Length == 0 || parserLanguage is null)
        {
            return;
        }

        if (languagesByPath.TryGetValue(path, out string? existing) &&
            !StringComparer.Ordinal.Equals(existing, parserLanguage))
        {
            throw new InvalidDataException("CodeGraph 同一路径存在冲突的语言标识。");
        }

        languagesByPath[path] = parserLanguage;
    }

    private static string NormalizePath(string path)
    {
        SafeProviderPath safePath = SafeProviderPath.FromProviderScalar(path);
        if (!safePath.IsKnown)
        {
            throw new InvalidDataException("CodeGraph 返回了非仓库相对路径。");
        }

        return safePath.Value;
    }

    private static SafeProviderPath ReadSafeProviderPath(JsonElement row, string property)
    {
        if (row.ValueKind == JsonValueKind.Object &&
            row.TryGetProperty(property, out JsonElement path) &&
            path.ValueKind == JsonValueKind.String)
        {
            return SafeProviderPath.FromProviderScalar(path.GetString());
        }

        return SafeProviderPath.FromProviderScalar(null);
    }

    private static void AddWarning(
        ICollection<ProviderCandidateOutcome> outcomes,
        SafeProviderPath path)
    {
        if (outcomes.Count < _maximumWarningDetails)
        {
            outcomes.Add(new ProviderCandidateOutcome(
                path,
                ScanCandidateOutcomeKind.Errored,
                "MalformedProviderRecord"));
        }
    }

    private static bool IsMalformedRowException(Exception exception) =>
        exception is InvalidDataException or KeyNotFoundException or InvalidOperationException or IOException or
            UnauthorizedAccessException;

    private sealed record EndpointTuple(
        string Kind,
        string Path,
        string Name,
        int? StartLine,
        int? EndLine,
        string Identity);
}

internal static class CodeGraphContextProviderQueries
{
    internal static readonly string CodeUnits = """
        MATCH (node)
        WHERE label(node) IN ['File', 'Module', 'Class', 'Function']
        RETURN 'code_unit' AS record_kind, label(node) AS provider_kind,
               node.path AS path, node.name AS name,
               node.line_number AS start_line, node.end_line AS end_line,
               node.lang AS parser_language
        ORDER BY provider_kind, path, name, start_line, end_line
        """.ReplaceLineEndings("\n");

    internal static readonly string Relationships = """
        MATCH (source)-[relationship]->(target)
        WHERE label(relationship) IN ['CONTAINS', 'IMPORTS', 'CALLS', 'INHERITS']
          AND label(source) IN ['File', 'Module', 'Class', 'Function']
          AND label(target) IN ['File', 'Module', 'Class', 'Function']
        RETURN 'relationship' AS record_kind, label(relationship) AS relationship_kind,
               label(source) AS source_provider_kind,
               source.path AS source_path, source.name AS source_name,
               source.line_number AS source_start_line, source.end_line AS source_end_line,
               label(target) AS target_provider_kind,
               target.path AS target_path, target.name AS target_name,
               target.line_number AS target_start_line, target.end_line AS target_end_line
        ORDER BY relationship_kind,
                 source_provider_kind, source_path, source_name, source_start_line, source_end_line,
                 target_provider_kind, target_path, target_name, target_start_line, target_end_line
        """.ReplaceLineEndings("\n");
}
