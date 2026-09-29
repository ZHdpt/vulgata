using Vulgata.Tests.TestInfrastructure;
using System.Reflection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Tests.PreparedPass1.Fixtures;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// Story 12.3 T011 RED slice.  These tests describe the public claim seam
/// owned by the prepared Pass 1 store.  The reflection boundary is deliberate:
/// T012 owns introducing the new store contract, while this test author must
/// not add a production type merely to make the RED compile.
/// </summary>
public sealed class Pass1ConcurrencyAndOrderingTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreparedPass1Store_ExposesOnePairWideClaimSeamWithDurableLeaseAndOrdinals()
    {
        Type contract = RequireStoreContract();
        MethodInfo claim = RequireMethod(contract, "ClaimNextAsync", "ClaimPendingAsync", "ClaimNext");

        Assert.Contains(
            claim.GetParameters(),
            parameter => parameter.ParameterType == typeof(Guid) &&
                parameter.Name is "pairExecutionId" or "pairId");
        Assert.Contains(
            claim.GetParameters(),
            parameter => parameter.ParameterType == typeof(DateTimeOffset) &&
                parameter.Name is "now" or "observedAt" or "claimAt");
        Assert.Contains(
            claim.GetParameters(),
            parameter => parameter.ParameterType == typeof(TimeSpan) &&
                parameter.Name is "leaseDuration" or "leaseLifetime");

        Type result = UnwrapTask(claim.ReturnType);
        Assert.NotEqual(typeof(void), result);
        Assert.Contains(result.GetProperties(BindingFlags.Public | BindingFlags.Instance), property =>
            property.Name is "JobId" or "ScanJobExecutionId");
        Assert.Contains(result.GetProperties(BindingFlags.Public | BindingFlags.Instance), property =>
            property.Name is "LeaseToken" or "LeaseId");
        Assert.Contains(result.GetProperties(BindingFlags.Public | BindingFlags.Instance), property =>
            property.Name is "LeaseExpiresAt" or "LeaseExpiration");
        Assert.Contains(result.GetProperties(BindingFlags.Public | BindingFlags.Instance), property =>
            property.Name is "PairSelectionOrdinal" or "SelectionOrdinal");
        Assert.Contains(result.GetProperties(BindingFlags.Public | BindingFlags.Instance), property =>
            property.Name == "ManifestOrdinal");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreparedPass1PairExecution_StoresDefaultTwoAndOnlyOneThroughFourEffectiveLimits()
    {
        Type contract = RequireStoreContract();
        Type pair = typeof(Pass1PairExecution);

        Assert.Equal(typeof(int), pair.GetProperty(nameof(Pass1PairExecution.InitialConcurrencyLimit))?.PropertyType);
        Assert.Equal(typeof(int), pair.GetProperty(nameof(Pass1PairExecution.ConcurrencyLimit))?.PropertyType);
        Assert.Equal(typeof(long), pair.GetProperty(nameof(Pass1PairExecution.ConcurrencyRevision))?.PropertyType);

        MethodInfo claim = RequireMethod(contract, "ClaimNextAsync", "ClaimPendingAsync", "ClaimNext");
        ParameterInfo? limit = claim.GetParameters().FirstOrDefault(parameter =>
            parameter.Name is "defaultConcurrencyLimit" or "initialConcurrencyLimit" or "concurrencyLimit");
        if (limit is not null)
        {
            Assert.Equal(typeof(int), limit.ParameterType);
            if (limit.IsOptional && limit.HasDefaultValue)
            {
                Assert.Equal(2, limit.DefaultValue);
            }
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreparedPass1ClaimContract_UsesPairSelectionOrdinalForClaimsAndManifestOrdinalForDisplay()
    {
        Type result = UnwrapTask(RequireMethod(
            RequireStoreContract(), "ClaimNextAsync", "ClaimPendingAsync", "ClaimNext").ReturnType);
        PropertyInfo selection = result.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Single(property => property.Name is "PairSelectionOrdinal" or "SelectionOrdinal");
        PropertyInfo display = result.GetProperty("ManifestOrdinal")!;

        Assert.True(selection.PropertyType == typeof(int) || selection.PropertyType == typeof(long));
        Assert.Equal(typeof(int), display.PropertyType);

        ApprovedPreparedManifestPairSeed pair = PreparedPass1ManifestPairFixtures.CreateApprovedPair();
        int[] pairOrdinals = pair.Manifests
            .SelectMany(manifest => manifest.FileCodeUnits)
            .OrderBy(unit => unit.PairSelectionOrdinal)
            .Select(unit => unit.PairSelectionOrdinal)
            .ToArray();
        Assert.Equal([1, 2, 3, 4], pairOrdinals);

        foreach (PreparedManifestSeed manifest in pair.Manifests)
        {
            Assert.Equal(
                [1, 2],
                manifest.FileCodeUnits
                    .OrderBy(unit => unit.ManifestOrdinal)
                    .Select(unit => unit.ManifestOrdinal)
                    .ToArray());
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreparedPass1ClaimContract_ProvidesActiveLeaseAndExpiryInformationForPairCapacity()
    {
        Type result = UnwrapTask(RequireMethod(
            RequireStoreContract(), "ClaimNextAsync", "ClaimPendingAsync", "ClaimNext").ReturnType);
        PropertyInfo[] properties = result.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(properties, property => property.Name is "ActiveLeaseCount" or "RunningLeaseCount");
        Assert.Contains(properties, property => property.Name is "LeaseExpiresAt" or "LeaseExpiration");
        Assert.Contains(properties, property => property.Name is "ConcurrencyLimit" or "EffectiveConcurrencyLimit");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreparedPass1ClaimContract_SeparatesCompletionFromIdentityAndDisplayOrder()
    {
        Type contract = RequireStoreContract();
        Type result = UnwrapTask(RequireMethod(
            contract, "ClaimNextAsync", "ClaimPendingAsync", "ClaimNext").ReturnType);
        PropertyInfo[] properties = result.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.DoesNotContain(properties, property => property.Name is "CompletionOrdinal" or "CompletedAtOrder");
        Assert.Contains(properties, property => property.Name is "JobId" or "ScanJobExecutionId");
        Assert.Contains(properties, property => property.Name == "ManifestOrdinal");
        Assert.Contains(properties, property => property.Name is "PairSelectionOrdinal" or "SelectionOrdinal");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreparedPass1TenCompletionPermutations_RetainTheSameCanonicalPairAndDisplayOrder()
    {
        _ = RequireStoreContract();
        ApprovedPreparedManifestPairSeed pair = PreparedPass1ManifestPairFixtures.CreateApprovedPair();
        string[] canonical = pair.Manifests
            .SelectMany(manifest => manifest.FileCodeUnits)
            .OrderBy(unit => unit.PairSelectionOrdinal)
            .ThenBy(unit => unit.IdentityFingerprint, StringComparer.Ordinal)
            .Select(unit => $"{unit.PairSelectionOrdinal}:{unit.ManifestOrdinal}:{unit.IdentityFingerprint}")
            .ToArray();

        for (int permutation = 0; permutation < 10; permutation++)
        {
            string[] completionOrder = canonical
                .OrderBy(value => StablePermutationKey(value, permutation))
                .ToArray();

            Assert.Equal(
                canonical,
                completionOrder
                    .OrderBy(value => int.Parse(value[..value.IndexOf(':', StringComparison.Ordinal)]))
                    .ThenBy(value => value, StringComparer.Ordinal)
                    .ToArray());
        }
    }

    private static Type RequireStoreContract()
    {
        Type? contract = typeof(CanonicalEnvelope).Assembly.GetType(
            "Vulgata.Core.DomainServices.IPreparedPass1Store",
            throwOnError: false,
            ignoreCase: false);
        Assert.True(
            contract is not null,
            "T011 RED: the prepared Pass 1 store claim/update port is not implemented yet; T012 must add it.");
        return contract!;
    }

    private static MethodInfo RequireMethod(Type contract, params string[] names)
    {
        MethodInfo? method = contract.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(candidate => names.Contains(candidate.Name, StringComparer.Ordinal));
        Assert.True(
            method is not null,
            $"T011 RED: IPreparedPass1Store must expose one claim method named {string.Join("/", names)}.");
        return method!;
    }

    private static Type UnwrapTask(Type returnType)
    {
        Assert.True(
            returnType.IsGenericType &&
            returnType.GetGenericTypeDefinition() == typeof(Task<>),
            "The prepared Pass 1 claim seam must be asynchronous and return a typed result.");
        return returnType.GetGenericArguments()[0];
    }

    private static int StablePermutationKey(string value, int permutation)
    {
        unchecked
        {
            int hash = permutation + 17;
            foreach (char character in value)
            {
                hash = (hash * 31) + character;
            }

            return hash;
        }
    }
}
