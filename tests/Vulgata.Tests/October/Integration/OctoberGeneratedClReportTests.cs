using System.Text.Json.Nodes;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Integration;

public sealed class OctoberGeneratedClReportTests
{
    private static readonly Guid Batch = Guid.Parse("11223344-1111-2222-3333-123456789abc");
    private static readonly Guid Repository = Guid.Parse("22223344-1111-2222-3333-123456789abc");
    private static readonly Guid Publication = Guid.Parse("33223344-1111-2222-3333-123456789abc");
    private static readonly KnowledgeBinding Expected = new("owned-database", "owned-target", Batch, new string('a', 64));
    private static readonly PreparedOrigin Origin = new(Repository, "owned-root", "src/Order.cs", new string('c', 64), "prepared-v1");

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Parse_ActualEntryPointProjection_RoundTripsControlledReportWithoutStorageClaimAsync()
    {
        var handle = new DocumentHandle(Batch, Publication, Guid.NewGuid(), Guid.NewGuid());
        var coverage = new FileCoverage(Guid.NewGuid(), Origin, CoverageDisposition.Accepted, null);
        var report = new OctoberScanReport("cl-only", "cl-only", OctoberCoverageStatus.Complete,
            OctoberExecutionStatus.Completed, OctoberPublicationStatus.Confirmed, new(Expected, Publication), [handle],
            [new(Guid.NewGuid(), coverage.AdmissionId, new string('d', 64), [new(Batch, handle.LogicalDocumentId, handle.DocumentVersionId)], coverage)],
            [coverage], new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero), new(null, null, null, true), [])
        {
            Inventory = [new(Repository, Origin.RelativePath, PreparedSourceEntryKind.RegularFile, "selected", "not-console-authority", 0, null)]
        };
        using var output = new StringWriter(); using var error = new StringWriter();
        int exit = await OctoberGeneratorEntryPoint.ExecuteConfiguredAsync(
            new(OctoberGeneratorMode.Generate, Expected.TargetId, Batch, null, new() { OutputStage = "cl-only" }),
            new ControlledModule(report), output, error);
        Assert.Equal(0, exit);
        Assert.Empty(error.ToString());
        Assert.DoesNotContain(Expected.CanonicalDatabaseIdentity, output.ToString(), StringComparison.Ordinal);
        var result = OctoberGeneratedClReport.Parse(exit, output.ToString(), Expected, Origin);
        Assert.Equal(report.Publication, result.Publication);
        Assert.Equal(handle, Assert.Single(result.Documents));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Parse_CompleteOneFileReport_ReturnsExactHandlesWithOnlyTrustedDatabaseIdentity()
    {
        var result = Parse(Report());
        Assert.Equal(new PublicationBinding(Expected, Publication), result.Publication);
        var handle = Assert.Single(result.Documents);
        Assert.Equal(Batch, handle.BatchId);
        Assert.Equal(Publication, handle.PublicationId);
        Assert.Equal(Guid.Parse("44223344-1111-2222-3333-123456789abc"), handle.LogicalDocumentId);
        Assert.Equal(Guid.Parse("55223344-1111-2222-3333-123456789abc"), handle.DocumentVersionId);
        Assert.Null(handle.SectionId);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Parse_OneFileWithMultipleMeaningfulUnits_RetainsAllExactHandles()
    {
        var report = Report();
        var reference = report["acceptedFiles"]![0]!["documents"]![0]!.DeepClone();
        reference["logicalDocumentId"] = Guid.NewGuid();
        reference["documentVersionId"] = Guid.NewGuid();
        report["acceptedFiles"]![0]!["documents"]!.AsArray().Add(reference);
        var handle = reference.DeepClone(); handle["publicationId"] = Publication; handle["sectionId"] = null;
        report["documents"]!.AsArray().Add(handle);
        Assert.Equal(2, Parse(report).Documents.Length);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("exit")]
    [InlineData("requested")]
    [InlineData("completed")]
    [InlineData("coverage")]
    [InlineData("execution")]
    [InlineData("publication-status")]
    [InlineData("target")]
    [InlineData("batch")]
    [InlineData("selection")]
    [InlineData("empty-publication")]
    [InlineData("database-in-output")]
    [InlineData("document-publication")]
    [InlineData("document-batch")]
    [InlineData("document-section")]
    [InlineData("duplicate-document")]
    [InlineData("missing-document")]
    [InlineData("accepted-document")]
    [InlineData("duplicate-file")]
    [InlineData("manifest")]
    [InlineData("admission")]
    [InlineData("proposal")]
    [InlineData("repository")]
    [InlineData("root")]
    [InlineData("path")]
    [InlineData("source-digest")]
    [InlineData("foreign-valid-digest")]
    [InlineData("prepared-version")]
    [InlineData("foreign-prepared-version")]
    [InlineData("terminal")]
    [InlineData("reason")]
    [InlineData("disposition")]
    [InlineData("inventory")]
    [InlineData("gaps")]
    public void Parse_IncompleteForeignOrContradictoryReport_Refuses(string mutation)
    {
        var report = Report();
        var file = report["acceptedFiles"]![0]!;
        var origin = file["coverage"]!["origin"]!;
        switch (mutation)
        {
            case "exit": report["exitCode"] = 2; break;
            case "requested": report["requestedStage"] = "business"; break;
            case "completed": report["completedStage"] = null; break;
            case "coverage": report["coverage"] = "Partial"; break;
            case "execution": report["execution"] = "Indeterminate"; break;
            case "publication-status": report["publicationStatus"] = "NoContent"; break;
            case "target": report["publication"]!["targetId"] = "foreign"; break;
            case "batch": report["publication"]!["batchId"] = Guid.NewGuid(); break;
            case "selection": report["publication"]!["selectionDigest"] = new string('b', 64); break;
            case "empty-publication": report["publication"]!["publicationId"] = Guid.Empty; break;
            case "database-in-output": report["publication"]!["canonicalDatabaseIdentity"] = "untrusted"; break;
            case "document-publication": report["documents"]![0]!["publicationId"] = Guid.NewGuid(); break;
            case "document-batch": report["documents"]![0]!["batchId"] = Guid.NewGuid(); break;
            case "document-section": report["documents"]![0]!["sectionId"] = "section"; break;
            case "duplicate-document": report["documents"]!.AsArray().Add(report["documents"]![0]!.DeepClone()); break;
            case "missing-document": report["documents"] = new JsonArray(); break;
            case "accepted-document": file["documents"]![0]!["documentVersionId"] = Guid.NewGuid(); break;
            case "duplicate-file": report["acceptedFiles"]!.AsArray().Add(file.DeepClone()); break;
            case "manifest": file["manifestId"] = Guid.Empty; break;
            case "admission": file["admissionId"] = Guid.NewGuid(); break;
            case "proposal": file["proposalDigest"] = "invalid"; break;
            case "repository": origin["repositoryId"] = Guid.NewGuid(); break;
            case "root": origin["rootBindingIdentity"] = "foreign"; break;
            case "path": origin["relativePath"] = "../Order.cs"; break;
            case "source-digest": origin["sourceDigest"] = "invalid"; break;
            case "foreign-valid-digest": origin["sourceDigest"] = new string('e', 64); break;
            case "prepared-version": origin["preparedVersion"] = ""; break;
            case "foreign-prepared-version": origin["preparedVersion"] = "another-approval"; break;
            case "terminal": report["terminalCoverage"]![0]!["origin"]!["sourceDigest"] = new string('b', 64); break;
            case "reason": file["coverage"]!["reason"] = "failed"; break;
            case "disposition": file["coverage"]!["disposition"] = "Failed"; break;
            case "inventory": report["inventory"]![0]!["relativePath"] = "different.cs"; break;
            case "gaps": report["gaps"] = new JsonArray(new JsonObject { ["code"] = "missing" }); break;
        }
        Assert.Throws<ArgumentException>(() => Parse(report));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("malformed")]
    [InlineData("multiple")]
    [InlineData("duplicate-property")]
    [InlineData("unknown-line")]
    [InlineData("prepare-startup-line")]
    [InlineData("nonzero-process")]
    public void Parse_AmbiguousConsoleOrFailedProcess_Refuses(string invalid)
    {
        string json = Report().ToJsonString();
        string stdout = invalid switch
        {
            "malformed" => "{",
            "multiple" => json + "\n" + json,
            "duplicate-property" => json.Replace("\"exitCode\":0", "\"exitCode\":1,\"exitCode\":0", StringComparison.Ordinal),
            "unknown-line" => "unrecognized output\n" + json,
            "prepare-startup-line" => "October provider configuration Bound.\n" + json,
            _ => json
        };
        Assert.Throws<ArgumentException>(() => OctoberGeneratedClReport.Parse(invalid == "nonzero-process" ? 1 : 0,
            stdout, Expected, Origin));
    }

    private static OctoberGeneratedClReport Parse(JsonObject report) => OctoberGeneratedClReport.Parse(0,
        " \n" + report.ToJsonString() + "\n", Expected, Origin);

    private sealed class ControlledModule(OctoberScanReport report) : IOctoberGeneratorModule
    {
        public Task<OctoberGeneratorExecutionResult> ExecuteAsync(OctoberGeneratorCommand command, CancellationToken cancellationToken = default)
            => Task.FromResult(new OctoberGeneratorExecutionResult(OctoberGeneratorExecutionStatus.GenerateFinished, Report: report));
    }

    private static JsonObject Report()
    {
        string logical = "44223344-1111-2222-3333-123456789abc", version = "55223344-1111-2222-3333-123456789abc";
        string admission = "66223344-1111-2222-3333-123456789abc";
        var reference = new JsonObject { ["batchId"] = Batch, ["logicalDocumentId"] = logical, ["documentVersionId"] = version };
        var handle = reference.DeepClone().AsObject(); handle["publicationId"] = Publication; handle["sectionId"] = null;
        var coverage = new JsonObject { ["admissionId"] = admission, ["disposition"] = "Accepted", ["reason"] = null,
            ["origin"] = new JsonObject { ["repositoryId"] = Repository, ["rootBindingIdentity"] = "owned-root", ["relativePath"] = "src/Order.cs", ["sourceDigest"] = new string('c', 64), ["preparedVersion"] = "prepared-v1" } };
        return new JsonObject
        {
            ["requestedStage"] = "cl-only", ["completedStage"] = "cl-only", ["coverage"] = "Complete", ["execution"] = "Completed",
            ["publicationStatus"] = "Confirmed", ["matching"] = "not-requested", ["businessLogic"] = "not-requested", ["exitCode"] = 0,
            ["publication"] = new JsonObject { ["targetId"] = Expected.TargetId, ["batchId"] = Batch, ["selectionDigest"] = Expected.SelectionDigest, ["publicationId"] = Publication },
            ["documents"] = new JsonArray(handle),
            ["acceptedFiles"] = new JsonArray(new JsonObject { ["manifestId"] = "77223344-1111-2222-3333-123456789abc", ["admissionId"] = admission,
                ["proposalDigest"] = new string('d', 64), ["documents"] = new JsonArray(reference), ["coverage"] = coverage }),
            ["terminalCoverage"] = new JsonArray(coverage.DeepClone()),
            ["inventory"] = new JsonArray(new JsonObject { ["repositoryId"] = Repository, ["relativePath"] = "src/Order.cs", ["kind"] = "RegularFile", ["disposition"] = "selected", ["reason"] = "configured-selected-source", ["priority"] = 0, ["observedByteLength"] = 20 }),
            ["gaps"] = new JsonArray(),
            ["timings"] = new JsonObject { ["elapsed"] = "00:00:01", ["queueWait"] = "00:00:00", ["toolWait"] = "00:00:00", ["modelWait"] = "00:00:01", ["storeWait"] = "00:00:00" },
            ["usage"] = new JsonObject { ["requests"] = 1, ["inputTokens"] = 10, ["outputTokens"] = 10, ["hasUnknownUsage"] = false }
        };
    }
}
