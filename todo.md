A) **Hedef (Türkçe)**
Admin tarafında “kategori silinemiyor çünkü ürün bağlı” durumunu çözmek için **ürünleri başka bir kategoriye taşı (Move)** ekranını ekliyoruz. Böylece örn. `Man` kategorisindeki ürünleri `Men` kategorisine taşıyıp sonra `Man` kategorisini silebileceksin.

B) **CLI Komutu (maksimum 1 komut)**

```bash
dotnet build
```

C) **Dosya Değişiklikleri (sadece değişen/eklenen dosyalar, TAM içerik)**

`./Models/ViewModels/Admin/CategoryMoveProductsVm.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace Pehlione.Models.ViewModels.Admin;

public sealed class CategoryMoveProductsVm
{
    [Required]
    public int SourceCategoryId { get; set; }

    public string SourceName { get; set; } = "";

    public int ProductCount { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Hedef kategori seçmelisin.")]
    [Display(Name = "Hedef kategori")]
    public int TargetCategoryId { get; set; }

    public IReadOnlyList<CategoryOptionVm> TargetOptions { get; set; } = Array.Empty<CategoryOptionVm>();
}

public sealed class CategoryOptionVm
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
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
            model.Name = entity.Name;
            model.Slug = entity.Slug;
            model.HasProducts = true;

            ModelState.AddModelError(string.Empty, "Silme engellendi (ilişkili kayıt olabilir).");
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> MoveProducts(int id, CancellationToken ct)
    {
        var source = await _db.Categories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (source is null)
            return NotFound();

        var count = await _db.Products.AsNoTracking().CountAsync(p => p.CategoryId == id, ct);

        var targets = await _db.Categories
            .AsNoTracking()
            .Where(c => c.Id != id && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryOptionVm { Id = c.Id, Name = c.Name })
            .ToListAsync(ct);

        return View(new CategoryMoveProductsVm
        {
            SourceCategoryId = source.Id,
            SourceName = source.Name,
            ProductCount = count,
            TargetOptions = targets
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveProducts(CategoryMoveProductsVm model, CancellationToken ct)
    {
        // View tekrar gösterilecekse dropdown lazım
        model.TargetOptions = await _db.Categories
            .AsNoTracking()
            .Where(c => c.Id != model.SourceCategoryId && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryOptionVm { Id = c.Id, Name = c.Name })
            .ToListAsync(ct);

        var sourceExists = await _db.Categories.AsNoTracking().AnyAsync(c => c.Id == model.SourceCategoryId, ct);
        if (!sourceExists)
            return NotFound();

        var targetExists = await _db.Categories.AsNoTracking().AnyAsync(c => c.Id == model.TargetCategoryId && c.IsActive, ct);
        if (!targetExists)
            ModelState.AddModelError(nameof(model.TargetCategoryId), "Geçersiz hedef kategori.");

        if (!ModelState.IsValid)
            return View(model);

        var products = await _db.Products
            .Where(p => p.CategoryId == model.SourceCategoryId)
            .ToListAsync(ct);

        foreach (var p in products)
            p.CategoryId = model.TargetCategoryId;

        await _db.SaveChangesAsync(ct);

        return RedirectToAction(nameof(Delete), new { id = model.SourceCategoryId });
    }
}
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

                <a class="btn btn-outline-primary"
                   asp-area="Admin"
                   asp-controller="Categories"
                   asp-action="MoveProducts"
                   asp-route-id="@Model.Id">
                    Ürünleri başka kategoriye taşı
                </a>

                <a class="btn btn-outline-secondary ms-2"
                   asp-area="Admin"
                   asp-controller="Products"
                   asp-action="Index">
                    Ürünlere git
                </a>
            }
            else
            {
                <div class="alert alert-danger mb-3">
                    Bu işlem geri alınamaz. Silmek istediğinden emin misin?
                </div>

                <form asp-area="Admin" asp-controller="Categories" asp-action="Delete" method="post">
                    @Html.AntiForgeryToken()
                    <input type="hidden" asp-for="Id" />

                    <div class="d-flex gap-2">
                        <button type="submit" class="btn btn-danger">Sil</button>
                        <a class="btn btn-outline-secondary" asp-area="Admin" asp-controller="Categories" asp-action="Index">İptal</a>
                    </div>
                </form>
            }
        </div>
    </div>
</div>
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

`./Areas/Admin/Views/Categories/MoveProducts.cshtml`

```cshtml
@model Pehlione.Models.ViewModels.Admin.CategoryMoveProductsVm
@{
    ViewData["Title"] = "Ürünleri Taşı";
}

<div class="container" style="max-width: 760px;">
    <h1 class="h3 mb-3">Ürünleri başka kategoriye taşı</h1>

    <div class="alert alert-info">
        Kaynak kategori: <strong>@Model.SourceName</strong> —
        Ürün sayısı: <strong>@Model.ProductCount</strong>
    </div>

    <form asp-area="Admin" asp-controller="Categories" asp-action="MoveProducts" method="post">
        @Html.AntiForgeryToken()
        <input type="hidden" asp-for="SourceCategoryId" />
        <input type="hidden" asp-for="SourceName" />
        <input type="hidden" asp-for="ProductCount" />

        <div asp-validation-summary="ModelOnly" class="text-danger mb-3"></div>

        <div class="mb-3">
            <label asp-for="TargetCategoryId" class="form-label"></label>
            <select asp-for="TargetCategoryId" class="form-select">
                <option value="0">-- hedef seç --</option>
                @foreach (var c in Model.TargetOptions)
                {
                    <option value="@c.Id" selected="@(Model.TargetCategoryId == c.Id)">@c.Name</option>
                }
            </select>
            <span asp-validation-for="TargetCategoryId" class="text-danger"></span>
            <div class="form-text">Sadece aktif kategoriler hedef olarak listelenir.</div>
        </div>

        <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary" @(Model.ProductCount == 0 ? "disabled" : "")>
                Taşı
            </button>
            <a class="btn btn-outline-secondary"
               asp-area="Admin"
               asp-controller="Categories"
               asp-action="Delete"
               asp-route-id="@Model.SourceCategoryId">Geri</a>
        </div>
    </form>
</div>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

D) **Kısa Açıklama (en fazla 5 madde, öğretici)**

* “Silinemiyor” durumunda artık **tek tıkla taşıma** sayfasına gidebilirsin: `/Admin/Categories/MoveProducts/{id}`
* Taşıma işlemi: kaynak kategorideki tüm ürünlerin `CategoryId` alanı hedef kategoriye güncellenir.
* Hedef kategori sadece **aktif** kategorilerden seçilir (yanlış hedefe taşıma azaltılır).
* Taşıma sonrası otomatik olarak tekrar **Delete** ekranına dönersin; ürün kalmadıysa artık silinebilir.
* Sonraki adım: istersen “**Kategori pasif yapınca ürünler otomatik pasif olsun mu?**” gibi business kuralı ekleriz.

E) **Git Commit**

* Commit mesajı: `Add move-products flow to unblock category deletion`
* Komut:

```bash
git add -A && git commit -m "Add move-products flow to unblock category deletion"
```

Bu adımı uygulayıp: `Man` kategorisi → “Ürünleri başka kategoriye taşı” → taşıma sonrası `Man` kategorisini silebildiysen **“bitti”** yaz.
