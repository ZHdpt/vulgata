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

    public Guid Id { get; private set; }
    public Guid Pass2ExecutionId { get; private set; }
    public Guid ExactResolutionDecisionId { get; private set; }
    public Pass2OmissionState State { get; private set; }
    public string ReasonCode { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
}
