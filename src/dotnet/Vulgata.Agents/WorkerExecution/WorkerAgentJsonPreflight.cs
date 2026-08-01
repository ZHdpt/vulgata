using System.Text;
using System.Text.Json;
using Vulgata.Core.DomainServices;

namespace Vulgata.Agents.WorkerExecution;

internal static class WorkerAgentJsonPreflight
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    internal static void Validate(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.IsEmpty ||
            utf8Json.Length > WorkerAgentContractLimits.MaximumProviderResponseUtf8Bytes)
        {
            throw new InvalidDataException("Worker Agent JSON exceeds its finite byte bound.");
        }

        try
        {
            _ = _strictUtf8.GetCharCount(utf8Json);
        }
        catch (DecoderFallbackException exception)
        {
            throw new JsonException("Worker Agent JSON is not strict UTF-8.", exception);
        }

        var reader = new Utf8JsonReader(
            utf8Json,
            new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = WorkerAgentContractLimits.MaximumJsonDepth,
            });
        var objectProperties = new Stack<HashSet<string>>();
        bool rootObjectSeen = false;
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    if (reader.CurrentDepth == 0)
                    {
                        if (rootObjectSeen)
                        {
                            throw new JsonException("Worker Agent JSON has trailing content.");
                        }

                        rootObjectSeen = true;
                    }

                    objectProperties.Push(new(StringComparer.Ordinal));
                    break;
                case JsonTokenType.PropertyName:
                    if (objectProperties.Count == 0 ||
                        !objectProperties.Peek().Add(reader.GetString()!))
                    {
                        throw new JsonException("Worker Agent JSON contains a duplicate property.");
                    }

                    break;
                case JsonTokenType.EndObject:
                    objectProperties.Pop();
                    break;
                case JsonTokenType.StartArray:
                case JsonTokenType.EndArray:
                case JsonTokenType.String:
                case JsonTokenType.Number:
                case JsonTokenType.True:
                case JsonTokenType.False:
                case JsonTokenType.Null:
                    if (reader.CurrentDepth == 0)
                    {
                        throw new JsonException("Worker Agent JSON root must be one object.");
                    }

                    break;
                default:
                    throw new JsonException("Worker Agent JSON contains an unsupported token.");
            }
        }

        if (!rootObjectSeen || objectProperties.Count != 0 || reader.BytesConsumed != utf8Json.Length)
        {
            throw new JsonException("Worker Agent JSON is incomplete or has trailing content.");
        }
    }
}
