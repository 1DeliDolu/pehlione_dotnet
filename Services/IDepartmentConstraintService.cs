using System.Security.Claims;

namespace Pehlione.Services;

public interface IDepartmentConstraintService
{
    Task<DepartmentAccessResult> GetAccessAsync(ClaimsPrincipal user, CancellationToken ct = default);
}

public sealed record DepartmentAccessResult(bool CanIncreaseStock, bool CanDeleteStock, int? MaxReceiveQuantity)
{
    public static DepartmentAccessResult AllowAll() => new(true, true, null);
}
