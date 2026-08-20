using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Analysis.Java;

/// <summary>
/// Converts exact top-level JDT-LS UTF-16 positions into raw-byte ranges.
/// It is intentionally pure; readiness and session lifecycle remain with the
/// prepared Java readiness service.
/// </summary>
public sealed class PreparedJavaRangeProvider
{
    public PreparedSourceSplitPlan GetRanges(
        ReadOnlyMemory<byte> strictUtf8Source,
        string expectedFileHash,
        IEnumerable<PreparedJdtSymbolSpan> exactTopLevelRanges) =>
        PreparedSourceRangePlanner.PlanJavaRanges(
            strictUtf8Source,
            expectedFileHash,
            exactTopLevelRanges);
}
