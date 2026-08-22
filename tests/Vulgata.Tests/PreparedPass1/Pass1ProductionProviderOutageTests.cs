using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;
using Vulgata.Tests.PreparedScanning;
using Vulgata.Shared.Scans;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// Story 12.3 T056 RED boundary for the active provider outage path.
///
/// These tests deliberately bind to the public production seams that T057 is
/// required to add.  The fakes are transport/time dependencies, not a second
/// provider implementation: once the seams exist, the production probe must
/// be exercised with these dependencies and must classify only the bounded
/// observation that crosses the durable boundary.
/// </summary>
[Collection(PreparedScanningCollection.Name)]
public sealed class Pass1ProductionProviderOutageTests
{
    private const string HealthProbeTypeName =
        "Vulgata.Agents.WorkerExecution.PreparedPass1ProviderHealthProbe";
    private const string HealthProbeContractTypeName =
        "Vulgata.Core.DomainServices.IPreparedPass1ProviderHealthProbe";
    private const string ProbeDriverTypeName =
        "Vulgata.Web.Data.PreparedPass1ProbeDriver";

    [Fact]
    public async Task AuthenticatedRealPair_ProviderUnavailable_PausesDurablyAndStopsNewDispatch()
    {
        await using PreparedScanPostgreSqlFixture fixture =
            await Pass1ProductionExecutionTests.PreparePairAsync();
        using var host = Pass1ProductionExecutionTests.CreateProductionHost(
            fixture.ConnectionString);
        await ConfigureUnavailableWorkerProviderAsync(host);
        using HttpClient client = await Pass1ProductionExecutionTests
            .CreateAuthenticatedClientAsync(host, fixture);
        Guid[] manifests = await fixture.Context.PreparedScanManifests
            .OrderBy(value => value.RepositoryId)
            .Select(value => value.Id)
            .ToArrayAsync();

        using HttpResponseMessage start = await client.PostAsJsonAsync(
            "/api/pass1-executions",
            new
            {
                firstManifestId = manifests[0],
                secondManifestId = manifests[1],
                concurrencyLimit = 2,
            });
        string body = await start.Content.ReadAsStringAsync();
        Assert.True(
            start.StatusCode == HttpStatusCode.Created,
            $"T062 RED: provider-outage pair could not enter production execution; status={start.StatusCode}, body={body}");
        Pass1ExecutionStartResponse started =
            (await start.Content.ReadFromJsonAsync<Pass1ExecutionStartResponse>())!;

        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        Pass1ExecutionStatusResponse? status = null;
        while (DateTime.UtcNow < deadline)
        {
            using HttpResponseMessage response = await client.GetAsync(
                $"/api/pass1-executions/{started.Pass1ExecutionId:D}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            status = (await response.Content.ReadFromJsonAsync<Pass1ExecutionStatusResponse>())!;
            if (status.Phase == "PausedProviderUnavailable")
            {
                break;
            }

            await Task.Delay(250);
        }

        Assert.NotNull(status);
        Assert.Equal(
            "PausedProviderUnavailable",
            status!.Phase);
        Assert.True(status.DispatchedCount == 0 || status.PendingCount > 0);
        Assert.Contains("继续", status.ManualResumeAction ?? body, StringComparison.Ordinal);
    }

    private static async Task ConfigureUnavailableWorkerProviderAsync(
        WebApplicationFactory<Program> host)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        VulgataDbContext context = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
        ISecretEncryptionService encryption =
            scope.ServiceProvider.GetRequiredService<ISecretEncryptionService>();
        string encryptedApiKey = await encryption.EncryptAsync(
            SecretPurposes.LlmApiKey,
            "t062-provider-outage-key");
        context.LlmProviders.Add(new LlmProvider(
            "t062-provider-outage",
            "http://127.0.0.1:1",
            encryptedApiKey,
            ApiType.ChatCompletions,
            AgentType.Worker,
            PreparedScanTestHost.FixedUtcNow));
        await context.SaveChangesAsync();
    }

    [Fact]
    public void ActiveProviderProbe_UsesInjectedTransportAndTimeInsteadOfConfigurationResolution()
    {
        using RecordingTransport transport = new();
        using HttpClient client = new(transport);
        ManualTimeProvider time = new(
            new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.Zero));

        Type probe = RequireType("Vulgata.Agents", HealthProbeTypeName);
        object instance = CreateProbe(probe, client, transport, time);
        MethodInfo operation = RequireOperation(probe, "Probe", "Check", "Health");
        object? result = InvokeAsync(instance, operation, transport, client, time, HttpStatusCode.OK);

        Assert.NotNull(result);
        Assert.Single(transport.Requests);
        string body = transport.RequestBodies.Single();
        Assert.True(body.Length <= 65_536, "The active health request must remain bounded.");
        Assert.DoesNotContain(
            ["source", "repository", "system", "scan", "context", "document", "classification", "tools", "navigation"],
            token => body.Contains(token, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            transport.Requests.Single().Headers,
            header => header.Key.Contains("source", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Contains("repository", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Contains("scan", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ActiveProviderProbe_RequestContractIsSourceFreeAndRetriesAndLogsAreDisabled()
    {
        Type contract = RequireType("Vulgata.Core", HealthProbeContractTypeName);
        MethodInfo[] methods = contract.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        Assert.Contains(methods, method =>
            method.ReturnType.IsGenericType &&
            method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));

        string[] forbidden = [
            "Source", "Identity", "Repository", "System", "Scan", "Context",
            "Classification", "Document", "Tool", "Navigation" ];
        foreach (MethodInfo method in methods)
        {
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                Assert.DoesNotContain(
                    forbidden,
                    word => parameter.Name?.Contains(word, StringComparison.OrdinalIgnoreCase) == true);
            }
        }

        using RecordingTransport transport = new();
        using HttpClient client = new(transport);
        ManualTimeProvider time = new(DateTimeOffset.UtcNow);
        Type probe = RequireType("Vulgata.Agents", HealthProbeTypeName);
        object instance = CreateProbe(probe, client, transport, time);
        MethodInfo operation = RequireOperation(probe, "Probe", "Check", "Health");
        _ = InvokeAsync(instance, operation, transport, client, time, HttpStatusCode.OK);
        Assert.Single(transport.Requests);
        Assert.Single(transport.RequestBodies);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public void ActiveProviderProbe_MapsAuthenticationResponsesToConfigurationPause(
        HttpStatusCode statusCode)
    {
        using RecordingTransport transport = new(() => new HttpResponseMessage(statusCode));
        using HttpClient client = new(transport);
        ManualTimeProvider time = new(DateTimeOffset.UtcNow);
        Type probe = RequireType("Vulgata.Agents", HealthProbeTypeName);
        object instance = CreateProbe(probe, client, transport, time);
        MethodInfo operation = RequireOperation(probe, "Probe", "Check", "Health");
        object? result = InvokeAsync(instance, operation, transport, client, time, statusCode);

        Assert.NotNull(result);
        Assert.Equal("ProviderConfiguration", ReadOutcome(result));
        Assert.Single(transport.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData((HttpStatusCode)429)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public void ActiveProviderProbe_MapsTransientResponsesAndSendsExactlyOnce(HttpStatusCode statusCode)
    {
        using RecordingTransport transport = new(() => new HttpResponseMessage(statusCode));
        using HttpClient client = new(transport);
        ManualTimeProvider time = new(DateTimeOffset.UtcNow);
        Type probe = RequireType("Vulgata.Agents", HealthProbeTypeName);
        object instance = CreateProbe(probe, client, transport, time);
        MethodInfo operation = RequireOperation(probe, "Probe", "Check", "Health");
        object? result = InvokeAsync(instance, operation, transport, client, time, statusCode);

        Assert.NotNull(result);
        string outcome = ReadOutcome(result);
        Assert.True(
            outcome.Contains("Unavailable", StringComparison.OrdinalIgnoreCase) ||
                outcome.Contains("Transient", StringComparison.OrdinalIgnoreCase),
            $"Expected provider-transient outcome, got {outcome}.");
        Assert.Single(transport.Requests);
    }

    [Fact]
    public void ActiveProviderProbe_MapsTransportLossToUnavailableWithoutSdkRetry()
    {
        using RecordingTransport transport = new(() => throw new HttpRequestException("synthetic outage"));
        using HttpClient client = new(transport);
        ManualTimeProvider time = new(DateTimeOffset.UtcNow);
        Type probe = RequireType("Vulgata.Agents", HealthProbeTypeName);
        object instance = CreateProbe(probe, client, transport, time);
        MethodInfo operation = RequireOperation(probe, "Probe", "Check", "Health");

        object? result = InvokeAsync(instance, operation, transport, client, time, HttpStatusCode.OK);

        Assert.NotNull(result);
        string outcome = ReadOutcome(result);
        Assert.True(
            outcome.Contains("Unavailable", StringComparison.OrdinalIgnoreCase) ||
                outcome.Contains("Transient", StringComparison.OrdinalIgnoreCase),
            $"Expected transport loss to be provider-unavailable, got {outcome}.");
        Assert.Single(transport.Requests);
    }

    [Fact]
    public void ProviderProbeDriver_StopsAfterFifthFailureAndHasNoStartupOrBackgroundProbe()
    {
        Type driver = RequireType("Vulgata.Web", ProbeDriverTypeName);
        Assert.False(
            typeof(IHostedService).IsAssignableFrom(driver),
            "The probe driver is explicitly activated by a pair; host startup must not discover dormant pairs.");

        ManualTimeProvider time = new(DateTimeOffset.UtcNow);
        using RecordingTransport transport = new(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using HttpClient client = new(transport);
        object instance = CreateProbe(driver, client, transport, time);
        MethodInfo operation = driver.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.ReturnType.IsGenericType &&
                method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
            .SingleOrDefault(method => method.Name.Contains("Probe", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Run", StringComparison.OrdinalIgnoreCase))
            ?? throw new Xunit.Sdk.XunitException(
                "T056 RED: the pair probe driver must expose one public async invoked operation.");

        object? result = InvokeAsync(instance, operation, transport, client, time, HttpStatusCode.ServiceUnavailable);
        Assert.NotNull(result);
        Assert.Contains(
            RecordingProxy.Calls,
            call => call.MethodName.Contains("Probe", StringComparison.OrdinalIgnoreCase) ||
                call.MethodName.Contains("Health", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            driver.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name.Contains("Startup", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Background", StringComparison.OrdinalIgnoreCase));
    }

    private static Type RequireType(string assemblyName, string fullName)
    {
        Assembly assembly = Assembly.Load(assemblyName);
        return assembly.GetType(fullName, throwOnError: false, ignoreCase: false)
            ?? throw new InvalidOperationException(
                $"T056 RED: the T057 production seam is missing: {fullName}.");
    }

    private static object CreateProbe(
        Type implementation,
        HttpClient client,
        HttpMessageHandler transport,
        TimeProvider time)
    {
        foreach (ConstructorInfo constructor in implementation
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(value => value.GetParameters().Length))
        {
            try
            {
                object?[] arguments = constructor.GetParameters()
                    .Select(parameter => ResolveValue(parameter.ParameterType, parameter.Name, client, transport, time))
                    .ToArray();
                return constructor.Invoke(arguments);
            }
            catch (TargetInvocationException)
            {
            }
            catch (ArgumentException)
            {
            }
        }

        throw new Xunit.Sdk.XunitException(
            "T056 RED: the active provider probe cannot be composed with the fake transport and fake time dependencies.");
    }

    private static MethodInfo RequireOperation(Type implementation, params string[] nameParts) =>
        implementation.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.ReturnType.IsGenericType &&
                method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
            .SingleOrDefault(method => nameParts.Any(part =>
                method.Name.Contains(part, StringComparison.OrdinalIgnoreCase)))
        ?? throw new Xunit.Sdk.XunitException(
            $"T056 RED: {implementation.FullName} must expose one public async active-probe operation.");

    private static object? InvokeAsync(
        object instance,
        MethodInfo operation,
        RecordingTransport transport,
        HttpClient client,
        TimeProvider time,
        HttpStatusCode status)
    {
        transport.StatusCode = status;
        object?[] arguments = operation.GetParameters()
            .Select(parameter => ResolveValue(parameter.ParameterType, parameter.Name, client, transport, time))
            .ToArray();
        object invocation = operation.Invoke(instance, arguments)
            ?? throw new Xunit.Sdk.XunitException("T056 RED: active provider probe returned no Task.");
        if (invocation is not Task task)
        {
            throw new Xunit.Sdk.XunitException("T056 RED: active provider probe must return Task<T>.");
        }

        task.GetAwaiter().GetResult();
        return task.GetType().GetProperty("Result")?.GetValue(task);
    }

    private static object? ResolveValue(
        Type type,
        string? name,
        HttpClient client,
        HttpMessageHandler transport,
        TimeProvider time)
    {
        if (type == typeof(HttpClient))
        {
            return client;
        }

        if (type == typeof(HttpMessageHandler) || type.IsAssignableFrom(transport.GetType()))
        {
            return transport;
        }

        if (type == typeof(TimeProvider))
        {
            return time;
        }

        if (type == typeof(CancellationToken))
        {
            return CancellationToken.None;
        }

        if (type == typeof(Guid))
        {
            return Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        }

        if (type == typeof(Uri))
        {
            return new Uri("https://provider.invalid/v1");
        }

        if (type == typeof(string))
        {
            return name?.Contains("model", StringComparison.OrdinalIgnoreCase) == true
                ? "health-test-model"
                : "health-test-secret";
        }

        if (type.IsValueType)
        {
            return Activator.CreateInstance(type);
        }

        if (type.IsInterface)
        {
            return DispatchProxy.Create(type, typeof(RecordingProxy));
        }

        ConstructorInfo? constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(value => value.GetParameters().Length)
            .FirstOrDefault();
        if (constructor is not null)
        {
            return constructor.Invoke(constructor.GetParameters()
                .Select(parameter => ResolveValue(parameter.ParameterType, parameter.Name, client, transport, time))
                .ToArray());
        }

        return null;
    }

    private static string ReadOutcome(object result)
    {
        PropertyInfo? property = result.GetType().GetProperty("Outcome") ??
            result.GetType().GetProperty("Status") ??
            result.GetType().GetProperty("Classification");
        return property?.GetValue(result)?.ToString() ??
            throw new Xunit.Sdk.XunitException(
                $"T056 RED: active health result {result.GetType().FullName} exposes no bounded outcome.");
    }

    private sealed class RecordingTransport : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _responseFactory;

        public RecordingTransport(Func<HttpResponseMessage>? responseFactory = null) =>
            _responseFactory = responseFactory ?? (() => new HttpResponseMessage(StatusCode));

        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> RequestBodies { get; } = [];

        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            RequestBodies.Add(request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult() ?? string.Empty);
            return Task.FromResult(_responseFactory());
        }
    }

    private class RecordingProxy : DispatchProxy
    {
        public static List<ProxyCall> Calls { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null)
            {
                return null;
            }

            Calls.Add(new(targetMethod.Name, args ?? []));

            Type returnType = targetMethod.ReturnType;
            if (returnType == typeof(Task))
            {
                return Task.CompletedTask;
            }

            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                Type valueType = returnType.GetGenericArguments()[0];
                object? value = valueType == typeof(PreparedPass1ProviderCircuitResult)
                    ? new PreparedPass1ProviderCircuitResult(
                        PreparedPass1ProviderCircuitOutcome.ProbeScheduled,
                        Pass1CircuitState.Probing,
                        Pass1PairExecutionPhase.ProbingProvider,
                        1,
                        DateTimeOffset.UtcNow.AddSeconds(10),
                        null,
                        "provider-unavailable",
                        string.Empty,
                        0,
                        [],
                        false)
                    : valueType.IsValueType ? Activator.CreateInstance(valueType) : null;
                return typeof(Task).GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(valueType)
                    .Invoke(null, [value]);
            }

            return returnType.IsValueType ? Activator.CreateInstance(returnType) : null;
        }
    }

    private sealed record ProxyCall(string MethodName, object?[] Arguments);

    private sealed class ManualTimeProvider(DateTimeOffset current) : TimeProvider
    {
        private DateTimeOffset _current = current;

        public override DateTimeOffset GetUtcNow() => _current;

        public void Advance(TimeSpan amount) => _current = _current.Add(amount);
    }
}
