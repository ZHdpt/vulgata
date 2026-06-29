using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Vulgata.Infrastructure.Git;

public sealed partial class GitRemoteValidationService : IGitRemoteValidationService
{
    public async Task<GitRemoteValidationResult> ValidateAsync(string gitUrl, CancellationToken cancellationToken = default)
    {
        string normalizedUrl = (gitUrl ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedUrl))
        {
            return GitRemoteValidationResult.Unreachable("Git 地址不能为空。");
        }

        ProcessStartInfo startInfo = new("git")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("ls-remote");
        startInfo.ArgumentList.Add(normalizedUrl);

        using Process? process = Process.Start(startInfo);
        if (process is null)
        {
            return GitRemoteValidationResult.Unreachable("Git 校验进程启动失败。");
        }

        string stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        string stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode == 0)
        {
            return GitRemoteValidationResult.Reachable;
        }

        string combined = string.Join("\n", [stdout, stderr]);
        string sanitized = SanitizeSensitiveText(combined, normalizedUrl);

        if (ContainsAuthHint(sanitized))
        {
            return GitRemoteValidationResult.AuthenticationRequired("目标仓库需要认证，请提供可访问凭据。");
        }

        string message = FirstLineOrFallback(sanitized, "无法访问目标 Git 地址。");
        return GitRemoteValidationResult.Unreachable(message);
    }

    private static bool ContainsAuthHint(string message)
    {
        string normalized = (message ?? string.Empty).ToLowerInvariant();

        return normalized.Contains("authentication")
            || normalized.Contains("unauthorized")
            || normalized.Contains("forbidden")
            || normalized.Contains("could not read username")
            || normalized.Contains("terminal prompts disabled")
            || normalized.Contains("permission denied")
            || normalized.Contains("access denied")
            || normalized.Contains("requires authentication")
            || normalized.Contains("http basic");
    }

    private static string FirstLineOrFallback(string raw, string fallback)
    {
        string[] lines = (raw ?? string.Empty)
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (lines.Length == 0)
        {
            return fallback;
        }

        return lines[0];
    }

    private static string SanitizeSensitiveText(string text, string gitUrl)
    {
        string sanitized = (text ?? string.Empty).Replace(gitUrl, SanitizeUrl(gitUrl), StringComparison.OrdinalIgnoreCase);
        sanitized = CredentialUrlPattern().Replace(sanitized, match => SanitizeUrl(match.Value));
        return sanitized;
    }

    private static string SanitizeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return url;
        }

        if (string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            return uri.ToString();
        }

        string host = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
        return $"{uri.Scheme}://***@{host}{uri.AbsolutePath}";
    }

    [GeneratedRegex(@"https?://[^\s/@]+:[^\s/@]+@[^\s]+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CredentialUrlPattern();
}
