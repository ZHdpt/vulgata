namespace Vulgata.Tests.RetainedSource;

internal static class RetainedSourceProcessGlobalCollection
{
    internal const string Name = "retained-source-process-global";
}

[CollectionDefinition(RetainedSourceProcessGlobalCollection.Name, DisableParallelization = true)]
public sealed class RetainedSourceProcessGlobalCollectionDefinition
{
}
