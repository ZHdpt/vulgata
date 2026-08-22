using System.Security.Cryptography;
using System.Text;

namespace Vulgata.Core.DomainServices;

public sealed record PreparedPass1RevisionBinding(
    Guid PairExecutionId,
    Guid RepositoryId,
    Guid ScanRunId,
    string ProfiledRevision,
    string BindingToken);

public sealed record PreparedPass1PairRevisionBindings(
    Guid PairExecutionId,
    IReadOnlyList<PreparedPass1RevisionBinding> Bindings);

public interface IPreparedPass1RevisionBindingRegistry
{
    Task<PreparedPass1PairRevisionBindings?> AcquirePairAsync(
        Guid pairExecutionId,
        IReadOnlyList<PreparedRevisionRequest> requests,
        CancellationToken cancellationToken = default);

    PreparedPass1RevisionBinding? ResolveBinding(
        Guid pairExecutionId,
        Guid repositoryId);

    ValueTask ReleasePairAsync(Guid pairExecutionId);
}

public interface IPreparedRevisionLeaseResolver
{
    bool TryResolveLease(
        string bindingToken,
        out ICommittedRepositorySnapshotLease? lease);
}

public static class PreparedPass1RevisionBindingToken
{
    public static string Create(
        Guid pairExecutionId,
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision)
    {
        if (pairExecutionId == Guid.Empty || repositoryId == Guid.Empty || scanRunId == Guid.Empty)
        {
            throw new ArgumentException("Complete pair and Repository identities are required.");
        }

        string revision = profiledRevision ?? string.Empty;
        if (revision is not { Length: 40 or 64 } || revision.Any(character =>
                !char.IsAsciiDigit(character) && character is not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("An exact Profiled Revision is required.", nameof(profiledRevision));
        }

        byte[] source = Encoding.UTF8.GetBytes(string.Join(
            '|',
            "vulgata/story-12.3/pass1-revision-binding/1.0",
            pairExecutionId.ToString("D"),
            repositoryId.ToString("D"),
            scanRunId.ToString("D"),
            revision));
        return "p1-" + Convert.ToHexStringLower(SHA256.HashData(source));
    }
}
