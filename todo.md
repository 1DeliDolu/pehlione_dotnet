A) **Hedef (Türkçe)**
Admin tarafında **Kategori Silme (Delete)** akışını ekleyeceğiz. Kural: Kategoriye bağlı **ürün varsa silme yok** (uyarı göster).

B) **CLI Komutu (maksimum 1 komut)**

```bash
dotnet build
```

C) **Dosya Değişiklikleri (sadece değişen/eklenen dosyalar, TAM içerik)**

`./Models/ViewModels/Admin/CategoryDeleteVm.cs`

```csharp
namespace Pehlione.Models.ViewModels.Admin;

public sealed class CategoryDeleteVm
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public bool HasProducts { get; set; }
}
```

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

    [HttpGet]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entity = await _db.Categories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound();

        var hasProducts = await _db.Products.AsNoTracking().AnyAsync(p => p.CategoryId == id, ct);

        return View(new CategoryDeleteVm
        {
            Id = entity.Id,
            Name = entity.Name,
            Slug = entity.Slug,
            HasProducts = hasProducts
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(CategoryDeleteVm model, CancellationToken ct)
    {
        var entity = await _db.Categories.FirstOrDefaultAsync(x => x.Id == model.Id, ct);
        if (entity is null)
            return NotFound();

        var hasProducts = await _db.Products.AsNoTracking().AnyAsync(p => p.CategoryId == model.Id, ct);
        if (hasProducts)
        {
            model.Name = entity.Name;
            model.Slug = entity.Slug;
            model.HasProducts = true;

            ModelState.AddModelError(string.Empty, "Bu kategoriye bağlı ürünler var. Önce ürünleri taşıyın veya silin.");
            return View(model);
        }

        _db.Categories.Remove(entity);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // DB tarafında FK Restrict devreye girerse kullanıcı dostu hata
            model.Name = entity.Name;
            model.Slug = entity.Slug;
            model.HasProducts = true;

            ModelState.AddModelError(string.Empty, "Silme engellendi (ilişkili kayıt olabilir).");
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }
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
                    <div class="d-inline-flex gap-2">
                        <a class="btn btn-sm btn-outline-primary"
                           asp-area="Admin"
                           asp-controller="Categories"
                           asp-action="Edit"
                           asp-route-id="@c.Id">Düzenle</a>

                        <a class="btn btn-sm btn-outline-danger"
                           asp-area="Admin"
                           asp-controller="Categories"
                           asp-action="Delete"
                           asp-route-id="@c.Id">Sil</a>
                    </div>
                </td>
            </tr>
        }
        </tbody>
    </table>
</div>
```

`./Areas/Admin/Views/Categories/Delete.cshtml`

```cshtml
@model Pehlione.Models.ViewModels.Admin.CategoryDeleteVm
@{
    ViewData["Title"] = "Kategori Sil";
}

<div class="container" style="max-width: 720px;">
    <h1 class="h3 mb-3">Kategori sil</h1>

    <div asp-validation-summary="ModelOnly" class="text-danger mb-3"></div>

    <div class="card">
        <div class="card-body">
            <p class="mb-1"><strong>Ad:</strong> @Model.Name</p>
            <p class="mb-3"><strong>Slug:</strong> <code>@Model.Slug</code></p>

            @if (Model.HasProducts)
            {
                <div class="alert alert-warning mb-3">
                    Bu kategoriye bağlı ürünler var. Silme işlemi engellendi.
                </div>
            }
            else
            {
                <div class="alert alert-danger mb-3">
                    Bu işlem geri alınamaz. Silmek istediğinden emin misin?
                </div>
            }

            <form asp-area="Admin" asp-controller="Categories" asp-action="Delete" method="post">
                @Html.AntiForgeryToken()
                <input type="hidden" asp-for="Id" />

                <div class="d-flex gap-2">
                    <button type="submit" class="btn btn-danger" @(Model.HasProducts ? "disabled" : "")>Sil</button>
                    <a class="btn btn-outline-secondary" asp-area="Admin" asp-controller="Categories" asp-action="Index">İptal</a>
                </div>
            </form>
        </div>
    </div>
</div>
```

D) **Kısa Açıklama (en fazla 5 madde, öğretici)**

* Silme için **GET = onay ekranı**, **POST = gerçek silme** kullandık (güvenli pattern).
* `Products.Any(CategoryId==id)` varsa silmeyi uygulama seviyesinde engelliyoruz.
* DB tarafında da FK `Restrict` devredeyse `DbUpdateException` ile yakalayıp kullanıcı dostu mesaj dönüyoruz.
* URL: `/Admin/Categories/Delete/{id}`
* Sonraki adım: **Ürün CRUD** (önce liste + create) — yine küçük adımlarla.

E) **Git Commit**

* Commit mesajı: `Add Admin category delete with product guard`
* Komut:

```bash
git add -A && git commit -m "Add Admin category delete with product guard"
```

Bir kategoriyi silebildiysen (ürün yokken) ve ürün bağlıysa silmenin engellendiğini gördüysen **“bitti”** yaz; sonraki adımda **Product: List + Create** ile devam edelim.
