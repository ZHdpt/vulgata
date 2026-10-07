using Microsoft.Extensions.AI;

namespace Vulgata.Agents.OctoberBusiness;

/// <summary>Feature-local construction only; the host supplies the selected client and externalized prompt.</summary>
public static class OctoberBusinessModule
{
    public static OctoberResolutionExecutor CreateResolutionExecutor(IChatClient selectedClient,
        string resolutionInstructions, TimeProvider? timeProvider = null) =>
        new(selectedClient, resolutionInstructions, timeProvider);
}
