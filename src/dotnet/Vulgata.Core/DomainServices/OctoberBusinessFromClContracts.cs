using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Vulgata.Core.DomainServices.OctoberKnowledge;

/// <summary>Exact original live request to use an immutable, fully published CL corpus.</summary>
public sealed record BusinessFromClRequest(PublicationBinding Publication, string SubjectId, Guid RunId,
    string CanonicalPayloadDigest);

public sealed record BusinessFromClSnapshot(PublicationBinding Publication, CorpusHandle Corpus,
    ImmutableArray<AcceptedFile> AcceptedFiles, ImmutableArray<FileCoverage> Coverage,
    ImmutableArray<Contributor> Contributors, ImmutableArray<ResolutionOutcome> ExistingResolutions);

/// <summary>Issuer-verified process-local ownership, never recreated from a stored receipt.</summary>
public abstract class BusinessFromClAdmission
{
    protected BusinessFromClAdmission() { }
}

public sealed record BusinessFromClResult(KnowledgeWriteStatus Status, OperationKey Operation,
    BusinessFromClSnapshot? Snapshot = null,
    [property: JsonIgnore] BusinessFromClAdmission? Admission = null);

/// <summary>Cleanup bookkeeping only; this request never accepts or publishes semantic content.</summary>
public sealed record BusinessFromClCompletionRequest(BusinessFromClRequest Begin, string CanonicalPayloadDigest);

public enum BusinessFromClSettlement { Settled, Unsettled, Unavailable }

public interface IOctoberBusinessFromClAuthority
{
    Task<DisclosureStatus> CheckAsync(BusinessFromClRequest request, ImmutableArray<Contributor> contributors,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Trusted original host verifies that generation is permanently fenced and every actual provider,
/// tool, authority and persistence call is settled. A log, report or caller-provided boolean is not proof.
/// </summary>
public interface IOctoberBusinessFromClSettlementAuthority
{
    Task<BusinessFromClSettlement> VerifyAsync(BusinessFromClCompletionRequest request,
        BusinessFromClAdmission admission, CancellationToken cancellationToken = default);
}

public interface IOctoberBusinessFromClStore
{
    Task<BusinessFromClResult> BeginAsync(BusinessFromClRequest request, CancellationToken cancellationToken = default);
    Task<BusinessFromClResult> LookupAsync(BusinessFromClRequest request, CancellationToken cancellationToken = default);
    Task<KnowledgeWriteResult> CompleteAsync(BusinessFromClCompletionRequest request, BusinessFromClAdmission admission,
        CancellationToken cancellationToken = default);
    Task<KnowledgeWriteResult> LookupCompletionAsync(BusinessFromClCompletionRequest request, BusinessFromClAdmission admission,
        CancellationToken cancellationToken = default);
}

public static class OctoberBusinessFromClPolicy
{
    public static OperationKey BeginOperation(BusinessFromClRequest request) =>
        new(request.Publication.Knowledge.BatchId, OperationKind.BeginBusinessFromCl, request.RunId);
    public static OperationKey CompleteOperation(BusinessFromClRequest request) =>
        new(request.Publication.Knowledge.BatchId, OperationKind.CompleteBusinessFromCl, request.RunId);
    public static string ComputeBeginDigest(BusinessFromClRequest request) => OctoberKnowledgePolicy.Hash(
        OctoberKnowledgePolicy.SerializeCanonical(new
        {
            domain = "october/business-from-cl/begin/1", request.Publication, request.SubjectId, request.RunId
        }));
    public static string ComputeCompletionDigest(BusinessFromClCompletionRequest request) => OctoberKnowledgePolicy.Hash(
        OctoberKnowledgePolicy.SerializeCanonical(new
        {
            domain = "october/business-from-cl/complete/1", operation = BeginOperation(request.Begin),
            beginPayloadDigest = request.Begin.CanonicalPayloadDigest
        }));
}
