using System.Text.Json;
using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Configuration;
using Vulgata.Shared;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.OctoberGenerator;

namespace Vulgata.Tests.October.Integration;

internal sealed record OctoberGenerateInputFiles(
    string RuntimeFilePath,
    string ScanPolicyFilePath,
    string WorkerProviderConfigurationFilePath);

/// <summary>Explicit inputs for the owned qualification fixture, not a production authority port.</summary>
internal static class OctoberGenerateFixtureInputs
{
    internal static IReadOnlyList<string> GenerateDiagnostics(int processExit, string stdout, string stderr)
    {
        var output = new List<string> { $"generate-process-exit={processExit.ToString(CultureInfo.InvariantCulture)}" };
        string startup = stderr.Trim() switch
        {
            "October startup configuration failed: InvalidConfiguration." => "startup-invalid-configuration",
            "October provider configuration failed: InvalidConfiguration." => "provider-invalid-configuration",
            "October Worker provider binding failed: InvalidConfiguration." => "worker-invalid-configuration",
            "October provider binding failed: CredentialUnavailable." => "provider-credential-unavailable",
            "" => "none",
            _ => "present-redacted"
        };
        output.Add($"generate-stderr-classification={startup}");
        try
        {
            if (stdout.Length is 0 or > 1048576) throw new JsonException();
            using var parsed = JsonDocument.Parse(stdout, new JsonDocumentOptions { MaxDepth = 32 });
            var report = parsed.RootElement;
            if (report.ValueKind != JsonValueKind.Object) throw new JsonException();
            Unique(report);
            string State(string name, params string[] allowed) => report.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.String && allowed.Contains(value.GetString(), StringComparer.Ordinal)
                ? value.GetString()! : "unavailable";
            string Count(string name) => report.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
                ? value.GetArrayLength().ToString(CultureInfo.InvariantCulture) : "unavailable";
            string Number(JsonElement owner, string name) => owner.ValueKind == JsonValueKind.Object
                && owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long number) && number >= 0
                ? number.ToString(CultureInfo.InvariantCulture) : "unavailable";
            string Phase(string name) => State(name, "not-requested", "not-started", "completed", "partial", "no-content", "failed", "interrupted", "indeterminate", "unavailable");
            // This is a redacted report observation, never database-confirmed persistence or dispatch evidence.
            output.Add($"generate-reported-status: requested={State("requestedStage", "cl-only", "cl-and-bl")}; completed={State("completedStage", "cl-only", "cl-and-bl")}; coverage={State("coverage", "Complete", "Partial", "None")}; execution={State("execution", "Completed", "Failed", "Interrupted", "Indeterminate")}; publication={State("publicationStatus", "NotRequested", "Confirmed", "NoContent", "Failed", "Indeterminate")}; matching={Phase("matching")}; business={Phase("businessLogic")}; exit={Number(report, "exitCode")}");
            output.Add($"generate-reported-counts: documents={Count("documents")}; accepted-files={Count("acceptedFiles")}; terminal-coverage={Count("terminalCoverage")}; inventory={Count("inventory")}; gaps={Count("gaps")}");
            if (report.TryGetProperty("gaps", out var gaps) && gaps.ValueKind == JsonValueKind.Array)
                output.Add("generate-reported-gap-codes=" + string.Join(',', gaps.EnumerateArray().Take(16).Select(gap =>
                    gap.ValueKind == JsonValueKind.Object && gap.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String
                        ? SafeGap(code.GetString()) : "other")));
            if (!report.TryGetProperty("usage", out var usage)) usage = default;
            string unknown = usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("hasUnknownUsage", out var flag)
                ? flag.ValueKind switch { JsonValueKind.True => "true", JsonValueKind.False => "false", _ => "unavailable" } : "unavailable";
            output.Add($"generate-reported-usage: requests={Number(usage, "requests")}; input-tokens={Number(usage, "inputTokens")}; output-tokens={Number(usage, "outputTokens")}; unknown={unknown}");
            if (!report.TryGetProperty("businessDiagnostics", out var business)) business = default;
            string Defined<T>(string name) where T : struct, Enum => business.ValueKind == JsonValueKind.Object
                && business.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                && Enum.GetNames<T>().Contains(value.GetString(), StringComparer.Ordinal) ? value.GetString()! : "unavailable";
            string mismatch = business.ValueKind == JsonValueKind.Object && business.TryGetProperty("operationMismatch", out var mismatchValue)
                ? mismatchValue.ValueKind switch { JsonValueKind.True => "true", JsonValueKind.False => "false", _ => "unavailable" } : "unavailable";
            output.Add($"generate-reported-business-diagnostics: finalizer-status={Defined<BusinessFinalizationStatus>("finalizerStatus")}; finalizer-persistence={Defined<KnowledgeWriteStatus>("finalizerPersistenceStatus")}; lookup-status={Defined<BusinessFinalizationStatus>("lookupStatus")}; lookup-persistence={Defined<KnowledgeWriteStatus>("lookupPersistenceStatus")}; boundary={Defined<OctoberBusinessDiagnosticBoundary>("boundary")}; operation-mismatch={mismatch}");
        }
        catch (JsonException) { output.Add("generate-report=unavailable-or-malformed"); }
        return output;

        static string SafeGap(string? code) => code switch
        {
            "owned_cleanup_unconfirmed" or "coverage_incomplete" or "invalid_run_configuration" or "authority_unavailable" or
            "begin_indeterminate" or "begin_settlement_pending" or "fresh_run_required" or "run_ineligible" or "run_interrupted" or
            "run_unavailable" or "single_file_checkpoint_limit" or "source_frame_unavailable" or "source_frame_binding_changed" or
            "file_deadline_exceeded" or "provider_settlement_unconfirmed" or "worker_not_completed" or "file_acceptance_unavailable" or
            "generate_terminal_report_unavailable" or "output_binding_invalid" or "commit_indeterminate" or "operation_log_unavailable" or "phase_ineligible" or
            "publication_failed" or "publication_interrupted" or "publication_unavailable" or "no_cl_content" or
            "freeze_settlement_pending" or "publication_settlement_pending" or "phase_closed" or "unsupported_stage" or
            "publication_not_started" or "source-authority-or-identity-lost" or "incomplete-directory" or "source-limitation" or
            "directory-limit" or "metadata-limit" or "listing-unavailable" or "cancelled" or "source-or-execution-gap" or
            "business_authority_unavailable" or "business_client_unavailable" or "business_corpus_unavailable" or
            "business_freeze_unavailable" or "business_initial_read_unavailable" or "business_original_slot_unavailable" or
            "business_planning_inputs_unavailable" or "business_publication_binding_unavailable" or
            "business_publication_read_unavailable" or "business_publication_unavailable" or
            "business_resolution_failed" or "business_resolution_ineligible" or "business_resolution_limit" or
            "business_resolution_unavailable" or "business_roots_unavailable" or "business_run_ineligible" => code,
            _ => "other"
        };

        static void Unique(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in value.EnumerateObject())
                { if (!names.Add(property.Name)) throw new JsonException(); Unique(property.Value); }
            }
            else if (value.ValueKind == JsonValueKind.Array)
                foreach (var item in value.EnumerateArray()) Unique(item);
        }
    }

    internal static TimeSpan GenerateWait(int runDeadlineSeconds)
    {
        if (runDeadlineSeconds is < 1 or > 1800)
            throw new ArgumentOutOfRangeException(nameof(runDeadlineSeconds));
        // This is a process-observation/cleanup margin, never a renewed Scan budget.
        return TimeSpan.FromSeconds(runDeadlineSeconds + 120);
    }

    internal static OctoberGenerateInputFiles? ValidateFiles(
        string ownedRoot, string mode, string? runtimeFilePath, string? scanPolicyFilePath,
        string? workerProviderConfigurationFilePath, bool hasBindingOverrides)
    {
        if (runtimeFilePath is null && scanPolicyFilePath is null &&
            workerProviderConfigurationFilePath is null)
            return null;

        if (mode != "generate" || hasBindingOverrides || runtimeFilePath is null ||
            scanPolicyFilePath is null || workerProviderConfigurationFilePath is null)
            throw new ArgumentException("Generate requires all three owned inputs without binding overrides.");

        string root = ValidateRoot(ownedRoot);
        return new(ValidateFile(root, runtimeFilePath), ValidateFile(root, scanPolicyFilePath),
            ValidateFile(root, workerProviderConfigurationFilePath));
    }

    internal static async Task<string> WriteWorkerProviderAsync(
        string ownedRoot, IDataProtectionProvider protection, OctoberProviderProfile approvedProfile,
        Func<CancellationToken, Task<string>> readApprovedCredential,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(protection);
        ArgumentNullException.ThrowIfNull(approvedProfile);
        ArgumentNullException.ThrowIfNull(readApprovedCredential);
        string root = ValidateRoot(ownedRoot);
        if (!Exact(approvedProfile.Name) || !Exact(approvedProfile.Model) ||
            !Exact(approvedProfile.CredentialReference) || !Exact(approvedProfile.Endpoint) ||
            approvedProfile.ApiType != ApiType.ChatCompletions ||
            !Uri.TryCreate(approvedProfile.Endpoint, UriKind.Absolute, out Uri? endpoint) ||
            endpoint.Scheme is not ("http" or "https") || endpoint.UserInfo.Length != 0 ||
            endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0)
            throw new ArgumentException("The approved Worker profile is invalid.");

        cancellationToken.ThrowIfCancellationRequested();
        string credential;
        try
        {
            // Retain the actual callback through completion even if it ignores cancellation.
            // Cancellation is checked again before protection or any file write.
            credential = await readApprovedCredential(cancellationToken);
        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("The approved credential could not be read.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(credential))
            throw new InvalidOperationException("The approved credential is unavailable.");

        string ciphertext;
        try
        {
            ciphertext = protection.CreateProtector(SecretPurposes.LlmApiKey).Protect(credential);
        }
        catch (Exception)
        {
            throw new InvalidOperationException("The approved credential could not be protected.");
        }
        string json = JsonSerializer.Serialize(new
        {
            sourceIdentity = "owned-generate-worker",
            precedence = "only-source",
            workerProfile = approvedProfile.Name,
            profiles = new[]
            {
                new
                {
                    name = approvedProfile.Name,
                    apiType = "chat-completions",
                    endpoint = approvedProfile.Endpoint,
                    model = approvedProfile.Model,
                    credentialReference = approvedProfile.CredentialReference,
                },
            },
            protectedCredentials = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [approvedProfile.CredentialReference] = ciphertext,
            },
        });
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRoot(root);
        string path = Path.Combine(root, $"generate-worker-{Guid.NewGuid():N}.json");
        bool created = false;
        try
        {
            // CreateNew does not overwrite the fixture's Prepare/Select provider document.
            await using FileStream file = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            created = true;
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
            await file.WriteAsync(bytes, cancellationToken);
            await file.FlushAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch
        {
            if (created && File.Exists(path)) File.Delete(path);
            throw;
        }
        return path;
    }

    private static string ValidateRoot(string ownedRoot)
    {
        if (string.IsNullOrWhiteSpace(ownedRoot) || !Path.IsPathFullyQualified(ownedRoot) ||
            !Directory.Exists(ownedRoot))
            throw new ArgumentException("An existing absolute owned directory is required.");
        string root = Path.GetFullPath(ownedRoot);
        for (DirectoryInfo? directory = new(root); directory is not null; directory = directory.Parent)
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new ArgumentException("Reparse directories are not qualification inputs.");
        return root;
    }

    private static string ValidateFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ||
            path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part is "." or ".."))
            throw new ArgumentException("An exact absolute owned input file is required.");
        string full = Path.GetFullPath(path);
        string relative = Path.GetRelativePath(root, full);
        if (Path.IsPathRooted(relative) || relative == ".." ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            !File.Exists(full) || (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
            throw new ArgumentException("The input must be an existing regular file inside the owned directory.");
        ValidateRoot(Path.GetDirectoryName(full)!);
        return full;
    }

    private static bool Exact(string? value) => !string.IsNullOrWhiteSpace(value) && value.Trim() == value;
}
