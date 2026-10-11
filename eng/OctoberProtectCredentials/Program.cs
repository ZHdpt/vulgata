using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;
using Vulgata.Shared;
using Vulgata.Web.Data;

internal static class Program
{
    private const string OperatorPurpose = "october-operator-credentials-v1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    private sealed record Input(string KeyRingPath, string ApplicationName, string OutputDirectory,
        string Endpoint, string Model, string ApiKey, string UserName, string Password);

    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args is ["--self-test"])
            {
                await SelfTestAsync();
                Console.WriteLine("self-test-passed");
                return 0;
            }
            if (args.Length != 0) return Error("invalid-arguments");
            char[] buffer = new char[65537];
            int length = 0, read;
            while (length < buffer.Length && (read = await Console.In.ReadAsync(buffer.AsMemory(length))) != 0) length += read;
            if (length is 0 or > 65536) return Error("invalid-input");
            using var document = JsonDocument.Parse(new string(buffer, 0, length));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return Error("invalid-input");
            var properties = document.RootElement.EnumerateObject().ToArray();
            if (properties.Length != 8 || properties.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != 8)
                return Error("invalid-input");
            Input input = document.RootElement.Deserialize<Input>(Json) ?? throw new ArgumentException();
            await PackageAsync(input);
            // Caller supplied the directory; fixed filenames avoid logging local paths or protected values.
            Console.WriteLine("packaged:provider.json,operator.credentials.protected");
            return 0;
        }
        catch (JsonException) { return Error("invalid-input"); }
        catch (ArgumentException) { return Error("invalid-input-or-boundary"); }
        catch (Exception) { return Error("packaging-failed"); }
    }

    private static int Error(string code) { Console.Error.WriteLine(code); return 1; }

    private static async Task PackageAsync(Input input)
    {
        string keys = ExistingDirectory(input.KeyRingPath), output = ExistingDirectory(input.OutputDirectory);
        if (keys.Equals(output, StringComparison.OrdinalIgnoreCase) || Directory.EnumerateFileSystemEntries(output).Any()
            || !Exact(input.ApplicationName) || !Exact(input.Model) || !Exact(input.UserName)
            || string.IsNullOrWhiteSpace(input.ApiKey) || string.IsNullOrEmpty(input.Password)
            || !Exact(input.Endpoint) || !Uri.TryCreate(input.Endpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme is not ("https" or "http") || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0)
            throw new ArgumentException();
        await using var services = Protection(keys, input.ApplicationName);
        string encryptedKey = await services.GetRequiredService<ISecretEncryptionService>().EncryptAsync(SecretPurposes.LlmApiKey, input.ApiKey);
        string encryptedOperator = services.GetRequiredService<IDataProtectionProvider>().CreateProtector(OperatorPurpose).Protect(
            JsonSerializer.Serialize(new { userName = input.UserName, password = input.Password,
                authenticatorCode = (string?)null, recoveryCode = (string?)null }));
        string provider = JsonSerializer.Serialize(new
        {
            sourceIdentity = "retained-pilot", precedence = "only-source", workerProfile = "selected", chatProfile = "selected",
            profiles = new[] { new { name = "selected", apiType = "chat-completions", endpoint = input.Endpoint,
                model = input.Model, credentialReference = "selected-key" } },
            protectedCredentials = new Dictionary<string, string> { ["selected-key"] = encryptedKey }
        });
        List<string> created = [];
        try
        {
            await WriteNewAsync("provider.json", provider);
            await WriteNewAsync("operator.credentials.protected", encryptedOperator);
        }
        catch
        {
            // Only successful CreateNew acquisitions are cleanup candidates. Never remove a collision.
            ExistingDirectory(output);
            foreach (string path in created)
                if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0) File.Delete(path);
            throw;
        }

        async Task WriteNewAsync(string name, string content)
        {
            ExistingDirectory(output);
            string path = Path.Combine(output, name);
            await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            created.Add(path);
            await stream.WriteAsync(new UTF8Encoding(false, true).GetBytes(content));
            await stream.FlushAsync();
        }
    }

    private static ServiceProvider Protection(string keys, string application)
    {
        var services = new ServiceCollection();
        services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keys)).SetApplicationName(application);
        services.AddSingleton<ISecretEncryptionService, SecretEncryptionService>();
        return services.BuildServiceProvider();
    }

    private static string ExistingDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !Directory.Exists(path)
            || path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(x => x is "." or ".."))
            throw new ArgumentException();
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        for (DirectoryInfo? current = new(full); current is not null; current = current.Parent)
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0) throw new ArgumentException();
        if (Directory.EnumerateFileSystemEntries(full).Any(x => (File.GetAttributes(x) & FileAttributes.ReparsePoint) != 0))
            throw new ArgumentException();
        return full;
    }

    private static bool Exact(string? value) => !string.IsNullOrWhiteSpace(value) && value.Trim() == value && !value.Any(char.IsControl);

    private static async Task SelfTestAsync()
    {
        string temp = Path.GetFullPath(Path.GetTempPath());
        string root = Path.Combine(temp, "october-protect-self-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string keys = Directory.CreateDirectory(Path.Combine(root, "keys")).FullName;
            string output = Directory.CreateDirectory(Path.Combine(root, "output")).FullName;
            var input = new Input(keys, "controlled-protector-test", output, "https://never-contacted.invalid", "controlled-model",
                "synthetic-api-key", "synthetic-user@example.invalid", "Synthetic1!Password");
            await PackageAsync(input);
            string providerText = await File.ReadAllTextAsync(Path.Combine(output, "provider.json"));
            string operatorText = await File.ReadAllTextAsync(Path.Combine(output, "operator.credentials.protected"));
            if (providerText.Contains(input.ApiKey, StringComparison.Ordinal) || providerText.Contains(input.Password, StringComparison.Ordinal)
                || operatorText.Contains(input.Password, StringComparison.Ordinal) || operatorText.Contains(input.UserName, StringComparison.Ordinal))
                throw new InvalidOperationException();
            // A fresh provider instance exercises persisted keys, not a shared in-memory protector.
            await using var services = Protection(keys, input.ApplicationName);
            using var provider = JsonDocument.Parse(providerText);
            string cipher = provider.RootElement.GetProperty("protectedCredentials").GetProperty("selected-key").GetString()!;
            if (await services.GetRequiredService<ISecretEncryptionService>().DecryptAsync(SecretPurposes.LlmApiKey, cipher) != input.ApiKey)
                throw new InvalidOperationException();
            using var credentials = await new OctoberProtectedFileCredentialSource(Path.Combine(output, "operator.credentials.protected"),
                services.GetRequiredService<IDataProtectionProvider>()).ReadAsync(CancellationToken.None);
            if (credentials is null || credentials.UserName != input.UserName || credentials.Password != input.Password)
                throw new InvalidOperationException();
            bool refused = false;
            try { await PackageAsync(input); } catch (ArgumentException) { refused = true; }
            if (!refused || await File.ReadAllTextAsync(Path.Combine(output, "provider.json")) != providerText
                || await File.ReadAllTextAsync(Path.Combine(output, "operator.credentials.protected")) != operatorText)
                throw new InvalidOperationException();
        }
        finally
        {
            string owned = ExistingDirectory(root);
            if (Path.GetDirectoryName(owned) != Path.TrimEndingDirectorySeparator(temp)) throw new InvalidOperationException();
            Directory.Delete(owned, recursive: true);
        }
    }
}
