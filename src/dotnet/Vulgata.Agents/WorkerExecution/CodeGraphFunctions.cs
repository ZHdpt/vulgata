using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Vulgata.Core.DomainServices;

namespace Vulgata.Agents.WorkerExecution;

public sealed record CodeGraphToolRegistration(
    CodeGraphToolReadiness Readiness,
    IReadOnlyList<AIFunction> Functions);

/// <summary>
/// Exposes one reviewed CodeGraph function after native discovery. The session is
/// borrowed from trusted composition and remains owned by its caller.
/// </summary>
public sealed class CodeGraphFunctions
{
    private readonly ICodeGraphWorkerToolSession _session;

    public CodeGraphFunctions(ICodeGraphWorkerToolSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public async Task<CodeGraphToolRegistration> CreateAsync(CancellationToken cancellationToken)
    {
        CodeGraphToolReadiness readiness = await _session.DiscoverAsync(cancellationToken);
        return new CodeGraphToolRegistration(
            readiness,
            readiness.IsReady ? [new ExploreFunction(_session)] : []);
    }

    private sealed class ExploreFunction : AIFunction
    {
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);
        private static readonly JsonElement Schema = ParseSchema();
        private readonly ICodeGraphWorkerToolSession _session;

        public ExploreFunction(ICodeGraphWorkerToolSession session)
        {
            _session = session;
        }

        public override string Name => "codegraph_explore";

        public override string Description =>
            "Explore the approved prepared source with the qualified native CodeGraph tool.";

        public override JsonElement JsonSchema => Schema;

        public override JsonElement? ReturnJsonSchema => null;

        protected override async ValueTask<object?> InvokeCoreAsync(
            AIFunctionArguments arguments,
            CancellationToken cancellationToken)
        {
            if (!TryArguments(arguments, out string query, out int maxFiles))
            {
                return new CodeGraphToolInvocationResult
                {
                    Status = CodeGraphToolInvocationStatus.Denied,
                };
            }

            return await _session.ExploreAsync(query, maxFiles, cancellationToken);
        }

        private static bool TryArguments(
            AIFunctionArguments arguments,
            out string query,
            out int maxFiles)
        {
            query = string.Empty;
            maxFiles = 4;
            if (arguments.Keys.Any(key => key is not ("query" or "maxFiles")) ||
                !arguments.TryGetValue("query", out object? rawQuery) ||
                !TryString(rawQuery, out query) ||
                string.IsNullOrWhiteSpace(query) ||
                !WithinUtf8Limit(query))
            {
                return false;
            }

            return !arguments.TryGetValue("maxFiles", out object? rawMaxFiles) ||
                (TryInt32(rawMaxFiles, out maxFiles) && maxFiles is >= 1 and <= 12);
        }

        private static bool TryString(object? raw, out string value)
        {
            value = raw switch
            {
                string text => text,
                JsonElement { ValueKind: JsonValueKind.String } element => element.GetString()!,
                _ => string.Empty,
            };
            return raw is string or JsonElement { ValueKind: JsonValueKind.String };
        }

        private static bool TryInt32(object? raw, out int value)
        {
            switch (raw)
            {
                case int integer:
                    value = integer;
                    return true;
                case long integer when integer is >= int.MinValue and <= int.MaxValue:
                    value = (int)integer;
                    return true;
                case JsonElement { ValueKind: JsonValueKind.Number } element:
                    return element.TryGetInt32(out value);
                default:
                    value = 0;
                    return false;
            }
        }

        private static bool WithinUtf8Limit(string query)
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

        private static JsonElement ParseSchema()
        {
            using JsonDocument document = JsonDocument.Parse("""
                {"type":"object","properties":{"query":{"type":"string","minLength":1},"maxFiles":{"type":"integer","minimum":1,"maximum":12}},"required":["query"],"additionalProperties":false}
                """);
            return document.RootElement.Clone();
        }
    }
}
