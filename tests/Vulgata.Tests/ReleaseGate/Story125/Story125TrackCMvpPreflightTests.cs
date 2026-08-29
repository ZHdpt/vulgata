namespace Vulgata.Tests.ReleaseGate;

public sealed class Story125TrackCMvpPreflightTests
{
    public static IEnumerable<object?[]> InvalidDatabaseNames()
    {
        yield return [null];
        yield return [string.Empty];
        yield return ["VULGATA_STORY125_01234567"];
        yield return ["vulgata_story125"];
        yield return ["vulgata_story125_bad-name"];
        yield return ["vulgata_story125_短名称"];
        yield return ["postgres"];
        yield return ["template0"];
        yield return ["template1"];
        yield return ["vulgata"];
        yield return ["vulgata_story124_0123456789abcdef0123456789abcdef"];
        yield return ["vulgata_demo_backup_b"];
        yield return ["development"];
        yield return ["story125_0123456789abcdef"];
    }

    [Theory]
    [MemberData(nameof(InvalidDatabaseNames))]
    public void PreconnectionGuard_InvalidTarget_DoesNotInvokeConnectionFactory(string? databaseName)
    {
        int factoryCalls = 0;

        Assert.Throws<InvalidOperationException>(() =>
            Story125ReleaseHarness.CreateOwnedDatabaseConnection<object>(
                databaseName,
                () =>
                {
                    factoryCalls++;
                    return new object();
                }));

        Assert.Equal(0, factoryCalls);
    }

    [Fact]
    public void PreconnectionGuard_GeneratedOwnedName_InvokesConnectionFactoryExactlyOnce()
    {
        string databaseName = Story125ReleaseHarness.CreateRunIdentity().DatabaseName;
        int factoryCalls = 0;
        object expected = new();

        object actual = Story125ReleaseHarness.CreateOwnedDatabaseConnection(
            databaseName,
            () =>
            {
                factoryCalls++;
                return expected;
            });

        Assert.Same(expected, actual);
        Assert.Equal(1, factoryCalls);
    }
}
