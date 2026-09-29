using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: Xunit.TestFramework("Vulgata.Tests.TestInfrastructure.SuiteTestFramework", "Vulgata.Tests")]

namespace Vulgata.Tests.TestInfrastructure;

/// <summary>Exclude disabled cases before xUnit constructs collection or class fixtures.</summary>
public sealed class SuiteTestFramework(IMessageSink diagnosticMessageSink) : XunitTestFramework(diagnosticMessageSink)
{
    protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName) =>
        new SuiteExecutor(assemblyName, SourceInformationProvider, DiagnosticMessageSink);

    private sealed class SuiteExecutor(
        AssemblyName assemblyName,
        ISourceInformationProvider sourceInformationProvider,
        IMessageSink diagnosticMessageSink)
        : XunitTestFrameworkExecutor(assemblyName, sourceInformationProvider, diagnosticMessageSink)
    {
        protected override void RunTestCases(
            IEnumerable<IXunitTestCase> testCases,
            IMessageSink executionMessageSink,
            ITestFrameworkExecutionOptions executionOptions)
        {
            var selected = new List<IXunitTestCase>();
            foreach (IXunitTestCase test in testCases)
            {
                if (!test.Traits.TryGetValue("Suite", out List<string>? suites) || suites.Count != 1)
                {
                    throw new InvalidOperationException($"Test {test.DisplayName} requires exactly one Suite trait.");
                }

                if (test.TestMethod.Method is not IReflectionMethodInfo reflected)
                    throw new InvalidOperationException($"Cannot inspect guarded attribute metadata for {test.DisplayName}.");
                SuiteAuthorization.ValidateMetadata(reflected.MethodInfo.GetCustomAttributes<Xunit.FactAttribute>(true), suites);

                if (SuiteAuthorization.IsEnabled(suites[0]))
                {
                    selected.Add(test);
                }
                else
                {
                    DiagnosticMessageSink.OnMessage(new DiagnosticMessage($"Excluded disabled suite: {test.DisplayName}"));
                }
            }

            base.RunTestCases(selected, executionMessageSink, executionOptions);
        }
    }
}
