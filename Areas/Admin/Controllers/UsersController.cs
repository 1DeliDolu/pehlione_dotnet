using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Identity;
using Pehlione.Models.ViewModels.Admin;
using Pehlione.Security;
using Pehlione.Services;

namespace Pehlione.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = IdentitySeed.RoleAdmin)]
public sealed class UsersController : Controller
{
    private static readonly string[] AllowedRoles =
    [
        IdentitySeed.RoleCustomer,
        IdentitySeed.RoleStaff,
        IdentitySeed.RoleAdmin
    ];

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAppEmailSender _emailSender;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        IAppEmailSender emailSender,
        ILogger<UsersController> logger)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var users = await _userManager.Users
            .OrderBy(u => u.Email)
            .ToListAsync(ct);

        var items = new List<UserListItemVm>(users.Count);

        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            items.Add(new UserListItemVm
            {
                Email = u.Email ?? "",
                UserName = u.UserName ?? "",
                Roles = roles.OrderBy(x => x).ToArray()
            });
        }

        return View(items);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateUserVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserVm model, CancellationToken ct)
    {
        if (!AllowedRoles.Contains(model.Role))
        {
            ModelState.AddModelError(nameof(model.Role), "Gecersiz rol secimi.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existing = await _userManager.FindByEmailAsync(model.Email);
        if (existing is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "Bu e-posta zaten kayitli.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true
        };

        var create = await _userManager.CreateAsync(user, model.Password);
        if (!create.Succeeded)
        {
            foreach (var err in create.Errors)
            {
                ModelState.AddModelError(string.Empty, err.Description);
            }

            return View(model);
        }

        var addRole = await _userManager.AddToRoleAsync(user, model.Role);
        if (!addRole.Succeeded)
        {
            foreach (var err in addRole.Errors)
            {
                ModelState.AddModelError(string.Empty, err.Description);
            }

            return View(model);
        }

        var claims = await _userManager.GetClaimsAsync(user);
        var hasFlag = claims.Any(c => c.Type == PehlioneClaimTypes.MustChangePassword);
        if (!hasFlag)
        {
            await _userManager.AddClaimAsync(user, new Claim(PehlioneClaimTypes.MustChangePassword, "true"));
        }

        try
        {
            var subject = "Pehlione - Hesabin olusturuldu";
            var body = $@"
                <p>Merhaba,</p>
                <p>Pehlione hesabin olusturuldu.</p>
                <ul>
                    <li><strong>E-posta/Kullanici adi:</strong> {System.Net.WebUtility.HtmlEncode(model.Email)}</li>
                    <li><strong>Rol:</strong> {System.Net.WebUtility.HtmlEncode(model.Role)}</li>
                </ul>
                <p>Ilk giriste sifre degistirmen istenecektir.</p>
                <p>Giris: <a href=""{Request.Scheme}://{Request.Host}/Account/Login"">/Account/Login</a></p>
            ";

            await _emailSender.SendAsync(model.Email, subject, body, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DEV email send failed for new user: {Email}", model.Email);
        }

        return RedirectToAction(nameof(Index));
    }
}
