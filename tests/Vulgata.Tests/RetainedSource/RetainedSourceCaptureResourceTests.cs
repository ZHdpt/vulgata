using Vulgata.Tests.TestInfrastructure;
using Xunit.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;

namespace Vulgata.Tests.RetainedSource;

[Collection(RetainedSourceProcessGlobalCollection.Name)]
public sealed class RetainedSourceCaptureResourceTests(ITestOutputHelper output)
{
    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_SixtyFourMiBCommittedPayload_StaysBelowBoundedAllocationCeiling()
    {
        const int blobSize = 8 * 1024 * 1024;
        const int blobCount = 8;
        const long payloadBytes = (long)blobSize * blobCount;
        const long allocationCeiling = 128L * 1024 * 1024;
        var files = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal);
        for (int index = 0; index < blobCount; index++)
        {
            byte[] content = new byte[blobSize];
            Array.Fill(content, (byte)(index + 1));
            files[$"src/resource-{index:D2}.bin"] = content;
        }

        await using RetainedSourceFixture source =
            await RetainedSourceFixture.CreateAsync(files);
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(source);
        IRetainedSourceApplication application =
            appFixture.Scope.ServiceProvider.GetRequiredService<IRetainedSourceApplication>();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long before = GC.GetTotalAllocatedBytes(true);
        SourceCaptureResult result = await application.AcquireAsync(
            source.RepositoryId,
            await source.Repository.RunGitAsync(["symbolic-ref", "HEAD"]));
        long allocated = GC.GetTotalAllocatedBytes(true) - before;
        double ratio = (double)allocated / payloadBytes;
        output.WriteLine(
            $"PayloadBytes={payloadBytes}; AllocatedBytes={allocated}; AllocationToPayloadRatio={ratio:F3}; CeilingBytes={allocationCeiling}");

        Assert.Equal(SourceCaptureStatus.Completed, result.Status);
        Assert.True(
            allocated < allocationCeiling,
            $"Expected capture allocation below {allocationCeiling} bytes; actual {allocated} bytes ({ratio:F3}x payload).");
    }
}
