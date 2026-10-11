using System.Collections.Immutable;
using FluentValidation;
using Vulgata.Core.DomainServices;

namespace Vulgata.OctoberGenerator;

public sealed record OctoberScanSourceRule(
    Guid RepositoryId,
    string RelativePathPrefix,
    string Disposition,
    string Reason,
    int Priority = 0);

public sealed record OctoberScanPolicy
{
    public string? OutputStage { get; init; }
    public int WorkerCap { get; init; } = 1;
    public int RunDeadlineSeconds { get; init; } = 1800;
    public int FileDeadlineSeconds { get; init; } = 600;
    public int GracefulDrainSeconds { get; init; } = 30;
    public int MaximumContinuationWindows { get; init; } = 4;
    public int MaximumModelRequestsPerFile { get; init; } = 8;
    public int AdvisoryToolCallsPerFile { get; init; } = 32;
    public int MaximumToolCallsPerFile { get; init; } = 48;
    public int MaximumTransientRetries { get; init; } = 2;
    /// <summary>Retained configuration compatibility; JSON corrections always request json_object.</summary>
    public bool UseTextForJsonCorrection { get; init; }
    public bool HardRetryMode { get; init; }
    public int MaximumProviderRequests { get; init; } = 64;
    public int MaximumRequestsPerMinute { get; init; } = 60;
    public long? MaximumTotalTokens { get; init; }
    public ImmutableArray<OctoberScanSourceRule> SourceRules { get; init; } = [];
}

public sealed class OctoberScanPolicyValidator : AbstractValidator<OctoberScanPolicy>
{
    public OctoberScanPolicyValidator()
    {
        RuleFor(policy => policy.OutputStage).Must(stage => stage is "cl-only" or "cl-and-bl");
        RuleFor(policy => policy.WorkerCap).InclusiveBetween(1, 64);
        RuleFor(policy => policy.RunDeadlineSeconds).InclusiveBetween(1, ExecutionOwnerLifetime.MaximumSeconds);
        RuleFor(policy => policy.FileDeadlineSeconds).GreaterThan(0)
            .LessThanOrEqualTo(policy => policy.RunDeadlineSeconds);
        RuleFor(policy => policy.GracefulDrainSeconds).GreaterThan(0)
            .LessThanOrEqualTo(policy => policy.RunDeadlineSeconds);
        RuleFor(policy => policy.MaximumContinuationWindows).GreaterThan(0)
            .LessThanOrEqualTo(policy => policy.MaximumModelRequestsPerFile);
        RuleFor(policy => policy.MaximumModelRequestsPerFile).GreaterThan(0)
            .LessThanOrEqualTo(policy => policy.MaximumProviderRequests);
        RuleFor(policy => policy.MaximumToolCallsPerFile).InclusiveBetween(2, 1000);
        RuleFor(policy => policy.AdvisoryToolCallsPerFile).GreaterThan(0)
            .LessThan(policy => policy.MaximumToolCallsPerFile);
        RuleFor(policy => policy.MaximumTransientRetries).InclusiveBetween(0, 2);
        When(policy => policy.HardRetryMode, () =>
            RuleFor(policy => policy.MaximumTransientRetries).Equal(0));
        RuleFor(policy => policy.MaximumProviderRequests).InclusiveBetween(1, 1_000_000);
        RuleFor(policy => policy.MaximumRequestsPerMinute).InclusiveBetween(1, 1_000_000);
        RuleFor(policy => policy.MaximumTotalTokens).NotNull().GreaterThan(0);
        RuleFor(policy => policy.SourceRules).Must(rules => !rules.IsDefaultOrEmpty &&
            rules.Length <= 1024 && rules.All(rule => rule is not null) &&
            rules.Select(rule => (rule.RepositoryId, rule.RelativePathPrefix))
                .Distinct().Count() == rules.Length);
        When(policy => !policy.SourceRules.IsDefault, () =>
            RuleForEach(policy => policy.SourceRules).ChildRules(rule =>
            {
                rule.RuleFor(value => value.RepositoryId).NotEmpty();
                rule.RuleFor(value => value.RelativePathPrefix).Must(IsRelativePrefix);
                rule.RuleFor(value => value.Disposition).Must(disposition =>
                    disposition is "selected" or "supporting" or "excluded" or "unsupported");
                rule.RuleFor(value => value.Reason).NotEmpty().MaximumLength(512)
                    .Must(reason => reason is not null && reason.Trim() == reason &&
                        !reason.Any(char.IsControl));
            }));
    }

    private static bool IsRelativePrefix(string? path) => path is not null &&
        path.Length <= 1024 && path.Trim() == path &&
        !path.Contains('\\') && !path.Contains(':') && !path.Contains('*') &&
        !path.Contains('?') && !path.Any(char.IsControl) && !path.StartsWith('/') &&
        (path.Length == 0 || path.Split('/').All(part =>
            part.Length > 0 && part is not "." and not ".." && !part.EndsWith('.') &&
            part.Trim() == part));
}
