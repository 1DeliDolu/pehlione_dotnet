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
        {
            return View(model);
        }

        var slug = (model.Slug ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(slug))
        {
            ModelState.AddModelError(nameof(model.Slug), "Slug zorunludur.");
            return View(model);
        }

        var slugExists = await _db.Categories.AnyAsync(x => x.Slug == slug, ct);
        if (slugExists)
        {
            ModelState.AddModelError(nameof(model.Slug), "Bu slug zaten kullaniliyor.");
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
        {
            return NotFound();
        }

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
        {
            return View(model);
        }

        var entity = await _db.Categories.FirstOrDefaultAsync(x => x.Id == model.Id, ct);
        if (entity is null)
        {
            return NotFound();
        }

        var slug = (model.Slug ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(slug))
        {
            ModelState.AddModelError(nameof(model.Slug), "Slug zorunludur.");
            return View(model);
        }

        var slugExists = await _db.Categories.AnyAsync(x => x.Slug == slug && x.Id != model.Id, ct);
        if (slugExists)
        {
            ModelState.AddModelError(nameof(model.Slug), "Bu slug zaten kullaniliyor.");
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
        {
            return NotFound();
        }

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
        {
            return NotFound();
        }

        var hasProducts = await _db.Products.AsNoTracking().AnyAsync(p => p.CategoryId == model.Id, ct);
        if (hasProducts)
        {
            model.Name = entity.Name;
            model.Slug = entity.Slug;
            model.HasProducts = true;

            ModelState.AddModelError(string.Empty, "Bu kategoriye bagli urunler var. Once urunleri tasiyin veya silin.");
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

            ModelState.AddModelError(string.Empty, "Silme engellendi (iliskili kayit olabilir).");
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }
}
