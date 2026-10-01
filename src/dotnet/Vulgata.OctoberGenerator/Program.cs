using System.Text.Json;
using Vulgata.Core.DomainServices;

namespace Vulgata.OctoberGenerator;

public static class OctoberGeneratorEntryPoint
{
    public static async Task<int> Main()
    {
        // Direct host configuration and target composition follow the T018 behavioral RED.
        string? modeText = Environment.GetEnvironmentVariable("October__Mode");
        string? targetId = Environment.GetEnvironmentVariable("October__TargetId");
        string? batchText = Environment.GetEnvironmentVariable("October__BatchId");
        string? setupRequestJson = Environment.GetEnvironmentVariable("October__SetupRequestJson");
        if (modeText is not ("prepare" or "select") ||
            string.IsNullOrWhiteSpace(targetId) ||
            !Guid.TryParse(batchText, out Guid batchId) || batchId == Guid.Empty)
        {
            return 1;
        }

        OctoberSetupRequest? setupRequest = null;
        if (modeText == "prepare")
        {
            try
            {
                setupRequest = JsonSerializer.Deserialize<OctoberSetupRequest>(setupRequestJson ?? string.Empty);
            }
            catch (JsonException)
            {
                return 1;
            }

            if (setupRequest is null ||
                !string.Equals(setupRequest.TargetId, targetId, StringComparison.Ordinal) ||
                setupRequest.BatchId != batchId)
            {
                return 1;
            }
        }

        _ = new OctoberGeneratorCommand(
            modeText == "prepare" ? OctoberGeneratorMode.Prepare : OctoberGeneratorMode.Select,
            targetId,
            batchId,
            setupRequest);
        return 1;
    }
}
