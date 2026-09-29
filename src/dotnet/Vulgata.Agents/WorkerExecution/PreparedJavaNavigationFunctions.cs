using System.Text.Json;
using Microsoft.Extensions.AI;
using Vulgata.Core.DomainServices;

namespace Vulgata.Agents.WorkerExecution;

/// <summary>
/// Borrows one already-issued navigation context for exactly two Worker calls.
/// The trusted caller, not the functions, owns the context lifetime.
/// </summary>
public sealed class PreparedJavaNavigationFunctions
{
    private readonly IPreparedJavaNavigationContext _context;

    public PreparedJavaNavigationFunctions(IPreparedJavaNavigationContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public IReadOnlyList<AIFunction> Create() =>
    [
        new NavigationFunction(_context, PreparedJavaNavigationOperation.Definition),
        new NavigationFunction(_context, PreparedJavaNavigationOperation.References),
    ];

    private sealed class NavigationFunction : AIFunction
    {
        private readonly IPreparedJavaNavigationContext _context;
        private readonly PreparedJavaNavigationOperation _operation;
        private readonly JsonElement _schema;

        internal NavigationFunction(
            IPreparedJavaNavigationContext context,
            PreparedJavaNavigationOperation operation)
        {
            _context = context;
            _operation = operation;
            using JsonDocument document = JsonDocument.Parse(Schema);
            _schema = document.RootElement.Clone();
        }

        public override string Name => _operation == PreparedJavaNavigationOperation.Definition
            ? "navigate_java_definition"
            : "navigate_java_references";

        public override string Description => _operation == PreparedJavaNavigationOperation.Definition
            ? "Find observed definitions within the approved prepared Java Repository."
            : "Find observed references within the approved prepared Java Repository.";

        public override JsonElement JsonSchema => _schema;

        public override JsonElement? ReturnJsonSchema => null;

        protected override async ValueTask<object?> InvokeCoreAsync(
            AIFunctionArguments arguments,
            CancellationToken cancellationToken)
        {
            if (!arguments.Keys.All(key => key is "relativePath" or "line" or "character" or "maximumLocations") ||
                !TryString(arguments, "relativePath", out string relativePath) ||
                !CanonicalJavaPath(relativePath) ||
                !TryInt32(arguments, "line", out int line) || line < 1 ||
                !TryInt32(arguments, "character", out int character) || character < 1 ||
                !TryInt32(arguments, "maximumLocations", out int maximumLocations) ||
                maximumLocations is < 1 or > 100)
            {
                return Empty(PreparedJavaNavigationOutcome.Denied);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Empty(PreparedJavaNavigationOutcome.Cancelled);
            }

            var request = new PreparedJavaNavigationRequest(
                relativePath, line, character, maximumLocations);
            try
            {
                PreparedJavaNavigationResult result = _operation == PreparedJavaNavigationOperation.Definition
                    ? await _context.GetDefinitionAsync(request, cancellationToken)
                    : await _context.GetReferencesAsync(request, cancellationToken);
                return cancellationToken.IsCancellationRequested
                    ? Empty(PreparedJavaNavigationOutcome.Cancelled)
                    : result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Empty(PreparedJavaNavigationOutcome.Cancelled);
            }
            catch (Exception)
            {
                return Empty(PreparedJavaNavigationOutcome.ProtocolFailed);
            }
        }

        private PreparedJavaNavigationResult Empty(PreparedJavaNavigationOutcome outcome) =>
            new(_context.RepositoryId, _operation, outcome, [], [], null);

        private static bool CanonicalJavaPath(string path) =>
            path.EndsWith(".java", StringComparison.OrdinalIgnoreCase) &&
            path.Length > 5 &&
            !path.StartsWith('/') &&
            !path.EndsWith('/') &&
            !path.Contains('\\') &&
            !path.Contains(':') &&
            path.Split('/').All(component =>
                component.Length is > 0 and <= 255 &&
                component is not "." and not ".." &&
                !component.EndsWith('.') &&
                !component.EndsWith(' ') &&
                !component.Any(character => character < ' ' ||
                    character is '<' or '>' or ':' or '"' or '\\' or '/' or '|' or '?' or '*'));

        private static bool TryString(
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

        private static bool TryInt32(
            AIFunctionArguments arguments,
            string name,
            out int value)
        {
            value = 0;
            if (!arguments.TryGetValue(name, out object? raw))
            {
                return false;
            }

            return raw switch
            {
                int integer => Assign(integer, out value),
                long integer when integer is >= int.MinValue and <= int.MaxValue =>
                    Assign((int)integer, out value),
                JsonElement { ValueKind: JsonValueKind.Number } element =>
                    element.TryGetInt32(out value),
                _ => false,
            };
        }

        private static bool Assign(int parsed, out int value)
        {
            value = parsed;
            return true;
        }
    }

    private const string Schema = """
        {"type":"object","properties":{"relativePath":{"type":"string"},"line":{"type":"integer","minimum":1},"character":{"type":"integer","minimum":1},"maximumLocations":{"type":"integer","minimum":1,"maximum":100}},"required":["relativePath","line","character","maximumLocations"],"additionalProperties":false}
        """;
}
