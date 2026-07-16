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
                CreateSyntheticPolicy());

            Assert.Null(lease);
            Assert.False(Directory.Exists(Path.Combine(attemptsRoot, "attempt-0002")));
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
    public async Task Execute_WhenAuthorizationMatches_ClaimsBeforeInvokingProviderSeamAsync()
    {
        string attemptsRoot = Path.Combine(Path.GetTempPath(), $"vulgata-provider-claim-{Guid.NewGuid():N}");
        const string token = "synthetic-process-token";
        const string exactCommand =
            "pwsh -NoProfile -File tests/Vulgata.Tests/CodeGraphQualification/Invoke-CodeGraphQualificationProvider.ps1 -AttemptId attempt-0002";
        try
        {
            string attemptDirectory = Path.Combine(attemptsRoot, "attempt-0002");
            Directory.CreateDirectory(attemptDirectory);
            string tokenHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(token)));
            const string approvalHash = "9633e66e1cbe5e31dd9b83fbf962539a42e269bac30e5d28d6bf6d068890b4fd";
            await File.WriteAllTextAsync(
                Path.Combine(attemptDirectory, "authorized.json"),
                $$"""
                {
                  "schemaVersion": "1.0",
                  "attemptId": "attempt-0002",
                  "tokenSha256": "{{tokenHash}}",
                  "expiresAt": "2026-07-16T20:00:00+08:00",
                  "exactCommand": "{{exactCommand}}",
                  "correctivePlanCommit": "84b0909b6713b410258d4db7ec37503cfe0c5980",
                  "planReviewPath": "specs/001-codegraph-extraction/reviews/plan-review-8.json",
                  "planReviewSha256": "f73f877b3c7d37b3c739ee123867e39e3b71769e4abd1d8bbe5bd2f51806b408",
                  "workflowApprovalObservedAt": "2026-07-16T18:10:00+08:00",
                  "workflowApprovalSummarySha256": "{{approvalHash}}"
                }
                """);
            Dictionary<string, string?> environment = new(StringComparer.Ordinal)
            {
                [ProviderQualificationEntry.AttemptIdEnvironmentVariable] = "attempt-0002",
                [ProviderQualificationEntry.TokenEnvironmentVariable] = token,
                [ProviderQualificationEntry.ExactCommandEnvironmentVariable] = exactCommand,
            };
            bool seamInvoked = false;

            bool executed = await ProviderQualificationEntry.ExecuteAsync(
                attemptsRoot,
                environment,
                CreateSyntheticPolicy(),
                async (lease, cancellationToken) =>
                {
                    Assert.True(File.Exists(Path.Combine(attemptDirectory, "started.json")));
                    Assert.Equal("attempt-0002", lease.Authorization.AttemptId);
                    seamInvoked = true;
                    await Task.CompletedTask;
                    return new ProviderQualificationTerminal("passed", "qualified");
                });

            Assert.True(executed);
            Assert.True(seamInvoked);
            Assert.True(File.Exists(Path.Combine(attemptDirectory, "terminal.json")));
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
    public void Issue_WhenNoClaimedAuthorization_RefusesProviderCapability()
    {
        ProviderExecutionLease? lease = ProviderExecutionGuard.Issue(claimedAuthorization: null);

        Assert.Null(lease);
        Assert.False(Directory.Exists(QualificationTestPaths.FromRepositoryRoot(
            "tests", "Vulgata.Tests", "CodeGraphQualification", "Fixtures", "QualificationRepository")));
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

        Assert.Equal([typeof(ProviderExecutionLease)], constructor.GetParameters().Select(parameter => parameter.ParameterType));
    }

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public void Issue_WhenAuthorizationShapeIsInspected_RejectsBroadOrStaticAuthority()
    {
        System.Reflection.MethodInfo issue = Assert.Single(
            typeof(ProviderExecutionGuard).GetMethods(
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
        Type authorizationType = Assert.Single(issue.GetParameters()).ParameterType;

        Assert.NotEqual(typeof(object), authorizationType);
        Assert.NotEqual(typeof(bool), authorizationType);
        Assert.NotEqual(typeof(string), authorizationType);
        Assert.Empty(typeof(ProviderExecutionGuard).GetFields(
            System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic));
    }

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public void Constructor_WhenQualificationRunnerIsInspected_RequiresInjectedLauncher()
    {
        System.Reflection.ConstructorInfo constructor = Assert.Single(
            typeof(CgcQualificationRunner).GetConstructors(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));

        Assert.Contains(
            constructor.GetParameters(),
            parameter => parameter.ParameterType == typeof(IQualificationProcessLauncher));
    }

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public void Discovery_WhenQualificationFactsAreInspected_UsesOnlyExactSyntheticCategory()
    {
        Type[] qualificationTestTypes = typeof(ProviderExecutionGuardTests).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(ProviderExecutionGuardTests).Namespace)
            .Where(type => type.GetMethods().Any(method =>
                method.GetCustomAttributes(inherit: true).Any(attribute =>
                    attribute.GetType().Name is "FactAttribute" or "TheoryAttribute")))
            .ToArray();

        Assert.NotEmpty(qualificationTestTypes);
        Assert.All(qualificationTestTypes.Where(type => type.Name != "CgcProviderQualificationTests"), type => Assert.Contains(
            type.CustomAttributes,
            attribute => attribute.AttributeType == typeof(Xunit.TraitAttribute) &&
                attribute.ConstructorArguments.Count == 2 &&
                Equals(attribute.ConstructorArguments[0].Value, "Category") &&
                Equals(attribute.ConstructorArguments[1].Value, "CodeGraphQualificationSynthetic")));
    }

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public void Discovery_WhenProviderEntryIsInspected_HasOneExactIdentityAndProviderCategory()
    {
        Type? providerType = typeof(ProviderExecutionGuardTests).Assembly.GetType(
            "Vulgata.Tests.CodeGraphQualification.CgcProviderQualificationTests",
            throwOnError: false,
            ignoreCase: false);

        Assert.NotNull(providerType);
        System.Reflection.MethodInfo method = Assert.Single(providerType.GetMethods()
            .Where(candidate => candidate.GetCustomAttributes(inherit: true)
                .Any(attribute => attribute.GetType().Name == "FactAttribute")));
        Assert.Equal("Attempt0002_WhenAuthorizedAndUnclaimed_RecordsSchema12EvidenceAsync", method.Name);
        Assert.Contains(method.CustomAttributes, attribute =>
            attribute.AttributeType == typeof(Xunit.TraitAttribute) &&
            attribute.ConstructorArguments.Count == 2 &&
            Equals(attribute.ConstructorArguments[0].Value, "Category") &&
            Equals(attribute.ConstructorArguments[1].Value, "CodeGraphQualificationProvider"));
        Assert.DoesNotContain(method.CustomAttributes, attribute =>
            attribute.AttributeType == typeof(Xunit.TraitAttribute) &&
            attribute.ConstructorArguments.Count == 2 &&
            Equals(attribute.ConstructorArguments[1].Value, "CodeGraphQualificationSynthetic"));
    }

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task ProviderEntry_WhenSourceIsInspected_WiresClaimedLeaseThroughRunnerWriterAndTerminalAsync()
    {
        string providerSource = await File.ReadAllTextAsync(QualificationTestPaths.FromRepositoryRoot(
            "tests", "Vulgata.Tests", "CodeGraphQualification", "CgcProviderQualificationTests.cs"));
        Assert.Contains("ProviderQualificationEntry.ExecuteAsync", providerSource, StringComparison.Ordinal);
        Assert.Contains("new DockerQualificationProcessLauncher(lease)", providerSource, StringComparison.Ordinal);
        Assert.Contains("new CgcQualificationRunner", providerSource, StringComparison.Ordinal);
        Assert.Contains("QualificationResultWriter.WriteQualifiedAsync", providerSource, StringComparison.Ordinal);
        Assert.Contains("QualificationResultWriter.WriteCorrectiveAsync", providerSource, StringComparison.Ordinal);
        Assert.Contains("new ProviderQualificationTerminal", providerSource, StringComparison.Ordinal);

        string runnerSource = await File.ReadAllTextAsync(QualificationTestPaths.FromRepositoryRoot(
            "tests", "Vulgata.Tests", "CodeGraphQualification", "CodeGraphQualificationRunner.cs"));
        Assert.Contains("QualificationTestPaths.Attempt0002RepositoryPath", runnerSource, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "\"Fixtures\",\r\n            \"QualificationRepository\"",
            runnerSource.ReplaceLineEndings("\r\n"),
            StringComparison.Ordinal);
    }

    private static QualificationAttemptLedgerPolicy CreateSyntheticPolicy() => new(
        "84b0909b6713b410258d4db7ec37503cfe0c5980",
        "specs/001-codegraph-extraction/reviews/plan-review-8.json",
        "f73f877b3c7d37b3c739ee123867e39e3b71769e4abd1d8bbe5bd2f51806b408",
        "2026-07-16T18:10:00+08:00",
        "9633e66e1cbe5e31dd9b83fbf962539a42e269bac30e5d28d6bf6d068890b4fd",
        () => DateTimeOffset.Parse("2026-07-16T19:45:00+08:00", System.Globalization.CultureInfo.InvariantCulture));
}
