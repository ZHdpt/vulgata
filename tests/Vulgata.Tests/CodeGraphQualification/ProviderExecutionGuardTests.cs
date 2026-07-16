namespace Vulgata.Tests.CodeGraphQualification;

[Trait("Category", "CodeGraphQualificationSynthetic")]
public sealed class ProviderExecutionGuardTests
{
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
        Assert.All(qualificationTestTypes, type => Assert.Contains(
            type.CustomAttributes,
            attribute => attribute.AttributeType == typeof(Xunit.TraitAttribute) &&
                attribute.ConstructorArguments.Count == 2 &&
                Equals(attribute.ConstructorArguments[0].Value, "Category") &&
                Equals(attribute.ConstructorArguments[1].Value, "CodeGraphQualificationSynthetic")));
        Assert.DoesNotContain(qualificationTestTypes, type => type.Name.Contains("ProviderQualification", StringComparison.Ordinal));
    }
}
