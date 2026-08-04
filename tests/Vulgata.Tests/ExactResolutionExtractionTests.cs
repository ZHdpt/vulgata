using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CrossRepository;

namespace Vulgata.Tests;

public sealed class ExactResolutionExtractionTests
{
    public static TheoryData<string, string?> InexactConsumerStatements => new()
    {
        { "// netTools.execute(ctx, \"cust/updateSession\");", null },
        { "/* netTools.execute(ctx, \"cust/updateSession\"); */", null },
        { "netTools.execute(ctx, transCode);", "variable_target" },
        { "netTools.execute(ctx, resolveEndpoint());", "variable_target" },
        { "netTools.execute(ctx, \"cust/\" + \"updateSession\");", "variable_target" },
        { "netTools.execute(ctx, String.format(\"cust/%s\", session));", "variable_target" },
        { "netTools.execute(ctx, \"\"\"cust/updateSession\"\"\");", null },
        { "otherTools.execute(ctx, \"cust/updateSession\");", null },
        { "netTools.call(ctx, \"cust/updateSession\");", null },
        { "netTools.execute(\"cust/updateSession\", ctx);", "variable_target" },
        { "netTools.execute(ctx, \"cust\\q/updateSession\");", null },
    };

    public static TheoryData<string> NonProviderDeclarations => new()
    {
        { "<?xml version=\"1.0\"?>\n<trans/>" },
        { "<?xml version=\"1.0\"?>\n<trans name=\" \"/>" },
        { "<?xml version=\"1.0\"?>\n<trans endpoint=\"in/cust/updateSession\"/>" },
        { "<?xml version=\"1.0\"?>\n<trans name=\"in/cust/updateSession\" extra=\"value\"/>" },
        { "<?xml version=\"1.0\"?>\n<other name=\"in/cust/updateSession\"/>" },
        { "<?xml version=\"1.0\"?>\n<trans xmlns=\"urn:not-configured\" name=\"in/cust/updateSession\"/>" },
        { "<?xml version=\"1.0\"?>\n<wrapper><trans name=\"in/cust/updateSession\"/></wrapper>" },
    };

    public static TheoryData<string> MalformedProviderDeclarations => new()
    {
        { "<?xml version=\"1.0\"?>\n<trans name=\"in/cust/updateSession\"" },
        { "<?xml version=\"1.0\"?>\n<trans name=\"first\" name=\"second\"/>" },
        { "<!DOCTYPE trans [<!ENTITY endpoint \"in/cust/updateSession\">]>\n<trans name=\"&endpoint;\"/>" },
        { "<?xml version=\"1.0\"?>\n<trans name=\"&missing;\"/>" },
    };

    [Fact]
    public async Task ExtractConsumerAsync_ReviewedLine45DirectLiteral_MapsCurrentOwnerAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ExactCrossRepositoryManagedFixture managed = fixture.CreateManagedRepositories();
        ExactPass1DocumentFixture consumer =
            fixture.CreatePass1Document(ExactPass1DocumentKind.Consumer);
        await using Vulgata.Infrastructure.Data.VulgataDbContext context =
            fixture.Database.CreateContext();
        context.Add(managed.System);
        context.Add(consumer.ScanRun);
        await context.SaveChangesAsync();
        LogicalDocument logicalDocument = LogicalDocument.Create(
            consumer.LogicalDocument.Id,
            consumer.DocumentVersion.RepositoryId,
            DocumentType.CodeLogic,
            "exact-pass1/consumer",
            ExactCrossRepositoryTestValues.CreatedAt);
        context.Add(logicalDocument);
        await context.SaveChangesAsync();
        context.Add(consumer.DocumentVersion);
        await context.SaveChangesAsync();
        logicalDocument.SetInitialCurrentVersion(consumer.DocumentVersion);
        await context.SaveChangesAsync();

        ResolutionOccurrence occurrence = Assert.Single(
            new ExactResolutionProfileCatalog().Load().Occurrences);
        await using var snapshot = new TestSnapshotLease(
            consumer.ScanRun.RepositoryId,
            consumer.ScanRun.Id,
            consumer.DocumentVersion.ProfiledRevision,
            fixture.ConsumerRepository.RootPath);

        var extractor = new ExactCandidateExtractors(context);
        ConsumerDispatchCandidate candidate = Assert.Single(
            await extractor.ExtractConsumersAsync(snapshot, occurrence));

        Assert.Equal("cust/updateSession", candidate.RawIdentifier);
        Assert.Equal("netTools", candidate.ReceiverSymbol);
        Assert.Equal("execute", candidate.MemberName);
        Assert.Equal(45, candidate.Line);
        Assert.Equal(1, candidate.ArgumentIndex);
        Assert.Equal(0, candidate.Ordinal);
        Assert.Equal(consumer.DocumentVersion.Id, candidate.Owner.DocumentVersionId);
        Assert.Equal(consumer.SourceAnchor.Id, candidate.Owner.SourceAnchorId);
        Assert.Equal(
            "src/main/java/cn/com/yitong/actions/mobile/UpdateChannelSessionOp.java#line=45;ordinal=0;rule=exact-java-direct-literal-v1",
            candidate.InvocationEvidenceLocator);
        Assert.DoesNotContain("return netTools", candidate.InvocationEvidenceLocator, StringComparison.Ordinal);
        Assert.True(
            candidate.InvocationEvidenceLocator.Length <=
            ExactResolutionLimits.MaxEvidenceLocatorLength);
    }

    [Fact]
    public async Task ExtractProvidersAsync_ReviewedLine2RootTransName_MapsExactImplementationOwnerAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ExactCrossRepositoryManagedFixture managed = fixture.CreateManagedRepositories();
        ExactPass1DocumentFixture provider =
            fixture.CreatePass1Document(ExactPass1DocumentKind.Provider);
        await using Vulgata.Infrastructure.Data.VulgataDbContext context =
            fixture.Database.CreateContext();
        context.Add(managed.System);
        context.Add(provider.ScanRun);
        await context.SaveChangesAsync();
        LogicalDocument logicalDocument = LogicalDocument.Create(
            provider.LogicalDocument.Id,
            provider.DocumentVersion.RepositoryId,
            DocumentType.CodeLogic,
            "exact-pass1/provider",
            ExactCrossRepositoryTestValues.CreatedAt);
        context.Add(logicalDocument);
        await context.SaveChangesAsync();
        context.Add(provider.DocumentVersion);
        await context.SaveChangesAsync();
        logicalDocument.SetInitialCurrentVersion(provider.DocumentVersion);
        await context.SaveChangesAsync();

        ResolutionOccurrence occurrence = Assert.Single(
            new ExactResolutionProfileCatalog().Load().Occurrences);
        Assert.Equal(
            "src/main/java/cn/com/yitong/actions/domain/login/UpdateSessionOp.java",
            occurrence.Provider.ImplementationDocument.RepositoryRelativePath);
        Assert.Equal(
            "cn.com.yitong.actions.domain.login.UpdateSessionOp",
            occurrence.Provider.ImplementationDocument.Symbol);
        fixture.ProviderRepository.Write(
            "src/main/resources/disgram/in/cust/not-listed.xml",
            "<!DOCTYPE trans [<!ENTITY forbidden 'must-not-be-read'>]><trans name=\"&forbidden;\"/>");
        await using var snapshot = new TestSnapshotLease(
            provider.ScanRun.RepositoryId,
            provider.ScanRun.Id,
            provider.DocumentVersion.ProfiledRevision,
            fixture.ProviderRepository.RootPath);

        var extractor = new ExactCandidateExtractors(context);
        ProviderEndpointCandidate candidate = Assert.Single(
            await extractor.ExtractProvidersAsync(snapshot, occurrence));

        Assert.Equal("in/cust/updateSession", candidate.RawIdentifier);
        Assert.Equal(provider.DocumentVersion.Id, candidate.Owner.DocumentVersionId);
        Assert.Equal(provider.SourceAnchor.Id, candidate.Owner.SourceAnchorId);
        Assert.Equal(
            "src/main/resources/disgram/in/cust/updateSession.xml#line=2;ordinal=0;rule=exact-xml-root-attribute-v1",
            candidate.DeclarationEvidenceLocator);
        Assert.NotEqual(provider.SourceAnchor.EvidenceLocator, candidate.DeclarationEvidenceLocator);
        Assert.DoesNotContain("<trans", candidate.DeclarationEvidenceLocator, StringComparison.Ordinal);
        Assert.True(
            candidate.DeclarationEvidenceLocator.Length <=
            ExactResolutionLimits.MaxEvidenceLocatorLength);
    }

    [Fact]
    public async Task ExtractProvidersAsync_DtdExternalResolution_IsRejectedAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ResolutionOccurrence occurrence = Assert.Single(
            new ExactResolutionProfileCatalog().Load().Occurrences);
        fixture.ProviderRepository.Write(
            Assert.Single(occurrence.Provider.Files),
            "<!DOCTYPE trans SYSTEM \"file:///must-not-resolve.dtd\"><trans name=\"in/cust/updateSession\"/>");
        await using Vulgata.Infrastructure.Data.VulgataDbContext context =
            fixture.Database.CreateContext();
        await using var snapshot = new TestSnapshotLease(
            ExactCrossRepositoryTestValues.ProviderRepositoryId,
            ExactCrossRepositoryTestValues.ProviderScanRunId,
            fixture.ProviderRepository.Commit,
            fixture.ProviderRepository.RootPath);

        ExactResolutionNonExactException exception =
            await Assert.ThrowsAsync<ExactResolutionNonExactException>(
            () => new ExactCandidateExtractors(context).ExtractProvidersAsync(
                snapshot,
                occurrence));

        Assert.Equal(occurrence.OccurrenceId, exception.OccurrenceId);
        Assert.Equal("malformed_provider", exception.ReasonCode);
        Assert.IsType<System.Xml.XmlException>(exception.InnerException);
    }

    [Fact]
    public async Task ExtractConsumersAsync_FirstConfiguredInvocationIsVariable_ClassifiesVariableTargetAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ExactCrossRepositoryManagedFixture managed = fixture.CreateManagedRepositories();
        ExactPass1DocumentFixture consumer =
            fixture.CreatePass1Document(ExactPass1DocumentKind.Consumer);
        await using Vulgata.Infrastructure.Data.VulgataDbContext context =
            fixture.Database.CreateContext();
        context.Add(managed.System);
        context.Add(consumer.ScanRun);
        await context.SaveChangesAsync();
        LogicalDocument logicalDocument = LogicalDocument.Create(
            consumer.LogicalDocument.Id,
            consumer.DocumentVersion.RepositoryId,
            DocumentType.CodeLogic,
            "exact-pass1/consumer-ordinal",
            ExactCrossRepositoryTestValues.CreatedAt);
        context.Add(logicalDocument);
        await context.SaveChangesAsync();
        context.Add(consumer.DocumentVersion);
        await context.SaveChangesAsync();
        logicalDocument.SetInitialCurrentVersion(consumer.DocumentVersion);
        await context.SaveChangesAsync();

        ResolutionOccurrence occurrence = Assert.Single(
            new ExactResolutionProfileCatalog().Load().Occurrences);
        fixture.ConsumerRepository.Write(
            Assert.Single(occurrence.Consumer.Files),
            new string('\n', 44) +
            "netTools.execute(ctx, transCode); netTools.execute(ctx, \"cust/updateSession\");\n");
        await using var snapshot = new TestSnapshotLease(
            consumer.ScanRun.RepositoryId,
            consumer.ScanRun.Id,
            consumer.DocumentVersion.ProfiledRevision,
            fixture.ConsumerRepository.RootPath);

        ExactResolutionNonExactException exception =
            await Assert.ThrowsAsync<ExactResolutionNonExactException>(() =>
                new ExactCandidateExtractors(context).ExtractConsumersAsync(
                snapshot,
                occurrence));

        Assert.Equal(occurrence.OccurrenceId, exception.OccurrenceId);
        Assert.Equal("variable_target", exception.ReasonCode);
    }

    [Fact]
    public async Task ExtractConsumersAsync_QualifiedReceiverMasqueradesAsSimpleMemberInvocation_ClassifiesDisallowedInvocationAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ExactCrossRepositoryManagedFixture managed = fixture.CreateManagedRepositories();
        ExactPass1DocumentFixture consumer =
            fixture.CreatePass1Document(ExactPass1DocumentKind.Consumer);
        await using Vulgata.Infrastructure.Data.VulgataDbContext context =
            fixture.Database.CreateContext();
        await SeedCurrentDocumentAsync(context, managed, consumer);

        ResolutionOccurrence occurrence = Assert.Single(
            new ExactResolutionProfileCatalog().Load().Occurrences);
        fixture.ConsumerRepository.Write(
            Assert.Single(occurrence.Consumer.Files),
            SourceAtLine(
                45,
                "this.netTools.execute(ctx, \"cust/updateSession\");"));
        await using var snapshot = new TestSnapshotLease(
            consumer.ScanRun.RepositoryId,
            consumer.ScanRun.Id,
            consumer.DocumentVersion.ProfiledRevision,
            fixture.ConsumerRepository.RootPath);

        ExactResolutionNonExactException exception =
            await Assert.ThrowsAsync<ExactResolutionNonExactException>(() =>
                new ExactCandidateExtractors(context).ExtractConsumersAsync(
                snapshot,
                occurrence));

        Assert.Equal(occurrence.OccurrenceId, exception.OccurrenceId);
        Assert.Equal("disallowed_invocation", exception.ReasonCode);
    }

    [Theory]
    [MemberData(nameof(InexactConsumerStatements))]
    public async Task ExtractConsumersAsync_InexactCallForms_ClassifyOnlyConfiguredNonliteralAsync(
        string statement,
        string? expectedReason)
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ExactCrossRepositoryManagedFixture managed = fixture.CreateManagedRepositories();
        ExactPass1DocumentFixture consumer =
            fixture.CreatePass1Document(ExactPass1DocumentKind.Consumer);
        await using Vulgata.Infrastructure.Data.VulgataDbContext context =
            fixture.Database.CreateContext();
        await SeedCurrentDocumentAsync(context, managed, consumer);
        ResolutionOccurrence occurrence = Assert.Single(
            new ExactResolutionProfileCatalog().Load().Occurrences);
        fixture.ConsumerRepository.Write(
            Assert.Single(occurrence.Consumer.Files),
            SourceAtLine(45, statement));
        await using var snapshot = Snapshot(consumer, fixture.ConsumerRepository.RootPath);

        if (expectedReason is null)
        {
            Assert.Empty(await new ExactCandidateExtractors(context).ExtractConsumersAsync(
                snapshot,
                occurrence));
        }
        else
        {
            ExactResolutionNonExactException exception =
                await Assert.ThrowsAsync<ExactResolutionNonExactException>(() =>
                    new ExactCandidateExtractors(context).ExtractConsumersAsync(
                        snapshot,
                        occurrence));
            Assert.Equal(expectedReason, exception.ReasonCode);
            Assert.Equal(ExactResolutionNonExactSource.Consumer, exception.EvidenceSource);
        }
    }

    [Theory]
    [MemberData(nameof(NonProviderDeclarations))]
    public async Task ExtractProvidersAsync_MissingWrongOrAmbiguousDeclaration_ClassifiesMappedMalformedProviderAsync(
        string xml)
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ExactCrossRepositoryManagedFixture managed = fixture.CreateManagedRepositories();
        ExactPass1DocumentFixture provider =
            fixture.CreatePass1Document(ExactPass1DocumentKind.Provider);
        await using Vulgata.Infrastructure.Data.VulgataDbContext context =
            fixture.Database.CreateContext();
        await SeedCurrentDocumentAsync(context, managed, provider);
        ResolutionOccurrence occurrence = Assert.Single(
            new ExactResolutionProfileCatalog().Load().Occurrences);
        fixture.ProviderRepository.Write(Assert.Single(occurrence.Provider.Files), xml);
        await using var snapshot = Snapshot(provider, fixture.ProviderRepository.RootPath);

        ExactResolutionNonExactException exception =
            await Assert.ThrowsAsync<ExactResolutionNonExactException>(() =>
                new ExactCandidateExtractors(context).ExtractProvidersAsync(
                snapshot,
                occurrence));

        Assert.Equal(occurrence.OccurrenceId, exception.OccurrenceId);
        Assert.Equal("malformed_provider", exception.ReasonCode);
        Assert.Equal(ExactResolutionNonExactSource.Provider, exception.EvidenceSource);
        Assert.Equal(provider.DocumentVersion.Id, exception.Owner?.DocumentVersionId);
        Assert.Equal(provider.SourceAnchor.Id, exception.Owner?.SourceAnchorId);
        Assert.StartsWith(
            Assert.Single(occurrence.Provider.Files),
            exception.EvidenceLocator,
            StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(MalformedProviderDeclarations))]
    public async Task ExtractProvidersAsync_MalformedXmlOrEntity_RejectsWithoutCandidateAsync(
        string xml)
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ResolutionOccurrence occurrence = Assert.Single(
            new ExactResolutionProfileCatalog().Load().Occurrences);
        fixture.ProviderRepository.Write(Assert.Single(occurrence.Provider.Files), xml);
        await using Vulgata.Infrastructure.Data.VulgataDbContext context =
            fixture.Database.CreateContext();
        await using var snapshot = new TestSnapshotLease(
            ExactCrossRepositoryTestValues.ProviderRepositoryId,
            ExactCrossRepositoryTestValues.ProviderScanRunId,
            fixture.ProviderRepository.Commit,
            fixture.ProviderRepository.RootPath);

        ExactResolutionNonExactException exception =
            await Assert.ThrowsAsync<ExactResolutionNonExactException>(
            () => new ExactCandidateExtractors(context).ExtractProvidersAsync(
                snapshot,
                occurrence));

        Assert.Equal(occurrence.OccurrenceId, exception.OccurrenceId);
        Assert.Equal("malformed_provider", exception.ReasonCode);
        Assert.IsType<System.Xml.XmlException>(exception.InnerException);
    }

    [Theory]
    [InlineData("consumer")]
    [InlineData("provider")]
    public async Task ExtractAsync_ZeroCurrentOwnerMapping_ProducesNoCandidateAsync(string side)
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ResolutionOccurrence occurrence = Assert.Single(
            new ExactResolutionProfileCatalog().Load().Occurrences);
        await using Vulgata.Infrastructure.Data.VulgataDbContext context =
            fixture.Database.CreateContext();
        if (side == "consumer")
        {
            await using var snapshot = new TestSnapshotLease(
                ExactCrossRepositoryTestValues.ConsumerRepositoryId,
                ExactCrossRepositoryTestValues.ConsumerScanRunId,
                fixture.ConsumerRepository.Commit,
                fixture.ConsumerRepository.RootPath);
            Assert.Empty(await new ExactCandidateExtractors(context).ExtractConsumersAsync(
                snapshot,
                occurrence));
        }
        else
        {
            await using var snapshot = new TestSnapshotLease(
                ExactCrossRepositoryTestValues.ProviderRepositoryId,
                ExactCrossRepositoryTestValues.ProviderScanRunId,
                fixture.ProviderRepository.Commit,
                fixture.ProviderRepository.RootPath);
            Assert.Empty(await new ExactCandidateExtractors(context).ExtractProvidersAsync(
                snapshot,
                occurrence));
        }
    }

    [Fact]
    public async Task ExtractConsumersAsync_MultipleContainingSourceAnchors_ProducesNoCandidateAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ExactCrossRepositoryManagedFixture managed = fixture.CreateManagedRepositories();
        ExactPass1DocumentFixture consumer =
            fixture.CreatePass1Document(ExactPass1DocumentKind.Consumer);
        consumer.DocumentVersion.SourceAnchors.Add(new SourceAnchor(
            ExactCrossRepositoryTestValues.Id(711),
            consumer.DocumentVersion.Id,
            consumer.DocumentVersion.RepositoryId,
            consumer.DocumentVersion.ScanRunId,
            consumer.CodeUnit.Id,
            consumer.DocumentVersion.ProfiledRevision,
            consumer.SourceAnchor.RepositoryRelativePath,
            "duplicate-consumer-anchor#line=45",
            consumer.SourceAnchor.LineStart,
            consumer.SourceAnchor.LineEnd,
            ExactCrossRepositoryTestValues.SourceHash,
            ExactCrossRepositoryTestValues.Pass1CompletedAt));
        await using Vulgata.Infrastructure.Data.VulgataDbContext context =
            fixture.Database.CreateContext();
        await SeedCurrentDocumentAsync(context, managed, consumer);
        ResolutionOccurrence occurrence = Assert.Single(
            new ExactResolutionProfileCatalog().Load().Occurrences);
        await using var snapshot = Snapshot(consumer, fixture.ConsumerRepository.RootPath);

        Assert.Empty(await new ExactCandidateExtractors(context).ExtractConsumersAsync(
            snapshot,
            occurrence));
    }

    [Fact]
    public async Task ExtractProvidersAsync_StaleImplementationDocument_ProducesNoCandidateAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ExactCrossRepositoryManagedFixture managed = fixture.CreateManagedRepositories();
        ExactPass1DocumentFixture provider =
            fixture.CreatePass1Document(ExactPass1DocumentKind.Provider);
        await using Vulgata.Infrastructure.Data.VulgataDbContext context =
            fixture.Database.CreateContext();
        await SeedCurrentDocumentAsync(context, managed, provider, makeCurrent: false);
        ResolutionOccurrence occurrence = Assert.Single(
            new ExactResolutionProfileCatalog().Load().Occurrences);
        await using var snapshot = Snapshot(provider, fixture.ProviderRepository.RootPath);

        Assert.Empty(await new ExactCandidateExtractors(context).ExtractProvidersAsync(
            snapshot,
            occurrence));
    }

    [Fact]
    public async Task ExtractProvidersAsync_ReparseTraversal_IsRejectedAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ResolutionOccurrence reviewed = Assert.Single(
            new ExactResolutionProfileCatalog().Load().Occurrences);
        string linkedDirectory = Path.Combine(
            fixture.ProviderRepository.RootPath,
            "src",
            "main",
            "resources",
            "disgram",
            "in",
            "linked");
        string targetDirectory = Path.Combine(
            fixture.ProviderRepository.RootPath,
            "src",
            "main",
            "resources",
            "disgram",
            "in",
            "cust");
        Directory.CreateSymbolicLink(linkedDirectory, targetDirectory);
        ResolutionOccurrence occurrence = WithProviderFiles(
            reviewed,
            ["src/main/resources/disgram/in/linked/updateSession.xml"]);
        await using Vulgata.Infrastructure.Data.VulgataDbContext context =
            fixture.Database.CreateContext();
        await using var snapshot = new TestSnapshotLease(
            ExactCrossRepositoryTestValues.ProviderRepositoryId,
            ExactCrossRepositoryTestValues.ProviderScanRunId,
            fixture.ProviderRepository.Commit,
            fixture.ProviderRepository.RootPath);

        ExactResolutionNonExactException exception =
            await Assert.ThrowsAsync<ExactResolutionNonExactException>(() =>
                new ExactCandidateExtractors(context).ExtractProvidersAsync(
                snapshot,
                occurrence));

        Assert.Equal(occurrence.OccurrenceId, exception.OccurrenceId);
        Assert.Equal("provider_out_of_root", exception.ReasonCode);
    }

    [Fact]
    public async Task ExtractProvidersAsync_OverBoundListedFile_IsRejectedAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ResolutionOccurrence occurrence = Assert.Single(
            new ExactResolutionProfileCatalog().Load().Occurrences);
        fixture.ProviderRepository.Write(
            Assert.Single(occurrence.Provider.Files),
            new string(' ', ExactCandidateExtractors.MaximumFileBytes + 1));
        await using Vulgata.Infrastructure.Data.VulgataDbContext context =
            fixture.Database.CreateContext();
        await using var snapshot = new TestSnapshotLease(
            ExactCrossRepositoryTestValues.ProviderRepositoryId,
            ExactCrossRepositoryTestValues.ProviderScanRunId,
            fixture.ProviderRepository.Commit,
            fixture.ProviderRepository.RootPath);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new ExactCandidateExtractors(context).ExtractProvidersAsync(
                snapshot,
                occurrence));
    }

    [Theory]
    [InlineData("../updateSession.xml")]
    [InlineData("src/main/resources/disgram/in/../../outside.xml")]
    [InlineData("C:/outside.xml")]
    [InlineData("/outside.xml")]
    public void ProviderSelector_TraversalOrOutOfRootFile_IsRejected(string file)
    {
        ResolutionOccurrence occurrence = Assert.Single(
            new ExactResolutionProfileCatalog().Load().Occurrences);

        Assert.ThrowsAny<ArgumentException>(() => new ExactProviderSelector(
            occurrence.Provider.Root,
            [file],
            occurrence.Provider.Declaration,
            occurrence.Provider.ImplementationDocument));
    }

    [Fact]
    public async Task ExtractConsumersAsync_OverBoundListedFile_IsRejectedAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        ResolutionOccurrence occurrence = Assert.Single(
            new ExactResolutionProfileCatalog().Load().Occurrences);
        fixture.ConsumerRepository.Write(
            Assert.Single(occurrence.Consumer.Files),
            new string(' ', ExactCandidateExtractors.MaximumFileBytes + 1));
        await using Vulgata.Infrastructure.Data.VulgataDbContext context =
            fixture.Database.CreateContext();
        await using var snapshot = new TestSnapshotLease(
            ExactCrossRepositoryTestValues.ConsumerRepositoryId,
            ExactCrossRepositoryTestValues.ConsumerScanRunId,
            fixture.ConsumerRepository.Commit,
            fixture.ConsumerRepository.RootPath);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => new ExactCandidateExtractors(context).ExtractConsumersAsync(
                snapshot,
                occurrence));
        Assert.InRange(
            ExactCandidateExtractors.MaximumCandidates,
            1,
            2_048);
        Assert.InRange(
            ExactResolutionLimits.MaxFilesPerSelector,
            1,
            16);
    }

    private static async Task SeedCurrentDocumentAsync(
        Vulgata.Infrastructure.Data.VulgataDbContext context,
        ExactCrossRepositoryManagedFixture managed,
        ExactPass1DocumentFixture document,
        bool makeCurrent = true)
    {
        context.Add(managed.System);
        context.Add(document.ScanRun);
        await context.SaveChangesAsync();
        LogicalDocument logicalDocument = LogicalDocument.Create(
            document.LogicalDocument.Id,
            document.DocumentVersion.RepositoryId,
            DocumentType.CodeLogic,
            $"exact-pass1/{document.DocumentVersion.Id:N}",
            ExactCrossRepositoryTestValues.CreatedAt);
        context.Add(logicalDocument);
        await context.SaveChangesAsync();
        context.Add(document.DocumentVersion);
        await context.SaveChangesAsync();
        if (makeCurrent)
        {
            logicalDocument.SetInitialCurrentVersion(document.DocumentVersion);
            await context.SaveChangesAsync();
        }
    }

    private static string SourceAtLine(int line, string statement) =>
        new string('\n', line - 1) + statement + '\n';

    private static TestSnapshotLease Snapshot(
        ExactPass1DocumentFixture document,
        string rootPath) =>
        new(
            document.ScanRun.RepositoryId,
            document.ScanRun.Id,
            document.DocumentVersion.ProfiledRevision,
            rootPath);

    private static ResolutionOccurrence WithProviderFiles(
        ResolutionOccurrence occurrence,
        IEnumerable<string> files) =>
        new(
            occurrence.OccurrenceId,
            occurrence.Protocol,
            occurrence.ConsumerAlias,
            occurrence.ProviderAlias,
            occurrence.Consumer,
            new ExactProviderSelector(
                occurrence.Provider.Root,
                files,
                occurrence.Provider.Declaration,
                occurrence.Provider.ImplementationDocument),
            occurrence.Normalization,
            occurrence.Comparison);

    private sealed class TestSnapshotLease(
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision,
        string rootPath) : ICommittedRepositorySnapshotLease
    {
        public Guid RepositoryId { get; } = repositoryId;
        public Guid ScanRunId { get; } = scanRunId;
        public string ProfiledRevision { get; } = profiledRevision;
        public string RootPath { get; } = rootPath;

        public Task VerifyIdentityAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
