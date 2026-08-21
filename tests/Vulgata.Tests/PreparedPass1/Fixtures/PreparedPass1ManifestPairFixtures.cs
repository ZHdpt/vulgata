using System.Security.Cryptography;
using System.Text;

namespace Vulgata.Tests.PreparedPass1.Fixtures;

internal static class PreparedPass1ManifestPairFixtures
{
    internal const string ProfiledRevision = "0123456789abcdef0123456789abcdef01234567";

    private static readonly Guid _coordinationId =
        Guid.Parse("12345678-90ab-4cde-8f01-234567890abc");
    private static readonly Guid _repositoryOneId =
        Guid.Parse("10000000-0000-4000-8000-000000000001");
    private static readonly Guid _repositoryTwoId =
        Guid.Parse("20000000-0000-4000-8000-000000000002");
    private static readonly Guid _scanRunOneId =
        Guid.Parse("30000000-0000-4000-8000-000000000003");
    private static readonly Guid _scanRunTwoId =
        Guid.Parse("40000000-0000-4000-8000-000000000004");

    internal static ApprovedPreparedManifestPairSeed CreateApprovedPair()
    {
        PreparedManifestSeed first = CreateManifest(
            _repositoryOneId,
            _scanRunOneId,
            "repository-one",
            1,
            [
                Source("src/main/java/neutral/OrderPolicy.java", "java", OrderPolicySource, 1),
                Source("src/main/resources/neutral/order-policy.xml", "xml", OrderPolicyXml, 2),
            ]);
        PreparedManifestSeed second = CreateManifest(
            _repositoryTwoId,
            _scanRunTwoId,
            "repository-two",
            3,
            [
                Source("src/main/java/neutral/LedgerGateway.java", "java", LedgerGatewaySource, 1),
                Source("src/main/resources/neutral/ledger-channel.xml", "xml", LedgerChannelXml, 2),
            ]);

        return new(
            _coordinationId,
            [first, second],
            Hash($"pair:{_coordinationId:D}:{first.ManifestFingerprint}:{second.ManifestFingerprint}"));
    }

    private static PreparedManifestSeed CreateManifest(
        Guid repositoryId,
        Guid scanRunId,
        string fixtureDirectory,
        int firstPairSelectionOrdinal,
        IReadOnlyList<PreparedFileCodeUnitSeed> files)
    {
        PreparedFileCodeUnitSeed[] ordered =
        [
            .. files.Select((file, index) => file with
            {
                ManifestOrdinal = index + 1,
                PairSelectionOrdinal = firstPairSelectionOrdinal + index,
            }),
        ];
        string fingerprint = Hash(string.Join(
            '|',
            repositoryId.ToString("D"),
            scanRunId.ToString("D"),
            ProfiledRevision,
            string.Join(';', ordered.Select(file => file.IdentityFingerprint))));
        return new(repositoryId, scanRunId, ProfiledRevision, fixtureDirectory, fingerprint, ordered);
    }

    private static PreparedFileCodeUnitSeed Source(
        string relativePath,
        string language,
        string source,
        int manifestOrdinal)
    {
        string sourceHash = Hash(source);
        return new(
            relativePath,
            language,
            source,
            sourceHash,
            sourceHash,
            Hash($"unit:{ProfiledRevision}:{relativePath}:{sourceHash}"),
            manifestOrdinal,
            manifestOrdinal);
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private const string OrderPolicySource =
        "package neutral;\n\npublic final class OrderPolicy {\n    public boolean maySubmit(int amount) {\n        return amount > 0 && amount <= 1000;\n    }\n}\n";
    private const string OrderPolicyXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<policy name=\"order-submit\" maximum=\"1000\" />\n";
    private const string LedgerGatewaySource =
        "package neutral;\n\npublic final class LedgerGateway {\n    public String record(String orderId) {\n        return \"recorded:\" + orderId;\n    }\n}\n";
    private const string LedgerChannelXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<channel name=\"ledger-record\" transport=\"http\" />\n";
}

internal sealed record ApprovedPreparedManifestPairSeed(
    Guid CoordinationId,
    IReadOnlyList<PreparedManifestSeed> Manifests,
    string PairFingerprint);

internal sealed record PreparedManifestSeed(
    Guid RepositoryId,
    Guid ScanRunId,
    string ProfiledRevision,
    string FixtureDirectory,
    string ManifestFingerprint,
    IReadOnlyList<PreparedFileCodeUnitSeed> FileCodeUnits);

internal sealed record PreparedFileCodeUnitSeed(
    string RelativePath,
    string Language,
    string Source,
    string FileHash,
    string SourceHash,
    string IdentityFingerprint,
    int ManifestOrdinal,
    int PairSelectionOrdinal);
