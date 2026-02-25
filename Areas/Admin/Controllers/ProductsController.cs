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
            CategoryOptions = await LoadCategoryOptionsAsync(includeCategoryId: null, ct)
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductCreateVm model, CancellationToken ct)
    {
        model.CategoryOptions = await LoadCategoryOptionsAsync(includeCategoryId: null, ct);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var sku = (model.Sku ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(sku))
        {
            ModelState.AddModelError(nameof(model.Sku), "SKU zorunludur.");
            return View(model);
        }

        var categoryExists = await _db.Categories.AsNoTracking().AnyAsync(c => c.Id == model.CategoryId, ct);
        if (!categoryExists)
        {
            ModelState.AddModelError(nameof(model.CategoryId), "Gecersiz kategori secimi.");
            return View(model);
        }

        var skuExists = await _db.Products.AsNoTracking().AnyAsync(p => p.Sku == sku, ct);
        if (skuExists)
        {
            ModelState.AddModelError(nameof(model.Sku), "Bu SKU zaten kullaniliyor.");
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
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var entity = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (entity is null)
        {
            return NotFound();
        }

        return View(new ProductEditVm
        {
            Id = entity.Id,
            CategoryId = entity.CategoryId,
            Name = entity.Name,
            Sku = entity.Sku,
            Price = entity.Price,
            IsActive = entity.IsActive,
            CategoryOptions = await LoadCategoryOptionsAsync(includeCategoryId: entity.CategoryId, ct)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProductEditVm model, CancellationToken ct)
    {
        model.CategoryOptions = await LoadCategoryOptionsAsync(includeCategoryId: model.CategoryId, ct);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var entity = await _db.Products.FirstOrDefaultAsync(p => p.Id == model.Id, ct);
        if (entity is null)
        {
            return NotFound();
        }

        var sku = (model.Sku ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(sku))
        {
            ModelState.AddModelError(nameof(model.Sku), "SKU zorunludur.");
            return View(model);
        }

        var categoryExists = await _db.Categories.AsNoTracking().AnyAsync(c => c.Id == model.CategoryId, ct);
        if (!categoryExists)
        {
            ModelState.AddModelError(nameof(model.CategoryId), "Gecersiz kategori secimi.");
            return View(model);
        }

        var skuExists = await _db.Products.AsNoTracking().AnyAsync(p => p.Sku == sku && p.Id != model.Id, ct);
        if (skuExists)
        {
            ModelState.AddModelError(nameof(model.Sku), "Bu SKU zaten kullaniliyor.");
            return View(model);
        }

        entity.CategoryId = model.CategoryId;
        entity.Name = model.Name.Trim();
        entity.Sku = sku;
        entity.Price = model.Price;
        entity.IsActive = model.IsActive;

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
        {
            return NotFound();
        }

        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(ProductDeleteVm model, CancellationToken ct)
    {
        var entity = await _db.Products.FirstOrDefaultAsync(p => p.Id == model.Id, ct);
        if (entity is null)
        {
            return NotFound();
        }

        _db.Products.Remove(entity);
        await _db.SaveChangesAsync(ct);

        return RedirectToAction(nameof(Index));
    }

    private async Task<IReadOnlyList<ProductCategoryOptionVm>> LoadCategoryOptionsAsync(int? includeCategoryId, CancellationToken ct)
    {
        return await _db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive || (includeCategoryId.HasValue && c.Id == includeCategoryId.Value))
            .OrderBy(c => c.Name)
            .Select(c => new ProductCategoryOptionVm { Id = c.Id, Name = c.Name })
            .ToListAsync(ct);
    }
}
