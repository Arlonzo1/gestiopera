namespace BillaresPos.Domain.Identity;

public static class RoleNames
{
    public const string Owner = "OWNER";
    public const string Administrator = "ADMINISTRATOR";
    public const string Cashier = "CASHIER";

    public static readonly string[] All = [Owner, Administrator, Cashier];
}
