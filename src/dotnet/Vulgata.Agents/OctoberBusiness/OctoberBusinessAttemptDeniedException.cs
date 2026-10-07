using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Agents.OctoberBusiness;

/// <summary>
/// The selected Business transport's own final admission guard refused dispatch.
/// The transport surfaces this only after actual task cleanup, preserving the exact status.
/// This exception does not prove that no dispatch occurred; callback and terminal observation
/// remain the authority for usage accounting. Unrelated transport failures are not admission denials.
/// </summary>
public sealed class OctoberBusinessAttemptDeniedException : Exception
{
    public OctoberBusinessAttemptDeniedException(ProviderAdmissionStatus status)
        : base("The selected Business attempt was refused by its final admission guard.")
    {
        if (!Enum.IsDefined(status) || status == ProviderAdmissionStatus.Granted)
            throw new ArgumentOutOfRangeException(nameof(status));
        Status = status;
    }

    public ProviderAdmissionStatus Status { get; }
}
