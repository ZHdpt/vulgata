using System.Diagnostics;

namespace Vulgata.Tests.CodeGraphQualification;

internal sealed class DockerQualificationProcessLauncher : IQualificationProcessLauncher
{
    private readonly ProviderExecutionLease _lease;

    internal DockerQualificationProcessLauncher(ProviderExecutionLease lease)
    {
        _lease = lease ?? throw new ArgumentNullException(nameof(lease));
    }

    public Process CreateProcess(ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        _ = _lease;
        return new Process { StartInfo = startInfo };
    }
}
