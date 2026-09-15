using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Vulgata.Infrastructure.Source;

public sealed class RetainedSourceCapturePolicy
{
    public const string SectionName = "SourceStorage";

    private RetainedSourceCapturePolicy(
        string rootPath,
        long maximumEntries,
        long maximumBlobBytes,
        long maximumAggregateBlobBytes,
        TimeSpan captureDeadline,
        TimeSpan operationStaleAfter,
        int verificationBufferBytes,
        int maximumReadOperations,
        int maximumMetadataEntries,
        long maximumReturnedBytes,
        int maximumEntriesPerOperation,
        int maximumBytesPerOperation,
        TimeSpan readDeadline)
    {
        RootPath = rootPath;
        MaximumEntries = maximumEntries;
        MaximumBlobBytes = maximumBlobBytes;
        MaximumAggregateBlobBytes = maximumAggregateBlobBytes;
        CaptureDeadline = captureDeadline;
        OperationStaleAfter = operationStaleAfter;
        VerificationBufferBytes = verificationBufferBytes;
        MaximumReadOperations = maximumReadOperations;
        MaximumMetadataEntries = maximumMetadataEntries;
        MaximumReturnedBytes = maximumReturnedBytes;
        MaximumEntriesPerOperation = maximumEntriesPerOperation;
        MaximumBytesPerOperation = maximumBytesPerOperation;
        ReadDeadline = readDeadline;
    }

    public string RootPath { get; }

    public long MaximumEntries { get; }

    public long MaximumBlobBytes { get; }

    public long MaximumAggregateBlobBytes { get; }

    public TimeSpan CaptureDeadline { get; }

    public TimeSpan OperationStaleAfter { get; }

    public int VerificationBufferBytes { get; }

    public int MaximumReadOperations { get; }

    public int MaximumMetadataEntries { get; }

    public long MaximumReturnedBytes { get; }

    public int MaximumEntriesPerOperation { get; }

    public int MaximumBytesPerOperation { get; }

    public TimeSpan ReadDeadline { get; }

    public static RetainedSourceCapturePolicy Create(
        IConfiguration configuration,
        string contentRootPath,
        params string[] excludedStorageRoots)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        string rootPath = NormalizeRequiredRoot(
            configuration[$"{SectionName}:RootPath"],
            contentRootPath);
        EnsureSeparated(rootPath, excludedStorageRoots);

        long maximumEntries = RequiredPositiveInt64(configuration, "MaximumEntries");
        long maximumBlobBytes = RequiredPositiveInt64(configuration, "MaximumBlobBytes");
        long maximumAggregateBlobBytes = RequiredPositiveInt64(
            configuration,
            "MaximumAggregateBlobBytes");
        if (maximumBlobBytes > maximumAggregateBlobBytes)
        {
            throw Invalid("MaximumBlobBytes must not exceed MaximumAggregateBlobBytes.");
        }

        long captureDeadlineSeconds = RequiredPositiveInt64(
            configuration,
            "CaptureDeadlineSeconds");
        long operationStaleAfterMinutes = RequiredPositiveInt64(
            configuration,
            "OperationStaleAfterMinutes");
        long verificationBufferBytes = RequiredPositiveInt64(
            configuration,
            "VerificationBufferBytes");
        if (verificationBufferBytes is < 4_096 or > 1_048_576)
        {
            throw Invalid("VerificationBufferBytes must be between 4096 and 1048576.");
        }

        int maximumReadOperations = OptionalPositiveInt32(
            configuration,
            "Read:MaximumOperations",
            64);
        int maximumMetadataEntries = OptionalPositiveInt32(
            configuration,
            "Read:MaximumMetadataEntries",
            4_096);
        long maximumReturnedBytes = OptionalPositiveInt64(
            configuration,
            "Read:MaximumReturnedBytes",
            4_194_304);
        int maximumEntriesPerOperation = OptionalPositiveInt32(
            configuration,
            "Read:MaximumEntriesPerOperation",
            512);
        int maximumBytesPerOperation = OptionalPositiveInt32(
            configuration,
            "Read:MaximumBytesPerOperation",
            1_048_576);
        long readDeadlineSeconds = OptionalPositiveInt64(
            configuration,
            "Read:DeadlineSeconds",
            60);
        if (maximumEntriesPerOperation > maximumMetadataEntries)
        {
            throw Invalid(
                "Read MaximumEntriesPerOperation must not exceed MaximumMetadataEntries.");
        }

        if (maximumBytesPerOperation > maximumReturnedBytes)
        {
            throw Invalid(
                "Read MaximumBytesPerOperation must not exceed MaximumReturnedBytes.");
        }

        return new(
            rootPath,
            maximumEntries,
            maximumBlobBytes,
            maximumAggregateBlobBytes,
            RepresentableSeconds(captureDeadlineSeconds, "CaptureDeadlineSeconds"),
            RepresentableMinutes(operationStaleAfterMinutes, "OperationStaleAfterMinutes"),
            checked((int)verificationBufferBytes),
            maximumReadOperations,
            maximumMetadataEntries,
            maximumReturnedBytes,
            maximumEntriesPerOperation,
            maximumBytesPerOperation,
            RepresentableSeconds(readDeadlineSeconds, "Read:DeadlineSeconds"));
    }

    private static string NormalizeRequiredRoot(string? configuredPath, string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath) || string.IsNullOrWhiteSpace(contentRootPath))
        {
            throw Invalid("RootPath is required.");
        }

        try
        {
            string expanded = Environment.ExpandEnvironmentVariables(configuredPath.Trim());
            string rooted = Path.IsPathRooted(expanded)
                ? expanded
                : Path.Combine(contentRootPath, expanded);
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(rooted));
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw Invalid("RootPath is invalid.");
        }
    }

    private static void EnsureSeparated(string sourceRoot, IEnumerable<string> excludedRoots)
    {
        foreach (string excludedRoot in excludedRoots.Where(static root =>
                     !string.IsNullOrWhiteSpace(root)))
        {
            string normalizedExcluded;
            try
            {
                normalizedExcluded = Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(excludedRoot));
            }
            catch (Exception exception) when (
                exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                throw Invalid("A tool storage root is invalid.");
            }

            if (Contains(sourceRoot, normalizedExcluded) || Contains(normalizedExcluded, sourceRoot))
            {
                throw Invalid("RootPath must be separate from tool storage roots.");
            }
        }
    }

    private static bool Contains(string root, string candidate)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (string.Equals(root, candidate, comparison))
        {
            return true;
        }

        string prefix = Path.EndsInDirectorySeparator(root)
            ? root
            : root + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, comparison);
    }

    private static long RequiredPositiveInt64(IConfiguration configuration, string relativeKey)
    {
        string key = $"{SectionName}:{relativeKey}";
        string? value = configuration[key];
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed) ||
            parsed <= 0)
        {
            throw Invalid($"{relativeKey} must be a positive integer.");
        }

        return parsed;
    }

    private static long OptionalPositiveInt64(
        IConfiguration configuration,
        string relativeKey,
        long defaultValue)
    {
        string? value = configuration[$"{SectionName}:{relativeKey}"];
        if (value is null)
        {
            return defaultValue;
        }

        return long.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long parsed) && parsed > 0
            ? parsed
            : throw Invalid($"{relativeKey} must be a positive integer.");
    }

    private static int OptionalPositiveInt32(
        IConfiguration configuration,
        string relativeKey,
        int defaultValue)
    {
        long value = OptionalPositiveInt64(configuration, relativeKey, defaultValue);
        return value <= int.MaxValue
            ? (int)value
            : throw Invalid($"{relativeKey} exceeds its supported bound.");
    }

    private static TimeSpan RepresentableSeconds(long value, string relativeKey)
    {
        try
        {
            return TimeSpan.FromSeconds(value);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw Invalid($"{relativeKey} exceeds its supported duration.");
        }
    }

    private static TimeSpan RepresentableMinutes(long value, string relativeKey)
    {
        try
        {
            return TimeSpan.FromMinutes(value);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw Invalid($"{relativeKey} exceeds its supported duration.");
        }
    }

    private static InvalidOperationException Invalid(string reason) =>
        new($"source_configuration_invalid: {reason}");
}
