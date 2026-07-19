namespace Vulgata.Tests.CodeGraphQualification;

[Trait("Category", "CodeGraphQualificationSynthetic")]
public sealed class ProviderExecutionGuardTests
{
    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Acquire_WhenWrapperEnvironmentIsAbsent_RemainsInertAsync()
    {
        string attemptsRoot = Path.Combine(Path.GetTempPath(), $"vulgata-provider-inert-{Guid.NewGuid():N}");
        try
        {
            ProviderExecutionLease? lease = await ProviderQualificationEntry.TryAcquireAsync(
                attemptsRoot,
                new Dictionary<string, string?>(StringComparer.Ordinal),
                () => DateTimeOffset.UtcNow);

            Assert.Null(lease);
            Assert.False(Directory.Exists(Path.Combine(attemptsRoot, "attempt-0003")));
        }
        finally
        {
            if (Directory.Exists(attemptsRoot))
            {
                Directory.Delete(attemptsRoot, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Execute_WhenRawBoundaryAndAuthorizationMatch_ClaimsBeforeProviderSeamAsync()
    {
        using QualificationAttemptLedgerTests.TemporaryQualificationRoot temporary = new();
        const string token = "synthetic-process-token";
        DateTimeOffset now = new(2026, 7, 18, 12, 0, 0, TimeSpan.FromHours(8));
        QualificationExecutionBoundary boundary = await temporary.WriteBoundaryAsync();
        await temporary.WriteAuthorizationAsync(boundary, token, now.AddMinutes(5));
        Dictionary<string, string?> environment = temporary.CreateEnvironment(boundary, token);
        bool seamInvoked = false;

        bool executed = await ProviderQualificationEntry.ExecuteAsync(
            temporary.AttemptsRoot,
            environment,
            () => now,
            async (lease, cancellationToken) =>
            {
                Assert.True(File.Exists(Path.Combine(temporary.AttemptDirectory, "started.json")));
                Assert.Equal("attempt-0003", lease.Authorization.AttemptId);
                seamInvoked = true;
                await Task.CompletedTask;
                return new ProviderQualificationTerminal("passed", "qualified");
            });

        Assert.True(executed);
        Assert.True(seamInvoked);
        Assert.True(File.Exists(Path.Combine(temporary.AttemptDirectory, "terminal.json")));
    }

    [Theory]
    [InlineData(ProviderQualificationEntry.AttemptIdEnvironmentVariable, "attempt-0002")]
    [InlineData(ProviderQualificationEntry.ExactCommandEnvironmentVariable, "dotnet test broad-filter")]
    [InlineData(
        ProviderQualificationEntry.BoundarySha256EnvironmentVariable,
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Acquire_WhenWrapperEnvironmentDrifts_RejectsBeforeStartAsync(
        string variable,
        string value)
    {
        using QualificationAttemptLedgerTests.TemporaryQualificationRoot temporary = new();
        const string token = "synthetic-environment-drift-token";
        DateTimeOffset now = new(2026, 7, 18, 12, 0, 0, TimeSpan.FromHours(8));
        QualificationExecutionBoundary boundary = await temporary.WriteBoundaryAsync();
        await temporary.WriteAuthorizationAsync(boundary, token, now.AddMinutes(5));
        Dictionary<string, string?> environment = temporary.CreateEnvironment(boundary, token);
        environment[variable] = value;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ProviderQualificationEntry.TryAcquireAsync(
                temporary.AttemptsRoot,
                environment,
                () => now));

        Assert.False(File.Exists(Path.Combine(temporary.AttemptDirectory, "started.json")));
    }

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public void Issue_WhenNoClaimedAuthorization_RefusesProviderCapability()
    {
        Assert.Null(ProviderExecutionGuard.Issue(claimedAuthorization: null));
    }

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public void Create_WhenSyntheticLauncherReceivesProviderCommand_RefusesExecution()
    {
        FakeQualificationProcessLauncher launcher = new();
        System.Diagnostics.ProcessStartInfo startInfo = new("docker");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            (Action)(() => launcher.CreateProcess(startInfo)));

        Assert.Equal("Synthetic qualification launchers cannot create provider processes.", exception.Message);
        Assert.Equal(0, launcher.CreatedProcessCount);
    }

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public void Constructor_WhenRealLauncherIsInspected_RequiresOnlySealedLease()
    {
        Assert.True(typeof(ProviderExecutionLease).IsSealed);
        Assert.Empty(typeof(ProviderExecutionLease).GetConstructors());
        System.Reflection.ConstructorInfo constructor = Assert.Single(
            typeof(DockerQualificationProcessLauncher).GetConstructors(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));
        Assert.Equal([typeof(ProviderExecutionLease)], constructor.GetParameters().Select(item => item.ParameterType));
    }

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public void Discovery_WhenProviderEntryIsInspected_HasAttempt0003IdentityAndProviderCategory()
    {
        Type providerType = typeof(CgcProviderQualificationTests);
        System.Reflection.MethodInfo method = Assert.Single(providerType.GetMethods()
            .Where(candidate => candidate.GetCustomAttributes(inherit: true)
                .Any(attribute => attribute.GetType().Name == "FactAttribute")));
        Assert.Equal(
            "Attempt0003_WhenAuthorizedAndUnclaimed_RecordsSchema12EvidenceAsync",
            method.Name);
        Assert.Contains(method.CustomAttributes, attribute =>
            attribute.AttributeType == typeof(Xunit.TraitAttribute) &&
            attribute.ConstructorArguments.Count == 2 &&
            Equals(attribute.ConstructorArguments[0].Value, "Category") &&
            Equals(attribute.ConstructorArguments[1].Value, "CodeGraphQualificationProvider"));
    }

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Source_WhenGuardAndWrapperAreInspected_HasNoApprovalSummaryLiteralAsync()
    {
        string guardSource = await File.ReadAllTextAsync(QualificationTestPaths.FromRepositoryRoot(
            "tests", "Vulgata.Tests", "CodeGraphQualification", "ProviderExecutionGuard.cs"));
        string wrapperSource = await File.ReadAllTextAsync(QualificationTestPaths.FromRepositoryRoot(
            "tests", "Vulgata.Tests", "CodeGraphQualification", "Invoke-CodeGraphQualificationProvider.ps1"));

        Assert.DoesNotContain("approvalSummary", guardSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("approvalSummary", wrapperSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VULGATA_QUALIFICATION_BOUNDARY_SHA256", wrapperSource, StringComparison.Ordinal);
    }
}
