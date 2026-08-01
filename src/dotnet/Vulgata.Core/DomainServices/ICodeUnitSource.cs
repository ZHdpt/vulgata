using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

/// <summary>
/// Read port for the prepared Code Units of a ScanRun, consumed by the
/// Sequential Scan Runner to materialize typed Worker Agent job packets.
/// </summary>
public interface ICodeUnitSource
{
    /// <summary>
    /// Return the prepared Code Units for the given ScanRun and Repository.
    /// The runner consumes them in deterministic order (by FilePath, then
    /// LineStart, then stable CodeUnitId).
    /// </summary>
    Task<IReadOnlyList<CodeUnit>> GetPreparedAsync(
        Guid scanRunId,
        Guid repositoryId,
        CancellationToken cancellationToken = default);
}
