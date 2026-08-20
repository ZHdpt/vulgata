using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.DomainServices;

namespace Vulgata.Core.Entities;

public sealed class ScanContextSnapshot
{
    private const string _supportedSchemaIdentity = "scan-context-snapshot/1.0";
    private const int _maximumContextLength = 10_000;

    private ScanContextSnapshot()
    {
    }

    private ScanContextSnapshot(
        Guid id,
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision,
        string? globalContext,
        string? systemContext,
        string? repositoryContext,
        string contentHash,
        DateTimeOffset capturedAt)
    {
        Id = id;
        RepositoryId = repositoryId;
        ScanRunId = scanRunId;
        ProfiledRevision = profiledRevision;
        GlobalContext = globalContext;
        SystemContext = systemContext;
        RepositoryContext = repositoryContext;
        SchemaIdentity = _supportedSchemaIdentity;
        ContentHash = contentHash;
        CapturedAt = capturedAt;
    }

    public Guid Id { get; private set; }
    public Guid RepositoryId { get; private set; }
    public Guid ScanRunId { get; private set; }
    public string ProfiledRevision { get; private set; } = string.Empty;
    public string? GlobalContext { get; private set; }
    public string? SystemContext { get; private set; }
    public string? RepositoryContext { get; private set; }
    public string SchemaIdentity { get; private set; } = _supportedSchemaIdentity;
    public string ContentHash { get; private set; } = string.Empty;
    public string? PreparedScanContextIdentityHash { get; private set; }
    public DateTimeOffset CapturedAt { get; private set; }

    public static ScanContextSnapshot Capture(
        ScanRun scanRun,
        string? globalContext,
        string? systemContext,
        string? repositoryContext,
        DateTimeOffset capturedAt)
    {
        ArgumentNullException.ThrowIfNull(scanRun);
        if (scanRun.ProfiledRevision is null)
        {
            throw new InvalidOperationException(
                "A Scan Context Snapshot requires a bound Profiled Revision.");
        }

        if (capturedAt < scanRun.StartedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(capturedAt));
        }

        string? normalizedGlobalContext = NormalizeContext(globalContext, nameof(globalContext));
        string? normalizedSystemContext = NormalizeContext(systemContext, nameof(systemContext));
        string? normalizedRepositoryContext = NormalizeContext(
            repositoryContext,
            nameof(repositoryContext));

        byte[] contentEnvelope = CreateEnvelope(
            _supportedSchemaIdentity,
            normalizedGlobalContext,
            normalizedSystemContext,
            normalizedRepositoryContext);
        byte[] contentDigest = SHA256.HashData(contentEnvelope);
        string contentHash = Convert.ToHexStringLower(contentDigest);

        byte[] identityEnvelope = CreateEnvelope(
            _supportedSchemaIdentity,
            scanRun.RepositoryId.ToString("D"),
            scanRun.Id.ToString("D"),
            scanRun.ProfiledRevision,
            contentHash,
            capturedAt.ToUniversalTime().ToString("O"));
        byte[] identityBytes = SHA256.HashData(identityEnvelope)[..16];
        identityBytes[6] = (byte)((identityBytes[6] & 0x0f) | 0x80);
        identityBytes[8] = (byte)((identityBytes[8] & 0x3f) | 0x80);

        return new ScanContextSnapshot(
            new Guid(identityBytes, bigEndian: true),
            scanRun.RepositoryId,
            scanRun.Id,
            scanRun.ProfiledRevision,
            normalizedGlobalContext,
            normalizedSystemContext,
            normalizedRepositoryContext,
            contentHash,
            capturedAt);
    }

    public static ScanContextSnapshot CapturePrepared(
        ScanRun scanRun,
        string? globalContext,
        string? systemContext,
        string? repositoryContext,
        DateTimeOffset capturedAt)
    {
        ScanContextSnapshot snapshot = Capture(
            scanRun,
            globalContext,
            systemContext,
            repositoryContext,
            capturedAt);
        snapshot.PreparedScanContextIdentityHash = PreparedScanIdentityBuilders
            .CreateContextIdentity(
                snapshot.RepositoryId,
                snapshot.ScanRunId,
                snapshot.ProfiledRevision,
                snapshot.ContentHash)
            .Fingerprint;
        return snapshot;
    }

    private static string? NormalizeContext(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim().Normalize(NormalizationForm.FormC);
        if (normalized.Length > _maximumContextLength)
        {
            throw new ArgumentException(
                $"Context must not exceed {_maximumContextLength} characters.",
                parameterName);
        }

        return normalized;
    }

    private static byte[] CreateEnvelope(params string?[] fields)
    {
        using var stream = new MemoryStream();
        foreach (string? field in fields)
        {
            byte[] length = new byte[4];
            if (field is null)
            {
                BinaryPrimitives.WriteInt32BigEndian(length, -1);
                stream.Write(length);
                continue;
            }

            byte[] value = Encoding.UTF8.GetBytes(field);
            BinaryPrimitives.WriteInt32BigEndian(length, value.Length);
            stream.Write(length);
            stream.Write(value);
        }

        return stream.ToArray();
    }
}
