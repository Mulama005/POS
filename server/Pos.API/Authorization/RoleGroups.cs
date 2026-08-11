namespace Pos.Api.Authorization;

public static class RoleGroups
{
    public const string AdminOnly = "Admin";
    public const string ManagerOrAdmin = "Manager,Admin";
    public const string CashierOnly = "Cashier";
    public const string TechnicianOnly = "Technician";

    /// Anyone with register/payment access — i.e. everyone except Technician.
    public const string RegisterCapableRoles = "Cashier,Manager,Admin";
}