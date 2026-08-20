using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.CommunicationDetection;

public sealed class EmbeddedPreparedCommunicationCatalogSource :
    IPreparedCommunicationCatalogSource
{
    public Task<PreparedCatalogLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            CommunicationPatternCatalog catalog = CommunicationCatalogLoader.LoadEmbedded();
            return Task.FromResult(new PreparedCatalogLoadResult(
                new Vulgata.Core.DomainServices.CommunicationPatternCatalog(
                    catalog.SchemaIdentity,
                    catalog.CatalogId,
                    catalog.Version,
                    catalog.CatalogSha256,
                    [.. catalog.Rules.Select(Map)]),
                null));
        }
        catch (InvalidDataException)
        {
            return Task.FromResult(new PreparedCatalogLoadResult(
                null,
                new PreparedScanFailure(
                    PreparedScanFailureReason.UnsafeInput,
                    "prepared-catalog-invalid",
                    "The embedded communication catalog is unavailable or invalid.")));
        }
    }

    private static CommunicationCatalogRule Map(CommunicationPatternRule rule) =>
        new(
            rule.Identity,
            rule.PathGlob,
            [.. rule.Selector.Select(step => new CommunicationCatalogElementStep(
                step.NamespaceUri,
                step.LocalName))],
            rule.Ancestor is null
                ? null
                : [.. rule.Ancestor.Select(step => new CommunicationCatalogElementStep(
                    step.NamespaceUri,
                    step.LocalName))],
            [.. rule.AttributeTests.Select(test => new CommunicationCatalogAttributeTest(
                test.NamespaceUri,
                test.LocalName,
                test.EqualsValue))],
            new CommunicationCatalogExtraction(
                rule.Extraction.Kind == CommunicationPatternExtractionKind.Attribute
                    ? CommunicationCatalogExtractionKind.Attribute
                    : CommunicationCatalogExtractionKind.ElementText,
                rule.Extraction.NamespaceUri,
                rule.Extraction.LocalName),
            rule.RemovePrefix,
            rule.CommunicationKind,
            rule.Rule,
            rule.Provenance);
}
