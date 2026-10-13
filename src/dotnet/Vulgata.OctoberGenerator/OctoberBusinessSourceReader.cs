using System.Security.Cryptography;
using System.Text;
using Vulgata.Agents.OctoberBusiness;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.OctoberGenerator;

/// <summary>A trusted, exact-source grant owned by one Business retrieval call.</summary>
public sealed class OctoberBusinessSourceGrant(
    IPreparedSourceReadContext context,
    Func<CancellationToken, Task<DisclosureStatus>> verifyCurrentAsync,
    Func<CancellationToken, Task<string?>> observeIdentityAsync,
    Func<ValueTask>? closeApproval = null) : IAsyncDisposable
{
    public IPreparedSourceReadContext Context { get; } = context ?? throw new ArgumentNullException(nameof(context));
    public Func<CancellationToken, Task<DisclosureStatus>> VerifyCurrentAsync { get; } = verifyCurrentAsync ?? throw new ArgumentNullException(nameof(verifyCurrentAsync));
    public Func<CancellationToken, Task<string?>> ObserveIdentityAsync { get; } = observeIdentityAsync ?? throw new ArgumentNullException(nameof(observeIdentityAsync));

    public async ValueTask DisposeAsync()
    {
        try { await Context.DisposeAsync(); }
        finally
        {
            if (closeApproval is not null) await closeApproval();
        }
    }
}

/// <summary>Verifies retained source bytes through contained grants before disclosing an exact line span.</summary>
public sealed class OctoberBusinessSourceReader(
    Func<SourceAnchor, CancellationToken, Task<OctoberBusinessSourceGrant?>> openGrant) : IOctoberBusinessSourceReader
{
    private const int MaximumFileBytes = 4 * 1024 * 1024;
    private const int MaximumPageBytes = 32 * 1024;
    private const long MaximumExaminedBytes = 16 * 1024 * 1024;
    // DecodeRange can inspect three preceding bytes and two four-byte scalar probes.
    // Charge this upper bound even on failed calls; it is not actual I/O telemetry.
    private const int BoundaryProbeBytes = 11;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly Func<SourceAnchor, CancellationToken, Task<OctoberBusinessSourceGrant?>> _openGrant =
        openGrant ?? throw new ArgumentNullException(nameof(openGrant));

    public async Task<BusinessSourcePage> ReadAsync(SourceAnchor source, int byteOffset, int maximumBytes,
        long maximumExaminedBytes, CancellationToken cancellationToken)
    {
        long charged = 0;
        BusinessSourcePage Failure(BusinessSourceStatus status) => new(status, source, byteOffset, null, null, charged);
        if (source is null || !OctoberKnowledgeInputPolicy.IsValidContentOrigin(source.Origin) ||
            source.Origin.RelativePath != OctoberKnowledgePolicy.NormalizePath(source.Origin.RelativePath) ||
            source.StartLine < 1 || source.EndLine < source.StartLine || byteOffset < 0)
            return Failure(BusinessSourceStatus.Denied);
        if (maximumBytes is < 1 or > MaximumPageBytes || maximumExaminedBytes <= BoundaryProbeBytes)
            return Failure(BusinessSourceStatus.BudgetExceeded);
        long budget = Math.Min(maximumExaminedBytes, MaximumExaminedBytes);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        var token = deadline.Token;
        OctoberBusinessSourceGrant? grant = null;
        try
        {
            token.ThrowIfCancellationRequested();
            grant = await _openGrant(source, token);
            if (grant is null) return Failure(BusinessSourceStatus.Unavailable);
            if (grant.Context.RepositoryId != source.Origin.RepositoryId)
                return Failure(BusinessSourceStatus.Denied);
            string? identity = null;
            long? length = null;

            async Task VerifyAsync()
            {
                token.ThrowIfCancellationRequested();
                var authority = await grant.VerifyCurrentAsync(token);
                token.ThrowIfCancellationRequested();
                if (authority != DisclosureStatus.Allowed)
                    throw new ReadFailure(authority == DisclosureStatus.Denied ? BusinessSourceStatus.Denied : BusinessSourceStatus.Unavailable);
                string? observed = await grant.ObserveIdentityAsync(token);
                token.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(observed))
                    throw new ReadFailure(grant.Context.Lifetime == PreparedSourceContextLifetime.AuthorityClosed
                        ? BusinessSourceStatus.Denied : BusinessSourceStatus.Unavailable);
                if (identity is not null && !string.Equals(identity, observed, StringComparison.Ordinal))
                    throw new ReadFailure(BusinessSourceStatus.Changed);
                identity = observed;
            }

            async Task<byte[]> ReadPassAsync()
            {
                using var content = new MemoryStream();
                long offset = 0;
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    long remaining = budget - charged - BoundaryProbeBytes;
                    if (remaining < 1) throw new ReadFailure(BusinessSourceStatus.BudgetExceeded);
                    int requested = (int)Math.Min(PreparedSourceCallLimits.MaximumReadBytes, remaining);
                    if (length is { } known && known > offset) requested = (int)Math.Min(requested, known - offset);
                    PreparedSourceResult result;
                    int retries = 0;
                    while (true)
                    {
                        token.ThrowIfCancellationRequested();
                        long cost = requested + BoundaryProbeBytes;
                        if (cost > budget - charged) throw new ReadFailure(BusinessSourceStatus.BudgetExceeded);
                        charged += cost;
                        result = await grant.Context.ReadTextRangeAsync(new(source.Origin.RelativePath, offset, requested), token);
                        token.ThrowIfCancellationRequested();
                        if (result is null || result.RepositoryId != source.Origin.RepositoryId ||
                            result.Operation != PreparedSourceOperation.ReadTextRange)
                            throw new ReadFailure(BusinessSourceStatus.Denied);
                        if (result.Outcome != PreparedSourceOutcome.InvalidRange || retries == 3 || requested <= 1) break;
                        requested--;
                        retries++;
                    }
                    if (result.Outcome is not (PreparedSourceOutcome.Complete or PreparedSourceOutcome.CompleteEmpty or PreparedSourceOutcome.Limited))
                        throw new ReadFailure(result.Outcome switch
                        {
                            PreparedSourceOutcome.ChangedInput => BusinessSourceStatus.Changed,
                            PreparedSourceOutcome.Denied => BusinessSourceStatus.Denied,
                            _ => BusinessSourceStatus.Unavailable,
                        });
                    if (result.Outcome == PreparedSourceOutcome.Limited && result.Text is null)
                        throw new ReadFailure(BusinessSourceStatus.BudgetExceeded);
                    if (result.ObservedFileByteLength is not { } observedLength || observedLength < 0 ||
                        result.ReadLocation is not { } location || location.RepositoryId != source.Origin.RepositoryId ||
                        location.RelativePath != source.Origin.RelativePath || location.ByteStart != offset || result.Text is null ||
                        result.Entries.Count != 0 || result.Matches.Count != 0 || result.Limitations.Count != 0 || result.Continuation is not null)
                        throw new ReadFailure(BusinessSourceStatus.Denied);
                    if (observedLength > MaximumFileBytes) throw new ReadFailure(BusinessSourceStatus.BudgetExceeded);
                    if (length is not null && length != observedLength) throw new ReadFailure(BusinessSourceStatus.Changed);
                    length = observedLength;
                    byte[] bytes = Utf8.GetBytes(result.Text);
                    if (bytes.Length > requested || location.ByteEndExclusive != offset + bytes.Length ||
                        location.ByteEndExclusive > observedLength || bytes.Length == 0 && offset != observedLength ||
                        result.Text.Contains('\0') || result.Text.Contains('\uFEFF') &&
                        (offset != 0 || result.Text.IndexOf('\uFEFF', 1) >= 0) ||
                        result.NextByteOffset is { } next && (next != location.ByteEndExclusive || next >= observedLength) ||
                        result.Outcome == PreparedSourceOutcome.CompleteEmpty && (bytes.Length != 0 || offset != observedLength) ||
                        result.Outcome != PreparedSourceOutcome.Limited && result.StoppingLimit is not null)
                        throw new ReadFailure(BusinessSourceStatus.Denied);
                    content.Write(bytes);
                    offset += bytes.Length;
                    if (offset == observedLength) return content.ToArray();
                    if (result.Outcome == PreparedSourceOutcome.Limited)
                        throw new ReadFailure(BusinessSourceStatus.BudgetExceeded);
                }
            }

            await VerifyAsync();
            byte[] first = await ReadPassAsync();
            await VerifyAsync();
            byte[] second = await ReadPassAsync();
            await VerifyAsync();
            if (!first.AsSpan().SequenceEqual(second) ||
                !string.Equals(Convert.ToHexString(SHA256.HashData(first)), source.Origin.SourceDigest, StringComparison.OrdinalIgnoreCase))
                return Failure(BusinessSourceStatus.Changed);

            var (start, end) = LineSpan(first, source.StartLine, source.EndLine);
            int spanLength = end - start;
            if (byteOffset > spanLength || !ScalarBoundary(first, start + byteOffset))
                return Failure(BusinessSourceStatus.Denied);
            int pageEnd = start + byteOffset + Math.Min(maximumBytes, spanLength - byteOffset);
            while (!ScalarBoundary(first, pageEnd)) pageEnd--;
            if (pageEnd == start + byteOffset && byteOffset < spanLength)
                return Failure(BusinessSourceStatus.BudgetExceeded);
            string text = Utf8.GetString(first, start + byteOffset, pageEnd - start - byteOffset);
            int consumed = pageEnd - start;
            await VerifyAsync();
            token.ThrowIfCancellationRequested();
            return new(consumed == spanLength ? BusinessSourceStatus.Complete : BusinessSourceStatus.Limited,
                source, byteOffset, consumed == spanLength ? null : consumed, text, charged);
        }
        catch (ReadFailure failure) { return Failure(failure.Status); }
        catch (Exception) { return Failure(BusinessSourceStatus.Unavailable); }
        finally
        {
            // Unconfirmed cleanup must stop the host flow, not become a retryable source page.
            if (grant is not null) await grant.DisposeAsync();
        }
    }

    private static bool ScalarBoundary(byte[] bytes, int position) =>
        position == bytes.Length || (bytes[position] & 0xC0) != 0x80;

    private static (int Start, int End) LineSpan(byte[] bytes, int startLine, int endLine)
    {
        if (bytes.Length == 0) throw new ReadFailure(BusinessSourceStatus.Denied);
        // Preserve original BOM, whitespace and line terminators. CRLF is one line break.
        int line = 1;
        int start = startLine == 1 ? 0 : -1;
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] is not (13 or 10)) continue;
            if (bytes[i] == 13 && i + 1 < bytes.Length && bytes[i + 1] == 10) i++;
            if (line == endLine) return (start, i + 1);
            line++;
            if (line == startLine) start = i + 1;
        }
        if (start < 0 || endLine != line) throw new ReadFailure(BusinessSourceStatus.Denied);
        return (start, bytes.Length);
    }

    private sealed class ReadFailure(BusinessSourceStatus status) : Exception("Referenced source is unavailable.")
    {
        internal BusinessSourceStatus Status { get; } = status;
    }
}
