using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.OctoberGenerator;

/// <summary>Scan-private mapping of the live Identity owner's exact reader binding.</summary>
internal sealed record OctoberVerifiedReaderBinding(KnowledgeSubject Subject,
    ReadSelectionRequest Selection, IOctoberKnowledgeReader Reader, IAsyncDisposable Lifetime);
