using Microsoft.Extensions.AI;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Agents.OctoberBusiness;

/// <summary>Feature-local construction only; the host supplies the selected client and externalized prompt.</summary>
public static class OctoberBusinessModule
{
    public static OctoberResolutionExecutor CreateResolutionExecutor(IChatClient selectedClient,
        string resolutionInstructions, TimeProvider? timeProvider = null) =>
        new(selectedClient, resolutionInstructions, timeProvider);

    public static OctoberBusinessFinalizer CreateFinalizer(IOctoberOriginalBusinessFinalizationSlot? originalSlot,
        IOctoberFlowPublicationStore store, IOctoberFlowPlanningInputLoader loader,
        IChatClient selectedClient, string providerProfileId,
        BusinessProviderConfigurationProjection providerConfiguration, string synthesisInstructions,
        IOctoberResolutionDisclosureAuthority disclosure, TimeProvider? timeProvider = null) =>
        new(originalSlot, store, loader, selectedClient, providerProfileId, providerConfiguration,
            synthesisInstructions, disclosure, timeProvider);
}
