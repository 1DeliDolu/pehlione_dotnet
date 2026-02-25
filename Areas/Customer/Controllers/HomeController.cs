using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pehlione.Data;

namespace Pehlione.Areas.Customer.Controllers;

[Area("Customer")]
[Authorize(Roles = IdentitySeed.RoleCustomer)]
public sealed class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
