namespace Vulgata.Core.Entities;

public enum Pass2OmissionState
{
    Omitted,
}

public sealed class Pass2Omission
{
    private Pass2Omission()
    {
    }

    public static Pass2Omission CreateBoundExcluded(
        Guid id,
        Guid pass2ExecutionId,
        Guid exactResolutionDecisionId,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || pass2ExecutionId == Guid.Empty ||
            exactResolutionDecisionId == Guid.Empty || createdAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A complete UTC-bound Pass 2 omission identity is required.");
        }

        return new Pass2Omission
        {
            Id = id,
            Pass2ExecutionId = pass2ExecutionId,
            ExactResolutionDecisionId = exactResolutionDecisionId,
            State = Pass2OmissionState.Omitted,
            ReasonCode = "flow-group-bound",
            CreatedAt = createdAt,
        };
    }

    public Guid Id { get; private set; }
    public Guid Pass2ExecutionId { get; private set; }
    public Guid ExactResolutionDecisionId { get; private set; }
    public Pass2OmissionState State { get; private set; }
    public string ReasonCode { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
}
