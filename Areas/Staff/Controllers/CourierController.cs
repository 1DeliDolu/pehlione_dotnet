using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Commerce;
using Pehlione.Models.ViewModels.Admin;
using Pehlione.Services;

namespace Pehlione.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = $"{IdentitySeed.RoleCourier},{IdentitySeed.RoleAdmin}")]
public sealed class CourierController : Controller
{
    private static readonly string[] CourierVisibleStatuses =
    [
        OrderStatusWorkflow.Cancelled,
        OrderStatusWorkflow.Shipped,
        OrderStatusWorkflow.CourierPickedUp,
        OrderStatusWorkflow.OutForDelivery,
        OrderStatusWorkflow.ReturnPickedUp,
        "Returned"
    ];

    private readonly PehlioneDbContext _db;
    private readonly IOrderStatusEmailService _orderStatusEmailService;
    private readonly IOrderWorkflowNotificationService _orderWorkflowNotificationService;
    private readonly IOrderStatusTimelineService _orderStatusTimelineService;

    public CourierController(
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

    [HttpGet]
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
            var visible = CourierVisibleStatuses.ToList();
            query = query.Where(o => visible.Contains(o.Status));
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
            var filter = OrderStatusWorkflow.Normalize(normalizedStatus);
            query = query.Where(o => o.Status == filter || o.Status == normalizedStatus);
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
        {
            item.NextStatusOptions = GetCourierAllowedStatuses(item.Status, isAdmin);
        }

        ViewBag.Query = normalizedQ;
        ViewBag.Status = OrderStatusWorkflow.Normalize(normalizedStatus);
        ViewBag.Statuses = items.Select(x => x.Status)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => Array.IndexOf(OrderStatusWorkflow.AllStatuses.ToArray(), x))
            .ToArray();

        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status, string? q = null, string? currentStatus = null, CancellationToken ct = default)
    {
        if (id <= 0)
            return RedirectToAction(nameof(Orders), new { q, status = currentStatus });

        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (order is null)
        {
            TempData["CourierError"] = "Siparis bulunamadi.";
            return RedirectToAction(nameof(Orders), new { q, status = currentStatus });
        }

        var current = OrderStatusWorkflow.Normalize(order.Status);
        var target = OrderStatusWorkflow.Normalize(status);
        var isAdmin = User.IsInRole(IdentitySeed.RoleAdmin);

        var allowed = GetCourierAllowedStatuses(current, isAdmin);

        if (!allowed.Contains(target, StringComparer.OrdinalIgnoreCase))
        {
            TempData["CourierError"] = $"Gecersiz gecis: {current} -> {target}";
            return RedirectToAction(nameof(Orders), new { q, status = currentStatus });
        }

        var oldStatus = order.Status;
        order.Status = target;
        await _db.SaveChangesAsync(ct);
        await _orderStatusTimelineService.LogStatusChangedAsync(
            orderId: order.Id,
            fromStatus: oldStatus,
            toStatus: target,
            changedByUserId: User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            changedByDepartment: "Courier",
            ct: ct);
        await _orderStatusEmailService.NotifyStatusChangedAsync(order, oldStatus, target, ct);
        await _orderWorkflowNotificationService.OnStatusChangedAsync(order, oldStatus, target, ct);

        TempData["CourierSuccess"] = $"Siparis #{id} durumu guncellendi: {target}";
        return RedirectToAction(nameof(Orders), new { q, status = currentStatus });
    }

    private static string[] GetCourierAllowedStatuses(string? currentStatus, bool isAdmin)
    {
        var current = OrderStatusWorkflow.Normalize(currentStatus);
        if (isAdmin)
            return OrderStatusWorkflow.GetNextStatuses(current).ToArray();

        // Kurye akisinda sade operasyon:
        // Shipped -> Courier Picked Up -> Delivered
        // Iade tarafi: Return Picked Up -> Return Delivered to Seller
        if (current.Equals(OrderStatusWorkflow.Shipped, StringComparison.OrdinalIgnoreCase))
            return [OrderStatusWorkflow.CourierPickedUp];
        if (current.Equals(OrderStatusWorkflow.Cancelled, StringComparison.OrdinalIgnoreCase))
            return [OrderStatusWorkflow.ReturnPickedUp];
        if (current.Equals(OrderStatusWorkflow.CourierPickedUp, StringComparison.OrdinalIgnoreCase))
            return [OrderStatusWorkflow.Delivered];
        if (current.Equals(OrderStatusWorkflow.OutForDelivery, StringComparison.OrdinalIgnoreCase))
            return [OrderStatusWorkflow.Delivered];
        if (current.Equals(OrderStatusWorkflow.ReturnPickedUp, StringComparison.OrdinalIgnoreCase))
            return [OrderStatusWorkflow.ReturnDeliveredToSeller];

        return Array.Empty<string>();
    }
}
