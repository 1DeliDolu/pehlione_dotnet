A) **Hedef (Türkçe)**
Admin tarafında **Kategori Güncelleme (Edit/Update)** ekranını ekleyeceğiz: `/Admin/Categories/Edit/{id}` ile kategori adı/slug/durum düzenlenecek ve slug için çakışma kontrolü yapılacak.

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

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var entity = await _db.Categories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound();

        return View(new CategoryEditVm
        {
            Id = entity.Id,
            Name = entity.Name,
            Slug = entity.Slug,
            IsActive = entity.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CategoryEditVm model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(model);

        var entity = await _db.Categories.FirstOrDefaultAsync(x => x.Id == model.Id, ct);
        if (entity is null)
            return NotFound();

        var slug = (model.Slug ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(slug))
        {
            ModelState.AddModelError(nameof(model.Slug), "Slug zorunludur.");
            return View(model);
        }

        var slugExists = await _db.Categories.AnyAsync(x => x.Slug == slug && x.Id != model.Id, ct);
        if (slugExists)
        {
            ModelState.AddModelError(nameof(model.Slug), "Bu slug zaten kullanılıyor.");
            return View(model);
        }

        entity.Name = model.Name.Trim();
        entity.Slug = slug;
        entity.IsActive = model.IsActive;

        await _db.SaveChangesAsync(ct);

        return RedirectToAction(nameof(Index));
    }
}
```

`./Models/ViewModels/Admin/CategoryEditVm.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace Pehlione.Models.ViewModels.Admin;

public sealed class CategoryEditVm
{
    [Required]
    public int Id { get; set; }

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
            <th class="text-end">İşlem</th>
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
                <td class="text-end">
                    <a class="btn btn-sm btn-outline-primary"
                       asp-area="Admin"
                       asp-controller="Categories"
                       asp-action="Edit"
                       asp-route-id="@c.Id">Düzenle</a>
                </td>
            </tr>
        }
        </tbody>
    </table>
</div>
```

`./Areas/Admin/Views/Categories/Edit.cshtml`

```cshtml
@model Pehlione.Models.ViewModels.Admin.CategoryEditVm
@{
    ViewData["Title"] = "Kategori Düzenle";
}

<div class="container" style="max-width: 640px;">
    <h1 class="h3 mb-3">Kategori düzenle</h1>

    <form asp-area="Admin" asp-controller="Categories" asp-action="Edit" method="post">
        @Html.AntiForgeryToken()
        <input type="hidden" asp-for="Id" />

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

* Index’e “Düzenle” linki ekledik; Edit GET formu doldurur, Edit POST günceller.
* Slug çakışma kontrolü `x.Id != model.Id` ile “kendisi hariç” çalışır.
* `AsNoTracking()` sadece GET’te; update tarafında entity tracked olsun diye normal query kullandık.
* URL: `/Admin/Categories/Edit/1`
* Sonraki küçük adım: **Delete** (silme) + “Ürün varsa silme” kuralını netleştirme.

E) **Git Commit**

* Commit mesajı: `Add Admin category edit (update) flow`
* Komut:

```bash
git add -A && git commit -m "Add Admin category edit (update) flow"
```

Kategori düzenleyip kaydedebildiysen **“bitti”** yaz; bir sonraki adımda sadece **Delete** ekleyelim.
