using System.Diagnostics;

namespace Vulgata.Tests.CodeGraphQualification;

internal sealed class FakeQualificationProcessLauncher : IQualificationProcessLauncher
{
    internal int CreatedProcessCount { get; private set; }

    public Process CreateProcess(ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        throw new InvalidOperationException("Synthetic qualification launchers cannot create provider processes.");
    }
}
