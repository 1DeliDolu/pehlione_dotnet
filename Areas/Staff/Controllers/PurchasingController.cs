using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pehlione.Data;

namespace Pehlione.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = $"{IdentitySeed.RolePurchasing},{IdentitySeed.RoleAdmin}")]
public sealed class PurchasingController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
