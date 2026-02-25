using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pehlione.Data;

namespace Pehlione.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = IdentitySeed.RoleStaff)]
public sealed class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
