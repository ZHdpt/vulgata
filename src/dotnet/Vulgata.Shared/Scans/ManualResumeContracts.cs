using FluentValidation;

namespace Vulgata.Shared.Scans;

public sealed record ManualResumeRequest(string ScanExecutionId);

public sealed record ManualResumeResponse(
    Guid ScanExecutionId,
    string Phase,
    int PendingJobCount,
    int RunningJobCount,
    int RetryScheduledJobCount,
    int CompletedJobCount,
    int FailedJobCount,
    int CancelledJobCount,
    int RecoveredExpiredLeaseCount,
    int ReleasedDueRetryCount,
    bool StateChanged,
    string? ReasonCode);

public sealed class ManualResumeRequestValidator : AbstractValidator<ManualResumeRequest>
{
    public ManualResumeRequestValidator()
    {
        RuleFor(request => request.ScanExecutionId)
            .Must(value => Guid.TryParse(value, out Guid id) && id != Guid.Empty)
            .WithMessage("扫描执行标识无效。");
    }
}
