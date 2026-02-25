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
}
