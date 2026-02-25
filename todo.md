A) **Hedef (Türkçe)**
“Kişiler (Customer) / Çalışanlar (Staff) / Admin” bölümlerini **rol bazlı** korumaya alıp, Identity ile **Login/Logout** UI’si ekleyeceğiz; böylece her rol yalnızca kendi Area’sına erişebilecek.

B) **CLI Komutu (maksimum 1 komut)**

```bash
dotnet run
```

C) **Dosya Değişiklikleri (sadece değişen/eklenen dosyalar, TAM içerik)**

`./Areas/Admin/Controllers/HomeController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pehlione.Data;

namespace Pehlione.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = IdentitySeed.RoleAdmin)]
public sealed class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
```

`./Areas/Staff/Controllers/HomeController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pehlione.Data;

namespace Pehlione.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = IdentitySeed.RoleStaff)]
public sealed class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
```

`./Areas/Customer/Controllers/HomeController.cs`

```csharp
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
```

`./Controllers/AccountController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Pehlione.Models.Identity;
using Pehlione.Models.ViewModels;

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

        // Email ile dene, yoksa username olarak dene
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
}
```

`./Models/ViewModels/LoginViewModel.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace Pehlione.Models.ViewModels;

public sealed class LoginViewModel
{
    [Required]
    [Display(Name = "E-posta veya kullanıcı adı")]
    public string EmailOrUserName { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Parola")]
    public string Password { get; set; } = "";

    [Display(Name = "Beni hatırla")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
```

`./Views/Account/Login.cshtml`

```cshtml
@model Pehlione.Models.ViewModels.LoginViewModel
@{
    ViewData["Title"] = "Giriş";
}

<div class="container" style="max-width: 520px;">
    <h1 class="h3 mb-3">Giriş</h1>

    <div class="alert alert-info">
        Dev kullanıcıları seed edildiyse: Admin / Staff / Customer e-postaları <code>appsettings.Development.json</code> içindeki <code>Seed</code> bölümündedir.
    </div>

    <form asp-action="Login" method="post">
        @Html.AntiForgeryToken()
        <input type="hidden" asp-for="ReturnUrl" />

        <div asp-validation-summary="ModelOnly" class="text-danger mb-3"></div>

        <div class="mb-3">
            <label asp-for="EmailOrUserName" class="form-label"></label>
            <input asp-for="EmailOrUserName" class="form-control" autocomplete="username" />
            <span asp-validation-for="EmailOrUserName" class="text-danger"></span>
        </div>

        <div class="mb-3">
            <label asp-for="Password" class="form-label"></label>
            <input asp-for="Password" class="form-control" autocomplete="current-password" />
            <span asp-validation-for="Password" class="text-danger"></span>
        </div>

        <div class="form-check mb-3">
            <input asp-for="RememberMe" class="form-check-input" />
            <label asp-for="RememberMe" class="form-check-label"></label>
        </div>

        <button type="submit" class="btn btn-primary w-100">Giriş yap</button>
    </form>
</div>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

`./Views/Account/Denied.cshtml`

```cshtml
@{
    ViewData["Title"] = "Erişim Reddedildi";
}

<div class="container" style="max-width: 820px;">
    <h1 class="h3 mb-3">Erişim reddedildi</h1>
    <p>Bu sayfaya erişim yetkin yok. Doğru rol ile giriş yapmalısın.</p>

    <div class="d-flex gap-2">
        <a class="btn btn-outline-primary" asp-controller="Account" asp-action="Login">Giriş</a>
        <a class="btn btn-outline-secondary" asp-controller="Home" asp-action="Index">Ana sayfa</a>
    </div>
</div>
```

`./Views/Home/Index.cshtml`

```cshtml
@{
    ViewData["Title"] = "Home Page";
}

<div class="text-center">
    <h1 class="display-4">Pehlione</h1>
    <p>Prototip alışveriş sitesi - bölüm iskeletleri</p>

    <hr />

    @if (User?.Identity?.IsAuthenticated == true)
    {
        <p class="mb-2">
            Giriş yapıldı: <strong>@User.Identity!.Name</strong>
        </p>

        <form asp-controller="Account" asp-action="Logout" method="post" class="mb-3">
            @Html.AntiForgeryToken()
            <button type="submit" class="btn btn-outline-dark">Çıkış</button>
        </form>

        <h2 class="h4">Bölümler (Rolüne göre)</h2>
        <div class="d-flex justify-content-center gap-3 flex-wrap">
            @if (User.IsInRole("Customer"))
            {
                <a class="btn btn-outline-primary" asp-area="Customer" asp-controller="Home" asp-action="Index">Müşteri</a>
            }
            @if (User.IsInRole("Staff"))
            {
                <a class="btn btn-outline-secondary" asp-area="Staff" asp-controller="Home" asp-action="Index">Çalışanlar</a>
            }
            @if (User.IsInRole("Admin"))
            {
                <a class="btn btn-outline-danger" asp-area="Admin" asp-controller="Home" asp-action="Index">Admin</a>
            }
        </div>
    }
    else
    {
        <p class="mb-3">Bölümler rol ile korunuyor. Devam etmek için giriş yap.</p>
        <a class="btn btn-primary" asp-controller="Account" asp-action="Login">Giriş</a>
    }
</div>
```

D) **Kısa Açıklama (en fazla 5 madde, öğretici)**

* Area controller’larına `[Authorize(Roles="...")]` ekleyerek rol bazlı erişimi netleştirdik. ([Microsoft Learn][1])
* `AccountController` içinde `SignInManager` ile `PasswordSignInAsync` yapıp cookie tabanlı oturum açıyoruz (JWT’ye sonra geçeceğiz). ([Microsoft Learn][2])
* `ConfigureApplicationCookie`’deki `LoginPath` ve `AccessDeniedPath` artık gerçek endpoint’lere bağlı. ([Microsoft Learn][3])
* Ana sayfada butonları `User.IsInRole(...)` ile role göre gösteriyoruz; böylece UI tarafında da “kendi bölümün” kuralı görünür oluyor. ([Microsoft Learn][1])
* Not: Login’in gerçekten çalışması için Identity tablolarının DB’de olması gerekir; bir sonraki adım migration + database update olacak.

E) **Git Commit**

* Commit mesajı: `Add login UI and role-guarded areas (Admin/Staff/Customer)`
* Komut:

```bash
git add -A && git commit -m "Add login UI and role-guarded areas (Admin/Staff/Customer)"
```
