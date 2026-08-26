using System.Security.Cryptography;
using System.Text;

namespace Vulgata.Tests.PreparedPass2;

/// <summary>
/// Small, deterministic fixtures shared by the Story 12.4 contract tests.
/// They contain identifiers and fingerprints only; source content is never
/// required by the foundation contract tests.
/// </summary>
internal static class Pass2TestFixtures
{
    internal static readonly Guid SystemId = Guid.Parse("10000000-0000-4000-8000-000000000001");
    internal static readonly Guid ConsumerRepositoryId = Guid.Parse("20000000-0000-4000-8000-000000000002");
    internal static readonly Guid ProviderRepositoryId = Guid.Parse("30000000-0000-4000-8000-000000000003");
    internal static readonly Guid ConsumerDocumentVersionId = Guid.Parse("40000000-0000-4000-8000-000000000004");
    internal static readonly Guid ProviderXmlDocumentVersionId = Guid.Parse("50000000-0000-4000-8000-000000000005");
    internal static readonly Guid ProviderImplementationDocumentVersionId = Guid.Parse("60000000-0000-4000-8000-000000000006");
    internal static readonly Guid RelationshipId = Guid.Parse("70000000-0000-4000-8000-000000000007");
    internal static readonly Guid FlowGroupId = Guid.Parse("80000000-0000-4000-8000-000000000008");

    internal const string ProfiledRevision = "0123456789abcdef0123456789abcdef01234567";
    internal const string Protocol = "custom-rpc";
    internal const string NormalizedEndpoint = "ledger.record";
    internal const string ConsumerEntryStableKey = "src/neutral/OrderPolicy.java#OrderPolicy.submit";

    internal static ResolutionFixture ExactResolution() => new(
        SystemId,
        ConsumerRepositoryId,
        ProviderRepositoryId,
        ConsumerDocumentVersionId,
        ProviderXmlDocumentVersionId,
        ProviderImplementationDocumentVersionId,
        RelationshipId,
        FlowGroupId,
        Protocol,
        NormalizedEndpoint,
        ConsumerEntryStableKey,
        ProfiledRevision);

    internal static string Sha256(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

internal sealed record ResolutionFixture(
    Guid SystemId,
    Guid ConsumerRepositoryId,
    Guid ProviderRepositoryId,
    Guid ConsumerDocumentVersionId,
    Guid ProviderXmlDocumentVersionId,
    Guid ProviderImplementationDocumentVersionId,
    Guid RelationshipId,
    Guid FlowGroupId,
    string Protocol,
    string NormalizedEndpoint,
    string ConsumerEntryStableKey,
    string ProfiledRevision);
