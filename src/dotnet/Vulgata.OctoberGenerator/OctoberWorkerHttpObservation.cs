using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.OctoberGenerator;

internal sealed record OctoberWorkerHttpObservationOptions(string TargetId, Guid BatchId,
    string SubjectId, string ManagementAuthorityId);

/// <summary>Optional original-run observation of bytes already consumed by the SDK. No reads ahead or persistence.</summary>
internal sealed class OctoberWorkerHttpObservation(OctoberWorkerHttpObservationOptions options) : IOctoberWorkerHttpObservationClient
{
    private readonly Lock _sync = new();
    private OctoberGenerateExecution? _execution;
    private Guid _runId;
    private bool _consumed;
    private Leaf? _leaf;
    internal bool TryBind(OctoberGenerateExecution execution, Guid runId)
    {
        lock (_sync)
        {
            if (execution is null || runId == Guid.Empty || options.BatchId == Guid.Empty ||
                string.IsNullOrWhiteSpace(options.SubjectId) || string.IsNullOrWhiteSpace(options.ManagementAuthorityId) ||
                execution.Session.SubjectId != options.SubjectId || execution.Session.ManagementAuthorityId != options.ManagementAuthorityId ||
                execution.Authority.TargetId != options.TargetId || execution.Authority.BatchId != options.BatchId ||
                execution.Setup.TargetId != options.TargetId || execution.Setup.BatchId != options.BatchId) return false;
            if (_execution is not null) return ReferenceEquals(execution, _execution) && runId == _runId;
            _execution = execution; _runId = runId;
            return true;
        }
    }
    public void BeginLeaf(Guid runId, IOctoberProviderAttemptLease lease)
    {
        lock (_sync)
        {
            if (_execution is null || runId != _runId || _consumed) return;
            _leaf?.Clear();
            _leaf = new(lease);
        }
    }
    internal Leaf? For(IOctoberProviderAttemptLease lease)
    {
        lock (_sync) return _leaf is { } leaf && ReferenceEquals(leaf.Lease, lease) && !_consumed ? leaf : null;
    }
    public OctoberWorkerHttpObservationSnapshot? CompleteParseFailure(Guid runId, IOctoberProviderAttemptLease lease, string parseInput)
    {
        lock (_sync)
        {
            if (_execution is null || runId != _runId || _consumed || _leaf is not { } leaf || !ReferenceEquals(leaf.Lease, lease)) return null;
            _consumed = true;
            try { return leaf.Complete(parseInput); }
            catch (Exception) { return new(Status: OctoberWorkerHttpObservationStatus.Failed); }
            finally { leaf.Clear(); _leaf = null; }
        }
    }
    public void Discard(Guid runId)
    {
        lock (_sync) if (_runId == runId) { _leaf?.Clear(); _leaf = null; }
    }
    internal void Discard() { lock (_sync) { _leaf?.Clear(); _leaf = null; } }

    internal sealed class Leaf(IOctoberProviderAttemptLease lease)
    {
        private const int TextLimit = 262144, EventLimit = 524288, WireLimit = 4194304;
        private static readonly UTF8Encoding Utf8 = new(false, true);
        private readonly Lock _sync = new();
        private StringBuilder _http = new(), _sdk = new();
        private List<byte> _line = [];
        private StringBuilder _event = new();
        private int _eventBytes, _httpBytes, _sdkBytes;
        private long _wireBytes;
        private bool _cr, _done, _eof, _sdkComplete, _closed, _afterFinish, _afterDone, _partial, _wireOverflow;
        private OctoberWorkerHttpObservationStatus? _failure;
        private OctoberWorkerObservedFinish? _httpFinish, _sdkFinish;
        private long? _httpReasoning, _sdkReasoning, _cap;
        internal IOctoberProviderAttemptLease Lease { get; } = lease;
        internal void Cap(long maximum) { lock (_sync) if (!_closed) _cap = maximum; }
        internal void Sdk(ChatResponseUpdate update)
        {
            lock (_sync)
            {
                if (_closed || _failure is not null) return;
                try
                {
                    if (update.FinishReason is { } finish) _sdkFinish = Finish(finish.Value);
                    foreach (AIContent content in update.Contents)
                    {
                        if (content is TextContent text) Append(_sdk, text.Text, ref _sdkBytes);
                        else if (content is UsageContent usage) _sdkReasoning = usage.Details.ReasoningTokenCount;
                        else if (content is not TextReasoningContent) Fail(OctoberWorkerHttpObservationStatus.Unsupported);
                    }
                }
                catch (Exception) { Fail(OctoberWorkerHttpObservationStatus.Failed); }
            }
        }
        internal void SdkCompleted() { lock (_sync) if (!_closed) _sdkComplete = true; }
        internal void Read(ReadOnlySpan<byte> bytes)
        {
            lock (_sync)
            {
                if (_closed) return;
                try
                {
                    if (bytes.Length == 0) { _eof = true; _partial |= _line.Count != 0 || _event.Length != 0; return; }
                    if (bytes.Length > WireLimit - _wireBytes) { _wireOverflow = true; Fail(OctoberWorkerHttpObservationStatus.LimitExceeded); return; }
                    _wireBytes += bytes.Length;
                    if (_failure is not null) return;
                    foreach (byte value in bytes)
                    {
                        if (value == 10 && _cr) { _cr = false; continue; }
                        _cr = false;
                        if (++_eventBytes > EventLimit) { Fail(OctoberWorkerHttpObservationStatus.LimitExceeded); break; }
                        if (value is 10 or 13)
                        {
                            _cr = value == 13;
                            Line();
                        }
                        else _line.Add(value);
                        if (_failure is not null) break;
                    }
                }
                catch (Exception) { Fail(OctoberWorkerHttpObservationStatus.Failed); }
            }
        }
        private void Line()
        {
            string line = Utf8.GetString(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_line));
            _line = [];
            if (line.Length == 0)
            {
                if (_event.Length != 0) Event(_event.ToString());
                _event = new(); _eventBytes = 0;
            }
            else if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                if (_event.Length != 0) _event.Append('\n');
                _event.Append(line.AsSpan(line.Length > 5 && line[5] == ' ' ? 6 : 5));
            }
            else if (!line.StartsWith(':')) Fail(OctoberWorkerHttpObservationStatus.Unsupported);
        }
        private void Event(string data)
        {
            if (data == "[DONE]") { _done = true; return; }
            using JsonDocument document = JsonDocument.Parse(data, new JsonDocumentOptions { MaxDepth = 32 });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) { Fail(OctoberWorkerHttpObservationStatus.Unsupported); return; }
            if (root.TryGetProperty("usage", out JsonElement usage) && usage.ValueKind == JsonValueKind.Object &&
                usage.TryGetProperty("completion_tokens_details", out JsonElement details) && details.ValueKind == JsonValueKind.Object &&
                details.TryGetProperty("reasoning_tokens", out JsonElement reasoning) && reasoning.TryGetInt64(out long count) && count >= 0)
                _httpReasoning = count;
            if (!root.TryGetProperty("choices", out JsonElement choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() > 1)
            { Fail(OctoberWorkerHttpObservationStatus.Unsupported); return; }
            if (choices.GetArrayLength() == 0) return;
            JsonElement choice = choices[0];
            if (!choice.TryGetProperty("index", out JsonElement index) || !index.TryGetInt32(out int number) || number != 0 ||
                !choice.TryGetProperty("delta", out JsonElement delta) || delta.ValueKind != JsonValueKind.Object)
            { Fail(OctoberWorkerHttpObservationStatus.Unsupported); return; }
            if (delta.TryGetProperty("tool_calls", out _) || delta.TryGetProperty("function_call", out _))
            { Fail(OctoberWorkerHttpObservationStatus.Unsupported); return; }
            if (delta.TryGetProperty("content", out JsonElement content) && content.ValueKind != JsonValueKind.Null)
            {
                if (content.ValueKind != JsonValueKind.String) { Fail(OctoberWorkerHttpObservationStatus.Unsupported); return; }
                string text = content.GetString()!;
                if (text.Length != 0)
                {
                    if (_httpFinish is not null) _afterFinish = true;
                    if (_done) _afterDone = true;
                    Append(_http, text, ref _httpBytes);
                }
            }
            if (choice.TryGetProperty("finish_reason", out JsonElement finish) && finish.ValueKind != JsonValueKind.Null)
            {
                if (finish.ValueKind != JsonValueKind.String) { Fail(OctoberWorkerHttpObservationStatus.Unsupported); return; }
                _httpFinish = Finish(finish.GetString()!);
            }
        }
        private void Append(StringBuilder builder, string? value, ref int count)
        {
            if (value is null) return;
            int length = Utf8.GetByteCount(value);
            if (length > TextLimit - count) { Fail(OctoberWorkerHttpObservationStatus.LimitExceeded); return; }
            count += length; builder.Append(value);
        }
        private void Fail(OctoberWorkerHttpObservationStatus status)
        {
            _failure ??= status;
            _line = []; _event = new(); _http = new(); _sdk = new();
        }
        internal void ReleaseStreamBuffers() { lock (_sync) { _partial |= _line.Count != 0 || _event.Length != 0; _line = []; _event = new(); } }
        internal void Clear() { lock (_sync) { _closed = true; _line = []; _event = new(); _http = new(); _sdk = new(); } }
        internal OctoberWorkerHttpObservationSnapshot Complete(string parse)
        {
            lock (_sync)
            {
                int length = Utf8.GetByteCount(parse);
                if (length > TextLimit) Fail(OctoberWorkerHttpObservationStatus.LimitExceeded);
                bool sufficient = _failure is null && !_partial && _sdkComplete && (_done || _eof);
                string? http = sufficient ? _http.ToString() : null, sdk = sufficient ? _sdk.ToString() : null;
                return new(Status: _failure ?? (sufficient ? OctoberWorkerHttpObservationStatus.Complete : OctoberWorkerHttpObservationStatus.Incomplete),
                    CapKey: _cap.HasValue ? OctoberWorkerTokenCapKey.MaxTokens : null, CapValue: _cap,
                    HttpFinish: _httpFinish, SdkFinish: _sdkFinish, DoneSeen: _done ? true : _failure is null && !_partial ? false : null, EofSeen: _eof,
                    ContentAfterFinish: _afterFinish ? true : _failure is null && !_partial && _eof ? false : null,
                    ContentAfterDone: _afterDone ? true : _failure is null && !_partial && _eof ? false : null, WireBytes: _wireOverflow ? null : _wireBytes,
                    HttpTextUtf8Bytes: sufficient ? _httpBytes : null, SdkTextUtf8Bytes: sufficient ? _sdkBytes : null,
                    ParseInputUtf8Bytes: length <= TextLimit ? length : null,
                    HttpEqualsSdk: sufficient ? http == sdk : null, SdkEqualsParse: sufficient ? sdk == parse : null,
                    HttpJson: sufficient ? Classify(http!) : null, SdkJson: sufficient ? Classify(sdk!) : null,
                    ParseJson: sufficient ? Classify(parse) : null, HttpReasoningTokens: _httpReasoning, SdkReasoningTokens: _sdkReasoning);
            }
        }
        private static OctoberWorkerObservedFinish Finish(string value) => value switch
        {
            "stop" => OctoberWorkerObservedFinish.Stop, "length" => OctoberWorkerObservedFinish.Length,
            "tool_calls" => OctoberWorkerObservedFinish.ToolCalls, _ => OctoberWorkerObservedFinish.Other
        };
        private static OctoberWorkerJsonObservation Classify(string text)
        {
            var stack = new List<char>();
            bool quoted = false, escape = false, mismatch = false, depth = false;
            foreach (char value in text)
            {
                if (quoted)
                {
                    if (escape) escape = false;
                    else if (value == '\\') escape = true;
                    else if (value == '"') quoted = false;
                    continue;
                }
                if (value == '"') quoted = true;
                else if (value is '{' or '[') { if (stack.Count == 32) { depth = true; break; } stack.Add(value); }
                else if (value is '}' or ']')
                {
                    if (stack.Count == 0 || stack[^1] != (value == '}' ? '{' : '[')) { mismatch = true; break; }
                    stack.RemoveAt(stack.Count - 1);
                }
            }
            int objects = stack.Count(c => c == '{'), arrays = stack.Count - objects;
            bool? atEof = null;
            try
            {
                using var parsed = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 32 });
                return new(parsed.RootElement.ValueKind == JsonValueKind.Object ? OctoberWorkerJsonObservationKind.ValidObject : OctoberWorkerJsonObservationKind.ValidNonObject, 0, 0, true);
            }
            catch (JsonException error)
            {
                // Only numeric parser coordinates are inspected, never diagnostic text or paths.
                if (error.LineNumber is { } lineNumber && error.BytePositionInLine is { } column)
                {
                    int start = 0;
                    long lineNumberSeen = 0;
                    for (int i = 0; i < text.Length && lineNumberSeen < lineNumber; i++)
                        if (text[i] == '\n') { lineNumberSeen++; start = i + 1; }
                    if (lineNumberSeen == lineNumber)
                        atEof = Utf8.GetByteCount(text.AsSpan(0, start)) + column == Utf8.GetByteCount(text);
                }
            }
            var kind = quoted ? OctoberWorkerJsonObservationKind.UnterminatedString : mismatch ? OctoberWorkerJsonObservationKind.MismatchedDelimiter : OctoberWorkerJsonObservationKind.InvalidSyntax;
            if (!quoted && !mismatch && !depth && stack.Count > 0)
            {
                string closers = new(stack.AsEnumerable().Reverse().Select(c => c == '{' ? '}' : ']').ToArray());
                try { using var repaired = JsonDocument.Parse(text + closers, new JsonDocumentOptions { MaxDepth = 32 }); kind = OctoberWorkerJsonObservationKind.MissingClosingDelimiters; }
                catch (JsonException) { }
            }
            if (depth)
            {
                // Establish that a deeper, still bounded parse succeeds before attributing failure to depth.
                try { using var deeper = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 128 }); kind = OctoberWorkerJsonObservationKind.DepthExceeded; }
                catch (JsonException) { }
            }
            return new(kind, depth ? null : objects, depth ? null : arrays, atEof);
        }
    }

    internal static HttpContent Wrap(HttpContent original, Leaf leaf) => new ObservedContent(original, leaf);
    private sealed class ObservedContent : HttpContent
    {
        private readonly HttpContent _original;
        private readonly Leaf _leaf;
        internal ObservedContent(HttpContent original, Leaf leaf)
        {
            _original = original; _leaf = leaf;
            foreach (var header in original.Headers) Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        protected override bool TryComputeLength(out long length) { length = _original.Headers.ContentLength ?? 0; return _original.Headers.ContentLength.HasValue; }
        protected override async Task<Stream> CreateContentReadStreamAsync() => new ObservedStream(await _original.ReadAsStreamAsync(), _leaf);
        protected override async Task<Stream> CreateContentReadStreamAsync(CancellationToken token) => new ObservedStream(await _original.ReadAsStreamAsync(token), _leaf);
        protected override Stream CreateContentReadStream(CancellationToken token) => new ObservedStream(_original.ReadAsStream(token), _leaf);
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        { using Stream source = await CreateContentReadStreamAsync(); await source.CopyToAsync(stream, 65536); }
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken token)
        { using Stream source = await CreateContentReadStreamAsync(token); await source.CopyToAsync(stream, 65536, token); }
        protected override void Dispose(bool disposing) { if (disposing) { _leaf.ReleaseStreamBuffers(); _original.Dispose(); } base.Dispose(disposing); }
    }
    private sealed class ObservedStream(Stream inner, Leaf leaf) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) { int n = inner.Read(buffer, offset, count); if (count > 0) leaf.Read(buffer.AsSpan(offset, n)); return n; }
        public override int Read(Span<byte> buffer) { int n = inner.Read(buffer); if (buffer.Length > 0) leaf.Read(buffer[..n]); return n; }
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
        { int n = await inner.ReadAsync(buffer, offset, count, token); if (count > 0) leaf.Read(buffer.AsSpan(offset, n)); return n; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        { int n = await inner.ReadAsync(buffer, token); if (buffer.Length > 0) leaf.Read(buffer.Span[..n]); return n; }
        public override void Flush() => inner.Flush();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) { leaf.ReleaseStreamBuffers(); inner.Dispose(); } base.Dispose(disposing); }
        public override async ValueTask DisposeAsync() { leaf.ReleaseStreamBuffers(); await inner.DisposeAsync(); GC.SuppressFinalize(this); }
    }
}
