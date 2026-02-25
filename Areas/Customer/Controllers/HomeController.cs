using Microsoft.AspNetCore.Mvc;

namespace Pehlione.Areas.Customer.Controllers;

[Area("Customer")]
public sealed class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
