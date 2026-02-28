using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Communication;
using Pehlione.Models.ViewModels.Staff;

namespace Pehlione.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = $"{IdentitySeed.RoleStaff},{IdentitySeed.RolePurchasing},{IdentitySeed.RoleWarehouse},{IdentitySeed.RoleIt},{IdentitySeed.RoleAccounting},{IdentitySeed.RoleCourier},{IdentitySeed.RoleAdmin}")]
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
        var isAdmin = User.IsInRole(IdentitySeed.RoleAdmin);
        var allowedDepartments = isAdmin ? GetAllDepartments() : GetAllowedDepartments();

        var query = _db.Notifications.AsNoTracking();
        if (!isAdmin)
        {
            if (allowedDepartments.Count == 0)
                return View(new NotificationIndexVm());

            query = query.Where(x => allowedDepartments.Contains(x.Department));
        }

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
            var allowedDepartments = GetAllowedDepartments();
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
        if (User.IsInRole(IdentitySeed.RoleAdmin))
            return true;

        return GetAllowedDepartments().Contains(department);
    }

    private HashSet<string> GetAllowedDepartments()
    {
        var departments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (User.IsInRole(IdentitySeed.RoleStaff))
            departments.Add(NotificationDepartments.Sales);
        if (User.IsInRole(IdentitySeed.RolePurchasing))
            departments.Add(NotificationDepartments.Purchasing);
        if (User.IsInRole(IdentitySeed.RoleIt))
            departments.Add(NotificationDepartments.It);
        if (User.IsInRole(IdentitySeed.RoleWarehouse))
            departments.Add(NotificationDepartments.Warehouse);
        if (User.IsInRole(IdentitySeed.RoleAccounting))
            departments.Add(NotificationDepartments.Accounting);
        if (User.IsInRole(IdentitySeed.RoleCourier))
            departments.Add(NotificationDepartments.Courier);

        return departments;
    }

    private static HashSet<string> GetAllDepartments()
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            NotificationDepartments.Sales,
            NotificationDepartments.Purchasing,
            NotificationDepartments.Warehouse,
            NotificationDepartments.It,
            NotificationDepartments.Accounting,
            NotificationDepartments.Courier,
            "HR"
        };
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
            }

            return Url.Action("Index", "Notifications", new { area = "Staff" });
        }

        if (string.Equals(relatedEntityType, "Product", StringComparison.OrdinalIgnoreCase))
            return Url.Action("Receive", "Inventory", new { area = "Staff", productId = relatedEntityId });

        return null;
    }
}
