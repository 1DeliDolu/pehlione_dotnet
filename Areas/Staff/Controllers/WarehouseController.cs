using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pehlione.Data;

namespace Pehlione.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = $"{IdentitySeed.RoleWarehouse},{IdentitySeed.RoleAdmin}")]
public sealed class WarehouseController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
