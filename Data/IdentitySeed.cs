using Microsoft.AspNetCore.Identity;
using Pehlione.Models.Identity;

namespace Pehlione.Data;

public static class IdentitySeed
{
    public const string RoleAdmin = "Admin";
    public const string RoleStaff = "Staff";
    public const string RolePurchasing = "Purchasing";
    public const string RoleIt = "IT";
    public const string RoleCustomer = "Customer";
    public const string RoleHr = "HR";
    public const string RoleWarehouse = "Warehouse";
    public const string RoleAccounting = "Accounting";
    public const string RoleCourier = "Courier";
    public const string RoleCustomerRelations = "CustomerRelations";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var config = services.GetRequiredService<IConfiguration>();

        await EnsureRoleAsync(roleManager, RoleAdmin);
        await EnsureRoleAsync(roleManager, RoleStaff);
        await EnsureRoleAsync(roleManager, RolePurchasing);
        await EnsureRoleAsync(roleManager, RoleIt);
        await EnsureRoleAsync(roleManager, RoleCustomer);
        await EnsureRoleAsync(roleManager, RoleHr);
        await EnsureRoleAsync(roleManager, RoleWarehouse);
        await EnsureRoleAsync(roleManager, RoleAccounting);
        await EnsureRoleAsync(roleManager, RoleCourier);
        await EnsureRoleAsync(roleManager, RoleCustomerRelations);

        await EnsureUserAsync(
            userManager,
            email: GetSeedValue(config, "AdminEmail", "admin@pehlione.local"),
            password: GetSeedValue(config, "AdminPassword", string.Empty),
            role: RoleAdmin);

        await EnsureUserAsync(
            userManager,
            email: GetSeedValue(config, "StaffEmail", "staff@pehlione.local"),
            password: GetSeedValue(config, "StaffPassword", string.Empty),
            role: RoleStaff);

        await EnsureUserAsync(
            userManager,
            email: GetSeedValue(config, "PurchasingEmail", "purchasing@pehlione.local"),
            password: GetSeedValue(config, "PurchasingPassword", string.Empty),
            role: RolePurchasing);

        await EnsureUserAsync(
            userManager,
            email: GetSeedValue(config, "ItEmail", "it@pehlione.local"),
            password: GetSeedValue(config, "ItPassword", string.Empty),
            role: RoleIt);

        await EnsureUserAsync(
            userManager,
            email: GetSeedValue(config, "CustomerEmail", "customer@pehlione.local"),
            password: GetSeedValue(config, "CustomerPassword", string.Empty),
            role: RoleCustomer);

        await EnsureUserAsync(
            userManager,
            email: GetSeedValue(config, "HrEmail", "hr@pehlione.local"),
            password: GetSeedValue(config, "HrPassword", "password"),
            role: RoleHr);

        await EnsureUserAsync(
            userManager,
            email: GetSeedValue(config, "WarehouseEmail", "warehouse@pehlione.local"),
            password: GetSeedValue(config, "WarehousePassword", "password"),
            role: RoleWarehouse);

        await EnsureUserAsync(
            userManager,
            email: GetSeedValue(config, "AccountingEmail", "accounting@pehlione.local"),
            password: GetSeedValue(config, "AccountingPassword", "password"),
            role: RoleAccounting);

        await EnsureUserAsync(
            userManager,
            email: GetSeedValue(config, "CourierEmail", "courier@pehlione.local"),
            password: GetSeedValue(config, "CourierPassword", "password"),
            role: RoleCourier);

        await EnsureUserAsync(
            userManager,
            email: GetSeedValue(config, "CustomerRelationsEmail", "customerrelations@pehlione.local"),
            password: GetSeedValue(config, "CustomerRelationsPassword", "password"),
            role: RoleCustomerRelations);
    }

    private static string GetSeedValue(IConfiguration config, string key, string fallback)
    {
        return Environment.GetEnvironmentVariable($"SEED__{key.ToUpperInvariant()}") ??
               config[$"Seed:{key}"] ??
               fallback;
    }

    private static async Task EnsureRoleAsync(RoleManager<IdentityRole> roleManager, string roleName)
    {
        if (await roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        var result = await roleManager.CreateAsync(new IdentityRole(roleName));
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Role create failed: {roleName} - {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }

    private static async Task EnsureUserAsync(UserManager<ApplicationUser> userManager, string email, string password, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return;
            }

            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var create = await userManager.CreateAsync(user, password);
            if (!create.Succeeded)
            {
                throw new InvalidOperationException($"User create failed: {email} - {string.Join("; ", create.Errors.Select(e => e.Description))}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            var addRole = await userManager.AddToRoleAsync(user, role);
            if (!addRole.Succeeded)
            {
                throw new InvalidOperationException($"AddToRole failed: {email} -> {role} - {string.Join("; ", addRole.Errors.Select(e => e.Description))}");
            }
        }
    }
}
