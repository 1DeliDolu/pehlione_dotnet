using System.Net;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;
using Pehlione.Models.Commerce;

namespace Pehlione.Services;

public sealed class OrderStatusEmailService : IOrderStatusEmailService
{
    private readonly PehlioneDbContext _db;
    private readonly IAppEmailSender _emailSender;
    private readonly ILogger<OrderStatusEmailService> _logger;

    public OrderStatusEmailService(
        PehlioneDbContext db,
        IAppEmailSender emailSender,
        ILogger<OrderStatusEmailService> logger)
    {
        _db = db;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task NotifyStatusChangedAsync(Order order, string oldStatus, string newStatus, CancellationToken ct = default)
    {
        if (order.Id <= 0 || string.IsNullOrWhiteSpace(order.UserId))
            return;

        var oldNormalized = OrderStatusWorkflow.Normalize(oldStatus);
        var newNormalized = OrderStatusWorkflow.Normalize(newStatus);
        if (string.Equals(oldNormalized, newNormalized, StringComparison.OrdinalIgnoreCase))
            return;

        var email = await _db.Users
            .AsNoTracking()
            .Where(x => x.Id == order.UserId)
            .Select(x => x.Email)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(email))
            return;

        var subject = $"Siparis durumunuz guncellendi #{order.Id}";
        var statusLabel = GetStatusLabel(newNormalized);
        var oldLabel = GetStatusLabel(oldNormalized);
        var extra = GetExtraMessage(newNormalized, order.ShippingCarrier, order.TrackingCode);

        var body = $@"
            <h2>Siparis durum bilgilendirmesi</h2>
            <p>Siparis numaraniz: <strong>#{order.Id}</strong></p>
            <p>Onceki durum: <strong>{WebUtility.HtmlEncode(oldLabel)}</strong></p>
            <p>Yeni durum: <strong>{WebUtility.HtmlEncode(statusLabel)}</strong></p>
            {extra}
            <p>Hesabim sayfasindan siparisinizi takip edebilirsiniz.</p>
        ";

        try
        {
            await _emailSender.SendAsync(email, subject, body, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Order status email could not be sent for order {OrderId}", order.Id);
        }
    }

    private static string GetStatusLabel(string status)
    {
        return status switch
        {
            OrderStatusWorkflow.Pending => "Siparis alindi (Pending)",
            OrderStatusWorkflow.Paid => "Odeme alindi (Paid)",
            OrderStatusWorkflow.Processing => "Siparis hazirlaniyor (Processing)",
            OrderStatusWorkflow.Packed => "Siparis paketlendi (Packed)",
            OrderStatusWorkflow.Shipped => "Paket kargoya verildi (Shipped)",
            OrderStatusWorkflow.CourierPickedUp => "Kargo paketi teslim aldi (Courier Picked Up)",
            OrderStatusWorkflow.OutForDelivery => "Dagitima cikti (Out for Delivery)",
            OrderStatusWorkflow.Delivered => "Musteriye teslim edildi (Delivered)",
            OrderStatusWorkflow.Completed => "Siparis tamamlandi (Completed)",
            OrderStatusWorkflow.Cancelled => "Siparis iptal edildi (Cancelled)",
            OrderStatusWorkflow.ReturnPickedUp => "Iade kargosu teslim alindi (Return Picked Up)",
            OrderStatusWorkflow.ReturnDeliveredToSeller => "Iade saticiya teslim edildi (Return Delivered to Seller)",
            OrderStatusWorkflow.Refunded => "Ucret iade edildi (Refunded)",
            _ => status
        };
    }

    private static string GetExtraMessage(string status, string? shippingCarrier, string? trackingCode)
    {
        if (status.Equals(OrderStatusWorkflow.Shipped, StringComparison.OrdinalIgnoreCase))
        {
            var carrierText = string.IsNullOrWhiteSpace(shippingCarrier) ? "-" : WebUtility.HtmlEncode(shippingCarrier);
            var trackingText = string.IsNullOrWhiteSpace(trackingCode) ? "-" : WebUtility.HtmlEncode(trackingCode);
            return $"<p>Kargo firmasi: <strong>{carrierText}</strong><br/>Takip kodu: <strong>{trackingText}</strong></p>";
        }

        if (status.Equals(OrderStatusWorkflow.Delivered, StringComparison.OrdinalIgnoreCase))
            return "<p>Paketiniz teslim edildi. Iyi gunlerde kullanin.</p>";

        if (status.Equals(OrderStatusWorkflow.ReturnDeliveredToSeller, StringComparison.OrdinalIgnoreCase))
            return "<p>Iade urunu saticiya teslim edildi. Muhasebe iade surecini tamamlayacaktir.</p>";

        if (status.Equals(OrderStatusWorkflow.Refunded, StringComparison.OrdinalIgnoreCase))
            return "<p>Iade odemeniz baslatildi/tamamlandi.</p>";

        return string.Empty;
    }
}
