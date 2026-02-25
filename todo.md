A) **Hedef (Türkçe)**
Haklısın — bundan sonra adımları **küçük parçalara** böleceğim. Bu adımda sadece **Admin** tarafına “kullanıcıları ve rollerini gör” ekranı ekliyoruz: `/Admin/Users` sayfası, `Admin/Staff/Customer` demo kullanıcılarını listeler.

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
}
```

`./Models/ViewModels/Admin/UserListItemVm.cs`

```csharp
namespace Pehlione.Models.ViewModels.Admin;

public sealed class UserListItemVm
{
    public string Email { get; set; } = "";
    public string UserName { get; set; } = "";
    public string[] Roles { get; set; } = Array.Empty<string>();
}
```

`./Areas/Admin/Views/Users/Index.cshtml`

```cshtml
@model IReadOnlyList<Pehlione.Models.ViewModels.Admin.UserListItemVm>
@{
    ViewData["Title"] = "Kullanıcılar";
}

<div class="container" style="max-width: 980px;">
    <h1 class="h3 mb-3">Kullanıcılar</h1>

    <p class="text-muted">
        Bu liste, Identity kullanıcılarını ve rollerini gösterir (dev seed dahil).
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

D) **Kısa Açıklama (en fazla 5 madde, öğretici)**

* Bu ekran sadece **Admin rolü** ile erişilebilir (`[Authorize(Roles=...)]`).
* `UserManager.Users` üzerinden kullanıcıları çekiyoruz; `GetRolesAsync` ile rollerini alıyoruz.
* Şimdilik N+1 (her kullanıcı için rol sorgusu) var; demo için sorun değil, ileride optimize ederiz.
* URL: `/Admin/Users`
* Sonraki adımda istersen **Admin panelinden yeni Staff/Customer oluşturma** (Create form) ekleyebiliriz.

E) **Git Commit**

* Commit mesajı: `Add Admin users list page (users + roles)`
* Komut:

```bash
git add -A && git commit -m "Add Admin users list page (users + roles)"
```

Bunu uygulayıp `/Admin/Users` sayfasında listeyi gördüysen **“bitti”** yaz.
