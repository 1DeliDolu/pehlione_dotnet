A) **Hedef (Türkçe)**
Admin tarafında **Ürün Oluşturma (Create)** ekranını ekleyeceğiz: `/Admin/Products/Create`. Ürün eklerken **kategori seçimi**, **SKU benzersizliği** ve **fiyat doğrulaması** yapılacak.

B) **CLI Komutu (maksimum 1 komut)**

```bash
dotnet build
```

C) **Dosya Değişiklikleri (sadece değişen/eklenen dosyalar, TAM içerik)**

`./Areas/Admin/Controllers/ProductsController.cs`

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
public sealed class ProductsController : Controller
{
    private readonly PehlioneDbContext _db;

    public ProductsController(PehlioneDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var items = await _db.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new ProductListItemVm
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                CategoryName = p.Category != null ? p.Category.Name : "",
                Price = p.Price,
                IsActive = p.IsActive
            })
            .ToListAsync(ct);

        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var vm = new ProductCreateVm
        {
            CategoryOptions = await LoadCategoryOptionsAsync(ct)
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductCreateVm model, CancellationToken ct)
    {
        // Dropdown verisini her durumda dolduralım (validation fail olursa view tekrar lazım)
        model.CategoryOptions = await LoadCategoryOptionsAsync(ct);

        if (!ModelState.IsValid)
            return View(model);

        var sku = (model.Sku ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(sku))
        {
            ModelState.AddModelError(nameof(model.Sku), "SKU zorunludur.");
            return View(model);
        }

        var categoryExists = await _db.Categories.AsNoTracking().AnyAsync(c => c.Id == model.CategoryId, ct);
        if (!categoryExists)
        {
            ModelState.AddModelError(nameof(model.CategoryId), "Geçersiz kategori seçimi.");
            return View(model);
        }

        var skuExists = await _db.Products.AsNoTracking().AnyAsync(p => p.Sku == sku, ct);
        if (skuExists)
        {
            ModelState.AddModelError(nameof(model.Sku), "Bu SKU zaten kullanılıyor.");
            return View(model);
        }

        var entity = new Product
        {
            CategoryId = model.CategoryId,
            Name = model.Name.Trim(),
            Sku = sku,
            Price = model.Price,
            IsActive = model.IsActive
        };

        _db.Products.Add(entity);
        await _db.SaveChangesAsync(ct);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var item = await _db.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProductDeleteVm
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                CategoryName = p.Category != null ? p.Category.Name : ""
            })
            .FirstOrDefaultAsync(ct);

        if (item is null)
            return NotFound();

        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(ProductDeleteVm model, CancellationToken ct)
    {
        var entity = await _db.Products.FirstOrDefaultAsync(p => p.Id == model.Id, ct);
        if (entity is null)
            return NotFound();

        _db.Products.Remove(entity);
        await _db.SaveChangesAsync(ct);

        return RedirectToAction(nameof(Index));
    }

    private async Task<IReadOnlyList<ProductCategoryOptionVm>> LoadCategoryOptionsAsync(CancellationToken ct)
    {
        return await _db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new ProductCategoryOptionVm { Id = c.Id, Name = c.Name })
            .ToListAsync(ct);
    }
}
```

`./Models/ViewModels/Admin/ProductCreateVm.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace Pehlione.Models.ViewModels.Admin;

public sealed class ProductCreateVm
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Kategori seçmelisin.")]
    [Display(Name = "Kategori")]
    public int CategoryId { get; set; }

    [Required]
    [MaxLength(160)]
    [Display(Name = "Ürün adı")]
    public string Name { get; set; } = "";

    [Required]
    [MaxLength(64)]
    [Display(Name = "SKU")]
    public string Sku { get; set; } = "";

    [Required]
    [Range(typeof(decimal), "0.01", "9999999", ErrorMessage = "Fiyat 0.01 ve üzeri olmalı.")]
    [Display(Name = "Fiyat")]
    public decimal Price { get; set; }

    [Display(Name = "Aktif mi?")]
    public bool IsActive { get; set; } = true;

    // Dropdown için
    public IReadOnlyList<ProductCategoryOptionVm> CategoryOptions { get; set; } = Array.Empty<ProductCategoryOptionVm>();
}

public sealed class ProductCategoryOptionVm
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
```

`./Areas/Admin/Views/Products/Index.cshtml`

```cshtml
@model IReadOnlyList<Pehlione.Models.ViewModels.Admin.ProductListItemVm>
@{
    ViewData["Title"] = "Ürünler";
}

<div class="container" style="max-width: 1100px;">
    <div class="d-flex align-items-center justify-content-between mb-3">
        <h1 class="h3 m-0">Ürünler</h1>
        <a class="btn btn-primary" asp-area="Admin" asp-controller="Products" asp-action="Create">Yeni ürün</a>
    </div>

    <table class="table table-striped align-middle">
        <thead>
        <tr>
            <th>Ad</th>
            <th>SKU</th>
            <th>Kategori</th>
            <th class="text-end">Fiyat</th>
            <th>Durum</th>
            <th class="text-end">İşlem</th>
        </tr>
        </thead>
        <tbody>
        @foreach (var p in Model)
        {
            <tr>
                <td>@p.Name</td>
                <td><code>@p.Sku</code></td>
                <td>@p.CategoryName</td>
                <td class="text-end">@p.Price.ToString("0.00")</td>
                <td>
                    @if (p.IsActive)
                    {
                        <span class="badge bg-success">Aktif</span>
                    }
                    else
                    {
                        <span class="badge bg-secondary">Pasif</span>
                    }
                </td>
                <td class="text-end">
                    <a class="btn btn-sm btn-outline-danger"
                       asp-area="Admin"
                       asp-controller="Products"
                       asp-action="Delete"
                       asp-route-id="@p.Id">Sil</a>
                </td>
            </tr>
        }
        </tbody>
    </table>

    <p class="text-muted mb-0">
        Not: “Kategori sil” engeli için önce bağlı ürünleri buradan silebilir (veya sonraki adımda taşıyabilirsin).
    </p>
</div>
```

`./Areas/Admin/Views/Products/Create.cshtml`

```cshtml
@model Pehlione.Models.ViewModels.Admin.ProductCreateVm
@{
    ViewData["Title"] = "Yeni Ürün";
}

<div class="container" style="max-width: 720px;">
    <h1 class="h3 mb-3">Yeni ürün</h1>

    <form asp-area="Admin" asp-controller="Products" asp-action="Create" method="post">
        @Html.AntiForgeryToken()

        <div asp-validation-summary="ModelOnly" class="text-danger mb-3"></div>

        <div class="mb-3">
            <label asp-for="CategoryId" class="form-label"></label>
            <select asp-for="CategoryId" class="form-select">
                <option value="0">-- seç --</option>
                @foreach (var c in Model.CategoryOptions)
                {
                    <option value="@c.Id" selected="@(Model.CategoryId == c.Id)">
                        @c.Name
                    </option>
                }
            </select>
            <span asp-validation-for="CategoryId" class="text-danger"></span>
        </div>

        <div class="mb-3">
            <label asp-for="Name" class="form-label"></label>
            <input asp-for="Name" class="form-control" />
            <span asp-validation-for="Name" class="text-danger"></span>
        </div>

        <div class="mb-3">
            <label asp-for="Sku" class="form-label"></label>
            <input asp-for="Sku" class="form-control" />
            <span asp-validation-for="Sku" class="text-danger"></span>
            <div class="form-text">SKU otomatik olarak büyük harfe çevrilir.</div>
        </div>

        <div class="mb-3">
            <label asp-for="Price" class="form-label"></label>
            <input asp-for="Price" class="form-control" />
            <span asp-validation-for="Price" class="text-danger"></span>
        </div>

        <div class="form-check mb-3">
            <input asp-for="IsActive" class="form-check-input" />
            <label asp-for="IsActive" class="form-check-label"></label>
        </div>

        <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary">Kaydet</button>
            <a class="btn btn-outline-secondary" asp-area="Admin" asp-controller="Products" asp-action="Index">İptal</a>
        </div>
    </form>
</div>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

D) **Kısa Açıklama (en fazla 5 madde, öğretici)**

* Create formunda sadece **aktif kategoriler** listelenir.
* SKU hem uygulamada kontrol ediliyor hem de DB tarafında unique index var (çakışmayı iki kat güvenceye alır).
* Validation fail olunca dropdown boş kalmaması için `CategoryOptions` POST’ta da dolduruluyor.
* URL: `/Admin/Products/Create`
* Sonraki küçük adım: **Product Edit (Update)**.

E) **Git Commit**

* Commit mesajı: `Add Admin product create page with category selection and SKU check`
* Komut:

```bash
git add -A && git commit -m "Add Admin product create page with category selection and SKU check"
```

Bir ürün ekleyip `/Admin/Products` listesinde görüyorsan **“bitti”** yaz; sonraki adımda sadece **Product Edit (Update)** ekleyelim.
