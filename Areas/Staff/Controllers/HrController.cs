using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Identity;
using Pehlione.Models.ViewModels.Staff;
using Pehlione.Security;

namespace Pehlione.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = $"{IdentitySeed.RoleHr},{IdentitySeed.RoleAdmin}")]
public sealed class HrController : Controller
{
    private static readonly string[] AllowedRoles =
    [
        IdentitySeed.RoleStaff,
        IdentitySeed.RolePurchasing,
        IdentitySeed.RoleWarehouse,
        IdentitySeed.RoleIt,
        IdentitySeed.RoleHr
    ];

    private readonly UserManager<ApplicationUser> _userManager;

    public HrController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var vm = await BuildVmAsync(ct);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePerson(string userId, string role, string? department, string? position, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return RedirectToAction(nameof(Index));

        if (!AllowedRoles.Contains(role))
        {
            TempData["HrError"] = "Gecersiz rol secimi.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null)
        {
            TempData["HrError"] = "Personel bulunamadi.";
            return RedirectToAction(nameof(Index));
        }

        var roles = await _userManager.GetRolesAsync(user);
        foreach (var existingRole in roles.Where(x => AllowedRoles.Contains(x)).ToList())
        {
            if (!string.Equals(existingRole, role, StringComparison.OrdinalIgnoreCase))
                await _userManager.RemoveFromRoleAsync(user, existingRole);
        }

        if (!await _userManager.IsInRoleAsync(user, role))
            await _userManager.AddToRoleAsync(user, role);

        await UpsertClaimAsync(user, PehlioneClaimTypes.Department, department);
        await UpsertClaimAsync(user, PehlioneClaimTypes.Position, position);

        TempData["HrSuccess"] = "Personel bilgileri guncellendi.";
        return RedirectToAction(nameof(Index));
    }

    private async Task UpsertClaimAsync(ApplicationUser user, string claimType, string? value)
    {
        var claims = await _userManager.GetClaimsAsync(user);
        var existing = claims.Where(x => x.Type == claimType).ToList();
        if (existing.Count > 0)
            await _userManager.RemoveClaimsAsync(user, existing);

        if (!string.IsNullOrWhiteSpace(value))
            await _userManager.AddClaimAsync(user, new Claim(claimType, value.Trim()));
    }

    private async Task<HrDashboardVm> BuildVmAsync(CancellationToken ct)
    {
        var users = await _userManager.Users
            .OrderBy(x => x.Email)
            .ToListAsync(ct);

        var rows = new List<HrPersonRowVm>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains(IdentitySeed.RoleCustomer))
                continue;

            var claims = await _userManager.GetClaimsAsync(user);
            rows.Add(new HrPersonRowVm
            {
                UserId = user.Id,
                Email = user.Email ?? user.UserName ?? "",
                Role = roles.FirstOrDefault(x => AllowedRoles.Contains(x)) ?? "-",
                Department = claims.FirstOrDefault(x => x.Type == PehlioneClaimTypes.Department)?.Value,
                Position = claims.FirstOrDefault(x => x.Type == PehlioneClaimTypes.Position)?.Value
            });
        }

        return new HrDashboardVm
        {
            People = rows
        };
    }
}
