namespace Vulgata.Shared;

public static class RoleNames
{
    public const string Administrator = "Administrator";
    public const string SystemOwner = "SystemOwner";
    public const string User = "User";

    public const string ManagementRolesCsv = Administrator + "," + SystemOwner;

    public static readonly string[] SeededRoles = [Administrator, SystemOwner, User];
}
