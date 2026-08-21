using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// Story 12.3 T004 RED slice.  The vectors are immutable contract fixtures.
/// The test-only encoder below is deliberately independent of the future
/// PreparedPass1CanonicalCodec; the production-path assertion uses the
/// existing public CENV writer for one representative vector.
/// </summary>
public sealed class Pass1CanonicalEnvelopeTests
{
    private const string PairDomain = "vulgata/story-12.3/pass1-pair-execution/1.0";
    private const string ChangeDomain = "vulgata/story-12.3/pass1-concurrency-change/1.0";
    private const string LogicalDomain = "vulgata/story-12.3/code-logic-logical-document/1.0";
    private const string VersionDomain = "vulgata/story-12.3/code-logic-document-version/1.0";
    private const string AnchorDomain = "vulgata/story-12.3/source-anchor/1.0";
    private const string ReceiptDomain = "vulgata/story-12.3/navigate-code-receipt/1.0";
    private const string ObservationDomain = "vulgata/story-12.3/reference-observation/1.0";
    private const string SectionDomain = "vulgata/story-12.3/document-section/1.0";
    private const string IndexDomain = "vulgata/story-12.3/index-entry/1.0";
    private const string EdgeDomain = "vulgata/story-12.3/references-edge/1.0";
    private const string CommitDomain = "vulgata/story-12.3/code-logic-commit/1.0";

    private const string PairHex =
        "5647434501060000002b76756c676174612f73746f72792d31322e332f70617373312d706169722d657865637574696f6e2f312e30090000000800010400112233445546778899aabbccddeeff00020410213243546548798a9bacbdcedfe0f100030700000020000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f00040700000020ffeeddccbbaa99887766554433221100ffeeddccbbaa9988776655443322110000050800000002090000000700010422334455667748998aabccddeeff001100020433445566778849aabbccddeeff0011220003044455667788994abbccddeeff001122330004045566778899aa4bccddeeff001122334400050700000020000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f000606000000283031323334353637383961626364656630313233343536373839616263646566303132333435363700070700000020ffeeddccbbaa99887766554433221100ffeeddccbbaa99887766554433221100090000000700010422334455667748998aabccddeeff001100020433445566778849aabbccddeeff0011220003044455667788994abbccddeeff001122330004045566778899aa4bccddeeff001122334400050700000020ffeeddccbbaa99887766554433221100ffeeddccbbaa99887766554433221100000606000000403031323334353637383961626364656630313233343536373839616263646566303132333435363738396162636465663031323334353637383961626364656600070700000020000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f00060800000002060000000870726f6d70742d61060000000870726f6d70742d6200070600000012636f64652d6c6f6769632f66696c652d7632000808000000020700000020000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f0700000020ffeeddccbbaa99887766554433221100ffeeddccbbaa99887766554433221100";

    private static readonly Vector[] Vectors =
    [
        new("pair", PairDomain, PairFields, PairHex, "184effa2148391503e0e00bfedbb1788129bb9c0bd6d678c150b48b39b2176b9", "19b580e0-cea2-8d7a-821b-0dfc0cd4546f", "Pass1PairExecution"),
        new("concurrency change", ChangeDomain, ChangeFields, "5647434501060000002f76756c676174612f73746f72792d31322e332f70617373312d636f6e63757272656e63792d6368616e67652f312e30090000000700010400112233445546778899aabbccddeeff000203000000000000000700030300000000000000040004030000000000000001000503000000000000000800060300000000000000030007060000000d6f776e6572406578616d706c65", "fb6eba79c5e6ba65a24fbe8980ade609c83c5d8d3a21ccaab81757fb135ca466", "beec693b-3921-89cb-96c7-9e7793b7c301", "Pass1ConcurrencyChange"),
        new("logical document", LogicalDomain, LogicalFields, "5647434501060000003276756c676174612f73746f72792d31322e332f636f64652d6c6f6769632d6c6f676963616c2d646f63756d656e742f312e30090000000300010422334455667748998aabccddeeff0011000206000000127372632fe4b89ae58aa12fc39c2e6a6176610003030000000000000002", "d2e2b819d39e0eca4f70a95c62644d3787dd44a24d7101f4775441e9f7fdc058", "19859d97-0836-8fb9-b847-90e20c4a8195", "CodeLogicLogicalDocument"),
        new("document version", VersionDomain, VersionFields, "5647434501060000003276756c676174612f73746f72792d31322e332f636f64652d6c6f6769632d646f63756d656e742d76657273696f6e2f312e300900000003000104778899aabbcc4ddeff0011223344556600020700000020000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f00030600000012636f64652d6c6f6769632f66696c652d7632", "ddb9def59921db2c4d3e9a4568a60fea754a234164ed504baa76a8185886a62f", "d0c70725-2a15-8e6e-af49-1b210c8e8ab0", "CodeLogicDocumentVersion"),
        new("source anchor", AnchorDomain, AnchorFields, "5647434501060000002476756c676174612f73746f72792d31322e332f736f757263652d616e63686f722f312e3009000000090001048899aabbccdd4eff001122334455667700020466778899aabb4ccdeeff001122334455000306000000127372632fe4b89ae58aa12fc39c2e6a617661000403000000000000000000050300000000000000300006030000000000000001000703000000000000000700080700000020000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f000906000000054c373a4333", "1337913119e7e11a32bac785e3da5c32aa85c8038fa73fccfa1295ded78f7a7d", "99be734b-0d64-86f0-88f5-90f4e32b4337", "SourceAnchor"),
        new("source receipt", ReceiptDomain, ReceiptFields, "5647434501060000002c76756c676174612f73746f72792d31322e332f6e617669676174652d636f64652d726563656970742f312e30090000000d000107000000201212121212121212121212121212121212121212121212121212121212121212000204778899aabbcc4ddeff00112233445566000303000000000000000100040300000000000000010005050000000a5265666572656e63657300060300000000000000020007050000000e536f7572636545766964656e636500080000090800000001090000000700010500000006536f75726365000206000000127372632fe4b89ae58aa12fc39c2e6a61766100030600000008e5aea2e688b7c39c0004090000000200010300000000000000070002030000000000000003000509000000020001030000000000000010000203000000000000001800060700000020000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f0007060000001e7372633a2f2f7372632fe4b89ae58aa12fc39c2e6a617661234c373a4333000a030000000000000001000b030000000000000050000c030000000000000001000d030000000000000050", "9e8919ee26c5e87b2ede7cf2708a9157738b8eed1551bd86cee8a63001a141f0", "e6f1b06a-54f9-871d-abed-afe6e917d1ee", "NavigationReceipt"),
        new("depth refusal receipt", ReceiptDomain, DepthReceiptFields, "5647434501060000002c76756c676174612f73746f72792d31322e332f6e617669676174652d636f64652d726563656970742f312e30090000000d000107000000201212121212121212121212121212121212121212121212121212121212121212000204778899aabbcc4ddeff00112233445566000303000000000000000200040300000000000000020005050000000a5265666572656e63657300060300000000000000040007050000000d426f756e64457863656564656400080500000005446570746800090800000000000a030000000000000000000b030000000000000000000c030000000000000000000d030000000000000000", "bcfa62f45e505372b4ddeb3f63f580db0ca5bb2b91b73e2f0fb059036cc966bd", "83acaf84-099a-8f24-bdd3-71dfe074ec91", "NavigationReceipt"),
        new("calls marker receipt", ReceiptDomain, CallsReceiptFields, "5647434501060000002c76756c676174612f73746f72792d31322e332f6e617669676174652d636f64652d726563656970742f312e30090000000d000100000204778899aabbcc4ddeff001122334455660003000004000005000006000007050000000d426f756e6445786365656465640008050000000543616c6c7300090800000000000a030000000000000000000b030000000000000000000c030000000000000008000d030000000000000050", "023969f24b179a5144d862d9603d03b3da37d319be272de9d198bec44b08fb2e", "7bdb3913-dcf7-8f91-a600-1c48e811c089", "NavigationReceipt"),
        new("reference observation", ObservationDomain, ObservationFields, "5647434501060000002c76756c676174612f73746f72792d31322e332f7265666572656e63652d6f62736572766174696f6e2f312e3009000000080001048899aabbccdd4eff001122334455667700020466778899aabb4ccdeeff0011223344550003050000000a5265666572656e6365730004060000000b636c61696d404c373a433300050600000008e5aea2e688b7c39c000606000000167372632fe4b89ae58aa12f5461726765742e6a61766100070700000020ffeeddccbbaa99887766554433221100ffeeddccbbaa998877665544332211000008090000000200010300000000000000070002030000000000000009", "fc2da82c9fa0cd3f4e85c994ad4dd666e6ff3b0d66ef9763b73df2b5197a5605", "fb489a46-ee88-8576-9ec5-aae54728e709", "ReferenceObservation"),
        new("section", SectionDomain, SectionFields, "5647434501060000002776756c676174612f73746f72792d31322e332f646f63756d656e742d73656374696f6e2f312e3009000000020001048899aabbccdd4eff001122334455667700020500000015436f6d6d756e69636174696f6e45766964656e6365", "226308f94595bdfeba0e8313626bac1cff6d6375fa2040e2d5832d21318ca3f9", "e6cc743e-30f2-8df5-af04-07a5b00eacbc", "DocumentSection"),
        new("index", IndexDomain, IndexFields, "5647434501060000002276756c676174612f73746f72792d31322e332f696e6465782d656e7472792f312e3009000000020001048899aabbccdd4eff001122334455667700020500000009436f64654c6f676963", "b43bd66b574db348554ba211a05ce0542260062bffc5ded4af8e9d924797ce80", "04044ddb-ffd2-8991-b291-b6de40715d60", "IndexEntry"),
        new("edge", EdgeDomain, EdgeFields, "5647434501060000002676756c676174612f73746f72792d31322e332f7265666572656e6365732d656467652f312e3009000000030001048899aabbccdd4eff0011223344556677000204aabbccddeeff401122334455667788990003050000000a5265666572656e636573", "410d55e8ecc7c3a23c5d6752e1e792dac4d4d6f2b708a98b29561a58cf709c99", "67d59b95-01d2-88cc-b277-d2364ab3f933", "ReferencesEdge"),
        new("commit", CommitDomain, CommitFields, "5647434501060000002876756c676174612f73746f72792d31322e332f636f64652d6c6f6769632d636f6d6d69742f312e30090000000b00010400112233445546778899aabbccddeeff000204778899aabbcc4ddeff0011223344556600030499aabbccddee4f0011223344556677880004048899aabbccdd4eff001122334455667700050700000020000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f000608000000020700000020ffeeddccbbaa99887766554433221100ffeeddccbbaa998877665544332211000700000020000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f0007080000000104aabbccddeeff40112233445566778899000808000000010700000020000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f00090700000020ffeeddccbbaa99887766554433221100ffeeddccbbaa99887766554433221100000a080000000107000000203333333333333333333333333333333333333333333333333333333333333333000b080000000107000000204444444444444444444444444444444444444444444444444444444444444444", "ba7596b3b58dd40a253253057951e5c89f59d647b45b1797ba507506ca5fe6b4", "", null)
    ];

    [Fact]
    public void PreparedPass1CanonicalCodec_ExposesVersionedPublicSeam()
    {
        Type? codec = typeof(CanonicalEnvelope).Assembly.GetType(
            "Vulgata.Core.DomainServices.PreparedPass1CanonicalCodec");

        Assert.True(
            codec is not null,
            "Story 12.3 T005 production canonical codec is missing; this RED is expected before implementation.");
        Assert.Contains(
            codec!.GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name.Contains("Encode", StringComparison.Ordinal) ||
                method.Name.Contains("Create", StringComparison.Ordinal) ||
                method.Name.Contains("Derive", StringComparison.Ordinal));
    }

    [Fact]
    public void IndependentReconstructor_ReproducesLiteralEnvelopeDigestAndUuidVectors()
    {
        foreach (Vector vector in Vectors)
        {
            byte[] envelope = IndependentEnvelope(vector.Domain, vector.Fields());
            string digest = Convert.ToHexStringLower(SHA256.HashData(envelope));

            Assert.Equal(vector.EnvelopeHex, Convert.ToHexStringLower(envelope));
            Assert.Equal(vector.Digest, digest);
            if (vector.Kind is not null)
            {
                Assert.Equal(vector.Uuid, DeriveUuid(vector.Kind, Convert.FromHexString(digest)).ToString("D"));
            }
        }
    }

    [Fact]
    public void ExistingStory12_1JobIdentity_RemainsTheStableJobIdentity()
    {
        OrdinaryPass1JobIdentity identity = OrdinaryPass1JobIdentity.Create(
            Guid.Parse("11111111-2222-3333-8444-555555555555"),
            Guid.Parse("66666666-7777-4888-9999-aaaaaaaaaaaa"),
            Guid.Parse("bbbbbbbb-cccc-4ddd-8eee-ffffffffffff"),
            Guid.Parse("01234567-89ab-4cde-8f01-23456789abcd"),
            "0123456789abcdef0123456789abcdef01234567");

        Assert.Equal(Guid.Parse("6b9362f7-193b-88d9-8d9c-b2a21ecfe2fd"), identity.JobId);
    }

    [Fact]
    public void ExistingProductionCenvWriter_AgreesWithIndependentLogicalDocumentVector()
    {
        const string productionDomain =
            "vulgata/story-12.2/prepared-scan-context-snapshot/1.0";
        IReadOnlyList<Field> fields =
        [
            F(1, U("22334455-6677-4899-8aab-ccddeeff0011")),
            F(2, U("33445566-7788-49aa-bbcc-ddeeff001122")),
            F(3, S("0123456789abcdef0123456789abcdef01234567")),
            F(4, S("scan-context-snapshot/1.0")),
            F(5, B("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f"))
        ];
        byte[] production = CanonicalEnvelope.Create(
            productionDomain,
            ToProductionObject(fields));

        Assert.Equal(
            Convert.ToHexStringLower(IndependentEnvelope(productionDomain, fields)),
            Convert.ToHexStringLower(production));
        Assert.Equal(
            IndependentEnvelope(productionDomain, fields),
            production);
    }

    private static IReadOnlyList<Field> PairFields() =>
    [
        F(1, U("00112233-4455-4677-8899-aabbccddeeff")),
        F(2, U("10213243-5465-4879-8a9b-acbdcedfe0f1")),
        F(3, B("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f")),
        F(4, B("ffeeddccbbaa99887766554433221100ffeeddccbbaa99887766554433221100")),
        F(5, L(PairBindingOne(), PairBindingTwo())),
        F(6, L(S("prompt-a"), S("prompt-b"))),
        F(7, S("code-logic/file-v2")),
        F(8, L(B("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f"), B("ffeeddccbbaa99887766554433221100ffeeddccbbaa99887766554433221100")))
    ];

    private static Value PairBindingOne() => O(
        F(1, U("22334455-6677-4899-8aab-ccddeeff0011")),
        F(2, U("33445566-7788-49aa-bbcc-ddeeff001122")),
        F(3, U("44556677-8899-4abb-ccdd-eeff00112233")),
        F(4, U("55667788-99aa-4bcc-ddee-ff0011223344")),
        F(5, B("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f")),
        F(6, S("0123456789abcdef0123456789abcdef01234567")),
        F(7, B("ffeeddccbbaa99887766554433221100ffeeddccbbaa99887766554433221100")));

    private static Value PairBindingTwo() => O(
        F(1, U("22334455-6677-4899-8aab-ccddeeff0011")),
        F(2, U("33445566-7788-49aa-bbcc-ddeeff001122")),
        F(3, U("44556677-8899-4abb-ccdd-eeff00112233")),
        F(4, U("55667788-99aa-4bcc-ddee-ff0011223344")),
        F(5, B("ffeeddccbbaa99887766554433221100ffeeddccbbaa99887766554433221100")),
        F(6, S("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")),
        F(7, B("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f")));

    private static IReadOnlyList<Field> ChangeFields() => [F(1, U("00112233-4455-4677-8899-aabbccddeeff")), F(2, Q(7)), F(3, Q(4)), F(4, Q(1)), F(5, Q(8)), F(6, Q(3)), F(7, S("owner@example"))];
    private static IReadOnlyList<Field> LogicalFields() => [F(1, U("22334455-6677-4899-8aab-ccddeeff0011")), F(2, S("src/业务/Ü.java")), F(3, Q(2))];
    private static IReadOnlyList<Field> VersionFields() => [F(1, U("778899aa-bbcc-4dde-ff00-112233445566")), F(2, B("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f")), F(3, S("code-logic/file-v2"))];
    private static IReadOnlyList<Field> AnchorFields() => [F(1, U("8899aabb-ccdd-4eff-0011-223344556677")), F(2, U("66778899-aabb-4ccd-eeff-001122334455")), F(3, S("src/业务/Ü.java")), F(4, Q(0)), F(5, Q(48)), F(6, Q(1)), F(7, Q(7)), F(8, B("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f")), F(9, S("L7:C3"))];
    private static IReadOnlyList<Field> ReceiptFields() => [F(1, B("1212121212121212121212121212121212121212121212121212121212121212")), F(2, U("778899aa-bbcc-4dde-ff00-112233445566")), F(3, Q(1)), F(4, Q(1)), F(5, E("References")), F(6, Q(2)), F(7, E("SourceEvidence")), F(8, N()), F(9, L(O(F(1, E("Source")), F(2, S("src/业务/Ü.java")), F(3, S("客户Ü")), F(4, O(F(1, Q(7)), F(2, Q(3)))), F(5, O(F(1, Q(16)), F(2, Q(24)))), F(6, B("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f")), F(7, S("src://src/业务/Ü.java#L7:C3"))))), F(10, Q(1)), F(11, Q(80)), F(12, Q(1)), F(13, Q(80))];
    private static IReadOnlyList<Field> DepthReceiptFields() => [F(1, B("1212121212121212121212121212121212121212121212121212121212121212")), F(2, U("778899aa-bbcc-4dde-ff00-112233445566")), F(3, Q(2)), F(4, Q(2)), F(5, E("References")), F(6, Q(4)), F(7, E("BoundExceeded")), F(8, E("Depth")), F(9, L()), F(10, Q(0)), F(11, Q(0)), F(12, Q(0)), F(13, Q(0))];
    private static IReadOnlyList<Field> CallsReceiptFields() => [F(1, N()), F(2, U("778899aa-bbcc-4dde-ff00-112233445566")), F(3, N()), F(4, N()), F(5, N()), F(6, N()), F(7, E("BoundExceeded")), F(8, E("Calls")), F(9, L()), F(10, Q(0)), F(11, Q(0)), F(12, Q(8)), F(13, Q(80))];
    private static IReadOnlyList<Field> ObservationFields() => [F(1, U("8899aabb-ccdd-4eff-0011-223344556677")), F(2, U("66778899-aabb-4ccd-eeff-001122334455")), F(3, E("References")), F(4, S("claim@L7:C3")), F(5, S("客户Ü")), F(6, S("src/业务/Target.java")), F(7, B("ffeeddccbbaa99887766554433221100ffeeddccbbaa99887766554433221100")), F(8, O(F(1, Q(7)), F(2, Q(9))))];
    private static IReadOnlyList<Field> SectionFields() => [F(1, U("8899aabb-ccdd-4eff-0011-223344556677")), F(2, E("CommunicationEvidence"))];
    private static IReadOnlyList<Field> IndexFields() => [F(1, U("8899aabb-ccdd-4eff-0011-223344556677")), F(2, E("CodeLogic"))];
    private static IReadOnlyList<Field> EdgeFields() => [F(1, U("8899aabb-ccdd-4eff-0011-223344556677")), F(2, U("aabbccdd-eeff-4011-2233-445566778899")), F(3, E("References"))];
    private static IReadOnlyList<Field> CommitFields() => [F(1, U("00112233-4455-4677-8899-aabbccddeeff")), F(2, U("778899aa-bbcc-4dde-ff00-112233445566")), F(3, U("99aabbcc-ddee-4f00-1122-334455667788")), F(4, U("8899aabb-ccdd-4eff-0011-223344556677")), F(5, B("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f")), F(6, L(B("ffeeddccbbaa99887766554433221100ffeeddccbbaa99887766554433221100"), B("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f"))), F(7, L(U("aabbccdd-eeff-4011-2233-445566778899"))), F(8, L(B("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f"))), F(9, B("ffeeddccbbaa99887766554433221100ffeeddccbbaa99887766554433221100")), F(10, L(B("3333333333333333333333333333333333333333333333333333333333333333"))), F(11, L(B("4444444444444444444444444444444444444444444444444444444444444444")))];

    private static Field F(ushort tag, Value value) => new(tag, value);
    private static Value U(string value) => new UuidValue(Guid.Parse(value));
    private static Value S(string value) => new StringValue(value);
    private static Value E(string value) => new EnumValue(value);
    private static Value B(string value) => new BytesValue(Convert.FromHexString(value));
    private static Value Q(ulong value) => new UIntValue(value);
    private static Value N() => NullValue.Instance;
    private static Value L(params Value[] values) => new ListValue(values);
    private static Value O(params Field[] fields) => new ObjectValue(fields);

    private static byte[] IndependentEnvelope(string domain, IReadOnlyList<Field> fields)
    {
        using var stream = new MemoryStream();
        stream.Write("VGCE"u8);
        stream.WriteByte(1);
        WriteString(stream, domain, 0x06);
        WriteValue(stream, new ObjectValue(fields));
        return stream.ToArray();
    }

    private static void WriteValue(Stream stream, Value value)
    {
        switch (value)
        {
            case NullValue:
                stream.WriteByte(0);
                break;
            case UIntValue unsigned:
                stream.WriteByte(3);
                Span<byte> integer = stackalloc byte[8];
                BinaryPrimitives.WriteUInt64BigEndian(integer, unsigned.Value);
                stream.Write(integer);
                break;
            case UuidValue uuid:
                stream.WriteByte(4);
                stream.Write(Convert.FromHexString(uuid.Value.ToString("D").Replace("-", string.Empty, StringComparison.Ordinal)));
                break;
            case EnumValue @enum:
                WriteString(stream, @enum.Value, 5);
                break;
            case StringValue text:
                WriteString(stream, text.Value, 6);
                break;
            case BytesValue bytes:
                WriteBytes(stream, bytes.Value, 7);
                break;
            case ListValue list:
                stream.WriteByte(8);
                WriteUInt32(stream, checked((uint)list.Values.Count));
                foreach (Value item in list.Values)
                {
                    WriteValue(stream, item);
                }
                break;
            case ObjectValue @object:
                stream.WriteByte(9);
                WriteUInt32(stream, checked((uint)@object.Fields.Count));
                foreach (Field field in @object.Fields)
                {
                    Span<byte> tag = stackalloc byte[2];
                    BinaryPrimitives.WriteUInt16BigEndian(tag, field.Tag);
                    stream.Write(tag);
                    WriteValue(stream, field.Value);
                }
                break;
            default:
                throw new InvalidOperationException($"Unhandled canonical test value {value.GetType().Name}.");
        }
    }

    private static void WriteString(Stream stream, string value, byte type)
    {
        stream.WriteByte(type);
        WriteBytes(stream, Encoding.UTF8.GetBytes(value), 0);
    }

    private static void WriteBytes(Stream stream, byte[] bytes, byte type)
    {
        if (type != 0)
        {
            stream.WriteByte(type);
        }

        WriteUInt32(stream, checked((uint)bytes.Length));
        stream.Write(bytes);
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static Guid DeriveUuid(string kind, ReadOnlySpan<byte> fingerprint)
    {
        byte[] envelope = IndependentEnvelope(
            "vulgata/story-12.3/deterministic-uuid/1.0",
            [F(1, E(kind)), F(2, new BytesValue(fingerprint.ToArray()))]);
        byte[] digest = SHA256.HashData(envelope);
        digest[6] = (byte)((digest[6] & 0x0f) | 0x80);
        digest[8] = (byte)((digest[8] & 0x3f) | 0x80);
        return new Guid(digest.AsSpan(0, 16), bigEndian: true);
    }

    private static CanonicalValue ToProduction(Value value) => value switch
    {
        NullValue => CanonicalEnvelope.Null(),
        UIntValue unsigned => CanonicalEnvelope.UInt64(unsigned.Value),
        UuidValue uuid => CanonicalEnvelope.Uuid(uuid.Value),
        EnumValue @enum => CanonicalEnvelope.Enum(@enum.Value),
        StringValue text => CanonicalEnvelope.String(text.Value),
        BytesValue bytes => CanonicalEnvelope.Bytes(bytes.Value),
        ListValue list => CanonicalEnvelope.List(list.Values.Select(ToProduction)),
        ObjectValue @object => ToProductionObject(@object.Fields),
        _ => throw new InvalidOperationException($"Unhandled production test value {value.GetType().Name}.")
    };

    private static CanonicalValue ToProductionObject(IReadOnlyList<Field> fields) =>
        CanonicalEnvelope.Object(fields.Select(field => new CanonicalField(field.Tag, ToProduction(field.Value))));

    private abstract record Value;
    private sealed record NullValue : Value { public static NullValue Instance { get; } = new(); }
    private sealed record UIntValue(ulong Value) : Value;
    private sealed record UuidValue(Guid Value) : Value;
    private sealed record EnumValue(string Value) : Value;
    private sealed record StringValue(string Value) : Value;
    private sealed record BytesValue(byte[] Value) : Value;
    private sealed record ListValue(IReadOnlyList<Value> Values) : Value;
    private sealed record ObjectValue(IReadOnlyList<Field> Fields) : Value;
    private sealed record Field(ushort Tag, Value Value);
    private sealed record Vector(string Name, string Domain, Func<IReadOnlyList<Field>> Fields, string EnvelopeHex, string Digest, string Uuid, string? Kind);
}
