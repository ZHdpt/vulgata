using System.Text;
using System.Text.Json;
using Vulgata.Agents.OctoberBusiness;
using Vulgata.Core.DomainServices;

namespace Vulgata.OctoberGenerator;

/// <summary>Opt-in, protected operator diagnostics; never publication evidence or model input.</summary>
internal sealed class OctoberBusinessDiagnosticCapture(string logDirectory, ISecretEncryptionService encryption)
{
    internal const string Purpose = "Vulgata.October.Business.SemanticDiagnostics.v1";
    internal const int MaximumRecordBytes = 4 * 1024 * 1024;
    private const long MaximumCaptureBytes = 64 * 1024 * 1024;
    private readonly string _directory = Path.Combine(Path.GetFullPath(logDirectory), "business-semantic");
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long _bytes;

    internal async Task CaptureAsync(BusinessSynthesisDiagnostic diagnostic, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        if (diagnostic.RunId == Guid.Empty || diagnostic.GroupVersionId == Guid.Empty ||
            diagnostic.Stage is not ("draft" or "review" or "correction" or "re-review") &&
            !Enumerable.Range(1, 12).Any(n => diagnostic.Stage == "source-" + n))
            throw new InvalidOperationException("Invalid Business diagnostic identity.");
        string plain = JsonSerializer.Serialize(diagnostic);
        int bytes = Encoding.UTF8.GetByteCount(plain);
        if (bytes > MaximumRecordBytes) throw new InvalidOperationException("Business diagnostic record limit exceeded.");
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_bytes + bytes > MaximumCaptureBytes)
                throw new InvalidOperationException("Business diagnostic capture limit exceeded.");
            string cipher = await encryption.EncryptAsync(Purpose, plain, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(cipher) || cipher == plain)
                throw new InvalidOperationException("Business diagnostic protection unavailable.");
            Directory.CreateDirectory(_directory);
            string path = Path.Combine(_directory, $"{diagnostic.RunId:D}-{diagnostic.GroupVersionId:D}-{diagnostic.Stage}.protected");
            await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.Asynchronous);
            await stream.WriteAsync(Encoding.UTF8.GetBytes(cipher), cancellationToken);
            await stream.FlushAsync(cancellationToken);
            _bytes += bytes;
        }
        finally { _gate.Release(); }
    }
}
