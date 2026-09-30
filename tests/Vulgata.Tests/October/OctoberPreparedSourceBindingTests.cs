using System.Collections.Concurrent;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Source;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.PreparedSource;
using Vulgata.Tests.TestInfrastructure;

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

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
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
        OctoberWebApprovalCheck webCheck)
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
