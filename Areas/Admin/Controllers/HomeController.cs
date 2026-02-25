using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pehlione.Data;

namespace Pehlione.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = IdentitySeed.RoleAdmin)]
public sealed class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
