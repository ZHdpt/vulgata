using System.Reflection;

namespace Vulgata.Tests.PreparedPass2;

public sealed class Pass2ExecutionContractTests
{
    [Theory]
    [InlineData("Pass2Execution")]
    [InlineData("Pass2Omission")]
    [InlineData("Pass2JobExecution")]
    [InlineData("Pass2RetrievalBudget")]
    [InlineData("Pass2RetrievalReceipt")]
    [InlineData("Pass2RetrievalResult")]
    public void FoundationEntity_IsPresentWithDurableStateSurface(string entityName)
    {
        Type type = FindCoreType(entityName);
        Assert.NotNull(type.GetProperty("Id"));
        Assert.NotNull(type.GetProperty("State"));
    }

    [Fact]
    public void FlowGroup_ExposesPrePublicationIdentityAndCompleteness()
    {
        Type type = FindCoreType("FlowGroup");
        Assert.NotNull(type.GetProperty("BusinessLogicDocumentVersionId"));
        Assert.NotNull(type.GetProperty("LogicalDocumentStableKey"));
        Assert.NotNull(type.GetProperty("State"));
        Assert.NotNull(type.GetProperty("PlanFingerprint"));
    }

    [Fact]
    public void LogicalDocument_ExposesBusinessFlowSchemaIdentityWithoutBreakingCurrentPointer()
    {
        Type type = FindCoreType("LogicalDocument");
        Assert.NotNull(type.GetProperty("StableKey"));
        Assert.NotNull(type.GetProperty("CurrentVersionId"));
        Assert.NotNull(type.GetProperty("DocumentType"));
    }

    private static Type FindCoreType(string name) =>
        typeof(Vulgata.Core.Entities.DocumentEdge).Assembly.GetType(
            $"Vulgata.Core.Entities.{name}", throwOnError: false) ??
        throw new Xunit.Sdk.XunitException(
            $"Expected Story 12.4 public Core seam is absent: {name}.");
}
