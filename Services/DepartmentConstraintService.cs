using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Communication;
using Pehlione.Models.Security;

namespace Pehlione.Services;

public sealed class DepartmentConstraintService : IDepartmentConstraintService
{
    private readonly PehlioneDbContext _db;

    public DepartmentConstraintService(PehlioneDbContext db)
    {
        _db = db;
    }

    public async Task<DepartmentAccessResult> GetAccessAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (user.IsInRole(IdentitySeed.RoleAdmin))
            return DepartmentAccessResult.AllowAll();

        var departments = GetDepartmentsForUser(user);
        if (departments.Count == 0)
            return new DepartmentAccessResult(false, false, false, 0);

        var persisted = await _db.Set<DepartmentConstraint>()
            .AsNoTracking()
            .Where(x => departments.Contains(x.Department))
            .ToListAsync(ct);

        var merged = departments
            .Select(dept => persisted.FirstOrDefault(x => x.Department == dept) ?? GetDefaultConstraint(dept))
            .ToList();

        var canRead = merged.Any(x => x.CanReadStock);
        var canIncrease = merged.Any(x => x.CanIncreaseStock);
        var canDelete = merged.Any(x => x.CanDeleteStock);

        int? maxReceiveQty = null;
        var maxValues = merged
            .Where(x => x.MaxReceiveQuantity.HasValue)
            .Select(x => x.MaxReceiveQuantity!.Value)
            .ToList();

        if (maxValues.Count > 0)
            maxReceiveQty = maxValues.Max();

        return new DepartmentAccessResult(canRead, canIncrease, canDelete, maxReceiveQty);
    }

    public static IReadOnlyList<string> GetSupportedDepartments()
    {
        return new[]
        {
            NotificationDepartments.Sales,
            NotificationDepartments.Purchasing,
            NotificationDepartments.It,
            NotificationDepartments.Warehouse,
            NotificationDepartments.Accounting,
            NotificationDepartments.Courier
        };
    }

    public static DepartmentConstraint GetDefaultConstraint(string department)
    {
        if (department.Equals(NotificationDepartments.Purchasing, StringComparison.OrdinalIgnoreCase))
        {
            return new DepartmentConstraint
            {
                Department = NotificationDepartments.Purchasing,
                CanReadStock = true,
                CanIncreaseStock = true,
                CanDeleteStock = false,
                MaxReceiveQuantity = null
            };
        }

        if (department.Equals(NotificationDepartments.It, StringComparison.OrdinalIgnoreCase))
        {
            return new DepartmentConstraint
            {
                Department = NotificationDepartments.It,
                CanReadStock = true,
                CanIncreaseStock = false,
                CanDeleteStock = true,
                MaxReceiveQuantity = null
            };
        }

        if (department.Equals(NotificationDepartments.Warehouse, StringComparison.OrdinalIgnoreCase))
        {
            return new DepartmentConstraint
            {
                Department = NotificationDepartments.Warehouse,
                CanReadStock = true,
                CanIncreaseStock = true,
                CanDeleteStock = false,
                MaxReceiveQuantity = null
            };
        }

        if (department.Equals(NotificationDepartments.Accounting, StringComparison.OrdinalIgnoreCase))
        {
            return new DepartmentConstraint
            {
                Department = NotificationDepartments.Accounting,
                CanReadStock = true,
                CanIncreaseStock = false,
                CanDeleteStock = false,
                MaxReceiveQuantity = null
            };
        }

        if (department.Equals(NotificationDepartments.Courier, StringComparison.OrdinalIgnoreCase))
        {
            return new DepartmentConstraint
            {
                Department = NotificationDepartments.Courier,
                CanReadStock = true,
                CanIncreaseStock = false,
                CanDeleteStock = false,
                MaxReceiveQuantity = null
            };
        }

        return new DepartmentConstraint
        {
            Department = NotificationDepartments.Sales,
            CanReadStock = true,
            CanIncreaseStock = false,
            CanDeleteStock = false,
            MaxReceiveQuantity = null
        };
    }

    private static HashSet<string> GetDepartmentsForUser(ClaimsPrincipal user)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (user.IsInRole(IdentitySeed.RoleStaff))
            set.Add(NotificationDepartments.Sales);

        if (user.IsInRole(IdentitySeed.RolePurchasing))
            set.Add(NotificationDepartments.Purchasing);

        if (user.IsInRole(IdentitySeed.RoleIt))
            set.Add(NotificationDepartments.It);

        if (user.IsInRole(IdentitySeed.RoleWarehouse))
            set.Add(NotificationDepartments.Warehouse);

        if (user.IsInRole(IdentitySeed.RoleAccounting))
            set.Add(NotificationDepartments.Accounting);

        if (user.IsInRole(IdentitySeed.RoleCourier))
            set.Add(NotificationDepartments.Courier);

        return set;
    }
}
