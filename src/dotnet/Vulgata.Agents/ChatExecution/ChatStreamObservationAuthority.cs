using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vulgata.Agents.ChatExecution;

internal sealed record ChatStreamObservationReceipt(
    string ObservationAuthority,
    string ProcessObservationId,
    string AdapterImplementation,
    string AdapterVersion,
    string AdapterAssemblySha256,
    string ProcessPublicKeyFingerprint,
    int ObservationSequence,
    string CandidateCommit,
    string CaseId,
    string ProviderIdentity,
    string ProviderApi,
    string ProviderConfigurationFingerprint,
    string RequestId,
    string AttemptKind,
    string CanonicalStreamingRequestFingerprint,
    bool RequestStreamMode,
    string ResponseHeaderDisposition,
    int? ResponseStatusCode,
    string? ResponseMediaType,
    string? NegotiatedProtocol,
    DateTimeOffset? ResponseHeadersAtUtc,
    string FirstPhysicalReadDisposition,
    DateTimeOffset? FirstPhysicalReadAtUtc,
    DateTimeOffset? PhysicalEofAtUtc,
    string PhysicalEofDisposition,
    bool StreamingTransport,
    int RawReadCount,
    int MaximumRawReadByteCount,
    int RawBodyByteCount,
    int ProviderChunkCount,
    int MaximumReceivedProviderChunkByteCount,
    int AssembledReceivedProviderResponseByteCount,
    int MaximumSseLineByteCount,
    int MaximumSseEventByteCount,
    DateTimeOffset StreamStartedAtUtc,
    DateTimeOffset? FirstDataAtUtc,
    DateTimeOffset? TerminalAtUtc,
    long? TimeToFirstDataMilliseconds,
    int AnswerDeltaCount,
    int MaximumAnswerDeltaByteCount,
    int AssembledAnswerByteCount,
    int TerminalEventCount,
    int InactivityLimitMilliseconds,
    DateTimeOffset OverallDeadlineAtUtc,
    string TransportOutcome,
    string? RejectionCategory,
    string ValidationOutcome,
    long? AcceptedAnswerDurationMilliseconds);

/// <summary>
/// Same-process authority for content-dark raw Chat stream observations.
/// The private signing key is ephemeral and never leaves this instance.
/// </summary>
internal sealed class ChatStreamObservationAuthority : IDisposable
{
    private const string _authority = "production-raw-response-header-adapter";
    private const string _adapterType =
        "Vulgata.Agents.ChatExecution.OpenAiCompatibleChatProviderStreamingTransport";
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private readonly ECDsa _signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly string _processObservationId = $"process-{Guid.NewGuid():N}";
    private readonly string _adapterVersion;
    private readonly string _adapterAssemblySha256;
    private readonly string _publicKeyFingerprint;
    private readonly Dictionary<string, string> _receipts = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private int _observationSequence;

    public ChatStreamObservationAuthority()
    {
        var assembly = typeof(OpenAiCompatibleChatProviderStreamingTransport).Assembly;
        _adapterVersion = assembly.GetCustomAttributes(false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .Select(attribute => attribute.InformationalVersion)
            .FirstOrDefault() ?? assembly.GetName().Version?.ToString() ?? "unknown";
        _adapterAssemblySha256 = ComputeAssemblySha256(assembly.Location);
        _publicKeyFingerprint = Convert.ToHexStringLower(
            SHA256.HashData(_signer.ExportSubjectPublicKeyInfo()));
    }

    internal string ProcessObservationId => _processObservationId;

    internal string PublicKeyFingerprint => _publicKeyFingerprint;

    internal string AdapterVersion => _adapterVersion;

    internal string AdapterAssemblySha256 => _adapterAssemblySha256;

    internal int NextObservationSequence() =>
        Interlocked.Increment(ref _observationSequence);

    internal string Record(ChatStreamObservationReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (!HasAuthorizedBinding(receipt))
        {
            throw new InvalidOperationException("The raw stream observation binding is invalid.");
        }

        lock (_gate)
        {
            string envelope = CreateSignedEnvelope(receipt);
            _receipts[receipt.RequestId + ":" + receipt.AttemptKind] = envelope;
            return envelope;
        }
    }

    internal bool TryGetVerifiedReceipt(
        string requestId,
        string attemptKind,
        out string? receipt)
    {
        lock (_gate)
        {
            if (!_receipts.TryGetValue(requestId + ":" + attemptKind, out receipt))
            {
                return false;
            }
        }

        return Verify(receipt);
    }

    internal bool TryUpdateValidationOutcome(
        string requestId,
        string attemptKind,
        string validationOutcome)
    {
        if (string.IsNullOrWhiteSpace(requestId) ||
            attemptKind is not ("primary" or "syntax-repair") ||
            validationOutcome is not (
                "syntax-repair-required" or "accepted" or "rejected-before-parse" or
                "rejected-syntax" or "rejected-semantic" or "cancelled"))
        {
            return false;
        }

        string key = requestId + ":" + attemptKind;
        lock (_gate)
        {
            if (!_receipts.TryGetValue(key, out string? serializedReceipt) ||
                !TryReadVerifiedReceipt(serializedReceipt, out ChatStreamObservationReceipt? receipt))
            {
                return false;
            }

            ChatStreamObservationReceipt updated = receipt with
            {
                ValidationOutcome = validationOutcome,
            };
            _receipts[key] = CreateSignedEnvelope(updated);
            return true;
        }
    }

    internal bool Verify(string serializedReceipt)
    {
        if (string.IsNullOrWhiteSpace(serializedReceipt))
        {
            return false;
        }

        try
        {
            lock (_gate)
            {
                return TryReadVerifiedReceipt(serializedReceipt, out _);
            }
        }
        catch (Exception exception) when (
            exception is JsonException or FormatException or CryptographicException)
        {
            return false;
        }
    }

    public void Dispose() => _signer.Dispose();

    private string CreateSignedEnvelope(ChatStreamObservationReceipt receipt)
    {
        byte[] canonical = JsonSerializer.SerializeToUtf8Bytes(receipt, _serializerOptions);
        byte[] signature = _signer.SignHash(SHA256.HashData(canonical));
        string envelope = JsonSerializer.Serialize(new SignedReceipt(
            receipt,
            Base64UrlEncode(signature)), _serializerOptions);
        CryptographicOperations.ZeroMemory(canonical);
        CryptographicOperations.ZeroMemory(signature);
        return envelope;
    }

    private bool TryReadVerifiedReceipt(
        string serializedReceipt,
        out ChatStreamObservationReceipt? receipt)
    {
        receipt = null;
        SignedReceipt? envelope = JsonSerializer.Deserialize<SignedReceipt>(
            serializedReceipt,
            _serializerOptions);
        if (envelope?.Receipt is null ||
            string.IsNullOrWhiteSpace(envelope.Signature) ||
            !HasAuthorizedBinding(envelope.Receipt))
        {
            return false;
        }

        byte[] canonical = JsonSerializer.SerializeToUtf8Bytes(
            envelope.Receipt,
            _serializerOptions);
        byte[] signature = Base64UrlDecode(envelope.Signature);
        bool verified = _signer.VerifyHash(SHA256.HashData(canonical), signature);
        CryptographicOperations.ZeroMemory(canonical);
        CryptographicOperations.ZeroMemory(signature);
        if (verified)
        {
            receipt = envelope.Receipt;
        }

        return verified;
    }

    private bool HasAuthorizedBinding(ChatStreamObservationReceipt receipt) =>
        StringComparer.Ordinal.Equals(receipt.ObservationAuthority, _authority) &&
        StringComparer.Ordinal.Equals(receipt.ProcessObservationId, _processObservationId) &&
        StringComparer.Ordinal.Equals(receipt.AdapterImplementation, _adapterType) &&
        StringComparer.Ordinal.Equals(receipt.AdapterVersion, _adapterVersion) &&
        StringComparer.Ordinal.Equals(receipt.AdapterAssemblySha256, _adapterAssemblySha256) &&
        StringComparer.Ordinal.Equals(
            receipt.ProcessPublicKeyFingerprint,
            _publicKeyFingerprint) &&
        receipt.ObservationSequence > 0 &&
        receipt.RequestStreamMode &&
        receipt.StreamingTransport &&
        !string.IsNullOrWhiteSpace(receipt.CandidateCommit) &&
        !string.IsNullOrWhiteSpace(receipt.CaseId) &&
        !string.IsNullOrWhiteSpace(receipt.ProviderIdentity) &&
        !string.IsNullOrWhiteSpace(receipt.ProviderApi) &&
        IsSha256(receipt.ProviderConfigurationFingerprint) &&
        !string.IsNullOrWhiteSpace(receipt.RequestId) &&
        receipt.AttemptKind is "primary" or "syntax-repair" &&
        IsSha256(receipt.CanonicalStreamingRequestFingerprint) &&
        receipt.RawReadCount >= 0 &&
        receipt.MaximumRawReadByteCount is >= 0 and <= 16_384 &&
        receipt.RawBodyByteCount is >= 0 and <= 1_048_576 &&
        receipt.ProviderChunkCount is >= 0 and <= 4_096 &&
        receipt.MaximumReceivedProviderChunkByteCount is >= 0 and <= 16_385 &&
        receipt.AssembledReceivedProviderResponseByteCount is >= 0 and <= 65_537 &&
        receipt.MaximumSseLineByteCount is >= 0 and <= 131_072 &&
        receipt.MaximumSseEventByteCount is >= 0 and <= 131_072 &&
        receipt.AnswerDeltaCount is >= 0 and <= 4_096 &&
        receipt.MaximumAnswerDeltaByteCount is >= 0 and <= 16_384 &&
        receipt.AssembledAnswerByteCount is >= 0 and <= 65_536 &&
        receipt.TerminalEventCount is >= 0 and <= 8 &&
        receipt.InactivityLimitMilliseconds == 10_000 &&
        receipt.OverallDeadlineAtUtc >= receipt.StreamStartedAtUtc &&
        receipt.FirstDataAtUtc is null == (receipt.TimeToFirstDataMilliseconds is null) &&
        receipt.AcceptedAnswerDurationMilliseconds is null or >= 0 &&
        !string.IsNullOrWhiteSpace(receipt.TransportOutcome) &&
        !string.IsNullOrWhiteSpace(receipt.ValidationOutcome) &&
        !string.IsNullOrWhiteSpace(receipt.ResponseHeaderDisposition) &&
        !string.IsNullOrWhiteSpace(receipt.FirstPhysicalReadDisposition) &&
        !string.IsNullOrWhiteSpace(receipt.PhysicalEofDisposition);

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(static character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string ComputeAssemblySha256(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return Convert.ToHexStringLower(
                SHA256.HashData(Encoding.UTF8.GetBytes(_adapterType)));
        }

        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        string normalized = value.Replace('-', '+').Replace('_', '/');
        normalized = normalized.PadRight((normalized.Length + 3) / 4 * 4, '=');
        return Convert.FromBase64String(normalized);
    }

    private sealed record SignedReceipt(
        ChatStreamObservationReceipt Receipt,
        string Signature);
}
