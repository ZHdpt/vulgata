using System.Text.Json.Serialization;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Agents.WorkerExecution;

internal sealed record WorkerAgentPass1RequestDto(
    Guid JobId,
    Guid SystemId,
    Guid RepositoryId,
    Guid ScanRunId,
    Guid CodeUnitId,
    string ProfiledRevision,
    int SchemaVersion,
    string Kind,
    WorkerAgentPass1SourceDto Source,
    IReadOnlyList<WorkerAgentStructuralContextDto> NeighboringStructuralContext,
    IReadOnlyList<WorkerAgentCommunicationEvidenceDto> ApprovedCommunicationEvidence,
    IReadOnlyList<Guid> AllowedReferenceDocumentVersionIds)
{
    internal static WorkerAgentPass1RequestDto From(WorkerAgentExecutionContext context)
    {
        WorkerAgentJobPacket packet = context.Packet;
        var pass1 = (Pass1CodeLogicJobPayload)packet.Payload;
        return new(
            packet.JobId,
            packet.SystemId,
            packet.RepositoryId,
            packet.ScanRunId,
            packet.CodeUnitId,
            packet.ProfiledRevision,
            packet.SchemaVersion,
            nameof(WorkerAgentJobKind.Pass1CodeLogicDocument),
            new(
                pass1.RepositoryRelativePath,
                pass1.LineStart,
                pass1.LineEnd,
                packet.SourceSlice.SymbolName,
                packet.SourceSlice.UnitType,
                packet.SourceSlice.Language,
                pass1.SourceHash,
                pass1.FileIdentityHash,
                context.SourceText),
            context.NeighboringStructuralContext
                .Select(item => new WorkerAgentStructuralContextDto(
                    item.CodeUnitId,
                    item.RepositoryRelativePath,
                    item.SymbolName))
                .ToArray(),
            context.ApprovedCommunicationEvidence
                .Select(item => new WorkerAgentCommunicationEvidenceDto(
                    item.EvidenceId,
                    item.Kind,
                    item.Description))
                .ToArray(),
            context.AllowedReferenceDocumentVersionIds);
    }
}

internal sealed record WorkerAgentPass1SourceDto(
    string RepositoryRelativePath,
    int LineStart,
    int LineEnd,
    string SymbolName,
    CodeUnitType UnitType,
    string? Language,
    string SourceHash,
    string FileIdentityHash,
    string SourceText);

internal sealed record WorkerAgentStructuralContextDto(
    Guid CodeUnitId,
    string RepositoryRelativePath,
    string SymbolName);

internal sealed record WorkerAgentCommunicationEvidenceDto(
    Guid EvidenceId,
    string Kind,
    string Description);

internal sealed record WorkerAgentPass2RequestDto(
    Guid JobId,
    Guid SystemId,
    Guid RepositoryId,
    Guid ScanRunId,
    string ProfiledRevision,
    int SchemaVersion,
    string Kind,
    string PreparedFlowSchemaIdentity,
    Guid ManifestId,
    Guid FlowId,
    IReadOnlyList<WorkerAgentPass2TraceEvidenceDto> TraceEvidence,
    IReadOnlyList<WorkerAgentCodeLogicDocumentVersionDto> CodeLogicDocumentVersions)
{
    internal static WorkerAgentPass2RequestDto From(WorkerAgentJobPacket packet)
    {
        var pass2 = (Pass2BusinessLogicJobPayload)packet.Payload;
        return new(
            packet.JobId,
            packet.SystemId,
            packet.RepositoryId,
            packet.ScanRunId,
            packet.ProfiledRevision,
            packet.SchemaVersion,
            nameof(WorkerAgentJobKind.Pass2BusinessLogicDocument),
            pass2.PreparedFlowSchemaIdentity,
            pass2.ManifestId,
            pass2.FlowId,
            pass2.TraceEvidence.Select(item => new WorkerAgentPass2TraceEvidenceDto(
                item.Order,
                item.MemberJobId,
                item.CodeUnitId,
                item.Label,
                item.Locator)).ToArray(),
            pass2.CodeLogicDocumentVersions.Select(document =>
                new WorkerAgentCodeLogicDocumentVersionDto(
                    document.LogicalDocumentId,
                    document.DocumentVersionId,
                    document.Title,
                    document.RepositoryRelativePath,
                    document.ContentMarkdown,
                    document.SourceAnchors.Select(anchor =>
                        new WorkerAgentSourceAnchorDto
                        {
                            Id = anchor.Id,
                            RepositoryRelativePath = anchor.RepositoryRelativePath,
                            EvidenceLocator = anchor.EvidenceLocator,
                            LineStart = anchor.LineStart,
                            LineEnd = anchor.LineEnd,
                            SourceHash = anchor.SourceHash,
                        }).ToArray(),
                    document.Sections.Select(section =>
                        new WorkerAgentCurrentSectionDto(
                            section.StableSectionId,
                            section.SectionOrder,
                            section.SectionKind,
                            section.Heading,
                            section.ContentMarkdown,
                            section.SearchText,
                            section.SourceAnchorIds,
                            section.AuthorizedDocumentVersionIds)).ToArray()))
                .ToArray());
    }
}

internal sealed record WorkerAgentPass2TraceEvidenceDto(
    int Order,
    Guid MemberJobId,
    Guid CodeUnitId,
    string Label,
    string Locator);

internal sealed record WorkerAgentCodeLogicDocumentVersionDto(
    Guid LogicalDocumentId,
    Guid DocumentVersionId,
    string Title,
    string RepositoryRelativePath,
    string ContentMarkdown,
    IReadOnlyList<WorkerAgentSourceAnchorDto> SourceAnchors,
    IReadOnlyList<WorkerAgentCurrentSectionDto> Sections);

internal sealed record WorkerAgentCurrentSectionDto(
    Guid StableSectionId,
    int SectionOrder,
    DocumentSectionKind SectionKind,
    string Heading,
    string ContentMarkdown,
    string SearchText,
    IReadOnlyList<Guid> SourceAnchorIds,
    IReadOnlyList<Guid> AuthorizedDocumentVersionIds);

internal sealed class WorkerAgentPass1ResponseDto
{
    public required Guid JobId { get; init; }
    public required Guid SystemId { get; init; }
    public required Guid RepositoryId { get; init; }
    public required Guid ScanRunId { get; init; }
    public required Guid CodeUnitId { get; init; }
    public required string ProfiledRevision { get; init; }
    public required string RepositoryRelativePath { get; init; }
    public required int LineStart { get; init; }
    public required int LineEnd { get; init; }
    public required string SourceHash { get; init; }
    public required string FileIdentityHash { get; init; }
    public required WorkerAgentCanonicalContentDto Content { get; init; }
    public required IReadOnlyList<WorkerAgentSourceAnchorDto> SourceAnchors { get; init; }
    public required IReadOnlyList<Guid> ReferenceDocumentVersionIds { get; init; }
    public required IReadOnlyList<Guid> CommunicationEvidenceIds { get; init; }
}

internal sealed class WorkerAgentPass2ResponseDto
{
    public required Guid JobId { get; init; }
    public required Guid SystemId { get; init; }
    public required Guid RepositoryId { get; init; }
    public required Guid ScanRunId { get; init; }
    public required string ProfiledRevision { get; init; }
    public required Guid FlowId { get; init; }
    public required string Purpose { get; init; }
    public required IReadOnlyList<WorkerAgentBusinessMainStepDto> MainSteps { get; init; }
    public required string Outcome { get; init; }
    public required WorkerAgentCanonicalContentDto Content { get; init; }
    public required IReadOnlyList<WorkerAgentSourceAnchorDto> SourceAnchors { get; init; }
    public required IReadOnlyList<int> TraceEvidenceOrders { get; init; }
    public required IReadOnlyList<Guid> GeneratedFromDocumentVersionIds { get; init; }
}

internal sealed class WorkerAgentBusinessMainStepDto
{
    public required int Order { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<int> TraceEvidenceOrders { get; init; }
}

internal sealed class WorkerAgentCanonicalContentDto
{
    public required string SchemaIdentity { get; init; }
    public required DocumentType DocumentType { get; init; }
    public required string StableKey { get; init; }
    public required string Title { get; init; }
    public required string RepositoryRelativePath { get; init; }
    public required string Summary { get; init; }
    public required IReadOnlyList<string> KeySymbols { get; init; }
    public required IReadOnlyList<string> BusinessTerms { get; init; }
    public required IReadOnlyList<string> TechnicalTerms { get; init; }
    public required IReadOnlyList<WorkerAgentSectionDto> Sections { get; init; }
}

internal sealed class WorkerAgentSectionDto
{
    public required Guid StableSectionId { get; init; }
    public required int Order { get; init; }
    public required DocumentSectionKind Kind { get; init; }
    public required string Heading { get; init; }
    public required string Markdown { get; init; }
    public required string SearchText { get; init; }
    public required IReadOnlyList<Guid> SourceAnchorIds { get; init; }
    public required IReadOnlyList<Guid> DocumentVersionIds { get; init; }
}

internal sealed class WorkerAgentSourceAnchorDto
{
    public required Guid Id { get; init; }
    public required string RepositoryRelativePath { get; init; }
    public required string EvidenceLocator { get; init; }
    public required int LineStart { get; init; }
    public required int LineEnd { get; init; }
    public required string SourceHash { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    UseStringEnumConverter = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    GenerationMode = JsonSourceGenerationMode.Metadata | JsonSourceGenerationMode.Serialization)]
[JsonSerializable(typeof(WorkerAgentPass1RequestDto))]
[JsonSerializable(typeof(WorkerAgentPass1ResponseDto))]
[JsonSerializable(typeof(WorkerAgentPass2RequestDto))]
[JsonSerializable(typeof(WorkerAgentPass2ResponseDto))]
internal sealed partial class WorkerAgentJsonContext : JsonSerializerContext
{
    internal static string Pass1ResponseSchemaJson =>
        """
        {
          "type":"object",
          "additionalProperties":false,
          "properties":{
            "jobId":{"type":"string","format":"uuid"},
            "systemId":{"type":"string","format":"uuid"},
            "repositoryId":{"type":"string","format":"uuid"},
            "scanRunId":{"type":"string","format":"uuid"},
            "codeUnitId":{"type":"string","format":"uuid"},
            "profiledRevision":{"type":"string"},
            "repositoryRelativePath":{"type":"string"},
            "lineStart":{"type":"integer"},
            "lineEnd":{"type":"integer"},
            "sourceHash":{"type":"string"},
            "fileIdentityHash":{"type":"string"},
            "content":{
              "type":"object",
              "additionalProperties":false,
              "properties":{
                "schemaIdentity":{"type":"string"},
                "documentType":{"type":"string","enum":["CodeLogic"]},
                "stableKey":{"type":"string"},
                "title":{"type":"string"},
                "repositoryRelativePath":{"type":"string"},
                "summary":{"type":"string"},
                "keySymbols":{"type":"array","items":{"type":"string"}},
                "businessTerms":{"type":"array","items":{"type":"string"}},
                "technicalTerms":{"type":"array","items":{"type":"string"}},
                "sections":{
                  "type":"array",
                  "items":{
                    "type":"object",
                    "additionalProperties":false,
                    "properties":{
                      "stableSectionId":{"type":"string","format":"uuid"},
                      "order":{"type":"integer"},
                      "kind":{"type":"string","enum":["Overview","BusinessRule","ProcessStep","TechnicalDetail","Evidence","Other"]},
                      "heading":{"type":"string"},
                      "markdown":{"type":"string"},
                      "searchText":{"type":"string"},
                      "sourceAnchorIds":{"type":"array","items":{"type":"string","format":"uuid"}},
                      "documentVersionIds":{"type":"array","items":{"type":"string","format":"uuid"}}
                    },
                    "required":["stableSectionId","order","kind","heading","markdown","searchText","sourceAnchorIds","documentVersionIds"]
                  }
                }
              },
              "required":["schemaIdentity","documentType","stableKey","title","repositoryRelativePath","summary","keySymbols","businessTerms","technicalTerms","sections"]
            },
            "sourceAnchors":{
              "type":"array",
              "items":{
                "type":"object",
                "additionalProperties":false,
                "properties":{
                  "id":{"type":"string","format":"uuid"},
                  "repositoryRelativePath":{"type":"string"},
                  "evidenceLocator":{"type":"string"},
                  "lineStart":{"type":"integer"},
                  "lineEnd":{"type":"integer"},
                  "sourceHash":{"type":"string"}
                },
                "required":["id","repositoryRelativePath","evidenceLocator","lineStart","lineEnd","sourceHash"]
              }
            },
            "referenceDocumentVersionIds":{"type":"array","items":{"type":"string","format":"uuid"}},
            "communicationEvidenceIds":{"type":"array","items":{"type":"string","format":"uuid"}}
          },
          "required":["jobId","systemId","repositoryId","scanRunId","codeUnitId","profiledRevision","repositoryRelativePath","lineStart","lineEnd","sourceHash","fileIdentityHash","content","sourceAnchors","referenceDocumentVersionIds","communicationEvidenceIds"]
        }
        """;

    internal static string Pass2ResponseSchemaJson =>
        """
        {
          "type":"object",
          "additionalProperties":false,
          "properties":{
            "jobId":{"type":"string","format":"uuid"},
            "systemId":{"type":"string","format":"uuid"},
            "repositoryId":{"type":"string","format":"uuid"},
            "scanRunId":{"type":"string","format":"uuid"},
            "profiledRevision":{"type":"string"},
            "flowId":{"type":"string","format":"uuid"},
            "purpose":{"type":"string"},
            "mainSteps":{
              "type":"array",
              "items":{
                "type":"object",
                "additionalProperties":false,
                "properties":{
                  "order":{"type":"integer"},
                  "description":{"type":"string"},
                  "traceEvidenceOrders":{"type":"array","items":{"type":"integer"}}
                },
                "required":["order","description","traceEvidenceOrders"]
              }
            },
            "outcome":{"type":"string"},
            "content":{
              "type":"object",
              "additionalProperties":false,
              "properties":{
                "schemaIdentity":{"type":"string"},
                "documentType":{"type":"string","enum":["BusinessLogic"]},
                "stableKey":{"type":"string"},
                "title":{"type":"string"},
                "repositoryRelativePath":{"type":"string"},
                "summary":{"type":"string"},
                "keySymbols":{"type":"array","items":{"type":"string"}},
                "businessTerms":{"type":"array","items":{"type":"string"}},
                "technicalTerms":{"type":"array","items":{"type":"string"}},
                "sections":{
                  "type":"array",
                  "items":{
                    "type":"object",
                    "additionalProperties":false,
                    "properties":{
                      "stableSectionId":{"type":"string","format":"uuid"},
                      "order":{"type":"integer"},
                      "kind":{"type":"string","enum":["Overview","BusinessRule","ProcessStep","TechnicalDetail","Evidence","Other"]},
                      "heading":{"type":"string"},
                      "markdown":{"type":"string"},
                      "searchText":{"type":"string"},
                      "sourceAnchorIds":{"type":"array","items":{"type":"string","format":"uuid"}},
                      "documentVersionIds":{"type":"array","items":{"type":"string","format":"uuid"}}
                    },
                    "required":["stableSectionId","order","kind","heading","markdown","searchText","sourceAnchorIds","documentVersionIds"]
                  }
                }
              },
              "required":["schemaIdentity","documentType","stableKey","title","repositoryRelativePath","summary","keySymbols","businessTerms","technicalTerms","sections"]
            },
            "sourceAnchors":{
              "type":"array",
              "items":{
                "type":"object",
                "additionalProperties":false,
                "properties":{
                  "id":{"type":"string","format":"uuid"},
                  "repositoryRelativePath":{"type":"string"},
                  "evidenceLocator":{"type":"string"},
                  "lineStart":{"type":"integer"},
                  "lineEnd":{"type":"integer"},
                  "sourceHash":{"type":"string"}
                },
                "required":["id","repositoryRelativePath","evidenceLocator","lineStart","lineEnd","sourceHash"]
              }
            },
            "traceEvidenceOrders":{"type":"array","items":{"type":"integer"}},
            "generatedFromDocumentVersionIds":{"type":"array","items":{"type":"string","format":"uuid"}}
          },
          "required":["jobId","systemId","repositoryId","scanRunId","profiledRevision","flowId","purpose","mainSteps","outcome","content","sourceAnchors","traceEvidenceOrders","generatedFromDocumentVersionIds"]
        }
        """;
}
