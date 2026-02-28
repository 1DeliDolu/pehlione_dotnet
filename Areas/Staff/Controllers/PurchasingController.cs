using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Inventory;
using Pehlione.Models.Commerce;
using Pehlione.Models.ViewModels.Admin;
using Pehlione.Services;

namespace Pehlione.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = $"{IdentitySeed.RolePurchasing},{IdentitySeed.RoleAdmin}")]
public sealed class PurchasingController : Controller
{
    private const string ReturnRestockReasonPrefix = "Return Restock Order #";
    private readonly PehlioneDbContext _db;
    private readonly IOrderWorkflowNotificationService _orderWorkflowNotificationService;

    public PurchasingController(PehlioneDbContext db, IOrderWorkflowNotificationService orderWorkflowNotificationService)
    {
        _db = db;
        _orderWorkflowNotificationService = orderWorkflowNotificationService;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Returns(string? q, CancellationToken ct)
    {
        var query = _db.Orders.AsNoTracking().AsQueryable();

        query = query.Where(o =>
            o.Status == OrderStatusWorkflow.Cancelled ||
            o.Status == OrderStatusWorkflow.ReturnDeliveredToSeller ||
            o.Status == "Returned" ||
            o.Status == OrderStatusWorkflow.Refunded);

        var normalizedQ = (q ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(normalizedQ))
        {
            query = query.Where(o =>
                o.Id.ToString().Contains(normalizedQ) ||
                (o.User != null && o.User.Email != null && o.User.Email.Contains(normalizedQ)));
        }

        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Take(500)
            .Select(o => new OrderListItemVm
            {
                Id = o.Id,
                CreatedAt = o.CreatedAt,
                CustomerEmail = o.User != null ? (o.User.Email ?? o.User.UserName ?? "-") : "-",
                Status = OrderStatusWorkflow.Normalize(o.Status),
                ShippingCarrier = o.ShippingCarrier,
                TrackingCode = o.TrackingCode,
                ItemCount = o.Items.Count,
                TotalAmount = o.TotalAmount,
                Currency = o.Currency
            })
            .ToListAsync(ct);

        var orderIds = items.Select(x => x.Id).Distinct().ToArray();
        var reasonKeys = orderIds.Select(id => ReturnRestockReasonPrefix + id).ToList();
        var restockedIds = await _db.StockMovements
            .AsNoTracking()
            .Where(m => m.Type == StockMovementType.In && m.Reason != null)
            .Where(m => reasonKeys.Contains(m.Reason!))
            .Select(m => m.Reason!)
            .ToListAsync(ct);

        var restockedOrderIdSet = restockedIds
            .Select(r => r.Replace(ReturnRestockReasonPrefix, "", StringComparison.Ordinal))
            .Select(x => int.TryParse(x, out var oid) ? oid : 0)
            .Where(x => x > 0)
            .ToHashSet();

        foreach (var item in items)
        {
            item.IsRestocked = restockedOrderIdSet.Contains(item.Id);
            item.CanRestock = !item.IsRestocked &&
                              (item.Status.Equals(OrderStatusWorkflow.ReturnDeliveredToSeller, StringComparison.OrdinalIgnoreCase)
                               || item.Status.Equals("Returned", StringComparison.OrdinalIgnoreCase));
        }

        ViewBag.Query = normalizedQ;
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestockReturnedOrder(int id, string? q = null, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            TempData["PurchasingError"] = "Gecersiz siparis.";
            return RedirectToAction(nameof(Returns), new { q });
        }

        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
        {
            TempData["PurchasingError"] = "Siparis bulunamadi.";
            return RedirectToAction(nameof(Returns), new { q });
        }

        var status = OrderStatusWorkflow.Normalize(order.Status);
        if (!status.Equals(OrderStatusWorkflow.ReturnDeliveredToSeller, StringComparison.OrdinalIgnoreCase))
        {
            TempData["PurchasingError"] = "Stok artisi sadece 'Return Delivered to Seller' durumunda yapilir.";
            return RedirectToAction(nameof(Returns), new { q });
        }

        var reason = ReturnRestockReasonPrefix + id;
        var alreadyRestocked = await _db.StockMovements
            .AsNoTracking()
            .AnyAsync(m => m.Type == StockMovementType.In && m.Reason == reason, ct);
        if (alreadyRestocked)
        {
            TempData["PurchasingError"] = $"Siparis #{id} icin iade stok girisi zaten yapilmis.";
            return RedirectToAction(nameof(Returns), new { q });
        }

        if (order.Items.Count == 0)
        {
            TempData["PurchasingError"] = "Sipariste iade edilecek urun kalemi yok.";
            return RedirectToAction(nameof(Returns), new { q });
        }

        var groupedItems = order.Items
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
            .Where(x => x.ProductId > 0 && x.Qty > 0)
            .ToList();

        if (groupedItems.Count == 0)
        {
            TempData["PurchasingError"] = "Iade urun kalemleri gecersiz.";
            return RedirectToAction(nameof(Returns), new { q });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        foreach (var item in groupedItems)
        {
            var rows = await _db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE stocks SET quantity = quantity + {item.Qty} WHERE product_id = {item.ProductId}",
                ct);

            if (rows == 0)
            {
                _db.Stocks.Add(new Stock
                {
                    ProductId = item.ProductId,
                    Quantity = item.Qty
                });
                await _db.SaveChangesAsync(ct);
            }

            _db.StockMovements.Add(new StockMovement
            {
                ProductId = item.ProductId,
                Type = StockMovementType.In,
                Quantity = item.Qty,
                Reason = reason,
                CreatedByUserId = userId
            });
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await _orderWorkflowNotificationService.OnReturnRestockApprovedAsync(order, ct);

        TempData["PurchasingSuccess"] = $"Siparis #{id} iade urunleri stoga eklendi.";
        return RedirectToAction(nameof(Returns), new { q });
    }
}
