using Microsoft.AspNetCore.Mvc;

namespace Pehlione.Areas.Staff.Controllers;

[Area("Staff")]
public sealed class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
