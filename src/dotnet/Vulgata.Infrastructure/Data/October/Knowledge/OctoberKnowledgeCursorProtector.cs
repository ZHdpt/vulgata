using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

/// <summary>Host supplies authenticated encryption; neither a browser nor a model can issue authority.</summary>
public interface IOctoberKnowledgeTokenProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedText);
}

internal sealed record OctoberReadTicket(ReadSelectionRequest Selection, DateTimeOffset ExpiresAt, string Nonce);
internal sealed record OctoberReadPosition(string SelectionDigest, string Operation, string Query, int PageSize,
    DocumentHandle? Handle, string OrderDigest, int Offset, DateTimeOffset ExpiresAt);
internal sealed class OctoberInvalidContinuationException : Exception { }

/// <summary>Authenticated opaque positions grant no permission. Every reader call rechecks live authority.</summary>
public sealed class OctoberKnowledgeCursorProtector
{
    private readonly IOctoberKnowledgeTokenProtector _protector;
    private readonly TimeProvider _clock;
    public OctoberKnowledgeCursorProtector(IOctoberKnowledgeTokenProtector protector, TimeProvider? timeProvider = null)
    {
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        _clock = timeProvider ?? TimeProvider.System;
    }

    internal DateTimeOffset Now => _clock.GetUtcNow();
    internal ReadSelection Issue(ReadSelectionRequest selection) => new(Protect("selection",
        new OctoberReadTicket(selection, Now + KnowledgeContract.ContinuationLifetime, Guid.NewGuid().ToString("N"))));
    internal OctoberReadTicket Read(ReadSelection selection)
    {
        var ticket = Unprotect<OctoberReadTicket>("selection", selection?.Token);
        if (ticket.ExpiresAt <= Now || ticket.Selection is null || string.IsNullOrEmpty(ticket.Nonce)) throw new OctoberInvalidContinuationException();
        return ticket;
    }
    internal ContinuationToken Issue(OctoberReadPosition position) => new(Protect("position", position));
    internal OctoberReadPosition Read(ContinuationToken token)
    {
        var position = Unprotect<OctoberReadPosition>("position", token?.Value);
        if (position.ExpiresAt <= Now || position.Offset < 0) throw new OctoberInvalidContinuationException();
        return position;
    }
    internal static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private string Protect<T>(string kind, T value)
    {
        string token = _protector.Protect(JsonSerializer.Serialize(new Envelope<T>(1, kind, value)));
        if (string.IsNullOrWhiteSpace(token) || token.Length > 65536) throw new OctoberKnowledgeReadBudgetException();
        return token;
    }
    private T Unprotect<T>(string kind, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 65536) throw new OctoberInvalidContinuationException();
        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope<T>>(_protector.Unprotect(value));
            if (envelope is null || envelope.Version != 1 || envelope.Kind != kind || envelope.Value is null) throw new OctoberInvalidContinuationException();
            return envelope.Value;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Do not return cryptographic, parser or payload details to a caller.
            throw new OctoberInvalidContinuationException();
        }
    }
    private sealed record Envelope<T>(int Version, string Kind, T Value);
}
