using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Vulgata.Tests.CodeGraphQualification;

internal sealed record QualificationAttemptTerminalRecord(
    string SchemaVersion,
    string AttemptId,
    DateTimeOffset CompletedAt,
    string Outcome,
    string Category,
    string EvidenceCitation);

internal sealed partial class QualificationTerminalPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    internal async Task<QualificationAttemptTerminalRecord> PublishAsync(
        string attemptDirectory,
        QualificationAttemptTerminalRecord terminal,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attemptDirectory);
        ValidateRecord(terminal);
        string terminalPath = Path.Combine(attemptDirectory, "terminal.json");
        if (File.Exists(terminalPath))
        {
            return await ValidatePublishedAsync(terminalPath, cancellationToken);
        }

        string temporaryPath = Path.Combine(
            attemptDirectory,
            $".terminal.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, terminal, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            await ValidatePublishedAsync(temporaryPath, cancellationToken);
            try
            {
                File.Move(temporaryPath, terminalPath, overwrite: false);
                return terminal;
            }
            catch (IOException) when (File.Exists(terminalPath))
            {
                File.Delete(temporaryPath);
                return await ValidatePublishedAsync(terminalPath, cancellationToken);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    internal async Task<QualificationAttemptTerminalRecord> ValidateExistingAsync(
        string attemptDirectory,
        CancellationToken cancellationToken = default) =>
        await ValidatePublishedAsync(
            Path.Combine(attemptDirectory, "terminal.json"),
            cancellationToken);

    private static async Task<QualificationAttemptTerminalRecord> ValidatePublishedAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            using JsonDocument document = JsonDocument.Parse(bytes);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("The terminal root must be an object.");
            }

            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new JsonException("The terminal contains a duplicate property.");
                }
            }

            QualificationAttemptTerminalRecord terminal =
                JsonSerializer.Deserialize<QualificationAttemptTerminalRecord>(bytes, JsonOptions)
                ?? throw new JsonException("The terminal is empty.");
            ValidateRecord(terminal);
            return terminal;
        }
        catch (Exception exception) when (
            exception is IOException or JsonException or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "The qualification attempt terminal is ledger corruption.",
                exception);
        }
    }

    private static void ValidateRecord(QualificationAttemptTerminalRecord terminal)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        if (terminal.SchemaVersion != "1.0" ||
            !AttemptIdPattern().IsMatch(terminal.AttemptId) ||
            terminal.CompletedAt == default ||
            terminal.Outcome is not ("passed" or "failed") ||
            string.IsNullOrWhiteSpace(terminal.Category) ||
            !terminal.EvidenceCitation.StartsWith(
                "specs/001-codegraph-extraction/qualification/evidence/",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The qualification terminal classification is invalid.");
        }
    }

    [GeneratedRegex("^attempt-[0-9]{4}$", RegexOptions.CultureInvariant)]
    private static partial Regex AttemptIdPattern();
}
