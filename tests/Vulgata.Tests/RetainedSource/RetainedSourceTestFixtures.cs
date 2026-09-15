namespace Vulgata.Tests.RetainedSource;

/// <summary>
/// Evidence categories for this story's source-only test boundary. These labels
/// describe what a test proves and do not imply Scan, Knowledge, Chat, tool, or
/// deployment qualification.
/// </summary>
internal enum RetainedSourceEvidenceKind
{
    Success,
    Limitation,
    Denial,
    Cancellation,
    Failure,
}

internal sealed record RetainedSourceTestEvidence(
    RetainedSourceEvidenceKind Kind,
    string Code,
    string Description)
{
    internal const string Scope = "retained-source";

    internal const string SourceOnlyBoundary = "source-only";

    internal string EvidenceScope => Scope;

    internal string Boundary => SourceOnlyBoundary;
}

internal static class RetainedSourceEvidence
{
    internal static RetainedSourceTestEvidence Success(
        string code,
        string description = "Retained source capability succeeded.") =>
        Create(RetainedSourceEvidenceKind.Success, code, description);

    internal static RetainedSourceTestEvidence Limitation(
        string code,
        string description = "Retained source capability reported a supported limitation.") =>
        Create(RetainedSourceEvidenceKind.Limitation, code, description);

    internal static RetainedSourceTestEvidence Denial(
        string code,
        string description = "Retained source capability denied the request.") =>
        Create(RetainedSourceEvidenceKind.Denial, code, description);

    internal static RetainedSourceTestEvidence Cancellation(
        string code,
        string description = "Retained source capability was cancelled.") =>
        Create(RetainedSourceEvidenceKind.Cancellation, code, description);

    internal static RetainedSourceTestEvidence Failure(
        string code,
        string description = "Retained source capability failed within the source boundary.") =>
        Create(RetainedSourceEvidenceKind.Failure, code, description);

    private static RetainedSourceTestEvidence Create(
        RetainedSourceEvidenceKind kind,
        string code,
        string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return new(kind, code, description);
    }
}
