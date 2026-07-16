using System.Diagnostics;

namespace Vulgata.Tests.CodeGraphQualification;

internal interface IQualificationProcessLauncher
{
    Process CreateProcess(ProcessStartInfo startInfo);
}
