using Microsoft.AspNetCore.Identity;
using Pehlione.Models.Identity;

namespace Pehlione.Data;

public static class IdentitySeed
{
    public const string RoleAdmin = "Admin";
    public const string RoleStaff = "Staff";
    public const string RoleCustomer = "Customer";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var config = services.GetRequiredService<IConfiguration>();

        await EnsureRoleAsync(roleManager, RoleAdmin);
        await EnsureRoleAsync(roleManager, RoleStaff);
        await EnsureRoleAsync(roleManager, RoleCustomer);

        await EnsureUserAsync(
            userManager,
            email: config["Seed:AdminEmail"] ?? "admin@pehlione.local",
            password: config["Seed:AdminPassword"] ?? string.Empty,
            role: RoleAdmin);

        await EnsureUserAsync(
            userManager,
            email: config["Seed:StaffEmail"] ?? "staff@pehlione.local",
            password: config["Seed:StaffPassword"] ?? string.Empty,
            role: RoleStaff);

        await EnsureUserAsync(
            userManager,
            email: config["Seed:CustomerEmail"] ?? "customer@pehlione.local",
            password: config["Seed:CustomerPassword"] ?? string.Empty,
            role: RoleCustomer);
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
