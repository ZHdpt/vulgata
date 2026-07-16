using System.Diagnostics;

namespace Vulgata.Tests.CodeGraphQualification;

internal sealed class FakeQualificationProcessLauncher : IQualificationProcessLauncher
{
    internal int CreatedProcessCount { get; private set; }
    internal CgcQualificationLaunchPlan? ObservedPlan { get; private set; }
    internal List<string> Events { get; } = [];

    internal CgcFakeQualificationOutcome Outcome { get; set; } = CgcFakeQualificationOutcome.Success;

    internal Task<CgcFakeQualificationOutcome> RunAsync(
        CgcQualificationLaunchPlan plan,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObservedPlan = plan ?? throw new ArgumentNullException(nameof(plan));
        Events.Add("operation");
        Events.Add("gateway-SIGTERM");
        Events.Add("gateway-grace-5s");
        Events.Add("gateway-SIGKILL-if-running");
        Events.Add("context-delete");
        return Task.FromResult(Outcome);
    }

    public Process CreateProcess(ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        throw new InvalidOperationException("Synthetic qualification launchers cannot create provider processes.");
    }
}

internal sealed record CgcFakeQualificationOutcome(
    bool OperationSucceeded,
    bool CleanupSucceeded,
    string? FailureSummary)
{
    internal static CgcFakeQualificationOutcome Success { get; } = new(true, true, null);
}
