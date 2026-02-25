using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Pehlione.Models.Auth;
using Pehlione.Models.Identity;
using Pehlione.Services;

namespace Pehlione.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthController(UserManager<ApplicationUser> userManager, IJwtTokenService jwtTokenService)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("token")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResponse>> Token([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var user = await _userManager.FindByEmailAsync(request.EmailOrUserName)
                   ?? await _userManager.FindByNameAsync(request.EmailOrUserName);

        if (user is null)
        {
            return Unauthorized(new { message = "Invalid credentials." });
        }

        var ok = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!ok)
        {
            return Unauthorized(new { message = "Invalid credentials." });
        }

        var (token, expiresAtUtc, roles) = await _jwtTokenService.CreateTokenAsync(user, ct);

        return Ok(new TokenResponse
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresAtUtc = expiresAtUtc,
            Roles = roles
        });
    }
}
