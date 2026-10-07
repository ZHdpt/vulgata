using System.Text;
using System.Text.Json;

namespace Vulgata.Agents.ChatExecution.October;

internal sealed class OctoberChatTransportException(bool budgetExceeded = false)
    : IOException("The controlled Chat stream was rejected.")
{
    internal bool BudgetExceeded { get; } = budgetExceeded;
}

/// <summary>One bounded SSE event and bounded native-call fragments, never a complete answer.</summary>
internal sealed class OctoberChatSseValidator(OctoberChatBudget budget)
{
    private static readonly UTF8Encoding _utf8 = new(false, true);
    private readonly List<byte> _line = [];
    private readonly StringBuilder _data = new();
    private readonly Dictionary<int, NativeCall> _calls = [];
    private readonly int _remainingCalls = budget.Limits.ToolCalls - budget.ToolCalls;
    private bool _hasData;
    private bool _afterCr;
    private bool _done;
    private bool _finished;
    private int _invalidAttempts;
    private string? _responseId;

    internal int ObservedAttempts => _calls.Count + _invalidAttempts;

    internal byte[]? Accept(byte value)
    {
        try
        {
            if (_done)
            {
                if (value is not (9 or 10 or 13 or 32)) throw new OctoberChatTransportException();
                return null;
            }

            if (_afterCr)
            {
                _afterCr = false;
                if (value == 10) return null;
            }

            if (value is 10 or 13)
            {
                _afterCr = value == 13;
                string line = _utf8.GetString(_line.ToArray());
                _line.Clear();
                if (line.Length == 0) return EndEvent();
                if (line[0] == ':') return null;
                int separator = line.IndexOf(':');
                string field = separator < 0 ? line : line[..separator];
                string text = separator < 0 ? string.Empty : line[(separator + 1)..];
                if (text.StartsWith(' ')) text = text[1..];
                if (field == "data")
                {
                    if (_hasData) _data.Append('\n');
                    _data.Append(text);
                    _hasData = true;
                }
                else if (field != "event" || text != "message") throw new OctoberChatTransportException();
                return null;
            }

            _line.Add(value);
            return null;
        }
        catch (DecoderFallbackException) { throw new OctoberChatTransportException(); }
    }

    internal byte[] Complete()
    {
        if (!_done || !_finished || _line.Count != 0 || _hasData) throw new OctoberChatTransportException();
        return "data: [DONE]\n\n"u8.ToArray();
    }

    private byte[]? EndEvent()
    {
        if (!_hasData) return null;
        string data = _data.ToString();
        _data.Clear();
        _hasData = false;
        if (data == "[DONE]")
        {
            if (!_finished) throw new OctoberChatTransportException();
            _done = true;
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(data, new JsonDocumentOptions { MaxDepth = 32 });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || HasDuplicateProperties(root)) throw new OctoberChatTransportException();
            if (root.TryGetProperty("id", out JsonElement id))
            {
                string? value = id.GetString();
                if (string.IsNullOrWhiteSpace(value) || _responseId is not null && value != _responseId)
                    throw new OctoberChatTransportException();
                _responseId = value;
            }

            JsonElement choices = root.GetProperty("choices");
            if (choices.ValueKind != JsonValueKind.Array) throw new OctoberChatTransportException();
            if (choices.GetArrayLength() == 0)
            {
                if (!_finished || !root.TryGetProperty("usage", out JsonElement usage) || usage.ValueKind != JsonValueKind.Object)
                    throw new OctoberChatTransportException();
            }
            else
            {
                if (_finished || choices.GetArrayLength() != 1) throw new OctoberChatTransportException();
                JsonElement choice = choices[0];
                if (choice.GetProperty("index").GetInt32() != 0) throw new OctoberChatTransportException();
                JsonElement delta = choice.GetProperty("delta");
                if (delta.ValueKind != JsonValueKind.Object) throw new OctoberChatTransportException();
                if (delta.TryGetProperty("role", out JsonElement role) && role.GetString() != "assistant")
                    throw new OctoberChatTransportException();
                if (delta.TryGetProperty("content", out JsonElement content) && content.ValueKind is not (JsonValueKind.Null or JsonValueKind.String))
                    throw new OctoberChatTransportException();
                if (delta.TryGetProperty("function_call", out _)) throw new OctoberChatTransportException();
                if (delta.TryGetProperty("tool_calls", out JsonElement calls)) AcceptCalls(calls);
                if (choice.TryGetProperty("finish_reason", out JsonElement finish) && finish.ValueKind != JsonValueKind.Null)
                    Finish(finish.GetString());
            }

            return _utf8.GetBytes("data: " + data + "\n\n");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException or EncoderFallbackException)
        {
            throw new OctoberChatTransportException();
        }
    }

    private void AcceptCalls(JsonElement calls)
    {
        if (calls.ValueKind != JsonValueKind.Array)
        {
            _invalidAttempts++;
            throw new OctoberChatTransportException();
        }

        foreach (JsonElement fragment in calls.EnumerateArray())
        {
            if (fragment.ValueKind != JsonValueKind.Object || !fragment.TryGetProperty("index", out JsonElement indexValue) ||
                indexValue.ValueKind != JsonValueKind.Number || !indexValue.TryGetInt32(out int index) || index < 0)
            {
                _invalidAttempts++;
                throw new OctoberChatTransportException();
            }

            if (!_calls.TryGetValue(index, out NativeCall? call))
            {
                call = new();
                _calls.Add(index, call);
                if (_calls.Count > _remainingCalls) throw new OctoberChatTransportException(true);
            }

            if (fragment.TryGetProperty("type", out JsonElement type) && type.GetString() != "function")
                throw new OctoberChatTransportException();
            if (fragment.TryGetProperty("id", out JsonElement id))
            {
                string? value = id.GetString();
                if (string.IsNullOrWhiteSpace(value) || call.Id is not null && call.Id != value ||
                    _calls.Any(other => other.Key != index && other.Value.Id == value)) throw new OctoberChatTransportException();
                call.Id = value;
            }

            if (!fragment.TryGetProperty("function", out JsonElement function)) continue;
            if (function.ValueKind != JsonValueKind.Object) throw new OctoberChatTransportException();
            if (function.TryGetProperty("name", out JsonElement name))
            {
                string? value = name.GetString();
                if (value is null || !OctoberChatProviderAdapter.ToolNames.Contains(value, StringComparer.Ordinal) ||
                    call.Name is not null && call.Name != value) throw new OctoberChatTransportException();
                call.Name = value;
            }

            if (function.TryGetProperty("arguments", out JsonElement arguments))
            {
                string? fragmentText = arguments.GetString();
                if (fragmentText is null) throw new OctoberChatTransportException();
                int bytes = _utf8.GetByteCount(fragmentText);
                if (bytes > budget.Limits.ToolArgumentBytes - call.ArgumentBytes) throw new OctoberChatTransportException(true);
                call.ArgumentBytes += bytes;
                call.Arguments.Append(fragmentText);
            }
        }
    }

    private void Finish(string? reason)
    {
        if (reason is not ("stop" or "tool_calls" or "length" or "content_filter") ||
            reason == "tool_calls" && _calls.Count == 0 || reason != "tool_calls" && _calls.Count != 0)
            throw new OctoberChatTransportException();
        foreach (NativeCall call in _calls.Values)
        {
            if (call.Id is null || call.Name is null) throw new OctoberChatTransportException();
            using JsonDocument arguments = JsonDocument.Parse(call.Arguments.ToString(), new JsonDocumentOptions { MaxDepth = 16 });
            // The SDK deserializes arguments before the facade receives them. Reject
            // duplicate keys here before that normalization can discard evidence;
            // argument binding is case-insensitive throughout the facade.
            if (arguments.RootElement.ValueKind != JsonValueKind.Object ||
                HasDuplicateProperties(arguments.RootElement, StringComparer.OrdinalIgnoreCase))
                throw new OctoberChatTransportException();
        }
        _finished = true;
    }

    private static bool HasDuplicateProperties(JsonElement element, StringComparer? comparer = null)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(comparer ?? StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
                if (!names.Add(property.Name) || HasDuplicateProperties(property.Value, comparer)) return true;
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (JsonElement value in element.EnumerateArray()) if (HasDuplicateProperties(value, comparer)) return true;
        return false;
    }

    private sealed class NativeCall
    {
        internal string? Id;
        internal string? Name;
        internal int ArgumentBytes;
        internal readonly StringBuilder Arguments = new();
    }
}
