namespace Vulgata.Core.DomainServices;

/// <summary>Supported finite lifetime for an explicitly configured execution owner, not an operation budget.</summary>
public static class ExecutionOwnerLifetime
{
    public const int MaximumSeconds = 86400;
    public static TimeSpan Maximum => TimeSpan.FromSeconds(MaximumSeconds);
}
