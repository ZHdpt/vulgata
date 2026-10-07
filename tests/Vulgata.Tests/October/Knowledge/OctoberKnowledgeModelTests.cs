using System.Collections.Immutable;
using System.Text.Json;
using System.Numerics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities.OctoberKnowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

public sealed class OctoberKnowledgeModelTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void CanonicalizeJson_ObjectsAndNarrativeArrays_SortsKeysButPreservesMeaning()
    {
        Assert.Equal("{\"a\":{\"b\":2,\"z\":1},\"narrative\":[\"second\",\"first\"]}", OctoberKnowledgePolicy.CanonicalizeJson("{\"narrative\":[\"second\",\"first\"],\"a\":{\"z\":1,\"b\":2}}"));
        Assert.NotEqual(OctoberKnowledgePolicy.CanonicalizeJson("{\"text\":\" a \"}"), OctoberKnowledgePolicy.CanonicalizeJson("{\"text\":\"a\"}"));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("{\"a\":1,\"a\":2}")]
    [InlineData("{\"nested\":{\"a\":1,\"a\":2}}")]
    [InlineData("{\"value\":NaN}")]
    [InlineData("{\"value\":1e9999}")]
    public void CanonicalizeJson_AmbiguousOrNonfiniteJson_Rejects(string json) => Assert.ThrowsAny<Exception>(() => OctoberKnowledgePolicy.CanonicalizeJson(json));

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void CreateContentJson_IndependentSchema_ProducesExactCanonicalQualifiedContent()
    {
        ImmutableArray<DocumentSection> sections = [new("s", 0, "Overview", [new("g", "A stated hypothesis.", ClaimQualification.Hypothesis, [], ["Not source proof"])])];
        const string expected = "{\"schemaVersion\":1,\"title\":\"Title\",\"sections\":[{\"sectionId\":\"s\",\"ordinal\":0,\"title\":\"Overview\",\"claims\":[{\"claimGroupId\":\"g\",\"text\":\"A stated hypothesis.\",\"qualification\":2,\"evidence\":[],\"limitations\":[\"Not source proof\"]}]}]}";
        using var parsed = JsonDocument.Parse(OctoberKnowledgePolicy.CreateContentJson("Title", sections));
        using var expectedParsed = JsonDocument.Parse(expected);
        Assert.True(JsonElement.DeepEquals(expectedParsed.RootElement, parsed.RootElement));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ComputeProposalDigest_BoundIdentityAndSelfField_IsStableCompleteAndOrderAwareAsync()
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var proposal = fixture.Proposal;
        string digest = OctoberKnowledgePolicy.ComputeProposalDigest(proposal);
        Assert.Matches("^[0-9A-Fa-f]{64}$", digest);
        Assert.Equal(digest, OctoberKnowledgePolicy.ComputeProposalDigest(proposal with { CanonicalProposalDigest = "ignored-self-field" }));
        Assert.Equal(digest, OctoberKnowledgePolicy.ComputeProposalDigest(proposal with { Contributors = proposal.Contributors.Reverse().ToImmutableArray() }));
        Assert.NotEqual(digest, OctoberKnowledgePolicy.ComputeProposalDigest(proposal with { Binding = proposal.Binding with { BatchId = Guid.NewGuid() } }));
        Assert.NotEqual(digest, OctoberKnowledgePolicy.ComputeProposalDigest(proposal with { Origin = proposal.Origin with { PreparedVersion = "other-prepared-version" } }));
        Assert.NotEqual(digest, OctoberKnowledgePolicy.ComputeProposalDigest(proposal with { Contributors = [fixture.SourceContributor] }));
        Assert.NotEqual(digest, OctoberKnowledgePolicy.ComputeProposalDigest(proposal with { Uncertainties = [] }));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("duplicate-unit")]
    [InlineData("orphan-parent")]
    [InlineData("cycle")]
    [InlineData("anchor-range")]
    [InlineData("claim-anchor")]
    [InlineData("observation-owner")]
    [InlineData("observation-anchor")]
    [InlineData("uncertainty-anchor")]
    [InlineData("coverage-origin")]
    [InlineData("missing-contributor")]
    [InlineData("unsupported-schema")]
    [InlineData("duplicate-section")]
    [InlineData("duplicate-ordinal")]
    [InlineData("duplicate-claim")]
    [InlineData("divergent-json")]
    [InlineData("unattributed-mixed")]
    public async Task Validate_InvalidNestedMeaning_RejectsBeforeStorageAsync(string mutation)
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        var proposal = fixture.Proposal;
        var root = proposal.Units[0];
        var section = root.Sections[0];
        var badEvidence = new EvidenceReference(null, null, new(fixture.Origin with { SourceDigest = "wrong-source" }, 10, 12), null, "wrong-source");
        switch (mutation)
        {
            case "duplicate-unit": proposal = proposal with { Units = [root, root] }; break;
            case "orphan-parent": proposal = proposal with { Units = proposal.Units.SetItem(1, proposal.Units[1] with { ParentSelector = "missing" }) }; break;
            case "cycle": proposal = proposal with { Units = proposal.Units.SetItem(0, root with { ParentSelector = proposal.Units[1].SemanticSelector }) }; break;
            case "anchor-range": proposal = proposal with { Units = proposal.Units.SetItem(0, root with { Anchors = [new(fixture.Origin, 1, 101)] }) }; break;
            case "claim-anchor": root = root with { Sections = [section with { Claims = [section.Claims[0] with { Evidence = [badEvidence] }] }] }; proposal = proposal with { Units = proposal.Units.SetItem(0, Rebuild(root)) }; break;
            case "observation-owner": proposal = proposal with { Observations = [proposal.Observations[0] with { OwnerSelector = "missing" }] }; break;
            case "observation-anchor": proposal = proposal with { Observations = [proposal.Observations[0] with { Evidence = [badEvidence] }] }; break;
            case "uncertainty-anchor": proposal = proposal with { Uncertainties = [proposal.Uncertainties[0] with { Evidence = [badEvidence] }] }; break;
            case "coverage-origin": proposal = proposal with { Coverage = proposal.Coverage with { Origin = fixture.Origin with { RelativePath = "other.cs" } } }; break;
            case "missing-contributor": proposal = proposal with { Contributors = [] }; break;
            case "unsupported-schema": proposal = proposal with { SchemaVersion = 99 }; break;
            case "duplicate-section": root = root with { Sections = [section, section with { Ordinal = 1 }] }; proposal = proposal with { Units = proposal.Units.SetItem(0, Rebuild(root)) }; break;
            case "duplicate-ordinal": root = root with { Sections = [section, section with { SectionId = "other" }] }; proposal = proposal with { Units = proposal.Units.SetItem(0, Rebuild(root)) }; break;
            case "duplicate-claim": root = root with { Sections = [section with { Claims = [section.Claims[0], section.Claims[0]] }] }; proposal = proposal with { Units = proposal.Units.SetItem(0, Rebuild(root)) }; break;
            case "divergent-json": proposal = proposal with { Units = proposal.Units.SetItem(0, root with { ContentJson = root.ContentJson.Replace("Acquires", "Never acquires", StringComparison.Ordinal) }) }; break;
            case "unattributed-mixed": root = root with { Sections = [section with { Claims = [section.Claims[0] with { Qualification = ClaimQualification.Mixed, Evidence = section.Claims[0].Evidence.AddRange(section.Claims[1].Evidence), Limitations = [] }] }] }; proposal = proposal with { Units = proposal.Units.SetItem(0, Rebuild(root)) }; break;
        }
        Assert.False(OctoberKnowledgePolicy.Validate(proposal, new(fixture.Origin, 100)));
    }
    private static SemanticUnitProposal Rebuild(SemanticUnitProposal unit) => OctoberKnowledgeAcceptanceFixture.Unit(unit.SemanticSelector, unit.ParentSelector, unit.Title, unit.Anchors, unit.Sections);

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Validate_IndependentValidSchemaAndSharedNestedAnchors_AcceptsAsync()
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        Assert.True(OctoberKnowledgePolicy.Validate(fixture.Proposal, new(fixture.Origin, 100)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SaveChangesAsync_AcceptedVersionMutationOrDeletion_RejectsAndPreservesHistoryAsync()
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.Writer().AcceptCompletedFileAsync(fixture.Seal())).Status);
        await using (var context = fixture.Factory.CreateDbContext())
        {
            var version = await context.DocumentVersions.FirstAsync();
            version.ContentJson = "{}";
            await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync());
        }
        await using (var context = fixture.Factory.CreateDbContext())
        {
            context.DocumentVersions.Remove(await context.DocumentVersions.FirstAsync());
            await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync());
        }
        await using (var context = fixture.Factory.CreateDbContext())
        {
            var version = await context.DocumentVersions.FirstAsync();
            context.DocumentSections.Add(new() { BatchId = version.BatchId, VersionId = version.VersionId, SectionId = "postcommit-section", Ordinal = 99, Title = "Unaccepted section" });
            await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync());
        }
        await using var retained = fixture.Factory.CreateDbContext();
        Assert.Equal(2, await retained.DocumentVersions.CountAsync());
        Assert.Equal(2, await retained.DocumentSections.CountAsync());
        Assert.DoesNotContain(await retained.DocumentVersions.ToListAsync(), version => version.ContentJson == "{}");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Model_AcceptedRelationships_AreBatchQualifiedAndRestrictHistoryDeletionAsync()
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        await using var context = fixture.Factory.CreateDbContext();
        var owned = context.Model.GetEntityTypes().Where(entity => entity.ClrType.Namespace == typeof(AcceptedFileManifest).Namespace).ToArray();
        Assert.NotEmpty(owned);
        foreach (var foreignKey in owned.SelectMany(entity => entity.GetForeignKeys()))
        {
            Assert.Contains(foreignKey.Properties, property => property.Name == "BatchId");
            Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        }
        Assert.Contains(context.Model.FindEntityType(typeof(AcceptedFileManifest))!.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(new[] { "BatchId", "AdmissionId" }));
        Assert.Contains(context.Model.FindEntityType(typeof(CodeUnit))!.GetIndexes(), index => index.IsUnique && index.Properties.Any(property => property.Name == "SemanticKey"));
    }
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("1e-100")]
    [InlineData("123456789012345678901234567890123456789")]
    [InlineData("0.123456789012345678901234567890123456789")]
    public void CanonicalizeJson_TinyOrHighPrecisionNumber_PreservesExactValueOrRejectsUnsupportedPrecision(string number)
    {
        string canonical;
        try { canonical = OctoberKnowledgePolicy.CanonicalizeJson("{\"number\":" + number + "}"); }
        catch (JsonException) { return; }
        using var parsed = JsonDocument.Parse(canonical);
        var expected = ExactParts(number);
        var actual = ExactParts(parsed.RootElement.GetProperty("number").GetRawText());
        int commonExponent = Math.Min(expected.Exponent, actual.Exponent);
        Assert.Equal(expected.Coefficient * BigInteger.Pow(10, expected.Exponent - commonExponent), actual.Coefficient * BigInteger.Pow(10, actual.Exponent - commonExponent));
    }
    private static (BigInteger Coefficient, int Exponent) ExactParts(string number)
    {
        var parts = Regex.Match(number, @"^(?<sign>-?)(?<whole>\d+)(?:\.(?<fraction>\d+))?(?:[eE](?<exponent>[+-]?\d+))?$");
        Assert.True(parts.Success);
        string fraction = parts.Groups["fraction"].Value;
        var coefficient = BigInteger.Parse(parts.Groups["sign"].Value + parts.Groups["whole"].Value + fraction, CultureInfo.InvariantCulture);
        int exponent = parts.Groups["exponent"].Success ? int.Parse(parts.Groups["exponent"].Value, CultureInfo.InvariantCulture) : 0;
        return (coefficient, exponent - fraction.Length);
    }
}
