namespace Vulgata.Core.DomainServices.OctoberKnowledge;

/// <summary>Host-owned original request; never reconstructed from a model, restart or unknown receipt.</summary>
public sealed record GenerateAdmissionRequest(KnowledgeBinding Binding, string SubjectId, Guid RunId,
    Guid AdmissionOperationId, string CanonicalPayloadDigest);

/// <summary>Fresh held-operator authorization for the exact subject and selected binding, including receipt disclosure.</summary>
public interface IOctoberGenerateAdmissionAuthority
{
    Task<DisclosureStatus> CheckAsync(GenerateAdmissionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// One immutable execution admission per batch, using the owner-derived BeginGenerate operation key.
/// Call after source-free preflight and before native preparation, source inventory/frames or Worker work.
/// A receipt proves storage truth only: Scan permits dispatch once for the original live run, never restart.
/// Unknown acknowledgement or an absent lookup cannot authorize execution, another claim or a new run.
/// </summary>
public interface IOctoberGenerateAdmissionStore
{
    Task<KnowledgeWriteResult> StartFreshExecutionAsync(GenerateAdmissionRequest request,
        CancellationToken cancellationToken = default);
    Task<KnowledgeWriteResult> LookupAsync(GenerateAdmissionRequest request,
        CancellationToken cancellationToken = default);
}

public static class OctoberGenerateAdmissionPolicy
{
    /// <summary>Callers cannot select a second storage key for a batch.</summary>
    public static OperationKey OperationFor(KnowledgeBinding binding) =>
        new(binding.BatchId, OperationKind.BeginGenerate, binding.BatchId);

    /// <summary>Owner canonical identity helper seam; excludes CanonicalPayloadDigest itself.</summary>
    public static string ComputePayloadDigest(GenerateAdmissionRequest request) => throw new NotImplementedException();
}
