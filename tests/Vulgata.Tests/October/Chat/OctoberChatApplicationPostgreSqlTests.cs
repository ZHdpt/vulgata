using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Tests.Migrations;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Services.OctoberChat;
using static Vulgata.Tests.October.Chat.OctoberChatApplicationTests;

namespace Vulgata.Tests.October.Chat;

/// <summary>Real application/store/registry with runner-owned PostgreSQL; physical provider traffic stays controlled.</summary>
public sealed class OctoberChatApplicationPostgreSqlTests
{
    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task Application_TwoTurnsPreserveExactPostgreSqlReceiptsAndOwnerHistoryAsync()
    {
        await using var owned = await OwnedApplication.CreateAsync();
        var fixture = owned.Fixture;
        fixture.EnqueueComplete(Text("持久第一回答。") + Stop + Done);
        fixture.EnqueueComplete(Text("持久第二回答。") + Stop + Done);
        fixture.Application.Attach(callback => callback().AsTask());
        Assert.True(await fixture.Application.OpenAsync(fixture.Conversation));
        Assert.Equal(0, fixture.Registry.ActiveRegistrations);
        var first = Assert.IsType<OctoberChatCoordinationResult>(await fixture.Application.SubmitAsync("持久第一问题")
            .WaitAsync(TimeSpan.FromSeconds(20)));
        var firstReceipt = Assert.IsType<DialogueTerminalReceipt>(first.Terminal!.Receipt);
        Assert.Equal(DialogueTerminalState.Completed, firstReceipt.TerminalState);
        Assert.Equal(0, fixture.Registry.ActiveRegistrations);
        var history = await fixture.ReadHistoryAsync();
        var prior = Assert.Single(history.Turns);
        Assert.Equal(firstReceipt.TurnId, prior.TurnId);
        Assert.Equal("持久第一问题", prior.Question);
        Assert.Equal("持久第一回答。", prior.AnswerText);

        var second = Assert.IsType<OctoberChatCoordinationResult>(await fixture.Application.SubmitAsync("持久第二问题")
            .WaitAsync(TimeSpan.FromSeconds(20)));
        var secondReceipt = Assert.IsType<DialogueTerminalReceipt>(second.Terminal!.Receipt);
        Assert.Equal(DialogueTerminalState.Completed, secondReceipt.TerminalState);
        Assert.NotEqual(firstReceipt.TurnId, secondReceipt.TurnId);
        Assert.Equal(firstReceipt.Sequence + 1, secondReceipt.Sequence);
        Assert.Equal(firstReceipt.Fence + 1, secondReceipt.Fence);
        var reopened = await fixture.ReadReceiptAsync(firstReceipt);
        Assert.Equal(DialogueStatus.Existing, reopened.Status);
        Assert.Equal(firstReceipt, reopened.Receipt);
        Assert.Equal(prior.Question, reopened.Projection!.Question);
        Assert.Equal(prior.AnswerText, reopened.Projection.AnswerText);
        Assert.Equal(secondReceipt, (await fixture.ReadReceiptAsync(secondReceipt)).Receipt);
        Assert.Equal(new[] { firstReceipt.TurnId, secondReceipt.TurnId },
            (await fixture.ReadHistoryAsync()).Turns.Select(turn => turn.TurnId));
        Assert.Equal(2, fixture.RequestBodies.Count);
        using var request = JsonDocument.Parse(fixture.RequestBodies.Last());
        var users = request.RootElement.GetProperty("messages").EnumerateArray()
            .Where(message => message.GetProperty("role").GetString() == "user").Select(MessageText).ToArray();
        Assert.Equal(new[] { Encoding.UTF8.GetString(new OctoberDialoguePayloadCodec()
            .EncodeHistoryProjectionUtf8(history.Turns).AsSpan()), "持久第二问题" }, users);
        Assert.Equal(0, fixture.Registry.ActiveRegistrations);
        Assert.Equal(0, fixture.Requests.ActiveRequests);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task Application_PostgreSqlLostAcknowledgementUsesExactLookupWithoutOverlappingOrRedispatchingAsync()
    {
        await using var owned = await OwnedApplication.CreateAsync();
        var fixture = owned.Fixture;
        fixture.LoseSaveAcknowledgement = true;
        fixture.EnqueueComplete(Text("提交已完成，确认丢失。") + Stop + Done);
        fixture.Application.Attach(callback => callback().AsTask());
        Assert.True(await fixture.Application.OpenAsync(fixture.Conversation));
        Assert.Equal(0, fixture.Registry.ActiveRegistrations);
        var uncertain = Assert.IsType<OctoberChatCoordinationResult>(await fixture.Application.SubmitAsync("保留原保存操作")
            .WaitAsync(TimeSpan.FromSeconds(20)));
        Assert.Equal(DialogueStatus.Indeterminate, uncertain.Terminal!.Status);
        Assert.True(fixture.Application.IsSaveUncertain);
        Assert.NotNull(uncertain.PendingSave);
        Assert.NotNull(fixture.ActualSavedReceipt);
        Assert.Equal(1, fixture.Registry.ActiveRegistrations);
        Assert.Null(await fixture.Application.SubmitAsync("不能再提交"));
        Assert.Single(fixture.RequestBodies);
        Assert.Equal(1, fixture.SaveCalls);

        var recovered = Assert.IsType<OctoberChatCoordinationResult>(await fixture.Application.ReconcileAsync()
            .WaitAsync(TimeSpan.FromSeconds(20)));
        Assert.Equal(DialogueStatus.Existing, recovered.Terminal!.Status);
        Assert.Equal(fixture.ActualSavedReceipt, recovered.Terminal.Receipt);
        Assert.Equal(uncertain.PendingSave.SaveOperationId, recovered.Terminal.Receipt!.SaveOperationId);
        Assert.Equal(uncertain.PendingSave.OutcomeDigest, recovered.Terminal.Receipt.OutcomeDigest);
        Assert.Equal(1, fixture.SaveCalls);
        Assert.Equal(1, fixture.LookupCalls);
        Assert.Single(fixture.RequestBodies);
        Assert.False(fixture.Application.IsSaveUncertain);
        Assert.Equal(0, fixture.Registry.ActiveRegistrations);
        Assert.Single((await fixture.ReadHistoryAsync()).Turns);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task Application_PostgreSqlExplicitCancellationRetainsPhysicalWorkUntilOwnerCleanupAsync()
    {
        await using var owned = await OwnedApplication.CreateAsync();
        var fixture = owned.Fixture;
        var body = fixture.Enqueue(Text("已收到部分回答。"), Stop + Done);
        fixture.Application.Attach(callback => callback().AsTask());
        Assert.True(await fixture.Application.OpenAsync(fixture.Conversation));
        Assert.Equal(0, fixture.Registry.ActiveRegistrations);
        int disposedBefore = fixture.DisposedScopes;
        var pending = fixture.Application.SubmitAsync("明确取消");
        try
        {
            await body.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            fixture.Application.Cancel();
            Assert.False(pending.IsCompleted);
            Assert.True(fixture.Application.IsBusy);
            Assert.Equal(1, fixture.Registry.ActiveRegistrations);
            Assert.Equal(1, fixture.Requests.ActiveRequests);
            Assert.Equal(disposedBefore, fixture.DisposedScopes);
            Assert.Null(await fixture.Application.SubmitAsync("禁止重叠请求"));
            Assert.Single(fixture.RequestBodies);
            body.Release.TrySetResult();
            var ended = Assert.IsType<OctoberChatCoordinationResult>(await pending.WaitAsync(TimeSpan.FromSeconds(20)));
            Assert.Equal(DialogueTerminalState.Interrupted, ended.Terminal!.Receipt!.TerminalState);
            Assert.Equal(DialogueTerminalReason.UserCancelled, ended.Terminal.Receipt.Reason);
            Assert.Equal(ended.Terminal.Receipt, (await fixture.ReadReceiptAsync(ended.Terminal.Receipt)).Receipt);
            Assert.Empty((await fixture.ReadHistoryAsync()).Turns);
            Assert.True(fixture.DisposedScopes > disposedBefore);
            Assert.Equal(0, fixture.Registry.ActiveRegistrations);
            Assert.Equal(0, fixture.Requests.ActiveRequests);
            Assert.False(body.DisposedWhileReading);
        }
        finally
        {
            body.Release.TrySetResult();
            _ = await Record.ExceptionAsync(() => pending.WaitAsync(TimeSpan.FromSeconds(20)));
        }
    }

    private sealed class OwnedApplication(DisposablePostgreSqlDatabase database, ApplicationFixture fixture) : IAsyncDisposable
    {
        public ApplicationFixture Fixture => fixture;
        public static async Task<OwnedApplication> CreateAsync()
        {
            // This guard precedes all resource creation. Discovery cannot provision a database.
            var database = await DisposablePostgreSqlDatabase.CreateAsync(TestResourceScope.RequireAdministrator());
            try
            {
                TestResourceScope.ValidateOwnedTarget(database.ConnectionString);
                var options = new DbContextOptionsBuilder<OctoberDbContext>()
                    .UseNpgsql(database.ConnectionString, postgres => postgres.CommandTimeout(30)).Options;
                await using (var context = new OctoberDbContext(options)) await context.Database.MigrateAsync();
                return new(database, await ApplicationFixture.CreateAsync(options));
            }
            catch { await database.DisposeAsync(); throw; }
        }
        public async ValueTask DisposeAsync()
        {
            try { await Fixture.DisposeAsync(); }
            finally { await database.DisposeAsync(); }
        }
    }
}
