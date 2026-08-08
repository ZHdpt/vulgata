using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Agents.WorkerExecution;

public static class PreparedPass2FlowResolver
{
    public static void ValidateRuntime(ResolvedPreparedPass2Flow resolvedFlow)
    {
        ArgumentNullException.ThrowIfNull(resolvedFlow);
        _ = Pass2BusinessLogicJobPayload.Create(resolvedFlow);
    }

    public static ResolvedPreparedPass2Flow Resolve(
        PreparedPass2Flow flow,
        Guid systemId,
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision,
        RepositoryAccessScope scope,
        IReadOnlyList<PreparedPass2CodeLogicCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(candidates);
        if (systemId == Guid.Empty ||
            repositoryId == Guid.Empty ||
            scanRunId == Guid.Empty ||
            !IsProfiledRevision(profiledRevision) ||
            flow.SystemId != systemId ||
            flow.RepositoryId != repositoryId ||
            !scope.Contains(repositoryId) ||
            candidates.Count != flow.Members.Count)
        {
            throw new PreparedPass2FlowContractException();
        }

        if (HasDuplicates(candidates.Select(candidate => candidate.JobId)) ||
            HasDuplicates(candidates.Select(candidate => candidate.CodeUnitId)) ||
            HasDuplicates(candidates.Select(candidate => candidate.CommittedDocumentVersionId)) ||
            HasDuplicates(candidates.Select(candidate => candidate.CurrentProjection.LogicalDocumentId)) ||
            HasDuplicates(candidates.Select(candidate => candidate.CurrentProjection.DocumentVersionId)))
        {
            throw new PreparedPass2FlowContractException();
        }

        CurrentKnowledge[] ordered = new CurrentKnowledge[candidates.Count];
        for (int index = 0; index < flow.Members.Count; index++)
        {
            PreparedPass2FlowMember member = flow.Members[index];
            PreparedPass2CodeLogicCandidate candidate = candidates[index];
            CurrentKnowledge current = candidate.CurrentProjection ??
                throw new PreparedPass2FlowContractException();
            if (candidate.JobId != member.JobId ||
                candidate.CodeUnitId != member.CodeUnitId ||
                candidate.SystemId != systemId ||
                candidate.RepositoryId != repositoryId ||
                candidate.ScanRunId != scanRunId ||
                !StringComparer.Ordinal.Equals(
                    candidate.ProfiledRevision,
                    profiledRevision) ||
                candidate.CommittedDocumentVersionId == Guid.Empty ||
                current.LogicalDocumentId != member.LogicalDocumentId ||
                current.DocumentVersionId != candidate.CommittedDocumentVersionId ||
                current.RepositoryId != repositoryId ||
                current.DocumentType != DocumentType.CodeLogic ||
                current.SourceAnchors.Count == 0 ||
                current.SourceAnchors.Any(anchor =>
                    anchor.CodeUnitId != member.CodeUnitId))
            {
                throw new PreparedPass2FlowContractException();
            }

            ordered[index] = current;
        }

        return new(flow, ordered);
    }

    private static bool IsProfiledRevision(string value) =>
        value is not null &&
        value.Length is >= 7 and <= 64 &&
        value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool HasDuplicates(IEnumerable<Guid> identifiers)
    {
        Guid[] values = identifiers.ToArray();
        return values.Any(identifier => identifier == Guid.Empty) ||
            values.Distinct().Count() != values.Length;
    }
}
