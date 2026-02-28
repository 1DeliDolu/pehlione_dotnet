using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Communication;
using Pehlione.Models.ViewModels.Shared;

namespace Pehlione.ViewComponents;

public sealed class DashboardNotificationsViewComponent : ViewComponent
{
    private readonly PehlioneDbContext _db;

    public DashboardNotificationsViewComponent(PehlioneDbContext db)
    {
        _db = db;
    }

    public async Task<IViewComponentResult> InvokeAsync(int take = 8, CancellationToken ct = default)
    {
        var isAdmin = UserClaimsPrincipal.IsInRole(IdentitySeed.RoleAdmin);
        var query = _db.Notifications.AsNoTracking().AsQueryable();

        if (!isAdmin)
        {
            var departments = new List<string>();
            if (UserClaimsPrincipal.IsInRole(IdentitySeed.RoleStaff))
                departments.Add(NotificationDepartments.Sales);
            if (UserClaimsPrincipal.IsInRole(IdentitySeed.RolePurchasing))
                departments.Add(NotificationDepartments.Purchasing);
            if (UserClaimsPrincipal.IsInRole(IdentitySeed.RoleWarehouse))
                departments.Add(NotificationDepartments.Warehouse);
            if (UserClaimsPrincipal.IsInRole(IdentitySeed.RoleIt))
                departments.Add(NotificationDepartments.It);
            if (UserClaimsPrincipal.IsInRole(IdentitySeed.RoleHr))
                departments.Add("HR");
            if (UserClaimsPrincipal.IsInRole(IdentitySeed.RoleAccounting))
                departments.Add(NotificationDepartments.Accounting);
            if (UserClaimsPrincipal.IsInRole(IdentitySeed.RoleCourier))
                departments.Add(NotificationDepartments.Courier);

            if (departments.Count == 0)
                return View(new DashboardNotificationsVm());

            query = query.Where(x => departments.Contains(x.Department));
        }

        var unreadCount = await query.CountAsync(x => !x.IsRead, ct);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(take, 1, 20))
            .Select(x => new
            {
                x.Id,
                x.Department,
                x.Title,
                x.Message,
                x.RelatedEntityType,
                x.RelatedEntityId,
                x.IsRead,
                x.CreatedAt
            })
            .ToListAsync(ct);

        var mapped = items.Select(x => new DashboardNotificationItemVm
        {
            Id = x.Id,
            Department = x.Department,
            Title = x.Title,
            Message = x.Message,
            LinkUrl = BuildLink(x.RelatedEntityType, x.RelatedEntityId, x.Department),
            IsRead = x.IsRead,
            CreatedAt = x.CreatedAt
        }).ToList();

        return View(new DashboardNotificationsVm
        {
            IsAdmin = isAdmin,
            UnreadCount = unreadCount,
            Items = mapped
        });
    }

    private string? BuildLink(string? relatedEntityType, string? relatedEntityId, string department)
    {
        if (string.Equals(relatedEntityType, "Order", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(relatedEntityId))
            {
                if (UserClaimsPrincipal.IsInRole(IdentitySeed.RoleAdmin))
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
