using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.ViewModels.Staff;
using Pehlione.Services;

namespace Pehlione.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = $"{IdentitySeed.RolePurchasing},{IdentitySeed.RoleAdmin}")]
public sealed class InventoryController : Controller
{
    private readonly PehlioneDbContext _db;
    private readonly IInventoryService _inventoryService;

    public InventoryController(PehlioneDbContext db, IInventoryService inventoryService)
    {
        _db = db;
        _inventoryService = inventoryService;
    }

    [HttpGet]
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
