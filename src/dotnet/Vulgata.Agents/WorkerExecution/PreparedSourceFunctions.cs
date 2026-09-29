using System.Text.Json;
using Microsoft.Extensions.AI;
using Vulgata.Core.DomainServices;

namespace Vulgata.Agents.WorkerExecution;

/// <summary>
/// Exposes only the three prepared-source reads from an already issued context.
/// The context is borrowed for the Worker execution and remains owned by its issuer.
/// </summary>
public sealed class PreparedSourceFunctions
{
    private readonly IPreparedSourceReadContext _context;

    public PreparedSourceFunctions(IPreparedSourceReadContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public IReadOnlyList<AIFunction> Create() =>
    [
        new ListFunction(_context),
        new ReadFunction(_context),
        new SearchFunction(_context),
    ];

    private abstract class PreparedSourceFunction : AIFunction
    {
        protected readonly IPreparedSourceReadContext Context;
        private readonly JsonElement _schema;

        protected PreparedSourceFunction(
            IPreparedSourceReadContext context,
            string schema)
        {
            Context = context;
            using JsonDocument document = JsonDocument.Parse(schema);
            _schema = document.RootElement.Clone();
        }

        public override JsonElement JsonSchema => _schema;

        public override JsonElement? ReturnJsonSchema => null;

        protected PreparedSourceResult Denied(PreparedSourceOperation operation) => new()
        {
            RepositoryId = Context.RepositoryId,
            Operation = operation,
            Outcome = PreparedSourceOutcome.Denied,
        };

        protected static bool HasOnly(
            AIFunctionArguments arguments,
            params string[] allowed) =>
            arguments.Keys.All(key => allowed.Contains(key, StringComparer.Ordinal));

        protected static bool TryString(
            AIFunctionArguments arguments,
            string name,
            out string value)
        {
            value = string.Empty;
            if (!arguments.TryGetValue(name, out object? raw))
            {
                return false;
            }

            if (raw is string text)
            {
                value = text;
                return true;
            }

            if (raw is JsonElement { ValueKind: JsonValueKind.String } element)
            {
                value = element.GetString()!;
                return true;
            }

            return false;
        }

        protected static bool TryOptionalString(
            AIFunctionArguments arguments,
            string name,
            out string? value)
        {
            value = null;
            if (!arguments.TryGetValue(name, out object? raw) || raw is null ||
                raw is JsonElement { ValueKind: JsonValueKind.Null })
            {
                return true;
            }

            if (raw is string text)
            {
                value = text;
                return true;
            }

            if (raw is JsonElement { ValueKind: JsonValueKind.String } element)
            {
                value = element.GetString();
                return true;
            }

            return false;
        }

        protected static bool TryInt32(
            AIFunctionArguments arguments,
            string name,
            out int value)
        {
            value = 0;
            if (!arguments.TryGetValue(name, out object? raw))
            {
                return false;
            }

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
                    return false;
            }
        }

        protected static bool TryInt64(
            AIFunctionArguments arguments,
            string name,
            out long value)
        {
            value = 0;
            if (!arguments.TryGetValue(name, out object? raw))
            {
                return false;
            }

            switch (raw)
            {
                case long integer:
                    value = integer;
                    return true;
                case int integer:
                    value = integer;
                    return true;
                case JsonElement { ValueKind: JsonValueKind.Number } element:
                    return element.TryGetInt64(out value);
                default:
                    return false;
            }
        }
    }

    private sealed class ListFunction(IPreparedSourceReadContext context)
        : PreparedSourceFunction(context, ListSchema)
    {
        public override string Name => "list_prepared_source";

        public override string Description =>
            "List immediate entries within the approved prepared source.";

        protected override async ValueTask<object?> InvokeCoreAsync(
            AIFunctionArguments arguments,
            CancellationToken cancellationToken)
        {
            if (!HasOnly(arguments, "relativeDirectory", "pageSize", "continuation") ||
                !TryString(arguments, "relativeDirectory", out string relativeDirectory) ||
                !TryInt32(arguments, "pageSize", out int pageSize) ||
                !TryOptionalString(arguments, "continuation", out string? continuation))
            {
                return Denied(PreparedSourceOperation.List);
            }

            return await Context.ListAsync(
                new PreparedSourceListRequest(relativeDirectory, pageSize, continuation),
                cancellationToken);
        }
    }

    private sealed class ReadFunction(IPreparedSourceReadContext context)
        : PreparedSourceFunction(context, ReadSchema)
    {
        public override string Name => "read_prepared_source";

        public override string Description =>
            "Read a bounded UTF-8 byte range from the approved prepared source.";

        protected override async ValueTask<object?> InvokeCoreAsync(
            AIFunctionArguments arguments,
            CancellationToken cancellationToken)
        {
            if (!HasOnly(arguments, "relativePath", "byteOffset", "byteCount") ||
                !TryString(arguments, "relativePath", out string relativePath) ||
                !TryInt64(arguments, "byteOffset", out long byteOffset) ||
                !TryInt32(arguments, "byteCount", out int byteCount))
            {
                return Denied(PreparedSourceOperation.ReadTextRange);
            }

            return await Context.ReadTextRangeAsync(
                new PreparedSourceReadTextRangeRequest(relativePath, byteOffset, byteCount),
                cancellationToken);
        }
    }

    private sealed class SearchFunction(IPreparedSourceReadContext context)
        : PreparedSourceFunction(context, SearchSchema)
    {
        public override string Name => "search_prepared_source";

        public override string Description =>
            "Search strict UTF-8 text for a literal within the approved prepared source.";

        protected override async ValueTask<object?> InvokeCoreAsync(
            AIFunctionArguments arguments,
            CancellationToken cancellationToken)
        {
            if (!HasOnly(arguments, "relativeDirectory", "literal", "pageSize", "continuation") ||
                !TryString(arguments, "relativeDirectory", out string relativeDirectory) ||
                !TryString(arguments, "literal", out string literal) ||
                !TryInt32(arguments, "pageSize", out int pageSize) ||
                !TryOptionalString(arguments, "continuation", out string? continuation))
            {
                return Denied(PreparedSourceOperation.SearchText);
            }

            return await Context.SearchTextAsync(
                new PreparedSourceSearchTextRequest(
                    relativeDirectory,
                    literal,
                    pageSize,
                    continuation),
                cancellationToken);
        }
    }

    private const string ListSchema = """
        {"type":"object","properties":{"relativeDirectory":{"type":"string"},"pageSize":{"type":"integer","minimum":1,"maximum":100},"continuation":{"type":["string","null"]}},"required":["relativeDirectory","pageSize"],"additionalProperties":false}
        """;

    private const string ReadSchema = """
        {"type":"object","properties":{"relativePath":{"type":"string"},"byteOffset":{"type":"integer","minimum":0},"byteCount":{"type":"integer","minimum":1,"maximum":65536}},"required":["relativePath","byteOffset","byteCount"],"additionalProperties":false}
        """;

    private const string SearchSchema = """
        {"type":"object","properties":{"relativeDirectory":{"type":"string"},"literal":{"type":"string"},"pageSize":{"type":"integer","minimum":1,"maximum":64},"continuation":{"type":["string","null"]}},"required":["relativeDirectory","literal","pageSize"],"additionalProperties":false}
        """;
}
