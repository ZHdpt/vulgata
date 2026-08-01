using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

/// <summary>
/// Infrastructure adapter that implements <see cref="ICodeUnitSource"/> by
/// reading prepared <see cref="CodeUnit"/> rows for a ScanRun/Repository from
/// <see cref="VulgataDbContext.CodeUnits"/>. The runner applies its own
/// deterministic ordering (FilePath ordinal, then LineStart, then Id); this
/// adapter returns the prepared set without forcing a server-side order, so
/// the runner's ordering is authoritative.
/// </summary>
public sealed class CodeUnitSource(VulgataDbContext dbContext) : ICodeUnitSource
{
    public Task<List<CodeUnit>> GetPreparedAsync(
        Guid scanRunId,
        Guid repositoryId,
        CancellationToken cancellationToken = default) =>
        dbContext.CodeUnits
            .AsNoTracking()
            .Where(unit => unit.ScanRunId == scanRunId && unit.RepositoryId == repositoryId)
            .ToListAsync(cancellationToken);

    // The interface declares IReadOnlyList<CodeUnit>; List<CodeUnit> satisfies it.
    // Explicit interface implementation keeps the adapter's concrete return type
    // (List<CodeUnit>) while satisfying the port contract.
    async Task<IReadOnlyList<CodeUnit>> ICodeUnitSource.GetPreparedAsync(
        Guid scanRunId,
        Guid repositoryId,
        CancellationToken cancellationToken) =>
        await GetPreparedAsync(scanRunId, repositoryId, cancellationToken)
            .ConfigureAwait(false);
}
