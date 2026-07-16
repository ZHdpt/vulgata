using System.Diagnostics;
using System.Buffers;
using System.Text.Json;

namespace Vulgata.Tests.CodeGraphQualification;

internal sealed class CgcQualificationRunner
{
    private const string _imageName = "vulgata-codegraph-qualification:0.5.1";
    private const string _fixtureContainerPath = "/opt/vulgata-qualification-repository";
    private const string _gatewayHost = "127.0.0.1";
    private const int _gatewayPort = 48123;
    internal const string StaticStructuralQuery =
        "MATCH (source:Function)-[relationship:CALLS]->(target:Function) " +
        "RETURN 'CALLS' AS relationship_kind, source.path AS source_path, source.name AS source_symbol, source.line_number AS source_start_line, source.end_line AS source_end_line, source.uid AS source_identity, target.path AS target_path, target.name AS target_symbol, target.line_number AS target_start_line, target.end_line AS target_end_line, target.uid AS target_identity, false AS filtered_target " +
        "UNION ALL MATCH (source:Class)-[relationship:CONTAINS]->(target:Function) " +
        "RETURN 'CONTAINS' AS relationship_kind, source.path AS source_path, source.name AS source_symbol, source.line_number AS source_start_line, source.end_line AS source_end_line, source.uid AS source_identity, target.path AS target_path, target.name AS target_symbol, target.line_number AS target_start_line, target.end_line AS target_end_line, target.uid AS target_identity, false AS filtered_target " +
        "UNION ALL MATCH (source:File)-[relationship:IMPORTS]->(target:Module) " +
        "RETURN 'IMPORTS' AS relationship_kind, source.path AS source_path, source.name AS source_symbol, relationship.line_number AS source_start_line, relationship.line_number AS source_end_line, source.path AS source_identity, target.path AS target_path, target.name AS target_symbol, target.line_number AS target_start_line, target.line_number AS target_end_line, target.full_import_name AS target_identity, false AS filtered_target " +
        "UNION ALL MATCH (source:Class)-[relationship:INHERITS]->(target:Class) " +
        "RETURN 'INHERITS' AS relationship_kind, source.path AS source_path, source.name AS source_symbol, source.line_number AS source_start_line, source.end_line AS source_end_line, source.uid AS source_identity, target.path AS target_path, target.name AS target_symbol, target.line_number AS target_start_line, target.end_line AS target_end_line, target.uid AS target_identity, false AS filtered_target " +
        "UNION ALL MATCH (source:Function)-[relationship:CALLS]->(target:File) " +
        "RETURN 'CALLS' AS relationship_kind, source.path AS source_path, source.name AS source_symbol, source.line_number AS source_start_line, source.end_line AS source_end_line, source.uid AS source_identity, target.path AS target_path, target.name AS target_symbol, NULL AS target_start_line, NULL AS target_end_line, target.path AS target_identity, true AS filtered_target " +
        "ORDER BY relationship_kind, source_identity, target_identity LIMIT 256";
    private static readonly string _requestBody =
        JsonSerializer.Serialize(new { query = StaticStructuralQuery, @params = new { } });
    private static readonly IReadOnlyList<string> _requiredRelationshipKinds = Array.AsReadOnly(
    [
        "CALLS",
        "CONTAINS",
        "IMPORTS",
        "INHERITS",
    ]);
    private static readonly IReadOnlyList<string> _qualifiedCommandNames = Array.AsReadOnly(
    [
        "cgc-version",
        "cgc-doctor",
        "cgc-context-create",
        "cgc-context-mode",
        "cgc-context-default",
        "cgc-index",
        "cgc-api-start",
        "cgc-api-health",
        "cgc-api-query",
        "gateway-stop",
        "cgc-context-delete",
    ]);
    private static readonly IReadOnlyList<string> _qualifiedCommands = Array.AsReadOnly(
    [
        "cgc version",
        "cgc doctor",
        "cgc context create <private-context> --database kuzudb --db-path <private-database>",
        "cgc context mode named",
        "cgc context default <private-context>",
        "cgc index <read-only-fixture> --force --context <private-context>",
        "cgc api start --host 127.0.0.1 --port <private-port>",
        "GET http://127.0.0.1:<private-port>/health",
        "POST http://127.0.0.1:<private-port>/api/v1/query",
        "gateway-stop",
        "cgc context delete <private-context>",
    ]);

    private readonly string _repositoryRoot;
    private readonly TimeSpan _wholeProcessTimeout;
    private readonly IQualificationProcessLauncher _processLauncher;

    internal CgcQualificationRunner(
        string repositoryRoot,
        TimeSpan wholeProcessTimeout,
        IQualificationProcessLauncher processLauncher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(wholeProcessTimeout, TimeSpan.Zero);

        _repositoryRoot = Path.GetFullPath(repositoryRoot);
        _wholeProcessTimeout = wholeProcessTimeout;
        _processLauncher = processLauncher ?? throw new ArgumentNullException(nameof(processLauncher));
    }

    internal CgcQualificationContract CreateContract() =>
        new(_qualifiedCommandNames);

    internal async Task<CgcQualificationExecution> RunAsync(CancellationToken cancellationToken = default)
    {
        string fixturePath = Path.Combine(
            _repositoryRoot,
            "tests",
            "Vulgata.Tests",
            "CodeGraphQualification",
            "Fixtures",
            "QualificationRepository");

        if (!Directory.Exists(fixturePath))
        {
            return Failure("The synthetic qualification Repository was not available.");
        }

        ImagePreparationResult imagePreparation = await EnsureQualificationImageAsync(cancellationToken);
        if (!imagePreparation.Succeeded)
        {
            return Failure(imagePreparation.FailureSummary!);
        }

        string privateContextName = $"vulgata-qualification-{Guid.NewGuid():N}";
        string privateDatabasePath = $"/var/lib/vulgata-codegraph/{privateContextName}.kuzu";
        string privateHomePath = $"/tmp/{privateContextName}-home";
        string containerName = $"vulgata-qualification-{Guid.NewGuid():N}";
        ProcessStartInfo startInfo = CreateDockerStartInfo(
            fixturePath,
            privateContextName,
            privateDatabasePath,
            privateHomePath,
            containerName);

        using Process process = _processLauncher.CreateProcess(startInfo);
        using CancellationTokenSource overflowCancellation = new();
        int forceContainerRemoval = 0;
        CgcOverflowTerminationSignal overflowSignal = new(
            () =>
            {
                overflowCancellation.Cancel();
                TryTerminateProcessTree(process);
            },
            () => Interlocked.Exchange(ref forceContainerRemoval, 1));
        using CgcStreamingEvidenceObserver observer = new(overflowSignal.Signal);
        try
        {
            if (!process.Start())
            {
                return Failure("The CodeGraph qualification container process did not start.");
            }

            Task standardOutputTask = ObserveOutputAsync(
                process.StandardOutput.BaseStream,
                CgcOutputStream.StandardOutput,
                observer);
            Task standardErrorTask = ObserveOutputAsync(
                process.StandardError.BaseStream,
                CgcOutputStream.StandardError,
                observer);
            Task processExitTask = process.WaitForExitAsync();
            Task timeoutTask = Task.Delay(_wholeProcessTimeout);
            Task cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            Task overflowTask = Task.Delay(Timeout.InfiniteTimeSpan, overflowCancellation.Token);
            Task completedTask = await Task.WhenAny(processExitTask, timeoutTask, cancellationTask, overflowTask);

            bool timedOut = completedTask == timeoutTask;
            bool cancelled = completedTask == cancellationTask;
            bool overflowed = completedTask == overflowTask;
            if (timedOut || cancelled || overflowed)
            {
                TryTerminateProcessTree(process);
                Interlocked.Exchange(ref forceContainerRemoval, 1);
            }

            await processExitTask;
            await Task.WhenAll(standardOutputTask, standardErrorTask);
            CgcStreamingEvidence evidence = observer.Complete();
            overflowed |= evidence.Overflowed;
            if (overflowed)
            {
                Interlocked.Exchange(ref forceContainerRemoval, 1);
            }

            bool ranAsNonRoot = evidence.RanAsNonRoot;
            bool indexedWholeRepository = evidence.IndexedWholeRepository;
            bool cleanupSucceeded = evidence.CleanupSucceeded;
            IReadOnlyList<QualificationCommandEvidence> commandEvidence = evidence.Commands;
            StructuralObservation structuralObservation = StructuralObservation.From(evidence);

            if (timedOut)
            {
                return Failure(
                    CreateFailureSummary("The single whole-Repository qualification process timed out", evidence),
                    ranAsNonRoot,
                    indexedWholeRepository,
                    cleanupSucceeded,
                    commandEvidence,
                    structuralObservation);
            }

            if (cancelled)
            {
                return Failure(
                    CreateFailureSummary("The single whole-Repository qualification process was cancelled", evidence),
                    ranAsNonRoot,
                    indexedWholeRepository,
                    cleanupSucceeded,
                    commandEvidence,
                    structuralObservation);
            }

            if (process.ExitCode != 0)
            {
                return Failure(
                    CreateFailureSummary($"The CodeGraph qualification container exited with code {process.ExitCode}", evidence),
                    ranAsNonRoot,
                    indexedWholeRepository,
                    cleanupSucceeded,
                    commandEvidence,
                    structuralObservation);
            }

            if (!ranAsNonRoot || !indexedWholeRepository || !cleanupSucceeded)
            {
                return Failure(
                    CreateFailureSummary("The CodeGraph qualification process omitted a required completion marker", evidence),
                    ranAsNonRoot,
                    indexedWholeRepository,
                    cleanupSucceeded,
                    commandEvidence,
                    structuralObservation);
            }

            if (evidence.CommandEvidenceOverflowed)
            {
                return Failure(
                    CreateFailureSummary("The qualification lifecycle emitted more than the bounded command-evidence limit", evidence),
                    ranAsNonRoot,
                    indexedWholeRepository,
                    cleanupSucceeded,
                    commandEvidence,
                    structuralObservation);
            }

            if (!evidence.IsValidUtf8 || overflowed)
            {
                return Failure(
                    CreateFailureSummary("The streaming qualification observer rejected invalid UTF-8 or the shared output cap", evidence),
                    ranAsNonRoot,
                    indexedWholeRepository,
                    cleanupSucceeded,
                    commandEvidence,
                    structuralObservation);
            }

            if (!structuralObservation.Qualifies)
            {
                return Failure(
                    CreateFailureSummary($"The static CodeGraph query did not meet the structural gate ({structuralObservation.FailureSummary})", evidence),
                    ranAsNonRoot,
                    indexedWholeRepository,
                    cleanupSucceeded,
                    commandEvidence,
                    structuralObservation);
            }

            return new CgcQualificationExecution(
                Succeeded: true,
                FailureSummary: null,
                RanAsNonRoot: true,
                UsedSingleWholeRepositoryIndex: true,
                CleanupSucceeded: true,
                Commands: _qualifiedCommands,
                CommandEvidence: commandEvidence,
                HasBoundedStructuralEvidence: true,
                HasDeterministicStructuralGrammar: true,
                RelationshipKinds: structuralObservation.RelationshipKinds,
                EndpointIdentities: structuralObservation.EndpointIdentities,
                OrderedRanges: structuralObservation.OrderedRanges,
                StructuralGrammar: structuralObservation.Grammar,
                StructuralEvidence: evidence);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failure($"The CodeGraph qualification process could not be started ({exception.GetType().Name}).");
        }
        finally
        {
            if (Volatile.Read(ref forceContainerRemoval) == 1)
            {
                await TryForceRemoveContainerAsync(containerName);
            }
        }
    }

    private static ProcessStartInfo CreateDockerStartInfo(
        string fixturePath,
        string privateContextName,
        string privateDatabasePath,
        string privateHomePath,
        string containerName)
    {
        ProcessStartInfo startInfo = new("docker")
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--rm");
        startInfo.ArgumentList.Add("--name");
        startInfo.ArgumentList.Add(containerName);
        startInfo.ArgumentList.Add("--network");
        startInfo.ArgumentList.Add("none");
        startInfo.ArgumentList.Add("--mount");
        startInfo.ArgumentList.Add($"type=bind,src={fixturePath},dst={_fixtureContainerPath},readonly");
        startInfo.ArgumentList.Add("--entrypoint");
        startInfo.ArgumentList.Add("sh");
        startInfo.ArgumentList.Add(_imageName);
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(CreateQualificationScript(privateContextName, privateDatabasePath, privateHomePath));

        return startInfo;
    }

    private static string CreateQualificationScript(string privateContextName, string privateDatabasePath, string privateHomePath) =>
        $$"""
        set -eu
        request_body='{{_requestBody}}'
        context_name="{{privateContextName}}"
        database_path="{{privateDatabasePath}}"
        evidence_directory="{{privateHomePath}}/evidence"
        gateway_host="{{_gatewayHost}}"
        gateway_port="{{_gatewayPort}}"
        context_created=0
        gateway_pid=""

        emit_command_evidence() {
            command_name="$1"
            command_exit="$2"
            command_stdout="$3"
            command_stderr="$4"
            printf 'VULGATA_CGC_COMMAND=%s|%s|%s|%s\n' \
                "$command_name" \
                "$command_exit" \
                "$(sha256sum "$command_stdout" | awk '{print $1}')" \
                "$(sha256sum "$command_stderr" | awk '{print $1}')"
        }

        run_cgc() {
            command_name="$1"
            shift
            command_stdout="$evidence_directory/${command_name}.stdout"
            command_stderr="$evidence_directory/${command_name}.stderr"
            if "$@" >"$command_stdout" 2>"$command_stderr"; then
                command_exit=0
            else
                command_exit=$?
            fi
            emit_command_evidence "$command_name" "$command_exit" "$command_stdout" "$command_stderr"
            return "$command_exit"
        }

        run_context_delete() {
            command_stdout="$evidence_directory/cgc-context-delete.stdout"
            command_stderr="$evidence_directory/cgc-context-delete.stderr"
            if printf '%s\n' y | cgc context delete "$context_name" >"$command_stdout" 2>"$command_stderr"; then
                command_exit=0
            else
                command_exit=$?
            fi
            emit_command_evidence cgc-context-delete "$command_exit" "$command_stdout" "$command_stderr"
            return "$command_exit"
        }

        run_gateway_stop() {
            command_stdout="$evidence_directory/gateway-stop.stdout"
            command_stderr="$evidence_directory/gateway-stop.stderr"
            if [ -z "$gateway_pid" ]; then
                command_exit=0
                : >"$command_stdout"
                : >"$command_stderr"
            elif kill -0 "$gateway_pid" 2>/dev/null; then
                if kill -TERM "$gateway_pid" >"$command_stdout" 2>"$command_stderr"; then
                    command_exit=0
                else
                    command_exit=$?
                fi
                wait "$gateway_pid" >>"$command_stdout" 2>>"$command_stderr" || true
            else
                command_exit=0
                : >"$command_stdout"
                : >"$command_stderr"
            fi
            emit_command_evidence gateway-stop "$command_exit" "$command_stdout" "$command_stderr"
            return "$command_exit"
        }

        cleanup() {
            original_status=$?
            cleanup_ok=1
            printf '%s\n' 'VULGATA_CGC_QUALIFICATION_CLEANUP=ATTEMPTED'
            if [ "$context_created" -eq 1 ]; then
                if ! printf '%s\n' y | cgc context delete "$context_name" >/dev/null 2>&1; then
                    cleanup_ok=0
                fi
            fi
            if ! rm -rf "$database_path"; then
                cleanup_ok=0
            fi
            if ! rm -rf "$evidence_directory"; then
                cleanup_ok=0
            fi
            if [ -e "$database_path" ]; then
                cleanup_ok=0
            fi
            if [ "$cleanup_ok" -eq 1 ]; then
                printf '%s\n' 'VULGATA_CGC_QUALIFICATION_CLEANUP=SUCCEEDED'
            else
                printf '%s\n' 'VULGATA_CGC_QUALIFICATION_CLEANUP=FAILED' >&2
            fi
            if [ "$original_status" -ne 0 ]; then
                exit "$original_status"
            fi
            if [ "$cleanup_ok" -ne 1 ]; then
                exit 92
            fi
        }

        trap cleanup EXIT
        if [ "$(id -u)" -eq 0 ]; then
            printf '%s\n' 'VULGATA_CGC_QUALIFICATION_NON_ROOT=FAILED' >&2
            exit 93
        fi
        printf '%s\n' 'VULGATA_CGC_QUALIFICATION_NON_ROOT=SUCCEEDED'
        mkdir -p "{{privateHomePath}}"
        chmod 700 "{{privateHomePath}}"
        export HOME="{{privateHomePath}}"
        mkdir -p "$evidence_directory"
        run_cgc cgc-version cgc version
        run_cgc cgc-doctor cgc doctor
        run_cgc cgc-context-create cgc context create "$context_name" --database kuzudb --db-path "$database_path"
        context_created=1
        run_cgc cgc-context-mode cgc context mode named
        run_cgc cgc-context-default cgc context default "$context_name"
        run_cgc cgc-index cgc index {{_fixtureContainerPath}} --force --context "$context_name"
        printf '%s\n' 'VULGATA_CGC_QUALIFICATION_INDEX=SUCCEEDED'
        command_stdout="$evidence_directory/cgc-api-start.stdout"
        command_stderr="$evidence_directory/cgc-api-start.stderr"
        cgc api start --host "$gateway_host" --port "$gateway_port" >"$command_stdout" 2>"$command_stderr" &
        gateway_pid=$!
        emit_command_evidence cgc-api-start 0 "$command_stdout" "$command_stderr"
        command_exit=7
        health_attempt=0
        while [ "$health_attempt" -lt 100 ]; do
            if curl --silent --show-error --fail "http://$gateway_host:$gateway_port/health"; then
                command_exit=0
                break
            fi

            health_attempt=$((health_attempt + 1))
            sleep 0.1
        done
        if [ "$command_exit" -eq 0 ]; then
            printf '%s\n' 'VULGATA_CGC_HTTP_HEALTH=SUCCEEDED'
        fi
        if printf '%s' "$request_body" | curl --silent --show-error --fail --header 'Content-Type: application/json' --request POST --data @- "http://$gateway_host:$gateway_port/api/v1/query"; then
            command_exit=0
        else
            command_exit=$?
        fi
        if [ "$command_exit" -eq 0 ]; then
            printf '%s\n' 'VULGATA_CGC_HTTP_QUERY=SUCCEEDED'
        fi
        run_gateway_stop
        printf '%s\n' 'VULGATA_CGC_QUALIFICATION_CLEANUP=ATTEMPTED'
        run_context_delete
        context_created=0
        rm -rf "$database_path"
        rm -rf "$evidence_directory"
        if [ -e "$database_path" ]; then
            exit 94
        fi
        printf '%s\n' 'VULGATA_CGC_QUALIFICATION_CLEANUP=SUCCEEDED'
        trap - EXIT
        """;

    private static async Task ObserveOutputAsync(
        Stream stream,
        CgcOutputStream outputStream,
        CgcStreamingEvidenceObserver observer)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(1_024);
        try
        {
            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
            {
                if (observer.Observe(outputStream, buffer.AsSpan(0, read)).TerminationRequired)
                {
                    return;
                }
            }
        }
        finally
        {
            Array.Clear(buffer, 0, buffer.Length);
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task TryForceRemoveContainerAsync(string containerName)
    {
        try
        {
            ProcessStartInfo startInfo = new("docker")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add("container");
            startInfo.ArgumentList.Add("rm");
            startInfo.ArgumentList.Add("--force");
            startInfo.ArgumentList.Add("--volumes");
            startInfo.ArgumentList.Add(containerName);

            using Process process = _processLauncher.CreateProcess(startInfo);
            if (process.Start())
            {
                await process.WaitForExitAsync();
            }
        }
        catch (Exception)
        {
            // The primary process result is retained even if Docker has already removed the disposable container.
        }
    }

    private async Task<ImagePreparationResult> EnsureQualificationImageAsync(CancellationToken cancellationToken)
    {
        ProcessStartInfo inspectStartInfo = new("docker")
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        inspectStartInfo.ArgumentList.Add("image");
        inspectStartInfo.ArgumentList.Add("inspect");
        inspectStartInfo.ArgumentList.Add(_imageName);

        using Process inspect = _processLauncher.CreateProcess(inspectStartInfo);
        if (inspect.Start())
        {
            await inspect.WaitForExitAsync(cancellationToken);
            if (inspect.ExitCode == 0)
            {
                return ImagePreparationResult.Success();
            }
        }

        string dockerfilePath = Path.Combine(_repositoryRoot, "docker", "Dockerfile");
        ProcessStartInfo buildStartInfo = new("docker")
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        buildStartInfo.ArgumentList.Add("build");
        buildStartInfo.ArgumentList.Add("--pull=false");
        buildStartInfo.ArgumentList.Add("--file");
        buildStartInfo.ArgumentList.Add(dockerfilePath);
        buildStartInfo.ArgumentList.Add("--tag");
        buildStartInfo.ArgumentList.Add(_imageName);
        buildStartInfo.ArgumentList.Add(_repositoryRoot);

        using Process build = _processLauncher.CreateProcess(buildStartInfo);
        if (!build.Start())
        {
            return ImagePreparationResult.Failure("The qualification image build process did not start.");
        }

        string buildStdout = await build.StandardOutput.ReadToEndAsync(cancellationToken);
        string buildStderr = await build.StandardError.ReadToEndAsync(cancellationToken);
        await build.WaitForExitAsync(cancellationToken);

        if (build.ExitCode != 0)
        {
            string diagnostic = string.IsNullOrWhiteSpace(buildStderr) ? buildStdout : buildStderr;
            diagnostic = diagnostic.ReplaceLineEndings(" ").Trim();
            if (diagnostic.Length > 400)
            {
                diagnostic = diagnostic[..400];
            }

            return ImagePreparationResult.Failure($"The qualification image build failed with exit code {build.ExitCode}. {diagnostic}");
        }

        return ImagePreparationResult.Success();
    }

    private static void TryTerminateProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited while cancellation was being handled.
        }
    }

    private static CgcQualificationExecution Failure(
        string summary,
        bool ranAsNonRoot = false,
        bool indexedWholeRepository = false,
        bool cleanupSucceeded = false,
        IReadOnlyList<QualificationCommandEvidence>? commandEvidence = null,
        StructuralObservation? structuralObservation = null)
    {
        StructuralObservation observation = structuralObservation ?? StructuralObservation.None;
        return new CgcQualificationExecution(
            Succeeded: false,
            FailureSummary: summary,
            RanAsNonRoot: ranAsNonRoot,
            UsedSingleWholeRepositoryIndex: indexedWholeRepository,
            CleanupSucceeded: cleanupSucceeded,
            Commands: _qualifiedCommands,
            CommandEvidence: commandEvidence ?? Array.Empty<QualificationCommandEvidence>(),
            HasBoundedStructuralEvidence: observation.HasBoundedEvidence,
            HasDeterministicStructuralGrammar: observation.HasDeterministicGrammar,
            RelationshipKinds: observation.RelationshipKinds,
            EndpointIdentities: observation.EndpointIdentities,
            OrderedRanges: observation.OrderedRanges,
            StructuralGrammar: observation.Grammar);
    }

    private sealed record StructuralObservation(
        bool Qualifies,
        bool HasBoundedEvidence,
        bool HasDeterministicGrammar,
        IReadOnlyList<string> RelationshipKinds,
        IReadOnlyList<string> EndpointIdentities,
        IReadOnlyList<QualifiedSourceRange> OrderedRanges,
        string Grammar,
        string FailureSummary)
    {
        internal static StructuralObservation None { get; } = Failure("the static query did not run");

        internal static StructuralObservation Failure(string summary) =>
            new(false, false, false, [], [], [], "unqualified", summary);

        internal static StructuralObservation From(CgcStreamingEvidence evidence)
        {
            List<string> relationshipKinds = evidence.RelationshipKindCounts
                .Where(pair => pair.Value > 0)
                .Select(pair => pair.Key)
                .OrderBy(kind => kind, StringComparer.Ordinal)
                .ToList();
            bool qualifies = evidence.IsValidUtf8 &&
                !evidence.Overflowed &&
                evidence.HasExactlyOneOrderedStdoutMarkerPair &&
                evidence.HasValidStructuralRecord &&
                evidence.HasAllRequiredRelationshipKinds &&
                evidence.HasFilteredTargetCandidate &&
                evidence.HasPositiveOrderedRange &&
                evidence.HasNonEmptyEndpointIdentity &&
                evidence.AllStructuralFactsCorrelatedWithinValidRecords;
            string failureSummary = !evidence.IsValidUtf8
                ? "strict incremental UTF-8 failed"
                : evidence.Overflowed
                    ? "the shared 65,536-byte cap overflowed"
                    : !evidence.HasExactlyOneOrderedStdoutMarkerPair
                        ? "the stdout-only marker pair was missing, duplicated, reversed, or post-cap"
                        : !evidence.HasValidStructuralRecord
                            ? "no complete correlated structural row was observed"
                            : !evidence.HasAllRequiredRelationshipKinds
                                ? "not every required relationship kind was observed"
                                : !evidence.HasFilteredTargetCandidate
                                    ? "the filtered target candidate was absent"
                                    : !evidence.HasPositiveOrderedRange
                                        ? "no positive ordered range was observed"
                                        : !evidence.HasNonEmptyEndpointIdentity
                                            ? "no non-empty endpoint identity was observed"
                                            : "correlated structural predicates were not proved";

            return new StructuralObservation(
                qualifies,
                evidence.HasValidStructuralRecord,
                evidence.HasExactlyOneOrderedStdoutMarkerPair,
                relationshipKinds,
                [],
                [],
                "vulgata-cgc-structural-record-v1",
                failureSummary);
        }
    }

    private static string CreateFailureSummary(
        string reason,
        CgcStreamingEvidence evidence) =>
        $"{reason}. stdout SHA-256: {evidence.StandardOutput.Sha256}; stderr SHA-256: {evidence.StandardError.Sha256}; observed bytes: {evidence.ObservedTotalBytes}; overflowed: {evidence.Overflowed}.";
}

internal sealed record CgcQualificationExecution(
    bool Succeeded,
    string? FailureSummary,
    bool RanAsNonRoot,
    bool UsedSingleWholeRepositoryIndex,
    bool CleanupSucceeded,
    IReadOnlyList<string> Commands,
    IReadOnlyList<QualificationCommandEvidence> CommandEvidence,
    bool HasBoundedStructuralEvidence,
    bool HasDeterministicStructuralGrammar,
    IReadOnlyList<string> RelationshipKinds,
    IReadOnlyList<string> EndpointIdentities,
    IReadOnlyList<QualifiedSourceRange> OrderedRanges,
    string StructuralGrammar,
    CgcStreamingEvidence? StructuralEvidence = null);

internal sealed record QualifiedSourceRange(int StartLine, int EndLine);

internal sealed record ImagePreparationResult(bool Succeeded, string? FailureSummary)
{
    internal static ImagePreparationResult Success() => new(true, null);

    internal static ImagePreparationResult Failure(string summary) => new(false, summary);
}

internal sealed record CgcQualificationContract(IReadOnlyList<string> LifecycleEntries);
