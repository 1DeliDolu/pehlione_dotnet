using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.ViewModels.Customer;

namespace Pehlione.Areas.Customer.Controllers;

[Area("Customer")]
[AllowAnonymous]
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
            .Where(c => c.IsActive && c.ParentId == null)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CatalogCategoryListItemVm
            {
                Name = c.Name,
                Slug = c.Slug
            })
            .ToListAsync(ct);

        return View(categories);
    }

    [HttpGet]
    public async Task<IActionResult> Category(string slug, string? q, string? sort, CancellationToken ct)
    {
        slug = (slug ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        var allActiveCategories = await _db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new ActiveCategoryRow
            {
                Id = c.Id,
                ParentId = c.ParentId,
                Name = c.Name,
                Slug = c.Slug,
                SortOrder = c.SortOrder
            })
            .ToListAsync(ct);

        var categoryRow = allActiveCategories.FirstOrDefault(c => c.Slug == slug);
        if (categoryRow is null)
        {
            return NotFound();
        }

        var childCategories = allActiveCategories
            .Where(c => c.ParentId == categoryRow.Id)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CatalogCategoryListItemVm
            {
                Name = c.Name,
                Slug = c.Slug
            })
            .ToList();

        var descendantCategoryIds = GetDescendantCategoryIds(categoryRow.Id, allActiveCategories);

        var productQuery = _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive && descendantCategoryIds.Contains(p.CategoryId) && p.Category != null && p.Category.IsActive);

        var normalizedQuery = (q ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            productQuery = productQuery.Where(p => p.Name.Contains(normalizedQuery) || p.Sku.Contains(normalizedQuery));
        }

        var normalizedSort = (sort ?? "").Trim().ToLowerInvariant();
        productQuery = normalizedSort switch
        {
            "price_asc" => productQuery.OrderBy(p => p.Price).ThenBy(p => p.Name),
            "price_desc" => productQuery.OrderByDescending(p => p.Price).ThenBy(p => p.Name),
            _ => productQuery.OrderBy(p => p.Name)
        };

        var products = await productQuery
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
            Category = new CatalogCategoryVm
            {
                Name = categoryRow.Name,
                Slug = categoryRow.Slug
            },
            ChildCategories = childCategories,
            Products = products
        };

        ViewBag.Query = normalizedQuery;
        ViewBag.Sort = normalizedSort;
        ViewBag.ProductCount = products.Count;

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

    private static HashSet<int> GetDescendantCategoryIds(
        int rootCategoryId,
        IReadOnlyList<ActiveCategoryRow> allActiveCategories)
    {
        var childrenMap = allActiveCategories
            .Where(c => c.ParentId != null)
            .GroupBy(c => c.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        var result = new HashSet<int> { rootCategoryId };
        var queue = new Queue<int>();
        queue.Enqueue(rootCategoryId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!childrenMap.TryGetValue(current, out var children))
                continue;

            foreach (var childId in children)
            {
                if (!result.Add(childId))
                    continue;

                queue.Enqueue(childId);
            }
        }

        return result;
    }

    private sealed class ActiveCategoryRow
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public string Name { get; set; } = "";
        public string Slug { get; set; } = "";
        public int SortOrder { get; set; }
    }
}
