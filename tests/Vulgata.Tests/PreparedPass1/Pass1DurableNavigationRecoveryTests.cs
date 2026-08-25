using System.Reflection;
using System.Text.RegularExpressions;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// Story 12.3 T109 RED boundary for durable navigation recovery.
///
/// These checks stay provider/database-free: they inspect the actual public
/// production seams and their source-level call ordering. The behavioral
/// implementation is owned by T110; a missing seam is an honest RED rather
/// than a test-only substitute for durable state.
/// </summary>
public sealed class Pass1DurableNavigationRecoveryTests
{
    [Fact]
    public void ClaimedPacketHydration_ReconstructsExactDurableBudgetProjection()
    {
        string source = ReadRepositorySource(
            "src/dotnet/Vulgata.Infrastructure/Data/PreparedPass1ClaimedPacketSource.cs");

        Assert.Contains(
            "Pass1NavigationBudgets",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "ToolAttemptCount",
            source,
            StringComparison.Ordinal);
        Assert.True(
            Regex.IsMatch(
                source,
                @"RemainingNavigationBudget\s*=\s*8\s*-\s*(?:budget\.)?ToolAttemptCount",
                RegexOptions.CultureInvariant),
            "T109 RED: hydration must derive remaining capacity as 8 - durable ToolAttemptCount, not default to eight.");
        Assert.Contains(
            "AcceptedCallCount",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "EvidenceBytes",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "StateVersion",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void HighWaterEight_RepairsCallsMarkerBeforeRetryOrTerminalVisibility()
    {
        string runner = ReadRepositorySource(
            "src/dotnet/Vulgata.Web/Data/PreparedPass1JobRunner.cs");
        string store = ReadRepositorySource(
            "src/dotnet/Vulgata.Infrastructure/Data/PreparedPass1Store.cs");

        Assert.Contains(
            "EnsureNavigationCallsMarkerAsync",
            store,
            StringComparison.Ordinal);
        int repair = runner.IndexOf(
            "EnsureNavigationCallsMarkerAsync",
            StringComparison.Ordinal);
        int retry = runner.IndexOf(
            "RecordFailureAsync(",
            StringComparison.Ordinal);
        int terminal = runner.IndexOf(
            "CompletePass1Async(",
            StringComparison.Ordinal);
        Assert.True(
            repair >= 0 && (retry < 0 || repair < retry) &&
                (terminal < 0 || repair < terminal),
            "T109 RED: marker nine must be repaired before provider retry or terminal visibility.");
    }

    [Fact]
    public void RetryLeaseRecoveryAndRestart_RehydrateOnlyDurableClaimState()
    {
        string runner = ReadRepositorySource(
            "src/dotnet/Vulgata.Web/Data/PreparedPass1JobRunner.cs");
        string coordinator = ReadRepositorySource(
            "src/dotnet/Vulgata.Web/Data/PreparedPass1Coordinator.cs");
        string dispatcher = ReadRepositorySource(
            "src/dotnet/Vulgata.Agents/WorkerExecution/PreparedPass1WorkerDispatcher.cs");

        Assert.Contains(
            "IPreparedPass1ClaimedPacketSource",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "IPreparedPass1Store",
            coordinator,
            StringComparison.Ordinal);
        Assert.Contains(
            "RecoverAsync",
            coordinator,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "_conversationStates",
            dispatcher,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ConversationState",
            dispatcher,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RemainingNavigationBudget--",
            dispatcher,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ReservationPort_IsReachableFromPreparedProductionComposition()
    {
        Type reservationPort = typeof(IPass1NavigationAttemptReservationStore);
        Assert.True(
            reservationPort.IsInterface,
            "The durable reservation boundary must remain a Core interface.");
        Assert.True(
            reservationPort.GetMethod("ReserveNextAsync") is not null,
            "The durable reservation boundary must expose ReserveNextAsync.");

        string program = ReadRepositorySource("src/dotnet/Vulgata.Web/Program.cs");
        Assert.Contains(
            "IPass1NavigationAttemptReservationStore",
            program,
            StringComparison.Ordinal);
    }

    private static string ReadRepositorySource(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        string path = Path.Combine(
            directory!.FullName,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"T109 source seam is missing: {relativePath}");
        return File.ReadAllText(path);
    }
}
