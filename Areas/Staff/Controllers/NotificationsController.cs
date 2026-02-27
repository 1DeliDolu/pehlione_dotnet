using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Communication;
using Pehlione.Models.ViewModels.Staff;

namespace Pehlione.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = $"{IdentitySeed.RoleStaff},{IdentitySeed.RolePurchasing},{IdentitySeed.RoleIt},{IdentitySeed.RoleAdmin}")]
public sealed class NotificationsController : Controller
{
    private readonly PehlioneDbContext _db;

    public NotificationsController(PehlioneDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(bool includeRead = false, CancellationToken ct = default)
    {
        var query = _db.Notifications.AsNoTracking();
        if (!User.IsInRole(IdentitySeed.RoleAdmin))
        {
            var allowedDepartments = GetAllowedDepartments();
            if (allowedDepartments.Count == 0)
                return View(new NotificationIndexVm());

            query = query.Where(x => allowedDepartments.Contains(x.Department));
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

        return View(new NotificationIndexVm
        {
            IncludeRead = includeRead,
            Items = items
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(long id, bool includeRead = false, CancellationToken ct = default)
    {
        if (id <= 0)
            return RedirectToAction(nameof(Index), new { includeRead });

        var item = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null)
            return RedirectToAction(nameof(Index), new { includeRead });

        if (!CanAccessDepartment(item.Department))
            return Forbid();

        if (!item.IsRead)
        {
            item.IsRead = true;
            await _db.SaveChangesAsync(ct);
        }

        return RedirectToAction(nameof(Index), new { includeRead });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(bool includeRead = false, CancellationToken ct = default)
    {
        var query = _db.Notifications.Where(x => !x.IsRead);

        if (!User.IsInRole(IdentitySeed.RoleAdmin))
        {
            var allowedDepartments = GetAllowedDepartments();
            if (allowedDepartments.Count == 0)
                return RedirectToAction(nameof(Index), new { includeRead });

            query = query.Where(x => allowedDepartments.Contains(x.Department));
        }

        await query.ExecuteUpdateAsync(x => x.SetProperty(y => y.IsRead, true), ct);
        return RedirectToAction(nameof(Index), new { includeRead });
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

        return departments;
    }
}
