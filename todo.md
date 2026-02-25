A) **Hedef (Türkçe)**
Kategori silmenin “ürün bağlı” olduğu için engellenmesi doğru. Şimdi bunu yönetebilmek için **Admin → Ürün Listesi + Ürün Silme** ekranını ekliyoruz. Böylece kategoriye bağlı ürünleri silip (veya sonraki adımda taşıyıp) kategoriyi silebileceksin.

B) **CLI Komutu (maksimum 1 komut)**

```bash
dotnet build
```

C) **Dosya Değişiklikleri (sadece değişen/eklenen dosyalar, TAM içerik)**

`./Models/ViewModels/Admin/ProductListItemVm.cs`

```csharp
namespace Pehlione.Models.ViewModels.Admin;

public sealed class ProductListItemVm
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Sku { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
}
```

`./Models/ViewModels/Admin/ProductDeleteVm.cs`

```csharp
namespace Pehlione.Models.ViewModels.Admin;

public sealed class ProductDeleteVm
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Sku { get; set; } = "";
    public string CategoryName { get; set; } = "";
}
```

`./Areas/Admin/Controllers/ProductsController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
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
        <span class="text-muted">Şimdilik sadece liste + silme</span>
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
        Kategori silmek için önce bu listeden kategoriye bağlı ürünleri sil (veya sonraki adımda “taşı” ekleyeceğiz).
    </p>
</div>
```

`./Areas/Admin/Views/Products/Delete.cshtml`

```cshtml
@model Pehlione.Models.ViewModels.Admin.ProductDeleteVm
@{
    ViewData["Title"] = "Ürün Sil";
}

<div class="container" style="max-width: 760px;">
    <h1 class="h3 mb-3">Ürün sil</h1>

    <div class="card">
        <div class="card-body">
            <p class="mb-1"><strong>Ad:</strong> @Model.Name</p>
            <p class="mb-1"><strong>SKU:</strong> <code>@Model.Sku</code></p>
            <p class="mb-3"><strong>Kategori:</strong> @Model.CategoryName</p>

            <div class="alert alert-danger mb-3">
                Bu işlem geri alınamaz. Silmek istediğinden emin misin?
            </div>

            <form asp-area="Admin" asp-controller="Products" asp-action="Delete" method="post">
                @Html.AntiForgeryToken()
                <input type="hidden" asp-for="Id" />

                <div class="d-flex gap-2">
                    <button type="submit" class="btn btn-danger">Sil</button>
                    <a class="btn btn-outline-secondary" asp-area="Admin" asp-controller="Products" asp-action="Index">İptal</a>
                </div>
            </form>
        </div>
    </div>
</div>
```

D) **Kısa Açıklama (en fazla 5 madde, öğretici)**

* `/Admin/Products` ile kategoriye bağlı ürünleri görüp silebilirsin; böylece kategori silme engeli kalkar.
* Silme akışı güvenli pattern: **GET onay** + **POST sil**.
* Bu adımda “Create/Edit” yok; bir sonraki adımda **Ürün ekleme**yi ekleyelim.
* Eğer kategori silmek istiyorsan: önce ürünleri sil → sonra kategori sil.
* Sonraki adım: `/Admin/Products/Create` (ürün oluşturma + kategori seçimi + SKU unique kontrol).

E) **Git Commit**

* Commit mesajı: `Add Admin products list and delete pages`
* Komut:

```bash
git add -A && git commit -m "Add Admin products list and delete pages"
```

`/Admin/Products` ekranından, “Man” kategorisine bağlı ürünü/ürünleri silip sonra tekrar kategori silmeyi denediğinde başarılıysa **“bitti”** yaz; bir sonraki adımda **Ürün Create** ekleyelim.
