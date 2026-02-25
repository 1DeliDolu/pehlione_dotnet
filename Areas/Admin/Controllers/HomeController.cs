using Microsoft.AspNetCore.Mvc;

namespace Pehlione.Areas.Admin.Controllers;

[Area("Admin")]
public sealed class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
