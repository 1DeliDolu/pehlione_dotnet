using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Commerce;

namespace Pehlione.Services;

public sealed class OrderStatusTimelineService : IOrderStatusTimelineService
{
    private readonly PehlioneDbContext _db;

    public OrderStatusTimelineService(PehlioneDbContext db)
    {
        _db = db;
    }

    public Task LogOrderPlacedAsync(int orderId, string? changedByUserId = null, CancellationToken ct = default)
    {
        return LogStatusChangedAsync(
            orderId: orderId,
            fromStatus: null,
            toStatus: OrderStatusWorkflow.Pending,
            changedByUserId: changedByUserId,
            changedByDepartment: "Customer",
            ct: ct);
    }

    public async Task LogStatusChangedAsync(
        int orderId,
        string? fromStatus,
        string? toStatus,
        string? changedByUserId = null,
        string? changedByDepartment = null,
        CancellationToken ct = default)
    {
        if (orderId <= 0)
            return;

        var normalizedTo = OrderStatusWorkflow.Normalize(toStatus);
        if (string.IsNullOrWhiteSpace(normalizedTo))
            return;

        var normalizedFrom = OrderStatusWorkflow.Normalize(fromStatus);
        if (string.Equals(normalizedFrom, normalizedTo, StringComparison.OrdinalIgnoreCase))
            return;

        var lastStatus = await _db.OrderStatusLogs
            .AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .OrderByDescending(x => x.ChangedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => x.ToStatus)
            .FirstOrDefaultAsync(ct);

        if (string.Equals(OrderStatusWorkflow.Normalize(lastStatus), normalizedTo, StringComparison.OrdinalIgnoreCase))
            return;

        _db.OrderStatusLogs.Add(new OrderStatusLog
        {
            OrderId = orderId,
            FromStatus = string.IsNullOrWhiteSpace(normalizedFrom) ? null : normalizedFrom,
            ToStatus = normalizedTo,
            ChangedAt = DateTime.UtcNow,
            ChangedByUserId = string.IsNullOrWhiteSpace(changedByUserId) ? null : changedByUserId.Trim(),
            ChangedByDepartment = string.IsNullOrWhiteSpace(changedByDepartment) ? null : changedByDepartment.Trim()
        });

        await _db.SaveChangesAsync(ct);
    }
}
