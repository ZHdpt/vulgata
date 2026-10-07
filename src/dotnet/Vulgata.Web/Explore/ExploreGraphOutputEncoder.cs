using System.Collections.Immutable;
using System.Text.Json;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Web.Explore;

public sealed class ExploreGraphOutputEncoder : IOctoberKnowledgeOutputEncoder
{
    private static readonly JsonSerializerOptions _canonicalOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public string ProfileId => "october.explore.graph.default-json.v1";
    public ImmutableArray<byte> EncodeSearch(SearchPage page) => [.. JsonSerializer.SerializeToUtf8Bytes(page)];
    public ImmutableArray<byte> EncodeContent(ContentPage page) => [.. JsonSerializer.SerializeToUtf8Bytes(page)];
    public ImmutableArray<byte> EncodeRelated(RelatedPage page) => [.. JsonSerializer.SerializeToUtf8Bytes(page)];

    /// <summary>Owner-declared mechanical page binding, not capsule authentication or permission.</summary>
    public static string ComputePageDigest<T>(T page) where T : class =>
        OctoberKnowledgePolicy.Hash("october.knowledge.page.v1\n" + typeof(T).Name + "\n" +
            OctoberKnowledgePolicy.CanonicalizeJson(JsonSerializer.Serialize(page, _canonicalOptions)));
}
