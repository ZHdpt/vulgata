using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Vulgata.Core.DomainServices.OctoberKnowledge;

/// <summary>
/// The trusted prepared-source adapter verifies exact bytes/digest/version and line count.
/// Returning model-supplied metadata unchanged does not satisfy this contract. No host path is exposed.
/// </summary>
public sealed record VerifiedKnowledgeSource(PreparedOrigin Origin, int LineCount);
public interface IOctoberKnowledgeSourceVerifier
{
    Task<VerifiedKnowledgeSource?> VerifyAsync(KnowledgeBinding binding, PreparedOrigin origin, CancellationToken cancellationToken = default);
}

/// <summary>Live producer disclosure check, including uncited source/advisory contributors.</summary>
public interface IOctoberKnowledgeProducerAuthorization
{
    Task<DisclosureStatus> CheckAsync(KnowledgeBinding binding, ImmutableArray<Contributor> contributors, CancellationToken cancellationToken = default);
}

public static class OctoberKnowledgePolicy
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly UTF8Encoding _utf8 = new(false, true);

    /// <summary>
    /// CL schema 1 has exactly schemaVersion, title and qualified sections. ContentJson must
    /// equal this representation; it is not a second, independently authored prose channel.
    /// Sections are ordered by stable ordinal; claim/narrative order is preserved.
    /// </summary>
    public static string CreateContentJson(string title, ImmutableArray<DocumentSection> sections) =>
        SerializeCanonical(new { schemaVersion = KnowledgeContract.SchemaVersion, title, sections = sections.OrderBy(x => x.Ordinal) });

    public static string CanonicalizeJson(string json)
    {
        _ = _utf8.GetByteCount(json);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonical(writer, document.RootElement);
        }
        return _utf8.GetString(stream.ToArray());
    }

    public static string ComputeProposalDigest(CompletedFileProposal proposal)
    {
        var canonical = new
        {
            operationKind = OperationKind.AcceptFile,
            proposal.SchemaVersion,
            proposal.Binding,
            proposal.AdmissionId,
            proposal.AttemptId,
            origin = NormalizeOrigin(proposal.Origin),
            units = proposal.Units.OrderBy(x => x.SemanticSelector, StringComparer.Ordinal).Select(x => new
            {
                x.SemanticSelector, x.ParentSelector, x.Title,
                content = JsonSerializer.Deserialize<JsonElement>(CanonicalizeJson(x.ContentJson)),
                anchors = x.Anchors.Select(NormalizeAnchor).OrderBy(SerializeCanonical, StringComparer.Ordinal),
                sections = x.Sections.OrderBy(s => s.Ordinal)
            }),
            observations = proposal.Observations.OrderBy(x => x.ObservationKey, StringComparer.Ordinal),
            uncertainties = proposal.Uncertainties.OrderBy(x => x.Key, StringComparer.Ordinal),
            contributors = proposal.Contributors.OrderBy(SerializeCanonical, StringComparer.Ordinal),
            coverage = proposal.Coverage with { Origin = NormalizeOrigin(proposal.Coverage.Origin) }
        };
        return Hash(SerializeCanonical(canonical));
    }

    public static bool Validate(CompletedFileProposal proposal, VerifiedKnowledgeSource source)
    {
        try
        {
            return ValidateCore(proposal, source);
        }
        catch (Exception error) when (error is ArgumentException or JsonException or InvalidOperationException or NullReferenceException)
        {
            return false;
        }
    }

    public static string SemanticKey(CompletedFileProposal proposal, string selector) => Hash(SerializeCanonical(new
    {
        domain = "october/code-unit/1", proposal.Binding.BatchId, proposal.Origin.RepositoryId,
        proposal.Origin.RootBindingIdentity, relativePath = NormalizePath(proposal.Origin.RelativePath), selector
    }));

    public static Guid SemanticUnitId(CompletedFileProposal proposal, string selector) =>
        new(Convert.FromHexString(SemanticKey(proposal, selector)).AsSpan(0, 16));

    public static Guid LogicalDocumentId(Guid unitId) =>
        new(SHA256.HashData(_utf8.GetBytes(SerializeCanonical(new { domain = "october/cl/1", unitId }))).AsSpan(0, 16));

    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(_utf8.GetBytes(value)));
    public static string SerializeCanonical<T>(T value) => CanonicalizeJson(JsonSerializer.Serialize(value, _json));
    public static string NormalizePath(string value) => value.Replace('\\', '/');

    private static bool ValidateCore(CompletedFileProposal p, VerifiedKnowledgeSource source)
    {
        if (p.SchemaVersion != KnowledgeContract.SchemaVersion || p.Binding.BatchId == Guid.Empty ||
            !Text(p.Binding.TargetId) || !Text(p.Binding.CanonicalDatabaseIdentity) || !Digest(p.Binding.SelectionDigest) ||
            p.AdmissionId == Guid.Empty || p.AttemptId == Guid.Empty || source.LineCount < 0 ||
            !OriginValid(p.Origin) || NormalizeOrigin(p.Origin) != NormalizeOrigin(source.Origin) ||
            p.Coverage.AdmissionId != p.AdmissionId || NormalizeOrigin(p.Coverage.Origin) != NormalizeOrigin(p.Origin) ||
            p.Units.IsDefault || p.Observations.IsDefault || p.Uncertainties.IsDefault || p.Contributors.IsDefaultOrEmpty)
            return false;
        if (p.Units.IsEmpty ? p.Coverage.Disposition != CoverageDisposition.CompletedZeroUnit || !Text(p.Coverage.Reason)
            : p.Coverage.Disposition != CoverageDisposition.Accepted)
            return false;
        // The additive input declarations do not grant acceptance before verified-ledger support is implemented.
        if (p.Contributors.Any(x => x.SourceOrigin is not null || x.MetadataOrigin is not null)) return false;
        if (p.Contributors.Distinct().Count() != p.Contributors.Length || p.Contributors.Any(x => !ContributorValid(x, p.Origin)) ||
            !p.Contributors.Any(x => x.Kind == ContributorKind.Source && SourceContributorMatches(x, p.Origin)))
            return false;

        var units = p.Units.ToDictionary(x => x.SemanticSelector, StringComparer.Ordinal);
        foreach (var unit in p.Units)
        {
            if (!Key(unit.SemanticSelector) || !Text(unit.Title) || unit.Anchors.IsDefaultOrEmpty ||
                unit.Anchors.Any(x => !AnchorValid(x, source)) || unit.Sections.IsDefaultOrEmpty ||
                CanonicalizeJson(unit.ContentJson) != CreateContentJson(unit.Title, unit.Sections))
                return false;
            var visited = new HashSet<string>(StringComparer.Ordinal) { unit.SemanticSelector };
            var parent = unit.ParentSelector;
            while (parent is not null)
            {
                if (!visited.Add(parent) || !units.TryGetValue(parent, out var ancestor)) return false;
                parent = ancestor.ParentSelector;
            }
            if (unit.Sections.Select(x => x.SectionId).Distinct(StringComparer.Ordinal).Count() != unit.Sections.Length ||
                unit.Sections.Select(x => x.Ordinal).Distinct().Count() != unit.Sections.Length)
                return false;
            foreach (var section in unit.Sections)
            {
                if (!Key(section.SectionId) || section.Ordinal < 0 || !Text(section.Title) || section.Claims.IsDefaultOrEmpty ||
                    section.Claims.Select(x => x.ClaimGroupId).Distinct(StringComparer.Ordinal).Count() != section.Claims.Length)
                    return false;
                foreach (var claim in section.Claims)
                {
                    if (!Key(claim.ClaimGroupId) || !Text(claim.Text) || claim.Limitations.IsDefault ||
                        claim.Limitations.Any(x => !Text(x)) || !EvidenceValid(claim.Evidence, p, source) ||
                        !Qualified(claim.Qualification, claim.Evidence) ||
                        (claim.Qualification is ClaimQualification.Hypothesis or ClaimQualification.Uncertain && claim.Limitations.IsEmpty))
                        return false;
                }
            }
        }
        if (p.Observations.Select(x => x.ObservationKey).Distinct(StringComparer.Ordinal).Count() != p.Observations.Length ||
            p.Uncertainties.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count() != p.Uncertainties.Length)
            return false;
        foreach (var observation in p.Observations)
        {
            if (!Key(observation.ObservationKey) || !units.ContainsKey(observation.OwnerSelector) || !Text(observation.Role) ||
                !Text(observation.Mechanism) || !Text(observation.Operation) || observation.LiteralsOrExpressions.IsDefault ||
                !EvidenceValid(observation.Evidence, p, source) || !Qualified(observation.Qualification, observation.Evidence))
                return false;
        }
        return p.Uncertainties.All(x => Key(x.Key) && Text(x.Explanation) && !x.Limitations.IsDefault &&
            x.Limitations.All(Text) && EvidenceValid(x.Evidence, p, source));
    }

    private static bool EvidenceValid(ImmutableArray<EvidenceReference> evidence, CompletedFileProposal p, VerifiedKnowledgeSource source)
    {
        if (evidence.IsDefault) return false;
        foreach (var item in evidence)
        {
            // Initial file analysis cannot invent exact references to a yet-unaccepted Document.
            if (item.Document is not null || item.ClaimGroupId is not null || !Digest(item.ContentDigest)) return false;
            if (item.Source is { } anchor)
            {
                if (item.AdvisoryRecordId is not null || !AnchorValid(anchor, source) || item.ContentDigest != anchor.Origin.SourceDigest ||
                    !p.Contributors.Any(x => x.Kind == ContributorKind.Source && SourceContributorMatches(x, anchor.Origin))) return false;
            }
            else if (item.AdvisoryRecordId is { } advisory)
            {
                if (advisory == Guid.Empty || !p.Contributors.Any(x => x.Kind != ContributorKind.Source &&
                    x.AdvisoryRecordId == advisory && x.ContentDigest == item.ContentDigest)) return false;
            }
            else return false;
        }
        return true;
    }

    private static bool Qualified(ClaimQualification qualification, ImmutableArray<EvidenceReference> evidence)
    {
        bool source = evidence.Any(x => x.Source is not null);
        bool advisory = evidence.Any(x => x.AdvisoryRecordId is not null);
        return qualification switch
        {
            ClaimQualification.SourceSupported => source && !advisory,
            ClaimQualification.Advisory => advisory && !source,
            // Schema 1 has no clause-attribution field. Split mixed support into separate qualified groups.
            ClaimQualification.Mixed => false,
            ClaimQualification.Hypothesis or ClaimQualification.Uncertain => true,
            _ => false
        };
    }

    private static bool ContributorValid(Contributor c, PreparedOrigin origin) => Digest(c.ContentDigest) && (c.Kind switch
    {
        ContributorKind.Source => c.AdvisoryRecordId is null && SourceContributorMatches(c, origin),
        ContributorKind.GlobalAdvisory => c.AdvisoryRecordId is { } id && id != Guid.Empty && c.RepositoryId is null && c.SystemId is null && c.RootBindingIdentity is null,
        ContributorKind.SystemAdvisory => c.AdvisoryRecordId is { } id && id != Guid.Empty && c.SystemId is { } system && system != Guid.Empty && c.RepositoryId is null && c.RootBindingIdentity is null,
        ContributorKind.RepositoryAdvisory => c.AdvisoryRecordId is { } id && id != Guid.Empty && c.RepositoryId is { } repository && repository != Guid.Empty && c.RootBindingIdentity is null,
        _ => false
    });
    private static bool SourceContributorMatches(Contributor c, PreparedOrigin origin) => c.RepositoryId == origin.RepositoryId &&
        c.RootBindingIdentity == origin.RootBindingIdentity && c.ContentDigest == origin.SourceDigest;
    private static bool AnchorValid(SourceAnchor anchor, VerifiedKnowledgeSource source) =>
        NormalizeOrigin(anchor.Origin) == NormalizeOrigin(source.Origin) && anchor.StartLine >= 1 && anchor.EndLine >= anchor.StartLine && anchor.EndLine <= source.LineCount;
    private static PreparedOrigin NormalizeOrigin(PreparedOrigin origin) => origin with { RelativePath = NormalizePath(origin.RelativePath) };
    private static SourceAnchor NormalizeAnchor(SourceAnchor anchor) => anchor with { Origin = NormalizeOrigin(anchor.Origin) };
    private static bool OriginValid(PreparedOrigin origin)
    {
        var path = NormalizePath(origin.RelativePath);
        return origin.RepositoryId != Guid.Empty && Text(origin.RootBindingIdentity) && Digest(origin.SourceDigest) && Text(origin.PreparedVersion) &&
            Text(path) && !path.Contains(':') && !path.StartsWith('/') && !path.Any(char.IsControl) &&
            path.Split('/').All(segment => segment.Length > 0 && segment is not "." and not ".." && segment == segment.Trim());
    }
    private static bool Text(string? value) => !string.IsNullOrWhiteSpace(value);
    private static bool Key(string? value) => Text(value) && value == value!.Trim() && !value.Any(char.IsControl);
    private static bool Digest(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);

    private static string CanonicalNumber(string raw)
    {
        // Use floating point only to reject unsupported magnitude, never to round the value.
        if (raw.Length > 1024 || !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var magnitude) || !double.IsFinite(magnitude))
            throw new JsonException("Unsupported numeric magnitude.");
        bool negative = raw.StartsWith('-');
        string unsigned = negative ? raw[1..] : raw;
        var parts = unsigned.Split(['e', 'E']);
        int exponent = parts.Length == 2 ? int.Parse(parts[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture) : 0;
        string mantissa = parts[0];
        int point = mantissa.IndexOf('.');
        if (point >= 0) exponent -= mantissa.Length - point - 1;
        string digits = mantissa.Replace(".", "", StringComparison.Ordinal).TrimStart('0');
        if (digits.Length == 0) return "0";
        if (magnitude == 0) throw new JsonException("Unsupported numeric underflow.");
        int beforeTrim = digits.Length;
        digits = digits.TrimEnd('0');
        exponent += beforeTrim - digits.Length;
        int scientific = exponent + digits.Length - 1;
        string result;
        if (exponent >= 0 && scientific < 21) result = digits + new string('0', exponent);
        else if (exponent < 0 && scientific >= -6)
        {
            int position = digits.Length + exponent;
            result = position > 0 ? digits.Insert(position, ".") : "0." + new string('0', -position) + digits;
        }
        else result = digits[0] + (digits.Length > 1 ? "." + digits[1..] : "") + "e" + scientific.ToString(CultureInfo.InvariantCulture);
        return negative ? "-" + result : result;
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var properties = element.EnumerateObject().ToArray();
                if (properties.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length)
                    throw new JsonException("Duplicate properties are not canonical.");
                writer.WriteStartObject();
                foreach (var property in properties.OrderBy(x => x.Name, StringComparer.Ordinal))
                {
                    _ = _utf8.GetByteCount(property.Name);
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                string value = element.GetString()!;
                _ = _utf8.GetByteCount(value);
                writer.WriteStringValue(value);
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(CanonicalNumber(element.GetRawText()));
                break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            case JsonValueKind.Null: writer.WriteNullValue(); break;
            default: throw new JsonException("Unsupported value.");
        }
    }
}
