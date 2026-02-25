using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Identity;
using Pehlione.Models.ViewModels.Admin;

namespace Pehlione.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = IdentitySeed.RoleAdmin)]
public sealed class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UsersController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var users = await _userManager.Users
            .OrderBy(u => u.Email)
            .ToListAsync(ct);

        var items = new List<UserListItemVm>(users.Count);

        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            items.Add(new UserListItemVm
            {
                Email = u.Email ?? "",
                UserName = u.UserName ?? "",
                Roles = roles.OrderBy(x => x).ToArray()
            });
        }

        return View(items);
    }
}
