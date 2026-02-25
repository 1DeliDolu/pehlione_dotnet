A) **Hedef (Türkçe)**
Evet, kullanıcı tarafı (Admin/Staff/Customer, kullanıcı oluşturma, ilk girişte şifre değişimi, dev e-posta pickup) şu an için “MVP” düzeyinde tamam. Şimdi **Admin** tarafında **Kategori CRUD**’a başlıyoruz; bu adımda sadece **Listeleme + Yeni Kategori Oluşturma** ekranlarını ekliyoruz (`/Admin/Categories`).

B) **CLI Komutu (maksimum 1 komut)**

```bash
dotnet build
```

C) **Dosya Değişiklikleri (sadece değişen/eklenen dosyalar, TAM içerik)**

`./Areas/Admin/Controllers/CategoriesController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Catalog;
using Pehlione.Models.ViewModels.Admin;

namespace Pehlione.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = IdentitySeed.RoleAdmin)]
public sealed class CategoriesController : Controller
{
    private readonly PehlioneDbContext _db;

    public CategoriesController(PehlioneDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var items = await _db.Categories
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new CategoryListItemVm
            {
                Id = x.Id,
                Name = x.Name,
                Slug = x.Slug,
                IsActive = x.IsActive
            })
            .ToListAsync(ct);

        return View(items);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CategoryCreateVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryCreateVm model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(model);

        var slug = (model.Slug ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(slug))
        {
            ModelState.AddModelError(nameof(model.Slug), "Slug zorunludur.");
            return View(model);
        }

        var slugExists = await _db.Categories.AnyAsync(x => x.Slug == slug, ct);
        if (slugExists)
        {
            ModelState.AddModelError(nameof(model.Slug), "Bu slug zaten kullanılıyor.");
            return View(model);
        }

        var entity = new Category
        {
            Name = model.Name.Trim(),
            Slug = slug,
            IsActive = model.IsActive
        };

        _db.Categories.Add(entity);
        await _db.SaveChangesAsync(ct);

        return RedirectToAction(nameof(Index));
    }
}
```

`./Models/ViewModels/Admin/CategoryListItemVm.cs`

```csharp
namespace Pehlione.Models.ViewModels.Admin;

public sealed class CategoryListItemVm
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public bool IsActive { get; set; }
}
```

`./Models/ViewModels/Admin/CategoryCreateVm.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace Pehlione.Models.ViewModels.Admin;

public sealed class CategoryCreateVm
{
    [Required]
    [MaxLength(120)]
    [Display(Name = "Kategori adı")]
    public string Name { get; set; } = "";

    [Required]
    [MaxLength(160)]
    [Display(Name = "Slug (örn: erkek-ayakkabi)")]
    public string Slug { get; set; } = "";

    [Display(Name = "Aktif mi?")]
    public bool IsActive { get; set; } = true;
}
```

`./Areas/Admin/Views/Categories/Index.cshtml`

```cshtml
@model IReadOnlyList<Pehlione.Models.ViewModels.Admin.CategoryListItemVm>
@{
    ViewData["Title"] = "Kategoriler";
}

<div class="container" style="max-width: 980px;">
    <div class="d-flex align-items-center justify-content-between mb-3">
        <h1 class="h3 m-0">Kategoriler</h1>
        <a class="btn btn-primary" asp-area="Admin" asp-controller="Categories" asp-action="Create">Yeni kategori</a>
    </div>

    <table class="table table-striped align-middle">
        <thead>
        <tr>
            <th>Ad</th>
            <th>Slug</th>
            <th>Durum</th>
        </tr>
        </thead>
        <tbody>
        @foreach (var c in Model)
        {
            <tr>
                <td>@c.Name</td>
                <td><code>@c.Slug</code></td>
                <td>
                    @if (c.IsActive)
                    {
                        <span class="badge bg-success">Aktif</span>
                    }
                    else
                    {
                        <span class="badge bg-secondary">Pasif</span>
                    }
                </td>
            </tr>
        }
        </tbody>
    </table>

    <p class="text-muted mb-0">
        Not: Eğer burada “table doesn't exist” hatası görürsen, bir sonraki adımda migration ile tabloyu MySQL’e basacağız.
    </p>
</div>
```

`./Areas/Admin/Views/Categories/Create.cshtml`

```cshtml
@model Pehlione.Models.ViewModels.Admin.CategoryCreateVm
@{
    ViewData["Title"] = "Yeni Kategori";
}

<div class="container" style="max-width: 640px;">
    <h1 class="h3 mb-3">Yeni kategori</h1>

    <form asp-area="Admin" asp-controller="Categories" asp-action="Create" method="post">
        @Html.AntiForgeryToken()

        <div asp-validation-summary="ModelOnly" class="text-danger mb-3"></div>

        <div class="mb-3">
            <label asp-for="Name" class="form-label"></label>
            <input asp-for="Name" class="form-control" />
            <span asp-validation-for="Name" class="text-danger"></span>
        </div>

        <div class="mb-3">
            <label asp-for="Slug" class="form-label"></label>
            <input asp-for="Slug" class="form-control" />
            <span asp-validation-for="Slug" class="text-danger"></span>
            <div class="form-text">Küçük harf ve tire kullan: <code>erkek-ayakkabi</code></div>
        </div>

        <div class="form-check mb-3">
            <input asp-for="IsActive" class="form-check-input" />
            <label asp-for="IsActive" class="form-check-label"></label>
        </div>

        <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary">Kaydet</button>
            <a class="btn btn-outline-secondary" asp-area="Admin" asp-controller="Categories" asp-action="Index">İptal</a>
        </div>
    </form>
</div>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

D) **Kısa Açıklama (en fazla 5 madde, öğretici)**

* Kategori yönetimi sadece **Admin** rolüne açık (`[Authorize(Roles=Admin)]`).
* `Slug` için uygulama seviyesinde “unique” kontrol yaptık; DB’de de unique index var (migration ile aktifleşecek).
* İlk CRUD adımı olarak **Read + Create** verdik; Update/Delete’yi sonraki küçük adımlara böleceğiz.
* URL’ler: `/Admin/Categories` ve `/Admin/Categories/Create`
* Eğer tablo hatası alırsan, sonraki adım: **migration + database update**.

E) **Git Commit**

* Commit mesajı: `Add Admin category list and create pages`
* Komut:

```bash
git add -A && git commit -m "Add Admin category list and create pages"
```

`/Admin/Categories` ekranını açıp **1 kategori** oluşturabildiysen **“bitti”** yaz. Sonraki adımda **Edit (Update)** ekleyeceğim (tek adımda sadece edit).
