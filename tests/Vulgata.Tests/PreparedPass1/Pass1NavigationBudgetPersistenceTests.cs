using System.Reflection;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// Story 12.3 T105 RED seam.  These tests intentionally bind to the public
/// contract by reflection so this test project remains runnable before the
/// reservation port and its durable owner exist.  No provider or database is
/// needed for the contract observations.
/// </summary>
public sealed class Pass1NavigationBudgetPersistenceTests
{
    private const string ReservationPortName =
        "Vulgata.Core.DomainServices.IPass1NavigationAttemptReservationStore";

    [Fact]
    public void ReservationStore_ExposesReserveNextAsyncContract()
    {
        Type? reservationStore = typeof(Pass1NavigationBudget).Assembly.GetType(ReservationPortName);
        Assert.NotNull(reservationStore);

        Assert.True(reservationStore!.IsInterface);
        MethodInfo reserve = Assert.Single(
            reservationStore.GetMethods().Where(method => method.Name == "ReserveNextAsync"));
        Assert.Contains(
            reserve.GetParameters(),
            parameter => parameter.ParameterType == typeof(CancellationToken));
        Assert.True(
            typeof(Task).IsAssignableFrom(reserve.ReturnType) ||
            reserve.ReturnType == typeof(ValueTask) ||
            reserve.ReturnType.IsGenericType &&
            reserve.ReturnType.GetGenericTypeDefinition() == typeof(ValueTask<>));
    }

    [Fact]
    public void ReservationTicket_ExposesOnlyServerCursorAndLeaseAuthorization()
    {
        Type? ticket = typeof(Pass1NavigationBudget).Assembly.GetType(
            "Vulgata.Core.DomainServices.PreparedPass1NavigationReservationTicket");
        Assert.NotNull(ticket);

        PropertyInfo[] properties = ticket!.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.Contains(properties, property => property.Name is "JobId" or "ScanJobExecutionId");
        Assert.Contains(properties, property => property.Name == "PairExecutionId");
        Assert.Contains(properties, property => property.Name is "LeaseToken" or "LeaseId");
        Assert.Contains(properties, property => property.Name is "ToolAttemptOrdinal" or "ReservedOrdinal");
        Assert.Contains(properties, property => property.Name is "ToolAttemptCount" or "AttemptHighWater");
        Assert.Contains(properties, property => property.Name is "StateVersion" or "BudgetStateVersion");
    }

    [Fact]
    public void NavigationBudget_ExposesReservationAndReservedReceiptTransitions()
    {
        MethodInfo[] methods = typeof(Pass1NavigationBudget).GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

        Assert.Contains(methods, method => method.Name == "ReserveNextAttempt");
        Assert.Contains(methods, method => method.Name == "ApplyReservedReceipt");
        Assert.Contains(
            typeof(Pass1NavigationBudget).GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name == nameof(Pass1NavigationBudget.ToolAttemptCount));
        Assert.Contains(
            typeof(Pass1NavigationBudget).GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name == nameof(Pass1NavigationBudget.StateVersion));
    }

    [Fact]
    public void NavigationBudget_ReserveAdvancesOnlyDurableHighWaterAndReturnsLatestCursor()
    {
        MethodInfo? reserve = typeof(Pass1NavigationBudget).GetMethod(
            "ReserveNextAttempt",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(reserve);

        DateTimeOffset now = new(2026, 8, 25, 0, 0, 0, TimeSpan.Zero);
        Pass1NavigationBudget budget = Pass1NavigationBudget.Create(
            Guid.Parse("54ac64ab-d8bb-43be-b5c3-a3a6d3c6b101"),
            Guid.Parse("54ac64ab-d8bb-43be-b5c3-a3a6d3c6b102"),
            updatedAt: now);

        object first = Invoke(
            reserve!,
            budget,
            stateVersion: 0,
            toolAttemptCount: 0,
            ordinal: null,
            accepted: null,
            evidenceBytes: null,
            depth: null,
            now: now);
        Assert.Equal(1, budget.ToolAttemptCount);
        Assert.Equal(0, budget.AcceptedCallCount);
        Assert.Equal(0, budget.EvidenceBytes);
        Assert.Equal(0, budget.MaximumDepthObserved);
        Assert.Equal(1, budget.StateVersion);
        Assert.Equal(1, ReadInt(first, "ToolAttemptOrdinal", "ReservedOrdinal", "Ordinal"));
        Assert.Equal(1, ReadInt(first, "ToolAttemptCount", "AttemptHighWater", "HighWater"));
        Assert.Equal(1L, ReadLong(first, "StateVersion", "BudgetStateVersion", "CursorStateVersion"));

        object second = Invoke(
            reserve!,
            budget,
            ReadLong(first, "StateVersion", "BudgetStateVersion", "CursorStateVersion"),
            ReadInt(first, "ToolAttemptCount", "AttemptHighWater", "HighWater"),
            ordinal: null,
            accepted: null,
            evidenceBytes: null,
            depth: null,
            now: now.AddSeconds(1));
        Assert.Equal(2, budget.ToolAttemptCount);
        Assert.Equal(2, budget.StateVersion);
        Assert.Equal(2, ReadInt(second, "ToolAttemptOrdinal", "ReservedOrdinal", "Ordinal"));

        Assert.Throws<InvalidOperationException>(() => Invoke(
            reserve!, budget, stateVersion: 0, toolAttemptCount: 0,
            ordinal: null, accepted: null, evidenceBytes: null, depth: null,
            now: now.AddSeconds(2)));
        Assert.Throws<InvalidOperationException>(() => Invoke(
            reserve!, budget, stateVersion: budget.StateVersion,
            toolAttemptCount: 9, ordinal: null, accepted: null,
            evidenceBytes: null, depth: null, now: now.AddSeconds(2)));
    }

    [Fact]
    public void NavigationBudget_ReservedReceiptUpdatesEvidenceWithoutIncrementingAttemptHighWater()
    {
        MethodInfo? reserve = typeof(Pass1NavigationBudget).GetMethod(
            "ReserveNextAttempt", BindingFlags.Public | BindingFlags.Instance);
        MethodInfo? receipt = typeof(Pass1NavigationBudget).GetMethod(
            "ApplyReservedReceipt", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(reserve);
        Assert.NotNull(receipt);

        DateTimeOffset now = new(2026, 8, 25, 0, 0, 0, TimeSpan.Zero);
        Pass1NavigationBudget budget = Pass1NavigationBudget.Create(
            Guid.Parse("54ac64ab-d8bb-43be-b5c3-a3a6d3c6b201"),
            Guid.Parse("54ac64ab-d8bb-43be-b5c3-a3a6d3c6b202"),
            updatedAt: now);
        object reservation = Invoke(
            reserve!, budget, 0, 0, null, null, null, null, now);

        object replacement = Invoke(
            receipt!, budget,
            ReadLong(reservation, "StateVersion", "BudgetStateVersion", "CursorStateVersion"),
            ReadInt(reservation, "ToolAttemptOrdinal", "ReservedOrdinal", "Ordinal"),
            ordinal: null,
            accepted: true,
            evidenceBytes: 42,
            depth: 3,
            now: now.AddSeconds(1));
        Assert.Equal(1, budget.ToolAttemptCount);
        Assert.Equal(1, budget.AcceptedCallCount);
        Assert.Equal(42, budget.EvidenceBytes);
        Assert.Equal(3, budget.MaximumDepthObserved);
        Assert.Equal(2, budget.StateVersion);
        Assert.Equal(1, ReadInt(replacement, "ToolAttemptCount", "AttemptHighWater", "HighWater"));
        Assert.Equal(2L, ReadLong(replacement, "StateVersion", "BudgetStateVersion", "CursorStateVersion"));

        Assert.Throws<InvalidOperationException>(() => Invoke(
            receipt!, budget, budget.StateVersion, 2, null, false, 0, 1, now.AddSeconds(2)));
    }

    [Fact]
    public void PreparedPass1Store_ExposesReservationReceiptAndMarkerRepairSeams()
    {
        MethodInfo[] methods = typeof(PreparedPass1Store).GetMethods(
            BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(methods, method => method.Name == "ReserveNavigationAttemptAsync");
        Assert.Contains(methods, method => method.Name == "CommitReservedNavigationReceiptAsync");
        Assert.Contains(methods, method => method.Name == "EnsureNavigationCallsMarkerAsync");
    }

    [Fact]
    public void NavigationReceipt_AllowsAReservedOrdinalWithoutASecondAttemptIncrement()
    {
        Guid jobId = Guid.Parse("4f9b2e51-1f82-46af-bf96-dc7e69f16d1e");
        Guid pairId = Guid.Parse("7d6bbf46-b6fd-4b27-a30b-f5d8772f5788");
        Pass1NavigationReceipt receipt = Pass1NavigationReceipt.Create(
            Guid.Parse("d9a4b0f4-3cb8-4b84-96f4-d8b4b18aa001"),
            jobId,
            pairId,
            receiptOrdinal: 3,
            toolAttemptOrdinal: 3,
            operation: Pass1NavigationOperation.Definition,
            depth: 1,
            outcome: Pass1NavigationOutcomeKind.DocumentEvidence,
            boundKind: null,
            requestFingerprint: new string('a', 64),
            receiptFingerprint: new string('b', 64),
            resultCount: 1,
            evidenceBytes: 10,
            descriptorJson: "{}",
            createdAt: new DateTimeOffset(2026, 8, 25, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(3, receipt.ReceiptOrdinal);
        Assert.Equal(3, receipt.ToolAttemptOrdinal);
    }

    [Fact]
    public void ReservationContract_DoesNotIntroduceANewNavigationEntityOrSchemaSurface()
    {
        Assert.DoesNotContain(
            typeof(VulgataDbContext).Assembly.GetTypes(),
            type => type.Namespace?.Contains(".Migrations", StringComparison.Ordinal) == true &&
                (type.Name.Contains("NavigationAttempt", StringComparison.Ordinal) ||
                 type.Name.Contains("Reservation", StringComparison.Ordinal)));
        Assert.DoesNotContain(
            typeof(VulgataDbContext).GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name.Contains("NavigationAttempt", StringComparison.Ordinal) ||
                property.Name.Contains("Reservation", StringComparison.Ordinal));
    }

    private static object Invoke(
        MethodInfo method,
        object target,
        long stateVersion,
        int toolAttemptCount,
        int? ordinal,
        bool? accepted,
        int? evidenceBytes,
        int? depth,
        DateTimeOffset now)
    {
        ParameterInfo[] parameters = method.GetParameters();
        object?[] arguments = new object?[parameters.Length];
        foreach (ParameterInfo parameter in parameters)
        {
            Type parameterType = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
            string name = parameter.Name ?? string.Empty;
            arguments[parameter.Position] = parameterType switch
            {
                _ when parameterType == typeof(long) => stateVersion,
                _ when parameterType == typeof(int) && name.Contains("ordinal", StringComparison.OrdinalIgnoreCase) => ordinal,
                _ when parameterType == typeof(int) && name.Contains("attempt", StringComparison.OrdinalIgnoreCase) => toolAttemptCount,
                _ when parameterType == typeof(int) && name.Contains("evidence", StringComparison.OrdinalIgnoreCase) => evidenceBytes,
                _ when parameterType == typeof(int) && name.Contains("depth", StringComparison.OrdinalIgnoreCase) => depth,
                _ when parameterType == typeof(bool) => accepted,
                _ when parameterType == typeof(DateTimeOffset) => now,
                _ when parameter.ParameterType == typeof(int?) => ordinal,
                _ => throw new Xunit.Sdk.XunitException(
                    $"Unexpected reservation transition parameter: {parameter.ParameterType} {parameter.Name}"),
            };
        }

        try
        {
            return method.Invoke(target, arguments)
                ?? throw new Xunit.Sdk.XunitException($"{method.Name} returned null.");
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    private static int ReadInt(object value, params string[] names) =>
        Convert.ToInt32(ReadMember(value, names));

    private static long ReadLong(object value, params string[] names) =>
        Convert.ToInt64(ReadMember(value, names));

    private static object ReadMember(object value, string[] names)
    {
        foreach (string name in names)
        {
            PropertyInfo? property = value.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property is not null)
            {
                return property.GetValue(value) ?? throw new Xunit.Sdk.XunitException($"{name} was null.");
            }
        }

        PropertyInfo? cursor = value.GetType().GetProperty(
            "Cursor", BindingFlags.Public | BindingFlags.Instance);
        if (cursor?.GetValue(value) is { } nested)
        {
            return ReadMember(nested, names);
        }

        throw new Xunit.Sdk.XunitException(
            $"{value.GetType().FullName} did not expose any of: {string.Join(", ", names)}.");
    }
}
