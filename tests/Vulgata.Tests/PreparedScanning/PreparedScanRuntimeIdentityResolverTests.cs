using Vulgata.Tests.TestInfrastructure;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulgata.Core.DomainServices;
using Vulgata.Tests;
using Vulgata.Web.Data;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// T023 RED slice for the internal configured runtime-identity authority.
/// The resolver is intentionally discovered by reflection because it is an
/// internal Web composition seam; these tests must not make that seam public.
/// </summary>
public sealed class PreparedScanRuntimeIdentityResolverTests
    : IClassFixture<LoginLogoutTests.CustomWebApplicationFactory>
{
    private static readonly Guid RepositoryId =
        Guid.Parse("12000000-0000-4000-8000-000000000023");

    private static readonly Guid ScanRunId =
        Guid.Parse("12000000-0000-4000-8000-000000000024");

    private readonly LoginLogoutTests.CustomWebApplicationFactory _factory;

    public PreparedScanRuntimeIdentityResolverTests(
        LoginLogoutTests.CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ProductionComposition_RegistersOneInternalConfiguredResolver()
    {
        Type contract = FindWebType("IPreparedScanRuntimeIdentityResolver");

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        object? resolver = scope.ServiceProvider.GetService(contract);
        Assert.NotNull(resolver);
        Assert.Equal(
            "ConfiguredPreparedScanRuntimeIdentityResolver",
            resolver.GetType().Name);
        Assert.Single(scope.ServiceProvider.GetServices(contract));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ResolverContract_IsInternalAndBindsRepositoryAndProfiledRevision()
    {
        Type contract = FindWebType("IPreparedScanRuntimeIdentityResolver");

        Assert.True(
            contract.IsNotPublic,
            "The runtime resolver is an internal Web composition seam, not a public API.");

        MethodInfo resolve = Assert.Single(
            contract.GetMethods(BindingFlags.Instance | BindingFlags.Public));
        Assert.Contains("Resolve", resolve.Name, StringComparison.Ordinal);
        Assert.Contains(
            resolve.GetParameters(),
            parameter =>
                parameter.ParameterType == typeof(Guid) ||
                parameter.Name?.Contains("scope", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(
            resolve.GetParameters(),
            parameter =>
                parameter.Name?.Contains("revision", StringComparison.OrdinalIgnoreCase) == true ||
                parameter.ParameterType == typeof(AnalysisScope));
        Assert.True(
            resolve.ReturnType == typeof(AnalysisRuntimeIdentity) ||
            resolve.ReturnType.Name.Contains("Task", StringComparison.Ordinal),
            "The resolver must return the checked canonical runtime identity, not a caller DTO.");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Resolver_ProducesCanonicalIdentityPerRepositoryAndSharesHealthAuthority()
    {
        Type contract = FindWebType("IPreparedScanRuntimeIdentityResolver");
        AnalysisRuntimeIdentity expectedIdentity = CreateIdentity(
            RepositoryId,
            "0123456789abcdef0123456789abcdef01234567");
        RuntimeResolverProxy resolverProxy = CreateResolverProxy(contract, expectedIdentity);
        using WebApplicationFactory<Program> factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll(contract);
                services.AddSingleton(contract, resolverProxy);
            }));

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        resolverProxy.CallCount = 0;
        IAnalysisRuntimeHealthProbe probe =
            scope.ServiceProvider.GetRequiredService<IAnalysisRuntimeHealthProbe>();
        AnalysisHealthProbeResult health = await probe.CheckConfiguredIntegrityAsync(
            CreateScope(),
            CancellationToken.None);

        Assert.True(resolverProxy.CallCount > 0, "Configured health must delegate to the shared resolver.");
        Assert.Equal(AnalysisRuntimeHealthStatus.Ready, health.Status);
        Assert.Equal(expectedIdentity.Sha256, health.RuntimeIdentityHash);

        using JsonDocument identityJson = JsonDocument.Parse(expectedIdentity.CanonicalJson);
        Assert.Equal(
            RepositoryId.ToString(),
            identityJson.RootElement.GetProperty("workspace").GetProperty("repositoryId").GetString());
        Assert.Equal(
            expectedIdentity.ProfiledRevision,
            identityJson.RootElement.GetProperty("workspace").GetProperty("profiledRevision").GetString());
        Assert.Equal(
            AnalysisRuntimeIdentity.ComputeSha256(expectedIdentity.CanonicalJson),
            expectedIdentity.Sha256);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ConfiguredResolver_DefaultWithoutToolFiles_ReturnsNotReady()
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        IPreparedScanRuntimeIdentityResolver resolver =
            scope.ServiceProvider.GetRequiredService<IPreparedScanRuntimeIdentityResolver>();

        PreparedScanRuntimeIdentityResolution first = await resolver.ResolveAsync(
            RepositoryId,
            "0123456789abcdef0123456789abcdef01234567",
            CancellationToken.None);
        PreparedScanRuntimeIdentityResolution second = await resolver.ResolveAsync(
            Guid.Parse("12000000-0000-4000-8000-000000000026"),
            "fedcba9876543210fedcba9876543210fedcba98",
            CancellationToken.None);

        Assert.Equal(AnalysisRuntimeHealthStatus.NotReady, first.Health.Status);
        Assert.Equal(AnalysisRuntimeHealthStatus.NotReady, second.Health.Status);
        Assert.Null(first.Identity);
        Assert.Null(second.Identity);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AnalysisRuntimeIdentity_CanonicalConstruction_BindsRepositoryAndRevision()
    {
        const string firstRevision = "0123456789abcdef0123456789abcdef01234567";
        const string secondRevision = "fedcba9876543210fedcba9876543210fedcba98";
        Guid firstRepository = Guid.Parse("12000000-0000-4000-8000-000000000025");
        Guid secondRepository = Guid.Parse("12000000-0000-4000-8000-000000000026");

        AnalysisRuntimeIdentity first = CreateIdentity(firstRepository, firstRevision);
        AnalysisRuntimeIdentity second = CreateIdentity(secondRepository, secondRevision);

        Assert.Equal("analysis-runtime/1", first.SchemaIdentity);
        Assert.Equal(firstRepository, first.RepositoryId);
        Assert.Equal(firstRevision, first.ProfiledRevision);
        Assert.Equal(AnalysisRuntimeIdentity.ComputeSha256(first.CanonicalJson), first.Sha256);
        Assert.Equal("analysis-runtime/1", second.SchemaIdentity);
        Assert.Equal(secondRepository, second.RepositoryId);
        Assert.Equal(secondRevision, second.ProfiledRevision);
        Assert.Equal(AnalysisRuntimeIdentity.ComputeSha256(second.CanonicalJson), second.Sha256);
        Assert.NotEqual(first.Sha256, second.Sha256);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ConfiguredHealth_IsStableForIdenticalRepositoryAndProfiledRevision()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IAnalysisRuntimeHealthProbe probe =
            scope.ServiceProvider.GetRequiredService<IAnalysisRuntimeHealthProbe>();
        AnalysisScope analysisScope = new(
            RepositoryId,
            ScanRunId,
            "0123456789abcdef0123456789abcdef01234567",
            "prepared-runtime-resolver",
            new string('a', 64),
            new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds()));

        AnalysisHealthProbeResult first =
            await probe.CheckConfiguredIntegrityAsync(analysisScope, CancellationToken.None);
        AnalysisHealthProbeResult second =
            await probe.CheckConfiguredIntegrityAsync(analysisScope, CancellationToken.None);

        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.FailureCode, second.FailureCode);
        Assert.Equal(first.RuntimeIdentityHash, second.RuntimeIdentityHash);
        Assert.Equal(first.ComponentChecks, second.ComponentChecks);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DefaultComposition_UsesCodeGraph15AndJdtLsPorts()
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();

        string program = ReadRepositoryFile("src", "dotnet", "Vulgata.Web", "Program.cs");
        Assert.Contains("CodeGraphAnalysisAdapter", program, StringComparison.Ordinal);
        Assert.Contains("ICodeGraphAnalysisPort", program, StringComparison.Ordinal);
        Assert.Contains("JdtLsAnalysisSessionFactory", program, StringComparison.Ordinal);
        Assert.Contains("IJavaAnalysisSessionFactory", program, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ConfiguredIntegrityAlgorithm_HasOneOwnerInsteadOfASecondProgramCopy()
    {
        string program = ReadRepositoryFile("src", "dotnet", "Vulgata.Web", "Program.cs");

        Assert.DoesNotContain(
            "_codeGraphHashes",
            program,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ComputeSha256Async",
            program,
            StringComparison.Ordinal);
    }

    private static Type FindWebType(string simpleName) =>
        typeof(Program).Assembly.GetTypes().SingleOrDefault(type =>
            string.Equals(type.Name, simpleName, StringComparison.Ordinal))
        ?? throw new Xunit.Sdk.XunitException(
            $"Expected internal Web type '{simpleName}' was not composed yet.");

    private static RuntimeResolverProxy CreateResolverProxy(
        Type contract,
        AnalysisRuntimeIdentity identity)
    {
        MethodInfo create = typeof(DispatchProxy)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method =>
                method.Name == nameof(DispatchProxy.Create) &&
                method.IsGenericMethodDefinition &&
                method.GetGenericArguments().Length == 2)
            .MakeGenericMethod(contract, typeof(RuntimeResolverProxy));
        RuntimeResolverProxy proxy = (RuntimeResolverProxy)create.Invoke(null, null)!;
        proxy.Identity = identity;
        return proxy;
    }

    private static AnalysisScope CreateScope(
        Guid? repositoryId = null,
        string? profiledRevision = null) => new(
        repositoryId ?? RepositoryId,
        ScanRunId,
        profiledRevision ?? "0123456789abcdef0123456789abcdef01234567",
        "prepared-runtime-resolver",
        new string('a', 64),
        new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds()));

    private static async Task<object> InvokeResolverAsync(
        MethodInfo resolve,
        object resolver,
        Guid repositoryId,
        string profiledRevision)
    {
        List<object?> arguments = [];
        foreach (ParameterInfo parameter in resolve.GetParameters())
        {
            if (parameter.ParameterType == typeof(Guid))
            {
                arguments.Add(repositoryId);
            }
            else if (parameter.ParameterType == typeof(string))
            {
                arguments.Add(parameter.Name?.Contains("revision", StringComparison.OrdinalIgnoreCase) == true
                    ? profiledRevision
                    : "prepared-runtime-resolver");
            }
            else if (parameter.ParameterType == typeof(AnalysisScope))
            {
                arguments.Add(new AnalysisScope(
                    repositoryId,
                    ScanRunId,
                    profiledRevision,
                    "prepared-runtime-resolver",
                    new string('a', 64),
                    new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds())));
            }
            else if (parameter.ParameterType == typeof(CancellationToken))
            {
                arguments.Add(CancellationToken.None);
            }
            else
            {
                throw new Xunit.Sdk.XunitException(
                    $"Unsupported resolver parameter: {parameter.ParameterType.FullName}.");
            }
        }

        object? invocation = resolve.Invoke(resolver, [.. arguments]);
        if (invocation is Task task)
        {
            await task.ConfigureAwait(false);
            invocation = task.GetType().GetProperty("Result")?.GetValue(task);
        }

        return invocation
               ?? throw new Xunit.Sdk.XunitException("Resolver returned no identity result.");
    }

    private static AnalysisRuntimeIdentity ExtractIdentity(object result)
    {
        if (result is AnalysisRuntimeIdentity identity)
        {
            return identity;
        }

        PropertyInfo? identityProperty = result.GetType().GetProperty("Identity") ??
            result.GetType().GetProperty("RuntimeIdentity");
        return identityProperty?.GetValue(result) as AnalysisRuntimeIdentity
               ?? throw new Xunit.Sdk.XunitException(
                   $"Resolver result {result.GetType().FullName} does not expose AnalysisRuntimeIdentity.");
    }

    private class RuntimeResolverProxy : DispatchProxy
    {
        public AnalysisRuntimeIdentity Identity { get; set; } = null!;
        public int CallCount { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            CallCount++;
            if (targetMethod is null)
            {
                return null;
            }

            object? result = targetMethod.ReturnType == typeof(AnalysisRuntimeIdentity)
                ? Identity
                : CreateReturnValue(targetMethod.ReturnType);
            return result;
        }

        private object? CreateReturnValue(Type returnType)
        {
            if (returnType == typeof(Task))
            {
                return Task.CompletedTask;
            }

            if (returnType == typeof(ValueTask))
            {
                return ValueTask.CompletedTask;
            }

            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                Type resultType = returnType.GetGenericArguments()[0];
                object? value = CreateTypedResult(resultType);
                return typeof(Task)
                    .GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(resultType)
                    .Invoke(null, [value]);
            }

            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
            {
                Type resultType = returnType.GetGenericArguments()[0];
                object? value = CreateTypedResult(resultType);
                return Activator.CreateInstance(returnType, value);
            }

            return CreateTypedResult(returnType);
        }

        private object? CreateTypedResult(Type resultType)
        {
            if (resultType == typeof(AnalysisRuntimeIdentity))
            {
                return Identity;
            }

            if (resultType == typeof(AnalysisHealthProbeResult))
            {
                return new AnalysisHealthProbeResult(
                    AnalysisRuntimeHealthStatus.Ready,
                    "none",
                    string.Empty,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    [
                        new AnalysisHealthCheck("codegraph", AnalysisRuntimeHealthStatus.Ready),
                        new AnalysisHealthCheck("java", AnalysisRuntimeHealthStatus.Ready),
                        new AnalysisHealthCheck("jdt-ls", AnalysisRuntimeHealthStatus.Ready),
                        new AnalysisHealthCheck("streamjsonrpc", AnalysisRuntimeHealthStatus.Ready),
                    ],
                    Identity.Sha256);
            }

            ConstructorInfo? constructor = resultType.GetConstructor([typeof(AnalysisRuntimeIdentity)]);
            if (constructor is not null)
            {
                return constructor.Invoke([Identity]);
            }

            MethodInfo? ready = resultType.GetMethod(
                "Ready",
                BindingFlags.Public | BindingFlags.Static,
                [typeof(AnalysisRuntimeIdentity)]);
            return ready?.Invoke(null, [Identity]);
        }
    }

    private static string ReadRepositoryFile(params string[] segments) =>
        File.ReadAllText(Path.Combine([GetRepositoryRoot(), .. segments]));

    private static string GetRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException("Repository root could not be located.");
    }

    private sealed class RecordingHealthProbe : IAnalysisRuntimeHealthProbe
    {
        public List<AnalysisHealthProbeResult> Results { get; } = [];

        public Task<AnalysisHealthProbeResult> CheckConfiguredIntegrityAsync(
            AnalysisScope scope,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AnalysisRuntimeIdentity identity = CreateIdentity(scope.RepositoryId, scope.ProfiledRevision);
            AnalysisHealthProbeResult result = new(
                AnalysisRuntimeHealthStatus.Ready,
                "none",
                string.Empty,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                [
                    new AnalysisHealthCheck("codegraph", AnalysisRuntimeHealthStatus.Ready),
                    new AnalysisHealthCheck("java", AnalysisRuntimeHealthStatus.Ready),
                    new AnalysisHealthCheck("jdt-ls", AnalysisRuntimeHealthStatus.Ready),
                    new AnalysisHealthCheck("streamjsonrpc", AnalysisRuntimeHealthStatus.Ready),
                ],
                identity.Sha256);
            Results.Add(result);
            return Task.FromResult(result);
        }
    }

    private static AnalysisRuntimeIdentity CreateIdentity(Guid repositoryId, string profiledRevision) =>
        AnalysisRuntimeIdentity.Create(
            new AnalysisRuntimeOptions("codegraph", "jdt-ls", "java"),
            repositoryId,
            profiledRevision,
            "@colbymchenry/codegraph",
            "1.5.0",
            "https://github.com/colbymchenry/codegraph",
            "codegraph",
            "@colbymchenry/codegraph-win32-x64@1.5.0",
            [
                new(
                    "lib/dist/bin/codegraph.js",
                    "03e4c791cc0dd91ed264278461bf9a56c0278aa0670d5942fc4732311c66de03"),
                new(
                    "node.exe",
                    "b3094d0b49f9ad602262a9921551737bb97637c05dd357a06ae98188d7290aa3"),
                new(
                    "npm-shim.js",
                    "80419260f06862d7a422d13ef69e3b24c7a21dbef4309bce41db02ad642b1877"),
            ],
            "jdt-language-server-1.61.0-202608141332",
            "jdt-language-server-1.61.0-202608141332.zip",
            new('b', 64),
            21,
            "StreamJsonRpc",
            "2.25.29",
            "stdio");
}
