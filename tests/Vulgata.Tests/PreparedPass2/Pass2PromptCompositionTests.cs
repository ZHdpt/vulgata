using System.Reflection;
using System.Text;

namespace Vulgata.Tests.PreparedPass2;

public sealed class Pass2PromptCompositionTests
{
    private const string PromptResourceName =
        "Vulgata.Agents.Prompts.pass2-business-logic-worker.v1.md";

    [Fact]
    public void Pass2Prompt_IsOneFiniteEmbeddedResource_WithStrictFunctionBoundary()
    {
        Assembly assembly = typeof(Vulgata.Agents.WorkerExecution.WorkerAgentDispatcher).Assembly;
        string[] resources = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith("pass2-business-logic-worker.v1.md", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Single(resources);
        Assert.Equal(PromptResourceName, resources[0]);
        using Stream stream = assembly.GetManifestResourceStream(resources[0])!;
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        string prompt = reader.ReadToEnd();

        Assert.NotEmpty(prompt);
        Assert.Contains("retrieve_code_logic", prompt, StringComparison.Ordinal);
        Assert.Contains("submit_business_logic", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("navigate_code", prompt, StringComparison.Ordinal);
        Pass2PrivacyAssertions.AssertContentDark(prompt);
    }
}
