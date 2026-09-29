using Vulgata.Tests.TestInfrastructure;
using System.Reflection;

namespace Vulgata.Tests.PreparedPass2;

public sealed class Pass2WorkerDispatcherContractTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreparedPass2WorkerDispatcher_IsPublicAndDeclaresOnlyApprovedSeam()
    {
        Type? dispatcher = typeof(Vulgata.Core.Entities.DocumentEdge).Assembly.GetType(
            "Vulgata.Core.DomainServices.IPreparedPass2WorkerDispatcher", throwOnError: false);
        Assert.NotNull(dispatcher);
        Assert.True(dispatcher!.IsInterface);
        MethodInfo[] methods = dispatcher.GetMethods();
        Assert.Single(methods);
        Assert.Contains("Dispatch", methods[0].Name, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("PreparedPass2Flow")]
    [InlineData("PreparedPass2RuntimeBinding")]
    [InlineData("Pass2ExecutionContracts")]
    [InlineData("Pass2RetrievalContracts")]
    public void Pass2ContractModule_IsPresent(string typeName)
    {
        Assembly core = typeof(Vulgata.Core.Entities.DocumentEdge).Assembly;
        Assert.NotNull(core.GetType($"Vulgata.Core.DomainServices.{typeName}", false));
    }
}
