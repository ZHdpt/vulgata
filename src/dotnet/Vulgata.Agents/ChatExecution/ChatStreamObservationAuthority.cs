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
    string ProviderIdentity,
    string RequestId,
    string AttemptKind,
    string CanonicalStreamingRequestFingerprint,
    bool RequestStreamMode,
    string ResponseHeaderDisposition,
    int? ResponseStatusCode,
    string? ResponseMediaType,
    string? NegotiatedProtocol,
    string FirstPhysicalReadDisposition,
    string PhysicalEofDisposition,
    int RawReadCount,
    int MaximumRawReadByteCount,
    int RawBodyByteCount,
    string Outcome,
    string? RejectionCategory);

/// <summary>
/// Same-process authority for content-dark raw Chat stream observations.
/// The private signing key is ephemeral and never leaves this instance.
/// </summary>
internal sealed class ChatStreamObservationAuthority : IDisposable
{
    private const string _authority = "production-raw-response-header-adapter";
    private const string _adapterType =
        "Vulgata.Agents.ChatExecution.OpenAiCompatibleChatProviderStreamingTransport";
    private readonly ECDsa _signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly string _processObservationId = $"process-{Guid.NewGuid():N}";
    private readonly string _adapterVersion;
    private readonly string _adapterAssemblySha256;
    private readonly string _publicKeyFingerprint;
    private readonly Dictionary<string, string> _receipts = new(StringComparer.Ordinal);
    private readonly object _gate = new();

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

    internal string Record(ChatStreamObservationReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (!HasAuthorizedBinding(receipt))
        {
            throw new InvalidOperationException("The raw stream observation binding is invalid.");
        }

        byte[] canonical = JsonSerializer.SerializeToUtf8Bytes(receipt);
        byte[] signature = _signer.SignHash(SHA256.HashData(canonical));
        string envelope = JsonSerializer.Serialize(new SignedReceipt(
            receipt,
            Base64UrlEncode(signature)));
        lock (_gate)
        {
            _receipts[receipt.RequestId + ":" + receipt.AttemptKind] = envelope;
        }

        CryptographicOperations.ZeroMemory(canonical);
        CryptographicOperations.ZeroMemory(signature);
        return envelope;
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

    internal bool Verify(string serializedReceipt)
    {
        if (string.IsNullOrWhiteSpace(serializedReceipt))
        {
            return false;
        }

        try
        {
            SignedReceipt? envelope = JsonSerializer.Deserialize<SignedReceipt>(
                serializedReceipt,
                new JsonSerializerOptions { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow });
            if (envelope?.Receipt is null ||
                string.IsNullOrWhiteSpace(envelope.Signature) ||
                !HasAuthorizedBinding(envelope.Receipt))
            {
                return false;
            }

            byte[] canonical = JsonSerializer.SerializeToUtf8Bytes(envelope.Receipt);
            byte[] signature = Base64UrlDecode(envelope.Signature);
            bool verified = _signer.VerifyHash(SHA256.HashData(canonical), signature);
            CryptographicOperations.ZeroMemory(canonical);
            CryptographicOperations.ZeroMemory(signature);
            return verified;
        }
        catch (Exception exception) when (
            exception is JsonException or FormatException or CryptographicException)
        {
            return false;
        }
    }

    public void Dispose() => _signer.Dispose();

    private bool HasAuthorizedBinding(ChatStreamObservationReceipt receipt) =>
        StringComparer.Ordinal.Equals(receipt.ObservationAuthority, _authority) &&
        StringComparer.Ordinal.Equals(receipt.ProcessObservationId, _processObservationId) &&
        StringComparer.Ordinal.Equals(receipt.AdapterImplementation, _adapterType) &&
        StringComparer.Ordinal.Equals(receipt.AdapterVersion, _adapterVersion) &&
        StringComparer.Ordinal.Equals(receipt.AdapterAssemblySha256, _adapterAssemblySha256) &&
        receipt.RequestStreamMode &&
        !string.IsNullOrWhiteSpace(receipt.ProviderIdentity) &&
        !string.IsNullOrWhiteSpace(receipt.RequestId) &&
        receipt.AttemptKind is "primary" or "repair" &&
        IsSha256(receipt.CanonicalStreamingRequestFingerprint) &&
        receipt.RawReadCount >= 0 &&
        receipt.MaximumRawReadByteCount is >= 0 and <= 16_384 &&
        receipt.RawBodyByteCount is >= 0 and <= 1_048_576 &&
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
