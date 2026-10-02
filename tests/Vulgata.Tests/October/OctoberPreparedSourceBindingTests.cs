using System.Collections.Concurrent;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Infrastructure.Source;
using Vulgata.OctoberGenerator;
using Vulgata.Shared;
using Vulgata.Tests.PreparedSource;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;

namespace Vulgata.Tests.October;

[Collection(PreparedSourceTestCollection.Name)]
public sealed class OctoberPreparedSourceBindingTests
{
    private const string TargetId = "october-source-target-a";
    private const string OtherTargetId = "october-source-target-b";
    private const string SubjectId = "october-source-subject";
    private const string OperatorLabel = "prepared-source-operator";
    private static readonly Guid BatchId = Guid.Parse("490581b3-a6c9-41c2-8d10-579cef0fa751");
    private static readonly Guid RepositoryOne = Guid.Parse("c07e2cc2-f96f-4c8e-8fd7-e975e220b917");
    private static readonly Guid RepositoryTwo = Guid.Parse("a17b99e0-f130-41fb-8d2d-985fd53a43bb");

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [InlineData("current", PreparedSourceIssueStatus.Issued)]
    [InlineData("revoked", PreparedSourceIssueStatus.Denied)]
    [InlineData("unavailable", PreparedSourceIssueStatus.AuthorityUnavailable)]
    [InlineData("substituted", PreparedSourceIssueStatus.Denied)]
    [InlineData("root-version-mismatch", PreparedSourceIssueStatus.Denied)]
    public async Task ContextIssuer_OctoberVerifierRequiresExactWebAndRootAuthority(
        string scenario,
        PreparedSourceIssueStatus expected)
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        PreparedSourceApproval approval = CreateApproval(RepositoryOne, "root-version-one");
        ControlledWebApproval web = new((target, requested, _) =>
        {
            Assert.Equal(TargetId, target);
            PreparedSourceAuthorityResult response = scenario switch
            {
                "revoked" => new(PreparedSourceAuthorityStatus.Revoked, null),
                "unavailable" => new(PreparedSourceAuthorityStatus.Unavailable, null),
                "substituted" => new(PreparedSourceAuthorityStatus.Current,
                    requested with { ApprovalIdentity = "another-approval" }),
                _ => new(PreparedSourceAuthorityStatus.Current, requested),
            };
            return Task.FromResult(response);
        });
        bool rootVersionCurrent = scenario != "root-version-mismatch";
        var verifier = new OctoberPreparedSourceAuthorityVerifier(
            TargetId,
            web.CheckAsync,
            requested => requested == approval && rootVersionCurrent);
        var issuer = new PreparedSourceContextIssuer(verifier);

        PreparedSourceIssueResult result = await issuer.IssueAsync(
            new PreparedSourceGrantRequest(
                approval, fixture.PreparedRootPath, PreparedSourceGrantLimits.Default));

        Assert.Equal(expected, result.Status);
        if (expected == PreparedSourceIssueStatus.Issued)
        {
            Assert.NotNull(result.Context);
            Assert.Equal(RepositoryOne, result.Context.RepositoryId);
            await result.Context.DisposeAsync();
            Assert.Equal(PreparedSourceContextLifetime.Disposed, result.Context.Lifetime);
        }
        else
        {
            Assert.Null(result.Context);
        }

        Assert.Single(web.Calls);
        Assert.Equal(TargetId, web.Calls[0].TargetId);
        Assert.Equal(approval, web.Calls[0].Approval);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task IssueAsync_CurrentApprovalAndExactRootBindings_IssuesIndependentRepositoryContexts()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture firstRoot = await PreparedSourceFixture.CreateAsync();
        await using PreparedSourceFixture secondRoot = await PreparedSourceFixture.CreateAsync();
        await File.WriteAllTextAsync(secondRoot.UnfamiliarTextPath, "second repository source");
        PreparedSourceApproval firstApproval = CreateApproval(RepositoryOne, "root-version-one");
        PreparedSourceApproval secondApproval = CreateApproval(RepositoryTwo, "root-version-two");
        PreparedSourceRootBinding[] roots =
        [
            new(TargetId, BatchId, RepositoryOne, "root-version-one", firstRoot.PreparedRootPath),
            new(TargetId, BatchId, RepositoryTwo, "root-version-two", secondRoot.PreparedRootPath),
        ];
        ControlledWebApproval web = new((target, approval, _) =>
        {
            Assert.Equal(TargetId, target);
            return Task.FromResult(new PreparedSourceAuthorityResult(
                PreparedSourceAuthorityStatus.Current, approval));
        });
        PreparedSourceBinding binding = CreateBinding(TargetId, roots, web.CheckAsync);

        Assert.True(binding.HasCurrentRootBinding(firstApproval));
        Assert.True(binding.HasCurrentRootBinding(secondApproval));
        Assert.False(binding.HasCurrentRootBinding(firstApproval with { BatchId = Guid.NewGuid() }));
        Assert.False(binding.HasCurrentRootBinding(firstApproval with { RepositoryId = RepositoryTwo }));
        Assert.False(binding.HasCurrentRootBinding(firstApproval with
        {
            RootBindingIdentity = "unrecognized-root-version",
        }));

        PreparedSourceIssueResult first = await binding.IssueAsync(
            firstApproval, PreparedSourceGrantLimits.Default);
        PreparedSourceIssueResult second = await binding.IssueAsync(
            secondApproval, PreparedSourceGrantLimits.Default);

        Assert.Equal(PreparedSourceIssueStatus.Issued, first.Status);
        Assert.Equal(PreparedSourceIssueStatus.Issued, second.Status);
        Assert.NotNull(first.Context);
        Assert.NotNull(second.Context);
        Assert.NotSame(first.Context, second.Context);
        Assert.Equal(RepositoryOne, first.Context.RepositoryId);
        Assert.Equal(RepositoryTwo, second.Context.RepositoryId);
        Assert.Equal(new[] { firstApproval, secondApproval },
            web.Calls.Take(2).Select(call => call.Approval).ToArray());

        IPreparedSourceReadContext firstContext = first.Context;
        IPreparedSourceReadContext secondContext = second.Context;
        PreparedSourceResult firstRead = await firstContext.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest("notes.oddlangx", 0, 6));
        PreparedSourceResult secondRead = await secondContext.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest("notes.oddlangx", 0, 6));
        Assert.Equal(PreparedSourceOutcome.Complete, firstRead.Outcome);
        Assert.Equal(PreparedSourceOutcome.Complete, secondRead.Outcome);
        Assert.Equal(RepositoryOne, firstRead.RepositoryId);
        Assert.Equal(RepositoryTwo, secondRead.RepositoryId);
        Assert.Equal("café\n", firstRead.Text);
        Assert.Equal("second", secondRead.Text);
        await firstContext.DisposeAsync();
        await secondContext.DisposeAsync();
        Assert.Equal(PreparedSourceContextLifetime.Disposed, firstContext.Lifetime);
        Assert.Equal(PreparedSourceContextLifetime.Disposed, secondContext.Lifetime);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [InlineData("web-revoked", PreparedSourceIssueStatus.Denied)]
    [InlineData("web-unavailable", PreparedSourceIssueStatus.AuthorityUnavailable)]
    [InlineData("web-substituted-approval", PreparedSourceIssueStatus.Denied)]
    [InlineData("root-version-mismatch", PreparedSourceIssueStatus.Denied)]
    [InlineData("foreign-target", PreparedSourceIssueStatus.Denied)]
    public async Task IssueAsync_NonCurrentOrForeignBinding_ReturnsNoContext(
        string scenario,
        PreparedSourceIssueStatus expected)
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        PreparedSourceApproval approval = CreateApproval(RepositoryOne, "root-version-one");
        PreparedSourceRootBinding root = new(
            TargetId, BatchId, RepositoryOne, "root-version-one", fixture.PreparedRootPath);
        ControlledWebApproval web = new((target, requested, _) =>
        {
            Assert.Equal(TargetId, target);
            PreparedSourceAuthorityResult response = scenario switch
            {
                "web-revoked" => new(PreparedSourceAuthorityStatus.Revoked, null),
                "web-unavailable" => new(PreparedSourceAuthorityStatus.Unavailable, null),
                "web-substituted-approval" => new(
                    PreparedSourceAuthorityStatus.Current,
                    requested with { OperatorLabel = "substituted-label" }),
                _ => new(PreparedSourceAuthorityStatus.Current, requested),
            };
            return Task.FromResult(response);
        });
        string configuredTarget = scenario == "foreign-target" ? OtherTargetId : TargetId;
        PreparedSourceBinding binding = CreateBinding(configuredTarget, [root], web.CheckAsync);
        PreparedSourceApproval attempted = scenario switch
        {
            "root-version-mismatch" => approval with { RootBindingIdentity = "rotated-version" },
            "foreign-target" => approval,
            _ => approval,
        };

        PreparedSourceIssueResult result = await binding.IssueAsync(
            attempted, PreparedSourceGrantLimits.Default);

        Assert.Equal(expected, result.Status);
        Assert.Null(result.Context);
        if (scenario is "root-version-mismatch" or "foreign-target")
        {
            Assert.Empty(web.Calls);
        }
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [InlineData("revoked", PreparedSourceOutcome.Denied)]
    [InlineData("unavailable", PreparedSourceOutcome.AuthorityUnavailable)]
    [InlineData("root-version-rotated", PreparedSourceOutcome.Denied)]
    public async Task ReadTextRangeAsync_AuthorityChangesBeforeDisclosure_ClosesWithoutPayload(
        string change,
        PreparedSourceOutcome expected)
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        PreparedSourceApproval approval = CreateApproval(RepositoryOne, "root-version-one");
        bool rootCurrent = true;
        int webChecks = 0;
        var verifier = new OctoberPreparedSourceAuthorityVerifier(
            TargetId,
            (target, requested, cancellationToken) =>
            {
                Assert.Equal(TargetId, target);
                cancellationToken.ThrowIfCancellationRequested();
                webChecks++;
                if (change == "revoked" && webChecks > 1)
                {
                    return Task.FromResult(new PreparedSourceAuthorityResult(
                        PreparedSourceAuthorityStatus.Revoked, null));
                }

                if (change == "unavailable" && webChecks > 1)
                {
                    return Task.FromResult(new PreparedSourceAuthorityResult(
                        PreparedSourceAuthorityStatus.Unavailable, null));
                }

                return Task.FromResult(new PreparedSourceAuthorityResult(
                    PreparedSourceAuthorityStatus.Current, requested));
            },
            _ => rootCurrent);
        var issuer = new PreparedSourceContextIssuer(verifier);
        PreparedSourceRootBinding root = new(
            TargetId, BatchId, RepositoryOne, approval.RootBindingIdentity, fixture.PreparedRootPath);
        var binding = new PreparedSourceBinding(issuer, TargetId, [root]);
        PreparedSourceIssueResult issue = await binding.IssueAsync(
            approval, PreparedSourceGrantLimits.Default);
        Assert.Equal(PreparedSourceIssueStatus.Issued, issue.Status);
        Assert.NotNull(issue.Context);
        IPreparedSourceReadContext context = issue.Context;

        if (change == "root-version-rotated")
        {
            rootCurrent = false;
        }

        PreparedSourceResult result = await context.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest("notes.oddlangx", 0, 5));

        Assert.Equal(expected, result.Outcome);
        Assert.Null(result.Text);
        Assert.Null(result.ReadLocation);
        Assert.Null(result.Continuation);
        Assert.Equal(PreparedSourceContextLifetime.AuthorityClosed, context.Lifetime);
        await context.DisposeAsync();
        Assert.Equal(PreparedSourceContextLifetime.Disposed, context.Lifetime);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task ReadTextRangeAsync_CancellationDuringBorrowedAuthorityCheck_SettlesBeforeCallerDisposal()
    {
        RequireWindowsHost();
        await using PreparedSourceFixture fixture = await PreparedSourceFixture.CreateAsync();
        PreparedSourceApproval approval = CreateApproval(RepositoryOne, "root-version-one");
        var secondCheckEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int checkCount = 0;
        CancellationToken borrowedCheckToken = default;
        var verifier = new OctoberPreparedSourceAuthorityVerifier(
            TargetId,
            async (target, requested, cancellationToken) =>
            {
                Assert.Equal(TargetId, target);
                if (Interlocked.Increment(ref checkCount) == 2)
                {
                    borrowedCheckToken = cancellationToken;
                    secondCheckEntered.TrySetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }

                return new PreparedSourceAuthorityResult(
                    PreparedSourceAuthorityStatus.Current, requested);
            },
            requested => requested == approval);
        var issuer = new PreparedSourceContextIssuer(verifier);
        var binding = new PreparedSourceBinding(issuer, TargetId,
        [
            new PreparedSourceRootBinding(
                TargetId, BatchId, RepositoryOne, approval.RootBindingIdentity, fixture.PreparedRootPath),
        ]);
        PreparedSourceIssueResult issue = await binding.IssueAsync(
            approval, PreparedSourceGrantLimits.Default);
        Assert.Equal(PreparedSourceIssueStatus.Issued, issue.Status);
        Assert.NotNull(issue.Context);
        IPreparedSourceReadContext context = issue.Context;
        using var cancellation = new CancellationTokenSource();
        Task<PreparedSourceResult> borrowed = context.ReadTextRangeAsync(
            new PreparedSourceReadTextRangeRequest("notes.oddlangx", 0, 5), cancellation.Token);

        try
        {
            await secondCheckEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            cancellation.Cancel();
            await borrowed.WaitAsync(TimeSpan.FromSeconds(5));
            await context.DisposeAsync();
            throw;
        }

        cancellation.Cancel();
        PreparedSourceResult result = await borrowed.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(PreparedSourceOutcome.Cancelled, result.Outcome);
        Assert.True(borrowedCheckToken.IsCancellationRequested);
        Assert.Null(result.Text);
        Assert.Null(result.ReadLocation);
        Assert.Null(result.Continuation);
        Assert.Equal(PreparedSourceContextLifetime.ExpiredOrCancelled, context.Lifetime);

        await context.DisposeAsync();
        Assert.Equal(PreparedSourceContextLifetime.Disposed, context.Lifetime);
    }

    private static PreparedSourceBinding CreateBinding(
        string targetId,
        IReadOnlyCollection<PreparedSourceRootBinding> roots,
        OctoberCurrentApprovalCheck webCheck)
    {
        PreparedSourceBinding? binding = null;
        var verifier = new OctoberPreparedSourceAuthorityVerifier(
            targetId,
            webCheck,
            approval => binding!.HasCurrentRootBinding(approval));
        binding = new PreparedSourceBinding(
            new PreparedSourceContextIssuer(verifier), targetId, roots);
        return binding;
    }

    private static PreparedSourceApproval CreateApproval(Guid repositoryId, string rootBindingIdentity) =>
        new(
            BatchId,
            repositoryId,
            SubjectId,
            PreparedSourcePurpose.WorkerSourceRead,
            $"approval-{repositoryId:N}",
            rootBindingIdentity,
            OperatorLabel);

    private static void RequireWindowsHost()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw Xunit.Sdk.SkipException.ForSkip(
                "Windows handle-relative prepared-source access is not qualified on this platform.");
        }
    }

    private sealed class ControlledWebApproval(
        Func<string, PreparedSourceApproval, CancellationToken, Task<PreparedSourceAuthorityResult>> response)
    {
        private readonly ConcurrentQueue<ApprovalCheck> _calls = new();

        internal IReadOnlyList<ApprovalCheck> Calls => _calls.ToArray();

        internal Task<PreparedSourceAuthorityResult> CheckAsync(
            string targetId,
            PreparedSourceApproval approval,
            CancellationToken cancellationToken)
        {
            _calls.Enqueue(new ApprovalCheck(targetId, approval, cancellationToken));
            return response(targetId, approval, cancellationToken);
        }
    }

    private sealed record ApprovalCheck(
        string TargetId,
        PreparedSourceApproval Approval,
        CancellationToken CancellationToken);
}

public sealed partial class OctoberLocalAuthorityTests
{
    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task PreparedSourceBinding_UsesRealOctoberAuthorityAndKeepsRepositoriesIsolated()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw Xunit.Sdk.SkipException.ForSkip(
                "Windows handle-relative prepared-source access is not qualified on this platform.");
        }

        await using SelectionFixture firstFixture = await SelectionFixture.CreateAsync();
        await using SelectionFixture secondFixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession firstSession = await firstFixture.AuthenticateAsync();
        await using IOctoberOperatorSession secondSession = await secondFixture.AuthenticateAsync();
        await using PreparedSourceFixture firstRoot = await PreparedSourceFixture.CreateAsync();
        await using PreparedSourceFixture secondRoot = await PreparedSourceFixture.CreateAsync();
        await File.WriteAllTextAsync(secondRoot.UnfamiliarTextPath, "second repository source");

        (PreparedSourceApproval firstApproval, IPreparedSourceReadContext firstContext) =
            await IssueIntegratedContextAsync(firstFixture, firstSession, firstRoot);
        (PreparedSourceApproval secondApproval, IPreparedSourceReadContext secondContext) =
            await IssueIntegratedContextAsync(secondFixture, secondSession, secondRoot);
        Assert.NotEqual(firstApproval.RepositoryId, secondApproval.RepositoryId);
        Assert.NotSame(firstContext, secondContext);
        Assert.Equal(firstApproval.RepositoryId, firstContext.RepositoryId);
        Assert.Equal(secondApproval.RepositoryId, secondContext.RepositoryId);

        IReadOnlyList<AIFunction> firstFunctions = new PreparedSourceFunctions(firstContext).Create();
        IReadOnlyList<AIFunction> secondFunctions = new PreparedSourceFunctions(secondContext).Create();
        string[] functionNames = ["list_prepared_source", "read_prepared_source", "search_prepared_source"];
        Assert.Equal(functionNames, firstFunctions.Select(function => function.Name));
        Assert.Equal(functionNames, secondFunctions.Select(function => function.Name));

        PreparedSourceResult firstBeforeRevocation = Assert.IsType<PreparedSourceResult>(
            await ReadPreparedSourceAsync(firstFunctions[1]));
        PreparedSourceResult secondBeforeRevocation = Assert.IsType<PreparedSourceResult>(
            await ReadPreparedSourceAsync(secondFunctions[1]));
        Assert.Equal(PreparedSourceOutcome.Complete, firstBeforeRevocation.Outcome);
        Assert.Equal(firstApproval.RepositoryId, firstBeforeRevocation.RepositoryId);
        Assert.Equal("café\n", firstBeforeRevocation.Text);
        Assert.Equal(PreparedSourceOutcome.Complete, secondBeforeRevocation.Outcome);
        Assert.Equal(secondApproval.RepositoryId, secondBeforeRevocation.RepositoryId);
        Assert.Equal("second", secondBeforeRevocation.Text);

        OctoberRevocationStatus revoked = await firstFixture.Authority.RevokeSourceApprovalAsync(
            firstSession, TargetId, firstApproval.ApprovalIdentity, CancellationToken.None);
        Assert.Equal(OctoberRevocationStatus.Revoked, revoked);

        PreparedSourceResult firstAfterRevocation = Assert.IsType<PreparedSourceResult>(
            await ReadPreparedSourceAsync(firstFunctions[1]));
        Assert.Equal(PreparedSourceOutcome.Denied, firstAfterRevocation.Outcome);
        Assert.Equal(firstApproval.RepositoryId, firstAfterRevocation.RepositoryId);
        Assert.Null(firstAfterRevocation.Text);
        Assert.Null(firstAfterRevocation.ReadLocation);
        Assert.Null(firstAfterRevocation.Continuation);
        Assert.Equal(PreparedSourceContextLifetime.AuthorityClosed, firstContext.Lifetime);

        PreparedSourceResult secondAfterFirstRevocation = Assert.IsType<PreparedSourceResult>(
            await ReadPreparedSourceAsync(secondFunctions[1]));
        Assert.Equal(PreparedSourceOutcome.Complete, secondAfterFirstRevocation.Outcome);
        Assert.Equal(secondApproval.RepositoryId, secondAfterFirstRevocation.RepositoryId);
        Assert.Equal("second", secondAfterFirstRevocation.Text);

        await firstContext.DisposeAsync();
        await secondContext.DisposeAsync();
        Assert.Equal(PreparedSourceContextLifetime.Disposed, firstContext.Lifetime);
        Assert.Equal(PreparedSourceContextLifetime.Disposed, secondContext.Lifetime);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [InlineData("role", PreparedSourceIssueStatus.Denied)]
    [InlineData("membership", PreparedSourceIssueStatus.Denied)]
    [InlineData("stamp", PreparedSourceIssueStatus.Denied)]
    [InlineData("expiry", PreparedSourceIssueStatus.Denied)]
    [InlineData("root-row", PreparedSourceIssueStatus.Denied)]
    [InlineData("session", PreparedSourceIssueStatus.Denied)]
    [InlineData("revoked", PreparedSourceIssueStatus.Denied)]
    [InlineData("store", PreparedSourceIssueStatus.AuthorityUnavailable)]
    public async Task PreparedSourceBinding_RealOctoberAuthorityLossBeforeIssueFailsClosed(
        string change,
        PreparedSourceIssueStatus expected)
    {
        RequireWindowsHostForIntegration();
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        await using PreparedSourceFixture preparedRoot = await PreparedSourceFixture.CreateAsync();
        (PreparedSourceApproval approval, PreparedSourceBinding binding) =
            await CreateIntegratedBindingAsync(fixture, session, preparedRoot);

        await ApplyAuthorityLossAsync(fixture, session, approval, change);

        PreparedSourceIssueResult issue = await binding.IssueAsync(
            approval, PreparedSourceGrantLimits.Default, CancellationToken.None);

        Assert.Equal(expected, issue.Status);
        Assert.Null(issue.Context);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [InlineData("role", PreparedSourceOutcome.Denied)]
    [InlineData("membership", PreparedSourceOutcome.Denied)]
    [InlineData("stamp", PreparedSourceOutcome.Denied)]
    [InlineData("expiry", PreparedSourceOutcome.Denied)]
    [InlineData("root-row", PreparedSourceOutcome.Denied)]
    [InlineData("session", PreparedSourceOutcome.Denied)]
    [InlineData("revoked", PreparedSourceOutcome.Denied)]
    [InlineData("store", PreparedSourceOutcome.AuthorityUnavailable)]
    public async Task PreparedSourceFunction_RealOctoberAuthorityLossBetweenCallsReturnsNoPayload(
        string change,
        PreparedSourceOutcome expected)
    {
        RequireWindowsHostForIntegration();
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        await using PreparedSourceFixture preparedRoot = await PreparedSourceFixture.CreateAsync();
        (PreparedSourceApproval approval, IPreparedSourceReadContext context) =
            await IssueIntegratedContextAsync(fixture, session, preparedRoot);
        AIFunction readFunction = new PreparedSourceFunctions(context).Create()[1];

        await ApplyAuthorityLossAsync(fixture, session, approval, change);

        PreparedSourceResult result = Assert.IsType<PreparedSourceResult>(
            await ReadPreparedSourceAsync(readFunction));

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(approval.RepositoryId, result.RepositoryId);
        Assert.Null(result.Text);
        Assert.Null(result.ReadLocation);
        Assert.Null(result.Continuation);
        Assert.Empty(result.Entries);
        Assert.Empty(result.Matches);
        Assert.Equal(PreparedSourceContextLifetime.AuthorityClosed, context.Lifetime);
        await context.DisposeAsync();
        Assert.Equal(PreparedSourceContextLifetime.Disposed, context.Lifetime);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [InlineData("revoked", PreparedSourceOutcome.Denied)]
    [InlineData("store", PreparedSourceOutcome.AuthorityUnavailable)]
    public async Task PreparedSourceFunction_RechecksRealOctoberAuthorityImmediatelyBeforeDisclosure(
        string change,
        PreparedSourceOutcome expected)
    {
        RequireWindowsHostForIntegration();
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        await using IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        await using PreparedSourceFixture preparedRoot = await PreparedSourceFixture.CreateAsync();
        int checks = 0;
        (PreparedSourceApproval approval, PreparedSourceBinding binding) =
            await CreateIntegratedBindingAsync(fixture, session, preparedRoot, async (check, requested, _) =>
            {
                Interlocked.Exchange(ref checks, check);
                if (check == 2)
                {
                    if (change == "revoked")
                    {
                        OctoberRevocationStatus revoked = await fixture.Authority.RevokeSourceApprovalAsync(
                            session, TargetId, requested.ApprovalIdentity, CancellationToken.None);
                        Assert.Equal(OctoberRevocationStatus.Revoked, revoked);
                    }
                    else
                    {
                        await using AsyncServiceScope outageScope = fixture.Provider.CreateAsyncScope();
                        OctoberDbContext october = outageScope.ServiceProvider
                            .GetRequiredService<OctoberDbContext>();
                        await october.Database.ExecuteSqlRawAsync("DROP TABLE OctoberPreparedSourceApprovals;");
                    }
                }
            });

        PreparedSourceIssueResult issue = await binding.IssueAsync(
            approval, PreparedSourceGrantLimits.Default, CancellationToken.None);
        Assert.Equal(PreparedSourceIssueStatus.Issued, issue.Status);
        IPreparedSourceReadContext context = Assert.IsAssignableFrom<IPreparedSourceReadContext>(issue.Context);
        AIFunction readFunction = new PreparedSourceFunctions(context).Create()[1];

        PreparedSourceResult result = Assert.IsType<PreparedSourceResult>(
            await ReadPreparedSourceAsync(readFunction));

        Assert.Equal(expected, result.Outcome);
        Assert.Null(result.Text);
        Assert.Null(result.ReadLocation);
        Assert.Null(result.Continuation);
        Assert.Equal(PreparedSourceContextLifetime.AuthorityClosed, context.Lifetime);
        Assert.Equal(3, checks);
        await context.DisposeAsync();
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task PreparedSourceFunction_CancellationSettlesBorrowedCheckBeforeContextAndSessionDisposal()
    {
        RequireWindowsHostForIntegration();
        await using SelectionFixture fixture = await SelectionFixture.CreateAsync();
        IOctoberOperatorSession session = await fixture.AuthenticateAsync();
        await using PreparedSourceFixture preparedRoot = await PreparedSourceFixture.CreateAsync();
        TaskCompletionSource enteredCheck = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseCheck = new(TaskCreationOptions.RunContinuationsAsynchronously);
        (PreparedSourceApproval approval, PreparedSourceBinding binding) =
            await CreateIntegratedBindingAsync(fixture, session, preparedRoot, async (check, _, _) =>
            {
                if (check == 2)
                {
                    enteredCheck.TrySetResult();
                    await releaseCheck.Task;
                }
            });

        PreparedSourceIssueResult issue = await binding.IssueAsync(
            approval, PreparedSourceGrantLimits.Default, CancellationToken.None);
        Assert.Equal(PreparedSourceIssueStatus.Issued, issue.Status);
        IPreparedSourceReadContext context = Assert.IsAssignableFrom<IPreparedSourceReadContext>(issue.Context);
        AIFunction readFunction = new PreparedSourceFunctions(context).Create()[1];
        using var cancellation = new CancellationTokenSource();
        Task<object?> borrowedRead = ReadPreparedSourceAsync(readFunction, cancellation.Token).AsTask();

        try
        {
            await enteredCheck.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            releaseCheck.TrySetResult();
            await borrowedRead.WaitAsync(TimeSpan.FromSeconds(5));
            await context.DisposeAsync();
            await session.DisposeAsync();
            throw;
        }

        cancellation.Cancel();
        releaseCheck.TrySetResult();
        PreparedSourceResult cancelled = Assert.IsType<PreparedSourceResult>(
            await borrowedRead.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Equal(PreparedSourceOutcome.Cancelled, cancelled.Outcome);
        Assert.Equal(approval.RepositoryId, cancelled.RepositoryId);
        Assert.Null(cancelled.Text);
        Assert.Null(cancelled.ReadLocation);
        Assert.Null(cancelled.Continuation);
        Assert.Equal(PreparedSourceContextLifetime.ExpiredOrCancelled, context.Lifetime);

        await context.DisposeAsync();
        Assert.Equal(PreparedSourceContextLifetime.Disposed, context.Lifetime);
        await session.DisposeAsync();
    }

    private static async Task<(PreparedSourceApproval Approval, IPreparedSourceReadContext Context)>
        IssueIntegratedContextAsync(
            SelectionFixture fixture,
            IOctoberOperatorSession session,
            PreparedSourceFixture preparedRoot)
    {
        (PreparedSourceApproval approval, PreparedSourceBinding binding) =
            await CreateIntegratedBindingAsync(fixture, session, preparedRoot);

        PreparedSourceIssueResult contextIssue = await binding.IssueAsync(
            approval, PreparedSourceGrantLimits.Default, CancellationToken.None);
        Assert.Equal(PreparedSourceIssueStatus.Issued, contextIssue.Status);
        IPreparedSourceReadContext context = Assert.IsAssignableFrom<IPreparedSourceReadContext>(
            contextIssue.Context);
        Assert.Equal(approval.RepositoryId, context.RepositoryId);
        return (approval, context);
    }

    private static async Task<(PreparedSourceApproval Approval, PreparedSourceBinding Binding)>
        CreateIntegratedBindingAsync(
            SelectionFixture fixture,
            IOctoberOperatorSession session,
            PreparedSourceFixture preparedRoot,
            Func<int, PreparedSourceApproval, CancellationToken, Task>? afterAuthorityCheck = null)
    {
        IOctoberHistoricalBoundSetupData boundSetup =
            (await CreateCommittedBoundSetupAsync(fixture, session)).BoundSetup;
        OctoberSourceApprovalResult issued = await fixture.Authority.IssueSourceApprovalAsync(
            session, boundSetup, fixture.RepositoryId, CancellationToken.None);
        Assert.Equal(OctoberSourceApprovalStatus.Issued, issued.Status);
        PreparedSourceApproval approval = Assert.IsAssignableFrom<PreparedSourceApproval>(issued.Approval);

        PreparedSourceBinding? binding = null;
        int checkCount = 0;
        var verifier = new OctoberPreparedSourceAuthorityVerifier(
            TargetId,
            async (target, requested, cancellationToken) =>
            {
                PreparedSourceAuthorityResult current = await fixture.Authority.VerifySourceApprovalAsync(
                    session, target, requested, cancellationToken);
                if (afterAuthorityCheck is not null)
                {
                    await afterAuthorityCheck(
                        Interlocked.Increment(ref checkCount), requested, cancellationToken);
                }

                return current;
            },
            requested => binding!.HasCurrentRootBinding(requested));
        binding = new PreparedSourceBinding(
            new PreparedSourceContextIssuer(verifier),
            TargetId,
            [new PreparedSourceRootBinding(
                TargetId,
                approval.BatchId,
                approval.RepositoryId,
                approval.RootBindingIdentity,
                preparedRoot.PreparedRootPath)]);

        return (approval, binding);
    }

    private static async Task ApplyAuthorityLossAsync(
        SelectionFixture fixture,
        IOctoberOperatorSession session,
        PreparedSourceApproval approval,
        string change)
    {
        switch (change)
        {
            case "role":
                await using (AsyncServiceScope identityScope = fixture.Provider.CreateAsyncScope())
                {
                    UserManager<ApplicationUser> users = identityScope.ServiceProvider
                        .GetRequiredService<UserManager<ApplicationUser>>();
                    ApplicationUser user = Assert.IsAssignableFrom<ApplicationUser>(
                        await users.FindByIdAsync(SubjectId));
                    IdentityResult removed = await users.RemoveFromRoleAsync(user, RoleNames.SystemOwner);
                    Assert.True(removed.Succeeded,
                        string.Join("; ", removed.Errors.Select(error => error.Code)));
                }
                break;
            case "membership":
                await using (VulgataDbContext management = await fixture.CreateManagementContextAsync())
                {
                    Guid systemId = Assert.IsType<Guid>(fixture.SystemId);
                    SystemOwnerAssignment assignment = await management.SystemOwnerAssignments
                        .SingleAsync(value => value.SystemId == systemId && value.UserId == SubjectId);
                    management.SystemOwnerAssignments.Remove(assignment);
                    await management.SaveChangesAsync();
                }
                break;
            case "stamp":
                await using (AsyncServiceScope identityScope = fixture.Provider.CreateAsyncScope())
                {
                    UserManager<ApplicationUser> users = identityScope.ServiceProvider
                        .GetRequiredService<UserManager<ApplicationUser>>();
                    ApplicationUser user = Assert.IsAssignableFrom<ApplicationUser>(
                        await users.FindByIdAsync(SubjectId));
                    IdentityResult updated = await users.UpdateSecurityStampAsync(user);
                    Assert.True(updated.Succeeded,
                        string.Join("; ", updated.Errors.Select(error => error.Code)));
                }
                break;
            case "expiry":
                fixture.Clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));
                break;
            case "root-row":
                await using (AsyncServiceScope octoberScope = fixture.Provider.CreateAsyncScope())
                {
                    OctoberDbContext october = octoberScope.ServiceProvider.GetRequiredService<OctoberDbContext>();
                    OctoberRepositoryRecord row = await october.RepositorySelections.SingleAsync(value =>
                        value.BatchId == approval.BatchId && value.RepositoryId == approval.RepositoryId);
                    row.RootBindingIdentity = "new-current-root-version";
                    await october.SaveChangesAsync();
                }
                break;
            case "session":
                await session.DisposeAsync();
                break;
            case "revoked":
                Assert.Equal(OctoberRevocationStatus.Revoked,
                    await fixture.Authority.RevokeSourceApprovalAsync(
                        session, TargetId, approval.ApprovalIdentity, CancellationToken.None));
                break;
            case "store":
                await using (AsyncServiceScope outageScope = fixture.Provider.CreateAsyncScope())
                {
                    OctoberDbContext october = outageScope.ServiceProvider.GetRequiredService<OctoberDbContext>();
                    await october.Database.ExecuteSqlRawAsync("DROP TABLE OctoberPreparedSourceApprovals;");
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(change), change, "Unknown authority-loss scenario.");
        }
    }

    private static ValueTask<object?> ReadPreparedSourceAsync(
        AIFunction readFunction,
        CancellationToken cancellationToken = default) =>
        readFunction.InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["relativePath"] = "notes.oddlangx",
                ["byteOffset"] = 0L,
                ["byteCount"] = 6,
            }),
            cancellationToken);

    private static void RequireWindowsHostForIntegration()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw Xunit.Sdk.SkipException.ForSkip(
                "Windows handle-relative prepared-source access is not qualified on this platform.");
        }
    }
}
