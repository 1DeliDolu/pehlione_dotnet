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
            ReturnUrl = HttpContext?.Request?.Path.Value ?? "",
            CreateEventDepartmentOptions = GetAllDepartmentOptions(),
            Items = mapped
        });
    }

    private static string[] GetAllDepartmentOptions()
    {
        return
        [
            NotificationDepartments.Sales,
            NotificationDepartments.Purchasing,
            NotificationDepartments.Warehouse,
            NotificationDepartments.It,
            NotificationDepartments.Hr,
            NotificationDepartments.Accounting,
            NotificationDepartments.Courier,
            NotificationDepartments.CustomerRelations
        ];
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
