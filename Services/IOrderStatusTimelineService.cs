namespace Pehlione.Services;

public interface IOrderStatusTimelineService
{
    Task LogOrderPlacedAsync(int orderId, string? changedByUserId = null, CancellationToken ct = default);
    Task LogStatusChangedAsync(
        int orderId,
        string? fromStatus,
        string? toStatus,
        string? changedByUserId = null,
        string? changedByDepartment = null,
        CancellationToken ct = default);
}
