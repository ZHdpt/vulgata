using System.Text.Json;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace Vulgata.Agents.WorkerExecution;

public sealed record Pass2StrictDeclarationSet(
    byte[] ProjectedUtf8,
    byte[] ProjectedSubmitOnlyUtf8);

/// <summary>
/// The sole Pass 2 provider declaration authority.
/// </summary>
public static class Pass2StrictToolDeclarations
{
    public const string Identity = "pass2-strict-tools/1.0";
    public const string RetrieveCodeLogicName = "retrieve_code_logic";
    public const string SubmitBusinessLogicName = "submit_business_logic";
    private const int MaximumDeclarationsBytes = 81_920;

    public static Pass2StrictDeclarationSet Create(
        AIFunction retrieveCodeLogic,
        AIFunction submitBusinessLogic)
    {
        ArgumentNullException.ThrowIfNull(retrieveCodeLogic);
        ArgumentNullException.ThrowIfNull(submitBusinessLogic);
        Validate(retrieveCodeLogic, RetrieveCodeLogicName);
        Validate(submitBusinessLogic, SubmitBusinessLogicName);

        ChatTool retrieve = retrieveCodeLogic.AsOpenAIChatTool();
        ChatTool submit = submitBusinessLogic.AsOpenAIChatTool();
        byte[] projected = Serialize([retrieve, submit]);
        byte[] submitOnly = Serialize([submit]);
        if (projected.Length > MaximumDeclarationsBytes ||
            submitOnly.Length > MaximumDeclarationsBytes)
        {
            throw new InvalidDataException("Pass 2 declarations exceed their byte bound.");
        }

        return new(projected, submitOnly);
    }

    private static void Validate(AIFunction function, string expectedName)
    {
        if (!string.Equals(function.Name, expectedName, StringComparison.Ordinal) ||
            !function.AdditionalProperties.TryGetValue("strict", out object? strict) ||
            strict is not true)
        {
            throw new InvalidDataException(
                $"The accepted {expectedName} function is not strict.");
        }
    }

    private static byte[] Serialize(IReadOnlyList<ChatTool> tools)
    {
        using var destination = new MemoryStream();
        using (var writer = new Utf8JsonWriter(destination))
        {
            writer.WriteStartArray();
            foreach (ChatTool tool in tools)
            {
                if (tool.FunctionParameters is null)
                {
                    throw new InvalidDataException("A Pass 2 function schema is unavailable.");
                }

                writer.WriteStartObject();
                writer.WriteString("type", "function");
                writer.WriteStartObject("function");
                writer.WriteString("name", tool.FunctionName);
                writer.WriteString("description", tool.FunctionDescription);
                writer.WritePropertyName("parameters");
                writer.WriteRawValue(
                    tool.FunctionParameters.ToMemory().Span,
                    skipInputValidation: false);
                writer.WriteBoolean("strict", true);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return destination.ToArray();
    }
}
