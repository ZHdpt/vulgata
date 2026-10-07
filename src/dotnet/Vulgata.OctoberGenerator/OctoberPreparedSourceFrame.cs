using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.DomainServices;
using System.Security.Cryptography;
using System.Text;

namespace Vulgata.OctoberGenerator;

public enum OctoberSourceFrameStatus
{
    Acquired, InvalidInput, Denied, SourceChanged, Limited, Unsupported, Unavailable, Cancelled
}

public sealed record OctoberSourceFrameLimits(
    long MaximumFileBytes = 1048576,
    int PageByteCount = 65536,
    int MaximumGrantRenewals = 1);

public sealed record OctoberSourceFrameResult(
    OctoberSourceFrameStatus Status,
    OctoberPreparedSourceFrame? Frame = null);

/// <summary>Exact text acquired by the host through contained source grants; not a model-created DTO.</summary>
public sealed class OctoberPreparedSourceFrame : IOctoberVerifiedFileFrame
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly OctoberToolRuntime _runtime;
    private readonly OctoberSourceFrameLimits _limits;

    internal OctoberPreparedSourceFrame(PreparedOrigin origin, string text, long byteLength,
        int lineCount, string fileIdentity, OctoberToolRuntime runtime, OctoberSourceFrameLimits limits)
    {
        Origin = origin;
        Text = text;
        ByteLength = byteLength;
        LineCount = lineCount;
        FileIdentity = fileIdentity;
        _runtime = runtime;
        _limits = limits;
    }

    public PreparedOrigin Origin { get; }
    public string Text { get; }
    public long ByteLength { get; }
    public int LineCount { get; }
    public string FileIdentity { get; }

    public Task<bool> VerifyCurrentAsync(CancellationToken cancellationToken = default)
        => _runtime.VerifyFrameAsync(this, _limits, cancellationToken);

    internal static bool Valid(string relativePath, OctoberSourceFrameLimits limits)
        => !string.IsNullOrWhiteSpace(relativePath) && relativePath.Length <= 2048 &&
            !relativePath.Contains('\\') && !relativePath.Contains(':') && !relativePath.Contains('\0') &&
            relativePath.Split('/').All(part => part is not ("" or "." or "..")) &&
            limits.MaximumFileBytes is >= 0 and <= 4 * 1024 * 1024 &&
            limits.PageByteCount is >= 4 and <= PreparedSourceCallLimits.MaximumReadBytes &&
            limits.MaximumGrantRenewals is >= 0 and <= 7;

    internal static async Task<OctoberSourceFrameResult> AcquireAsync(OctoberToolRuntime runtime,
        PreparedSourceApproval approval, string relativePath, OctoberSourceFrameLimits limits,
        Func<CancellationToken, Task<PreparedSourceIssueResult>> openSource,
        Func<IPreparedSourceReadContext, string, CancellationToken, Task<string?>> observeIdentity,
        CancellationToken cancellationToken)
    {
        await using var reader = new FrameReader(approval.RepositoryId, relativePath, limits, openSource, observeIdentity);
        try
        {
            byte[] first = await reader.ReadPassAsync(cancellationToken);
            byte[] second = await reader.ReadPassAsync(cancellationToken);
            // Metadata is only observational: a same-length write with restored timestamps
            // must not turn mixed first-pass chunks into an acquired source frame.
            if (!first.AsSpan().SequenceEqual(second)) return new(OctoberSourceFrameStatus.SourceChanged);
            await reader.VerifyIdentityAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            string text = Utf8.GetString(first);
            // ApprovalIdentity labels this root/session provenance, NOT a Source Snapshot
            // or source revision. Exact retained bytes are bound independently by their
            // digest and pinned file identity. Another approval requires a new attempt.
            var origin = new PreparedOrigin(approval.RepositoryId, approval.RootBindingIdentity,
                relativePath, Convert.ToHexString(SHA256.HashData(first)), approval.ApprovalIdentity);
            return new(OctoberSourceFrameStatus.Acquired,
                new OctoberPreparedSourceFrame(origin, text, first.Length, CountLines(text), reader.Identity!, runtime, limits));
        }
        catch (FrameFailure failure) { return new(failure.Status); }
        catch (OperationCanceledException) { return new(OctoberSourceFrameStatus.Cancelled); }
        catch (DecoderFallbackException) { return new(OctoberSourceFrameStatus.Unsupported); }
        catch (EncoderFallbackException) { return new(OctoberSourceFrameStatus.Unsupported); }
    }

    private static int CountLines(string text)
    {
        if (text.Length == 0) return 0;
        int count = 1;
        for (int i = 0; i < text.Length; i++)
            if (text[i] is '\r' or '\n')
            {
                count++;
                if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
            }
        return count;
    }

    private sealed class FrameFailure(OctoberSourceFrameStatus status) : Exception("Contained source frame unavailable.")
    {
        internal OctoberSourceFrameStatus Status { get; } = status;
    }

    private sealed class FrameReader(Guid repositoryId, string path, OctoberSourceFrameLimits limits,
        Func<CancellationToken, Task<PreparedSourceIssueResult>> openSource,
        Func<IPreparedSourceReadContext, string, CancellationToken, Task<string?>> observeIdentity) : IAsyncDisposable
    {
        private IPreparedSourceReadContext? _context;
        private int _grants;
        private long? _length;
        internal string? Identity { get; private set; }

        private async Task OpenAsync(CancellationToken token)
        {
            if (_grants >= limits.MaximumGrantRenewals + 1) throw new FrameFailure(OctoberSourceFrameStatus.Limited);
            if (_context is not null)
            {
                await _context.DisposeAsync();
                _context = null;
            }
            token.ThrowIfCancellationRequested();
            _grants++;
            PreparedSourceIssueResult issued = await openSource(token);
            _context = issued.Context;
            if (issued.Status != PreparedSourceIssueStatus.Issued || _context is null || _context.RepositoryId != repositoryId)
                throw new FrameFailure(issued.Status switch
                {
                    PreparedSourceIssueStatus.Denied => OctoberSourceFrameStatus.Denied,
                    PreparedSourceIssueStatus.Cancelled => OctoberSourceFrameStatus.Cancelled,
                    PreparedSourceIssueStatus.DeadlineExceeded => OctoberSourceFrameStatus.Limited,
                    _ => OctoberSourceFrameStatus.Unavailable
                });
            await VerifyIdentityAsync(token);
        }

        internal async Task VerifyIdentityAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (_context is null) throw new FrameFailure(OctoberSourceFrameStatus.Unavailable);
            string? observed = await observeIdentity(_context, path, token);
            if (string.IsNullOrWhiteSpace(observed))
                throw new FrameFailure(_context.Lifetime switch
                {
                    PreparedSourceContextLifetime.AuthorityClosed => OctoberSourceFrameStatus.Denied,
                    PreparedSourceContextLifetime.ExpiredOrCancelled => OctoberSourceFrameStatus.Limited,
                    _ => OctoberSourceFrameStatus.Unavailable
                });
            if (Identity is not null && !string.Equals(Identity, observed, StringComparison.Ordinal))
                throw new FrameFailure(OctoberSourceFrameStatus.SourceChanged);
            Identity = observed;
        }

        internal async Task<byte[]> ReadPassAsync(CancellationToken token)
        {
            if (_context is null) await OpenAsync(token);
            else await VerifyIdentityAsync(token);
            using var content = new MemoryStream();
            long offset = 0;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                int requested = _length is { } known && offset < known
                    ? (int)Math.Min(limits.PageByteCount, known - offset) : limits.PageByteCount;
                PreparedSourceResult result;
                int shrink = 0;
                while (true)
                {
                    result = await _context!.ReadTextRangeAsync(new(path, offset, requested), token);
                    if (result.Outcome != PreparedSourceOutcome.InvalidRange || shrink == 3 || requested <= 1) break;
                    requested--;
                    shrink++;
                }
                if (result.RepositoryId != repositoryId || result.Operation != PreparedSourceOperation.ReadTextRange)
                    throw new FrameFailure(OctoberSourceFrameStatus.Denied);
                if (result.Outcome == PreparedSourceOutcome.DeadlineExceeded ||
                    result.Outcome == PreparedSourceOutcome.Limited && result.Text is null &&
                    result.StoppingLimit is PreparedSourceStoppingLimit.GrantOperations or PreparedSourceStoppingLimit.GrantExaminedBytes or PreparedSourceStoppingLimit.GrantReturnedBytes)
                {
                    await OpenAsync(token);
                    continue;
                }
                if (result.Outcome is not (PreparedSourceOutcome.Complete or PreparedSourceOutcome.CompleteEmpty or PreparedSourceOutcome.Limited))
                    throw new FrameFailure(result.Outcome switch
                    {
                        PreparedSourceOutcome.ChangedInput => OctoberSourceFrameStatus.SourceChanged,
                        PreparedSourceOutcome.Denied => OctoberSourceFrameStatus.Denied,
                        PreparedSourceOutcome.UnsupportedText or PreparedSourceOutcome.InvalidRange => OctoberSourceFrameStatus.Unsupported,
                        PreparedSourceOutcome.Cancelled => OctoberSourceFrameStatus.Cancelled,
                        _ => OctoberSourceFrameStatus.Unavailable
                    });
                if (result.ObservedFileByteLength is not { } observedLength || observedLength < 0 ||
                    result.ReadLocation is not { } location || location.RepositoryId != repositoryId ||
                    location.RelativePath != path || location.ByteStart != offset || result.Text is null)
                    throw new FrameFailure(OctoberSourceFrameStatus.Denied);
                if (observedLength > limits.MaximumFileBytes) throw new FrameFailure(OctoberSourceFrameStatus.Limited);
                if (_length is not null && _length != observedLength) throw new FrameFailure(OctoberSourceFrameStatus.SourceChanged);
                _length = observedLength;
                byte[] bytes = Utf8.GetBytes(result.Text);
                if (bytes.Length > requested || location.ByteEndExclusive != offset + bytes.Length ||
                    location.ByteEndExclusive > observedLength || bytes.Length == 0 && offset != observedLength)
                    throw new FrameFailure(OctoberSourceFrameStatus.Denied);
                if (result.Outcome == PreparedSourceOutcome.CompleteEmpty && (bytes.Length != 0 || offset != observedLength))
                    throw new FrameFailure(OctoberSourceFrameStatus.Denied);
                content.Write(bytes);
                offset += bytes.Length;
                if (offset == observedLength)
                {
                    await VerifyIdentityAsync(token);
                    return content.ToArray();
                }
                if (result.Outcome == PreparedSourceOutcome.Limited) await OpenAsync(token);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_context is not null) await _context.DisposeAsync();
        }
    }
}
