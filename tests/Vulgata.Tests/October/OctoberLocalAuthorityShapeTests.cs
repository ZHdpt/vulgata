using System.Collections.Immutable;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;

namespace Vulgata.Tests.October;

public sealed class OctoberLocalAuthorityShapeTests
{
    private const string TargetId = "october-authentication-target";
    private const string AuthorityId = "october-authentication-authority";

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AuthorityConfiguration_OperationTimeoutAboveMaximum_RejectsConstruction()
    {
        const string canonicalDatabaseIdentity = "sqlite://owned/october-authentication-target";
        const string connectionString = "Host=synthetic.invalid;Password=synthetic-only-not-a-credential";

        ArgumentException failure = Assert.ThrowsAny<ArgumentException>(() =>
        {
            _ = new OctoberAuthorityConfiguration(
                AuthorityId,
                TargetId,
                Guid.Parse("862de94c-c28a-4c86-9ac6-4cfca8a69c5c"),
                canonicalDatabaseIdentity,
                connectionString,
                connectionString,
                canonicalDatabaseIdentity,
                ImmutableDictionary<string, string>.Empty.Add(TargetId, canonicalDatabaseIdentity),
                TimeSpan.FromSeconds(31),
                TimeSpan.FromHours(1));
        });

        Assert.DoesNotContain("synthetic-only-not-a-credential", failure.ToString(), StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AuthorityConfiguration_ValidMaximumBounds_RetainsExplicitConnectionsAndOrdinalAllowlist()
    {
        const string canonicalDatabaseIdentity = "sqlite://owned/october-authentication-target";
        const string dataConnection = "Data Source=synthetic-october-data;Password=data-only-test-value";
        const string identityConnection = "Data Source=synthetic-october-identity;Password=identity-only-test-value";
        ImmutableDictionary<string, string> inputAllowlist =
            ImmutableDictionary.Create<string, string>(StringComparer.OrdinalIgnoreCase)
                .Add(TargetId, canonicalDatabaseIdentity)
                .Add("october-secondary-target", "sqlite://owned/october-secondary-target");

        OctoberAuthorityConfiguration configuration = new(
            AuthorityId,
            TargetId,
            Guid.Parse("862de94c-c28a-4c86-9ac6-4cfca8a69c5c"),
            canonicalDatabaseIdentity,
            dataConnection,
            identityConnection,
            "sqlite://owned/october-identity-database",
            inputAllowlist,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromHours(1));

        Assert.Equal(dataConnection, configuration.DataConnectionString);
        Assert.Equal(identityConnection, configuration.IdentityConnectionString);
        Assert.Equal("sqlite://owned/october-identity-database", configuration.IdentityCanonicalDatabaseIdentity);
        Assert.Equal(TimeSpan.FromSeconds(30), configuration.OperationTimeout);
        Assert.Equal(TimeSpan.FromHours(1), configuration.SessionLifetime);
        Assert.True(configuration.AllowedTargets.ContainsKey(TargetId));
        Assert.True(configuration.AllowedTargets.ContainsKey("october-secondary-target"));
        Assert.False(configuration.AllowedTargets.ContainsKey(TargetId.ToUpperInvariant()));
        Assert.DoesNotContain(dataConnection, configuration.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(identityConnection, configuration.ToString(), StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AuthorityConfiguration_NonExactIdentity_RejectsConstruction()
    {
        ArgumentException failure = Assert.ThrowsAny<ArgumentException>(() =>
            CreateAuthorityConfiguration(targetId: $" {TargetId}"));

        Assert.DoesNotContain(TargetId, failure.ToString(), StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("data")]
    [InlineData("identity")]
    public void AuthorityConfiguration_MissingExplicitConnection_RejectsConstruction(string connection)
    {
        ArgumentException failure = Assert.ThrowsAny<ArgumentException>(() =>
            CreateAuthorityConfiguration(
                dataConnectionString: connection == "data" ? string.Empty : "Data Source=identity-test",
                identityConnectionString: connection == "identity" ? string.Empty : "Data Source=data-test"));

        Assert.DoesNotContain("Data Source=", failure.ToString(), StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("timeout")]
    [InlineData("lifetime")]
    public void AuthorityConfiguration_NonPositiveBound_RejectsConstruction(string bound)
    {
        ArgumentException failure = Assert.ThrowsAny<ArgumentException>(() =>
            CreateAuthorityConfiguration(
                operationTimeout: bound == "timeout" ? TimeSpan.Zero : TimeSpan.FromSeconds(30),
                sessionLifetime: bound == "lifetime" ? TimeSpan.Zero : TimeSpan.FromHours(1)));

        Assert.DoesNotContain("synthetic", failure.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AuthorityConfiguration_SessionLifetimeAboveMaximum_RejectsConstruction()
    {
        ArgumentException failure = Assert.ThrowsAny<ArgumentException>(() =>
            CreateAuthorityConfiguration(sessionLifetime: TimeSpan.FromMinutes(61)));

        Assert.DoesNotContain("synthetic", failure.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AuthorityConfiguration_EmptyBatchIdentity_RejectsConstruction()
    {
        ArgumentException failure = Assert.ThrowsAny<ArgumentException>(() =>
            CreateAuthorityConfiguration(batchId: Guid.Empty));

        Assert.DoesNotContain("synthetic", failure.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AuthorityConfiguration_AllowlistWithoutExactSelectedBinding_RejectsConstruction()
    {
        ImmutableDictionary<string, string> allowlist = ImmutableDictionary<string, string>.Empty
            .Add(TargetId.ToUpperInvariant(), "sqlite://owned/october-authentication-target");

        ArgumentException failure = Assert.ThrowsAny<ArgumentException>(() =>
            CreateAuthorityConfiguration(allowedTargets: allowlist));

        Assert.DoesNotContain("sqlite://", failure.ToString(), StringComparison.Ordinal);
    }

    private static OctoberAuthorityConfiguration CreateAuthorityConfiguration(
        string authorityId = AuthorityId,
        string targetId = TargetId,
        Guid? batchId = null,
        string canonicalDatabaseIdentity = "sqlite://owned/october-authentication-target",
        string dataConnectionString = "Data Source=synthetic-october-data",
        string identityConnectionString = "Data Source=synthetic-october-identity",
        string identityCanonicalDatabaseIdentity = "sqlite://owned/october-authentication-target",
        ImmutableDictionary<string, string>? allowedTargets = null,
        TimeSpan? operationTimeout = null,
        TimeSpan? sessionLifetime = null)
    {
        ImmutableDictionary<string, string> selectedTargets = allowedTargets ??
            ImmutableDictionary<string, string>.Empty.Add(targetId, canonicalDatabaseIdentity);

        return new OctoberAuthorityConfiguration(
            authorityId,
            targetId,
            batchId ?? Guid.Parse("862de94c-c28a-4c86-9ac6-4cfca8a69c5c"),
            canonicalDatabaseIdentity,
            dataConnectionString,
            identityConnectionString,
            identityCanonicalDatabaseIdentity,
            selectedTargets,
            operationTimeout ?? TimeSpan.FromSeconds(30),
            sessionLifetime ?? TimeSpan.FromHours(1));
    }
}
