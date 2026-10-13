using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Vulgata.Core.DomainServices.OctoberKnowledge;

/// <summary>Exact original live request to use an immutable, fully published CL corpus.</summary>
public sealed record BusinessFromClRequest(PublicationBinding Publication, string SubjectId, Guid RunId,
    string CanonicalPayloadDigest)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResolutionPolicyVersion { get; init; }
}

public sealed record BusinessFromClSnapshot(PublicationBinding Publication, CorpusHandle Corpus,
    ImmutableArray<AcceptedFile> AcceptedFiles, ImmutableArray<FileCoverage> Coverage,
    ImmutableArray<Contributor> Contributors, ImmutableArray<ResolutionOutcome> ExistingResolutions)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResolutionPolicyVersion { get; init; }
}

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
    /// <summary>Call again under the semantic transaction's batch lock before physical submission.</summary>
    Task<CommitEligibilityStatus> CheckActiveAsync(BusinessFromClRequest request, BusinessFromClAdmission admission,
        CancellationToken cancellationToken = default);
    Task<BusinessFromClResult> BeginAsync(BusinessFromClRequest request, CancellationToken cancellationToken = default);
    Task<BusinessFromClResult> LookupAsync(BusinessFromClRequest request, CancellationToken cancellationToken = default);
    Task<KnowledgeWriteResult> CompleteAsync(BusinessFromClCompletionRequest request, BusinessFromClAdmission admission,
        CancellationToken cancellationToken = default);
    Task<KnowledgeWriteResult> LookupCompletionAsync(BusinessFromClCompletionRequest request, BusinessFromClAdmission admission,
        CancellationToken cancellationToken = default);
}

/// <summary>Administrative abandonment, never evidence of successful generation or absent prior effects.</summary>
public sealed record BusinessFromClAbandonment(PublicationBinding Publication, string BeginPayloadDigest,
    string RecoverySubjectId, string EvidenceDigest)
{
    public string BeginSubjectId { get; init; } = "";
}

public sealed record BusinessFromClRecoveryRequest(BusinessFromClRequest Begin, string RecoverySubjectId,
    string EvidenceDigest, string CanonicalPayloadDigest);

/// <summary>
/// Trusted maintenance host verifies explicit operator authority, permanent original host/descendant
/// exclusion and settled actual provider/database work. Receipt absence or a caller boolean is not proof.
/// Verification must remain valid through submission; no capability for generation is issued.
/// </summary>
public interface IOctoberBusinessFromClRecoveryAuthority
{
    Task<BusinessFromClSettlement> VerifyAsync(BusinessFromClRecoveryRequest request,
        CancellationToken cancellationToken = default);
}

public static class OctoberBusinessFromClRecoveryPolicy
{
    public static OperationKey Operation(BusinessFromClRecoveryRequest request) =>
        new(request.Begin.Publication.Knowledge.BatchId, OperationKind.AbandonBusinessFromCl, request.Begin.RunId);
    public static string ComputeDigest(BusinessFromClRecoveryRequest request) => OctoberKnowledgePolicy.Hash(
        OctoberKnowledgePolicy.SerializeCanonical(new
        {
            domain = "october/business-from-cl/abandon/1", request.Begin, request.RecoverySubjectId, request.EvidenceDigest
        }));
}

public static class OctoberBusinessFromClPolicy
{
    public static OperationKey BeginOperation(BusinessFromClRequest request) =>
        new(request.Publication.Knowledge.BatchId, OperationKind.BeginBusinessFromCl, request.RunId);
    public static OperationKey CompleteOperation(BusinessFromClRequest request) =>
        new(request.Publication.Knowledge.BatchId, OperationKind.CompleteBusinessFromCl, request.RunId);
    public static string ComputeBeginDigest(BusinessFromClRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = OctoberResolutionPolicy.SelectedPolicyVersion(request.ResolutionPolicyVersion);
        return request.ResolutionPolicyVersion is null ? OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(new
        {
            domain = "october/business-from-cl/begin/1", request.Publication, request.SubjectId, request.RunId
        })) : OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(new
        {
            domain = "october/business-from-cl/begin/2", request.Publication, request.SubjectId, request.RunId,
            request.ResolutionPolicyVersion
        }));
    }
    public static string ComputeCompletionDigest(BusinessFromClCompletionRequest request) => OctoberKnowledgePolicy.Hash(
        OctoberKnowledgePolicy.SerializeCanonical(new
        {
            domain = "october/business-from-cl/complete/1", operation = BeginOperation(request.Begin),
            beginPayloadDigest = request.Begin.CanonicalPayloadDigest
        }));
}
