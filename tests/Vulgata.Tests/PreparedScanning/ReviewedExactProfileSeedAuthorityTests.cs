using System.Collections;
using System.Reflection;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.CrossRepository;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// Story 12.5 T037 RED seam for the server-owned reviewed exact-profile seed
/// authority. Reflection is used only at this boundary because the Core port,
/// value objects, and Infrastructure adapter are deliberately absent at RED.
/// Once the closed seam exists, these tests exercise its public values without
/// duplicating the embedded profile in production code.
/// </summary>
public sealed class ReviewedExactProfileSeedAuthorityTests
{
    private static readonly Guid AccountRepository =
        Guid.Parse("10000000-0000-4000-8000-000000000001");
    private static readonly Guid ChannelRepository =
        Guid.Parse("10000000-0000-4000-8000-000000000002");

    [Fact]
    public void ExactPersistedNameRefAndRevisionMatch_DerivesCanonicalFourRoleUnion()
    {
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        object result = Evaluate(CreateAuthority(), CreateRequest(profile));

        Assert.Equal("Matched", Property(result, "Outcome")?.ToString());
        IReadOnlyList<object> sources = Items(Property(result, "RequiredSources"));
        Assert.Equal(4, sources.Count);

        string[] expectedRoles =
        [
            "Consumer",
            "ProviderDeclaration",
            "ProviderImplementation",
            "UnsupportedObservationOwner",
        ];
        string[] actualRoles = sources
            .SelectMany(source => Items(Property(source, "Roles")))
            .Select(role => role.ToString()!)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedRoles.OrderBy(value => value, StringComparer.Ordinal), actualRoles);

        string[] bindings = sources
            .Select(source => $"{Property(source, "RepositoryId")}|{Property(source, "RelativePath")}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(4, bindings.Distinct(StringComparer.Ordinal).Count());

        string[] paths = sources
            .Select(source => Property(source, "RelativePath")?.ToString() ??
                throw new InvalidOperationException("A required source path is missing."))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Contains("src/main/java/cn/com/yitong/actions/mobile/UpdateChannelSessionOp.java", paths);
        Assert.Contains("src/main/resources/disgram/in/cust/updateSession.xml", paths);
        Assert.Contains("src/main/java/cn/com/yitong/actions/domain/login/UpdateSessionOp.java", paths);
        Assert.Contains("src/main/java/cn/com/yitong/actions/cust/UploadOcrToOssOp.java", paths);
        Assert.Contains($"{AccountRepository}|src/main/java/cn/com/yitong/actions/mobile/UpdateChannelSessionOp.java", bindings);
        Assert.Contains($"{ChannelRepository}|src/main/resources/disgram/in/cust/updateSession.xml", bindings);
        Assert.Contains($"{ChannelRepository}|src/main/java/cn/com/yitong/actions/domain/login/UpdateSessionOp.java", bindings);
        Assert.Contains($"{AccountRepository}|src/main/java/cn/com/yitong/actions/cust/UploadOcrToOssOp.java", bindings);
    }

    [Fact]
    public void ExactMatch_IsIndependentOfRepositoryRequestOrder()
    {
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        object authority = CreateAuthority();
        object forward = Evaluate(authority, CreateRequest(profile, reverse: false));
        object reverse = Evaluate(authority, CreateRequest(profile, reverse: true));

        Assert.Equal("Matched", Property(forward, "Outcome")?.ToString());
        Assert.Equal(CanonicalSources(forward), CanonicalSources(reverse));
        Assert.Equal(Property(forward, "ProfileFingerprint"), Property(reverse, "ProfileFingerprint"));
    }

    [Fact]
    public void PartialNameRefOrRevisionMatch_IsNonMatchingWithNoRequiredSources()
    {
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        object result = Evaluate(
            CreateAuthority(),
            CreateRequest(profile, mutate: values => values[0] = values[0] with
            {
                StableGitIdentity = values[0].StableGitIdentity + "-wrong",
            }));

        Assert.Equal("NonMatching", Property(result, "Outcome")?.ToString());
        Assert.Empty(Items(Property(result, "RequiredSources")));
        Assert.Null(Property(result, "ProfileId"));
        Assert.Null(Property(result, "ProfileFingerprint"));
    }

    [Fact]
    public void CatalogUnavailable_IsClosedOutcomeRatherThanNonMatchFallback()
    {
        Assembly core = typeof(OrdinaryPass1JobIdentity).Assembly;
        Type? resultType = core.GetType(
            "Vulgata.Core.DomainServices.ReviewedExactProfileSeedAuthorityResult");
        Assert.True(resultType is not null, "The closed reviewed-profile result value is missing.");
        Type? outcomeType = resultType!.GetProperty("Outcome")?.PropertyType;
        Assert.True(outcomeType?.IsEnum == true, "The authority result must use a closed outcome enum.");
        Assert.Contains("Unavailable", Enum.GetNames(outcomeType!));

        Type authorityType = RequireInfrastructureType(
            "Vulgata.Infrastructure.CrossRepository.ReviewedExactProfileSeedAuthority");
        Assert.Contains(
            authorityType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            method => method.Name.Contains("Unavailable", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Evaluate", StringComparison.Ordinal));
    }

    private static object CreateAuthority()
    {
        Type type = RequireInfrastructureType(
            "Vulgata.Infrastructure.CrossRepository.ReviewedExactProfileSeedAuthority");
        ConstructorInfo? parameterless = type.GetConstructor(Type.EmptyTypes);
        if (parameterless is not null)
        {
            return parameterless.Invoke(null);
        }

        ConstructorInfo? catalog = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(value => value.GetParameters() is [{ ParameterType: var parameterType }]
                && parameterType == typeof(ExactResolutionProfileCatalog));
        Assert.True(catalog is not null, "The authority needs a closed construction seam over the validated catalog.");
        return catalog!.Invoke([new ExactResolutionProfileCatalog()]);
    }

    private static object Evaluate(object authority, object request)
    {
        MethodInfo? evaluate = authority.GetType().GetMethod(
            "Evaluate",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.True(evaluate is not null, "IReviewedExactProfileSeedAuthority.Evaluate is missing.");
        return evaluate!.Invoke(authority, [request])!;
    }

    private static object CreateRequest(
        ExactResolutionProfile profile,
        bool reverse = false,
        Action<List<BindingValues>>? mutate = null)
    {
        Type requestType = RequireCoreType("ReviewedExactProfileSeedRequest");
        ConstructorInfo requestConstructor = requestType.GetConstructors().Single();
        ParameterInfo repositoryParameter = requestConstructor.GetParameters().Single();
        Type bindingType = repositoryParameter.ParameterType.GetGenericArguments().Single();
        List<BindingValues> values = profile.Repositories
            .Select((repository, index) => new BindingValues(
                index == 0 ? AccountRepository : ChannelRepository,
                repository.StableGitIdentity,
                repository.ExpectedRef,
                repository.ExpectedCommit))
            .ToList();
        mutate?.Invoke(values);
        if (reverse)
        {
            values.Reverse();
        }

        Array bindings = Array.CreateInstance(bindingType, values.Count);
        for (int index = 0; index < values.Count; index++)
        {
            bindings.SetValue(CreateBinding(bindingType, values[index]), index);
        }

        object[] arguments = requestConstructor.GetParameters()
            .Select(parameter => parameter.ParameterType.IsInstanceOfType(bindings)
                ? bindings
                : throw new InvalidOperationException(
                    $"Unexpected request parameter {parameter.Name}; expected exactly the repository binding collection."))
            .ToArray();
        return requestConstructor.Invoke(arguments);
    }

    private static object CreateBinding(Type bindingType, BindingValues values)
    {
        ConstructorInfo constructor = bindingType.GetConstructors().Single();
        object[] arguments = constructor.GetParameters()
            .Select(parameter => (object)(parameter.Name?.ToLowerInvariant() switch
            {
                "repositoryid" => values.RepositoryId,
                "stablegitidentity" => values.StableGitIdentity,
                "requestedref" => values.RequestedRef,
                "profiledrevision" => values.ProfiledRevision,
                _ => throw new InvalidOperationException($"Unexpected binding parameter {parameter.Name}."),
            }))
            .ToArray();
        return constructor.Invoke(arguments);
    }

    private static string CanonicalSources(object result) => string.Join(
        "\n",
        Items(Property(result, "RequiredSources"))
            .Select(source =>
                $"{Property(source, "RepositoryId")}|{Property(source, "RelativePath")}|" +
                string.Join(',', Items(Property(source, "Roles")).Select(value => value.ToString())))
            .OrderBy(value => value, StringComparer.Ordinal));

    private static object? Property(object value, string name) =>
        value.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(value);

    private static IReadOnlyList<object> Items(object? value) => value is IEnumerable enumerable
        ? enumerable.Cast<object>().ToArray()
        : [];

    private static Type RequireCoreType(string name) =>
        typeof(OrdinaryPass1JobIdentity).Assembly.GetType(
            $"Vulgata.Core.DomainServices.{name}") ??
        throw new InvalidOperationException($"Missing Core type {name}.");

    private static Type RequireInfrastructureType(string name) =>
        typeof(ExactResolutionProfileCatalog).Assembly.GetType(name) ??
        throw new InvalidOperationException($"Missing Infrastructure type {name}.");

    private sealed record BindingValues(
        Guid RepositoryId,
        string StableGitIdentity,
        string RequestedRef,
        string ProfiledRevision);
}
