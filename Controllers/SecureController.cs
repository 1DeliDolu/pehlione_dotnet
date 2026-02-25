using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pehlione.Data;

namespace Pehlione.Controllers;

[ApiController]
[Route("api/secure")]
public sealed class SecureController : ControllerBase
{
    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public IActionResult Me()
    {
        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .Distinct()
            .ToArray();

        return Ok(new
        {
            name = User.Identity?.Name,
            roles
        });
    }

    [HttpGet("admin")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = IdentitySeed.RoleAdmin)]
    public IActionResult AdminOnly() => Ok(new { ok = true, area = "Admin" });

    [HttpGet("staff")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = IdentitySeed.RoleStaff)]
    public IActionResult StaffOnly() => Ok(new { ok = true, area = "Staff" });

    [HttpGet("customer")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = IdentitySeed.RoleCustomer)]
    public IActionResult CustomerOnly() => Ok(new { ok = true, area = "Customer" });
}
