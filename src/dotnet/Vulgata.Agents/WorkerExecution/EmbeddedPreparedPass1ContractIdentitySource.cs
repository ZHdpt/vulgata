using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.DomainServices;

namespace Vulgata.Agents.WorkerExecution;

public sealed class EmbeddedPreparedPass1ContractIdentitySource
    : IPreparedPass1ContractIdentitySource
{
    private const string _documentSchemaIdentity = "code-logic/file-v2";
    private const string _promptIdentityPrefix =
        "pass1-code-logic-worker.v2/sha256/";
    private const string _promptResourceName =
        "Vulgata.Agents.Prompts.pass1-code-logic-worker.v2.md";

    public PreparedPass1ContractIdentity? GetCurrent()
    {
        Assembly assembly = typeof(EmbeddedPreparedPass1ContractIdentitySource).Assembly;
        string[] matches = [.. assembly.GetManifestResourceNames()
            .Where(name => string.Equals(name, _promptResourceName, StringComparison.Ordinal))
        ];
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                "The fixed prepared Pass 1 v2 prompt is unavailable or ambiguous.");
        }

        using Stream stream = assembly.GetManifestResourceStream(matches[0])
            ?? throw new InvalidOperationException(
                "The fixed prepared Pass 1 v2 prompt is unavailable.");
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true);
        string prompt = reader.ReadToEnd();
        if (string.IsNullOrWhiteSpace(prompt) ||
            !prompt.Contains("`schemaIdentity` is exactly", StringComparison.Ordinal) ||
            !prompt.Contains($"`{_documentSchemaIdentity}`", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The fixed prepared Pass 1 v2 prompt has no consistent document schema identity.");
        }

        string promptHash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(prompt)));
        return new PreparedPass1ContractIdentity(
            _promptIdentityPrefix + promptHash,
            _documentSchemaIdentity);
    }
}
