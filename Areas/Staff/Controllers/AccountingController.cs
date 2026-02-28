using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Commerce;
using Pehlione.Models.ViewModels.Admin;
using Pehlione.Services;

namespace Pehlione.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = $"{IdentitySeed.RoleAccounting},{IdentitySeed.RoleAdmin}")]
public sealed class AccountingController : Controller
{
    private static readonly string[] AccountingVisibleStatuses =
    [
        OrderStatusWorkflow.Pending,
        OrderStatusWorkflow.Paid,
        OrderStatusWorkflow.Cancelled,
        OrderStatusWorkflow.ReturnDeliveredToSeller,
        OrderStatusWorkflow.Refunded,
        "Odendi"
    ];

    private readonly PehlioneDbContext _db;
    private readonly IOrderStatusEmailService _orderStatusEmailService;
    private readonly IOrderWorkflowNotificationService _orderWorkflowNotificationService;
    private readonly IOrderStatusTimelineService _orderStatusTimelineService;

    public AccountingController(
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
            var visibleStatuses = AccountingVisibleStatuses.ToList();
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
            item.NextStatusOptions = isAdmin
                ? OrderStatusWorkflow.GetNextStatuses(item.Status).ToArray()
                : OrderStatusWorkflow.GetNextStatuses(item.Status)
                    .Where(x => x.Equals(OrderStatusWorkflow.Paid, StringComparison.OrdinalIgnoreCase)
                                || x.Equals(OrderStatusWorkflow.Refunded, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
        }

        var statuses = items
            .Select(x => x.Status)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => Array.IndexOf(OrderStatusWorkflow.AllStatuses.ToArray(), x))
            .ToArray();

        ViewBag.Query = normalizedQ;
        ViewBag.Status = OrderStatusWorkflow.Normalize(normalizedStatus);
        ViewBag.Statuses = statuses;

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
            TempData["AccountingError"] = "Siparis bulunamadi.";
            return RedirectToAction(nameof(Orders), new { q, status = currentStatus });
        }

        var current = OrderStatusWorkflow.Normalize(order.Status);
        var target = OrderStatusWorkflow.Normalize(status);
        var isAdmin = User.IsInRole(IdentitySeed.RoleAdmin);

        if (!isAdmin &&
            !target.Equals(OrderStatusWorkflow.Paid, StringComparison.OrdinalIgnoreCase)
            && !target.Equals(OrderStatusWorkflow.Refunded, StringComparison.OrdinalIgnoreCase))
        {
            TempData["AccountingError"] = "Muhasebe sadece Paid veya Refunded adimlarini gunceller.";
            return RedirectToAction(nameof(Orders), new { q, status = currentStatus });
        }

        if (!OrderStatusWorkflow.CanTransition(current, target))
        {
            TempData["AccountingError"] = $"Gecersiz gecis: {current} -> {target}";
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
            changedByDepartment: "Accounting",
            ct: ct);
        await _orderStatusEmailService.NotifyStatusChangedAsync(order, oldStatus, target, ct);
        await _orderWorkflowNotificationService.OnStatusChangedAsync(order, oldStatus, target, ct);

        TempData["AccountingSuccess"] = $"Siparis #{id} durumu guncellendi: {target}";
        return RedirectToAction(nameof(Orders), new { q, status = currentStatus });
    }
}
