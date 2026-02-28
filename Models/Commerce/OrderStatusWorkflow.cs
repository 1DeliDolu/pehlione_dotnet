namespace Pehlione.Models.Commerce;

public static class OrderStatusWorkflow
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Paid = "Paid";
    public const string Shipped = "Shipped";
    public const string Delivered = "Delivered";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string Refunded = "Refunded";

    private static readonly string[] OrderedStatuses =
    [
        Pending,
        Processing,
        Paid,
        Shipped,
        Delivered,
        Completed,
        Cancelled,
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
            return Processing;
        if (value.Equals("Gonderildi", StringComparison.OrdinalIgnoreCase))
            return Shipped;
        if (value.Equals("Delicered", StringComparison.OrdinalIgnoreCase))
            return Delivered;

        var canonical = OrderedStatuses.FirstOrDefault(s => s.Equals(value, StringComparison.OrdinalIgnoreCase));
        return canonical ?? value;
    }

    public static IReadOnlyList<string> GetNextStatuses(string? current)
    {
        var status = Normalize(current);
        return status switch
        {
            Pending => [Paid, Cancelled],
            Paid => [Processing, Refunded],
            Processing => [Shipped, Cancelled],
            Shipped => [Delivered, Refunded],
            Delivered => [Completed, Refunded],
            Completed => [Refunded],
            Cancelled => [Refunded],
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
        Shipped
    ];

    public static bool IsWarehouseActionable(string? status)
    {
        var normalized = Normalize(status);
        return WarehouseQueueStatuses.Any(s => s.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }
}
