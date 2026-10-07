using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Configuration;
using Vulgata.Shared;

namespace Vulgata.Tests.October.Integration;

internal sealed record OctoberGenerateInputFiles(
    string RuntimeFilePath,
    string ScanPolicyFilePath,
    string WorkerProviderConfigurationFilePath);

/// <summary>Explicit inputs for the owned qualification fixture, not a production authority port.</summary>
internal static class OctoberGenerateFixtureInputs
{
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
