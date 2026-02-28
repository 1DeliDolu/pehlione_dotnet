using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pehlione.Data;

namespace Pehlione.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = $"{IdentitySeed.RoleCustomerRelations},{IdentitySeed.RoleAdmin}")]
public sealed class CustomerRelationsController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}
