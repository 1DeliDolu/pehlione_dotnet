using Pehlione.Models.Commerce;

namespace Pehlione.Services;

public interface IOrderWorkflowNotificationService
{
    Task OnOrderPlacedAsync(Order order, CancellationToken ct = default);
    Task OnStatusChangedAsync(Order order, string oldStatus, string newStatus, CancellationToken ct = default);
    Task OnReturnRestockApprovedAsync(Order order, CancellationToken ct = default);
}
