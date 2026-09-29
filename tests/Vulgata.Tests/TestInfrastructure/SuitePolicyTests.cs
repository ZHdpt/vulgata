namespace Vulgata.Tests.TestInfrastructure;

public sealed class SuitePolicyTests
{
    [global::Xunit.Trait("Suite", "SelfContained"), global::Vulgata.Tests.TestInfrastructure.SuiteFact("SelfContained")]
    public void AttributeMetadata_ValidatesActualGuardsIncludingDerivedAttributes()
    {
        Assert.Equal("SelfContained", SuiteAuthorization.ValidateMetadata([new ControlledFactAttribute()], ["SelfContained"]));
        Assert.Equal("SelfContained", SuiteAuthorization.ValidateMetadata([new SuiteTheoryAttribute("SelfContained")], ["SelfContained"]));
        Assert.Throws<InvalidOperationException>(() => SuiteAuthorization.ValidateMetadata([new Xunit.FactAttribute()], ["SelfContained"]));
        Assert.Throws<InvalidOperationException>(() => SuiteAuthorization.ValidateMetadata([new ControlledFactAttribute()], ["External"]));
        Assert.Throws<InvalidOperationException>(() => SuiteAuthorization.ValidateMetadata([new ControlledFactAttribute()], []));
    }

    private sealed class ControlledFactAttribute : SuiteFactAttribute
    {
        public ControlledFactAttribute() : base("SelfContained") { }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void OwnedTarget_RequiresRegistryMembershipEvenWithIdenticalValidEndpoints()
    {
        const string admin = "Host=127.0.0.1;Port=32123;Database=postgres;Username=postgres;Password=generated";
        string recorded = admin.Replace("Database=postgres", "Database=vulgata_recorded", StringComparison.Ordinal);
        string unrecorded = admin.Replace("Database=postgres", "Database=vulgata_other", StringComparison.Ordinal);
        TestResourceScope.ValidateOwnedTarget(recorded, "PostgreSql", new string('a',32), admin, ["vulgata_recorded"]);
        Assert.Throws<InvalidOperationException>(() => TestResourceScope.ValidateOwnedTarget(unrecorded, "PostgreSql", new string('a',32), admin, ["vulgata_recorded"]));
        Assert.Throws<InvalidOperationException>(() => TestResourceScope.ValidateOwnedTarget(recorded, "PostgreSql", new string('a',32), admin, []));
    }
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void DefaultSuite_IsEnabledWithoutExternalAuthorization() => Assert.True(SuiteAuthorization.IsEnabled("SelfContained"));

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AdministratorScope_RejectsAmbientDatabaseBeforeConnection() =>
        Assert.Throws<InvalidOperationException>(() => TestResourceScope.ValidateAdministrator(
            "Host=example.invalid;Database=shared;Username=application;Password=unused"));

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("Host=remote.invalid;Port=32123;Database=postgres;Username=postgres;Password=generated")]
    [InlineData("Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=generated")]
    [InlineData("Host=127.0.0.1;Port=32123;Database=shared;Username=postgres;Password=generated")]
    [InlineData("Host=127.0.0.1;Port=32123;Database=postgres;Username=postgres;Password=ambient")]
    public void AdministratorScope_RejectsDifferentEndpointDatabaseOrCredential(string supplied) =>
        Assert.Throws<InvalidOperationException>(() => TestResourceScope.ValidateAdministrator(supplied,
            "PostgreSql", new string('a', 32),
            "Host=127.0.0.1;Port=32123;Database=postgres;Username=postgres;Password=generated"));

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AdministratorScope_AcceptsExactOwnedEndpointWithoutConnecting()
    {
        const string owned = "Host=127.0.0.1;Port=32123;Database=postgres;Username=postgres;Password=generated";
        TestResourceScope.ValidateAdministrator(owned, "PostgreSql", new string('a', 32), owned);
        Assert.Throws<InvalidOperationException>(() => TestResourceScope.ValidateAdministrator(owned, "SelfContained", new string('a', 32), owned));
        Assert.Throws<InvalidOperationException>(() => TestResourceScope.ValidateAdministrator(owned, "PostgreSql", null, owned));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void DisabledAttribute_RetainsAuthorizationEvenWhenDerivedSkipIsCleared()
    {
        var fact = new SuiteFactAttribute("NeverAuthorized") { Skip = null };
        Assert.Contains("requires explicit", fact.Skip);
        var theory = new SuiteTheoryAttribute("NeverAuthorized") { Skip = null };
        Assert.Contains("requires explicit", theory.Skip);
    }
}
