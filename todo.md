A) **Hedef (Türkçe)**
Admin panelinde `/Admin/Users` üzerinden **yeni kullanıcı oluşturma** (Customer / Staff / Admin rolü seçerek) ekranını ekleyeceğiz. Böylece “kişiler/çalışanlar/admin” için demo seed’e bağlı kalmadan kullanıcı açabileceksin.

B) **CLI Komutu (maksimum 1 komut)**

```bash
dotnet build
```

C) **Dosya Değişiklikleri (sadece değişen/eklenen dosyalar, TAM içerik)**

`./Areas/Admin/Controllers/UsersController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Identity;
using Pehlione.Models.ViewModels.Admin;

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

        return RedirectToAction(nameof(Index));
    }
}
```

`./Models/ViewModels/Admin/CreateUserVm.cs`

```csharp
using System.ComponentModel.DataAnnotations;
using Pehlione.Data;

namespace Pehlione.Models.ViewModels.Admin;

public sealed class CreateUserVm
{
    [Required]
    [EmailAddress]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Parola")]
    public string Password { get; set; } = "";

    [Required]
    [Display(Name = "Rol")]
    public string Role { get; set; } = IdentitySeed.RoleCustomer;
}
```

`./Areas/Admin/Views/Users/Index.cshtml`

```cshtml
@model IReadOnlyList<Pehlione.Models.ViewModels.Admin.UserListItemVm>
@{
    ViewData["Title"] = "Kullanıcılar";
}

<div class="container" style="max-width: 980px;">
    <div class="d-flex align-items-center justify-content-between mb-3">
        <h1 class="h3 m-0">Kullanıcılar</h1>
        <a class="btn btn-primary" asp-area="Admin" asp-controller="Users" asp-action="Create">Yeni kullanıcı</a>
    </div>

    <p class="text-muted">
        Bu liste, Identity kullanıcılarını ve rollerini gösterir.
    </p>

    <table class="table table-striped align-middle">
        <thead>
            <tr>
                <th>E-posta</th>
                <th>Kullanıcı Adı</th>
                <th>Roller</th>
            </tr>
        </thead>
        <tbody>
        @foreach (var u in Model)
        {
            <tr>
                <td>@u.Email</td>
                <td>@u.UserName</td>
                <td>@(u.Roles.Length == 0 ? "-" : string.Join(", ", u.Roles))</td>
            </tr>
        }
        </tbody>
    </table>
</div>
```

`./Areas/Admin/Views/Users/Create.cshtml`

```cshtml
@model Pehlione.Models.ViewModels.Admin.CreateUserVm
@{
    ViewData["Title"] = "Yeni Kullanıcı";
}

<div class="container" style="max-width: 620px;">
    <h1 class="h3 mb-3">Yeni kullanıcı</h1>

    <form asp-area="Admin" asp-controller="Users" asp-action="Create" method="post">
        @Html.AntiForgeryToken()

        <div asp-validation-summary="ModelOnly" class="text-danger mb-3"></div>

        <div class="mb-3">
            <label asp-for="Email" class="form-label"></label>
            <input asp-for="Email" class="form-control" autocomplete="email" />
            <span asp-validation-for="Email" class="text-danger"></span>
        </div>

        <div class="mb-3">
            <label asp-for="Password" class="form-label"></label>
            <input asp-for="Password" class="form-control" autocomplete="new-password" />
            <span asp-validation-for="Password" class="text-danger"></span>
            <div class="form-text">Parola politikası Identity ayarlarına göre doğrulanır.</div>
        </div>

        <div class="mb-3">
            <label asp-for="Role" class="form-label"></label>
            <select asp-for="Role" class="form-select">
                <option value="Customer">Customer</option>
                <option value="Staff">Staff</option>
                <option value="Admin">Admin</option>
            </select>
            <span asp-validation-for="Role" class="text-danger"></span>
        </div>

        <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary">Oluştur</button>
            <a class="btn btn-outline-secondary" asp-area="Admin" asp-controller="Users" asp-action="Index">İptal</a>
        </div>
    </form>
</div>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

D) **Kısa Açıklama (en fazla 5 madde, öğretici)**

* `/Admin/Users/Create` sadece **Admin** rolüne açık.
* Rol seçimini `AllowedRoles` ile whitelist yaptık (kötü niyetli “başka rol adı” post edilemesin).
* `UserName = Email` yaparak login tarafını basit tuttuk.
* Hata mesajlarını Identity’nin `Errors` listesinden ModelState’e aktardık.
* Sonraki adım: oluşturulan kullanıcıya **“ilk girişte şifre değiştir”** zorunluluğu koyacağız (MustChangePassword akışı).

E) **Git Commit**

* Commit mesajı: `Add Admin create-user page with role assignment`
* Komut:

```bash
git add -A && git commit -m "Add Admin create-user page with role assignment"
```

Bunu uygulayıp `/Admin/Users/Create` ile bir **Staff** ve bir **Customer** kullanıcı oluşturabildiysen **“bitti”** yaz. Sonraki adımda “ilk girişte şifre değişimi zorunlu” akışına geçelim.
