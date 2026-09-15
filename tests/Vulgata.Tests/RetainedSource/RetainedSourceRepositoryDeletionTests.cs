using System.Net;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;
using Vulgata.Web.Data;

namespace Vulgata.Tests.RetainedSource;

public sealed class RetainedSourceRepositoryDeletionTests
{
    [Fact]
    public async Task Coordinator_DeleteCapturedRepository_ReturnsConflictAndPreservesRows()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        await CaptureAsync(fixture, appFixture);

        RepositoryMutationResult<bool> outcome = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRepositoryManagementCoordinator>()
            .DeleteAsync(
                appFixture.SystemId,
                fixture.RepositoryId,
                appFixture.Scope.ServiceProvider.GetRequiredService<IUserContext>());

        AssertCapturedSnapshotRemains(appFixture, fixture);
        Assert.NotEqual(RepositoryMutationOutcome.Success, outcome.Outcome);
        Assert.Contains("保留", outcome.Message ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RepositoryDeleteEndpoint_ReturnsSanitizedConflictAndPreservesRows()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);
        await CaptureAsync(fixture, appFixture);

        MethodInfo mapRepositoryEndpoints =
            typeof(Program).Assembly
                .GetType("Vulgata.Web.Api.Repositories.RepositoryApiEndpoints")!
                .GetMethod(
                    "MapRepositoryApiEndpoints",
                    BindingFlags.Public | BindingFlags.Static)!
                ?? throw new InvalidOperationException("The repository endpoint map method is missing.");
        mapRepositoryEndpoints.Invoke(null, [appFixture.Application]);

        IEndpointRouteBuilder routeBuilder = appFixture.Application;
        RouteEndpoint endpoint = routeBuilder.DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(candidate => candidate.RoutePattern.RawText ==
                "/api/systems/{systemId:guid}/repositories/{repositoryId:guid}");
        Assert.NotNull(endpoint.RequestDelegate);

        using IServiceScope endpointScope = appFixture.Application.Services.CreateScope();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, appFixture.UserId),
                new Claim(ClaimTypes.Role, RoleNames.Administrator),
            ],
            authenticationType: "RetainedSourceDeletionTest"));
        var context = new DefaultHttpContext
        {
            RequestServices = endpointScope.ServiceProvider,
            User = principal,
        };
        context.Response.Body = new MemoryStream();
        context.Request.Method = HttpMethods.Delete;
        context.Request.RouteValues["systemId"] = appFixture.SystemId.ToString("D");
        context.Request.RouteValues["repositoryId"] = fixture.RepositoryId.ToString("D");
        appFixture.Application.Services.GetRequiredService<IHttpContextAccessor>().HttpContext = context;

        await endpoint.RequestDelegate!(context);

        AssertCapturedSnapshotRemains(appFixture, fixture);
        Assert.Equal((int)HttpStatusCode.Conflict, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        string responseBody = await reader.ReadToEndAsync();
        Assert.Contains("保留", responseBody, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.SourceStorageRoot, responseBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Coordinator_DeleteRepositoryWithoutRetainedSnapshot_Succeeds()
    {
        await using RetainedSourceFixture fixture = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);

        RepositoryMutationResult<bool> outcome = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRepositoryManagementCoordinator>()
            .DeleteAsync(
                appFixture.SystemId,
                fixture.RepositoryId,
                appFixture.Scope.ServiceProvider.GetRequiredService<IUserContext>());

        Assert.Equal(RepositoryMutationOutcome.Success, outcome.Outcome);
        Assert.True(outcome.Value);
        IDbContextFactory<VulgataDbContext> factory = appFixture.Scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        using VulgataDbContext context = factory.CreateDbContext();
        Assert.Empty(context.Repositories.Where(repository => repository.Id == fixture.RepositoryId));
    }

    private static async Task CaptureAsync(
        RetainedSourceFixture fixture,
        RetainedSourceCaptureApplicationFixture appFixture)
    {
        SourceCaptureResult result = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .AcquireAsync(
                fixture.RepositoryId,
                await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]));
        Assert.Equal(SourceCaptureStatus.Completed, result.Status);
    }

    private static void AssertCapturedSnapshotRemains(
        RetainedSourceCaptureApplicationFixture appFixture,
        RetainedSourceFixture fixture)
    {
        IDbContextFactory<VulgataDbContext> factory = appFixture.Scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        using VulgataDbContext context = factory.CreateDbContext();
        Assert.Equal(1, context.SourceSnapshots.Count(snapshot => snapshot.RepositoryId == fixture.RepositoryId));
        Assert.Equal(1, context.Repositories.Count(repository => repository.Id == fixture.RepositoryId));
    }

}
