using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Pehlione.Models.Identity;
using Pehlione.Models.ViewModels;
using Pehlione.Services;

namespace Pehlione.Controllers;

[Authorize]
public sealed class TokenController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;

    public TokenController(UserManager<ApplicationUser> userManager, IJwtTokenService jwtTokenService)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var (token, expiresAtUtc, roles) = await _jwtTokenService.CreateTokenAsync(user, ct);

        return View(new TokenViewModel
        {
            AccessToken = token,
            ExpiresAtUtc = expiresAtUtc,
            Roles = roles
        });
    }
}
