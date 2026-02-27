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

    public InventoryController(
        PehlioneDbContext db,
        IInventoryService inventoryService,
        INotificationService notificationService)
    {
        _db = db;
        _inventoryService = inventoryService;
        _notificationService = notificationService;
    }

    [HttpGet]
    [Authorize(Roles = $"{IdentitySeed.RolePurchasing},{IdentitySeed.RoleIt},{IdentitySeed.RoleAdmin}")]
    public async Task<IActionResult> Receive(int? productId, CancellationToken ct)
    {
        var vm = new ReceiveStockVm
        {
            ProductId = productId.GetValueOrDefault(),
            ProductOptions = await LoadProductOptionsAsync(ct)
        };

        if (vm.ProductId <= 0 && vm.ProductOptions.Count > 0 && int.TryParse(vm.ProductOptions[0].Value, out var firstProductId))
            vm.ProductId = firstProductId;

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "CanIncreaseStock")]
    public async Task<IActionResult> Receive(ReceiveStockVm model, CancellationToken ct)
    {
        model.ProductOptions = await LoadProductOptionsAsync(ct);

        if (!ModelState.IsValid)
            return View(model);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _inventoryService.ReceiveStockAsync(model.ProductId, model.Quantity, model.Note, userId, ct);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Stok girisi basarisiz.");
            return View(model);
        }

        TempData["InventorySuccess"] = $"Stok guncellendi. Yeni stok: {result.CurrentQuantity}";
        return RedirectToAction(nameof(Receive), new { productId = model.ProductId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "CanDeleteStock")]
    public async Task<IActionResult> DeleteProduct(int productId, CancellationToken ct)
    {
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

    private async Task<IReadOnlyList<SelectListItem>> LoadProductOptionsAsync(CancellationToken ct)
    {
        return await _db.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new SelectListItem
            {
                Value = p.Id.ToString(),
                Text = $"{p.Name} ({p.Sku})"
            })
            .ToListAsync(ct);
    }
}
