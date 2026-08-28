namespace Vulgata.Core.DomainServices;

public interface IChatAdvisoryContextReader
{
    Task<ChatAdvisoryContextReadResult> ReadAsync(
        AuthorizedChatScope scope,
        CancellationToken cancellationToken = default);
}

public sealed record AuthorizedChatScope(
    Guid SystemId,
    IReadOnlyCollection<Guid> RepositoryIds,
    RepositoryAccessScope AccessScope);

public enum ChatAdvisoryContextReadResultKind
{
    Succeeded,
    NotFoundOrUnauthorized,
    InconsistentCurrentEvidence,
}

public enum ChatAdvisoryContextSourceKind
{
    Global,
    System,
    Repository,
}

public sealed record ChatAdvisoryContextSourceUnit(
    ChatAdvisoryContextSourceKind Kind,
    Guid SourceId,
    DateTimeOffset UpdatedAt,
    string Text);

public sealed record ChatAdvisoryContextSnapshot(
    IReadOnlyList<ChatAdvisoryContextSourceUnit> SourceUnits,
    string ComposedText,
    IReadOnlyList<string> Terms,
    int EncodedUtf8Bytes,
    string AuthorityFingerprint);

public sealed record ChatAdvisoryContextReadResult(
    ChatAdvisoryContextReadResultKind Kind,
    ChatAdvisoryContextSnapshot? Snapshot)
{
    public static ChatAdvisoryContextReadResult Succeeded(
        ChatAdvisoryContextSnapshot snapshot) =>
        new(ChatAdvisoryContextReadResultKind.Succeeded, snapshot);

    public static ChatAdvisoryContextReadResult NotFoundOrUnauthorized() =>
        new(ChatAdvisoryContextReadResultKind.NotFoundOrUnauthorized, null);

    public static ChatAdvisoryContextReadResult InconsistentCurrentEvidence() =>
        new(ChatAdvisoryContextReadResultKind.InconsistentCurrentEvidence, null);
}
