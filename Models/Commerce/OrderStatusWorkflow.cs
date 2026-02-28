namespace Pehlione.Models.Commerce;

public static class OrderStatusWorkflow
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Processing = "Processing";
    public const string Packed = "Packed";
    public const string Shipped = "Shipped";
    public const string CourierPickedUp = "Courier Picked Up";
    public const string OutForDelivery = "Out for Delivery";
    public const string Delivered = "Delivered";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string ReturnPickedUp = "Return Picked Up";
    public const string ReturnDeliveredToSeller = "Return Delivered to Seller";
    public const string Refunded = "Refunded";

    private static readonly string[] OrderedStatuses =
    [
        Pending,
        Paid,
        Processing,
        Packed,
        Shipped,
        CourierPickedUp,
        OutForDelivery,
        Delivered,
        Completed,
        Cancelled,
        ReturnPickedUp,
        ReturnDeliveredToSeller,
        Refunded
    ];

    public static IReadOnlyList<string> AllStatuses => OrderedStatuses;

    public static string Normalize(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
            return Pending;

        if (value.Equals("Odendi", StringComparison.OrdinalIgnoreCase))
            return Paid;
        if (value.Equals("Process", StringComparison.OrdinalIgnoreCase))
            return Processing;
        if (value.Equals("Hazirlaniyor", StringComparison.OrdinalIgnoreCase))
            return Processing;
        if (value.Equals("Paketlendi", StringComparison.OrdinalIgnoreCase))
            return Packed;
        if (value.Equals("Gonderildi", StringComparison.OrdinalIgnoreCase))
            return Shipped;
        if (value.Equals("CourierPickedUp", StringComparison.OrdinalIgnoreCase))
            return CourierPickedUp;
        if (value.Equals("Courier Picked Up", StringComparison.OrdinalIgnoreCase))
            return CourierPickedUp;
        if (value.Equals("OutForDelivery", StringComparison.OrdinalIgnoreCase))
            return OutForDelivery;
        if (value.Equals("Out for delivery", StringComparison.OrdinalIgnoreCase))
            return OutForDelivery;
        if (value.Equals("Delicered", StringComparison.OrdinalIgnoreCase))
            return Delivered;
        if (value.Equals("Returned", StringComparison.OrdinalIgnoreCase))
            return ReturnDeliveredToSeller;
        if (value.Equals("ReturnPickedUp", StringComparison.OrdinalIgnoreCase))
            return ReturnPickedUp;
        if (value.Equals("Return Picked Up", StringComparison.OrdinalIgnoreCase))
            return ReturnPickedUp;
        if (value.Equals("ReturnDeliveredToSeller", StringComparison.OrdinalIgnoreCase))
            return ReturnDeliveredToSeller;
        if (value.Equals("Return Delivered to Seller", StringComparison.OrdinalIgnoreCase))
            return ReturnDeliveredToSeller;

        var canonical = OrderedStatuses.FirstOrDefault(s => s.Equals(value, StringComparison.OrdinalIgnoreCase));
        return canonical ?? value;
    }

    public static IReadOnlyList<string> GetNextStatuses(string? current)
    {
        var status = Normalize(current);
        return status switch
        {
            Pending => [Paid, Cancelled],
            Paid => [Processing, Cancelled, Refunded],
            Processing => [Packed, Cancelled],
            Packed => [Shipped, Cancelled],
            Shipped => [CourierPickedUp, ReturnPickedUp],
            CourierPickedUp => [OutForDelivery, ReturnPickedUp],
            OutForDelivery => [Delivered, ReturnPickedUp],
            Delivered => [Completed, ReturnPickedUp],
            Completed => [ReturnPickedUp],
            Cancelled => [Refunded],
            ReturnPickedUp => [ReturnDeliveredToSeller],
            ReturnDeliveredToSeller => [Refunded],
            _ => []
        };
    }

    public static bool CanTransition(string? current, string? next)
    {
        var normalizedCurrent = Normalize(current);
        var normalizedNext = Normalize(next);
        if (normalizedCurrent.Equals(normalizedNext, StringComparison.OrdinalIgnoreCase))
            return true;

        return GetNextStatuses(normalizedCurrent)
            .Any(s => s.Equals(normalizedNext, StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<string> WarehouseQueueStatuses =>
    [
        Paid,
        Processing,
        Packed,
        Shipped
    ];

    public static bool IsWarehouseActionable(string? status)
    {
        var normalized = Normalize(status);
        return WarehouseQueueStatuses.Any(s => s.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }
}
