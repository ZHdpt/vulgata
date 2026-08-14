namespace Vulgata.Web.Components.Pages;

internal interface IChatRevealDelay
{
    Task DelayAsync(CancellationToken cancellationToken);
}

internal sealed class ChatRevealDelay : IChatRevealDelay
{
    private static readonly TimeSpan _interval = TimeSpan.FromMilliseconds(25);

    public Task DelayAsync(CancellationToken cancellationToken) =>
        Task.Delay(_interval, cancellationToken);
}
