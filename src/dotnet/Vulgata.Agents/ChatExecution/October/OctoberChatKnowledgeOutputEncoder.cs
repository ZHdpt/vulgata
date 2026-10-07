using System.Collections.Immutable;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Agents.ChatExecution.October;

/// <summary>Mechanical complete Chat wrapper encoding; no disclosure or persistence authority.</summary>
internal sealed class OctoberChatKnowledgeOutputEncoder : IOctoberKnowledgeOutputEncoder
{
    public string ProfileId => "october.chat.tool-result.v1";
    public ImmutableArray<byte> EncodeSearch(SearchPage page) => Encode(new(OctoberChatToolStatus.Succeeded, Search: page, References: []));
    public ImmutableArray<byte> EncodeContent(ContentPage page) => Encode(new(OctoberChatToolStatus.Succeeded, Content: page, References: []));
    public ImmutableArray<byte> EncodeRelated(RelatedPage page) => Encode(new(OctoberChatToolStatus.Succeeded, Related: page, References: []));
    private static ImmutableArray<byte> Encode(OctoberChatToolResult result) => OctoberWikiTools.SerializeResultUtf8(result);
}
