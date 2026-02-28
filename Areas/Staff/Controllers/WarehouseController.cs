using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Commerce;
using Pehlione.Models.ViewModels.Admin;
using Pehlione.Services;

namespace Pehlione.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = $"{IdentitySeed.RoleWarehouse},{IdentitySeed.RoleAdmin}")]
public sealed class WarehouseController : Controller
{
    private static readonly string[] WarehouseVisibleStatuses =
    [
        OrderStatusWorkflow.Paid,
        OrderStatusWorkflow.Processing,
        OrderStatusWorkflow.Packed,
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
    private readonly IOrderStatusEmailService _orderStatusEmailService;
    private readonly IOrderWorkflowNotificationService _orderWorkflowNotificationService;
    private readonly IOrderStatusTimelineService _orderStatusTimelineService;

    public WarehouseController(
        PehlioneDbContext db,
        IOrderStatusEmailService orderStatusEmailService,
        IOrderWorkflowNotificationService orderWorkflowNotificationService,
        IOrderStatusTimelineService orderStatusTimelineService)
    {
        _db = db;
        _orderStatusEmailService = orderStatusEmailService;
        _orderWorkflowNotificationService = orderWorkflowNotificationService;
        _orderStatusTimelineService = orderStatusTimelineService;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Orders(string? q, string? status, CancellationToken ct)
    {
        var isAdmin = User.IsInRole(IdentitySeed.RoleAdmin);
        var query = _db.Orders
            .AsNoTracking()
            .AsQueryable();

        if (!isAdmin)
        {
            var visibleStatuses = WarehouseVisibleStatuses.ToList();
            query = query.Where(o => visibleStatuses.Contains(o.Status));
        }

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
            item.NextStatusOptions = GetWarehouseAllowedStatuses(item.Status, isAdmin);

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
        ViewBag.IsAdmin = isAdmin;

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
        var isAdmin = User.IsInRole(IdentitySeed.RoleAdmin);
        if (!isAdmin && !OrderStatusWorkflow.IsWarehouseActionable(current))
        {
            TempData["WarehouseError"] = "Bu siparis depo islem kuyrugunda degil.";
            return RedirectToAction(nameof(Orders), new { q, status = currentStatus });
        }

        var allowed = GetWarehouseAllowedStatuses(current, isAdmin);
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

        var oldStatus = order.Status;
        order.Status = normalized;
        await _db.SaveChangesAsync(ct);
        await _orderStatusTimelineService.LogStatusChangedAsync(
            orderId: order.Id,
            fromStatus: oldStatus,
            toStatus: normalized,
            changedByUserId: User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            changedByDepartment: "Warehouse",
            ct: ct);
        await _orderStatusEmailService.NotifyStatusChangedAsync(order, oldStatus, normalized, ct);
        await _orderWorkflowNotificationService.OnStatusChangedAsync(order, oldStatus, normalized, ct);

        TempData["WarehouseSuccess"] = $"Siparis #{id} durumu guncellendi: {normalized}";
        return RedirectToAction(nameof(Orders), new { q, status = currentStatus });
    }

    private static string[] GetWarehouseAllowedStatuses(string? currentStatus, bool isAdmin)
    {
        var current = OrderStatusWorkflow.Normalize(currentStatus);
        if (isAdmin)
            return OrderStatusWorkflow.GetNextStatuses(current).ToArray();

        // Warehouse operasyonunda hızlı akış:
        // Paid -> Processing veya Shipped
        // Processing -> Shipped
        // Packed -> Shipped (eski veriler için)
        if (current.Equals(OrderStatusWorkflow.Paid, StringComparison.OrdinalIgnoreCase))
            return [OrderStatusWorkflow.Processing, OrderStatusWorkflow.Shipped];
        if (current.Equals(OrderStatusWorkflow.Processing, StringComparison.OrdinalIgnoreCase))
            return [OrderStatusWorkflow.Shipped];
        if (current.Equals(OrderStatusWorkflow.Packed, StringComparison.OrdinalIgnoreCase))
            return [OrderStatusWorkflow.Shipped];

        return Array.Empty<string>();
    }
}
