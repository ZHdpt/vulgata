namespace Vulgata.Tests.RetainedSource;

/// <summary>
/// Marker and mode contract for the later child-process test. No test method is
/// declared in the scaffolding phase, so invoking the filter cannot produce a
/// passing placeholder or be mistaken for reopen qualification.
/// </summary>
internal static class RetainedSourceSeparateProcessChild
{
    internal const string ModeVariable = "VULGATA_RETAINED_SOURCE_CHILD";

    internal const string ModeValue = "1";

    internal static bool IsEnabled() =>
        string.Equals(
            Environment.GetEnvironmentVariable(ModeVariable),
            ModeValue,
            StringComparison.Ordinal);

    internal static void RequireEnabled()
    {
        if (!IsEnabled())
        {
            throw new InvalidOperationException(
                "The retained-source child process requires its explicit test mode.");
        }
    }
}
