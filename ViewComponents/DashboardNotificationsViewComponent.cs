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
            .Select(x => new DashboardNotificationItemVm
            {
                Id = x.Id,
                Department = x.Department,
                Title = x.Title,
                Message = x.Message,
                IsRead = x.IsRead,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);

        return View(new DashboardNotificationsVm
        {
            IsAdmin = isAdmin,
            UnreadCount = unreadCount,
            Items = items
        });
    }
}
