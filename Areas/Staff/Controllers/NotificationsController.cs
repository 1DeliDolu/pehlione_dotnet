using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Communication;
using Pehlione.Models.ViewModels.Staff;

namespace Pehlione.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = $"{IdentitySeed.RoleStaff},{IdentitySeed.RolePurchasing},{IdentitySeed.RoleWarehouse},{IdentitySeed.RoleIt},{IdentitySeed.RoleHr},{IdentitySeed.RoleAccounting},{IdentitySeed.RoleCourier},{IdentitySeed.RoleCustomerRelations},{IdentitySeed.RoleAdmin}")]
public sealed class NotificationsController : Controller
{
    private readonly PehlioneDbContext _db;

    public NotificationsController(PehlioneDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(bool includeRead = false, string? q = null, string? department = null, CancellationToken ct = default)
    {
        var allowedDepartments = GetAllDepartments();

        var query = _db.Notifications.AsNoTracking();

        var normalizedDepartment = (department ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(normalizedDepartment))
        {
            if (allowedDepartments.Contains(normalizedDepartment))
                query = query.Where(x => x.Department == normalizedDepartment);
            else
                normalizedDepartment = "";
        }

        var normalizedQuery = (q ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            query = query.Where(x =>
                x.Title.Contains(normalizedQuery) ||
                x.Message.Contains(normalizedQuery) ||
                (x.RelatedEntityType != null && x.RelatedEntityType.Contains(normalizedQuery)) ||
                (x.RelatedEntityId != null && x.RelatedEntityId.Contains(normalizedQuery)));
        }

        if (!includeRead)
            query = query.Where(x => !x.IsRead);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .Select(x => new NotificationListItemVm
            {
                Id = x.Id,
                Department = x.Department,
                Title = x.Title,
                Message = x.Message,
                RelatedEntityType = x.RelatedEntityType,
                RelatedEntityId = x.RelatedEntityId,
                IsRead = x.IsRead,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);

        foreach (var item in items)
            item.LinkUrl = BuildLink(item.RelatedEntityType, item.RelatedEntityId, item.Department);

        return View(new NotificationIndexVm
        {
            IncludeRead = includeRead,
            Query = normalizedQuery,
            Department = normalizedDepartment,
            DepartmentOptions = allowedDepartments.OrderBy(x => x).ToArray(),
            Items = items
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEvent(
        string[]? departments,
        string? department,
        string title,
        string message,
        string? returnUrl = null,
        CancellationToken ct = default)
    {
        var normalizedTitle = (title ?? "").Trim();
        var normalizedMessage = (message ?? "").Trim();

        var targetDepartments = (departments ?? Array.Empty<string>())
            .Select(x => (x ?? "").Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (targetDepartments.Count == 0 && !string.IsNullOrWhiteSpace(department))
            targetDepartments.Add(department.Trim());

        if (targetDepartments.Count == 0 || string.IsNullOrWhiteSpace(normalizedTitle) || string.IsNullOrWhiteSpace(normalizedMessage))
        {
            TempData["EventError"] = "Departman, baslik ve mesaj zorunludur.";
            return RedirectToLocalOrIndex(returnUrl);
        }

        var allowedDepartments = GetAllDepartments();
        if (targetDepartments.Any(d => !allowedDepartments.Contains(d)))
        {
            TempData["EventError"] = "Bu departman icin event olusturma yetkiniz yok.";
            return RedirectToLocalOrIndex(returnUrl);
        }

        foreach (var targetDepartment in targetDepartments)
        {
            _db.Notifications.Add(new Notification
            {
                Department = targetDepartment,
                Title = normalizedTitle,
                Message = normalizedMessage,
                RelatedEntityType = "ManualEvent",
                CreatedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync(ct);

        TempData["EventSuccess"] = $"Event olusturuldu. Hedef departman sayisi: {targetDepartments.Count}";
        return RedirectToLocalOrIndex(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(long id, bool includeRead = false, string? q = null, string? department = null, CancellationToken ct = default)
    {
        if (id <= 0)
            return RedirectToAction(nameof(Index), new { includeRead, q, department });

        var item = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null)
            return RedirectToAction(nameof(Index), new { includeRead, q, department });

        if (!CanAccessDepartment(item.Department))
            return Forbid();

        if (!item.IsRead)
        {
            item.IsRead = true;
            await _db.SaveChangesAsync(ct);
        }

        return RedirectToAction(nameof(Index), new { includeRead, q, department });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(bool includeRead = false, string? q = null, string? department = null, CancellationToken ct = default)
    {
        var query = _db.Notifications.Where(x => !x.IsRead);

        if (!User.IsInRole(IdentitySeed.RoleAdmin))
        {
            var allowedDepartments = GetAllDepartments();
            if (allowedDepartments.Count == 0)
                return RedirectToAction(nameof(Index), new { includeRead, q, department });

            query = query.Where(x => allowedDepartments.Contains(x.Department));
        }

        var normalizedDepartment = (department ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(normalizedDepartment))
            query = query.Where(x => x.Department == normalizedDepartment);

        await query.ExecuteUpdateAsync(x => x.SetProperty(y => y.IsRead, true), ct);
        return RedirectToAction(nameof(Index), new { includeRead, q, department });
    }

    private bool CanAccessDepartment(string department)
    {
        return GetAllDepartments().Contains(department);
    }

    private static HashSet<string> GetAllDepartments()
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            NotificationDepartments.Sales,
            NotificationDepartments.Purchasing,
            NotificationDepartments.Warehouse,
            NotificationDepartments.It,
            NotificationDepartments.Hr,
            NotificationDepartments.Accounting,
            NotificationDepartments.Courier,
            NotificationDepartments.CustomerRelations,
        };
    }

    private IActionResult RedirectToLocalOrIndex(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }

    private string? BuildLink(string? relatedEntityType, string? relatedEntityId, string department)
    {
        if (string.Equals(relatedEntityType, "Order", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(relatedEntityId))
            {
                if (User.IsInRole(IdentitySeed.RoleAdmin))
                    return Url.Action("Index", "Orders", new { area = "Admin", q = relatedEntityId });

                if (department.Equals(NotificationDepartments.Accounting, StringComparison.OrdinalIgnoreCase))
                    return Url.Action("Orders", "Accounting", new { area = "Staff", q = relatedEntityId });

                if (department.Equals(NotificationDepartments.Warehouse, StringComparison.OrdinalIgnoreCase))
                    return Url.Action("Orders", "Warehouse", new { area = "Staff", q = relatedEntityId });

                if (department.Equals(NotificationDepartments.Courier, StringComparison.OrdinalIgnoreCase))
                    return Url.Action("Orders", "Courier", new { area = "Staff", q = relatedEntityId });

                if (department.Equals(NotificationDepartments.Purchasing, StringComparison.OrdinalIgnoreCase))
                    return Url.Action("Returns", "Purchasing", new { area = "Staff", q = relatedEntityId });

                if (department.Equals(NotificationDepartments.CustomerRelations, StringComparison.OrdinalIgnoreCase))
                    return Url.Action("Index", "CustomerRelations", new { area = "Staff" });
            }

            return Url.Action("Index", "Notifications", new { area = "Staff" });
        }

        if (string.Equals(relatedEntityType, "Product", StringComparison.OrdinalIgnoreCase))
            return Url.Action("Receive", "Inventory", new { area = "Staff", productId = relatedEntityId });

        return null;
    }
}
