A) **Hedef (Türkçe)**
Yeni oluşturulan kullanıcıların **ilk girişte şifre değiştirmesini zorunlu** yapacağız. Bunu DB şemasına dokunmadan, Identity’nin mevcut tablolarını kullanarak **User Claim** ile işaretleyeceğiz (`must_change_password=true`). Login sonrası bu claim varsa kullanıcı doğrudan **/Account/ChangePassword**’a yönlenecek.

B) **CLI Komutu (maksimum 1 komut)**

```bash
dotnet build
```

C) **Dosya Değişiklikleri (sadece değişen/eklenen dosyalar, TAM içerik)**

`./Security/PehlioneClaimTypes.cs`

```csharp
namespace Pehlione.Security;

public static class PehlioneClaimTypes
{
    public const string MustChangePassword = "pehlione.must_change_password";
}
```

`./Models/ViewModels/ChangePasswordViewModel.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace Pehlione.Models.ViewModels;

public sealed class ChangePasswordViewModel
{
    [Required]
    public string? ReturnUrl { get; set; }

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Mevcut parola")]
    public string CurrentPassword { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni parola")]
    public string NewPassword { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni parola (tekrar)")]
    [Compare(nameof(NewPassword), ErrorMessage = "Yeni parolalar eşleşmiyor.")]
    public string ConfirmNewPassword { get; set; } = "";
}
```

`./Views/Account/ChangePassword.cshtml`

```cshtml
@model Pehlione.Models.ViewModels.ChangePasswordViewModel
@{
    ViewData["Title"] = "Şifre Değiştir";
}

<div class="container" style="max-width: 560px;">
    <h1 class="h3 mb-3">Şifre değiştir</h1>

    <div class="alert alert-warning">
        Bu hesap için <strong>ilk girişte şifre değişimi</strong> zorunlu olabilir.
    </div>

    <form asp-action="ChangePassword" method="post">
        @Html.AntiForgeryToken()
        <input type="hidden" asp-for="ReturnUrl" />

        <div asp-validation-summary="ModelOnly" class="text-danger mb-3"></div>

        <div class="mb-3">
            <label asp-for="CurrentPassword" class="form-label"></label>
            <input asp-for="CurrentPassword" class="form-control" autocomplete="current-password" />
            <span asp-validation-for="CurrentPassword" class="text-danger"></span>
        </div>

        <div class="mb-3">
            <label asp-for="NewPassword" class="form-label"></label>
            <input asp-for="NewPassword" class="form-control" autocomplete="new-password" />
            <span asp-validation-for="NewPassword" class="text-danger"></span>
        </div>

        <div class="mb-3">
            <label asp-for="ConfirmNewPassword" class="form-label"></label>
            <input asp-for="ConfirmNewPassword" class="form-control" autocomplete="new-password" />
            <span asp-validation-for="ConfirmNewPassword" class="text-danger"></span>
        </div>

        <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary">Kaydet</button>
            <a class="btn btn-outline-secondary" asp-controller="Home" asp-action="Index">Ana sayfa</a>
        </div>
    </form>
</div>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

`./Controllers/AccountController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.EmailOrUserName)
                   ?? await _userManager.FindByNameAsync(model.EmailOrUserName);

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Kullanıcı adı/e-posta veya parola hatalı.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            userName: user.UserName!,
            password: model.Password,
            isPersistent: model.RememberMe,
            lockoutOnFailure: false);

        if (result.Succeeded)
        {
            // İlk girişte şifre değişimi zorunluluğu (claim tabanlı)
            var claims = await _userManager.GetClaimsAsync(user);
            var mustChange = claims.Any(c => c.Type == PehlioneClaimTypes.MustChangePassword && c.Value == "true");
            if (mustChange)
            {
                var ru = (string.IsNullOrWhiteSpace(model.ReturnUrl) ? Url.Content("~/") : model.ReturnUrl)!;
                return RedirectToAction(nameof(ChangePassword), new { returnUrl = ru });
            }

            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError(string.Empty, "Kullanıcı adı/e-posta veya parola hatalı.");
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
            return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return Challenge();

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var err in result.Errors)
                ModelState.AddModelError(string.Empty, err.Description);

            return View(model);
        }

        // Zorunlu değişim claim'ini temizle
        var claims = await _userManager.GetClaimsAsync(user);
        var mustChangeClaims = claims.Where(c => c.Type == PehlioneClaimTypes.MustChangePassword).ToList();
        if (mustChangeClaims.Count > 0)
        {
            await _userManager.RemoveClaimsAsync(user, mustChangeClaims);
        }

        await _signInManager.RefreshSignInAsync(user);

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        return RedirectToAction("Index", "Home");
    }
}
```

`./Areas/Admin/Controllers/UsersController.cs`

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Identity;
using Pehlione.Models.ViewModels.Admin;
using Pehlione.Security;

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

    public UsersController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
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
            ModelState.AddModelError(nameof(model.Role), "Geçersiz rol seçimi.");
        }

        if (!ModelState.IsValid)
            return View(model);

        var existing = await _userManager.FindByEmailAsync(model.Email);
        if (existing is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "Bu e-posta zaten kayıtlı.");
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
                ModelState.AddModelError(string.Empty, err.Description);

            return View(model);
        }

        var addRole = await _userManager.AddToRoleAsync(user, model.Role);
        if (!addRole.Succeeded)
        {
            foreach (var err in addRole.Errors)
                ModelState.AddModelError(string.Empty, err.Description);

            return View(model);
        }

        // İlk girişte şifre değişimi zorunlu
        var claims = await _userManager.GetClaimsAsync(user);
        var hasFlag = claims.Any(c => c.Type == PehlioneClaimTypes.MustChangePassword);
        if (!hasFlag)
        {
            await _userManager.AddClaimAsync(user, new Claim(PehlioneClaimTypes.MustChangePassword, "true"));
        }

        return RedirectToAction(nameof(Index));
    }
}
```

D) **Kısa Açıklama (en fazla 5 madde, öğretici)**

* Yeni kullanıcı oluşturulunca `pehlione.must_change_password=true` claim’i ekleniyor (DB migration yok).
* Login başarılı olunca bu claim kontrol ediliyor; varsa kullanıcı zorunlu olarak `/Account/ChangePassword`’a gidiyor.
* Şifre değişince claim kaldırılıyor ve `RefreshSignInAsync` ile cookie güncelleniyor.
* Admin/Staff/Customer hepsi için geçerli (admin isterse yine değiştirebilir).
* Sonraki adımda istersen: **Admin “reset password”** (token ile) akışını da ekleyebiliriz.

E) **Git Commit**

* Commit mesajı: `Force first-login password change via Identity claim`
* Komut:

```bash
git add -A && git commit -m "Force first-login password change via Identity claim"
```

Bunu yaptıktan sonra test: Admin panelinden yeni bir Staff oluştur → o kullanıcıyla login ol → **direkt ChangePassword** sayfasına düşüyorsan tamam. Olduysa **“bitti”** yaz.
