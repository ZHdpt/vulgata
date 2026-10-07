using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Shared.Explore;

/// <summary>Opaque presentation continuation; only the issuing facade can consume it.</summary>
public abstract class ExploreReadSession { protected ExploreReadSession() { } }
public abstract class ExploreBrowseSession { protected ExploreBrowseSession() { } }
public sealed record ExploreReadResult(ContentPage Page, ExploreReadSession? Session,
    KnowledgeMode? Mode = null, bool IsControlledFixture = false);
public sealed record ExploreBrowseResult(SearchPage Page, ExploreBrowseSession? Session,
    KnowledgeMode? Mode = null, bool IsControlledFixture = false);
