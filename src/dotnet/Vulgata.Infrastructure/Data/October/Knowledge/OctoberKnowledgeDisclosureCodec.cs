using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

internal sealed record DisclosureBinding(PublicationBinding Publication, string SubjectId, KnowledgeMode Mode,
    KnowledgePurpose Purpose, ImmutableArray<Guid> RepositoryScope);
internal sealed record DisclosureGroup(DocumentKind? Kind, ImmutableArray<Contributor> Contributors);
internal sealed record DisclosureContent(DocumentHandle Document, string ProjectionDigest, int Start, int End, int TotalLength,
    string ChunkDigest, long ContentBytes, CoverageProjection? Coverage);
internal sealed record DisclosureOrigin(string Kind, string InputDigest, string PageDigest, string OutputProfile,
    string OutputDigest, ImmutableArray<DisclosureGroup> Groups, DisclosureContent? Content);
internal sealed record DisclosureSet(DisclosureBinding Binding, ImmutableArray<DisclosureOrigin> Origins);
internal sealed record ConsumedSlice(DisclosureContent Content, ImmutableArray<DisclosureGroup> Groups);
internal sealed record ConsumedSet(DisclosureBinding Binding, DocumentHandle Document, ImmutableArray<ConsumedSlice> Slices);
internal sealed record DisclosureSeal<T>(int Version, string Kind, Guid Id, string BindingDigest, T Value);

/// <summary>Private owner codec. Consumers neither construct plaintext origins nor infer contributor completeness.</summary>
internal static class OctoberKnowledgeDisclosureCodec
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new StrictStringConverter() } };
    internal static readonly UTF8Encoding StrictUtf8 = new(false, true);
    internal static string Canonical<T>(T value) => OctoberKnowledgePolicy.CanonicalizeJson(JsonSerializer.Serialize(value, _json));
    internal static T? Read<T>(string value) => JsonSerializer.Deserialize<T>(value, _json);
    internal static string Hash(string value) => Hash(StrictUtf8.GetBytes(value));
    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    internal static string Digest<T>(string domain, T value) => Hash(domain + "\n" + Canonical(value));
    internal static bool IsDigest(string? value) => value is { Length: 64 } && value.All(character => char.IsAsciiHexDigit(character) && !char.IsAsciiLetterLower(character));
    internal static void LimitText(string value, int limit)
    {
        if (StrictUtf8.GetByteCount(value) > limit) throw new DisclosureFailure(KnowledgeDependencyStatus.BudgetExceeded);
    }
    internal static DisclosureBinding Binding(ReadSelectionRequest selection)
    {
        if (selection?.Publication?.Knowledge is null || selection.Subject is null ||
            string.IsNullOrWhiteSpace(selection.Subject.SubjectId) || string.IsNullOrWhiteSpace(selection.Subject.AuthenticationBinding) ||
            selection.Publication.PublicationId == Guid.Empty || selection.Publication.Knowledge.BatchId == Guid.Empty ||
            !Enum.IsDefined(selection.Mode) || !Enum.IsDefined(selection.Purpose) || selection.RepositoryScope.IsDefault ||
            selection.RepositoryScope.Any(id => id == Guid.Empty) || selection.RepositoryScope.Distinct().Count() != selection.RepositoryScope.Length)
            throw new DisclosureFailure(KnowledgeDependencyStatus.InvalidProof);
        _ = StrictUtf8.GetByteCount(selection.Subject.SubjectId);
        foreach (string value in new[] { selection.Publication.Knowledge.CanonicalDatabaseIdentity, selection.Publication.Knowledge.TargetId, selection.Publication.Knowledge.SelectionDigest })
        {
            if (string.IsNullOrWhiteSpace(value)) throw new DisclosureFailure(KnowledgeDependencyStatus.InvalidProof);
            _ = StrictUtf8.GetByteCount(value);
        }
        return new(selection.Publication, selection.Subject.SubjectId, selection.Mode, selection.Purpose,
            selection.RepositoryScope.OrderBy(id => id.ToString("D"), StringComparer.Ordinal).ToImmutableArray());
    }
    internal static ImmutableArray<DisclosureGroup> Groups(IEnumerable<DisclosureRequest> disclosures) => disclosures
        .GroupBy(request => request.Kind).Select(group => new DisclosureGroup(group.Key, group.SelectMany(request => request.AllContributors)
            .Distinct().OrderBy(Canonical, StringComparer.Ordinal).ToImmutableArray()))
        .OrderBy(Canonical, StringComparer.Ordinal).ToImmutableArray();
    private sealed class StrictStringConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string? value = reader.GetString();
            if (value is not null) _ = StrictUtf8.GetByteCount(value);
            return value;
        }
        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            _ = StrictUtf8.GetByteCount(value);
            writer.WriteStringValue(value);
        }
    }

    internal static DisclosureSet Normalize(DisclosureSet set)
    {
        if (set.Origins.IsDefaultOrEmpty) throw new DisclosureFailure(KnowledgeDependencyStatus.UnknownLineage);
        if (set.Binding is null || set.Origins.Any(origin => origin is null || origin.Groups.IsDefault ||
            origin.Groups.Any(group => group is null || group.Contributors.IsDefaultOrEmpty)))
            throw new DisclosureFailure(KnowledgeDependencyStatus.InvalidProof);
        var origins = set.Origins.DistinctBy(Canonical).OrderBy(Canonical, StringComparer.Ordinal).ToImmutableArray();
        if (origins.Length > OctoberKnowledgeDisclosureLimits.MaximumOriginGroups) throw new DisclosureFailure(KnowledgeDependencyStatus.BudgetExceeded);
        int contributors = origins.SelectMany(origin => origin.Groups).SelectMany(group => group.Contributors).Distinct().Count();
        if (contributors > OctoberKnowledgeDisclosureLimits.MaximumContributors) throw new DisclosureFailure(KnowledgeDependencyStatus.BudgetExceeded);
        foreach (var origin in origins)
        {
            if (origin.Kind is not ("root" or "search" or "content" or "related") || origin.Groups.IsDefault ||
                (origin.Kind == "root" && (!IsDigest(origin.InputDigest) || !origin.Groups.IsEmpty || origin.Content is not null || origin.PageDigest != "" || origin.OutputDigest != "" || origin.OutputProfile != "")) ||
                (origin.Kind != "root" && (origin.InputDigest != "" || !IsDigest(origin.PageDigest) || !IsDigest(origin.OutputDigest) || string.IsNullOrWhiteSpace(origin.OutputProfile))) ||
                (origin.Kind == "content") != (origin.Content is not null)) throw new DisclosureFailure(KnowledgeDependencyStatus.InvalidProof);
            foreach (var group in origin.Groups)
            {
                if (group.Contributors.IsDefaultOrEmpty || (group.Kind is { } kind && !Enum.IsDefined(kind)) ||
                    (set.Binding.Mode == KnowledgeMode.Business && group.Kind == DocumentKind.CodeLogic)) throw new DisclosureFailure(KnowledgeDependencyStatus.InvalidProof);
                foreach (var contributor in group.Contributors)
                {
                    if (contributor is null || !Enum.IsDefined(contributor.Kind) || string.IsNullOrWhiteSpace(contributor.ContentDigest)) throw new DisclosureFailure(KnowledgeDependencyStatus.InvalidProof);
                    _ = StrictUtf8.GetByteCount(contributor.ContentDigest);
                    if (contributor.RootBindingIdentity is not null) _ = StrictUtf8.GetByteCount(contributor.RootBindingIdentity);
                }
            }
            if (origin.Content is { } content && (content.Document is null || content.Document.BatchId != set.Binding.Publication.Knowledge.BatchId ||
                content.Document.PublicationId != set.Binding.Publication.PublicationId || content.Start < 0 || content.End <= content.Start ||
                content.End > content.TotalLength || content.ContentBytes < 1 || !IsDigest(content.ProjectionDigest) || !IsDigest(content.ChunkDigest)))
                throw new DisclosureFailure(KnowledgeDependencyStatus.InvalidProof);
        }
        return set with { Origins = origins };
    }
}
internal sealed class DisclosureFailure(KnowledgeDependencyStatus status) : Exception
{
    public KnowledgeDependencyStatus Status { get; } = status;
}
