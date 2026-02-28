using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Communication;
using Pehlione.Models.ViewModels.Staff;
using Pehlione.Services;

namespace Pehlione.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize]
public sealed class InventoryController : Controller
{
    private readonly PehlioneDbContext _db;
    private readonly IInventoryService _inventoryService;
    private readonly INotificationService _notificationService;
    private readonly IDepartmentConstraintService _departmentConstraintService;

    public InventoryController(
        PehlioneDbContext db,
        IInventoryService inventoryService,
        INotificationService notificationService,
        IDepartmentConstraintService departmentConstraintService)
    {
        _db = db;
        _inventoryService = inventoryService;
        _notificationService = notificationService;
        _departmentConstraintService = departmentConstraintService;
    }

    [HttpGet]
    [Authorize(Roles = $"{IdentitySeed.RolePurchasing},{IdentitySeed.RoleWarehouse},{IdentitySeed.RoleIt},{IdentitySeed.RoleAdmin}")]
    public async Task<IActionResult> Receive(int? topCategoryId, int? subCategoryId, int? subSubCategoryId, int? productId, CancellationToken ct)
    {
        var allCategories = await LoadAllCategoriesAsync(ct);
        var allProducts = await LoadProductOptionsAsync(ct);
        var topCategoryOptions = BuildTopCategorySelectItems(allCategories);

        var selectedTopCategoryId = topCategoryId.GetValueOrDefault();
        if (selectedTopCategoryId <= 0 && topCategoryOptions.Count > 0 && int.TryParse(topCategoryOptions[0].Value, out var firstTopCategoryId))
            selectedTopCategoryId = firstTopCategoryId;

        var subCategoryOptions = BuildChildCategorySelectItems(allCategories, selectedTopCategoryId);
        var selectedSubCategoryId = subCategoryId;
        if ((!selectedSubCategoryId.HasValue || selectedSubCategoryId <= 0) && subCategoryOptions.Count > 0 && int.TryParse(subCategoryOptions[0].Value, out var firstSubCategoryId))
            selectedSubCategoryId = firstSubCategoryId;

        var subSubCategoryOptions = BuildChildCategorySelectItems(allCategories, selectedSubCategoryId);
        var selectedSubSubCategoryId = subSubCategoryId;
        if ((!selectedSubSubCategoryId.HasValue || selectedSubSubCategoryId <= 0) && subSubCategoryOptions.Count > 0 && int.TryParse(subSubCategoryOptions[0].Value, out var firstSubSubCategoryId))
            selectedSubSubCategoryId = firstSubSubCategoryId;

        var selectedCategoryForProducts = selectedSubSubCategoryId ?? selectedSubCategoryId ?? selectedTopCategoryId;
        var productOptions = BuildProductSelectItems(allProducts, allCategories, selectedCategoryForProducts);

        var selectedProductId = productId.GetValueOrDefault();
        if (selectedProductId <= 0)
        {
            selectedProductId = productOptions
                .Select(x => int.TryParse(x.Value, out var id) ? id : 0)
                .Where(x => x > 0)
                .FirstOrDefault();
        }

        var vm = new ReceiveStockVm
        {
            TopCategoryId = selectedTopCategoryId,
            SubCategoryId = selectedSubCategoryId,
            SubSubCategoryId = selectedSubSubCategoryId,
            ProductId = selectedProductId,
            TopCategoryOptions = topCategoryOptions,
            SubCategoryOptions = subCategoryOptions,
            SubSubCategoryOptions = subSubCategoryOptions,
            ProductOptions = productOptions,
            AllCategories = allCategories,
            AllProducts = allProducts
        };

        await PopulateDashboardAsync(vm, ct);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "CanIncreaseStock")]
    public async Task<IActionResult> Receive(ReceiveStockVm model, CancellationToken ct)
    {
        model.AllCategories = await LoadAllCategoriesAsync(ct);
        model.AllProducts = await LoadProductOptionsAsync(ct);
        model.TopCategoryOptions = BuildTopCategorySelectItems(model.AllCategories);
        model.SubCategoryOptions = BuildChildCategorySelectItems(model.AllCategories, model.TopCategoryId);
        model.SubSubCategoryOptions = BuildChildCategorySelectItems(model.AllCategories, model.SubCategoryId);
        var selectedCategoryForProducts = model.SubSubCategoryId ?? model.SubCategoryId ?? model.TopCategoryId;
        model.ProductOptions = BuildProductSelectItems(model.AllProducts, model.AllCategories, selectedCategoryForProducts);
        await PopulateDashboardAsync(model, ct);

        if (!ModelState.IsValid)
            return View(model);

        var topCategory = model.AllCategories.FirstOrDefault(x => x.CategoryId == model.TopCategoryId && x.ParentCategoryId is null);
        if (topCategory is null)
        {
            ModelState.AddModelError(nameof(model.TopCategoryId), "Gecerli bir ana kategori secin.");
            return View(model);
        }

        if (model.SubCategoryId.HasValue)
        {
            var subCategory = model.AllCategories.FirstOrDefault(x => x.CategoryId == model.SubCategoryId.Value);
            if (subCategory is null || subCategory.ParentCategoryId != model.TopCategoryId)
            {
                ModelState.AddModelError(nameof(model.SubCategoryId), "Alt grup secimi gecersiz.");
                return View(model);
            }
        }

        if (model.SubSubCategoryId.HasValue)
        {
            var subSubCategory = model.AllCategories.FirstOrDefault(x => x.CategoryId == model.SubSubCategoryId.Value);
            if (subSubCategory is null || subSubCategory.ParentCategoryId != model.SubCategoryId)
            {
                ModelState.AddModelError(nameof(model.SubSubCategoryId), "Alt grup 2 secimi gecersiz.");
                return View(model);
            }
        }

        var product = model.AllProducts.FirstOrDefault(x => x.ProductId == model.ProductId);
        var allowedCategoryIds = GetDescendantCategoryIds(model.AllCategories, selectedCategoryForProducts);
        if (product is null || !allowedCategoryIds.Contains(product.CategoryId))
        {
            ModelState.AddModelError(nameof(model.ProductId), "Secilen urun kategori/agac secimi ile uyusmuyor.");
            return View(model);
        }

        var access = await _departmentConstraintService.GetAccessAsync(User, ct);
        if (!access.CanIncreaseStock)
        {
            ModelState.AddModelError(string.Empty, "Departmaniniz stok girisi islemi icin kisitlidir.");
            return View(model);
        }

        if (access.MaxReceiveQuantity.HasValue && model.Quantity > access.MaxReceiveQuantity.Value)
        {
            ModelState.AddModelError(nameof(model.Quantity), $"Bu departman icin tek seferde en fazla {access.MaxReceiveQuantity.Value} adet girilebilir.");
            return View(model);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _inventoryService.ReceiveStockAsync(model.ProductId, model.Quantity, null, userId, ct);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Stok girisi basarisiz.");
            return View(model);
        }

        TempData["InventorySuccess"] = $"Stok guncellendi. Yeni stok: {result.CurrentQuantity}";
        return RedirectToAction(nameof(Receive), new { topCategoryId = model.TopCategoryId, subCategoryId = model.SubCategoryId, subSubCategoryId = model.SubSubCategoryId, productId = model.ProductId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "CanDecreaseStock")]
    public async Task<IActionResult> Decrease(ReceiveStockVm model, CancellationToken ct)
    {
        model.AllCategories = await LoadAllCategoriesAsync(ct);
        model.AllProducts = await LoadProductOptionsAsync(ct);
        model.TopCategoryOptions = BuildTopCategorySelectItems(model.AllCategories);
        model.SubCategoryOptions = BuildChildCategorySelectItems(model.AllCategories, model.TopCategoryId);
        model.SubSubCategoryOptions = BuildChildCategorySelectItems(model.AllCategories, model.SubCategoryId);
        var selectedCategoryForProducts = model.SubSubCategoryId ?? model.SubCategoryId ?? model.TopCategoryId;
        model.ProductOptions = BuildProductSelectItems(model.AllProducts, model.AllCategories, selectedCategoryForProducts);
        await PopulateDashboardAsync(model, ct);

        if (!ModelState.IsValid)
            return View("Receive", model);

        var topCategory = model.AllCategories.FirstOrDefault(x => x.CategoryId == model.TopCategoryId && x.ParentCategoryId is null);
        if (topCategory is null)
        {
            ModelState.AddModelError(nameof(model.TopCategoryId), "Gecerli bir ana kategori secin.");
            return View("Receive", model);
        }

        if (model.SubCategoryId.HasValue)
        {
            var subCategory = model.AllCategories.FirstOrDefault(x => x.CategoryId == model.SubCategoryId.Value);
            if (subCategory is null || subCategory.ParentCategoryId != model.TopCategoryId)
            {
                ModelState.AddModelError(nameof(model.SubCategoryId), "Alt grup secimi gecersiz.");
                return View("Receive", model);
            }
        }

        if (model.SubSubCategoryId.HasValue)
        {
            var subSubCategory = model.AllCategories.FirstOrDefault(x => x.CategoryId == model.SubSubCategoryId.Value);
            if (subSubCategory is null || subSubCategory.ParentCategoryId != model.SubCategoryId)
            {
                ModelState.AddModelError(nameof(model.SubSubCategoryId), "Alt grup 2 secimi gecersiz.");
                return View("Receive", model);
            }
        }

        var product = model.AllProducts.FirstOrDefault(x => x.ProductId == model.ProductId);
        var allowedCategoryIds = GetDescendantCategoryIds(model.AllCategories, selectedCategoryForProducts);
        if (product is null || !allowedCategoryIds.Contains(product.CategoryId))
        {
            ModelState.AddModelError(nameof(model.ProductId), "Secilen urun kategori/agac secimi ile uyusmuyor.");
            return View("Receive", model);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _inventoryService.ReduceStockAsync(model.ProductId, model.Quantity, null, userId, ct);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Stok dusme islemi basarisiz.");
            return View("Receive", model);
        }

        TempData["InventorySuccess"] = $"Stok dusuruldu. Yeni stok: {result.CurrentQuantity}";
        return RedirectToAction(nameof(Receive), new { topCategoryId = model.TopCategoryId, subCategoryId = model.SubCategoryId, subSubCategoryId = model.SubSubCategoryId, productId = model.ProductId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "CanDeleteStock")]
    public async Task<IActionResult> DeleteProduct(int productId, CancellationToken ct)
    {
        var access = await _departmentConstraintService.GetAccessAsync(User, ct);
        if (!access.CanDeleteStock)
        {
            TempData["InventoryError"] = "Departmaniniz urun silme islemi icin kisitlidir.";
            return RedirectToAction(nameof(Receive));
        }

        if (productId <= 0)
            return BadRequest();

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null)
        {
            TempData["InventoryError"] = "Urun bulunamadi.";
            return RedirectToAction(nameof(Receive));
        }

        _db.Products.Remove(product);

        try
        {
            await _db.SaveChangesAsync(ct);
            TempData["InventorySuccess"] = "Urun silindi.";
            await _notificationService.CreateAsync(
                department: NotificationDepartments.Sales,
                title: "Urun silindi",
                message: $"{product.Name} ({product.Sku}) urunu IT tarafindan silindi.",
                relatedEntityType: "Product",
                relatedEntityId: productId.ToString(),
                ct: ct);
            await _notificationService.CreateAsync(
                department: NotificationDepartments.Purchasing,
                title: "Urun silindi",
                message: $"{product.Name} ({product.Sku}) urunu IT tarafindan silindi.",
                relatedEntityType: "Product",
                relatedEntityId: productId.ToString(),
                ct: ct);
        }
        catch (DbUpdateException)
        {
            TempData["InventoryError"] = "Urun silinemedi. Iliskili kayitlar (siparis vb.) oldugu icin engellendi.";
        }

        return RedirectToAction(nameof(Receive));
    }

    [HttpGet]
    [Authorize(Roles = $"{IdentitySeed.RolePurchasing},{IdentitySeed.RoleWarehouse},{IdentitySeed.RoleIt},{IdentitySeed.RoleAdmin}")]
    public async Task<IActionResult> SubCategories(int parentId, CancellationToken ct)
    {
        if (parentId <= 0)
            return Json(Array.Empty<object>());

        var items = await _db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive && c.ParentId == parentId)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new
            {
                value = c.Id,
                text = c.Name
            })
            .ToListAsync(ct);

        return Json(items);
    }

    [HttpGet]
    [Authorize(Roles = $"{IdentitySeed.RolePurchasing},{IdentitySeed.RoleWarehouse},{IdentitySeed.RoleIt},{IdentitySeed.RoleAdmin}")]
    public async Task<IActionResult> ProductsByCategory(int categoryId, CancellationToken ct)
    {
        if (categoryId <= 0)
            return Json(Array.Empty<object>());

        var allCategories = await _db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new ReceiveCategoryOptionVm
            {
                CategoryId = c.Id,
                ParentCategoryId = c.ParentId,
                SortOrder = c.SortOrder,
                Name = c.Name
            })
            .ToListAsync(ct);

        var allowedCategoryIds = GetDescendantCategoryIds(allCategories, categoryId);

        var items = await _db.Products
            .AsNoTracking()
            .Where(p => allowedCategoryIds.Contains(p.CategoryId))
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                value = p.Id,
                text = p.Name + " (" + p.Sku + ")"
            })
            .ToListAsync(ct);

        return Json(items);
    }

    private async Task<IReadOnlyList<ReceiveProductOptionVm>> LoadProductOptionsAsync(CancellationToken ct)
    {
        return await _db.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new ReceiveProductOptionVm
            {
                ProductId = p.Id,
                CategoryId = p.CategoryId,
                Name = p.Name,
                Sku = p.Sku
            })
            .ToListAsync(ct);
    }

    private async Task<IReadOnlyList<ReceiveCategoryOptionVm>> LoadAllCategoriesAsync(CancellationToken ct)
    {
        return await _db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new ReceiveCategoryOptionVm
            {
                CategoryId = c.Id,
                ParentCategoryId = c.ParentId,
                SortOrder = c.SortOrder,
                Name = c.Name
            })
            .ToListAsync(ct);
    }

    private static IReadOnlyList<SelectListItem> BuildTopCategorySelectItems(IReadOnlyList<ReceiveCategoryOptionVm> categories)
    {
        return categories
            .Where(c => c.ParentCategoryId is null)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new SelectListItem
            {
                Value = c.CategoryId.ToString(),
                Text = c.Name
            })
            .ToList();
    }

    private static IReadOnlyList<SelectListItem> BuildChildCategorySelectItems(IReadOnlyList<ReceiveCategoryOptionVm> categories, int? parentCategoryId)
    {
        if (!parentCategoryId.HasValue || parentCategoryId <= 0)
            return Array.Empty<SelectListItem>();

        return categories
            .Where(c => c.ParentCategoryId == parentCategoryId.Value)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new SelectListItem
            {
                Value = c.CategoryId.ToString(),
                Text = c.Name
            })
            .ToList();
    }

    private static IReadOnlyList<SelectListItem> BuildProductSelectItems(
        IReadOnlyList<ReceiveProductOptionVm> allProducts,
        IReadOnlyList<ReceiveCategoryOptionVm> categories,
        int categoryId)
    {
        var allowedCategoryIds = GetDescendantCategoryIds(categories, categoryId);

        return allProducts
            .Where(p => categoryId <= 0 || allowedCategoryIds.Contains(p.CategoryId))
            .OrderBy(p => p.Name)
            .Select(p => new SelectListItem
            {
                Value = p.ProductId.ToString(),
                Text = $"{p.Name} ({p.Sku})"
            })
            .ToList();
    }

    private static HashSet<int> GetDescendantCategoryIds(IReadOnlyList<ReceiveCategoryOptionVm> categories, int categoryId)
    {
        var set = new HashSet<int>();
        if (categoryId <= 0)
            return set;

        var childrenByParent = categories
            .Where(c => c.ParentCategoryId.HasValue)
            .GroupBy(c => c.ParentCategoryId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.CategoryId).ToArray());

        var queue = new Queue<int>();
        queue.Enqueue(categoryId);
        set.Add(categoryId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!childrenByParent.TryGetValue(current, out var children))
                continue;

            foreach (var child in children)
            {
                if (set.Add(child))
                    queue.Enqueue(child);
            }
        }

        return set;
    }

    private async Task PopulateDashboardAsync(ReceiveStockVm model, CancellationToken ct)
    {
        model.StockSnapshots = await _db.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new StockSnapshotVm
            {
                ProductId = p.Id,
                ProductName = p.Name,
                Sku = p.Sku,
                Quantity = _db.Stocks.Where(s => s.ProductId == p.Id).Select(s => (int?)s.Quantity).FirstOrDefault() ?? 0
            })
            .ToListAsync(ct);

        model.RecentMovements = await _db.StockMovements
            .AsNoTracking()
            .OrderByDescending(m => m.CreatedAt)
            .Take(20)
            .Select(m => new StockMovementListItemVm
            {
                Id = m.Id,
                ProductId = m.ProductId,
                ProductName = m.Product != null ? m.Product.Name : "",
                Sku = m.Product != null ? m.Product.Sku : "",
                Type = m.Type.ToString(),
                Quantity = m.Quantity,
                Reason = m.Reason,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync(ct);
    }
}
