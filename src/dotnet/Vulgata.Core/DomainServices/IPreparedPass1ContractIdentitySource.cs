namespace Vulgata.Core.DomainServices;

public interface IPreparedPass1ContractIdentitySource
{
    PreparedPass1ContractIdentity? GetCurrent();
}
