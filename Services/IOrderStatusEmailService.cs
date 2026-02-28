using Pehlione.Models.Commerce;

namespace Pehlione.Services;

public interface IOrderStatusEmailService
{
    Task NotifyStatusChangedAsync(Order order, string oldStatus, string newStatus, CancellationToken ct = default);
}
