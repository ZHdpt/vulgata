using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Tests.October.Knowledge;

namespace Vulgata.Tests.October.Chat;

/// <summary>Controlled Chat consumer wiring over Business's fixture; no host, provider or durable storage.</summary>
internal sealed class ControlledChatFixture
{
    public ControlledChatFixture()
    {
        CanonicalReader = new ControlledKnowledgeReader(Knowledge, Policy);
        Reader = new RecordingReader(CanonicalReader);
    }

    public OctoberKnowledgeFixture Knowledge { get; } = new();
    public ControlledDisclosurePolicy Policy { get; } = new();
    public ControlledKnowledgeReader CanonicalReader { get; }
    public RecordingReader Reader { get; }

    public ReadSelectionRequest Selection(KnowledgeMode mode = KnowledgeMode.IT) =>
        Knowledge.Selection(mode) with { Purpose = KnowledgePurpose.Chat };

    internal sealed class RecordingReader(IOctoberKnowledgeReader inner) : IOctoberKnowledgeReader
    {
        public List<ReadSelectionRequest> OpenRequests { get; } = [];
        public int SearchCalls { get; private set; }
        public KnowledgeReadStatus? OpenStatus { get; set; }
        public KnowledgeReadStatus? SearchStatus { get; set; }
        public bool ThrowOnOpen { get; set; }
        public TaskCompletionSource? OpenGate { get; set; }

        public async Task<ReadSelectionResult> OpenReadSelectionAsync(ReadSelectionRequest request, CancellationToken cancellationToken = default)
        {
            OpenRequests.Add(request);
            if (OpenGate is not null)
            {
                await OpenGate.Task.WaitAsync(cancellationToken);
            }

            if (ThrowOnOpen)
            {
                throw new InvalidOperationException("controlled private connection failure");
            }

            return OpenStatus is { } status
                ? new(status, null)
                : await inner.OpenReadSelectionAsync(request, cancellationToken);
        }

        public Task<SearchPage> SearchAsync(KnowledgeSearchRequest request, CancellationToken cancellationToken = default)
        {
            SearchCalls++;
            return SearchStatus is { } status
                ? Task.FromResult(new SearchPage(status, [], null, null))
                : inner.SearchAsync(request, cancellationToken);
        }

        public Task<ContentPage> ReadExactAsync(ExactReadRequest request, CancellationToken cancellationToken = default) =>
            inner.ReadExactAsync(request, cancellationToken);

        public Task<RelatedPage> GetRelatedAsync(RelatedReadRequest request, CancellationToken cancellationToken = default) =>
            inner.GetRelatedAsync(request, cancellationToken);
    }
}
