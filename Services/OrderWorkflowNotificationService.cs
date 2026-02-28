using Pehlione.Models.Communication;
using Pehlione.Models.Commerce;

namespace Pehlione.Services;

public sealed class OrderWorkflowNotificationService : IOrderWorkflowNotificationService
{
    private readonly INotificationService _notificationService;

    public OrderWorkflowNotificationService(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task OnOrderPlacedAsync(Order order, CancellationToken ct = default)
    {
        await _notificationService.CreateAsync(
            department: NotificationDepartments.Accounting,
            title: "Yeni siparis geldi",
            message: $"Siparis #{order.Id} olusturuldu. Odeme onayi bekleniyor.",
            relatedEntityType: "Order",
            relatedEntityId: order.Id.ToString(),
            ct: ct);
    }

    public async Task OnStatusChangedAsync(Order order, string oldStatus, string newStatus, CancellationToken ct = default)
    {
        var oldNormalized = OrderStatusWorkflow.Normalize(oldStatus);
        var newNormalized = OrderStatusWorkflow.Normalize(newStatus);

        if (string.Equals(oldNormalized, newNormalized, StringComparison.OrdinalIgnoreCase))
            return;

        if (newNormalized.Equals(OrderStatusWorkflow.Paid, StringComparison.OrdinalIgnoreCase))
        {
            await _notificationService.CreateAsync(
                department: NotificationDepartments.Warehouse,
                title: "Odeme onaylandi",
                message: $"Siparis #{order.Id} odendi. Depo hazirlama surecini baslatabilir.",
                relatedEntityType: "Order",
                relatedEntityId: order.Id.ToString(),
                ct: ct);
            return;
        }

        if (newNormalized.Equals(OrderStatusWorkflow.Shipped, StringComparison.OrdinalIgnoreCase))
        {
            await _notificationService.CreateAsync(
                department: NotificationDepartments.Courier,
                title: "Kargoya verildi",
                message: $"Siparis #{order.Id} depodan cikti. Kurye teslim alma adimini baslatmali.",
                relatedEntityType: "Order",
                relatedEntityId: order.Id.ToString(),
                ct: ct);
            return;
        }

        if (newNormalized.Equals(OrderStatusWorkflow.Packed, StringComparison.OrdinalIgnoreCase))
        {
            await _notificationService.CreateAsync(
                department: NotificationDepartments.Courier,
                title: "Siparis hazir",
                message: $"Siparis #{order.Id} paketlendi. Kurye alimi icin hazir.",
                relatedEntityType: "Order",
                relatedEntityId: order.Id.ToString(),
                ct: ct);
            return;
        }

        if (newNormalized.Equals(OrderStatusWorkflow.Delivered, StringComparison.OrdinalIgnoreCase))
        {
            await _notificationService.CreateAsync(
                department: NotificationDepartments.Warehouse,
                title: "Siparis teslim edildi",
                message: $"Siparis #{order.Id} musteriye teslim edildi. Sevkiyat tamamlandi.",
                relatedEntityType: "Order",
                relatedEntityId: order.Id.ToString(),
                ct: ct);
            return;
        }

        if (newNormalized.Equals(OrderStatusWorkflow.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            await _notificationService.CreateAsync(
                department: NotificationDepartments.Courier,
                title: "Iptal siparis iade sureci",
                message: $"Siparis #{order.Id} iptal edildi. Musteriden geri alim surecini baslatin.",
                relatedEntityType: "Order",
                relatedEntityId: order.Id.ToString(),
                ct: ct);
            return;
        }

        if (newNormalized.Equals(OrderStatusWorkflow.ReturnPickedUp, StringComparison.OrdinalIgnoreCase))
        {
            await _notificationService.CreateAsync(
                department: NotificationDepartments.Purchasing,
                title: "Kurye iade aldi",
                message: $"Siparis #{order.Id} iadesi musteriden teslim alindi. Satin alma kabul onayi bekleniyor.",
                relatedEntityType: "Order",
                relatedEntityId: order.Id.ToString(),
                ct: ct);
            return;
        }

        if (newNormalized.Equals(OrderStatusWorkflow.ReturnDeliveredToSeller, StringComparison.OrdinalIgnoreCase))
        {
            await _notificationService.CreateAsync(
                department: NotificationDepartments.Purchasing,
                title: "Iade saticiya teslim edildi",
                message: $"Siparis #{order.Id} iadesi saticiya teslim edildi. Satin alma stok onayi yapmali.",
                relatedEntityType: "Order",
                relatedEntityId: order.Id.ToString(),
                ct: ct);
            return;
        }
    }

    public async Task OnReturnRestockApprovedAsync(Order order, CancellationToken ct = default)
    {
        await _notificationService.CreateAsync(
            department: NotificationDepartments.Accounting,
            title: "Iade stok onayi tamamlandi",
            message: $"Siparis #{order.Id} iade urunleri stoga alindi. Geri odeme islemini tamamlayin.",
            relatedEntityType: "Order",
            relatedEntityId: order.Id.ToString(),
            ct: ct);
    }
}
