using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Commerce;
using Pehlione.Models.ViewModels.Admin;

namespace Pehlione.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = $"{IdentitySeed.RoleWarehouse},{IdentitySeed.RoleAdmin}")]
public sealed class WarehouseController : Controller
{
    private static readonly string[] WarehouseVisibleStatuses =
    [
        OrderStatusWorkflow.Paid,
        OrderStatusWorkflow.Processing,
        OrderStatusWorkflow.Shipped,
        "Odendi",
        "Hazirlaniyor",
        "Paketlendi",
        "Gonderildi"
    ];

    private static readonly string[] ShippingCarriers =
    [
        "DHL",
        "UPS",
        "GLS",
        "Hermes",
        "Yurtiçi Kargo",
        "MNG Kargo",
        "Aras Kargo",
        "PTT Kargo"
    ];

    private readonly PehlioneDbContext _db;

    public WarehouseController(PehlioneDbContext db)
    {
        _db = db;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Orders(string? q, string? status, CancellationToken ct)
    {
        var query = _db.Orders
            .AsNoTracking()
            .Where(o => WarehouseVisibleStatuses.Contains(o.Status))
            .AsQueryable();

        var normalizedQ = (q ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(normalizedQ))
        {
            query = query.Where(o =>
                o.Id.ToString().Contains(normalizedQ) ||
                (o.User != null && o.User.Email != null && o.User.Email.Contains(normalizedQ)));
        }

        var normalizedStatus = (status ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            var filterStatus = OrderStatusWorkflow.Normalize(normalizedStatus);
            query = query.Where(o => o.Status == filterStatus || o.Status == normalizedStatus);
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

        foreach (var item in items)
            item.NextStatusOptions = OrderStatusWorkflow.GetNextStatuses(item.Status)
                .Where(x => x.Equals(OrderStatusWorkflow.Processing, StringComparison.OrdinalIgnoreCase)
                            || x.Equals(OrderStatusWorkflow.Shipped, StringComparison.OrdinalIgnoreCase)
                            || x.Equals(OrderStatusWorkflow.Delivered, StringComparison.OrdinalIgnoreCase))
                .ToArray();

        var existingStatuses = items
            .Select(x => x.Status)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => Array.IndexOf(OrderStatusWorkflow.AllStatuses.ToArray(), x))
            .ToArray();

        ViewBag.Query = normalizedQ;
        ViewBag.Status = OrderStatusWorkflow.Normalize(normalizedStatus);
        ViewBag.Statuses = existingStatuses;
        ViewBag.ShippingCarriers = ShippingCarriers;
        ViewBag.IsAdmin = User.IsInRole(IdentitySeed.RoleAdmin);

        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOrderStatus(
        int id,
        string status,
        string? shippingCarrier = null,
        string? trackingCode = null,
        string? q = null,
        string? currentStatus = null,
        CancellationToken ct = default)
    {
        if (id <= 0)
            return RedirectToAction(nameof(Orders), new { q, status = currentStatus });

        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (order is null)
        {
            TempData["WarehouseError"] = "Siparis bulunamadi.";
            return RedirectToAction(nameof(Orders), new { q, status = currentStatus });
        }

        var normalized = OrderStatusWorkflow.Normalize(status);
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 32)
        {
            TempData["WarehouseError"] = "Gecersiz siparis durumu.";
            return RedirectToAction(nameof(Orders), new { q, status = currentStatus });
        }

        var current = OrderStatusWorkflow.Normalize(order.Status);
        if (!OrderStatusWorkflow.IsWarehouseActionable(current))
        {
            TempData["WarehouseError"] = "Bu siparis depo islem kuyrugunda degil.";
            return RedirectToAction(nameof(Orders), new { q, status = currentStatus });
        }

        var allowed = OrderStatusWorkflow.GetNextStatuses(current)
            .Where(x => x.Equals(OrderStatusWorkflow.Processing, StringComparison.OrdinalIgnoreCase)
                        || x.Equals(OrderStatusWorkflow.Shipped, StringComparison.OrdinalIgnoreCase)
                        || x.Equals(OrderStatusWorkflow.Delivered, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (!allowed.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            TempData["WarehouseError"] = $"Gecersiz gecis: {current} -> {normalized}";
            return RedirectToAction(nameof(Orders), new { q, status = currentStatus });
        }

        if (normalized.Equals(OrderStatusWorkflow.Shipped, StringComparison.OrdinalIgnoreCase))
        {
            var carrier = (shippingCarrier ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(carrier))
            {
                TempData["WarehouseError"] = "Shipped icin kargo firmasi secilmeli.";
                return RedirectToAction(nameof(Orders), new { q, status = currentStatus });
            }

            order.ShippingCarrier = carrier;
            order.TrackingCode = (trackingCode ?? string.Empty).Trim();
        }

        order.Status = normalized;
        await _db.SaveChangesAsync(ct);

        TempData["WarehouseSuccess"] = $"Siparis #{id} durumu guncellendi: {normalized}";
        return RedirectToAction(nameof(Orders), new { q, status = currentStatus });
    }
}
