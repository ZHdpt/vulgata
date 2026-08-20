using System.Collections.ObjectModel;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.CommunicationDetection;

public sealed record CommunicationPatternElementStep(string NamespaceUri, string LocalName);

public sealed record CommunicationPatternAttributeTest(
    string NamespaceUri,
    string LocalName,
    bool Present,
    string? EqualsValue);

public enum CommunicationPatternExtractionKind
{
    Attribute,
    ElementText,
}

public sealed record CommunicationPatternExtraction(
    CommunicationPatternExtractionKind Kind,
    string? NamespaceUri,
    string? LocalName);

public sealed class CommunicationPatternRule
{
    public CommunicationPatternRule(
        string identity,
        string pathGlob,
        IEnumerable<CommunicationPatternElementStep> selector,
        IEnumerable<CommunicationPatternElementStep>? ancestor,
        IEnumerable<CommunicationPatternAttributeTest> attributeTests,
        CommunicationPatternExtraction extraction,
        string? removePrefix,
        CommunicationKind communicationKind,
        EvidenceRule rule,
        EvidenceProvenance provenance)
    {
        Identity = identity;
        PathGlob = pathGlob;
        Selector = new ReadOnlyCollection<CommunicationPatternElementStep>([.. selector]);
        Ancestor = ancestor is null
            ? null
            : new ReadOnlyCollection<CommunicationPatternElementStep>([.. ancestor]);
        AttributeTests = new ReadOnlyCollection<CommunicationPatternAttributeTest>([.. attributeTests]);
        Extraction = extraction;
        RemovePrefix = removePrefix;
        CommunicationKind = communicationKind;
        Rule = rule;
        Provenance = provenance;
    }

    public string Identity { get; }
    public string PathGlob { get; }
    public IReadOnlyList<CommunicationPatternElementStep> Selector { get; }
    public IReadOnlyList<CommunicationPatternElementStep>? Ancestor { get; }
    public IReadOnlyList<CommunicationPatternAttributeTest> AttributeTests { get; }
    public CommunicationPatternExtraction Extraction { get; }
    public string? RemovePrefix { get; }
    public CommunicationKind CommunicationKind { get; }
    public EvidenceRule Rule { get; }
    public EvidenceProvenance Provenance { get; }
}

public sealed class CommunicationPatternCatalog
{
    public CommunicationPatternCatalog(
        string schemaIdentity,
        string catalogId,
        string version,
        string catalogSha256,
        IEnumerable<CommunicationPatternRule> rules)
    {
        SchemaIdentity = schemaIdentity;
        CatalogId = catalogId;
        Version = version;
        CatalogSha256 = catalogSha256;
        Rules = new ReadOnlyCollection<CommunicationPatternRule>([.. rules]);
    }

    public string SchemaIdentity { get; }
    public string CatalogId { get; }
    public string Version { get; }
    public string CatalogSha256 { get; }
    public IReadOnlyList<CommunicationPatternRule> Rules { get; }
}

public sealed record CommunicationCatalogMatch(
    CommunicationKind Kind,
    EvidenceRule Rule,
    EvidenceProvenance Provenance,
    XmlEvidenceLocator Locator,
    string Identifier,
    string CatalogRuleIdentity);
