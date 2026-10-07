using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Agents.ChatExecution.October;

/// <summary>Tracks consumed exact chunks only. This is not an all-contributor disclosure receipt.</summary>
public sealed class OctoberTurnReadLedger(OctoberChatTurnBinding binding)
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);
    private readonly object _gate = new();
    private readonly Dictionary<DocumentHandle, Dictionary<string, Chunk>> _reads = [];
    private readonly List<DocumentHandle> _reported = [];
    public OctoberChatTurnBinding Binding { get; } = binding;

    public bool Record(DocumentHandle request, ContinuationToken? requestedContinuation, ContentPage page)
    {
        if (request.BatchId != binding.Request.Publication.Knowledge.BatchId ||
            request.PublicationId != binding.Request.Publication.PublicationId ||
            request.LogicalDocumentId == Guid.Empty || request.DocumentVersionId == Guid.Empty ||
            page.Status != KnowledgeReadStatus.Success || page.Document?.Handle != request || page.Content is null ||
            requestedContinuation is not null && string.IsNullOrWhiteSpace(requestedContinuation.Value) ||
            page.Next is not null && string.IsNullOrWhiteSpace(page.Next.Value))
        {
            return false;
        }

        byte[] content;
        try { content = _strictUtf8.GetBytes(page.Content); }
        catch (ArgumentException) { return false; }
        if (content.Length > KnowledgeContract.MaximumContentBytes) return false;
        var chunk = new Chunk(page.Next?.Value, content.Length, Convert.ToHexString(SHA256.HashData(content)));
        string cursor = requestedContinuation?.Value ?? string.Empty;
        lock (_gate)
        {
            if (!_reads.TryGetValue(request, out Dictionary<string, Chunk>? chunks))
            {
                chunks = new(StringComparer.Ordinal);
                _reads.Add(request, chunks);
            }

            if (chunks.TryGetValue(cursor, out Chunk? previous)) return previous == chunk;
            chunks.Add(cursor, chunk);
            return true;
        }
    }

    /// <summary>Consumed-read validation only. A future disclosure check is required before emitting these references.</summary>
    public ImmutableArray<OctoberChatReference>? Report(IEnumerable<DocumentHandle> documents)
    {
        lock (_gate)
        {
            DocumentHandle[] requested = documents.ToArray();
            if (requested.Any(document => document is null || !_reads.ContainsKey(document))) return null;
            foreach (DocumentHandle document in requested)
            {
                if (!_reported.Contains(document)) _reported.Add(document);
            }

            return _reported.Select(Project).ToImmutableArray();
        }
    }

    /// <summary>All successful consumed targets, not the authorized answer footer.</summary>
    public ImmutableArray<OctoberChatReference> Snapshot
    {
        get { lock (_gate) return _reads.Keys.Select(Project).ToImmutableArray(); }
    }

    private OctoberChatReference Project(DocumentHandle document)
    {
        Dictionary<string, Chunk> chunks = _reads[document];
        string? cursor = string.Empty;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        bool complete = false;
        while (cursor is not null && visited.Add(cursor) && chunks.TryGetValue(cursor, out Chunk? chunk))
        {
            cursor = chunk.Next;
            complete = cursor is null;
        }

        return new(document, !complete, chunks.Count, chunks.Values.Sum(chunk => (long)chunk.Bytes));
    }

    private sealed record Chunk(string? Next, int Bytes, string Digest);
}
