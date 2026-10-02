namespace Carpediem.Modules.Identity.Authorization;

public static class CarpediemRoles
{
    public const string Admin = "Admin";
    public const string ChiefLeader = "ChiefLeader";
    public const string Leader = "Leader";

    public static readonly string[] All = [Admin, ChiefLeader, Leader];
}

public static class AuthorizationPolicies
{
    public const string RequireAdmin = nameof(RequireAdmin);
    public const string RequireChiefLeader = nameof(RequireChiefLeader);
    public const string RequireLeader = nameof(RequireLeader);
    public const string RequireChiefLeaderOrAdmin = nameof(RequireChiefLeaderOrAdmin);
    public const string RequireLeaderOrAdmin = nameof(RequireLeaderOrAdmin);
}
