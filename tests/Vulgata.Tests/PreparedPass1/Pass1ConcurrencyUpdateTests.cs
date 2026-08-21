using System.Reflection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// Story 12.3 T011 RED slice for versioned active-concurrency changes.  The
/// store contract is intentionally discovered at runtime so the test author
/// does not create or modify T012 production seams.
/// </summary>
public sealed class Pass1ConcurrencyUpdateTests
{
    [Fact]
    public void PreparedPass1Store_ExposesVersionedAuthorizedConcurrencyUpdate()
    {
        Type contract = RequireStoreContract();
        MethodInfo update = RequireUpdateMethod(contract);
        ParameterInfo[] parameters = update.GetParameters();

        Assert.Contains(parameters, parameter =>
            parameter.ParameterType == typeof(Guid) &&
            parameter.Name is "pairExecutionId" or "pairId");
        Assert.Contains(parameters, parameter =>
            parameter.ParameterType == typeof(int) &&
            parameter.Name is "requestedLimit" or "effectiveLimit" or "newLimit");
        Assert.Contains(parameters, parameter =>
            parameter.ParameterType == typeof(long) &&
            parameter.Name is "expectedStateVersion" or "expectedVersion");
        Assert.Contains(parameters, parameter =>
            parameter.ParameterType == typeof(string) &&
            parameter.Name is "principalId" or "authorizedPrincipalId" or "changedByPrincipalId");

        Type result = UnwrapTask(update.ReturnType);
        PropertyInfo[] properties = result.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.Contains(properties, property => property.Name is "Outcome" or "Result");
        Assert.Contains(properties, property => property.Name is "ConcurrencyLimit" or "EffectiveLimit");
        Assert.Contains(properties, property => property.Name == "StateVersion");
    }

    [Fact]
    public void PreparedPass1ConcurrencyUpdate_UsesImmutableAuditFieldsAndCanonicalRevision()
    {
        _ = RequireUpdateMethod(RequireStoreContract());
        Type change = typeof(Pass1ConcurrencyChange);

        Assert.Equal(typeof(long), change.GetProperty(nameof(Pass1ConcurrencyChange.ExpectedStateVersion))?.PropertyType);
        Assert.Equal(typeof(long), change.GetProperty(nameof(Pass1ConcurrencyChange.ResultingStateVersion))?.PropertyType);
        Assert.Equal(typeof(long), change.GetProperty(nameof(Pass1ConcurrencyChange.ConcurrencyRevision))?.PropertyType);
        Assert.Equal(typeof(int), change.GetProperty(nameof(Pass1ConcurrencyChange.PreviousLimit))?.PropertyType);
        Assert.Equal(typeof(int), change.GetProperty(nameof(Pass1ConcurrencyChange.EffectiveLimit))?.PropertyType);
        Assert.Equal(typeof(string), change.GetProperty(nameof(Pass1ConcurrencyChange.ChangedByPrincipalId))?.PropertyType);
        Assert.Equal(typeof(string), change.GetProperty(nameof(Pass1ConcurrencyChange.ChangeFingerprint))?.PropertyType);

        MethodInfo? codec = typeof(PreparedPass1CanonicalCodec).GetMethod(
            "CreateConcurrencyChange",
            BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(codec);
    }

    [Fact]
    public void PreparedPass1ConcurrencyUpdate_DeclaresExactReplayAndStaleWriterOutcomes()
    {
        Type result = UnwrapTask(RequireUpdateMethod(RequireStoreContract()).ReturnType);
        PropertyInfo outcome = result.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Single(property => property.Name is "Outcome" or "Result");

        Type? outcomeType = Nullable.GetUnderlyingType(outcome.PropertyType) ?? outcome.PropertyType;
        Assert.True(outcomeType.IsEnum, "Concurrency updates must return a closed outcome, not an exception-only result.");
        string[] names = Enum.GetNames(outcomeType);
        Assert.Contains(names, name => name is "Replay" or "Replayed");
        Assert.Contains(names, name => name is "Conflict" or "Stale");
        Assert.Contains(names, name => name is "Updated" or "Changed" or "Committed");
    }

    [Fact]
    public void PreparedPass1ConcurrencyUpdate_SeparatesInitialLimitFromEffectiveLimitAndPreservesLeases()
    {
        Type pair = typeof(Pass1PairExecution);
        _ = RequireUpdateMethod(RequireStoreContract());

        Assert.Contains(
            pair.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name == nameof(Pass1PairExecution.InitialConcurrencyLimit));
        Assert.Contains(
            pair.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name == nameof(Pass1PairExecution.ConcurrencyLimit));

        Type result = UnwrapTask(RequireUpdateMethod(RequireStoreContract()).ReturnType);
        PropertyInfo[] resultProperties = result.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.Contains(resultProperties, property => property.Name is "PreservedLeaseCount" or "ActiveLeaseCount");
        Assert.Contains(resultProperties, property => property.Name is "ConcurrencyRevision" or "ResultingConcurrencyRevision");
    }

    [Fact]
    public void PreparedPass1ConcurrencyUpdate_AllowsOnlyOneThroughFourAsTheRequestedDomain()
    {
        _ = RequireUpdateMethod(RequireStoreContract());
        Type pair = typeof(Pass1PairExecution);
        Assert.Equal(typeof(int), pair.GetProperty(nameof(Pass1PairExecution.InitialConcurrencyLimit))?.PropertyType);
        Assert.Equal(typeof(int), pair.GetProperty(nameof(Pass1PairExecution.ConcurrencyLimit))?.PropertyType);

        MethodInfo codec = typeof(PreparedPass1CanonicalCodec).GetMethod(
            "CreateConcurrencyChange",
            BindingFlags.Public | BindingFlags.Static)!;
        object input = new PreparedPass1ConcurrencyChangeInput(
            Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee"),
            4,
            2,
            3,
            5,
            1,
            "owner@example");
        Assert.NotNull(codec.Invoke(null, [input]));
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

    private static MethodInfo RequireUpdateMethod(Type contract)
    {
        MethodInfo? method = contract.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(candidate => candidate.Name is "UpdateConcurrencyAsync" or "ChangeConcurrencyAsync");
        Assert.True(
            method is not null,
            "T011 RED: IPreparedPass1Store must expose a versioned UpdateConcurrencyAsync seam.");
        return method!;
    }

    private static Type UnwrapTask(Type returnType)
    {
        Assert.True(
            returnType.IsGenericType &&
            returnType.GetGenericTypeDefinition() == typeof(Task<>),
            "The prepared Pass 1 concurrency update seam must be asynchronous and typed.");
        return returnType.GetGenericArguments()[0];
    }
}
