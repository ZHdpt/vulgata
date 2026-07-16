using System.Reflection;

namespace Vulgata.Tests.CodeGraphQualification;

[Trait("Category", "CodeGraphQualificationSynthetic")]
public sealed class CgcCommandQualificationTests
{
    [Fact]
    public async Task Run_WithFakeLauncher_UsesOneAtomicWholeRepositoryLifecycleAsync()
    {
        FakeQualificationProcessLauncher launcher = new();
        CgcQualificationRunner runner = new(
            QualificationTestPaths.RepositoryRoot,
            TimeSpan.FromSeconds(1_800),
            launcher);

        CgcQualificationExecution result = await runner.RunAsync();

        Assert.True(result.Succeeded, result.FailureSummary);
        CgcQualificationLaunchPlan plan = Assert.IsType<CgcQualificationLaunchPlan>(launcher.ObservedPlan);
        Assert.Equal(runner.CreateContract().LifecycleEntries, plan.LifecycleEntries);
        Assert.Equal(TimeSpan.FromSeconds(1_800), plan.WholeProcessTimeout);
        Assert.Equal("none", plan.NetworkMode);
        Assert.Equal("127.0.0.1", plan.GatewayHost);
        Assert.Empty(plan.PublishedPorts);
        Assert.True(plan.HealthPollingIsBounded);
        Assert.Equal(1, plan.QueryCount);
        Assert.False(plan.UsesMcpOrToolRoute);
        Assert.True(plan.PrivateHomePath.StartsWith("/tmp/vulgata-qualification-", StringComparison.Ordinal));
        Assert.EndsWith("-home", plan.PrivateHomePath, StringComparison.Ordinal);
        Assert.Contains(plan.PrivateContextName, plan.PrivateDatabasePath, StringComparison.Ordinal);
        Assert.True(plan.TerminatesProcessTree);
        Assert.Equal("SIGTERM", plan.GatewayStopSignal);
        Assert.Equal(TimeSpan.FromSeconds(5), plan.GatewayStopGrace);
        Assert.True(plan.UsesConditionalSigKill);

        MethodInfo createScript = typeof(CgcQualificationRunner).GetMethod(
            "CreateQualificationScript",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        string script = (string)createScript.Invoke(null, ["context", "/db", "/home"])!;
        Assert.Contains("kill -TERM", script, StringComparison.Ordinal);
        Assert.Contains("[ \"$gateway_grace_attempt\" -lt 50 ]", script, StringComparison.Ordinal);
        Assert.Contains("sleep 0.1", script, StringComparison.Ordinal);
        Assert.Contains("kill -KILL", script, StringComparison.Ordinal);
        Assert.DoesNotContain("if ! kill -KILL", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_WhenOperationAndCleanupFail_CleanupFailureTakesPrecedenceWithoutPartialStructuralResultAsync()
    {
        FakeQualificationProcessLauncher launcher = new()
        {
            Outcome = new CgcFakeQualificationOutcome(
                OperationSucceeded: false,
                CleanupSucceeded: false,
                FailureSummary: "synthetic operation failure"),
        };
        CgcQualificationRunner runner = new(
            QualificationTestPaths.RepositoryRoot,
            TimeSpan.FromSeconds(1_800),
            launcher);

        CgcQualificationExecution result = await runner.RunAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("Private Context cleanup failed after the qualification operation.", result.FailureSummary);
        Assert.False(result.CleanupSucceeded);
        Assert.False(result.HasBoundedStructuralEvidence);
        Assert.Empty(result.RelationshipKinds);
        Assert.Equal(
            ["operation", "gateway-SIGTERM", "gateway-grace-5s", "gateway-SIGKILL-if-running", "context-delete"],
            launcher.Events);
    }

    [Fact]
    public void CreateContract_WithSyntheticWholeRepository_DeclaresExactApprovedLifecycle()
    {
        CgcQualificationRunner runner = new(
            QualificationTestPaths.RepositoryRoot,
            TimeSpan.FromSeconds(1_800),
            new FakeQualificationProcessLauncher());

        CgcQualificationContract contract = runner.CreateContract();

        Assert.Equal(
            [
                "cgc-version",
                "cgc-doctor",
                "cgc-context-create",
                "cgc-context-mode",
                "cgc-context-default",
                "cgc-index",
                "cgc-api-start",
                "cgc-api-health",
                "cgc-api-query",
                "gateway-stop",
                "cgc-context-delete",
            ],
            contract.LifecycleEntries);
        Assert.Equal(11, contract.LifecycleEntries.Distinct(StringComparer.Ordinal).Count());
    }
}
