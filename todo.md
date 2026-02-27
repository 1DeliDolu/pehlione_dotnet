### A) Hedef

UI tarafında **Login/Logout** akışını ekleyip, `purchasing@local` ve `it@local` ile giriş yaparak **Purchasing yalnızca artırır**, **IT yalnızca siler** kuralını ekranda doğrulayacağız.

### B) CLI Komutu (maks. 1 komut)

```bash
dotnet run
```

### C) Dosya Değişiklikleri (tam içerik)

#### ./Controllers/AccountController.cs

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Pehlione.Models;
using Pehlione.ViewModels.Account;

namespace Pehlione.Controllers;

[AllowAnonymous]
public sealed class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AccountController(SignInManager<ApplicationUser> signInManager)
    {
        _signInManager = signInManager;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        var vm = new LoginVm { ReturnUrl = returnUrl };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var result = await _signInManager.PasswordSignInAsync(
            userName: vm.Email,
            password: vm.Password,
            isPersistent: vm.RememberMe,
            lockoutOnFailure: false);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Giriş başarısız. E-posta veya şifre hatalı.");
            return View(vm);
        }

        if (!string.IsNullOrWhiteSpace(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
            return Redirect(vm.ReturnUrl);

        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
```

#### ./ViewModels/Account/LoginVm.cs

```csharp
using System.ComponentModel.DataAnnotations;

namespace Pehlione.ViewModels.Account;

public sealed class LoginVm
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
```

#### ./Views/Account/Login.cshtml

```cshtml
@model Pehlione.ViewModels.Account.LoginVm

@{
    ViewData["Title"] = "Login";
}

<h1>Login</h1>

<form asp-action="Login" method="post" class="mt-3" style="max-width:420px;">
    @Html.AntiForgeryToken()

    <input type="hidden" asp-for="ReturnUrl" />

    <div class="mb-3">
        <label asp-for="Email" class="form-label"></label>
        <input asp-for="Email" class="form-control" />
        <span asp-validation-for="Email" class="text-danger"></span>
    </div>

    <div class="mb-3">
        <label asp-for="Password" class="form-label"></label>
        <input asp-for="Password" class="form-control" />
        <span asp-validation-for="Password" class="text-danger"></span>
    </div>

    <div class="form-check mb-3">
        <input asp-for="RememberMe" class="form-check-input" />
        <label asp-for="RememberMe" class="form-check-label"></label>
    </div>

    <button type="submit" class="btn btn-primary">Giriş</button>

    <div class="mt-3 text-muted" style="font-size:0.9rem;">
        <div><strong>Dev kullanıcıları:</strong></div>
        <div>purchasing@local / DevPass!12345</div>
        <div>it@local / DevPass!12345</div>
    </div>
</form>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

#### ./Views/Account/AccessDenied.cshtml

```cshtml
@{
    ViewData["Title"] = "Access Denied";
}

<h1>Erişim Engellendi</h1>
<p>Bu işlem için yetkin yok.</p>
<p>
    <a asp-controller="Home" asp-action="Index">Ana sayfa</a>
</p>
```

#### ./Program.cs

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pehlione.Data;
using Pehlione.Models;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// EF Core (MySQL)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

// Identity (Roles dahil)
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;

        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 10;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Cookie redirect paths (AccessDenied/Login)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// AuthN: Cookie (UI) + JWT (API/endpoint)
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    var jwt = builder.Configuration.GetSection("Jwt");
    var key = jwt["Key"] ?? throw new InvalidOperationException("Jwt:Key missing");

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ValidIssuer = jwt["Issuer"],
        ValidAudience = jwt["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});

// AuthZ: Policy bazlı net yetki ayrımı
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanIncreaseStock", policy =>
        policy.RequireRole("Purchasing", "Admin"));

    options.AddPolicy("CanDeleteStock", policy =>
        policy.RequireRole("IT", "Admin"));
});

var app = builder.Build();

// Seed roles + dev users (only in Development)
if (app.Environment.IsDevelopment())
{
    await SeedData.EnsureSeededAsync(app.Services);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

#### ./Views/Shared/_Layout.cshtml

> Not: Bu dosyanın içeriği template’e göre değişebilir. Aşağıdaki içerik **tam dosya** olarak veriliyor; mevcut layout’unu bununla değiştir.

```cshtml
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] - Pehlione</title>
    <link rel="stylesheet" href="~/lib/bootstrap/dist/css/bootstrap.min.css" />
    <link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
</head>
<body>
<header>
    <nav class="navbar navbar-expand-sm navbar-toggleable-sm navbar-light bg-white border-bottom box-shadow mb-3">
        <div class="container-fluid">
            <a class="navbar-brand" asp-area="" asp-controller="Home" asp-action="Index">Pehlione</a>
            <button class="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target=".navbar-collapse">
                <span class="navbar-toggler-icon"></span>
            </button>

            <div class="navbar-collapse collapse d-sm-inline-flex justify-content-between">
                <ul class="navbar-nav flex-grow-1">
                    <li class="nav-item">
                        <a class="nav-link text-dark" asp-controller="Home" asp-action="Index">Home</a>
                    </li>
                    <li class="nav-item">
                        <a class="nav-link text-dark" asp-controller="Inventory" asp-action="Index">Inventory</a>
                    </li>
                </ul>

                <ul class="navbar-nav">
                    @if (User?.Identity?.IsAuthenticated ?? false)
                    {
                        <li class="nav-item">
                            <span class="nav-link text-muted">
                                @User.Identity!.Name
                            </span>
                        </li>
                        <li class="nav-item">
                            <form asp-controller="Account" asp-action="Logout" method="post" class="d-inline">
                                @Html.AntiForgeryToken()
                                <button type="submit" class="btn btn-sm btn-outline-secondary">Logout</button>
                            </form>
                        </li>
                    }
                    else
                    {
                        <li class="nav-item">
                            <a class="nav-link text-dark" asp-controller="Account" asp-action="Login">Login</a>
                        </li>
                    }
                </ul>
            </div>
        </div>
    </nav>
</header>

<div class="container">
    <main role="main" class="pb-3">
        @RenderBody()
    </main>
</div>

<footer class="border-top footer text-muted">
    <div class="container">
        &copy; @DateTime.Now.Year - Pehlione
    </div>
</footer>

<script src="~/lib/bootstrap/dist/js/bootstrap.bundle.min.js"></script>
@await RenderSectionAsync("Scripts", required: false)
</body>
</html>
```

### D) Kısa Açıklama (en fazla 5 madde)

* Cookie auth ile UI login çalışır; policy’ler endpoint’leri **403/AccessDenied** ile korur.
* `Inventory/Increase` sadece Purchasing/Admin; `Inventory/DeleteProduct` sadece IT/Admin.
* UI’da butonlar role göre görünür ama asıl güvenlik **policy**’dedir.
* `dotnet run` ile açıp `/Account/Login` üzerinden giriş yapabilirsin.
* Test: Purchasing ile “Sil” butonu görünmez; URL ile POST denersen de AccessDenied alırsın.

### E) Git Commit

**Commit mesajı:** `Add UI login/logout and access denied page`

```bash
git add -A && git commit -m "Add UI login/logout and access denied page"
```

“**bitti**” yazınca bir sonraki adımda: stok artırmayı **atomik** hale getireceğiz (yarış durumlarına dayanıklı) ve ayrıca **Purchasing’in silmeye kesinlikle erişemediğini** endpoint test senaryosuyla netleştireceğiz.


“**bitti**” yazınca sıradaki adım: **UI Login/Logout** (AccountController + Views) ekleyip `purchasing@local` ve `it@local` ile giriş yaparak Inventory ekranında butonların doğru kısıtlandığını canlı test edeceğiz.
