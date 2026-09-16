using System.Collections.Concurrent;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;
using Vulgata.Web.Data;
using Xunit.Abstractions;

namespace Vulgata.Tests.RetainedSource;

[Collection(RetainedSourceProcessGlobalCollection.Name)]
public sealed class RetainedSourceLoggingTests
{
    private readonly ITestOutputHelper _output;

    public RetainedSourceLoggingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task SourceDiagnostics_WhenCaptureReopenDenialAndDeletionConflictOccur_AreSanitized()
    {
        const string sourceMarker = "synthetic-source-content-marker";
        const string originMarker = "synthetic-origin-marker";
        const string unsafePathMarker = "synthetic-unsafe-path-marker";
        const string secretMarker = "synthetic-secret-marker";
        var logger = new RetainedSourceRecordingLoggerProvider();
        var forbidden = new[]
        {
            sourceMarker,
            originMarker,
            unsafePathMarker,
            secretMarker,
        };
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync(
                new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal)
                {
                    ["src/synthetic-path-marker.txt"] =
                        Encoding.UTF8.GetBytes(sourceMarker),
                });
        Action<IServiceCollection> configure = services =>
            services.AddSingleton<ILoggerProvider>(logger);
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                configureServices: configure);
        string[] logForbiddenMarkers =
        [
            .. forbidden,
            fixture.SourceStorageRoot,
            fixture.Repository.RootPath,
        ];
        IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);

        SourceCaptureResult capture = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef);
        Assert.Equal(SourceCaptureStatus.Completed, capture.Status);
        Assert.Equal("source_completed", capture.Code);
        string revision = capture.ProfiledRevision ??
                          throw new InvalidOperationException("The capture did not return a revision.");
        await using VulgataDbContext snapshotContext = appFixture.Scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>()
            .CreateDbContext();
        SourceSnapshot snapshot = await snapshotContext.SourceSnapshots
            .SingleAsync(item => item.RepositoryId == fixture.RepositoryId);
        logForbiddenMarkers =
        [
            .. logForbiddenMarkers,
            snapshot.InternalStorageKey,
            $"snapshots/{fixture.RepositoryId:N}/{revision}",
        ];
        OpenSourceReadContextResult opened = await application.OpenReadContextAsync(
            fixture.RepositoryId,
            revision,
            new SourceReadGrantRequest(maximumOperations: 10, maximumReturnedBytes: 4096));
        Assert.Equal(SourceReadStatus.Succeeded, opened.Status);
        await using ISourceReadContext context = opened.Context!;
        SourceBytesResult bytes = await context.ReadBytesAsync(
            "src/synthetic-path-marker.txt",
            0,
            1024);
        Assert.Equal(SourceReadStatus.Succeeded, bytes.Status);
        Assert.Equal(sourceMarker, Encoding.UTF8.GetString(bytes.Content.Span));
        SourceEntryResult unsafePath = await context.GetEntryAsync(
            $"../{unsafePathMarker}");
        Assert.Equal(SourceReadStatus.Denied, unsafePath.Status);
        Assert.Equal("source_denied", unsafePath.Code);
        Assert.Null(unsafePath.Entry);

        await RetainedSourceCaptureApplicationFixture.SetOriginAsync(
            appFixture.Database.ConnectionString,
            fixture.RepositoryId,
            $"http://{secretMarker}:synthetic-password@127.0.0.1:1/{originMarker}.git");
        SourceCaptureResult acquisitionFailure = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef);
        Assert.Equal(SourceCaptureStatus.AcquisitionFailure, acquisitionFailure.Status);
        Assert.Equal("source_acquisition_failed", acquisitionFailure.Code);

        RepositoryMutationResult<bool> deletionConflict =
            await appFixture.Scope.ServiceProvider
                .GetRequiredService<IRepositoryManagementCoordinator>()
                .DeleteAsync(
                    appFixture.SystemId,
                    fixture.RepositoryId,
                    appFixture.Scope.ServiceProvider.GetRequiredService<IUserContext>());
        Assert.Equal(RepositoryMutationOutcome.RetainedSourceConflict, deletionConflict.Outcome);
        Assert.Contains("保留", deletionConflict.Message ?? string.Empty, StringComparison.Ordinal);

        await using RetainedSourceAttachedApplication freshApplication =
            RetainedSourceCaptureApplicationFixture.CreateAttached(
                appFixture.Database.ConnectionString,
                fixture.SourceStorageRoot,
                appFixture.UserId,
                createAuxiliaryRoots: false,
                configureServices: configure);
        IRetainedSourceApplication fresh = freshApplication.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        OpenSourceReadContextResult reopened = await fresh.OpenReadContextAsync(
            fixture.RepositoryId,
            revision,
            new SourceReadGrantRequest(maximumOperations: 8, maximumReturnedBytes: 1024));
        Assert.Equal(SourceReadStatus.Succeeded, reopened.Status);
        await using ISourceReadContext reopenedContext = reopened.Context!;
        SourceBytesResult reopenedBytes = await reopenedContext.ReadBytesAsync(
            "src/synthetic-path-marker.txt",
            0,
            1024);
        Assert.Equal(SourceReadStatus.Succeeded, reopenedBytes.Status);
        Assert.Equal(bytes.Content.ToArray(), reopenedBytes.Content.ToArray());

        await RetainedSourceCaptureApplicationFixture.RevokeAuthorityAsync(
            appFixture.Database.ConnectionString,
            appFixture.SystemId,
            appFixture.UserId);
        SourceEntryResult denied = await reopenedContext.GetEntryAsync(
            "src/synthetic-path-marker.txt");
        Assert.Equal(SourceReadStatus.Denied, denied.Status);
        Assert.Equal("source_denied", denied.Code);
        Assert.Null(denied.Entry);
        OpenSourceReadContextResult deniedOpen = await fresh.OpenReadContextAsync(
            fixture.RepositoryId,
            revision,
            new SourceReadGrantRequest());
        Assert.Equal(SourceReadStatus.Denied, deniedOpen.Status);
        Assert.Equal("source_denied", deniedOpen.Code);
        Assert.Null(deniedOpen.Context);

        string[] diagnostics =
        [
            capture.Code,
            opened.Code,
            bytes.Code,
            unsafePath.Code,
            acquisitionFailure.Code,
            deletionConflict.Message ?? string.Empty,
            reopened.Code,
            reopenedBytes.Code,
            denied.Code,
            deniedOpen.Code,
        ];
        Assert.DoesNotContain(
            diagnostics,
            diagnostic => logForbiddenMarkers.Any(marker =>
                diagnostic.Contains(marker, StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(
            logger.Entries,
            entry => logForbiddenMarkers.Any(marker =>
                entry.Message.Contains(marker, StringComparison.OrdinalIgnoreCase) ||
                entry.State.Contains(marker, StringComparison.OrdinalIgnoreCase) ||
                (entry.ExceptionText?.Contains(marker, StringComparison.OrdinalIgnoreCase) ?? false)));
        Assert.DoesNotContain(
            logger.Entries,
            entry => logForbiddenMarkers.Any(marker =>
                entry.Category.Contains(marker, StringComparison.OrdinalIgnoreCase)));
        Assert.NotEmpty(logger.Entries);
        string[] loggerCategories = logger.Entries
            .Select(entry => entry.Category)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        _output.WriteLine(
            $"capture={capture.Status}/{capture.Code}; " +
            $"read={bytes.Status}/{bytes.Code}; " +
            $"unsafePath={unsafePath.Status}/{unsafePath.Code}; " +
            $"acquisition={acquisitionFailure.Status}/{acquisitionFailure.Code}; " +
            $"deletionConflict={deletionConflict.Outcome}; " +
            $"reopen={reopened.Status}/{reopened.Code}; " +
            $"denied={denied.Status}/{denied.Code}; " +
            $"freshOpenDenied={deniedOpen.Status}/{deniedOpen.Code}; " +
            $"loggerEntries={logger.Entries.Count}; " +
            $"loggerCategories={string.Join(',', loggerCategories)}; " +
            $"unsafeLoggerEntries={logger.Entries.Count(entry => logForbiddenMarkers.Any(marker =>
                entry.Message.Contains(marker, StringComparison.OrdinalIgnoreCase) ||
                entry.State.Contains(marker, StringComparison.OrdinalIgnoreCase) ||
                (entry.ExceptionText?.Contains(marker, StringComparison.OrdinalIgnoreCase) ?? false)))}; " +
            "unsafeDiagnostics=0; diagnosticEvidence=characterization");
    }
}

internal sealed record RetainedSourceLogEntry(
    string Category,
    LogLevel Level,
    EventId EventId,
    string Message,
    string State,
    string? ExceptionText);

internal sealed class RetainedSourceRecordingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<RetainedSourceLogEntry> _entries = [];

    internal IReadOnlyCollection<RetainedSourceLogEntry> Entries => [.. _entries];

    public ILogger CreateLogger(string categoryName) =>
        new RetainedSourceRecordingLogger(categoryName, this);

    public void Dispose()
    {
    }

    private void Record(RetainedSourceLogEntry entry) => _entries.Enqueue(entry);

    private sealed class RetainedSourceRecordingLogger(
        string categoryName,
        RetainedSourceRecordingLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            owner.Record(new(
                categoryName,
                logLevel,
                eventId,
                formatter(state, exception),
                FormatState(state),
                exception?.ToString()));
        }

        private static string FormatState<TState>(TState state)
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> structured)
            {
                return string.Join(
                    "|",
                    structured.Select(pair =>
                        $"{pair.Key}={pair.Value?.ToString() ?? string.Empty}"));
            }

            return state?.ToString() ?? string.Empty;
        }
    }

    private sealed class NullScope : IDisposable
    {
        internal static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}