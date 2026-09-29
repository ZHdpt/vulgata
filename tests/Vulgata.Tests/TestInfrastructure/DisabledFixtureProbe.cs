namespace Vulgata.Tests.TestInfrastructure;

// This selected-only sentinel deliberately fails if the executor initializes its fixture.
// Enable it only in the guard regression script, never during external qualification.
public sealed class DisabledFixtureProbe : IClassFixture<DisabledFixtureProbe.Fixture>
{
    public sealed class Fixture
    {
        public Fixture()
        {
            if (Environment.GetEnvironmentVariable("VULGATA_TEST_GUARD_PROBE") == "1")
                throw new InvalidOperationException("Disabled external fixture was initialized.");
        }
    }

    [Trait("Suite", "External")]
    [SuiteFact("External")]
    public void DisabledExternalFixture_MustNotInitialize() => Assert.True(SuiteAuthorization.IsEnabled("External"));
}
