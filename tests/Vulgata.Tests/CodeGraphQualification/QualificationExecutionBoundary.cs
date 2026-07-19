using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Vulgata.Tests.CodeGraphQualification;

internal sealed partial record QualificationExecutionBoundary(
    string SchemaVersion,
    string AttemptId,
    string ExactCommand,
    string CorrectivePlanCommit,
    string PlanReviewPath,
    string PlanReviewSha256,
    string ExecutionApprovalGateId,
    string ProviderFactIdentity,
    string ProviderCategory,
    bool NoRetry,
    string QualificationScopeId)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    [JsonIgnore]
    internal string RawSha256 { get; private init; } = string.Empty;

    [JsonIgnore]
    internal string SourcePath { get; private init; } = string.Empty;

    internal static async Task<QualificationExecutionBoundary> LoadAsync(
        string path,
        string expectedSha256,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSha256);
        byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        string actualSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (!string.Equals(actualSha256, expectedSha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The qualification execution boundary bytes do not match the approved SHA-256.");
        }

        QualificationExecutionBoundary boundary;
        try
        {
            string json = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(bytes);
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("The execution boundary root must be an object.");
            }

            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new JsonException("The execution boundary contains a duplicate property.");
                }
            }

            boundary = JsonSerializer.Deserialize<QualificationExecutionBoundary>(json, JsonOptions)
                ?? throw new InvalidOperationException("The qualification execution boundary is empty.");
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException)
        {
            throw new InvalidOperationException("The qualification execution boundary is invalid.", exception);
        }

        boundary.Validate();
        return boundary with
        {
            RawSha256 = actualSha256,
            SourcePath = Path.GetFullPath(path),
        };
    }

    private void Validate()
    {
        if (SchemaVersion != "1.0" ||
            AttemptId != "attempt-0003" ||
            !AttemptIdPattern().IsMatch(AttemptId) ||
            string.IsNullOrWhiteSpace(ExactCommand) ||
            !Sha1Pattern().IsMatch(CorrectivePlanCommit) ||
            string.IsNullOrWhiteSpace(PlanReviewPath) ||
            !Sha256Pattern().IsMatch(PlanReviewSha256) ||
            string.IsNullOrWhiteSpace(ExecutionApprovalGateId) ||
            string.IsNullOrWhiteSpace(ProviderFactIdentity) ||
            ProviderCategory != "CodeGraphQualificationProvider" ||
            !NoRetry ||
            string.IsNullOrWhiteSpace(QualificationScopeId))
        {
            throw new InvalidOperationException("The qualification execution boundary is invalid.");
        }
    }

    [GeneratedRegex("^attempt-[0-9]{4}$", RegexOptions.CultureInvariant)]
    private static partial Regex AttemptIdPattern();

    [GeneratedRegex("^[a-f0-9]{40}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha1Pattern();

    [GeneratedRegex("^[a-f0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();
}
