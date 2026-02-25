using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.ViewModels.Customer;

namespace Pehlione.Areas.Customer.Controllers;

[Area("Customer")]
[Authorize(Roles = IdentitySeed.RoleCustomer)]
public sealed class CatalogController : Controller
{
    private readonly PehlioneDbContext _db;

    public CatalogController(PehlioneDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var categories = await _db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CatalogCategoryListItemVm
            {
                Name = c.Name,
                Slug = c.Slug
            })
            .ToListAsync(ct);

        return View(categories);
    }

    [HttpGet]
    public async Task<IActionResult> Category(string slug, CancellationToken ct)
    {
        slug = (slug ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        var category = await _db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive && c.Slug == slug)
            .Select(c => new CatalogCategoryVm
            {
                Name = c.Name,
                Slug = c.Slug
            })
            .FirstOrDefaultAsync(ct);

        if (category is null)
        {
            return NotFound();
        }

        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.Category != null && p.Category.Slug == slug && p.Category.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new CatalogProductListItemVm
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                Price = p.Price
            })
            .ToListAsync(ct);

        var vm = new CatalogCategoryDetailsVm
        {
            Category = category,
            Products = products
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var vm = await _db.Products
            .AsNoTracking()
            .Where(p => p.Id == id && p.IsActive && p.Category != null && p.Category.IsActive)
            .Select(p => new CatalogProductDetailsVm
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                Price = p.Price,
                CategoryName = p.Category!.Name,
                CategorySlug = p.Category!.Slug
            })
            .FirstOrDefaultAsync(ct);

        if (vm is null)
        {
            return NotFound();
        }

        return View(vm);
    }
}
