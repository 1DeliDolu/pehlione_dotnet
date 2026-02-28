using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Pehlione.Data;
using Pehlione.Models.Identity;
using Pehlione.Models.ViewModels;
using Pehlione.Security;

namespace Pehlione.Controllers;

public sealed class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel
        {
            ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? Url.Content("~/") : returnUrl
        });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.EmailOrUserName)
            ?? await _userManager.FindByNameAsync(model.EmailOrUserName);

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Kullanici adi/e-posta veya parola hatali.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            userName: user.UserName!,
            password: model.Password,
            isPersistent: model.RememberMe,
            lockoutOnFailure: false);

        if (result.Succeeded)
        {
            var claims = await _userManager.GetClaimsAsync(user);
            var mustChange = claims.Any(c => c.Type == PehlioneClaimTypes.MustChangePassword && c.Value == "true");
            if (mustChange)
            {
                var ru = (string.IsNullOrWhiteSpace(model.ReturnUrl) ? Url.Content("~/") : model.ReturnUrl)!;
                return RedirectToAction(nameof(ChangePassword), new { returnUrl = ru });
            }

            var forceRoleDashboard = await _userManager.IsInRoleAsync(user, IdentitySeed.RoleAdmin)
                || await _userManager.IsInRoleAsync(user, IdentitySeed.RoleStaff)
                || await _userManager.IsInRoleAsync(user, IdentitySeed.RolePurchasing)
                || await _userManager.IsInRoleAsync(user, IdentitySeed.RoleIt)
                || await _userManager.IsInRoleAsync(user, IdentitySeed.RoleHr)
                || await _userManager.IsInRoleAsync(user, IdentitySeed.RoleWarehouse)
                || await _userManager.IsInRoleAsync(user, IdentitySeed.RoleAccounting);

            if (!forceRoleDashboard && !string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }
            return await RedirectToRoleDashboardAsync(user);
        }

        ModelState.AddModelError(string.Empty, "Kullanici adi/e-posta veya parola hatali.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Denied()
    {
        return View();
    }

    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword(string? returnUrl = null)
    {
        return View(new ChangePasswordViewModel
        {
            ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? Url.Content("~/") : returnUrl
        });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var err in result.Errors)
            {
                ModelState.AddModelError(string.Empty, err.Description);
            }

            return View(model);
        }

        var claims = await _userManager.GetClaimsAsync(user);
        var mustChangeClaims = claims.Where(c => c.Type == PehlioneClaimTypes.MustChangePassword).ToList();
        if (mustChangeClaims.Count > 0)
        {
            await _userManager.RemoveClaimsAsync(user, mustChangeClaims);
        }

        await _signInManager.RefreshSignInAsync(user);

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }
        return await RedirectToRoleDashboardAsync(user);
    }

    private async Task<IActionResult> RedirectToRoleDashboardAsync(ApplicationUser user)
    {
        if (await _userManager.IsInRoleAsync(user, IdentitySeed.RoleAdmin))
            return RedirectToAction("Index", "Home", new { area = "Admin" });

        if (await _userManager.IsInRoleAsync(user, IdentitySeed.RolePurchasing))
            return RedirectToAction("Index", "Purchasing", new { area = "Staff" });

        if (await _userManager.IsInRoleAsync(user, IdentitySeed.RoleIt))
            return RedirectToAction("Index", "It", new { area = "Staff" });

        if (await _userManager.IsInRoleAsync(user, IdentitySeed.RoleHr))
            return RedirectToAction("Index", "Hr", new { area = "Staff" });

        if (await _userManager.IsInRoleAsync(user, IdentitySeed.RoleWarehouse))
            return RedirectToAction("Index", "Warehouse", new { area = "Staff" });

        if (await _userManager.IsInRoleAsync(user, IdentitySeed.RoleAccounting))
            return RedirectToAction("Index", "Accounting", new { area = "Staff" });

        if (await _userManager.IsInRoleAsync(user, IdentitySeed.RoleStaff))
            return RedirectToAction("Index", "Home", new { area = "Staff" });

        if (await _userManager.IsInRoleAsync(user, IdentitySeed.RoleCustomer))
            return RedirectToAction("Index", "Home", new { area = "Customer" });

        return RedirectToAction("Index", "Home");
    }
}
