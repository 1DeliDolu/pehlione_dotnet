using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Commerce;
using Pehlione.Models.ViewModels.Admin;
using Pehlione.Services;

namespace Pehlione.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = IdentitySeed.RoleAdmin)]
public sealed class OrdersController : Controller
{
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

    public OrdersController(
        PehlioneDbContext db,
        IOrderStatusEmailService orderStatusEmailService,
        IOrderWorkflowNotificationService orderWorkflowNotificationService)
    {
        _db = db;
        _orderStatusEmailService = orderStatusEmailService;
        _orderWorkflowNotificationService = orderWorkflowNotificationService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? status, CancellationToken ct)
    {
        var query = _db.Orders
            .AsNoTracking()
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
            item.NextStatusOptions = OrderStatusWorkflow.GetNextStatuses(item.Status);

        ViewBag.Query = normalizedQ;
        ViewBag.Status = OrderStatusWorkflow.Normalize(normalizedStatus);
        var existingStatuses = items
            .Select(x => x.Status)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .OrderBy(x => Array.IndexOf(OrderStatusWorkflow.AllStatuses.ToArray(), x))
            .ToArray();
        ViewBag.Statuses = existingStatuses;
        ViewBag.StatusOptions = OrderStatusWorkflow.AllStatuses
            .Concat(existingStatuses)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        ViewBag.ShippingCarriers = ShippingCarriers;

        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(
        int id,
        string status,
        string? shippingCarrier = null,
        string? trackingCode = null,
        string? q = null,
        string? currentStatus = null,
        CancellationToken ct = default)
    {
        if (id <= 0)
            return RedirectToAction(nameof(Index), new { q, status = currentStatus });

        var normalized = OrderStatusWorkflow.Normalize(status);
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 32)
        {
            TempData["AdminError"] = "Gecersiz siparis durumu.";
            return RedirectToAction(nameof(Index), new { q, status = currentStatus });
        }

        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (order is null)
        {
            TempData["AdminError"] = "Siparis bulunamadi.";
            return RedirectToAction(nameof(Index), new { q, status = currentStatus });
        }

        var currentNormalized = OrderStatusWorkflow.Normalize(order.Status);
        if (!OrderStatusWorkflow.CanTransition(currentNormalized, normalized))
        {
            TempData["AdminError"] = $"Gecersiz gecis: {currentNormalized} -> {normalized}";
            return RedirectToAction(nameof(Index), new { q, status = currentStatus });
        }

        if (normalized.Equals(OrderStatusWorkflow.Shipped, StringComparison.OrdinalIgnoreCase))
        {
            var carrier = (shippingCarrier ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(carrier))
            {
                TempData["AdminError"] = "Shipped icin kargo firmasi secilmeli.";
                return RedirectToAction(nameof(Index), new { q, status = currentStatus });
            }

            order.ShippingCarrier = carrier;
            order.TrackingCode = (trackingCode ?? string.Empty).Trim();
        }

        var oldStatus = order.Status;
        order.Status = normalized;
        await _db.SaveChangesAsync(ct);
        await _orderStatusEmailService.NotifyStatusChangedAsync(order, oldStatus, normalized, ct);
        await _orderWorkflowNotificationService.OnStatusChangedAsync(order, oldStatus, normalized, ct);

        TempData["AdminSuccess"] = $"Siparis #{id} durumu guncellendi: {normalized}";
        return RedirectToAction(nameof(Index), new { q, status = currentStatus });
    }
}
